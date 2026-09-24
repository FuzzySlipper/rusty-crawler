namespace MightAndMagic7.Import.Maps;

/// <summary>The counts an indoor payload declares.</summary>
/// <remarks>
/// The four byte sizes are the payload's own declared pool sizes, not the sizes the walk consumed, so a
/// payload whose pools do not match its records is visible. The door slot count is the level's door
/// capacity: the door records themselves live in the delta, and only their number is stored here.
/// </remarks>
/// <param name="Version">The payload's format version; every shipped interior declares 1.</param>
/// <param name="FaceDataSizeBytes">Declared size of the shared face data pool.</param>
/// <param name="SectorDataSizeBytes">Declared size of the shared sector data pool.</param>
/// <param name="SectorLightDataSizeBytes">Declared size of the shared sector light pool.</param>
/// <param name="DoorsDataSizeBytes">Declared size of the door pool, which lives in the delta.</param>
/// <param name="VertexCount">How many vertices the payload declares.</param>
/// <param name="FaceCount">How many faces the payload declares.</param>
/// <param name="FaceExtraCount">How many face extras the payload declares.</param>
/// <param name="SectorCount">How many sectors the payload declares.</param>
/// <param name="DoorSlotCount">How many door slots the level has; every shipped interior has 200.</param>
/// <param name="DecorationCount">How many decorations the payload declares.</param>
/// <param name="LightCount">How many lights the payload declares.</param>
/// <param name="BspNodeCount">How many BSP nodes the payload declares.</param>
/// <param name="SpawnPointCount">How many spawn points the payload declares.</param>
/// <param name="MapOutlineCount">How many minimap outlines the payload declares.</param>
public readonly record struct IndoorCounts(
    int Version,
    int FaceDataSizeBytes,
    int SectorDataSizeBytes,
    int SectorLightDataSizeBytes,
    int DoorsDataSizeBytes,
    int VertexCount,
    int FaceCount,
    int FaceExtraCount,
    int SectorCount,
    int DoorSlotCount,
    int DecorationCount,
    int LightCount,
    int BspNodeCount,
    int SpawnPointCount,
    int MapOutlineCount);
