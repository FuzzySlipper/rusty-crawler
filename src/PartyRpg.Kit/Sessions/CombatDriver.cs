using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Input;
using PartyRpg.Kit.Time;
using Rusty.Engine;

namespace PartyRpg.Kit.Sessions;

/// <summary>The orders one update carries for a fight: an attack, and the two committed turns that are not one.</summary>
/// <param name="Attacked">Whether the act control ordered an attack.</param>
/// <param name="Skipped">Whether a committed turn forfeits the round's turn.</param>
/// <param name="Waited">Whether a committed turn defers to the round's end.</param>
internal readonly record struct FightOrders(bool Attacked, bool Skipped, bool Waited);

/// <summary>
/// What the act control's key means from one update to the next: a hold in real time, a press in a paced fight.
/// </summary>
/// <remarks>
/// <para>
/// In real time a held act control means "keep attacking as each member recovers", so what matters is that it
/// is down. In a paced fight it commits a turn, and turns are decisions rather than a state, so only a press
/// counts: holding the key cannot spend turn after turn.
/// </para>
/// <para>
/// A key held across a change of pacing belonged to the pacing the player was in, so the hold is dropped and the
/// control has to come up before it orders again. What comes up is the engine's own report, which is why the
/// control also has to be seen <em>down</em> after the switch before its absence means anything: the update the
/// toggle arrived in carries no attack event either, and reading that as a release would let the very next
/// update order again.
/// </para>
/// </remarks>
internal sealed class ActControl
{
    private bool _held;
    private bool _suppressed;
    private bool _seenDown;

    /// <summary>Reads the key for one update and says whether it orders anything.</summary>
    /// <param name="down">Whether the control is down this update.</param>
    /// <param name="paced">Whether the fight is paced, so only a press orders.</param>
    public bool Orders(bool down, bool paced)
    {
        bool pressed = down && !_held && !_suppressed;
        if (_suppressed)
        {
            if (down) _seenDown = true;
            else if (_seenDown)
            {
                // It came up: the next press is a decision about the pacing the player is in now.
                _suppressed = false;
                _seenDown = false;
            }
        }

        bool orders = paced ? pressed : down && !_suppressed;
        _held = down;
        return orders;
    }

    /// <summary>Drops the hold at a change of pacing: a key down now must come up before it orders again.</summary>
    public void Drop()
    {
        _suppressed = _held;
        _seenDown = false;
        _held = false;
    }
}

/// <summary>
/// Drives the fight inside the one admitted update, in either pacing, and switches the pacing on request.
/// </summary>
/// <remarks>
/// <para>
/// The fight is stepped in every update, whether or not its control was pressed, because what it reads is the
/// world: which creatures stand in the place, which of them have noticed the party, and how much recovery each
/// actor has left. The opposition acts first and the party's order last, so what a player did this update is
/// the newest fact on the panel; the field is then read once more, so a party that brings the last creature
/// down and walks out has emptied the place in this update.
/// </para>
/// <para>
/// <b>A paced fight is not a second update.</b> It runs inside the one the engine admitted, is triggered by the
/// committed action that update carried, and does nothing to the world except advance the one clock by the game
/// time each turn costs — the same clock, the same owners, and therefore the same recovery arithmetic real time
/// runs on.
/// </para>
/// </remarks>
internal sealed partial class CombatDriver(SessionOwners owners, MovementInput? movement, CombatIntentNames? names)
{
    private readonly CombatInput? _input = names is null ? null : new CombatInput(names);
    private readonly TurnInput? _turns = names?.Turn is { } turn ? new TurnInput(turn) : null;
    private readonly ActControl _act = new();

    /// <summary>The turn controls this update carried.</summary>
    public TurnControls ReadTurns(ActionInbox input) => _turns?.Read(input) ?? TurnControls.None;

    /// <summary>The orders this update carried, read whatever owns the controls so a release is never missed.</summary>
    /// <param name="input">The update's admitted input.</param>
    /// <param name="turn">The turn controls read from the same input.</param>
    /// <param name="screenOwnsControls">Whether a counter or a conversation owns the player's controls.</param>
    public FightOrders Read(ActionInbox input, TurnControls turn, bool screenOwnsControls)
    {
        SelectMembers(input);
        bool down = _input is not null && owners.Combat is not null && _input.Read(input) && !screenOwnsControls;
        bool paced = !screenOwnsControls && owners.Combat is { Pacing: CombatPacing.TurnBased };
        bool attacked = _act.Orders(down, paced);
        return paced ? new FightOrders(attacked, turn.Skip, turn.Wait) : new FightOrders(attacked, false, false);
    }

    /// <summary>Switches the pacing of the fight, and changes nothing else about it.</summary>
    /// <remarks>
    /// No party state, no world state, and no position moves, which is why a fight can be switched in the middle
    /// of a round and continued from exactly the state it was in. What the player was holding is released: a key
    /// the engine still reports as down arrives again next update, so what is dropped is the intent carried over.
    /// </remarks>
    public void TogglePacing()
    {
        if (owners.Combat is not { } combat)
        {
            owners.Diagnostics.Refused(
                "combat",
                "combat-pacing-unavailable",
                "The pacing was asked to switch and this session holds no fight to pace: its ruleset answered no combat policy, so there is nothing to play in rounds.");
            return;
        }

        CombatPacing pacing = combat.TogglePacing();
        _act.Drop();
        movement?.Release();
        _input?.Release();
        owners.Diagnostics.Applied(
            "combat",
            "combat-pacing-toggle",
            pacing == CombatPacing.TurnBased
                ? "The fight is paced turn-based: every turn of the party's now arrives as a committed action and the world waits for it."
                : "The fight is paced in real time again: recovery elapses with the world it always did.");
    }

