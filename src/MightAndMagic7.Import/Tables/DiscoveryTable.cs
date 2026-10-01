using MightAndMagic7.Import.Lod;

namespace MightAndMagic7.Import.Tables;

/// <summary>One row of the discovery table: the note a party keeps, and the kind of note it is.</summary>
/// <param name="Number">The note's number, which is what an event instruction sets.</param>
/// <param name="Text">The note as the table words it.</param>
/// <param name="Category">The table's own category word, lower-cased: <c>stat</c>, <c>obelisk</c>, <c>potion</c>, <c>teacher</c>, <c>misc</c>.</param>
public readonly record struct DiscoveryRecord(int Number, string Text, string Category);

/// <summary>
/// The game's discovery table — the auto notes a party's notes book holds — read from the rules archive.
/// </summary>
/// <remarks>
/// <para>
/// The table is numbered rows of a sentence and a category word, and the party keeps the set of numbers it
/// has learned (OpenEnroth <c>src/Engine/Tables/AutonoteTable.cpp:19-35</c>, which reads the number, the text
/// and the category; the map events set a number through the <c>autonote</c> variable,
/// <c>src/Engine/Objects/Character.cpp:4350</c>). The category word is written lower-cased because the shipped
/// table spells it both ways (<c>Misc</c> and <c>misc</c>, <c>potion</c> beside <c>Stat</c>).
/// </para>
/// <para>
/// <b>What is not a row.</b> The shipped table holds placeholder rows whose text is the single character
/// <c>0</c>, and trailing rows whose text is empty; neither is a note anybody can learn, so neither is read,
/// and how many there were is kept for the report.
/// </para>
/// </remarks>
public sealed class DiscoveryTable
{
    /// <summary>How many rows this table's own header occupies.</summary>
    public const int HeaderRowCount = 1;

    /// <summary>The text the shipped table writes in a row that holds no note.</summary>
    private const string Placeholder = "0";

    private DiscoveryTable(TabularTable table, DiscoveryRecord[] rows, int skipped)
    {
        Table = table;
        Rows = rows;
        SkippedRows = skipped;
    }

    /// <summary>The table this was read from.</summary>
    public TabularTable Table { get; }

    /// <summary>Every note the table holds, in table order.</summary>
    public IReadOnlyList<DiscoveryRecord> Rows { get; }

    /// <summary>How many numbered rows hold no note: a placeholder or an empty text.</summary>
    public int SkippedRows { get; }

    /// <summary>Reads the table from an installation.</summary>
    /// <param name="install">The installation.</param>
    /// <returns>The table.</returns>
    /// <exception cref="LodFormatException">A row's number cannot be read, or two rows claim one number.</exception>
    public static DiscoveryTable Read(LodInstall install)
    {
        TabularTable table = TabularTable.Read(install, Mm7TableSources.Discoveries, HeaderRowCount);
        List<DiscoveryRecord> rows = [];
        HashSet<int> numbers = [];
        int skipped = 0;
        foreach (TabularRow row in table.Rows)
        {
            // A row with no number is the table's own padding after its last note.
            if (row.Field(0).Trim().Length == 0) continue;
            int number = TableValue.Integer(table, row, 0, "Note bit");
            if (!numbers.Add(number))
            {
                throw new LodFormatException(LodFault.Ambiguous, $"{table.Source}: note {number} has more than one row.");
            }

            string text = TableValue.Text(row, 1);
            if (text.Length == 0 || string.Equals(text, Placeholder, StringComparison.Ordinal))
            {
                skipped++;
                continue;
            }

            rows.Add(new DiscoveryRecord(number, text, TableValue.Text(row, 2).ToLowerInvariant()));
        }

        if (rows.Count == 0)
        {
            throw new LodFormatException(LodFault.Missing, $"{table.Source}: the table has no notes.");
        }

        return new DiscoveryTable(table, [.. rows], skipped);
    }
}
