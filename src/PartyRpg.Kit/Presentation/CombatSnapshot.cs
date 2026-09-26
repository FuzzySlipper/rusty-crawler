using PartyRpg.Kit.Combat;

namespace PartyRpg.Kit.Presentation;

/// <summary>
/// One actor in a fight, as the panel needs it: who it is, whether it may act now, and how much game time it
/// still owes.
/// </summary>
/// <remarks>
/// <para>
/// These are the fight's own facts copied into one presentation value, never a second opinion about them.
/// The recovery is published as the mechanism holds it — a length of game time — and not as a countdown the
/// screen runs: a panel that ticked a bar down for itself would show a character ready before the product
/// says so, and the two would disagree the moment the session was held or the browser tab was throttled.
/// </para>
/// <para>
/// <see cref="Ready"/> and <see cref="RecoverySeconds"/> are two readings of one actor, published together
/// because a player needs both: whether the ready light is on, and how much game time is still owed. The
/// light is off for two different reasons and both are the actor's own state — it still owes recovery, or it
/// is out of the fight — so <see cref="Ready"/> is false for an actor that is recovering and for one that is
/// down, and <see cref="Down"/> is what tells the two apart.
/// </para>
/// </remarks>
/// <param name="Id">The combatant's identity, as the fight names it.</param>
/// <param name="Name">What the actor is called, as the ruleset named it.</param>
/// <param name="Ready">
/// Whether the actor may act now: its recovery has elapsed and nothing has laid it out. This is the ready
/// light the panel shows, and the flag a control that offers an act follows.
/// </param>
/// <param name="RecoverySeconds">How much game time it must still recover, zero when it is ready.</param>
/// <param name="Distance">How far it stands from the party, in the place's own units.</param>
/// <param name="HitPoints">What the actor has left to lose, read from wherever that is owned.</param>
/// <param name="HitPointsMax">What it can take altogether, which is the measure the first number needs.</param>
/// <param name="Conditions">What is acting on the actor, in the words the party already carries, empty when nothing is.</param>
/// <param name="Down">Whether the actor is out of the fight: laid out by what is on it, or taken down by harm.</param>
/// <param name="Activity">
/// What the actor is doing, for an actor something drives: <c>closing</c>, <c>backing away</c>,
/// <c>holding</c>, <c>attacking</c>, or <c>down</c>. Empty for the party's own members, whose doing is the
/// player's and is published as the last order instead.
/// </param>
public readonly record struct CombatActorSnapshot(
    string Id,
    string Name,
    bool Ready,
    double RecoverySeconds,
    double Distance,
    int HitPoints = 0,
    int HitPointsMax = 0,
    string Conditions = "",
    bool Down = false,
    string Activity = "");

