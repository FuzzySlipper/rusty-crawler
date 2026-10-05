namespace PartyRpg.Kit.Party;

/// <summary>
/// A game's reading of one carried item as a player inspects it: the word for what it is and the facts about this
/// instance — what it does, what it is worth, what state it is in — in the game's own words.
/// </summary>
/// <remarks>
/// The kit counts items and keeps their custody; what a sword's damage reads as, or whether an unidentified ring says
/// anything at all, is the game's. A screen shows the facts in the order given and works nothing out from them.
/// </remarks>
public interface IItemReadingRule
{
    /// <summary>Reads one item instance.</summary>
    /// <param name="item">The instance, wherever it is held.</param>
    /// <returns>What a player is told about it.</returns>
    ItemReading Read(ItemInstance item);

    /// <summary>The content key an item's picture is drawn with, or null when the game draws it with none.</summary>
    /// <param name="definition">The item's definition, which alone decides its picture — on a shelf or in the pack.</param>
    string? PictureOf(ItemDefinitionId definition);
}

/// <summary>What a player is told about one item.</summary>
/// <param name="Kind">The game's word for what it is: a sword, leather armour, a spell scroll.</param>
/// <param name="Facts">Each fact as a line a person reads, in the order the game states them.</param>
public sealed record ItemReading(string Kind, IReadOnlyList<string> Facts)
{
    /// <summary>The item's readable prose, shown intact when inspected; empty for an item without text.</summary>
    public string Text { get; init; } = string.Empty;
}
