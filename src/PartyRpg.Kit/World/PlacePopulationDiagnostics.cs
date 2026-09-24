namespace PartyRpg.Kit.World;

/// <summary>
/// What a population holds right now: where its entities are, how many the engine's store holds, and
/// how many of each content kind are alive.
/// </summary>
/// <remarks>
/// The entity count is read from the store rather than from the population's own list, so a leak — an
/// entity that outlived the visit that created it — shows up here even if the population's bookkeeping
/// agreed with itself. Diagnostics are an observation: nothing in the world depends on them.
/// </remarks>
/// <param name="Place">The place the population stands in, or null before it entered one.</param>
/// <param name="EntityCount">How many entities the store holds, which is zero when nothing is alive.</param>
/// <param name="ByKind">How many live entities each content kind has.</param>
public sealed record PlacePopulationDiagnostics(
    PlaceId? Place,
    int EntityCount,
    IReadOnlyDictionary<string, int> ByKind)
{
    /// <summary>How many entities of one content kind are alive, zero when none are.</summary>
    /// <param name="kind">The content kind to count.</param>
    public int CountOf(string kind)
    {
        ArgumentNullException.ThrowIfNull(kind);
        return ByKind.TryGetValue(kind, out int count) ? count : 0;
    }
}