    /// <summary>Steps the fight with this update's orders.</summary>
    /// <param name="orders">What the player ordered.</param>
    /// <param name="caster">The member whose casting this update was applied, when one was.</param>
    /// <param name="seconds">The world time this update admitted, which may be zero.</param>
    public void Step(FightOrders orders, CombatantId? caster, double seconds)
    {
        if (owners.Combat is not { } combat) return;
        combat.Step();

        // A pacing that holds nothing — nothing hostile, or a party that can no longer act — is real time,
        // because then the world proceeds exactly as it does outside the mode.
        if (combat.Pacing == CombatPacing.TurnBased && combat.Turns.IsHolding)
        {
            StepPaced(combat, orders, caster, seconds);
            return;
        }

        if (orders.Skipped || orders.Waited)
        {
            owners.Diagnostics.Refused(
                "combat",
                "no-turn",
                "A turn was passed or deferred while this fight had no turn to give: the pacing is not turn-based, nothing is being fought, or the party has nobody left who can act.");
        }

        if (owners.Director is { } director && owners.World is { } world)
        {
            director.Step(world.Place, seconds);
            if (orders.Attacked) combat.Engage();
            director.Observe(world.Place);
        }
        else if (orders.Attacked)
        {
            combat.Engage();
        }
    }

    /// <summary>
    /// The party's committed turn, then every turn that comes before the next one of the party's.
    /// </summary>
    /// <remarks>
    /// The movement phase is the party's own step: the update's admitted step already moved it, and what is
    /// spent here is the game time that step covered. Any committed turn ends the phase — the original's own
    /// controls do exactly that — and so does spending the allowance.
    /// </remarks>
    private void StepPaced(CombatState combat, FightOrders orders, CombatantId? caster, double seconds)
    {
        TurnBasedPacing turns = combat.Turns;
        bool committed = orders.Attacked || orders.Skipped || orders.Waited;

        if (turns.Phase == TurnPhase.Movement)
        {
            // A session with no clock has no scale to convert its step by and so no allowance anything could
            // spend; its phase ends with the update that entered it rather than holding a world forever.
            turns.SpendMovement(GameTime(seconds));
            if (!committed && owners.Clock is not null && !turns.MovementLeft.IsNone) return;
            turns.EndMovement();
            ResolveTurns(combat);
            return;
        }

        if (!turns.WaitsForPlayer)
        {
            ResolveTurns(combat);
            return;
        }

        if (orders.Attacked)
        {
            // A refused order leaves the turn where it was, so the player can see the answer and act again.
            if (combat.EngageSelected()?.IsApplied != true) return;
            turns.Took();
        }
        else if (caster is { } cast)
        {
            // The spell already went through the fight's own gated entry, so the turn it was taken on is spent.
            if (turns.Current is { } actor && actor.Id == cast) turns.Took();
        }
        else if (orders.Skipped)
        {
            turns.Skipped();
        }
        else if (orders.Waited)
        {
            if (!turns.Waited())
            {
                owners.Diagnostics.Refused(
                    "combat",
                    "already-waited",
                    $"{turns.Current?.Name ?? "That actor"} has already deferred its turn this round, so waiting again would hold the round open for a decision nothing has changed.");
            }
        }
        else
        {
            // Nothing was committed: the session keeps waiting, which is what makes a paced fight wait.
            return;
        }

        ResolveTurns(combat);
    }

    /// <summary>
    /// Hands out the turns before the party's next one: ask whose turn is next and how much game time passes
    /// first, advance the one clock by it, and let a creature act through the same driver real time uses.
    /// </summary>
    /// <remarks>
    /// It always terminates: every turn either advances the clock by time the round still has, or is taken by an
    /// actor that has not acted at this moment, and a moment only holds as many turns as the fight has actors.
    /// </remarks>
    private void ResolveTurns(CombatState combat)
    {
        while (true)
        {
            GameDuration interval = combat.Turns.Next();
            if (!interval.IsNone) owners.Clock?.Advance(interval);
            if (combat.Turns.Current is not { } actor) return;
            if (actor.Side == CombatSide.Party)
            {
                if (actor.Subject.Member is { } member) combat.SelectMember(member.Id);
                return;
            }
            if (owners.Director is { } director && owners.World is { } world)
            {
                director.TakeTurn(world.Place, actor.Id, AdmittedSecondsFor(interval));
            }

            combat.Turns.Took();
        }
    }

    /// <summary>The engine's own seconds for a length of game time, which is what a creature's step covers.</summary>
    private double AdmittedSecondsFor(GameDuration interval) =>
        owners.Clock is { } clock ? interval.TotalSeconds / clock.Scale.GameSecondsPerRealSecond : 0;

    /// <summary>The game time an admitted interval covers, at the one clock's own scale.</summary>
    private GameDuration GameTime(double admittedSeconds) =>
        owners.Clock is { } clock
            ? GameDuration.FromMilliseconds((long)Math.Round(
                admittedSeconds * clock.Scale.GameSecondsPerRealSecond * GameDuration.MillisecondsPerSecond,
                MidpointRounding.AwayFromZero))
            : GameDuration.None;
}
