using System.Globalization;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Progression;
using PartyRpg.Kit.Skills;

namespace PartyRpg.Kit.Presentation;

/// <summary>One skill a member holds, as the panel shows it: its level, its rung, and what the next point buys.</summary>
/// <remarks>
/// The ceiling travels beside the level rather than being compared by the screen, and the cost or the
/// refusal travels beside both: a panel that worked out "this is at the limit" from two numbers would be a
/// second copy of the ruleset's ceiling, which is exactly what the owner's own plan exists to prevent.
/// </remarks>
/// <param name="Skill">Which skill the row is, as content names it.</param>
/// <param name="Block">Which block of the skill list it belongs to, as the wire spells it.</param>
/// <param name="Level">The level the member holds.</param>
/// <param name="Tier">What the rung the member stands at is called.</param>
/// <param name="CeilingLevel">The highest level the member's class and rank allow.</param>
/// <param name="CeilingTier">What the highest rung the member's class and rank allow is called.</param>
/// <param name="PointsSpent">How many skill points have gone into this skill so far.</param>
/// <param name="Reached">The level one more point would leave the skill at, or the level it stands at when refused.</param>
/// <param name="Cost">What one more level would cost, zero when it would be refused.</param>
/// <param name="Refusal">Why one more level would be refused, empty when it would land.</param>
public readonly record struct SkillRowSnapshot(
    string Skill,
    string Block,
    int Level,
    string Tier,
    int CeilingLevel,
    string CeilingTier,
    int PointsSpent,
    int Reached,
    int Cost,
    string Refusal);

/// <summary>One member's skills, in the order the member learned them.</summary>
/// <param name="Index">The member's place in the party, counted from zero, which a raise control names.</param>
/// <param name="Member">The member's durable identity.</param>
/// <param name="Name">What the member is called.</param>
/// <param name="Class">The member's class, as content names it.</param>
/// <param name="Rank">The member's class rank, which is what a promotion raises.</param>
/// <param name="Skills">The skills the member has learned, with what the next level of each would cost.</param>
public readonly record struct SkillMemberSnapshot(
    int Index,
    string Member,
    string Name,
    string Class,
    int Rank,
    IReadOnlyList<SkillRowSnapshot> Skills);

/// <summary>What every member can hold and what the next skill point would buy.</summary>
/// <remarks>
/// <para>
/// This is the catalog read through the owner: each member's own entries with the rung they stand at, the
/// ceiling their class and rank impose, and the owner's own plan for one more level — the cost when it would
/// land, and the sentence that says why when it would not. A session whose ruleset answered no skill policy,
/// or one that holds no party, publishes <see cref="None"/> and the panel says so rather than showing a
/// character whose skills cannot grow.
/// </para>
/// <para>
/// The last raise is published here rather than beside the level and the experience, because it is this
/// block's own event: it moved a skill and spent points, and a panel that showed the new level without
/// saying what it cost would leave a player checking the purse of the wrong number.
/// </para>
/// </remarks>
/// <param name="Available">Whether the session holds a progression owner that was told how skills grow.</param>
/// <param name="Members">The party's members and their skills, in the party's own order.</param>
/// <param name="Outcome">What the last raise was: <c>none</c>, <c>raised</c>, or <c>refused</c>.</param>
/// <param name="Member">What the member the last raise was about is called, empty before any raise.</param>
/// <param name="Skill">The skill the last raise named, empty before any raise.</param>
/// <param name="Level">The level that skill now stands at, zero before any raise.</param>
/// <param name="Cost">What the last raise cost in skill points, zero when it was refused or none happened.</param>
/// <param name="Code">The last refusal's code, empty when the last raise landed or none has happened.</param>
/// <param name="Message">What the last raise reported, empty before anybody has spent a point.</param>
public readonly record struct SkillsSnapshot(
    bool Available,
    IReadOnlyList<SkillMemberSnapshot> Members,
    string Outcome,
    string Member,
    string Skill,
    int Level,
    int Cost,
    string Code,
    string Message)
{
    /// <summary>No skill policy: nobody's skills can be raised and nothing states how far they may go.</summary>
    public static SkillsSnapshot None => new(
        Available: false,
        Members: [],
        Outcome: "none",
        Member: string.Empty,
        Skill: string.Empty,
        Level: 0,
        Cost: 0,
        Code: string.Empty,
        Message: string.Empty);

    /// <summary>Reads every member's skills out of the progression owner, or none when it holds no policy.</summary>
    /// <param name="progression">The session's progression owner, or null when it holds none.</param>
    /// <returns>The skills the panel shows, or <see cref="None"/> when there is nothing to read.</returns>
    public static SkillsSnapshot From(PartyProgression? progression)
    {
        if (progression?.Skills is not { } policy) return None;

        List<SkillMemberSnapshot> members = [];
        for (int index = 0; index < progression.Party.Members.Count; index++)
        {
            PartyMember member = progression.Party.Members[index];
            List<SkillRowSnapshot> rows = [];
            foreach (SkillEntry entry in member.Skills.Entries)
            {
                SkillCeiling ceiling = policy.Ceiling(member, entry.Skill);
                SkillRaisePlan plan = progression.Plan(member.Id, entry.Skill);
                rows.Add(new SkillRowSnapshot(
                    entry.Skill.Value,
                    WireName(policy.Catalog.Read(entry.Skill).Block),
                    entry.Level,
                    policy.TierName(entry.Tier),
                    ceiling.MaximumLevel,
                    policy.TierName(ceiling.MaximumTier),
                    entry.PointsSpent,
                    plan.Reached,
                    plan.Points,
                    plan.Refusal?.Message ?? string.Empty));
            }

            members.Add(new SkillMemberSnapshot(
                index,
                member.Id.ToString(),
                member.Profile.Name,
                member.Profile.Class.Value,
                member.Progression.ClassRank,
                rows));
        }

        if (progression.LastRaise is { } raise)
        {
            return new SkillsSnapshot(
                Available: true,
                members,
                Outcome: raise.IsRaised ? "raised" : "refused",
                Member: raise.Name,
                Skill: raise.Skill.Value,
                Level: raise.Level,
                Cost: raise.Points,
                Code: raise.Refusal?.Code ?? string.Empty,
                Message: raise.IsRaised
                    ? string.Create(
                        CultureInfo.InvariantCulture,
                        $"{raise.Name} raised {raise.Skill} to level {raise.Level} for {raise.Points} skill point(s), leaving {raise.Remaining}.")
                    : raise.Refusal!.Message);
        }

        return new SkillsSnapshot(
            Available: true,
            members,
            Outcome: "none",
            Member: string.Empty,
            Skill: string.Empty,
            Level: 0,
            Cost: 0,
            Code: string.Empty,
            Message: string.Empty);
    }

    /// <summary>The wire name for a block of the skill list.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The block has no wire name, so the panel would show a wordless row.</exception>
    internal static string WireName(SkillBlock block) => block switch
    {
        SkillBlock.Weapon => "weapon",
        SkillBlock.Armour => "armour",
        SkillBlock.Magic => "magic",
        SkillBlock.Miscellaneous => "miscellaneous",
        SkillBlock.Unused => "unused",
        _ => throw new ArgumentOutOfRangeException(nameof(block), block, "Unknown skill block."),
    };
}
