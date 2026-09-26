using PartyRpg.Kit.Party;

namespace PartyRpg.Kit.Loot;

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
/// donor's own creature rows carry several such cells, and a creature that drops coin and nothing else is
/// not a creature with an unreadable treasure rule.
/// </para>
/// </remarks>
/// <param name="Chance">How often in a hundred the request gives anything at all.</param>
/// <param name="GoldRolls">How many dice of coin it rolls, none when it carries no coin.</param>
/// <param name="GoldSides">How many sides each of those dice has.</param>
/// <param name="Level">The treasure level of the item it asks for, or zero when it asks for no item.</param>
/// <param name="Filter">What it asks the level for, which is anything when it states nothing.</param>
public sealed record TreasureRoll(int Chance, int GoldRolls, int GoldSides, int Level, LootFilter Filter)
{
    /// <summary>How many treasure levels a request may name, which is the game family's own seven.</summary>
    /// <remarks>
    /// The seventh level is the one that guarantees an artifact rather than a weighted draw, which is why a
    /// request may name it and a level's weighted table does not have to weigh it.
    /// </remarks>
    public const int HighestLevel = 7;

    /// <summary>A request that gives nothing.</summary>
    public static TreasureRoll Nothing { get; } = new(0, 0, 0, 0, LootFilter.Any);

    /// <summary>Whether this request asks for an item of some level.</summary>
    public bool WantsItem => Level is >= 1 and <= HighestLevel;

    /// <summary>Whether this request rolls dice of coin.</summary>
    public bool WantsCoins => GoldRolls > 0 && GoldSides > 0;

    /// <inheritdoc />
    public override string ToString() =>
        $"{Chance}%{(WantsCoins ? $" {GoldRolls}D{GoldSides}" : string.Empty)}{(WantsItem ? $" L{Level} {Filter}" : string.Empty)}";
}

/// <summary>
/// The things one treasure level can yield, as content weighs them, and the one draw that picks among them.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is content's own pool, read, not a table invented here.</b> A game's item data weighs every item
/// by how often it appears at each treasure level; the pool is those weights, and picking one is a weighted
/// walk over the candidates the level weighs at all. An item the level does not weigh is not in the pool, so
/// a level nothing weighs yields nothing rather than the whole catalog.
/// </para>
/// <para>
/// <b>The request's filter is content's own tags.</b> A treasure request that asks for a cloak narrows the
/// pool to the candidates tagged as cloaks, and one that asks for anything takes the whole pool. Whether any
/// candidate matches at all is a fact about the content, and a level a filter empties is answered as nothing
/// rather than by quietly widening the request.
/// </para>
/// <para>
/// <b>The draw is keyed.</b> Which candidate is picked is decided by the rolls it is handed, so the same key
/// picks the same thing twice, and a pool of one picks it without a draw.
/// </para>
/// </remarks>
public sealed class LootTable
{
    private readonly LootCandidate[] _candidates;

    /// <summary>Reads a level's pool from the candidates content states.</summary>
    /// <param name="candidates">Every candidate the content offers, in content's own order.</param>
    /// <exception cref="ArgumentNullException">No candidates were supplied.</exception>
    public LootTable(IEnumerable<LootCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        _candidates = [.. candidates];
    }

    /// <summary>Every candidate this table holds, in content's own order.</summary>
    public IReadOnlyList<LootCandidate> Candidates => _candidates;

    /// <summary>What one level weighs altogether for one request, nothing when it can yield nothing.</summary>
    /// <param name="level">The treasure level, counted from one.</param>
    /// <param name="filter">What the request asks for.</param>
    /// <returns>The sum of the weights of everything the level offers and the request accepts.</returns>
    public int WeightAt(int level, LootFilter filter)
    {
        int total = 0;
        foreach (LootCandidate candidate in _candidates)
        {
            if (!filter.Accepts(candidate)) continue;
            total += candidate.WeightAt(level);
        }

        return total;
    }

    /// <summary>
    /// Draws one thing from a treasure level, or answers nothing when the level offers nothing the request
    /// accepts.
    /// </summary>
    /// <param name="level">The treasure level, counted from one.</param>
    /// <param name="filter">What the request asks for.</param>
    /// <param name="rolls">The keyed rolls the pick is drawn from.</param>
    /// <returns>The candidate, or null when there is nothing to pick.</returns>
    /// <exception cref="ArgumentNullException">No rolls were supplied.</exception>
    public LootCandidate? Pick(int level, LootFilter filter, LootRolls rolls)
    {
        ArgumentNullException.ThrowIfNull(rolls);
        int total = WeightAt(level, filter);
        if (total <= 0) return null;

        // The walk is content's own order and the draw is a weight inside the level's total, so a weighted
        // table yields its heavier entries more often without this mechanism knowing what any of them are.
        int drawn = rolls.Weighted(total);
        int walked = 0;
        LootCandidate? last = null;
        foreach (LootCandidate candidate in _candidates)
        {
            if (!filter.Accepts(candidate)) continue;
            int weight = candidate.WeightAt(level);
            if (weight <= 0) continue;
            walked += weight;
            last = candidate;
            if (drawn < walked) return candidate;
        }

        // The walk is exhausted only when the weights changed under it, which nothing here can do; the last
        // candidate is the honest answer rather than a null a caller would read as an empty level.
        return last;
    }

    /// <summary>Every candidate one level offers a request, in content's own order.</summary>
    /// <param name="level">The treasure level, counted from one.</param>
    /// <param name="filter">What the request asks for.</param>
    /// <returns>The candidates that may be drawn.</returns>
    public IReadOnlyList<LootCandidate> Offered(int level, LootFilter filter)
    {
        List<LootCandidate> offered = [];
        foreach (LootCandidate candidate in _candidates)
        {
            if (candidate.WeightAt(level) > 0 && filter.Accepts(candidate)) offered.Add(candidate);
        }

        return offered;
    }

    /// <summary>Finds the candidate that names one definition, or null when the table does not carry it.</summary>
    /// <param name="definition">The definition to look for.</param>
    /// <returns>The candidate, or null.</returns>
    public LootCandidate? Find(ItemDefinitionId definition)
    {
        foreach (LootCandidate candidate in _candidates)
        {
            if (candidate.Definition == definition) return candidate;
        }

        return null;
    }
}
