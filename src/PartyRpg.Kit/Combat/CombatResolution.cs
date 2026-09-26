using System.Globalization;

namespace PartyRpg.Kit.Combat;

/// <summary>
/// What one attack did, as the fight resolved and recorded it: whether it landed, what it rolled, what the
/// target's resistance took off it, what was left on the target, and where the target now stands.
/// </summary>
/// <remarks>
/// <para>
/// This is the value the fight publishes for a panel and reports to diagnostics, and it is the whole of what
/// an attack came to. Every number in it is a fact about what happened rather than an opinion: the chance the
/// ruleset stated, the value the hit roll drew, the dice the damage rolled, the resistance the target was
/// read for, and the harm that landed after it. A screen that showed only a name and a pool would leave a
/// player unable to tell a miss from an immune target from a hit that rolled low.
/// </para>
/// <para>
/// <b>A miss and an immune hit are different results.</b> Both leave the target's pool untouched, and they
/// are the two things a player most needs told apart: one says aim better, the other says stop using that.
/// The chance and the resistance reading are therefore carried on both, and the message says which happened.
/// </para>
/// </remarks>
public sealed record CombatResolution
{
    private CombatResolution(
        CombatantId actor,
        string actorName,
        CombatantId target,
        string targetName,
        AttackKind kind,
        bool hit,
        HitChance chance,
        int hitRoll,
        DamageKindId damageKind,
        int rolled,
        Resistance resistance,
        int damage,
        CombatCondition? condition,
        bool targetDown,
        int targetHitPoints,
        int targetHitPointsMax)
    {
        Actor = actor;
        ActorName = actorName;
        Target = target;
        TargetName = targetName;
        Kind = kind;
        Hit = hit;
        Chance = chance;
        HitRoll = hitRoll;
        DamageKind = damageKind;
        Rolled = rolled;
        Resistance = resistance;
        Damage = damage;
        Condition = condition;
        TargetDown = targetDown;
        TargetHitPoints = targetHitPoints;
        TargetHitPointsMax = targetHitPointsMax;
        Message = Describe();
    }

    /// <summary>The combatant that attacked.</summary>
    public CombatantId Actor { get; }

    /// <summary>What that combatant is called.</summary>
    public string ActorName { get; }

    /// <summary>The combatant that was attacked.</summary>
    public CombatantId Target { get; }

    /// <summary>What that combatant is called.</summary>
    public string TargetName { get; }

    /// <summary>How the attack was made.</summary>
    public AttackKind Kind { get; }

    /// <summary>Whether the attack landed.</summary>
    public bool Hit { get; }

    /// <summary>How likely the ruleset said it was to land.</summary>
    public HitChance Chance { get; }

    /// <summary>The value the hit roll drew, from zero to nine thousand nine hundred and ninety-nine.</summary>
    public int HitRoll { get; }

    /// <summary>What kind of harm the attack does.</summary>
    public DamageKindId DamageKind { get; }

    /// <summary>What the damage dice rolled, before the target's resistance.</summary>
    public int Rolled { get; }

    /// <summary>What the target resists of that kind of harm.</summary>
    public Resistance Resistance { get; }

    /// <summary>How much harm actually landed.</summary>
    public int Damage { get; }

    /// <summary>What the hit left on the target besides harm, or null when it left nothing.</summary>
    public CombatCondition? Condition { get; }

    /// <summary>Whether this hit is what took the target down.</summary>
    public bool TargetDown { get; }

    /// <summary>What the target has left to lose after this hit.</summary>
    public int TargetHitPoints { get; }

    /// <summary>What the target had to lose before anything was taken off it.</summary>
    public int TargetHitPointsMax { get; }

    /// <summary>What happened, in a sentence a person reads.</summary>
    public string Message { get; }

    /// <summary>The attack landed on nothing, because the actor aimed at nothing.</summary>
    /// <remarks>
    /// An actor with nothing in reach still acts and still pays for it; this is the resolution of that act,
    /// which is not a miss against a target and must not be reported as one.
    /// </remarks>
    /// <param name="actor">The combatant that acted.</param>
    /// <param name="actorName">What it is called.</param>
    /// <param name="kind">How it attacked.</param>
    /// <returns>The resolution.</returns>
    public static CombatResolution Nothing(CombatantId actor, string actorName, AttackKind kind) => new(
        actor,
        actorName,
        target: actor,
        targetName: string.Empty,
        kind,
        hit: false,
        HitChance.Never,
        hitRoll: 0,
        new DamageKindId("none"),
        rolled: 0,
        Resistance.Of(0),
        damage: 0,
        condition: null,
        targetDown: false,
        targetHitPoints: 0,
        targetHitPointsMax: 0);

