using PartyRpg.Kit.Loot;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// One treasure request as content states it: how often it gives anything, what its dice are worth, which
/// treasure level it asks for, and what it asks that level for.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the shape of a treasure rule, not its numbers.</b> A game's tables say "in one case in ten,
/// ten twenty-sided dice of coin, plus an item of the third level" in whatever words their own format
/// uses; this is the shape that statement takes once it has been read, so the mechanism that resolves it
/// never has to know the format. Reading a shipped cell into this belongs where the format is known.
/// </para>
/// <para>
/// A request with no level asks for coin alone, which is what a cell that states dice and no item is: the
/// original's own creature rows carry several such cells, and a creature that drops coin and nothing else is
/// not a creature with an unreadable treasure rule.
/// </para>
/// </remarks>
/// <param name="Chance">How often in a hundred the request gives anything at all.</param>
/// <param name="GoldRolls">How many dice of coin it rolls, none when it carries no coin.</param>
/// <param name="GoldSides">How many sides each of those dice has.</param>
/// <param name="Level">The treasure level of the item it asks for, or zero when it asks for no item.</param>
/// <param name="Filter">What it asks the level for, which is anything when it states nothing.</param>
internal sealed record TreasureRoll(int Chance, int GoldRolls, int GoldSides, int Level, LootFilter Filter)
{
    /// <summary>How many treasure levels a request may name, which is the game family's own seven.</summary>
    /// <remarks>
    /// The seventh level is the one that guarantees an artifact rather than a weighted draw, which is why a
    /// request may name it and a level's weighted table does not have to weigh it.
    /// </remarks>
    internal const int HighestLevel = 7;

    /// <summary>A request that gives nothing.</summary>
    internal static TreasureRoll Nothing { get; } = new(0, 0, 0, 0, LootFilter.Any);

    /// <summary>Whether this request asks for an item of some level.</summary>
    internal bool WantsItem => Level is >= 1 and <= HighestLevel;

    /// <summary>Whether this request rolls dice of coin.</summary>
    internal bool WantsCoins => GoldRolls > 0 && GoldSides > 0;

    /// <inheritdoc />
    public override string ToString() =>
        $"{Chance}%{(WantsCoins ? $" {GoldRolls}D{GoldSides}" : string.Empty)}{(WantsItem ? $" L{Level} {Filter}" : string.Empty)}";
}
