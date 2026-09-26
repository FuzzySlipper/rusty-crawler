using PartyRpg.Kit.Party;
using PartyRpg.Kit.Progression;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// This game's progression: the curve a level takes, how a party's award divides, what a level gives, and
/// what the world makes of it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The numbers are the donor's; the owner is the kit's.</b> The experience curve, the division among the
/// party, the per-level growth of hit and spell points, the skill points a level grants, and the fame a
/// party's deeds earn are Might and Magic's own tables and formulas, read from the donor reimplementation
/// and cited beside each. Nothing here changes anything: the progression owner asks these questions and
/// writes the answers onto the party, which is what keeps one owner of experience, levels, and points.
/// </para>
/// <para>
/// <b>The curve is the donor's own, and it is a threshold rather than a price.</b> A level takes a
/// thousand times the current level times the level plus one, halved — the same figure the training hall
/// judges a member against and the same one this answers — and a member keeps every point they have
/// earned, which is why this is what must be <em>banked</em> rather than what is spent
/// (<c>src/GUI/UI/Houses/Training.cpp:36-49</c>, and the same expression in
/// <c>src/Engine/PriceCalculator.cpp:214-215</c>). The shipped manual states "current level × 1000" as a
/// rough figure and gives 3000 to go from level 3 to 4
/// ([`docs/research/mm7-manual-outline.md`](../../../docs/research/mm7-manual-outline.md) §7, p.36); the
/// donor's cumulative curve is what this game uses, and the two disagree from level three on. That is the
/// donor being the reference rather than the manual's rounding, and it is recorded here rather than
/// rounded.
/// </para>
/// <para>
/// <b>The growth table is per class and rank.</b> The class identity is the shipped table's own name, read
/// without regard to case: a pack's capitalisation is not an identity, and a scenario that wrote a class in
/// another case would otherwise grow nothing. The donor's per-level figures are indexed by the
/// thirty-six class rows — nine base classes across a base rung, a first promotion, and two second
/// promotions (<c>src/Engine/Objects/Character.cpp:135-172</c>, <c>pBaseHealthPerLevelByClass</c>;
/// <c>:173-209</c>, <c>pBaseManaPerLevelByClass</c>) — and a member's rank here is the tier the donor
/// itself computes from that row: one, two, and three, where both second-promotion alternatives are tier
/// three (<c>src/Engine/Objects/CharacterEnumFunctions.h:149-152</c>, <c>getClassTier</c>). The light and
/// dark alternatives differ in what they unlock and not in what a level gives: every pair in both tables
/// states the same two numbers (Champion and Black Knight 9/0, Priest of the Sun and Priest of the Moon
/// 4/5, Arch Mage and Lich 3/6, and the rest), so a character who has not chosen a path yet still has
/// exactly one growth a level can give. Choosing the path is the promotion stone's, and it will move the
/// rank through the same owner this reads.
/// </para>
/// <para>
/// <b>What is not read.</b> The donor's learning bonus also counts hired teachers, instructors, and
/// scholars (<c>src/Engine/Objects/Character.cpp:625-641</c>, <c>learningPercent</c>; the shipped
/// professions carry the bonus). Our followers carry no profession this build can read, so the bonus is
/// the skill's alone and a hired tutor's share of it is a follower-owner question rather than a number
/// invented here.
/// </para>
/// </remarks>
internal sealed class MightAndMagic7Progression : IProgressionRule
{
    /// <summary>The one instance: this policy holds no state, so a session shares it.</summary>
    internal static MightAndMagic7Progression Instance { get; } = new();

    /// <summary>The skill whose level and mastery raise what an award is worth to a member.</summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/Objects/CharacterEnumFunctions.h:33-45</c> and
    /// <c>src/Engine/Objects/Character.cpp:625-641</c>: the learning skill is read at the member's own
    /// level and mastery, and the manual states the same ladder — nine percent plus one per level, doubled,
    /// tripled, or quintupled by the rung
    /// ([`docs/research/mm7-manual-outline.md`](../../../docs/research/mm7-manual-outline.md) §2, pp.38–41).
    /// </remarks>
    internal static readonly SkillId Learning = new("Learning");

