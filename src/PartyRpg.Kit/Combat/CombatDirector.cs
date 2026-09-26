using System.Globalization;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using Rusty.Engine;

namespace PartyRpg.Kit.Combat;

/// <summary>What one creature is doing, as the panel and a report read it.</summary>
/// <remarks>
/// This is the driver's own record of what it asked for, which is the half a fight does not state: the
/// fight says an attack was attempted and what came of it, and this says that a creature was closing, that
/// it was backing away, or that it was standing its ground. A creature that is down is reported as down
/// rather than as doing anything, because a body is not acting.
/// </remarks>
/// <param name="Creature">The creature the activity is about.</param>
/// <param name="Name">What it is called, as the ruleset named it.</param>
/// <param name="Action">What it decided, as the vocabulary spells it.</param>
/// <param name="Target">What the decision was about, empty when it was about nobody.</param>
/// <param name="Applied">Whether the fight or the engine took it, false when the actor could not act.</param>
public readonly record struct CreatureActivity(
    CombatantId Creature,
    string Name,
    string Action,
    string Target,
    bool Applied);

/// <summary>
/// The driver of a fight's other half: it decides for every creature the party is fighting, and gives the
/// orders the fight then judges.
/// </summary>
/// <remarks>
/// <para>
/// <b>It drives the opposition and nothing else.</b> The party's own members are ordered by whoever holds
/// the player's control, and this never touches them: what it drives is exactly the actors whose side is
/// <see cref="CombatSide.Opposition"/>, recomputed from the world by the fight's own step. A creature that
/// has not noticed the party is in no fight and is not driven — which is why walking into a place is quiet
/// until something looks up.
/// </para>
/// <para>
/// <b>It owns no pacing of its own.</b> An attack goes through <see cref="CombatState.Order"/>, the same
/// gated entry the player's control uses, so a creature recovering and a character recovering are one
/// mechanism: an order while recovering is refused by name whatever drove it. Movement is not gated that
/// way, because it is not an action: a creature whose turn has not come still closes the distance, exactly
/// as the donor's own AI pursues while its recovery runs down.
/// </para>
/// <para>
/// <b>Spawn, despawn, and a cleared place are here, and they are place state.</b> A creature that goes down
/// leaves the field — its live position is forgotten and nothing drives it again — and a place whose
/// creatures have all gone down is marked cleared through the world's own per-place state, which is what
/// makes the emptiness last. Nothing is counted in the update: the mark is the place's, the clock restores
/// it, and the population rebuilds from content when the world says the interval has elapsed.
/// </para>
/// <para>
/// <b>Every decision is reported and none of them is random here.</b> Chance belongs to the policy and
/// goes through the engine's keyed random service; this asks, applies, and reports, so the same state
/// produces the same fight and a panel can say what the opposition did.
/// </para>
/// </remarks>
public sealed class CombatDirector
{
    private readonly CombatState _combat;
    private readonly IMonsterAiPolicy _policy;
    private readonly ICreatureMover? _mover;
    private readonly PlaceStateLedger? _places;
    private readonly IDiagnosticsService? _diagnostics;
    private readonly Dictionary<CombatantId, long> _rounds = [];
    private readonly Dictionary<CombatantId, CreatureActivity> _activity = [];
    private PlaceId? _place;

    /// <summary>Creates the driver over one fight.</summary>
    /// <param name="combat">The fight, which is what every order goes through.</param>
    /// <param name="policy">This game's answer about how each creature behaves.</param>
    /// <param name="mover">
    /// How a creature moves through the world's collision. Without one creatures decide and attack and
    /// never close: a product with no engine has nothing to move them in, and says so rather than sliding
    /// them through walls.
    /// </param>
    /// <param name="places">
    /// The world's per-place state, which is what a place the party empties is marked in. Without one a
    /// cleared place is not remembered, and the next visit finds its population exactly as content left it.
    /// </param>
    /// <param name="diagnostics">Where every decision and every refusal is reported. Optional, and nothing depends on it.</param>
    /// <exception cref="ArgumentNullException">No fight or no policy was supplied.</exception>
    public CombatDirector(
        CombatState combat,
        IMonsterAiPolicy policy,
        ICreatureMover? mover = null,
        PlaceStateLedger? places = null,
        IDiagnosticsService? diagnostics = null)
    {
        _combat = combat ?? throw new ArgumentNullException(nameof(combat));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _mover = mover;
        _places = places;
        _diagnostics = diagnostics;
    }

    /// <summary>What every creature the party is fighting is doing, in combatant order.</summary>
    public IReadOnlyList<CreatureActivity> Activity
    {
        get
        {
            List<CreatureActivity> activities = [];
            foreach (Combatant combatant in _combat.Combatants)
            {
                if (_activity.TryGetValue(combatant.Id, out CreatureActivity activity)) activities.Add(activity);
            }

            return activities;
        }
    }

