using MightAndMagic7.Import.Lod;

namespace MightAndMagic7.Import.Tables;

/// <summary>One of a map's three random-encounter slots, as the map table states it.</summary>
/// <remarks>
/// <para>
/// A slot names the kind of monster the map puts on the field (its internal monster name, which the
/// monster table's own picture column repeats), how hard the grade odds lean at the map's difficulty,
/// and how many creatures appear when the level spawns from this slot.
/// </para>
/// <para>
/// The count is the range the donor draws a number from when a spawn point names one of the three
/// random slots (<c>OpenEnroth src/Engine/Objects/Actor.cpp:4244-4262</c>, the <c>encounterNMinCount</c>
/// and <c>encounterNMaxCount</c> pair read at <c>src/Engine/Tables/MapTable.cpp:84-89</c>); a spawn
/// naming a graded slot puts exactly one creature on the field, which is why the donor keeps its count
/// at one unless the slot is one of the three random ones.
/// </para>
/// </remarks>
/// <param name="Monster">The kind of monster the slot spawns, empty or zero when the map uses no such slot.</param>
/// <param name="Difficulty">How hard the map's grade odds lean: the donor's <c>Dif</c> column, zero to five.</param>
/// <param name="Minimum">The fewest creatures the slot spawns, zero when it states no count.</param>
/// <param name="Maximum">The most creatures the slot spawns, zero when it states no count.</param>
public readonly record struct EncounterSlot(string Monster, int Difficulty, int Minimum, int Maximum)
{
    /// <summary>Whether the map uses no such slot, which is the table's own zero or an empty cell.</summary>
    public bool IsEmpty => Monster.Length == 0 || Monster == "0";

    /// <summary>Whether the slot states a range a spawn can draw a number of creatures from.</summary>
    public bool HasCount => Minimum > 0 && Maximum >= Minimum;
}

/// <summary>Per-map metadata: identity, file link, reset behaviour, and encounter settings.</summary>
/// <param name="Id">The map id the game uses to reference this place.</param>
/// <param name="Name">The map's display name.</param>
/// <param name="FileName">The map file's name inside the assets container.</param>
/// <param name="RespawnDays">Days before the map's population resets.</param>
/// <param name="AlertDays">Days the map stays alerted after the party is noticed.</param>
/// <param name="TreasureLevel">Treasure level used when generating loot here.</param>
/// <param name="EncounterPercent">Base chance of a random encounter.</param>
/// <param name="Slot1">The map's first encounter slot.</param>
/// <param name="Slot2">The map's second encounter slot.</param>
/// <param name="Slot3">The map's third encounter slot.</param>
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
    EncounterSlot Slot1,
    EncounterSlot Slot2,
    EncounterSlot Slot3,
    string Track,
    string Environment,
    string Designer,
    string Notes)
{
    /// <summary>The map's encounter slots, in the table's own order.</summary>
    public IReadOnlyList<EncounterSlot> Slots => [Slot1, Slot2, Slot3];
}

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

    /// <summary>The table this was read from, whose source names the file in an error.</summary>
    private LodSource Source => Table.Source;

    /// <summary>The table this was read from.</summary>
    public TabularTable Table { get; }

    /// <summary>Every map row, in table order.</summary>
    public IReadOnlyList<MapStatsRecord> Maps { get; }

    /// <summary>
    /// Map file stems without their extension, which is how a map is named by an event program and by a
    /// travel destination. Case-insensitive, because the tables disagree on case.
    /// </summary>
    public IReadOnlyDictionary<string, int> FileStemIndex
    {
        get
        {
            Dictionary<string, int> index = new(StringComparer.OrdinalIgnoreCase);
            foreach (MapStatsRecord map in Maps)
            {
                string stem = Path.GetFileNameWithoutExtension(map.FileName);
                if (!index.TryAdd(stem, map.Id))
                {
                    throw new LodFormatException(
                        $"{Source}: maps {index[stem]} and {map.Id} both name the file '{map.FileName}', so a map cannot be identified by it.");
                }
            }

            return index;
        }
    }

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
            Slot(table, row, 17, 18, 19, "Mon 1"),
            Slot(table, row, 21, 22, 23, "Mon 2"),
            Slot(table, row, 25, 26, 27, "Mon 3"),
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

    /// <summary>
    /// Reads one encounter slot: the kind it spawns, the difficulty its grade odds are read at, and the
    /// range of creatures it spawns.
    /// </summary>
    /// <remarks>
    /// The range cell is written as a dash pair with padding (<c>" 2-5"</c>) or as a single number, and
    /// the donor reads it by splitting on the dash and trimming each half
    /// (<c>OpenEnroth src/Engine/Tables/MapTable.cpp:65-75</c>). An unused slot still states its own
    /// range, so a slot nothing spawns from keeps its numbers and is recognised by its empty name.
    /// </remarks>
    private static EncounterSlot Slot(TabularTable table, TabularRow row, int nameColumn, int difficultyColumn, int countColumn, string columnName)
    {
        string range = row.Field(countColumn).Trim();
        int dash = range.IndexOf('-', StringComparison.Ordinal);
        int minimum = 0;
        int maximum = 0;
        if (range.Length > 0)
        {
            string low = dash >= 0 ? range[..dash].Trim() : range;
            string high = dash >= 0 ? range[(dash + 1)..].Trim() : range;
            if (!int.TryParse(low, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out minimum) ||
                !int.TryParse(high, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out maximum))
            {
                throw new LodFormatException(
                    $"{table.Source}: row {row.Number} column '{columnName}' states its count as '{range}', which is neither a number nor a range.");
            }
        }

        return new EncounterSlot(
            TableValue.Text(row, nameColumn),
            TableValue.Integer(table, row, difficultyColumn, columnName),
            minimum,
            maximum);
    }
}
