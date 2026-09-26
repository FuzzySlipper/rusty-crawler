using PartyRpg.Kit.Time;

namespace PartyRpg.Kit.Combat;

/// <summary>
/// The turn-based pacing of one fight: the order its actors act in, the round they act in, and the two
/// phases a round has.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is a reading of the fight, not a second one.</b> There is no queue with state of its own, no
/// initiative roll, and no second copy of who is fighting: every question this type answers is asked of the
/// same <see cref="CombatState"/> the real-time pacing uses, and the quantity that orders the turns is that
/// fight's own <see cref="Combatant.Recovery"/>. An actor that acted more recently, or whose ruleset states
/// a shorter action, simply has less recovery left, so it comes up again sooner — which is how a fast actor
/// acts more than once in a round without anything here treating it specially.
/// </para>
/// <para>
/// <b>The order</b> is ascending remaining recovery, and "ready" is exactly zero, because recovery is a
/// debt that was fully paid. Actors tied on recovery keep the fight's own combatant order — the party's
/// members in roster order, then the place's creatures in the order content declared them — which is the
/// donor's own tie-break read one layer out: its queue sorts ascending initiative and, on a tie, prefers a
/// character to an actor and then the lower identity (<c>src/Engine/TurnEngine/TurnEngine.cpp:53-72</c> in the donor's own turn engine). Nothing here draws, so the same fight states the same
/// order twice.
/// </para>
/// <para>
/// <b>A round</b> is a length of game time, exactly as the donor's action phase is a fixed budget it ticks
/// down (<c>turn_initiative = 100</c> and <c>StepTurnQueue</c> decrementing every actor's initiative,
/// the donor's own turn engine, <c>src/Engine/TurnEngine/TurnEngine.cpp:120-125</c> and <c>:443-470</c>; the manual calls the
/// result "rounds of a few seconds", the manual's own account of the round, p.34). This build states
/// that length in the only quantity the fight has: the longest recovery any actor in the fight owes for one
/// action. So a round lasts long enough for the slowest actor to act once, and an actor whose action costs a
/// quarter of that acts four times inside it — the fast-actor behaviour the manual describes, as a
/// consequence of the arithmetic rather than a special case.
/// </para>
/// <para>
/// <b>A round is two phases.</b> In the action phase each actor whose recovery has elapsed takes its turn,
/// in order, and time moves to whichever actor is due next. When nothing is due before the round's end the
/// party gets its movement phase, in which the party may walk a short distance while nothing else acts and
/// the round's own time is what pays for the walking; the phase ends when that allowance is spent or the
/// player commits past it, and the next round begins. The donor's engine has the same two stages
/// (<c>TE_ATTACK</c> then <c>TE_MOVEMENT</c>, <c>src/Engine/TurnEngine/TurnEngineEnums.h:26-29</c>).
/// </para>
/// <para>
/// <b>It owns no time.</b> Nothing here reads a clock or advances one: the pacing says how much game time
/// passes before the turn it hands out, and whoever owns the session's one admitted update advances that
/// clock by it, so recovery is released by the same clock in both pacings and a held session releases
/// nobody. The same is true of the opposition: the pacing says whose turn it is, and the fight's own driver
/// takes it through the same gate a player's order goes through.
/// </para>
/// </remarks>
public sealed class TurnBasedPacing
{
    private readonly CombatState _combat;
    private readonly HashSet<CombatantId> _acted = [];
    private readonly HashSet<CombatantId> _skipped = [];
    private readonly HashSet<CombatantId> _waiting = [];
    private readonly HashSet<CombatantId> _waited = [];
    private Combatant? _current;
    private GameDuration _due;
    private GameDuration _elapsed;
    private GameDuration _length;
    private GameDuration _movement;

    /// <summary>Creates the pacing of one fight, which the fight itself holds.</summary>
    /// <param name="combat">The fight this pacing reads.</param>
    internal TurnBasedPacing(CombatState combat) => _combat = combat;

    /// <summary>Which round is being fought, counting from one, or zero when no round is under way.</summary>
    public int Round { get; private set; }

    /// <summary>Which part of the round the fight is in.</summary>
    public TurnPhase Phase { get; private set; }

    /// <summary>How much game time of this round's action phase has passed.</summary>
    public GameDuration Elapsed => _elapsed;

    /// <summary>
    /// How long this round's action phase is: the longest recovery any actor in the fight owes for one
    /// action, so the slowest actor acts once and everyone faster acts again inside the same round.
    /// </summary>
    public GameDuration Length => _length;

    /// <summary>What is left of the party's movement phase, in game time.</summary>
    public GameDuration MovementLeft => _movement;

