using PartyRpg.Kit.Party;
using PartyRpg.Kit.Skills;

namespace PartyRpg.Kit.Progression;

/// <summary>What raising a skill would cost and how far it would go, or why it would be refused.</summary>
/// <remarks>
/// <para>
/// This is the owner's read of a raise it has not performed: the panel publishes it, so what a screen shows
/// as "this would cost eight points" or "this is already at the limit" is the very answer the raise itself
/// would act on rather than an arithmetic the screen performed beside it.
/// </para>
/// <para>
/// A plan for a skill the member has not learned reports the level as zero and refuses by name: what is
/// missing is a lesson, not points, and the refusal says so rather than quoting a price for something the
/// character cannot train.
/// </para>
/// </remarks>
/// <param name="Skill">The skill being asked about.</param>
/// <param name="Levels">How many levels the raise would add.</param>
/// <param name="Level">The level the skill stands at now, zero when the member has not learned it.</param>
/// <param name="Reached">The level the raise would leave the skill at.</param>
/// <param name="Ceiling">How far the member's class and rank let this skill grow.</param>
/// <param name="Points">How many skill points the raise would cost, zero when it is refused.</param>
/// <param name="Refusal">Why the raise would be refused, or null when it would land.</param>
public readonly record struct SkillRaisePlan(
    SkillId Skill,
    int Levels,
    int Level,
    int Reached,
    SkillCeiling Ceiling,
    int Points,
    PartyRefusal? Refusal)
{
    /// <summary>Whether the raise would land if the member asked for it now.</summary>
    public bool IsPossible => Refusal is null;
}

/// <summary>What one raise did: the level and rung the skill now stands at, or why nothing was raised.</summary>
/// <remarks>
/// The points it cost and what is left are reported together, because a raise is one operation on two
/// numbers: a result that named the cost without the remainder would leave a screen subtracting, and a
/// screen that subtracted would be a second copy of the pool this owner holds.
/// </remarks>
/// <param name="Member">The member whose skill was raised.</param>
/// <param name="Name">What that member is called.</param>
/// <param name="Skill">The skill that was raised.</param>
/// <param name="Levels">How many levels the raise added.</param>
/// <param name="Level">The level the skill now stands at, or the level it stood at when the raise was refused.</param>
/// <param name="Tier">The rung the skill stands at, which a raise never moves.</param>
/// <param name="Points">How many skill points the raise cost, zero when nothing was spent.</param>
/// <param name="Remaining">How many skill points the member still holds unspent.</param>
/// <param name="Refusal">Why the raise was refused, or null when it landed.</param>
public sealed record SkillRaiseResult(
    PartyMemberId Member,
    string Name,
    SkillId Skill,
    int Levels,
    int Level,
    SkillTier Tier,
    int Points,
    int Remaining,
    PartyRefusal? Refusal)
{
    /// <summary>Whether the skill was raised.</summary>
    public bool IsRaised => Refusal is null;

    /// <summary>A raise that was refused, with the member's pool exactly as it stood.</summary>
    /// <param name="member">The member.</param>
    /// <param name="name">What the member is called.</param>
    /// <param name="skill">The skill that was asked about.</param>
    /// <param name="levels">How many levels were asked for.</param>
    /// <param name="level">The level the skill stands at.</param>
    /// <param name="tier">The rung the skill stands at.</param>
    /// <param name="remaining">How many skill points the member holds.</param>
    /// <param name="refusal">Why the raise was refused.</param>
    /// <exception cref="ArgumentNullException">No refusal was supplied.</exception>
    internal static SkillRaiseResult Refused(
        PartyMemberId member,
        string name,
        SkillId skill,
        int levels,
        int level,
        SkillTier tier,
        int remaining,
        PartyRefusal refusal)
    {
        ArgumentNullException.ThrowIfNull(refusal);
        return new SkillRaiseResult(
            member,
            name,
            skill,
            levels,
            level,
            tier,
            Points: 0,
            remaining,
            refusal);
    }
}
