namespace MightAndMagic7.Import.Maps;

/// <summary>A decoded indoor map, with its door delta when one was supplied.</summary>
/// <remarks>
/// Interior geometry is one vertex array, one face array and the sectors that own those faces. Faces do
/// not carry their vertex ids or texture coordinates directly: both live in a shared pool whose only
/// structure is the vertex count of each face, and the decoder has already resolved them. The doors
/// live in the separate delta payload, so a map decoded from its <c>.blv</c> alone has no doors rather
/// than invented ones.
/// </remarks>
public sealed class IndoorMap : DecodedMap
{
    internal IndoorMap(
        string fileName,
        int version,
        string name,
        IndoorCounts counts,
        IReadOnlyList<MapPoint> vertices,
        IReadOnlyList<MapFace> faces,
        IReadOnlyList<MapFaceExtra> faceExtras,
        IReadOnlyList<MapSector> sectors,
        IReadOnlyList<MapLight> lights,
        IReadOnlyList<MapBspNode> bspNodes,
        IReadOnlyList<MapDecoration> decorations,
        IReadOnlyList<MapEntryPoint> entryPoints,
        IReadOnlyList<MapSpawnPoint> spawnPoints,
        MapDelta? delta)
        : base(fileName)
    {
        Version = version;
        Name = name;
        Counts = counts;
        Vertices = vertices;
        Faces = faces;
        FaceExtras = faceExtras;
        Sectors = sectors;
        Lights = lights;
        BspNodes = bspNodes;
        Decorations = decorations;
        EntryPoints = entryPoints;
        SpawnPoints = spawnPoints;
        Delta = delta;

        MapBounds? bounds = null;
        foreach (MapPoint vertex in vertices)
        {
            MapBounds vertexBounds = new(vertex.X, vertex.Y, vertex.Z, vertex.X, vertex.Y, vertex.Z);
            bounds = MapBounds.Union(bounds, vertexBounds);
        }

        Bounds = bounds;
    }

    /// <inheritdoc />
    public override MapKind Kind => MapKind.Indoor;

    /// <summary>The payload's format version; every shipped interior declares 1.</summary>
    public int Version { get; }

    /// <inheritdoc />
    public override string Name { get; }

    /// <summary>The counts the payload declares.</summary>
    public IndoorCounts Counts { get; }

    /// <inheritdoc />
    public override IReadOnlyList<MapPoint> Vertices { get; }

    /// <inheritdoc />
    public override IReadOnlyList<MapFace> Faces { get; }

    /// <summary>The face extras the level's faces index.</summary>
    public IReadOnlyList<MapFaceExtra> FaceExtras { get; }

    /// <summary>The level's sectors, in payload order.</summary>
    public IReadOnlyList<MapSector> Sectors { get; }

    /// <summary>The level's lights, in payload order.</summary>
    public override IReadOnlyList<MapLight> Lights { get; }

    /// <summary>The level's BSP nodes; the tree is a rendering accelerator, not geometry.</summary>
    public IReadOnlyList<MapBspNode> BspNodes { get; }

    /// <summary>The bounding box of the level's vertices, or null when it has none.</summary>
    public MapBounds? Bounds { get; }

    /// <inheritdoc />
    public override IReadOnlyList<MapDecoration> Decorations { get; }

    /// <inheritdoc />
    public override IReadOnlyList<MapEntryPoint> EntryPoints { get; }

    /// <inheritdoc />
    public override IReadOnlyList<MapSpawnPoint> SpawnPoints { get; }

    /// <summary>The level's doors, or an empty list when it was decoded without its delta.</summary>
    public override IReadOnlyList<MapDoor> Doors => Delta?.Doors ?? [];

    /// <summary>The delta payload decoded with this map, or null when it was decoded on its own.</summary>
    public override MapDelta? Delta { get; }
}
