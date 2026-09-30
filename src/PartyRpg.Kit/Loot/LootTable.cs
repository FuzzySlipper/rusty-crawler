using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Party;

namespace PartyRpg.Kit.Loot;

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
    public LootCandidate? Pick(int level, LootFilter filter, KeyedRolls rolls)
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
