using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Combat;

/// <summary>What a creature decided to do with this update of a fight.</summary>
/// <remarks>
/// <para>
/// These are the four shapes a creature's moment has: it acts, it moves, it moves away, or it holds. There
/// is deliberately no shape per kind of attack and none per kind of monster — how a creature attacks is an
/// <see cref="AttackKind"/> beside the action, and what makes one creature behave differently from another
/// is data the ruleset read, not a case this vocabulary grew.
/// </para>
/// <para>
/// <b>Moving and acting are separate answers.</b> A creature attacks at an instant and moves over an
/// interval, and the fight paces the first by recovery while the second happens every update. A creature
/// that wants to attack but is still recovering therefore still has a decision this update — it closes, or
/// holds — which is why <see cref="Attack"/> carries a target and <see cref="Advance"/> carries one too.
/// </para>
/// </remarks>
public enum CreatureAction
{
    /// <summary>Stand where it is: a creature at its post, one with nothing in reach, or one that is waiting.</summary>
    Wait,

    /// <summary>Move toward the actor the decision is about.</summary>
    Advance,

    /// <summary>Move away from the actor the decision is about.</summary>
    Retreat,

    /// <summary>Attack the actor the decision is about, if the fight's gate lets it act.</summary>
    Attack,
}

/// <summary>One creature's decision: what it does, how, and about whom.</summary>
/// <remarks>
/// A decision is a value rather than a mutation: the driver applies it, and the fight decides whether the
/// actor may act. A decision naming no target is a creature with nothing to act against, which the driver
/// applies as holding rather than as an attack at nothing — a creature does not swing at empty air here.
/// </remarks>
/// <param name="Action">What the creature does with this update.</param>
/// <param name="Kind">How it attacks, when it attacks.</param>
/// <param name="Target">What the decision is about, or null when it is about nobody.</param>
/// <param name="Ability">
/// Which of the creature's own ways of attacking it chose, in the ruleset's own vocabulary, empty when it
/// chose none. It travels with the order the driver gives, which is what lets a second attack and a spell be
/// resolved as themselves rather than as another swing of the first.
/// </param>
public readonly record struct CreatureDecision(
    CreatureAction Action,
    AttackKind Kind,
    CombatantId? Target,
    string Ability = "")
{
    /// <summary>The creature holds where it stands.</summary>
    public static CreatureDecision Wait { get; } = new(CreatureAction.Wait, AttackKind.Melee, null);

    /// <summary>The creature closes on an actor.</summary>
    /// <param name="target">What it is closing on.</param>
    public static CreatureDecision Advance(CombatantId target) => new(CreatureAction.Advance, AttackKind.Melee, target);

    /// <summary>The creature backs away from an actor.</summary>
    /// <param name="target">What it is leaving.</param>
    public static CreatureDecision Retreat(CombatantId target) => new(CreatureAction.Retreat, AttackKind.Melee, target);

    /// <summary>The creature attacks an actor.</summary>
    /// <param name="target">What it attacks.</param>
    /// <param name="kind">How it attacks.</param>
    /// <param name="ability">Which of its own ways of attacking it chose, empty when it chose none.</param>
    public static CreatureDecision Attack(CombatantId target, AttackKind kind, string ability = "") =>
        new(CreatureAction.Attack, kind, target, ability);
}

/// <summary>One actor a creature could act about, as the fight reads it.</summary>
/// <remarks>
/// <para>
/// A candidate is what the fight already knows about an actor — who it is, what it is called — plus the two
/// answers that belong to this decision: whether the creature treats it as an enemy, which only the ruleset
/// can say, and how far it stands from <em>this</em> creature.
/// </para>
/// <para>
/// <b>The distance is the pair's own and not the fight's.</b> A fight measures every actor from the party,
/// because that is what decides who has noticed whom; a creature deciding what to do next needs the distance
/// between itself and each candidate instead, and those are different numbers: two creatures standing a
/// thousand units from the party and ten units apart are neighbours to each other and strangers to nobody.
/// The actor's own <see cref="Combatant.Distance"/> would answer the first question for all of them.
/// </para>
/// </remarks>
/// <param name="Actor">The actor itself, as the fight holds it.</param>
/// <param name="IsEnemy">Whether the driving policy answered that this creature treats it as an enemy.</param>
/// <param name="IsParty">Whether it is one of the party's own members, which a creature can tell a foe from.</param>
/// <param name="Distance">How far the candidate stands from the creature deciding, in the place's own units.</param>
public readonly record struct CreatureCandidate(Combatant Actor, bool IsEnemy, bool IsParty, double Distance);

