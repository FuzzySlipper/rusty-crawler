using System.Buffers.Binary;
using System.Text;
using MightAndMagic7.Import.Lod;

namespace MightAndMagic7.Import.Maps;

/// <summary>
/// The game's terrain tile table: which outdoor squares are water, and which normalized ground they carry.
/// </summary>
/// <remarks>
/// <para>
/// <b>The layout.</b> <c>dtile.bin</c> in <c>Events.lod</c> is a 32-bit record count followed by that many 26-byte
/// records: a sixteen-byte texture name, two unused words, the tileset, the variant, and the flags (OpenEnroth
/// <c>src/Engine/Snapshots/EntitySnapshots.h:189-197</c>, <c>TileData_MM7</c>, read whole by
/// <c>src/Engine/Snapshots/TableSerialization.cpp:119-137</c>). The operator's own copy holds 882 records, and its size
/// is exactly four bytes plus 882 records, which is what <see cref="Read"/> requires. A tile is water when its flags
/// carry <c>TILE_WATER</c>, 0x2 (<c>src/Engine/Data/TileEnums.h:9</c>); a shoreline drawn over water carries
/// <c>TILE_SHORE</c> instead and is land.
/// </para>
/// <para>
/// <b>From a map's tile byte to a record.</b> A map stores one byte per terrain square and four tilesets. A byte below
/// 90 is a record index as it stands; 90 to 233 is an offset into one of the four tilesets, 36 bytes each, counted from
/// that tileset's base tile; and 234 and above is no tile at all (OpenEnroth
/// <c>src/Engine/Snapshots/CompositeSnapshots.cpp:513-530</c>, <c>mapToGlobalTileId</c>). A tileset's base tile is the
/// first record with a name whose tileset reads as the same one and whose variant is the base, which is the
/// <c>(tileset, base)</c> lookup the donor builds keeping the first of any duplicates
/// (<c>CompositeSnapshots.cpp:532-537</c>, <c>TableSerialization.cpp:123-134</c>). Several stored tilesets read as
/// another — a cooled lava, a tropical, and most roads read as dirt, a city as sand — and the reading is the donor's
/// (<c>src/Engine/Snapshots/EnumSnapshots.cpp:65-97</c>).
/// </para>
/// </remarks>
public sealed class TerrainTileTable
{
    /// <summary>Where the table lives.</summary>
    public static LodSource Source { get; } = new("terrain-tiles", "Events.lod", "dtile.bin");

    /// <summary>How long one stored record is.</summary>
    private const int RecordSize = 26;

    /// <summary>The flag a water tile carries, <c>TILE_WATER</c>.</summary>
    private const ushort WaterFlag = 0x2;

    /// <summary>The first map tile byte that is an offset into a tileset rather than a record index.</summary>
    private const int FirstTilesetByte = 90;

    /// <summary>How many tile bytes one tileset spans.</summary>
    private const int TilesetSpan = 36;

    /// <summary>The first map tile byte that is no tile at all.</summary>
    private const int FirstInvalidByte = FirstTilesetByte + (4 * TilesetSpan);

    /// <summary>The stored variant every tileset's base tile has.</summary>
    private const ushort BaseVariant = 0;

    /// <summary>The stored tileset a record with no tileset has, which reads as none.</summary>
    private const ushort NoTileset = 255;

    private readonly Tile[] _tiles;

    private TerrainTileTable(Tile[] tiles) => _tiles = tiles;

    /// <summary>How many records the table holds.</summary>
    public int Count => _tiles.Length;

    /// <summary>Reads the table from the installation.</summary>
    /// <param name="install">The operator's installation.</param>
    public static TerrainTileTable Read(LodInstall install)
    {
        ArgumentNullException.ThrowIfNull(install);
        return Read(install.Read(Source).Bytes);
    }

    /// <summary>Reads the table from its decompressed bytes.</summary>
    /// <param name="bytes">The table's bytes.</param>
    /// <exception cref="LodFormatException">The bytes are not a count followed by exactly that many records.</exception>
    public static TerrainTileTable Read(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length < 4)
        {
            throw new LodFormatException(LodFault.Truncated, $"{Source.EntryName} holds {bytes.Length} bytes, too few for its record count.");
        }

        int count = BinaryPrimitives.ReadInt32LittleEndian(bytes);
        if (count < 0 || (long)count * RecordSize != bytes.Length - 4)
        {
            throw new LodFormatException(
                LodFault.Count,
                $"{Source.EntryName} declares {count} records of {RecordSize} bytes, and holds {bytes.Length - 4} bytes after the count.");
        }

