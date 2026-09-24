namespace MightAndMagic7.Import.Maps;

/// <summary>A decoded outdoor map.</summary>
/// <remarks>
/// The terrain is a 128×128 grid of 512-unit cells with its origin at the centre of the map, so grid
/// cell (0,0) is the north-west corner and world X grows east while world Y grows north. Height is
/// stored as a byte per cell and is 32 world units per step.
/// </remarks>
public sealed class OutdoorMap : DecodedMap
{
    /// <summary>Cells per side of the terrain grid.</summary>
    public const int TerrainCells = 128;

    /// <summary>World units per terrain cell.</summary>
    public const int TerrainCellSize = 512;

    /// <summary>World units per stored height step.</summary>
    public const int TerrainHeightUnit = 32;

    internal OutdoorMap(
        string fileName,
        string name,
        string payloadFileName,
        string description,
        string skyTexture,
        string groundTileset,
        IReadOnlyList<OutdoorTileType> tileTypes,
        byte[] heightMap,
        byte[] tileMap,
        OutdoorCounts counts,
        IReadOnlyList<OutdoorModel> models,
        IReadOnlyList<MapPoint> vertices,
        IReadOnlyList<MapFace> faces,
        IReadOnlyList<MapDecoration> decorations,
        IReadOnlyList<MapEntryPoint> entryPoints,
        IReadOnlyList<MapSpawnPoint> spawnPoints,
        MapDelta? delta)
        : base(fileName)
    {
        Name = name;
        PayloadFileName = payloadFileName;
        Description = description;
        SkyTexture = skyTexture;
        GroundTileset = groundTileset;
        TileTypes = tileTypes;
        HeightMap = heightMap;
        TileMap = tileMap;
        Counts = counts;
        Models = models;
        Vertices = vertices;
        Faces = faces;
        Decorations = decorations;
        EntryPoints = entryPoints;
        SpawnPoints = spawnPoints;
        Delta = delta;

        MapBounds? bounds = null;
        foreach (OutdoorModel model in models) bounds = MapBounds.Union(bounds, model.Bounds);
        Bounds = bounds;
        TerrainBounds = TerrainBoundsOf(heightMap);
    }

    /// <inheritdoc />
    public override MapKind Kind => MapKind.Outdoor;

    /// <inheritdoc />
    public override string Name { get; }

    /// <summary>The file name the payload stores for itself; every shipped map stores <c>"default.odm"</c>.</summary>
    public string PayloadFileName { get; }

    /// <summary>
    /// The payload's self-description. Every shipped Might and Magic VII map says <c>"MM6 Outdoor v7.00"</c>,
    /// and the donor documents an <c>v1.00</c> variant of the same field, so the value is reported rather
    /// than required: the seventh game's outdoor payload identifies itself as the sixth game's format.
    /// </summary>
    public string Description { get; }

    /// <summary>The sky texture's name, empty when the map declares none.</summary>
    public string SkyTexture { get; }

    /// <summary>The ground tileset's name, which the donor never reads.</summary>
    public string GroundTileset { get; }

    /// <summary>The map's four terrain tilesets.</summary>
    public IReadOnlyList<OutdoorTileType> TileTypes { get; }

    /// <summary>One height byte per terrain cell, indexed <c>gridY * 128 + gridX</c>.</summary>
    public byte[] HeightMap { get; }

    /// <summary>One tile byte per terrain cell, indexed <c>gridY * 128 + gridX</c>; only the top-left 127×127 cells are used.</summary>
    public byte[] TileMap { get; }

    /// <summary>The counts the payload declares.</summary>
    public OutdoorCounts Counts { get; }

    /// <summary>The placed models, in payload order.</summary>
    public IReadOnlyList<OutdoorModel> Models { get; }

    /// <summary>The union of the models' bounding boxes, or null when the map has no models.</summary>
    public MapBounds? Bounds { get; }

    /// <summary>The terrain's own extent, derived from the fixed grid and the height map.</summary>
    public MapBounds TerrainBounds { get; }

    /// <inheritdoc />
    public override IReadOnlyList<MapPoint> Vertices { get; }

    /// <inheritdoc />
    public override IReadOnlyList<MapFace> Faces { get; }

    /// <inheritdoc />
    public override IReadOnlyList<MapDecoration> Decorations { get; }

    /// <inheritdoc />
    public override IReadOnlyList<MapEntryPoint> EntryPoints { get; }

    /// <inheritdoc />
    public override IReadOnlyList<MapSpawnPoint> SpawnPoints { get; }

    /// <summary>Doors; always empty, because an outdoor map has none — they live in the indoor delta.</summary>
    public override IReadOnlyList<MapDoor> Doors => [];

    /// <summary>Lights; always empty, because an outdoor map has none — they live in the indoor payload.</summary>
    public override IReadOnlyList<MapLight> Lights => [];

    /// <summary>The delta payload decoded with this map, or null when it was decoded on its own.</summary>
    public override MapDelta? Delta { get; }

    /// <summary>The world height of a terrain cell, in engine units.</summary>
    /// <param name="gridX">Cell column, 0 at the western edge.</param>
    /// <param name="gridY">Cell row, 0 at the northern edge.</param>
    public int TerrainHeightAt(int gridX, int gridY) => TerrainHeightUnit * HeightMap[CellIndex(gridX, gridY)];

    /// <summary>The north-west corner of a terrain cell, in world coordinates.</summary>
    /// <param name="gridX">Cell column, 0 at the western edge.</param>
    /// <param name="gridY">Cell row, 0 at the northern edge.</param>
    public MapPoint CellToWorld(int gridX, int gridY)
    {
        // The arithmetic shift rounds a negative coordinate down, which is what the cell grid means:
        // cell (64, 63) starts at the origin and the terrain extends half a map in each direction.
        int x = (gridX - (TerrainCells / 2)) << 9;
        int y = ((TerrainCells / 2) - gridY) << 9;
        return new MapPoint(x, y, TerrainHeightUnit * HeightMap[CellIndex(gridX, gridY)]);
    }

    private static int CellIndex(int gridX, int gridY)
    {
        if (gridX < 0 || gridX >= TerrainCells) throw new ArgumentOutOfRangeException(nameof(gridX));
        if (gridY < 0 || gridY >= TerrainCells) throw new ArgumentOutOfRangeException(nameof(gridY));
        return (gridY * TerrainCells) + gridX;
    }

    private static MapBounds TerrainBoundsOf(byte[] heightMap)
    {
        int minZ = int.MaxValue;
        int maxZ = int.MinValue;
        foreach (byte height in heightMap)
        {
            int z = TerrainHeightUnit * height;
            if (z < minZ) minZ = z;
            if (z > maxZ) maxZ = z;
        }

        int west = -(TerrainCells / 2) * TerrainCellSize;
        int east = ((TerrainCells / 2) * TerrainCellSize) - TerrainCellSize;
        int south = -((TerrainCells / 2) * TerrainCellSize) + TerrainCellSize;
        int north = (TerrainCells / 2) * TerrainCellSize;
        return new MapBounds(west, south, minZ, east, north, maxZ);
    }
}
