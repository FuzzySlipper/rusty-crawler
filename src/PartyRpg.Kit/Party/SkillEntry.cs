namespace PartyRpg.Kit.Party;

/// <summary>One skill a character has learned, at the level and tier it currently stands at.</summary>
/// <remarks>
/// An entry is a value, so raising a skill replaces the entry rather than mutating one shared record, and a
/// captured save holds what the character knew at that moment. The points spent are recorded beside the
/// level because a game charges for levels and may refund or audit them; what that charge is remains the
/// ruleset's formula.
/// </remarks>
/// <param name="Skill">Which skill definition the entry is about.</param>
/// <param name="Level">How many levels of the skill the character has bought; at least one once learned.</param>
/// <param name="Tier">How far up the ladder the character has trained; see <see cref="SkillTier"/>.</param>
/// <param name="PointsSpent">How many skill points have gone into this entry so far.</param>
public readonly record struct SkillEntry(SkillId Skill, int Level, SkillTier Tier, int PointsSpent);