    /// <summary>Whose turn it is, or null when no turn has been handed out.</summary>
    public Combatant? Current => _current;

    /// <summary>How much game time passes before the current turn, zero when it is due now.</summary>
    public GameDuration Due => _due;

    /// <summary>What the party's last committed turn did, or null before the party has taken one.</summary>
    /// <remarks>
    /// It is the party's own turns that are recorded, because the party's turns are the ones a player decides:
    /// a creature's turn is the fight's business and is reported where every order is, so a panel that showed
    /// the last turn of any actor's would replace the skip a player just chose with the creature that acted
    /// after it — in the same update.
    /// </remarks>
    public TurnAction? Last { get; private set; }

    /// <summary>Whether a turn-based round is under way, which is what holds the field.</summary>
    /// <remarks>
    /// A pacing that is switched on with nothing to fight holds nothing: there is no round, the world steps
    /// as it does in real time, and the round begins by itself when something becomes hostile. A party that
    /// can no longer act is the same case from the other side — nobody has a turn to commit, so the fight is
    /// paced as it is outside the mode rather than waiting forever for an action nobody can give.
    /// </remarks>
    public bool IsHolding => Phase != TurnPhase.None;

    /// <summary>Whether the session is waiting for the player's committed action.</summary>
    /// <remarks>
    /// This is the fact the session's own mode is resolved from: while it holds, the session steps no world
    /// and advances no clock, exactly as creation and a counter's screen own the update, and every turn
    /// arrives as a committed action inside the update that carries it.
    /// </remarks>
    public bool WaitsForPlayer =>
        Phase == TurnPhase.Action && _current is { } actor && actor.Side == CombatSide.Party && !_combat.IsDown(actor);

    /// <summary>
    /// The fight's actors in the order they act, ascending remaining recovery, with the fight's own order
    /// breaking ties.
    /// </summary>
    /// <remarks>
    /// It is recomputed from the fight every time it is read rather than kept: an actor's place in it is its
    /// recovery at this moment, so a panel that read the order cannot show one the fight has moved past. Only
    /// the actors in the fight are listed — the party's members and the actors fighting it — because a
    /// neutral creature standing in the room has no turn to take and is not what either pacing is ordering.
    /// </remarks>
    public IReadOnlyList<TurnOrderEntry> Order
    {
        get
        {
            List<TurnOrderEntry> order = [];
            foreach (Combatant combatant in _combat.Combatants)
            {
                if (combatant.Side == CombatSide.Neutral) continue;
                order.Add(new TurnOrderEntry(
                    combatant.Id,
                    combatant.Name,
                    combatant.Side,
                    combatant.Recovery,
                    combatant.IsReady,
                    !_combat.IsDown(combatant),
                    _waiting.Contains(combatant.Id),
                    ReferenceEquals(combatant, _current)));
            }

            // Ordered by a stable sort, so actors tied on recovery keep the fight's own order rather than
            // whichever the sort happened to leave first.
            return [.. order.OrderBy(entry => entry.Remaining.Milliseconds)];
        }
    }

    /// <summary>
    /// Hands out the next turn, and says how much game time passes before it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The caller advances the session's one clock by the returned length and then reads
    /// <see cref="Current"/>: an actor that is due now is one whose recovery the advance has just released
    /// to exactly zero, which is the same arithmetic real time uses and the reason this needs no clock of
    /// its own. A length of none means the turn is due immediately, which is what a row of actors whose
    /// recovery has all elapsed look like.
    /// </para>
    /// <para>
    /// When nothing is due before the round's end, the rest of the round's time is returned and the phase
    /// becomes the party's movement phase; <see cref="Current"/> is null then, so a caller that loops on this
    /// method must treat a returned length with no current actor as the phase change it is.
    /// </para>
    /// <para>
    /// An actor that cannot act is skipped over here rather than handed a turn it could not use, and so is
    /// one that has forfeited the round or already acted at this moment: an actor acts once at a moment, and
    /// acting again needs time to pass.
    /// </para>
    /// </remarks>
    /// <returns>The game time that passes before the next turn, or none when nothing is due.</returns>
    public GameDuration Next()
    {
        if (Phase != TurnPhase.Action) return GameDuration.None;

        // A turn already handed out has already had its time accounted for: handing it out twice would move
        // the clock twice for one turn.
        if (_current is not null) return GameDuration.None;

        GameDuration left = Left;
        Combatant? next = null;
        GameDuration due = GameDuration.None;
        foreach (Combatant candidate in _combat.Combatants)
        {
            if (candidate.Side == CombatSide.Neutral) continue;
            if (_combat.IsDown(candidate)) continue;
            if (_skipped.Contains(candidate.Id)) continue;

            // An actor that acted at this very moment and owes nothing for it has nothing left to give until
            // time passes. One that acted and owes a recovery is a candidate again the moment its own debt
            // says so, which is how a fast actor's next turn cannot be lost behind a slow one's.
            if (candidate.Recovery.IsNone && _acted.Contains(candidate.Id)) continue;

            // A waiting actor's turn is the round's end: it is offered after everyone still due, and after
            // everyone due at the same instant.
            bool waiting = _waiting.Contains(candidate.Id);
            GameDuration wants = waiting ? left : candidate.Recovery;
            if (wants.Milliseconds > left.Milliseconds) continue;
            bool better = next is null
                || wants.Milliseconds < due.Milliseconds
                || (wants.Milliseconds == due.Milliseconds && !waiting && _waiting.Contains(next.Id));
            if (better)
            {
                next = candidate;
                due = wants;
            }
        }

        if (next is null)
        {
            // What is left of the round passes whether or not anybody acts in it, and the party's movement
            // phase is what comes next.
            GameDuration remainder = left;
            _elapsed = _length;
            Phase = TurnPhase.Movement;
            _movement = PartyStep();
            return remainder;
        }

        _current = next;
        _due = due;
        _elapsed += due;

        // Time passing puts everyone back in the running, including an actor that acted at the previous
        // moment; the current actor is added again when it takes its turn.
        if (!due.IsNone) _acted.Clear();
        return due;
    }

