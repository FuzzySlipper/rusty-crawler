using MightAndMagic7.Import.Lod;

namespace MightAndMagic7.Import.Tables;

/// <summary>One spell, grouped under the school its section belongs to.</summary>
/// <param name="Id">The spell's global id.</param>
/// <param name="School">The school the spell's section names.</param>
/// <param name="Level">The spell's level inside its school.</param>
/// <param name="Name">The spell's name.</param>
/// <param name="Resist">The damage or resistance type the spell uses.</param>
/// <param name="ShortName">The short name shown on the spellbook page.</param>
/// <param name="Description">The table's own description text.</param>
/// <param name="Normal">The effect text at normal mastery.</param>
/// <param name="Expert">The effect text at expert mastery.</param>
/// <param name="Master">The effect text at master mastery.</param>
/// <param name="GrandMaster">The effect text at grand master mastery.</param>
/// <param name="Stats">The table's per-spell flag string.</param>
public readonly record struct SpellRecord(
    int Id,
    string School,
    int Level,
    string Name,
    string Resist,
    string ShortName,
    string Description,
    string Normal,
    string Expert,
    string Master,
    string GrandMaster,
    string Stats);

/// <summary>The spell table.</summary>
public sealed class SpellTable
{
    /// <summary>How many rows this table's own header occupies.</summary>
    public const int HeaderRowCount = 2;

    /// <summary>How many spells the shipped table carries: nine schools of eleven.</summary>
    public const int ExpectedSpells = 99;

    /// <summary>How many schools the shipped table carries.</summary>
    public const int ExpectedSchools = 9;

    private SpellTable(TabularTable table, SpellRecord[] spells)
    {
        Table = table;
        Spells = spells;
    }

    /// <summary>The table this was read from.</summary>
    public TabularTable Table { get; }

    /// <summary>Every spell, in table order.</summary>
    public IReadOnlyList<SpellRecord> Spells { get; }

    /// <summary>The schools, in the order the table presents them.</summary>
    public IReadOnlyList<string> Schools => [.. Spells.Select(spell => spell.School).Distinct(StringComparer.Ordinal)];

    /// <summary>Reads the table from an installation.</summary>
    public static SpellTable Read(LodInstall install)
    {
        TabularTable table = TabularTable.Read(install, Mm7TableSources.Spells, HeaderRowCount);
        List<SpellRecord> spells = [];
        string school = string.Empty;
        foreach (TabularRow row in table.Rows)
        {
            // A section header is a row without an id whose name column ends in "Spells"; the first
            // section's header is the file's own title row and is consumed as the table header.
            if (row.Field(0).Trim().Length == 0)
            {
                string marker = TableValue.Text(row, 2);
                if (marker.EndsWith("Spells", StringComparison.OrdinalIgnoreCase))
                {
                    school = marker[..^"Spells".Length].Trim();
                }
                else if (marker.EndsWith("Spell", StringComparison.OrdinalIgnoreCase))
                {
                    school = marker[..^"Spell".Length].Trim();
                }

                continue;
            }

            if (school.Length == 0)
            {
                // The first section's header is the table title, so the school is named by the column
                // that carries the spell names.
                school = "Fire";
            }

            spells.Add(new SpellRecord(
                TableValue.Integer(table, row, 0, "#"),
                school,
                TableValue.Integer(table, row, 1, "Lvl"),
                TableValue.Text(row, 2),
                TableValue.Text(row, 3),
                TableValue.Text(row, 4),
                TableValue.Text(row, 5),
                TableValue.Text(row, 6),
                TableValue.Text(row, 7),
                TableValue.Text(row, 8),
                TableValue.Text(row, 9),
                TableValue.Text(row, 10)));
        }

        if (spells.Count != ExpectedSpells)
        {
            throw new LodFormatException($"{table.Source}: expected {ExpectedSpells} spells, read {spells.Count}.");
        }

        SpellTable result = new(table, [.. spells]);
        if (result.Schools.Count != ExpectedSchools)
        {
            throw new LodFormatException(
                $"{table.Source}: expected {ExpectedSchools} schools, read {result.Schools.Count} ({string.Join(", ", result.Schools)}).");
        }

        return result;
    }
}
