using MightAndMagic7.Import.Lod;

namespace MightAndMagic7.Import.Tables;

/// <summary>A profession's authored fee and the words its hiring conversation uses.</summary>
public sealed record NpcProfessionRecord(int Id, string Name, int HirePrice, string Action, string Benefit, string Join, string Dismiss);

/// <summary>
/// The profession table, read offline from the operator's installation. The source columns and four
/// header rows are documented by OpenEnroth src/Engine/Tables/NPCTable.cpp:142-163. No donor text is copied.
/// </summary>
public sealed class NpcProfessionTable
{
    private NpcProfessionTable(TabularTable source, IReadOnlyList<NpcProfessionRecord> professions)
    {
        Source = source;
        Professions = professions;
    }

    /// <summary>The source bytes and their import provenance.</summary>
    public TabularTable Source { get; }

    /// <summary>The table's numbered professions, in authored order.</summary>
    public IReadOnlyList<NpcProfessionRecord> Professions { get; }

    /// <summary>The profession of this number, or none when the table does not declare it.</summary>
    public NpcProfessionRecord? Find(int id) => Professions.FirstOrDefault(profession => profession.Id == id);

    /// <summary>Reads the actual profession prices and conversation text.</summary>
    public static NpcProfessionTable Read(LodInstall install)
    {
        TabularTable source = TabularTable.Read(install, Mm7TableSources.NpcProfessions, 4);
        List<NpcProfessionRecord> professions = [];
        HashSet<int> ids = [];
        foreach (TabularRow row in source.Rows)
        {
            // The original table finishes with pure-tab padding; the common table reader omits it.
            int id = TableValue.Integer(source, row, 0, "profession id");
            int price = TableValue.Integer(source, row, 2, "hire price");
            if (id < 0 || price < 0 || !ids.Add(id))
                throw new LodFormatException(LodFault.Value, $"{source.Source}: row {row.Number} has a negative id/price or repeats profession {id}; it cannot define an unambiguous hiring charge.");
            professions.Add(new NpcProfessionRecord(id, TableValue.Text(row, 1), price,
                TableValue.Text(row, 3), TableValue.Text(row, 4), TableValue.Text(row, 5), TableValue.Text(row, 6)));
        }

        return new NpcProfessionTable(source, professions);
    }
}
