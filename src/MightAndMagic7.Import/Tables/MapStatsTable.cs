using MightAndMagic7.Import.Lod;

namespace MightAndMagic7.Import.Tables;

/// <summary>Per-map metadata: identity, file link, reset behaviour, and encounter settings.</summary>
/// <param name="Id">The map id the game uses to reference this place.</param>
/// <param name="Name">The map's display name.</param>
/// <param name="FileName">The map file's name inside the assets container.</param>
/// <param name="RespawnDays">Days before the map's population resets.</param>
/// <param name="AlertDays">Days the map stays alerted after the party is noticed.</param>
/// <param name="TreasureLevel">Treasure level used when generating loot here.</param>
/// <param name="EncounterPercent">Base chance of a random encounter.</param>
/// <param name="MonsterSlot1">First encounter monster name.</param>
/// <param name="MonsterSlot2">Second encounter monster name.</param>
/// <param name="MonsterSlot3">Third encounter monster name.</param>
/// <param name="Track">The music track reference.</param>
/// <param name="Environment">The audio environment reference.</param>
/// <param name="Designer">The table's designer note.</param>
/// <param name="Notes">The table's free notes for the map.</param>
public readonly record struct MapStatsRecord(
    int Id,
    string Name,
    string FileName,
    int RespawnDays,
    int AlertDays,
    int TreasureLevel,
    int EncounterPercent,
    string MonsterSlot1,
    string MonsterSlot2,
    string MonsterSlot3,
    string Track,
    string Environment,
    string Designer,
    string Notes);

/// <summary>The per-map metadata table.</summary>
public sealed class MapStatsTable
{
    /// <summary>How many rows this table's own header occupies.</summary>
    public const int HeaderRowCount = 3;

    /// <summary>How many maps the shipped table carries: thirteen outdoor regions and sixty-three interiors.</summary>
    public const int ExpectedMaps = 76;

    private MapStatsTable(TabularTable table, MapStatsRecord[] maps)
    {
        Table = table;
        Maps = maps;
    }

    /// <summary>The table this was read from.</summary>
    public TabularTable Table { get; }

    /// <summary>Every map row, in table order.</summary>
    public IReadOnlyList<MapStatsRecord> Maps { get; }

    /// <summary>
    /// Map file stems without their extension, which is how a map is named by an event program and by a
    /// travel destination. Case-insensitive, because the tables disagree on case.
    /// </summary>
    public IReadOnlyDictionary<string, int> FileStemIndex =>
        Maps.ToDictionary(map => Path.GetFileNameWithoutExtension(map.FileName), map => map.Id, StringComparer.OrdinalIgnoreCase);

    /// <summary>Reads the table from an installation.</summary>
    public static MapStatsTable Read(LodInstall install)
    {
        TabularTable table = TabularTable.Read(install, Mm7TableSources.MapStats, HeaderRowCount);
        MapStatsRecord[] maps = [.. table.Rows.Select(row => new MapStatsRecord(
            TableValue.Integer(table, row, 0, "#"),
            TableValue.Text(row, 1),
            TableValue.Text(row, 2),
            TableValue.Integer(table, row, 6, "Refil Days"),
            TableValue.Integer(table, row, 7, "Alert Days"),
            TableValue.Integer(table, row, 11, "Tres 0-6"),
            TableValue.Integer(table, row, 12, "Enc %"),
            TableValue.Text(row, 17),
            TableValue.Text(row, 21),
            TableValue.Text(row, 25),
            TableValue.Text(row, 28),
            TableValue.Text(row, 29),
            TableValue.Text(row, 30),
            TableValue.Text(row, 31)))];

        if (maps.Length != ExpectedMaps)
        {
            throw new LodFormatException($"{table.Source}: expected {ExpectedMaps} maps, read {maps.Length}.");
        }

        foreach (MapStatsRecord map in maps)
        {
            if (map.Name.Length == 0 || map.FileName.Length == 0)
            {
                throw new LodFormatException($"{table.Source}: map {map.Id} is missing its name or file name.");
            }
        }

        return new MapStatsTable(table, maps);
    }
}