    /// <summary>How many decisions the driver has made for one creature, which is what its draws are keyed on.</summary>
    /// <param name="creature">The creature to ask about.</param>
    public long Rounds(CombatantId creature) => _rounds.GetValueOrDefault(creature);

    /// <summary>
    /// Decides for every creature the party is fighting, inside the admitted update, and applies what each
    /// decided.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The fight is read first — <see cref="CombatState.Step"/> is called by whoever owns the update before
    /// this — so what is driven is the opposition as the world stands now. The admitted interval is what
    /// movement covers; an update that admits none (a held session, or a mode that steps nothing) still
    /// decides and still attacks, because an attack is an instant, and moves nobody, because movement is an
    /// interval.
    /// </para>
    /// <para>
    /// A creature that leaves the field — it went down, or the place was restored under the party and its
    /// entity is gone — is forgotten here: its live position goes with it, and the rounds it took are
    /// dropped, because a fresh population is a fresh fight. Marking the place cleared happens after the
    /// forgetting, so a place the party has just finished is marked once and stays marked until the clock
    /// restores it.
    /// </para>
    /// </remarks>
    /// <param name="place">The place the party stands in, which is where its creatures are.</param>
    /// <param name="elapsedSeconds">The world time this update admitted, which may be zero.</param>
    /// <returns>What every driven creature decided.</returns>
    public IReadOnlyList<CreatureActivity> Step(PlaceId place, double elapsedSeconds)
    {
        Align(place);

        List<CreatureActivity> decided = [];
        foreach (Combatant combatant in _combat.Combatants.Where(entry => entry.Side == CombatSide.Opposition).ToList())
        {
            CreatureActivity activity = Turn(place, combatant, elapsedSeconds);
            if (!_combat.IsDown(combatant)) decided.Add(activity);
        }

        // The place is looked at again once the driving is done, because what stands there can have changed
        // under it: a creature that was brought down earlier this update is a body by now.
        Observe(place);
        return decided;
    }

    /// <summary>
    /// Takes one creature's turn: the same decision and the same gate as <see cref="Step"/>, for one actor
    /// instead of every actor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A turn-based round hands out one turn at a time, so it needs one creature to decide and act rather
    /// than all of them every update. Everything else is deliberately the same: the ruleset's own policy
    /// decides, the order goes through <see cref="CombatState.Order"/>, movement goes through the world's own
    /// collision, and the decision is keyed by the turn this creature is taking — so a fight paced in rounds
    /// is the same fight with the same AI, not a second one.
    /// </para>
    /// <para>
    /// The admitted interval is what its movement covers: in real time that is the update the round was
    /// fought in, and in a paced fight it is the game time between this creature's turn and the last one,
    /// converted to the unit the world's own mover measures in. A turn that covers no time still lets the
    /// creature attack, because an attack is an instant, and moves it nowhere, because movement is not.
    /// </para>
    /// </remarks>
    /// <param name="place">The place the party stands in, which is where its creatures are.</param>
    /// <param name="creature">The creature whose turn it is.</param>
    /// <param name="elapsedSeconds">The world time this turn covers, which may be zero.</param>
    /// <returns>What the creature decided.</returns>
    /// <exception cref="ArgumentOutOfRangeException">No actor in the fight has that identity.</exception>
    public CreatureActivity TakeTurn(PlaceId place, CombatantId creature, double elapsedSeconds)
    {
        Align(place);

        Combatant combatant = _combat.Find(creature) ?? throw new ArgumentOutOfRangeException(
            nameof(creature),
            creature,
            "No actor in this fight has that identity, so there is no turn to take; a turn is handed to an actor the fight itself published.");

        return Turn(place, combatant, elapsedSeconds);
    }

    /// <summary>One creature's turn, for a driver that has already read the place it is standing in.</summary>
    private CreatureActivity Turn(PlaceId place, Combatant combatant, double elapsedSeconds)
    {
        if (_combat.IsDown(combatant))
        {
            // A body is not driven: it neither acts nor is forgotten, because it is still where it fell and
            // the fight still publishes it. It stops counting as an enemy by the fight's own reading.
            CreatureActivity body = new(combatant.Id, combatant.Name, "down", string.Empty, Applied: false);
            _activity[combatant.Id] = body;
            return body;
        }

        long round = _rounds.GetValueOrDefault(combatant.Id);
        _rounds[combatant.Id] = round + 1;
        CreatureActivity activity = Apply(combatant, Situation(combatant, round, place), elapsedSeconds);
        _activity[combatant.Id] = activity;
        Report(place, activity);
        return activity;
    }

