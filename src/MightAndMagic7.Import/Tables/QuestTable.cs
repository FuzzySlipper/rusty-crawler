using MightAndMagic7.Import.Lod;

namespace MightAndMagic7.Import.Tables;

/// <summary>One quest bit and the text the journal shows for it.</summary>
/// <param name="Bit">The quest bit.</param>
/// <param name="Text">The quest note text.</param>
/// <param name="Notes">The table's own notes column.</param>
/// <param name="Owner">The quest's authoring owner.</param>
public readonly record struct QuestRecord(int Bit, string Text, string Notes, string Owner);

/// <summary>The quest text table.</summary>
public sealed class QuestTable
{
    /// <summary>How many rows this table's own header occupies.</summary>
    public const int HeaderRowCount = 1;

    private QuestTable(TabularTable table, QuestRecord[] quests)
    {
        Table = table;
        Quests = quests;
    }

    /// <summary>The table this was read from.</summary>
    public TabularTable Table { get; }

    /// <summary>Every quest row, in table order.</summary>
    public IReadOnlyList<QuestRecord> Quests { get; }

    /// <summary>Reads the table from an installation.</summary>
    public static QuestTable Read(LodInstall install)
    {
        TabularTable table = TabularTable.Read(install, Mm7TableSources.Quests, HeaderRowCount);
        QuestRecord[] quests = [.. table.Rows.Select(row => new QuestRecord(
            TableValue.Integer(table, row, 0, "Q Bit"),
            TableValue.Text(row, 1),
            TableValue.Text(row, 2),
            TableValue.Text(row, 3)))];

        if (quests.Length == 0)
        {
            throw new LodFormatException($"{table.Source}: the table has no quest rows.");
        }

        return new QuestTable(table, quests);
    }
}