    /// <summary>Takes the current turn: the actor has acted, and owes the recovery its action cost it.</summary>
    /// <remarks>
    /// The recovery itself is spent by the fight — through <see cref="CombatState.Order"/>, which is the one
    /// entry an attack goes through in either pacing — so nothing here decides what an action costs.
    /// </remarks>
    public void Took()
    {
        if (_current is not { } actor) return;
        _acted.Add(actor.Id);
        _waiting.Remove(actor.Id);
        if (actor.Side == CombatSide.Party) Last = TurnAction.Act;
        Release();
    }

    /// <summary>Passes the current turn: the actor forfeits the round and owes the action it did not take.</summary>
    /// <remarks>
    /// What a passed turn costs is the donor's own answer (    /// <c>src/Engine/TurnEngine/TurnEngine.cpp:322-350</c>): the actor is charged its attack recovery and
    /// then the queue moves on to whoever is due next, so skipping is a decision about this round rather than
    /// a way to act sooner. The actor takes no further turn this round.
    /// </remarks>
    public void Skipped()
    {
        if (_current is not { } actor) return;
        _acted.Add(actor.Id);
        _skipped.Add(actor.Id);
        _waiting.Remove(actor.Id);
        _combat.ChargeTurn(actor);
        if (actor.Side == CombatSide.Party) Last = TurnAction.Skip;
        Release();
    }

    /// <summary>Defers the current turn to the end of the round, which costs the actor nothing.</summary>
    /// <remarks>
    /// A waiting actor is offered its turn again after every actor still due in this round — and after every
    /// actor due at the very instant the round ends, because a deferral is a decision to go last rather than a
    /// second initiative. An actor may defer once in a round: a second wait would be the same decision made
    /// again with nothing between them, and it is refused so that a round cannot be held open by waiting
    /// forever.
    /// </remarks>
    /// <returns>Whether the turn was deferred, false when the actor had already waited this round.</returns>
    public bool Waited()
    {
        if (_current is not { } actor) return false;
        if (!_waited.Add(actor.Id)) return false;
        _waiting.Add(actor.Id);
        if (actor.Side == CombatSide.Party) Last = TurnAction.Wait;
        Release();
        return true;
    }

    /// <summary>Spends part of the party's movement phase, by the game time a step of it covered.</summary>
    /// <param name="elapsed">The game time the party's step took.</param>
    public void SpendMovement(GameDuration elapsed)
    {
        if (Phase != TurnPhase.Movement || elapsed.IsNone) return;
        _movement = elapsed.Milliseconds >= _movement.Milliseconds
            ? GameDuration.None
            : GameDuration.FromMilliseconds(_movement.Milliseconds - elapsed.Milliseconds);
    }

    /// <summary>Ends the movement phase and begins the next round.</summary>
    /// <remarks>
    /// The round begins with the actors' recovery exactly as the fight holds it, so a round boundary is not a
    /// reset: an actor that acted late in the last round owes most of its recovery in the new one, and the
    /// order of the new round is read from that.
    /// </remarks>
    /// <returns>Whether a movement phase was ended, false when the fight was not in one.</returns>
    public bool EndMovement()
    {
        if (Phase != TurnPhase.Movement) return false;
        Begin(Round + 1);
        return true;
    }