    /// <summary>
    /// What each rung of the learning ladder multiplies the skill's own level by, indexed by the tier's
    /// value.
    /// </summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/Objects/CharacterEnumFunctions.h</c>,
    /// <c>GetMultiplierForSkillLevel(SKILL_LEARNING, 1, 2, 3, 5)</c>, read at
    /// <c>src/Engine/Objects/Character.cpp:635</c>: normal, expert, master, and grand master. A member with
    /// no rung at all is not on the ladder and takes no bonus, which is what index zero is.
    /// </remarks>
    private static readonly int[] LearningMultipliers = [0, 1, 2, 3, 5];

    /// <summary>The conditions a member must be free of to take a share of an award.</summary>
    /// <remarks>
    /// The donor's own list (OpenEnroth <c>src/Engine/Party.cpp:838-846</c>, <c>Party::GivePartyExp</c>):
    /// an unconscious, dead, petrified, or eradicated character earns nothing. The four are this game's
    /// condition identities, which the party carries as content names them.
    /// </remarks>
    private static readonly ConditionId[] CannotEarn =
    [
        MightAndMagic7Conditions.Unconscious,
        MightAndMagic7Conditions.Dead,
        MightAndMagic7Conditions.Petrified,
        MightAndMagic7Conditions.Eradicated,
    ];

