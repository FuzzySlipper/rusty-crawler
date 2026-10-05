using MightAndMagic7.Import.Lod;

namespace MightAndMagic7.Import.Tables;

/// <summary>Original actor group assignments and their localized news.</summary>
/// <remarks>OpenEnroth src/Engine/Tables/NPCTable.cpp:96-112. Indices are zero-based.</remarks>
public sealed record NpcNewsTable(
    TabularTable GroupSource,
    TabularTable NewsSource,
    IReadOnlyDictionary<int, int> Groups,
    IReadOnlyDictionary<int, string> Texts)
{
    /// <summary>Reads both tables and rejects ambiguous or dangling assignments.</summary>
    public static NpcNewsTable Read(LodInstall install)
    {
        var groups = TabularTable.Read(install, Mm7TableSources.NpcGroups, 1);
        var news = TabularTable.Read(install, Mm7TableSources.NpcNews, 1);
        Dictionary<int, int> assignments = [];
        Dictionary<int, string> texts = [];
        foreach (var row in news.Rows)
        {
            if (string.IsNullOrWhiteSpace(row.Field(0))) continue;
            int id = TableValue.Integer(news, row, 0, "News");
            if (id < 0 || !texts.TryAdd(id, TableValue.Text(row, 1)))
                throw new LodFormatException(LodFault.Ambiguous, $"{news.Source}: invalid or repeated news row {id}.");
        }
        foreach (var row in groups.Rows)
        {
            if (string.IsNullOrWhiteSpace(row.Field(0))) continue;
            int id = TableValue.Integer(groups, row, 0, "Group");
            int text = TableValue.Integer(groups, row, 1, "News");
            if (id < 0 || text < 0 || (text != 0 && !texts.ContainsKey(text)) || !assignments.TryAdd(id, text))
                throw new LodFormatException(LodFault.Ambiguous, $"{groups.Source}: group {id} has an ambiguous or unresolved news assignment {text}.");
        }
        return new(groups, news, assignments, texts);
    }
}