    /// <summary>Begins pacing the fight, which is what switching the pacing on does.</summary>
    internal void Enter()
    {
        Reset();
        Reconcile();
    }

    /// <summary>Stops pacing it, which is what switching the pacing off does. The fight itself is untouched.</summary>
    internal void Leave() => Reset();

    /// <summary>
    /// Reads the fight again: a round begins when a fight the party can take turns in begins, and the pacing
    /// lets go when the fight ends or when nobody in the party can act any more.
    /// </summary>
    /// <remarks>
    /// This is called by the fight's own step, so what the pacing holds is always the fight as the world
    /// stands now: a party that walked out of the place, a place the world restored, and a party that has all
    /// gone down are three ways for there to be nothing to pace, and each of them ends the hold rather than
    /// leaving a round waiting for a turn nobody can take.
    /// </remarks>
    internal void Reconcile()
    {
        if (_combat.Pacing != CombatPacing.TurnBased || !_combat.IsEngaged || !AnyPartyActorCanAct())
        {
            Reset();
            return;
        }

        if (Phase == TurnPhase.None) Begin(1);

        // An actor the world took away — the party left the place, or the place was restored — or one the
        // fight has laid out cannot hold the turn it was given.
        if (_current is { } current && !Held(current))
        {
            _current = null;
            _due = GameDuration.None;
        }
    }

    /// <summary>The game time left in this round's action phase, which is never negative.</summary>
    private GameDuration Left => _length.Milliseconds > _elapsed.Milliseconds
        ? GameDuration.FromMilliseconds(_length.Milliseconds - _elapsed.Milliseconds)
        : GameDuration.None;

    /// <summary>Whether an actor is still in the fight and still able to act.</summary>
    private bool Held(Combatant combatant) =>
        _combat.Combatants.Contains(combatant) && !_combat.IsDown(combatant);

    /// <summary>Whether any of the party's members can still take a turn.</summary>
    private bool AnyPartyActorCanAct()
    {
        foreach (Combatant combatant in _combat.Combatants)
        {
            if (combatant.Side != CombatSide.Party) continue;
            if (!_combat.IsDown(combatant)) return true;
        }

        return false;
    }

    /// <summary>Begins a round, reading its length and its movement allowance from the fight as it stands.</summary>
    private void Begin(int round)
    {
        Round = round;
        Phase = TurnPhase.Action;
        _current = null;
        _due = GameDuration.None;
        _elapsed = GameDuration.None;
        _acted.Clear();
        _skipped.Clear();
        _waiting.Clear();
        _waited.Clear();
        _length = Longest(action => action.Side != CombatSide.Neutral);
        _movement = Longest(action => action.Side == CombatSide.Party);
    }

    /// <summary>
    /// How long the party may walk in its movement phase: the longest recovery its own members owe for one
    /// action.
    /// </summary>
    /// <remarks>
    /// The movement phase is the party's own step and is priced by the party's own quantity, which is what
    /// makes it a short distance in the same units the round is measured in rather than a second kind of
    /// time: the party walks for as much game time as its slowest member's action takes, and the world's own
    /// admitted step is what moves it while that lasts.
    /// </remarks>
    private GameDuration PartyStep() => Longest(action => action.Side == CombatSide.Party);

    /// <summary>How long the slowest actor a predicate accepts owes for one action of its own kind.</summary>
    /// <remarks>
    /// The ruleset's answer is a total length per actor and kind, which is what makes this a length of the
    /// round rather than a running total: it is the same value at every moment of the fight, so the round's
    /// own arithmetic does not drift as actors act.
    /// </remarks>
    private GameDuration Longest(Func<Combatant, bool> accept)
    {
        GameDuration longest = GameDuration.None;
        foreach (Combatant combatant in _combat.Combatants)
        {
            if (!accept(combatant)) continue;
            GameDuration recovery = _combat.RecoveryOf(combatant);
            if (recovery.Milliseconds > longest.Milliseconds) longest = recovery;
        }

        return longest;
    }

    /// <summary>Clears the turn that was handed out, which every committed action does.</summary>
    private void Release()
    {
        _current = null;
        _due = GameDuration.None;
    }

    /// <summary>Puts the pacing back to holding nothing, which touches no part of the fight.</summary>
    private void Reset()
    {
        Round = 0;
        Phase = TurnPhase.None;
        _current = null;
        _due = GameDuration.None;
        _elapsed = GameDuration.None;
        _length = GameDuration.None;
        _movement = GameDuration.None;
        _acted.Clear();
        _skipped.Clear();
        _waiting.Clear();
        _waited.Clear();
    }
}