    /// <summary>
    /// What one level adds to a member's hit points and spell points, per class and rank.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The donor's two per-level tables, transcribed row for row and cited here:
    /// <c>src/Engine/Objects/Character.cpp:135-172</c> for hit points and <c>:173-209</c> for spell points.
    /// Each entry is the base class's ladder of ranks one to three; the two second-promotion rows of the
    /// shipped table carry identical figures, which is why rank three has one entry rather than two.
    /// </para>
    /// <para>
    /// These are increments rather than the donor's absolute maxima on purpose. The donor computes a
    /// maximum as the class's base plus this figure times the level plus the endurance and skill terms
    /// (<c>Character.cpp:1777-1790</c>, <c>Character::GetMaxHealth</c>); on a level rise every term but the
    /// level stands still, so the table's own value is exactly what one level adds — and our party's
    /// starting pools are content's statement rather than a second formula that would have to agree with
    /// the donor's base array.
    /// </para>
    /// </remarks>
    private static readonly Dictionary<string, ProgressionGrowth[]> GrowthByClass = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Knight"] = [new(5, 0, 0), new(7, 0, 0), new(9, 0, 0)],
        ["Thief"] = [new(4, 0, 0), new(6, 1, 0), new(8, 1, 0)],
        ["Monk"] = [new(5, 0, 0), new(6, 1, 0), new(8, 1, 0)],
        ["Paladin"] = [new(4, 1, 0), new(5, 2, 0), new(6, 3, 0)],
        ["Archer"] = [new(3, 1, 0), new(4, 2, 0), new(6, 3, 0)],
        ["Ranger"] = [new(4, 0, 0), new(5, 2, 0), new(6, 3, 0)],
        ["Cleric"] = [new(2, 3, 0), new(3, 4, 0), new(4, 5, 0)],
        ["Druid"] = [new(2, 3, 0), new(3, 4, 0), new(4, 5, 0)],
        ["Sorcerer"] = [new(2, 3, 0), new(3, 4, 0), new(3, 6, 0)],
    };

    /// <summary>The highest rank this game's class ladder states, which is the donor's own tier three.</summary>
    internal const int HighestRank = 3;

    /// <summary>How much one level of a base class at a rank adds to a member's spell point pool.</summary>
    /// <remarks>
    /// The same table a level rise reads, so a member's pool and the formula that states what it can hold
    /// read one number rather than two: the donor's per-level mana array
    /// (<c>OpenEnroth/src/Engine/Objects/Character.cpp:173-209</c>, <c>pBaseManaPerLevelByClass</c>), whose
    /// value is both what a rise grants and what a caster's level term is worth in
    /// <c>Character::GetMaxMana</c> (<c>:1845-1856</c>). A class or rank this table does not carry answers
    /// nothing, and the spell policy then states no pool for it.
    /// </remarks>
    /// <param name="characterClass">The base class's name, as this game's ladder spells it.</param>
    /// <param name="rank">The member's rank in its class ladder, counting from one.</param>
    /// <returns>How much one level adds, zero when the class or rank states none.</returns>
    internal static int SpellPointsPerLevel(string characterClass, int rank)
    {
        if (!GrowthByClass.TryGetValue(characterClass, out ProgressionGrowth[]? ladder)) return 0;
        return rank >= 1 && rank <= ladder.Length ? ladder[rank - 1].SpellPoints : 0;
    }

    /// <inheritdoc />
    /// <remarks>
    /// OpenEnroth <c>src/GUI/UI/Houses/Training.cpp:37</c> and
    /// <c>src/Engine/PriceCalculator.cpp:214-215</c>: a thousand times the level times the level plus one,
    /// halved. The same expression is what the training hall judges a member against and what this answers,
    /// so a panel that shows "how much more is wanted" and the hall's own refusal are one arithmetic.
    /// </remarks>
    public long ExperienceForLevel(int level)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(level);
        return 1000L * level * (level + 1) / 2;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// The donor's one party award (OpenEnroth <c>src/Engine/Party.cpp:837-855</c>,
    /// <c>Party::GivePartyExp</c>): the amount is divided by how many members can earn — everyone not
    /// unconscious, dead, petrified, or eradicated — each of them taking an equal share plus the learning
    /// bonus that share earns them. The division is the donor's own integer division, so a remainder is
    /// dropped rather than handed to anybody, and a party with nobody able to earn takes nothing at all.
    /// </para>
    /// <para>
    /// A member who cannot earn is left out of the count rather than given a zero share: the donor divides
    /// among the active characters before it credits them, which is what makes a share larger when somebody
    /// is down rather than smaller.
    /// </para>
    /// </remarks>
    public IReadOnlyList<ProgressionShare> Divide(ProgressionDivision division)
    {
        ArgumentNullException.ThrowIfNull(division);
        List<PartyMember> earning = [];
        foreach (PartyMember member in division.Party.Members)
        {
            if (CanEarn(member)) earning.Add(member);
        }

        if (earning.Count == 0) return [];
        long each = division.Amount / earning.Count;
        List<ProgressionShare> shares = [];
        foreach (PartyMember member in earning)
        {
            int learning = LearningPercent(member);
            shares.Add(new ProgressionShare(member.Id, member.Profile.Name, each + (each * learning / 100)));
        }

        return shares;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// The class and rank tables above for the pools, and the donor's own skill point grant for the points:
    /// a level grants the new level divided by ten, plus five (OpenEnroth
    /// <c>src/GUI/UI/Houses/Training.cpp:72</c>, <c>uSkillPoints += uLevel / 10 + 5</c>, where the level has
    /// already risen). That is the manual's ladder as the donor implements it — five points a level, six
    /// from level ten, seven from level twenty
    /// ([`docs/research/mm7-manual-outline.md`](../../../docs/research/mm7-manual-outline.md) §7, p.36) —
    /// read from the donor's arithmetic rather than from the manual's sentence.
    /// </para>
    /// <para>
    /// <b>A promoted member grows at its family's own rate.</b> The donor's tables state a row per base
    /// class and rank, not per class name, and this game's ranks are classes: a Wizard, an Arch Mage, and a
    /// Lich all read the Sorcerer's ladder at their own rank, which is why the base class is resolved first.
    /// A pair this game describes no growth for is a defect rather than a level that quietly gives nothing —
    /// a member is created in one of the nine base classes and promoted through the three rungs of its own
    /// ladder — and the message says which pair disagreed.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">The member's class or rank has no growth this game describes.</exception>
    public ProgressionGrowth Growth(ProgressionGrowthRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        // A member's growth is read from the base class's row, exactly as its spell points are: a promoted
        // character belongs to a class the donor's table states no separate growth for — a Lich and an Arch
        // Mage grow at their family's own rate — so the family is resolved first and the rank picks the row
        // within it.
        string characterClass = MightAndMagic7Skills.BaseClassOf(request.Member.Profile.Class.Value);
        int rank = request.Member.Progression.ClassRank;
        if (!GrowthByClass.TryGetValue(characterClass, out ProgressionGrowth[]? ladder))
        {
            throw new InvalidOperationException(
                $"The class '{characterClass}' has no per-level growth in this game's table, so there is no table row to read for level {request.Level}.");
        }

        if (rank < 1 || rank > ladder.Length)
        {
            throw new InvalidOperationException(
                $"The class '{characterClass}' stands at rank {rank}, and this game's ladder for it states ranks one to {HighestRank}.");
        }

        ProgressionGrowth growth = ladder[rank - 1];
        int points = (request.Level / 10) + 5;
        return growth with { SkillPoints = points };
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <b>Fame is what the party's deeds are worth, read from the same experience they earned.</b> The donor
    /// keeps no fame of its own: <c>Party::fame()</c> is the party's total experience divided by a thousand,
    /// capped at what the type holds (OpenEnroth <c>src/Engine/Party.cpp:371-379</c>). This answers the
    /// increase that figure implies rather than the figure itself, because our party carries a fame value
    /// that content may set and a save round-trips, and a rule that overwrote it would discard standing the
    /// party already had. A party whose authored fame already stands above what its experience earns keeps
    /// it.
    /// </para>
    /// <para>
    /// <b>Reputation is untouched.</b> Neither the donor's kill path nor its training path changes it: a
    /// kill awards experience (<c>src/Engine/Objects/Actor.cpp:3164-3167</c>) and a training step charges
    /// gold (<c>src/GUI/UI/Houses/Training.cpp:65-88</c>), and the reputation a place holds is its own
    /// (<c>src/Engine/LocationInfo.h:7</c>). A deed that does change what the world thinks — a quest
    /// completed, a rank earned — answers here when it lands, through the same owner as the level it gave.
    /// </para>
    /// </remarks>
    public ProgressionStanding Standing(ProgressionStandingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        long total = 0;
        foreach (PartyMember member in request.Party.Members) total = checked(total + member.Progression.Experience);
        int earned = (int)Math.Min(total / 1000, int.MaxValue);
        int fame = Math.Max(0, earned - request.Party.Reputation.Fame);
        return new ProgressionStanding(Reputation: 0, Fame: fame);
    }

    /// <summary>
    /// The learning bonus one member's share earns, as the donor computes it.
    /// </summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/Objects/Character.cpp:625-641</c>: with the skill at all, nine percent plus
    /// the skill's level multiplied by its rung's multiplier; with no skill, nothing. What a hired
    /// teacher's own share of this would be is a follower question and not read here.
    /// </remarks>
    private static int LearningPercent(PartyMember member)
    {
        if (!member.Skills.Knows(Learning)) return 0;
        int rung = Math.Min(member.Skills.TierOf(Learning).Value, LearningMultipliers.Length - 1);
        int multiplier = LearningMultipliers[rung];
        return multiplier == 0 ? 0 : (multiplier * member.Skills.LevelOf(Learning)) + 9;
    }

    /// <summary>Whether a member can take a share of an award, which the donor's own condition list decides.</summary>
    private static bool CanEarn(PartyMember member)
    {
        foreach (ConditionId condition in CannotEarn)
        {
            if (member.Conditions.Has(condition)) return false;
        }

        return true;
    }
}
