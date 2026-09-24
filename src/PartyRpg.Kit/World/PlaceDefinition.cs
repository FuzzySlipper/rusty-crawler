using PartyRpg.Kit.Content;

namespace PartyRpg.Kit.World;

/// <summary>
/// One place: its identity, its kind, where the party can arrive in it, and the content entry it came
/// from.
/// </summary>
/// <remarks>
/// The content entry is kept so a ruleset can read the fields this layer has no opinion about —
/// encounter settings, music, a map file reference — without the kit growing a vocabulary for them.
/// </remarks>
/// <param name="Id">The place's identity.</param>
/// <param name="Kind">Whether the place is a region or an interior.</param>
/// <param name="Name">The place's display name.</param>
/// <param name="EntryPoints">The spots the party can arrive at, in content order.</param>
/// <param name="Source">The content entry the place was read from.</param>
public sealed record PlaceDefinition(
    PlaceId Id,
    PlaceKind Kind,
    string Name,
    IReadOnlyList<PlaceEntryPoint> EntryPoints,
    ContentEntry Source)
{
    /// <summary>Finds an arrival point by id, ignoring case, or null when the place has none.</summary>
    public PlaceEntryPoint? FindEntryPoint(string entryPointId) =>
        EntryPoints.FirstOrDefault(point => string.Equals(point.Id, entryPointId, StringComparison.OrdinalIgnoreCase));
}