    /// <summary>
    /// Forgets what belonged to a place the party has left, and drops the creatures that are gone.
    /// </summary>
    /// <remarks>
    /// Positions and counters belong to the place they were made in: a fresh place starts from content's own
    /// placements, and nothing may carry a coordinate or a creature's turn count across. A creature that is
    /// no longer fighting — it went down, or the place was restored under the party and its entity is gone —
    /// is forgotten here for the same reason, and the forgetting is what makes a fresh population a fresh
    /// fight. Both <see cref="Step"/> and <see cref="TakeTurn"/> begin here, so whichever of them the pacing
    /// uses, the driver is answering about the place the party is standing in now.
    /// </remarks>
    private void Align(PlaceId place)
    {
        if (_place is { } previous && previous != place)
        {
            // Positions belonged to the place the party has left: a fresh place starts from content's own
            // placements, and nothing may carry a coordinate across.
            _mover?.ForgetAll();
            _rounds.Clear();
            _activity.Clear();
        }

        _place = place;

        HashSet<CombatantId> live = [.. _combat.Combatants
            .Where(combatant => combatant.Side == CombatSide.Opposition)
            .Select(combatant => combatant.Id)];
        foreach (CombatantId gone in _rounds.Keys.Where(id => !live.Contains(id)).ToList())
        {
            _rounds.Remove(gone);
            _activity.Remove(gone);
            _mover?.Forget(gone);
        }
    }

    /// <summary>
    /// Marks the place cleared when nothing it holds is standing any more.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A place whose creatures have all gone down is a place the party has emptied. It is marked once,
    /// through the world's own per-place state, and stays cleared until the clock says the interval has
    /// elapsed — which is what makes a cleared place stay cleared and a restored one come back.
    /// </para>
    /// <para>
    /// <b>What counts is what fights there.</b> A creature's own nature is the ruleset's answer — the row's
    /// hostility band, in this game — so a creature that has not noticed the party yet still counts as
    /// something the party would have to deal with, and a person going about their day does not: a party that
    /// walks into a shop with a cellar full of monsters has not cleared the shop by killing the shopkeeper,
    /// and one that kills the monsters has not cleared the shopkeeper either. A creature the party provoked
    /// counts, because a fight the party started is still a fight the place was holding.
    /// </para>
    /// <para>
    /// <b>It is a reading of the field and not a step.</b> Whoever owns the update calls it after every
    /// action of the update has been applied — including the party's own order, which is applied after this
    /// driver has had its turn — so a party that brings the last creature down and walks straight out leaves
    /// the place marked rather than finding it populated again on the way back. Nothing is driven here and
    /// no decision is made: a body cannot be ordered anywhere, and marking a place is not an action.
    /// </para>
    /// </remarks>
    /// <param name="place">The place the party stands in.</param>
    public void Observe(PlaceId place)
    {
        if (_places is not { } ledger) return;

        bool held = false;
        bool standing = false;
        foreach (Combatant combatant in _combat.Combatants)
        {
            // Only the actors the place put there, and only the ones a fight is about: a member of the party
            // is not one of a place's creatures, and somebody who starts no fights is not what the party
            // cleared the place of.
            if (combatant.Subject.Member is not null) continue;
            if (combatant.Side != CombatSide.Opposition && !_combat.IsHostile(combatant)) continue;
            held = true;
            if (!_combat.IsDown(combatant)) standing = true;
        }

        if (!held || standing || ledger.StateOf(place).Cleared) return;
        Report(place, ledger.MarkCleared(place));
    }