    /// <summary>The attack missed.</summary>
    /// <param name="actor">The combatant that attacked.</param>
    /// <param name="actorName">What it is called.</param>
    /// <param name="target">The combatant it missed.</param>
    /// <param name="targetName">What that is called.</param>
    /// <param name="kind">How the attack was made.</param>
    /// <param name="chance">How likely it was to land.</param>
    /// <param name="hitRoll">The value the hit roll drew.</param>
    /// <param name="damageKind">What kind of harm it would have done.</param>
    /// <param name="targetHitPoints">What the target has left.</param>
    /// <param name="targetHitPointsMax">What the target had.</param>
    /// <returns>The resolution.</returns>
    public static CombatResolution Missed(
        CombatantId actor,
        string actorName,
        CombatantId target,
        string targetName,
        AttackKind kind,
        HitChance chance,
        int hitRoll,
        DamageKindId damageKind,
        int targetHitPoints,
        int targetHitPointsMax) => new(
        actor,
        actorName,
        target,
        targetName,
        kind,
        hit: false,
        chance,
        hitRoll,
        damageKind,
        rolled: 0,
        Resistance.Of(0),
        damage: 0,
        condition: null,
        targetDown: false,
        targetHitPoints,
        targetHitPointsMax);

    /// <summary>The attack landed.</summary>
    /// <param name="actor">The combatant that attacked.</param>
    /// <param name="actorName">What it is called.</param>
    /// <param name="target">The combatant it hit.</param>
    /// <param name="targetName">What that is called.</param>
    /// <param name="kind">How the attack was made.</param>
    /// <param name="chance">How likely it was to land.</param>
    /// <param name="hitRoll">The value the hit roll drew.</param>
    /// <param name="damageKind">What kind of harm it did.</param>
    /// <param name="rolled">What the damage dice rolled.</param>
    /// <param name="resistance">What the target resists of that kind.</param>
    /// <param name="damage">How much harm landed.</param>
    /// <param name="condition">What the hit left on the target, or null.</param>
    /// <param name="targetDown">Whether this hit took the target down.</param>
    /// <param name="targetHitPoints">What the target has left.</param>
    /// <param name="targetHitPointsMax">What the target had.</param>
    /// <returns>The resolution.</returns>
    public static CombatResolution Landed(
        CombatantId actor,
        string actorName,
        CombatantId target,
        string targetName,
        AttackKind kind,
        HitChance chance,
        int hitRoll,
        DamageKindId damageKind,
        int rolled,
        Resistance resistance,
        int damage,
        CombatCondition? condition,
        bool targetDown,
        int targetHitPoints,
        int targetHitPointsMax) => new(
        actor,
        actorName,
        target,
        targetName,
        kind,
        hit: true,
        chance,
        hitRoll,
        damageKind,
        rolled,
        resistance,
        damage,
        condition,
        targetDown,
        targetHitPoints,
        targetHitPointsMax);

    /// <summary>Writes what happened in one sentence, with the numbers that explain it.</summary>
    private string Describe()
    {
        if (TargetName.Length == 0)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{ActorName} attacks nothing in reach ({AttackKinds.WireName(Kind)}).");
        }

        if (!Hit)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{ActorName} attacks {TargetName} ({AttackKinds.WireName(Kind)}) and misses: the hit roll was {HitRoll} against a {Chance} chance.");
        }

        string harm = Resistance.IsImmune
            ? string.Create(CultureInfo.InvariantCulture, $"{TargetName} is immune to {DamageKind}, so the {Rolled} rolled lands for nothing")
            : Resistance.Points > 0
                ? string.Create(CultureInfo.InvariantCulture, $"{Rolled} {DamageKind} damage rolled, {Damage} landed through {Resistance.Points} resistance")
                : string.Create(CultureInfo.InvariantCulture, $"{Damage} {DamageKind} damage landed");

        string standing = TargetHitPointsMax > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{TargetName} is at {TargetHitPoints}/{TargetHitPointsMax}")
            : $"{TargetName} is down";

        string condition = Condition is { } left
            ? string.Create(CultureInfo.InvariantCulture, $" and is left {left.Condition} ({left.Source})")
            : string.Empty;
        string down = TargetDown ? ", and is down" : string.Empty;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{ActorName} hits {TargetName} ({AttackKinds.WireName(Kind)}): {harm}; {standing}{condition}{down}.");
    }

    /// <inheritdoc />
    public override string ToString() => Message;
}
