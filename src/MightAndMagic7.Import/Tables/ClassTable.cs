using MightAndMagic7.Import.Lod;

namespace MightAndMagic7.Import.Tables;

/// <summary>One class rank row: base class, first promotion, or one of the two second promotions.</summary>
/// <param name="Rank">The row's position in the table.</param>
/// <param name="Name">The rank's name.</param>
/// <param name="Description">The table's own description text.</param>
/// <param name="BaseClass">The base class this rank belongs to, as the table's notes column records it.</param>
public readonly record struct ClassRecord(int Rank, string Name, string Description, string BaseClass);

/// <summary>The class and rank table.</summary>
public sealed class ClassTable
{
    /// <summary>How many rows this table's own header occupies.</summary>
    public const int HeaderRowCount = 1;

    /// <summary>How many ranks the shipped table carries: nine base classes across four ranks each.</summary>
    public const int ExpectedRanks = 36;

    private ClassTable(TabularTable table, ClassRecord[] ranks)
    {
        Table = table;
        Ranks = ranks;
    }

    /// <summary>The table this was read from.</summary>
    public TabularTable Table { get; }

    /// <summary>Every rank row, in table order.</summary>
    public IReadOnlyList<ClassRecord> Ranks { get; }

    /// <summary>Reads the table from an installation.</summary>
    public static ClassTable Read(LodInstall install)
    {
        TabularTable table = TabularTable.Read(install, Mm7TableSources.Classes, HeaderRowCount);
        ClassRecord[] ranks = [.. table.Rows.Select(row => new ClassRecord(
            row.Number,
            TableValue.Text(row, 0),
            TableValue.Text(row, 1),
            TableValue.Text(row, 2)))];

        if (ranks.Length != ExpectedRanks)
        {
            throw new LodFormatException($"{table.Source}: expected {ExpectedRanks} ranks, read {ranks.Length}.");
        }

        foreach (ClassRecord rank in ranks)
        {
            if (rank.Name.Length == 0 || rank.BaseClass.Length == 0)
            {
                throw new LodFormatException($"{table.Source}: rank row {rank.Rank} is missing its name or base class.");
            }
        }

        return new ClassTable(table, ranks);
    }
}
