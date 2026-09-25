namespace MightAndMagic7.Import.Maps;

/// <summary>Totals over a set of decoded maps.</summary>
/// <param name="Maps">How many maps the totals cover.</param>
/// <param name="Faces">Total faces.</param>
/// <param name="Vertices">Total vertices.</param>
/// <param name="Doors">Total door records, including unused slots of an indoor delta.</param>
/// <param name="Lights">Total lights.</param>
/// <param name="EntryPoints">Total party arrival points.</param>
/// <param name="Decorations">Total level decorations.</param>
/// <param name="SpawnPoints">Total spawn points.</param>
/// <param name="Chests">Total chest records, including the runtime array's unplaced slots.</param>
/// <param name="SpriteObjects">Total sprite objects the deltas carry.</param>
public readonly record struct MapFamilyCounts(
    int Maps,
    int Faces,
    int Vertices,
    int Doors,
    int Lights,
    int EntryPoints,
    int Decorations,
    int SpawnPoints,
    int Chests,
    int SpriteObjects);
