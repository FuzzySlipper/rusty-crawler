using MightAndMagic7.Import.Lod;

namespace MightAndMagic7.Import.Tables;

/// <summary>One monster row, with the combat columns this importer understands typed and the rest kept.</summary>
/// <param name="Id">The monster id.</param>
/// <param name="Name">The monster's name.</param>
/// <param name="Level">The monster's level.</param>
/// <param name="HitPoints">The monster's hit points.</param>
/// <param name="ArmorClass">The monster's armor class.</param>
/// <param name="Experience">Experience awarded for defeating it.</param>
/// <param name="Treasure">The treasure dice string the table carries.</param>
/// <param name="Fly">Whether the monster flies.</param>
/// <param name="Movement">The monster's movement class.</param>
/// <param name="AiType">The monster's AI class.</param>
/// <param name="Hostility">The monster's hostility value.</param>
/// <param name="Speed">The monster's speed.</param>
/// <param name="Recovery">The monster's recovery time.</param>
/// <param name="Fields">Every field of the row, for the columns this importer does not type yet.</param>
public readonly record struct MonsterRecord(
    int Id,
    string Name,
    int Level,
    int HitPoints,
    int ArmorClass,
    int Experience,
    string Treasure,
    string Fly,
    string Movement,
    string AiType,
    int Hostility,
    int Speed,
    int Recovery,
    IReadOnlyList<string> Fields);

/// <summary>The monster table.</summary>
public sealed class MonsterTable
{
    /// <summary>How many rows this table's own header occupies.</summary>
    public const int HeaderRowCount = 3;

    /// <summary>How many monsters the shipped table carries.</summary>
    public const int ExpectedRows = 276;

    private MonsterTable(TabularTable table, MonsterRecord[] monsters, TabularRow[] annotations)
    {
        Table = table;
        Monsters = monsters;
        AnnotationRows = annotations;
    }

    /// <summary>The table this was read from.</summary>
    public TabularTable Table { get; }

    /// <summary>Every monster row, in table order.</summary>
    public IReadOnlyList<MonsterRecord> Monsters { get; }

    /// <summary>Rows the table writes without an id, which are legend text rather than monsters.</summary>
    public IReadOnlyList<TabularRow> AnnotationRows { get; }

    /// <summary>Reads the table from an installation.</summary>
    public static MonsterTable Read(LodInstall install)
    {
        TabularTable table = TabularTable.Read(install, Mm7TableSources.Monsters, HeaderRowCount);
        (IReadOnlyList<TabularRow> data, IReadOnlyList<TabularRow> annotations) = table.Partition();
        MonsterRecord[] monsters = [.. data.Select(row => new MonsterRecord(
            TableValue.Integer(table, row, 0, "#"),
            TableValue.Text(row, 1),
            TableValue.Integer(table, row, 3, "LVL"),
            TableValue.ThousandsInteger(table, row, 4, "HP"),
            TableValue.Integer(table, row, 5, "AC"),
            TableValue.ThousandsInteger(table, row, 6, "EXP"),
            TableValue.Text(row, 7),
            TableValue.Text(row, 9),
            TableValue.Text(row, 10),
            TableValue.Text(row, 11),
            TableValue.Integer(table, row, 12, "Hst"),
            TableValue.Integer(table, row, 13, "Spd"),
            TableValue.Integer(table, row, 14, "Rec"),
            row.Fields))];

        if (monsters.Length != ExpectedRows)
        {
            throw new LodFormatException($"{table.Source}: expected {ExpectedRows} monsters, read {monsters.Length}.");
        }

        return new MonsterTable(table, monsters, [.. annotations]);
    }
}
