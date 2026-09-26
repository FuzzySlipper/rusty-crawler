using PartyRpg.Kit.Party;

namespace PartyRpg.Kit.Magic;

/// <summary>What spell an item the party holds carries, and how using it spends the item.</summary>
/// <remarks>
/// <para>
/// <b>This is a reading of the game's own item rows, not a kind of item.</b> A game's table states which of
/// its items carry a spell and what that spell is; what the kit is handed is that answer, so the mechanism
/// knows an item carries one spell, how many uses it holds, and whether using it uses it up — and never
/// learns what any of those items is called.
/// </para>
/// <para>
/// <b>Where the uses live.</b> How many uses an item holds when it is full is stated here because the row
/// that states it is the game's; how many have been spent is the party's own item state, so an item found in
/// a chest, one bought at a counter, and one restored from a save are all read the same way and the count
/// cannot disagree with itself.
/// </para>
/// </remarks>
/// <param name="Spell">The spell the item carries, which is the one a casting from it uses.</param>
/// <param name="ConsumedByUse">Whether one use uses the item up, which is a scroll's own reading.</param>
/// <param name="Charges">How many uses the item holds when full, zero when one use uses it up.</param>
public readonly record struct SpellItemReading(SpellId Spell, bool ConsumedByUse, int Charges)
{
    /// <summary>A reading of an item that is used up by the one spell it carries.</summary>
    /// <param name="spell">The spell the item carries.</param>
    /// <returns>The reading.</returns>
    public static SpellItemReading Consumed(SpellId spell) => new(spell, ConsumedByUse: true, Charges: 0);

    /// <summary>A reading of an item that holds a number of uses of the spell it carries.</summary>
    /// <param name="spell">The spell the item carries.</param>
    /// <param name="charges">How many uses it holds when full, which must be at least one.</param>
    /// <returns>The reading.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The item holds no uses, so no reading of it is a weapon.</exception>
    public static SpellItemReading Charged(SpellId spell, int charges)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(charges, 1);
        return new SpellItemReading(spell, ConsumedByUse: false, charges);
    }
}

/// <summary>What a person reads for an item, as the game that names its own rows answers.</summary>
/// <remarks>
/// A pack holds definitions rather than names, and a panel that showed an item's identity would be showing
/// the operator's own row number to a player. Nothing in the kit knows what an item is called, so a game that
/// names its items answers here and a game that does not falls back to the identity, which is at least a
/// thing a person can match against the pack they are looking at.
/// </remarks>
public interface ISpellItemNames
{
    /// <summary>What a person reads for an item definition, empty when nothing names it.</summary>
    /// <param name="definition">The item definition to name.</param>
    /// <returns>The name, or empty when this game states none.</returns>
    string NameOf(ItemDefinitionId definition);
}

/// <summary>Which items carry a spell, and what using one costs the item, as the game's rows state it.</summary>
/// <remarks>
/// The rule is asked about a definition rather than about an instance because what a kind of item carries is
/// content's answer; how much of it is left is the party's item state, read where the item is used. A rule
/// that answered nothing for a definition is not an error: most items carry no spell, and a casting that
/// named one of them is refused by name rather than cast.
/// </remarks>
public interface ISpellItemRule
{
    /// <summary>The spell a definition's items carry, or null when they carry none this game can read.</summary>
    /// <param name="definition">The item definition to read.</param>
    /// <returns>The reading, or null when the item carries no spell.</returns>
    SpellItemReading? Reading(ItemDefinitionId definition);
}