/// <summary>
/// What the fight is, who is in it, and what the party's last order did.
/// </summary>
/// <remarks>
/// <para>
/// The block is published in every mode, for the same reason the rest and conversation blocks are: "this
/// session holds no fight mechanism", "nothing is hostile", and "the party is fighting and three of its
/// members are recovering" are four different facts, and a block that only appeared once something was
/// hostile would leave a player unable to tell a quiet street from a fight they cannot see.
/// </para>
/// <para>
/// <see cref="Engaged"/> is read from the fight's own state every time the projection is built rather than
/// remembered: a creature that notices the party as it walks closer turns the panel into a fight in the same
/// update the world moved.
/// </para>
/// <para>
/// <see cref="None"/> is what a session whose ruleset answered no combat policy publishes, so a panel says
/// the mechanism is not there rather than showing a fight that never happened.
/// </para>
/// </remarks>
/// <param name="Available">Whether the session holds a fight mechanism at all.</param>
/// <param name="Engaged">Whether anything is fighting the party right now.</param>
/// <param name="Opposition">How many actors are fighting the party.</param>
/// <param name="Ready">
/// How many of the party's members may act now. This is the count the panel's own line reads — "two of four
/// ready" — so it counts the members a player could order, not the members whose recovery happens to have
/// elapsed while they lie unconscious or dead.
/// </param>
/// <param name="Members">The party's members, in roster order, each with its readiness.</param>
/// <param name="Enemies">The actors fighting the party, in combatant order.</param>
/// <param name="Actor">Who attacked last, empty before the party has attacked.</param>
/// <param name="Kind">How the last attack was made, as the wire spells it; empty before any.</param>
/// <param name="Target">What the last attack was aimed at, empty when it was aimed at nothing.</param>
/// <param name="Outcome">What the last order did: <c>none</c>, <c>applied</c>, or <c>refused</c>.</param>
/// <param name="Code">The last refusal's code, empty when the last order applied or none has been given.</param>
/// <param name="Message">What the last order reported, empty before the party has attacked.</param>
/// <param name="RecoverySeconds">What the last attack cost in game time, zero for a refusal.</param>
/// <param name="Resolved">Whether the last attack was resolved at all, which a fight with no resolution rule never does.</param>
/// <param name="Hit">Whether the last resolved attack landed, false when it missed or nothing was resolved.</param>
/// <param name="Chance">How likely the ruleset said it was to land, in ten-thousandths.</param>
/// <param name="DamageRolled">What the last attack's damage dice rolled, before resistance.</param>
/// <param name="Damage">How much harm the last attack left.</param>
/// <param name="DamageKind">What kind of harm it did, as the ruleset names it, empty when nothing was resolved.</param>
/// <param name="Resistance">What the target resisted of that kind: <c>immune</c>, a weight, or empty when nothing was resolved.</param>
/// <param name="Condition">What the last hit left on its target besides harm, empty when it left nothing.</param>
/// <param name="TargetDown">Whether the last hit is what took its target down.</param>
/// <param name="ByParty">
/// Whether the last order was the party's own rather than a creature's. A fight is two-sided once
/// something drives the opposition, so the last thing that happened may be a blow the party took; a panel
/// that could not tell the two apart would show a wound with no author.
/// </param>
public readonly record struct CombatSnapshot(
    bool Available,
    bool Engaged,
    int Opposition,
    int Ready,
    IReadOnlyList<CombatActorSnapshot> Members,
    IReadOnlyList<CombatActorSnapshot> Enemies,
    string Actor,
    string Kind,
    string Target,
    string Outcome,
    string Code,
    string Message,
    double RecoverySeconds,
    bool Resolved = false,
    bool Hit = false,
    int Chance = 0,
    int DamageRolled = 0,
    int Damage = 0,
    string DamageKind = "",
    string Resistance = "",
    string Condition = "",
    bool TargetDown = false,
    bool ByParty = false,
    CombatPacing Pacing = CombatPacing.RealTime,
    CombatTurnSnapshot? Turn = null)
{
    /// <summary>No fight mechanism: nothing can be ordered and nothing is hostile.</summary>
    public static CombatSnapshot None => new(
        Available: false,
        Engaged: false,
        Opposition: 0,
        Ready: 0,
        Members: [],
        Enemies: [],
        Actor: string.Empty,
        Kind: string.Empty,
        Target: string.Empty,
        Outcome: "none",
        Code: string.Empty,
        Message: string.Empty,
        RecoverySeconds: 0);

    /// <summary>Reads the fight's facts out of the session's mechanism.</summary>
    /// <param name="combat">The session's fight, or null when it holds none.</param>
    /// <param name="director">
    /// The driver of the opposition, when the session has one: what each creature is doing is its own fact
    /// and no part of the fight, so a session that drives nothing publishes no activity rather than
    /// inventing some.
    /// </param>
    /// <returns>The facts the panel shows, or <see cref="None"/> when there is no mechanism.</returns>
    public static CombatSnapshot From(CombatState? combat, CombatDirector? director = null)
    {
        if (combat is null) return None;
        Dictionary<CombatantId, string> activity = [];
        foreach (CreatureActivity doing in director?.Activity ?? [])
        {
            activity[doing.Creature] = doing.Action;
        }

        List<CombatActorSnapshot> members = [];
        List<CombatActorSnapshot> enemies = [];
        int ready = 0;
        foreach (Combatant combatant in combat.Combatants)
        {
            // What an actor has left to lose and what is acting on it are read from wherever they are owned:
            // a member's pool and conditions are the party's own, and a world actor's harm is what the fight
            // has done to it. Nothing is recomputed here, so the panel cannot disagree with the fight.
            (int hitPoints, int hitPointsMax) = combat.Vitals(combatant);
            string conditions = combatant.Subject.Member is { } carried && carried.Conditions.Count > 0
                ? string.Join(", ", carried.Conditions.Active)
                : string.Empty;
            // The ready light is one fact with two ways to be off, and both are the fight's own answers: an
            // actor still owes recovery, or what is acting on it leaves it unable to act. Publishing the
            // recovery alone would light a laid-out character up the moment its debt elapsed — and a panel
            // whose control follows this flag would then offer an act the fight refuses by name.
            bool down = combat.IsDown(combatant);
            CombatActorSnapshot actor = new(
                combatant.Id.ToString(),
                combatant.Name,
                combatant.IsReady && !down,
                combatant.Recovery.TotalSeconds,
                combatant.Distance,
                hitPoints,
                hitPointsMax,
                conditions,
                down,
                activity.GetValueOrDefault(combatant.Id, string.Empty));
            if (combatant.Side != CombatSide.Party)
            {
                // Only the actors actually fighting are published as enemies: a creature that has not
                // noticed the party is in no fight, and listing it would make a quiet street read as one.
                if (combatant.Side == CombatSide.Opposition) enemies.Add(actor);
                continue;
            }

            if (actor.Ready) ready++;
            members.Add(actor);
        }

        // The last answer and the last attack are read together: a refusal attacked nothing, so the kind,
        // the target, and the recovery that cost are the applied attack's or nothing at all, and a reader
        // never sees a refusal beside the shape of an earlier swing.
        AttackInitiation? last = combat.LastAttack;
        CombatResult? order = combat.LastOrder;
        AttackInitiation? initiation = order is null || order.IsApplied ? last : null;
        CombatResolution? resolution = order is null || order.IsApplied ? order?.Resolution : null;
        return new CombatSnapshot(
            Available: true,
            Engaged: combat.IsEngaged,
            // What is still fighting, which is not the same as how many rows the fight publishes: an actor
            // that is down stands in the list so its death is visible, and does not count as an enemy.
            Opposition: combat.Opposition.Count,
            Ready: ready,
            Members: members,
            Enemies: enemies,
            Actor: order?.ActorName ?? string.Empty,
            Kind: initiation is { } applied ? AttackKinds.WireName(applied.Kind) : string.Empty,
            Target: initiation?.TargetName ?? string.Empty,
            Outcome: order is null ? "none" : order.IsApplied ? "applied" : "refused",
            Code: order?.Code ?? string.Empty,
            Message: order?.Message ?? string.Empty,
            RecoverySeconds: initiation?.Recovery.TotalSeconds ?? 0,
            Resolved: resolution is not null,
            Hit: resolution?.Hit ?? false,
            Chance: resolution?.Chance.BasisPoints ?? 0,
            DamageRolled: resolution?.Rolled ?? 0,
            Damage: resolution?.Damage ?? 0,
            DamageKind: resolution?.DamageKind.Value ?? string.Empty,
            Resistance: resolution?.Resistance.ToString() ?? string.Empty,
            Condition: resolution?.Condition?.ToString() ?? string.Empty,
            TargetDown: resolution?.TargetDown ?? false,
            // The last order's own side is read from the fight rather than assumed: once the opposition is
            // driven, the last thing that happened may be a blow the party took.
            ByParty: order is not null && combat.Find(order.Actor)?.Side == CombatSide.Party,
            Pacing: combat.Pacing,
            Turn: TurnFrom(combat));
    }

    /// <summary>Reads the round the fight is in, as the panel reads it.</summary>
    /// <remarks>
    /// Every fact here is the pacing's own, read at the moment the projection is built: whose turn it is, how
    /// much of the round has passed, and the order the actors will act in. Nothing is counted down by a
    /// reader, and the order is the fight's own reading of its actors' recovery rather than a list kept beside
    /// it, so a panel that shows a member due in two seconds is showing what the fight holds.
    /// </remarks>
    private static CombatTurnSnapshot TurnFrom(CombatState combat)
    {
        TurnBasedPacing turns = combat.Turns;
        List<TurnOrderActorSnapshot> order = [];
        foreach (TurnOrderEntry entry in turns.Order)
        {
            order.Add(new TurnOrderActorSnapshot(
                entry.Id.ToString(),
                entry.Name,
                entry.Side,
                entry.Remaining.TotalSeconds,
                entry.Ready,
                entry.CanAct,
                entry.Waiting,
                entry.Current));
        }

        return new CombatTurnSnapshot(
            turns.Phase,
            turns.Round,
            turns.Current?.Id.ToString() ?? string.Empty,
            turns.Current?.Name ?? string.Empty,
            turns.WaitsForPlayer,
            turns.Due.TotalSeconds,
            turns.Length.TotalSeconds,
            turns.Elapsed.TotalSeconds,
            turns.MovementLeft.TotalSeconds,
            turns.Last is { } last ? TurnActionName(last) : string.Empty,
            order);
    }

    /// <summary>What a committed turn did, in the words the projection publishes.</summary>
    /// <param name="action">The action to name.</param>
    /// <returns>The wire name of the action.</returns>
    public static string TurnActionName(TurnAction action) => action switch
    {
        TurnAction.Act => "act",
        TurnAction.Skip => "skip",
        _ => "wait",
    };
}

