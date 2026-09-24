namespace MightAndMagic7.Import.Maps;

/// <summary>A decoded map payload, with the parts both families share.</summary>
/// <remarks>
/// The two families differ in almost everything — an outdoor map is a height field with placed models,
/// an indoor map one vertex, face and sector graph — but they agree on what a traveller arrives at,
/// what the ground is, and what stands on it. Those shared members live here so a report can total
/// them across both families, and everything family-specific stays on <see cref="OutdoorMap"/> and
/// <see cref="IndoorMap"/>.
/// </remarks>
public abstract class DecodedMap
{
    /// <summary>Creates a decoded map for the container entry it was read from.</summary>
    /// <param name="fileName">The entry name the payload was read from.</param>
    protected DecodedMap(string fileName) => FileName = fileName;

    /// <summary>The container entry name the payload was read from.</summary>
    public string FileName { get; }

    /// <summary>Which family the payload belongs to.</summary>
    public abstract MapKind Kind { get; }

    /// <summary>The name the payload carries for itself.</summary>
    public abstract string Name { get; }

    /// <summary>Every vertex of the level, in payload order.</summary>
    public abstract IReadOnlyList<MapPoint> Vertices { get; }

    /// <summary>Every face of the level, in payload order.</summary>
    public abstract IReadOnlyList<MapFace> Faces { get; }

    /// <summary>Every level decoration, with its name.</summary>
    public abstract IReadOnlyList<MapDecoration> Decorations { get; }

    /// <summary>The party's arrival points, in decoration order.</summary>
    public abstract IReadOnlyList<MapEntryPoint> EntryPoints { get; }

    /// <summary>Every declared spawn point; these spawn monsters and treasure, not the party.</summary>
    public abstract IReadOnlyList<MapSpawnPoint> SpawnPoints { get; }

    /// <summary>The level's doors, or an empty list when the family or the delta has none.</summary>
    public abstract IReadOnlyList<MapDoor> Doors { get; }

    /// <summary>The level's lights, or an empty list outdoors.</summary>
    public abstract IReadOnlyList<MapLight> Lights { get; }

    /// <summary>The delta payload decoded with this map, or null when it was decoded on its own.</summary>
    public abstract MapDelta? Delta { get; }
}
