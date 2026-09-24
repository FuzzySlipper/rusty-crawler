namespace MightAndMagic7.Import.Maps;

/// <summary>One of an outdoor map's four terrain tilesets.</summary>
/// <remarks>
/// The tile offset is recomputed by the game from the tileset, so the stored one is informational; it
/// is kept because it is part of the payload and a re-encoded map would write it back.
/// </remarks>
/// <param name="Tileset">The tileset's index.</param>
/// <param name="TileOffset">The stored offset of the tileset's first tile.</param>
public readonly record struct OutdoorTileType(int Tileset, int TileOffset);