    /// <summary>Applies one creature's decision: an order through the fight, or a step through the world.</summary>
    /// <remarks>
    /// A decision to attack while the creature cannot act is applied as closing the distance, which is the
    /// donor's own answer for an actor whose recovery has not elapsed: it pursues, and it swings when its
    /// turn comes. An attack the fight refuses for any other reason is reported and not retried here —
    /// retrying would hide the refusal, and the refusal is the fact.
    /// </remarks>
    private CreatureActivity Apply(Combatant creature, CreatureSituation situation, double elapsedSeconds)
    {
        CreatureDecision decision = _policy.Decide(situation);
        if (decision.Target is not { } target)
        {
            return new CreatureActivity(creature.Id, creature.Name, "waiting", string.Empty, Applied: false);
        }

        string targetName = _combat.Find(target)?.Name ?? string.Empty;
        if (decision.Action == CreatureAction.Attack && creature.IsReady)
        {
            CombatResult result = _combat.Order(new AttackOrder(creature.Id, decision.Kind, target, decision.Ability));
            return new CreatureActivity(
                creature.Id,
                creature.Name,
                result.IsApplied ? "attacking" : "refused",
                targetName,
                result.IsApplied);
        }

        if (decision.Action is CreatureAction.Wait)
        {
            return new CreatureActivity(creature.Id, creature.Name, "holding", targetName, Applied: false);
        }

        CreatureMovePurpose purpose = decision.Action == CreatureAction.Retreat
            ? CreatureMovePurpose.Away
            : CreatureMovePurpose.Toward;
        if (elapsedSeconds <= 0 || _mover is not { } mover)
        {
            return new CreatureActivity(
                creature.Id,
                creature.Name,
                purpose == CreatureMovePurpose.Away ? "backing away" : "closing",
                targetName,
                Applied: false);
        }

        if (_combat.Find(target) is not { } about)
        {
            return new CreatureActivity(creature.Id, creature.Name, "waiting", string.Empty, Applied: false);
        }

        PlacePose from = mover.PoseOf(creature.Id) ?? creature.Subject.Pose;
        PlacePose to = mover.PoseOf(target) ?? about.Subject.Pose;
        CreatureMoveOutcome outcome = mover.Move(new CreatureMoveRequest(
            creature.Id,
            from,
            target,
            to,
            purpose,
            _policy.SpeedOf(creature.Subject),
            elapsedSeconds));

        return new CreatureActivity(
            creature.Id,
            creature.Name,
            purpose == CreatureMovePurpose.Away ? "backing away" : "closing",
            targetName,
            outcome.Moved);
    }

    /// <summary>Builds what one creature is told about its own moment.</summary>
    /// <remarks>
    /// Every other actor in the fight is a candidate, and the only thing the kit decides about one is
    /// whether the policy calls it an enemy — which it asks, once per candidate, so the answer a policy
    /// reads is the answer it gave. The party's own members are candidates whether or not the creature is
    /// hostile to them, because a creature that has not noticed the party is not in the fight at all.
    /// </remarks>
    private CreatureSituation Situation(Combatant creature, long round, PlaceId place)
    {
        PlacePose self = Where(creature);
        List<CreatureCandidate> candidates = [];
        foreach (Combatant other in _combat.Combatants)
        {
            if (ReferenceEquals(other, creature)) continue;
            bool party = other.Side == CombatSide.Party;
            candidates.Add(new CreatureCandidate(
                other,
                party || _policy.AreEnemies(creature.Subject, other.Subject),
                party,
                Distance(self, Where(other))));
        }

        (int current, int maximum) = _combat.Vitals(creature);
        return new CreatureSituation(
            creature,
            current,
            maximum,
            candidates,
            round,
            _combat.PartyPose);
    }

    /// <summary>
    /// Where an actor of the fight stands, which is where the movement has put it rather than where content
    /// placed it.
    /// </summary>
    /// <remarks>
    /// The party's own pose is the party's; a creature's is the live position whoever moved it owns, and the
    /// placement only when nothing has. A world with no mover leaves every creature where content put it,
    /// which is the honest reading of a world nothing moves in.
    /// </remarks>
    private PlacePose Where(Combatant combatant) => combatant.Side == CombatSide.Party
        ? _combat.PartyPose
        : _mover?.PoseOf(combatant.Id) ?? combatant.Subject.Pose;

    /// <summary>How far apart two positions are, in the place's own units.</summary>
    private static double Distance(PlacePose from, PlacePose to)
    {
        double x = to.X - from.X;
        double y = to.Y - from.Y;
        double z = to.Z - from.Z;
        return Math.Sqrt((x * x) + (y * y) + (z * z));
    }

    /// <summary>Reports one creature's decision, with the place it happened in.</summary>
    private void Report(PlaceId place, CreatureActivity activity)
    {
        _diagnostics?.Publish(new DiagnosticsPublishRequest(
            DiagnosticsSeverity.Info,
            DiagnosticsDisposition.Accepted,
            Source: "combat-ai",
            Code: activity.Applied ? "creature-acted" : "creature-decided",
            Message: activity.Target.Length > 0
                ? string.Create(
                    CultureInfo.InvariantCulture,
                    $"In place '{place}', {activity.Name} is {activity.Action} {activity.Target}.")
                : string.Create(
                    CultureInfo.InvariantCulture,
                    $"In place '{place}', {activity.Name} is {activity.Action}."),
            Correlation: string.Empty));
    }

    /// <summary>Reports that a place the party stands in has been emptied.</summary>
    private void Report(PlaceId place, PlaceState state)
    {
        _diagnostics?.Publish(new DiagnosticsPublishRequest(
            DiagnosticsSeverity.Info,
            DiagnosticsDisposition.Accepted,
            Source: "combat-ai",
            Code: "place-cleared",
            Message: string.Create(
                CultureInfo.InvariantCulture,
                $"The party has brought down everything standing in place '{place}', which now holds nobody until the world restores it."),
            Correlation: string.Empty));
    }
}
