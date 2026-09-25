namespace PartyRpg.Kit.Party;

/// <summary>One enchantment carried by an item instance, with the magnitude it applies at.</summary>
/// <remarks>
/// Content identity plus a number: which enchantment it is belongs to content, and what the magnitude
/// scales is the ruleset's reading of that definition. Keeping the pair as one value means an enchanted
/// item is state a save round-trips rather than an effect recomputed at load time.
/// </remarks>
/// <param name="Enchantment">Which enchantment definition the instance carries.</param>
/// <param name="Magnitude">The magnitude the enchantment applies at, which may be zero for one that only marks the item.</param>
public readonly record struct ItemEnchantment(EnchantmentId Enchantment, int Magnitude);
