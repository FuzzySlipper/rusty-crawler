using MightAndMagic7.Import.Tables;

namespace MightAndMagic7.Import.Maps;

/// <summary>The result of decoding every map the per-map table names.</summary>
/// <remarks>
/// A map that fails is kept as a failure with its id, entry name, and the field and offset that stopped
/// it, so an import reports what it could not read instead of presenting a short list as a complete one.
/// The totals count decoded maps only.
/// </remarks>
public sealed class MapDecodeReport
{
    private MapDecodeReport(MapDecodeOutcome[] outcomes, MapDecodeOutcome[] decoded, MapDecodeFailure[] failures)
    {
        Outcomes = outcomes;
        Decoded = decoded;
        Failures = failures;
        Outdoor = Counts(outcomes, MapKind.Outdoor);
        Indoor = Counts(outcomes, MapKind.Indoor);
        Total = new MapFamilyCounts(
            Outdoor.Maps + Indoor.Maps,
            Outdoor.Faces + Indoor.Faces,
            Outdoor.Vertices + Indoor.Vertices,
            Outdoor.Doors + Indoor.Doors,
            Outdoor.Lights + Indoor.Lights,
            Outdoor.EntryPoints + Indoor.EntryPoints,
            Outdoor.Decorations + Indoor.Decorations,
            Outdoor.SpawnPoints + Indoor.SpawnPoints,
            Outdoor.Chests + Indoor.Chests,
            Outdoor.SpriteObjects + Indoor.SpriteObjects);
    }

    /// <summary>Every map's outcome, in the per-map table's order.</summary>
    public IReadOnlyList<MapDecodeOutcome> Outcomes { get; }

    /// <summary>The outcomes that decoded.</summary>
    public IReadOnlyList<MapDecodeOutcome> Decoded { get; }

    /// <summary>The maps that did not decode.</summary>
    public IReadOnlyList<MapDecodeFailure> Failures { get; }

    /// <summary>How many maps the attempt covered.</summary>
    public int MapCount => Outcomes.Count;

    /// <summary>How many maps decoded.</summary>
    public int DecodedCount => Decoded.Count;

    /// <summary>How many maps failed.</summary>
    public int FailureCount => Failures.Count;

    /// <summary>Totals over the decoded outdoor maps.</summary>
    public MapFamilyCounts Outdoor { get; }

    /// <summary>Totals over the decoded indoor maps.</summary>
    public MapFamilyCounts Indoor { get; }

    /// <summary>Totals over every decoded map.</summary>
    public MapFamilyCounts Total { get; }

    internal static MapDecodeReport Create(IReadOnlyList<MapDecodeOutcome> outcomes)
    {
        ArgumentNullException.ThrowIfNull(outcomes);
        List<MapDecodeOutcome> decoded = [];
        List<MapDecodeFailure> failures = [];
        foreach (MapDecodeOutcome outcome in outcomes)
        {
            if (outcome.Decoded is not null) decoded.Add(outcome);
            else failures.Add(outcome.Failure!);
        }

        return new MapDecodeReport([.. outcomes], [.. decoded], [.. failures]);
    }

    private static MapFamilyCounts Counts(IReadOnlyList<MapDecodeOutcome> outcomes, MapKind kind)
    {
        int maps = 0;
        int faces = 0;
        int vertices = 0;
        int doors = 0;
        int lights = 0;
        int entryPoints = 0;
        int decorations = 0;
        int spawnPoints = 0;
        int chests = 0;
        int spriteObjects = 0;
        foreach (MapDecodeOutcome outcome in outcomes)
        {
            if (outcome.Decoded is not DecodedMap map || map.Kind != kind) continue;

            maps++;
            faces += map.Faces.Count;
            vertices += map.Vertices.Count;
            doors += map.Doors.Count;
            lights += map.Lights.Count;
            entryPoints += map.EntryPoints.Count;
            decorations += map.Decorations.Count;
            spawnPoints += map.SpawnPoints.Count;
            chests += map.Delta?.ChestCount ?? 0;
            spriteObjects += map.Delta?.SpriteObjectCount ?? 0;
        }

        return new MapFamilyCounts(maps, faces, vertices, doors, lights, entryPoints, decorations, spawnPoints, chests, spriteObjects);
    }
}
