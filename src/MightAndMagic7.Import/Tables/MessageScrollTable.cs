using MightAndMagic7.Import.Lod;

namespace MightAndMagic7.Import.Tables;

/// <summary>Item-keyed reading text, including empty source rows.</summary>
/// <remarks>OpenEnroth src/Engine/Tables/MessageScrollTable.cpp documents the item id and text columns.</remarks>
public sealed class MessageScrollTable
{
    private MessageScrollTable(TabularTable table, IReadOnlyDictionary<int, string> texts)
    {
        Table = table;
        Texts = texts;
    }

    /// <summary>Source identity and digest.</summary>
    public TabularTable Table { get; }

    /// <summary>Texts keyed by their item identity.</summary>
    public IReadOnlyDictionary<int, string> Texts { get; }

    /// <summary>Reads the source table without interpreting its prose.</summary>
    public static MessageScrollTable Read(LodInstall install)
    {
        var table = TabularTable.Read(install, Mm7TableSources.MessageScrolls, 1);
        Dictionary<int, string> texts = [];
        foreach (var row in table.Rows)
        {
            if (string.IsNullOrWhiteSpace(row.Field(0))) continue;
            int id = TableValue.Integer(table, row, 0, "Item");
            if (!texts.TryAdd(id, TableValue.Text(row, 1)))
                throw new LodFormatException(LodFault.Ambiguous, $"{table.Source}: item {id} has more than one reading text.");
        }
        return new MessageScrollTable(table, texts);
    }
}
