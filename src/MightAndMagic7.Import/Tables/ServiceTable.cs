using MightAndMagic7.Import.Lod;

namespace MightAndMagic7.Import.Tables;

/// <summary>One building record: a service, a house, or an entrance marker on a map.</summary>
/// <param name="Id">The building id.</param>
/// <param name="TypeSequence">The building's position among buildings of its own type.</param>
/// <param name="Type">The free-text type string.</param>
/// <param name="MapId">The map the building stands on, absent on the table's unused rows.</param>
/// <param name="Name">The building's name.</param>
/// <param name="Proprietor">The proprietor's name.</param>
/// <param name="Title">The proprietor's title.</param>
/// <param name="PriceMultiplier">The shop's price multiplier, absent on the three rows that carry none.</param>
/// <param name="SkillPriceMultiplier">The shop's skill and spell price multiplier, absent when the row is not a shop.</param>
/// <param name="StockIntervalDays">Days between stock refreshes; absent when the row has no stock.</param>
/// <param name="MaximumTrainableLevelText">The training cap column exactly as the table stores it.</param>
/// <param name="MaximumTrainableLevel">The training cap when the column is a number; null when it is not.</param>
/// <param name="OpenHour">The hour the building opens; absent when the row is not a building with hours.</param>
/// <param name="ClosedHour">The hour the building closes; absent when the row is not a building with hours.</param>
public readonly record struct ServiceRecord(
    int Id,
    int? TypeSequence,
    string Type,
    int? MapId,
    string Name,
    string Proprietor,
    string Title,
    double? PriceMultiplier,
    double? SkillPriceMultiplier,
    int? StockIntervalDays,
    string MaximumTrainableLevelText,
    int? MaximumTrainableLevel,
    int? OpenHour,
    int? ClosedHour);

/// <summary>
/// The building table, and the classification the engine itself applies to a building's type string.
/// </summary>
public sealed class ServiceTable
{
    /// <summary>How many rows this table's own header occupies.</summary>
    public const int HeaderRowCount = 2;

    /// <summary>How many building rows the shipped table carries.</summary>
    public const int ExpectedRows = 525;

    /// <summary>
    /// The type strings the engine classifies as a service. Everything else in the table is a house or
    /// an entrance marker, which matters for placement rather than for service behaviour.
    /// </summary>
    public static readonly IReadOnlyList<string> RecognizedTypes =
    [
        "Tavern",
        "Temple",
        "Weapon Shop",
        "Armor Shop",
        "Magic Shop",
        "Training",
        "Alchemist",
        "Boats",
        "Bank",
        "Stables",
        "Town Hall",
        "Fire Guild",
        "Air Guild",
        "Water Guild",
        "Earth Guild",
        "Spirit Guild",
        "Mind Guild",
        "Body Guild",
        "Light Guild",
        "Dark Guild",
        "Self Guild",
    ];

    private ServiceTable(TabularTable table, ServiceRecord[] buildings)
    {
        Table = table;
        Buildings = buildings;
    }

    /// <summary>The table this was read from.</summary>
    public TabularTable Table { get; }

    /// <summary>Every building row, in table order.</summary>
    public IReadOnlyList<ServiceRecord> Buildings { get; }

    /// <summary>
    /// Training halls that declare no numeric cap, which the table writes as free text rather than a
    /// number. The ruleset reads the text; the importer only carries it faithfully.
    /// </summary>
    public IEnumerable<ServiceRecord> UncappedTrainingHalls =>
        Buildings.Where(building => building.MaximumTrainableLevelText.Length > 0 && building.MaximumTrainableLevel is null);

    /// <summary>Rows the table reserves without naming a building; they are ids, not places.</summary>
    public IEnumerable<ServiceRecord> PlaceholderRows =>
        Buildings.Where(building => building.Name.Length == 0 && building.Type.Length == 0);

    /// <summary>The buildings whose type the engine recognises as a service.</summary>
    public IEnumerable<ServiceRecord> Services => Buildings.Where(building =>
        building.MapId is not null && RecognizedTypes.Contains(building.Type, StringComparer.OrdinalIgnoreCase));

    /// <summary>Reads the table from an installation.</summary>
    public static ServiceTable Read(LodInstall install)
    {
        TabularTable table = TabularTable.Read(install, Mm7TableSources.Services, HeaderRowCount);
        (IReadOnlyList<TabularRow> data, _) = table.Partition();
        ServiceRecord[] buildings = [.. data.Select(row => new ServiceRecord(
            TableValue.Integer(table, row, 0, "#"),
            TableValue.OptionalInteger(table, row, 1, "#"),
            TableValue.Text(row, 2),
            TableValue.OptionalInteger(table, row, 3, "Map"),
            TableValue.Text(row, 5),
            TableValue.Text(row, 6),
            TableValue.Text(row, 7),
            TableValue.OptionalDecimal(table, row, 12, "Val"),
            TableValue.OptionalDecimal(table, row, 13, "A"),
            TableValue.OptionalInteger(table, row, 15, "C"),
            TableValue.Text(row, 17),
            TableValue.NumericOrNull(row, 17),
            TableValue.OptionalInteger(table, row, 18, "Open"),
            TableValue.OptionalInteger(table, row, 19, "Closed")))];

        if (buildings.Length != ExpectedRows)
        {
            throw new LodFormatException($"{table.Source}: expected {ExpectedRows} buildings, read {buildings.Length}.");
        }

        return new ServiceTable(table, buildings);
    }
}
