using PartyRpg.Kit.Party;

namespace PartyRpg.Kit.Skills;

/// <summary>How far one character's class and rank let one skill grow: its level and the rungs open to it.</summary>
/// <remarks>
/// <para>
/// This is the ceiling vocabulary and nothing else. Which numbers apply to a class, a rank, and a skill is
/// the ruleset's answer over its own tables — a game without class ceilings answers a permissive value —
/// and this type only carries the answer so that the spend path, the teacher, and the screen all judge
/// against one pair of numbers instead of three readings of a table.
/// </para>
/// <para>
/// <b>A ceiling of zero is "this character may not have the skill at all".</b> That is a different fact
/// from "the skill is at its limit", which is a ceiling whose level has been reached, and the two are told
/// apart by <see cref="IsNone"/>: the first never allows a level, the second allows the skill to be held
/// exactly as far as it has come.
/// </para>
/// </remarks>
/// <param name="MaximumLevel">The highest level the skill may reach, zero when the skill may not be had.</param>
/// <param name="MaximumTier">The highest rung of the skill's ladder the character may reach.</param>
public readonly record struct SkillCeiling(int MaximumLevel, SkillTier MaximumTier)
{
    /// <summary>A ceiling under which the skill may not be held at all.</summary>
    public static SkillCeiling None => default;

    /// <summary>Whether the skill may not be had.</summary>
    public bool IsNone => MaximumLevel <= 0;

    /// <summary>Whether this ceiling lets the character reach a rung.</summary>
    /// <param name="tier">The rung being asked about.</param>
    public bool Allows(SkillTier tier) => MaximumTier.Value >= tier.Value;

    /// <summary>Whether this ceiling lets the character reach a level.</summary>
    /// <param name="level">The level being asked about.</param>
    public bool AllowsLevel(int level) => level <= MaximumLevel;

    /// <inheritdoc />
    public override string ToString() =>
        string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"level {MaximumLevel}, rung {MaximumTier.Value}");
}