        Tile[] tiles = new Tile[count];
        for (int index = 0; index < count; index++)
        {
            ReadOnlySpan<byte> record = bytes.AsSpan(4 + (index * RecordSize), RecordSize);
            int nameLength = record[..16].IndexOf((byte)0);
            string name = Encoding.ASCII.GetString(record[..(nameLength < 0 ? 16 : nameLength)]);
            tiles[index] = new Tile(
                name,
                BinaryPrimitives.ReadUInt16LittleEndian(record[20..]),
                BinaryPrimitives.ReadUInt16LittleEndian(record[22..]),
                BinaryPrimitives.ReadUInt16LittleEndian(record[24..]));
        }

        return new TerrainTileTable(tiles);
    }

    /// <summary>The water squares of one outdoor map, by column and row.</summary>
    /// <param name="map">The decoded map.</param>
    /// <returns>Whether each of the map's terrain squares is water, indexed <c>row * 127 + column</c>.</returns>
    public bool[] WaterSquares(OutdoorMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        int[] bases = [.. map.TileTypes.Select(type => BaseOf(type.Tileset))];
        int squares = OutdoorMap.TerrainCells - 1;
        bool[] water = new bool[squares * squares];
        for (int row = 0; row < squares; row++)
        {
            for (int column = 0; column < squares; column++)
            {
                int tile = Global(bases, map.TileMap[(row * OutdoorMap.TerrainCells) + column]);
                water[(row * squares) + column] = tile >= 0 && tile < _tiles.Length && (_tiles[tile].Flags & WaterFlag) != 0;
            }
        }

        return water;
    }

    /// <summary>The normalized ground words of a region, south-to-north row order.</summary>
    /// <remarks>
    /// Resolves the same records as water. Stored tileset folding remains here, not in runtime policy
    /// (OpenEnroth EnumSnapshots.cpp:65-97). Cell (0,0) starts at (-32768,-32256), with 127 squares
    /// each way; this reverses source rows without changing OutdoorTerrain.h:21-41's boundary reading.
    /// </remarks>
    public string[] GroundSquares(OutdoorMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        int[] bases = [.. map.TileTypes.Select(type => BaseOf(type.Tileset))];
        int squares = OutdoorMap.TerrainCells - 1;
        string[] grounds = new string[squares * squares];
        for (int row = 0; row < squares; row++)
        {
            for (int column = 0; column < squares; column++)
            {
                int tile = Global(bases, map.TileMap[((squares - 1 - row) * OutdoorMap.TerrainCells) + column]);
                int kind = tile >= 0 && tile < _tiles.Length ? Reading(_tiles[tile].Tileset) : NoTileset;
                grounds[(row * squares) + column] = kind switch
                {
                    0 => "grass", 1 => "snow", 2 => "desert", 4 => "dirt", 5 => "water",
                    6 => "badlands", 7 => "swamp", 10 => "road", _ => "default",
                };
            }
        }
        return grounds;
    }

    /// <summary>The record one map tile byte names, given the map's tileset bases; zero is no tile.</summary>
    private static int Global(int[] bases, byte local)
    {
        if (local < FirstTilesetByte) return local;
        if (local >= FirstInvalidByte) return 0;
        int tileset = (local - FirstTilesetByte) / TilesetSpan;
        return tileset < bases.Length ? bases[tileset] + ((local - FirstTilesetByte) % TilesetSpan) : 0;
    }

    /// <summary>The record a stored tileset's base tile is, or zero when the table has none.</summary>
    private int BaseOf(int storedTileset)
    {
        int wanted = Reading(storedTileset);
        if (wanted == NoTileset) return 0;
        for (int index = 0; index < _tiles.Length; index++)
        {
            Tile tile = _tiles[index];
            if (tile.Name.Length > 0 && tile.Variant == BaseVariant && Reading(tile.Tileset) == wanted) return index;
        }

        return 0;
    }

    /// <summary>
    /// The tileset a stored tileset reads as: the donor's own reading, which folds the tilesets the data files fill with
    /// dirt or sand into those, and refuses the two road sets the game never ships.
    /// </summary>
    private static int Reading(int stored) => stored switch
    {
        0 or 1 or 2 or 4 or 5 or 6 or 7 or 10 => stored,
        3 or 8 or 11 or 12 or 13 or 16 or 17 or (>= 22 and <= 28) => 4,
        9 => 2,
        _ => NoTileset,
    };

    /// <summary>One stored record, as far as this importer reads it.</summary>
    private readonly record struct Tile(string Name, ushort Tileset, ushort Variant, ushort Flags);
}
