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
/// <see cref="Ready"/> and <see cref="RecoverySeconds"/> are two readings of one value, published together
/// because a player needs both: whether the ready light is on, and how long until it is.
/// </para>
/// </remarks>
/// <param name="Id">The combatant's identity, as the fight names it.</param>
/// <param name="Name">What the actor is called, as the ruleset named it.</param>
/// <param name="Ready">Whether the actor may act now.</param>
/// <param name="RecoverySeconds">How much game time it must still recover, zero when it is ready.</param>
/// <param name="Distance">How far it stands from the party, in the place's own units.</param>
/// <param name="HitPoints">What the actor has left to lose, read from wherever that is owned.</param>
/// <param name="HitPointsMax">What it can take altogether, which is the measure the first number needs.</param>
/// <param name="Conditions">What is acting on the actor, in the words the party already carries, empty when nothing is.</param>
/// <param name="Down">Whether the actor is out of the fight: laid out by what is on it, or taken down by harm.</param>
public readonly record struct CombatActorSnapshot(
    string Id,
    string Name,
    bool Ready,
    double RecoverySeconds,
    double Distance,
    int HitPoints = 0,
    int HitPointsMax = 0,
    string Conditions = "",
    bool Down = false);

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
/// <param name="Ready">How many of the party's members may act now.</param>
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
    bool TargetDown = false)
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
    /// <returns>The facts the panel shows, or <see cref="None"/> when there is no mechanism.</returns>
    public static CombatSnapshot From(CombatState? combat)
    {
        if (combat is null) return None;

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
            CombatActorSnapshot actor = new(
                combatant.Id.ToString(),
                combatant.Name,
                combatant.IsReady,
                combatant.Recovery.TotalSeconds,
                combatant.Distance,
                hitPoints,
                hitPointsMax,
                conditions,
                combat.IsDown(combatant));
            if (combatant.Side != CombatSide.Party)
            {
                // Only the actors actually fighting are published as enemies: a creature that has not
                // noticed the party is in no fight, and listing it would make a quiet street read as one.
                if (combatant.Side == CombatSide.Opposition) enemies.Add(actor);
                continue;
            }

            if (combatant.IsReady) ready++;
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
            TargetDown: resolution?.TargetDown ?? false);
    }
}
