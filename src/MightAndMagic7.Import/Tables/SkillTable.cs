using MightAndMagic7.Import.Lod;

namespace MightAndMagic7.Import.Tables;

/// <summary>One skill row with the effect text each mastery tier grants.</summary>
/// <param name="Index">The row's position in the table.</param>
/// <param name="Name">The skill's name.</param>
/// <param name="Description">The table's own description text.</param>
/// <param name="Normal">The effect text at normal mastery.</param>
/// <param name="Expert">The effect text at expert mastery.</param>
/// <param name="Master">The effect text at master mastery.</param>
/// <param name="GrandMaster">The effect text at grand master mastery.</param>
public readonly record struct SkillRecord(
    int Index,
    string Name,
    string Description,
    string Normal,
    string Expert,
    string Master,
    string GrandMaster);

/// <summary>The skill table.</summary>
public sealed class SkillTable
{
    /// <summary>How many rows this table's own header occupies.</summary>
    public const int HeaderRowCount = 1;

    /// <summary>
    /// How many skill rows the shipped table carries. Four of them are leftovers the game does not
    /// use; the manual's four blocks account for the rest, and the import report lists both.
    /// </summary>
    public const int ExpectedRows = 37;

    private SkillTable(TabularTable table, SkillRecord[] skills)
    {
        Table = table;
        Skills = skills;
    }

    /// <summary>The table this was read from.</summary>
    public TabularTable Table { get; }

    /// <summary>Every skill row, in table order.</summary>
    public IReadOnlyList<SkillRecord> Skills { get; }

    /// <summary>Reads the table from an installation.</summary>
    public static SkillTable Read(LodInstall install)
    {
        TabularTable table = TabularTable.Read(install, Mm7TableSources.Skills, HeaderRowCount);
        SkillRecord[] skills = [.. table.Rows.Select(row => new SkillRecord(
            row.Number,
            TableValue.Text(row, 0),
            TableValue.Text(row, 1),
            TableValue.Text(row, 2),
            TableValue.Text(row, 3),
            TableValue.Text(row, 4),
            TableValue.Text(row, 5)))];

        if (skills.Length != ExpectedRows)
        {
            throw new LodFormatException($"{table.Source}: expected {ExpectedRows} skills, read {skills.Length}.");
        }

        return new SkillTable(table, skills);
    }
}