/// <summary>
/// One actor's place in the order a turn-based round acts in, as the panel reads it.
/// </summary>
/// <remarks>
/// It is the fight's own reading of the actor's recovery — the same quantity the real-time pacing releases —
/// published rather than recomputed, so the order a player sees is the order the fight will act in. A reader
/// that ran its own initiative, or counted a recovery down for itself, would show a turn arriving before or
/// after the fight agreed.
/// </remarks>
/// <param name="Id">The actor's identity, as the fight names it.</param>
/// <param name="Name">What the actor is called, as the ruleset named it.</param>
/// <param name="Side">Which side of the fight the actor is on.</param>
/// <param name="RemainingSeconds">How much game time it must still recover, zero when it is ready.</param>
/// <param name="Ready">Whether it may act now.</param>
/// <param name="CanAct">Whether the fight leaves it able to act at all.</param>
/// <param name="Waiting">Whether it has deferred its turn to the end of the round.</param>
/// <param name="Current">Whether this is the actor whose turn it is.</param>
public readonly record struct TurnOrderActorSnapshot(
    string Id,
    string Name,
    CombatSide Side,
    double RemainingSeconds,
    bool Ready,
    bool CanAct,
    bool Waiting,
    bool Current);

/// <summary>
/// The round the fight is in, as the panel needs it: which phase, whose turn, and what is left to act.
/// </summary>
/// <remarks>
/// <para>
/// It is published in both pacings, because "this fight is being played in real time", "a round is under way
/// and the party's turn is waiting for the player", and "the party is walking its movement phase" are three
/// different things a player must be able to tell apart; <see cref="Phase"/> is none when no round is under
/// way, which is what the real-time pacing publishes.
/// </para>
/// <para>
/// <see cref="PlayerTurn"/> is the session's own reason to wait: while it holds, the session steps no world
/// and advances no clock, so a panel that showed the fight moving on would be showing something the product
/// is not doing.
/// </para>
/// </remarks>
/// <param name="Phase">Which part of the round the fight is in.</param>
/// <param name="Round">Which round is being fought, counting from one, or zero when none is.</param>
/// <param name="Actor">The identity of the actor whose turn it is, empty when no turn is out.</param>
/// <param name="ActorName">What that actor is called, empty when no turn is out.</param>
/// <param name="PlayerTurn">Whether the session is waiting for the player's committed turn.</param>
/// <param name="DueSeconds">How much game time passes before the current turn, zero when it is due now.</param>
/// <param name="RoundSeconds">How long this round's action phase is.</param>
/// <param name="ElapsedSeconds">How much of it has passed.</param>
/// <param name="MovementSeconds">What is left of the party's movement phase.</param>
/// <param name="Last">What the party's last committed turn did: <c>act</c>, <c>skip</c>, or <c>wait</c>, empty before one.</param>
/// <param name="Order">The fight's actors in the order they act, ascending remaining recovery.</param>
public readonly record struct CombatTurnSnapshot(
    TurnPhase Phase,
    int Round,
    string Actor,
    string ActorName,
    bool PlayerTurn,
    double DueSeconds,
    double RoundSeconds,
    double ElapsedSeconds,
    double MovementSeconds,
    string Last,
    IReadOnlyList<TurnOrderActorSnapshot> Order);
