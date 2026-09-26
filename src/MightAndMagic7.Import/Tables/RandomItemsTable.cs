using MightAndMagic7.Import.Lod;

namespace MightAndMagic7.Import.Tables;

/// <summary>One item's own random-loot weights, by treasure level.</summary>
/// <param name="Id">The item id the row weighs.</param>
/// <param name="Chances">How often the item appears at each treasure level, the first level first.</param>
public readonly record struct RandomItemRow(int Id, IReadOnlyList<int> Chances)
{
    /// <summary>How likely the item is at one treasure level, nothing when the level does not reach it.</summary>
    /// <param name="level">The treasure level, counted from one.</param>
    public int ChanceAt(int level) => level >= 1 && level <= Chances.Count ? Chances[level - 1] : 0;

    /// <summary>Whether any treasure level weighs this item at all.</summary>
    public bool IsDrawn => Chances.Any(chance => chance > 0);
}

/// <summary>
/// The shipped random-item table: how often each item appears at each treasure level.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is what "a random item of level <em>n</em>" draws from.</b> The table's first section weighs every
/// item by how often it appears at each of the six treasure levels a random item is drawn at, and the donor
/// reads it into the item table's own per-level chances
/// (OpenEnroth <c>src/Engine/Tables/ItemTable.cpp:201-219</c>, <c>ItemTable::LoadRandomItems</c>, whose
/// <c>section1Size</c> of 618 is the same count read here).
/// </para>
/// <para>
/// <b>The second section is not read, and that is stated rather than silent.</b> The table's lower half
/// holds the chances of the enchantments an item may carry, which is a rule this build has no owner for: a
/// generated item here carries no standard or special enchantment, and the stone that brings items and
/// enchantments is where that section's numbers are read.
/// </para>
/// <para>
/// <b>The count is checked, not assumed.</b> A short read would silently weigh fewer items and make every
/// treasure level poorer, which is a wrong answer rather than a missing one, so the row count is required to
/// be the one the shipped table carries.
/// </para>
/// </remarks>
public sealed class RandomItemsTable
{
    /// <summary>How many rows the table declares before the item data begins: its title, its group, and its header.</summary>
    public const int HeaderRowCount = 3;

    /// <summary>How many weighed item rows the shipped table carries, which is the donor's own section size.</summary>
    public const int ExpectedRows = 618;

    /// <summary>How many treasure levels a row weighs.</summary>
    public const int Levels = 6;

    private RandomItemsTable(TabularTable table, RandomItemRow[] rows, TabularRow[] annotations)
    {
        Table = table;
        Rows = rows;
        AnnotationRows = annotations;
    }

    /// <summary>The table this was read from.</summary>
    public TabularTable Table { get; }

    /// <summary>Every weighed item row, in table order.</summary>
    public IReadOnlyList<RandomItemRow> Rows { get; }

    /// <summary>Rows the table carries that weigh no item, which are its second section and its padding.</summary>
    public IReadOnlyList<TabularRow> AnnotationRows { get; }

    /// <summary>How many of the weighed items any level may actually draw.</summary>
    public int DrawnCount => Rows.Count(row => row.IsDrawn);

    /// <summary>Reads the table from an installation.</summary>
    /// <exception cref="LodFormatException">The table does not carry the rows the shipped one does, or a row weighs no level.</exception>
    public static RandomItemsTable Read(LodInstall install)
    {
        TabularTable table = TabularTable.Read(install, Mm7TableSources.RandomItems, HeaderRowCount);
        List<RandomItemRow> rows = [];
        List<TabularRow> annotations = [];

        // The table's lower half is a second section that weighs no item, and its rows carry words where an
        // item id would be. Only a row whose first field is a number weighs anything; every other row is the
        // other section or the padding between them, and is kept rather than read as an item.
        foreach (TabularRow row in table.Rows)
        {
            if (TableValue.NumericOrNull(row, 0) is not { } id)
            {
                annotations.Add(row);
                continue;
            }

            // The table's own first row is the empty item zero: the item table's placeholder, which weighs
            // nothing. It is data rather than a header, so it is read and then left out of the weighed rows
            // exactly as the donor's own reader does.
            if (id == 0 && row.Fields.Skip(2).All(field => field.Length == 0)) continue;

            List<int> chances = [];
            for (int level = 0; level < Levels; level++)
            {
                chances.Add(TableValue.OptionalInteger(table, row, 2 + level, (level + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)) ?? 0);
            }

            rows.Add(new RandomItemRow(id, chances));
        }

        if (rows.Count != ExpectedRows)
        {
            throw new LodFormatException(
                $"{table.Source}: expected {ExpectedRows} weighed item rows, read {rows.Count}; a short read would leave every treasure level poorer than the data states.");
        }

        return new RandomItemsTable(table, [.. rows], [.. annotations]);
    }
}