/// <summary>
/// Everything a policy is told about one creature's moment, in the kit's own words.
/// </summary>
/// <remarks>
/// <para>
/// A situation is a reading and owns nothing: the combatant is the fight's own, the candidates are the
/// fight's own, and the policies that read them do not keep them. It carries no game word — no kind of
/// monster, no school of magic, no name of an effect — because a ruleset reads all of that from the
/// creature's own content through <see cref="Combatant.Subject"/>.
/// </para>
/// <para>
/// <see cref="Ready"/> says whether the fight's gate would let the creature act this update, so a policy
/// that wants to attack while recovering can say so and be understood: the driver closes the distance
/// instead, because a recovering creature has not lost its intent, only its turn.
/// </para>
/// </remarks>
/// <param name="Self">The creature deciding, as the fight holds it.</param>
/// <param name="HitPoints">How much harm it has left to take.</param>
/// <param name="HitPointsMax">What it can take altogether, which is the measure a policy's own thresholds need.</param>
/// <param name="Candidates">
/// Every other actor in the fight, in combatant order, each with the policy's own answer about it and its own
/// distance from this creature.
/// </param>
/// <param name="Round">
/// How many decisions this creature has made in this fight, counted from zero. It is what a policy keys a
/// random draw on — a chance to cast a spell, a chance to use a second attack — so that the same state
/// produces the same decision and nothing about a fight has to be recorded for a save.
/// </param>
/// <param name="PartyPose">Where the party stands, which is the one position a policy needs for its own distances.</param>
public sealed record CreatureSituation(
    Combatant Self,
    int HitPoints,
    int HitPointsMax,
    IReadOnlyList<CreatureCandidate> Candidates,
    long Round,
    PlacePose PartyPose)
{
    /// <summary>Whether the fight's gate would let the creature act right now.</summary>
    public bool Ready => Self.IsReady;

    /// <summary>How much of what the creature can take it has left, from zero to one.</summary>
    public double Fraction => HitPointsMax > 0 ? Math.Clamp(HitPoints / (double)HitPointsMax, 0, 1) : 0;

    /// <summary>The nearest candidate the policy called an enemy, or null when it has none.</summary>
    public CreatureCandidate? NearestEnemy
    {
        get
        {
            CreatureCandidate? nearest = null;
            foreach (CreatureCandidate candidate in Candidates)
            {
                if (!candidate.IsEnemy) continue;
                if (nearest is not { } found || candidate.Distance < found.Distance) nearest = candidate;
            }

            return nearest;
        }
    }

    /// <summary>The nearest candidate of the party's own side, or null when the party is not in the fight.</summary>
    public CreatureCandidate? NearestParty
    {
        get
        {
            CreatureCandidate? nearest = null;
            foreach (CreatureCandidate candidate in Candidates)
            {
                if (!candidate.IsParty) continue;
                if (nearest is not { } found || candidate.Distance < found.Distance) nearest = candidate;
            }

            return nearest;
        }
    }
}

/// <summary>
/// This game's answer about how a creature behaves: who it treats as an enemy, how fast it moves, and what
/// it does with each update it is given.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is where a monster's behavior lives, and it is data plus an answer rather than a class.</b> The
/// kit drives every creature the same way — ask, apply, pace — and this decides. A ruleset reads a
/// creature's own content for what it is: the row it names, how it fights, whether it runs when hurt, how
/// far it notices, and what it thinks of other kinds. Adding a kind of monster, therefore, adds a row of
/// content and no code; a per-monster class or a switch over monster kinds would be exactly the shape this
/// seam exists to prevent, and the architecture tests would have nothing to say about it because it would
/// compile.
/// </para>
/// <para>
/// <b>Determinism is the policy's own business.</b> A chance — whether to cast, whether to use a second
/// attack — must be drawn through the engine's keyed random service from a key derived from
/// <see cref="CreatureSituation.Round"/> and the creature's own identity, so the same state produces the
/// same decision however often it is replayed and nothing about the choice has to be carried in a save. A
/// policy that draws from an unrecorded generator breaks reproducibility, which is the property a test
/// checks by running the same fight twice.
/// </para>
/// </remarks>
public interface IMonsterAiPolicy
{
    /// <summary>
    /// Whether a creature treats another actor as an enemy, which is this game's answer about the
    /// relationships between kinds rather than a table the kit keeps.
    /// </summary>
    /// <remarks>
    /// The kit asks this about every creature in the place and hands the answer back to the policy inside
    /// the situation, so a policy's own decision can be written against it without the policy re-deriving
    /// it. A policy that answers nothing here says that no two creatures of this game are enemies, which is
    /// a place where the party alone is fought over.
    /// </remarks>
    /// <param name="self">The creature whose feelings are read.</param>
    /// <param name="other">The actor it is looking at.</param>
    /// <returns>Whether it treats the other as an enemy.</returns>
    bool AreEnemies(CombatSubject self, CombatSubject other);

    /// <summary>How fast a creature covers ground, in place units per second.</summary>
    /// <remarks>
    /// This is the creature's own pace and belongs to its content: a kind of creature that closes quickly
    /// and one that barely moves differ here and nowhere else. Zero or a value that is not a number means
    /// the creature does not move at all, which a stationary kind answers.
    /// </remarks>
    /// <param name="subject">The creature to price.</param>
    /// <returns>Its speed, in the units the place's positions are stated in.</returns>
    double SpeedOf(CombatSubject subject);

    /// <summary>What the creature does with this update.</summary>
    /// <param name="situation">The creature, what it can see, and how far through the fight it is.</param>
    /// <returns>What it decided to do.</returns>
    CreatureDecision Decide(CreatureSituation situation);
}
