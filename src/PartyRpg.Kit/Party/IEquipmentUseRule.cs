namespace PartyRpg.Kit.Party;

/// <summary>Whether one member may wear or wield one item, answered by whoever owns a game's rules.</summary>
/// <remarks>
/// This is the seam that keeps a game's vocabulary out of the kit: whether a member may use a blade depends
/// on a class, a skill, a mastery tier, and exemptions for what needs none — and every one of those names
/// belongs to the ruleset. The rule therefore reads the member it is handed (class, skills, tiers,
/// attributes) and the instance, and answers; the kit never learns a skill name and never guesses that an
/// unknown item is usable.
/// </remarks>
public interface IEquipmentUseRule
{
    /// <summary>Answers whether the member may put the item in the slot, or refuses it.</summary>
    /// <param name="member">The member who would wear or wield the item, with its class and skills to read.</param>
    /// <param name="slot">The slot of that member's figure the item would occupy.</param>
    /// <param name="item">The instance that would be equipped, with its state to read.</param>
    /// <returns>A refusal when the member may not use it, or null when it may.</returns>
    PartyRefusal? Judge(PartyMember member, EquipmentSlot slot, ItemInstance item);
}
