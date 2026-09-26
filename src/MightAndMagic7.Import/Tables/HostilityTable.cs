using MightAndMagic7.Import.Lod;

namespace MightAndMagic7.Import.Tables;

/// <summary>
/// What one kind of monster thinks of another, as the shipped matrix states it.
/// </summary>
/// <remarks>
/// <para>
/// The cell is the donor's own hostility band: zero is friendly and one to four are the four bands
/// whose values double as notice ranges. The band is read as the matrix stores it and given no meaning
/// here — which creature acts on it, and how far, is the ruleset's reading of these numbers.
/// </para>
/// <para>
/// The row and the column are monster <em>kinds</em>, not monster rows: the shipped monsters come in
/// groups of three graded variants of one kind, and the matrix names the kind once. The first row and
/// column are the party's own, which is what a creature that fights for the party reads its targets
/// from.
/// </para>
/// </remarks>
/// <param name="Kind">The kind this row is about.</param>
/// <param name="Bands">What it thinks of each kind, in the matrix's own column order.</param>
public readonly record struct HostilityRow(string Kind, IReadOnlyList<int> Bands)
{
    /// <summary>What this kind thinks of the kind named by a column.</summary>
    /// <param name="column">The column's index in the matrix.</param>
    /// <returns>The band, or null when the row is shorter than the matrix's own header.</returns>
    public int? BandAt(int column) => column >= 0 && column < Bands.Count ? Bands[column] : null;
}

/// <summary>
/// The monster-hostility matrix the shipped data carries.
/// </summary>
/// <remarks>
/// <para>
/// The table is <c>hostile.txt</c> in the rules archive: one header row naming every kind, then one row
/// per kind beginning with its own name and carrying one band per column
/// (<c>OpenEnroth src/Engine/Tables/HostilityTable.cpp:15-22</c>, which reads the same file and drops
/// the name column of every row). The donor indexes it by a monster's <em>type</em>, which is the
/// group of three graded variants a monster row belongs to
/// (<c>src/Engine/Objects/MonsterEnumFunctions.h:38-40</c>, <c>(id - 1) / 3 + 1</c>).
/// </para>
/// <para>
/// Rows and columns are kept as they are and not squared up: a row shorter than the header is a
/// remainder this reader reports rather than pads, because a band the table does not state is not the
/// same fact as a band of zero.
/// </para>
/// </remarks>
public sealed class HostilityTable
{
    /// <summary>How many kinds the shipped matrix carries besides the party's own row and column.</summary>
    public const int ExpectedKinds = 88;

    private HostilityTable(TabularTable table, string[] columns, HostilityRow[] rows)
    {
        Table = table;
        Columns = columns;
        Rows = rows;
        int mismatched = 0;
        for (int index = 0; index < rows.Length && index < columns.Length; index++)
        {
            if (!string.Equals(rows[index].Kind, columns[index], StringComparison.OrdinalIgnoreCase)) mismatched++;
        }

        MismatchedNames = mismatched;
    }

    /// <summary>The table this was read from, whose source names the file in an error.</summary>
    public TabularTable Table { get; }

    /// <summary>The kinds each row's bands are about, in column order, the party's own column first.</summary>
    public IReadOnlyList<string> Columns { get; }

    /// <summary>Every kind's row, in table order.</summary>
    public IReadOnlyList<HostilityRow> Rows { get; }

    /// <summary>
    /// How many rows spell their kind differently from the column at the same position.
    /// </summary>
    /// <remarks>
    /// The shipped file is not consistent about it — its peasant kinds are named with spacing the header
    /// does not repeat — and the donor therefore reads the matrix <em>positionally</em>: a row's number is
    /// the monster type it is about, and the header is only names
    /// (<c>OpenEnroth src/Engine/Tables/HostilityTable.cpp:17-21</c>, which zips the file's rows against a
    /// segment of numbers and never matches a name). The count is read rather than assumed so a report can
    /// say how much of the file's naming is decoration.
    /// </remarks>
    public int MismatchedNames { get; }

    /// <summary>The column a kind's bands stand in, or null when the matrix does not name it.</summary>
    /// <param name="kind">The kind to look for.</param>
    public int? ColumnOf(string kind)
    {
        for (int index = 0; index < Columns.Count; index++)
        {
            if (string.Equals(Columns[index], kind, StringComparison.OrdinalIgnoreCase)) return index;
        }

        return null;
    }

    /// <summary>The row a kind's own feelings are stated in, or null when the matrix does not name it.</summary>
    /// <param name="kind">The kind to look for.</param>
    public HostilityRow? RowOf(string kind)
    {
        foreach (HostilityRow row in Rows)
        {
            if (string.Equals(row.Kind, kind, StringComparison.OrdinalIgnoreCase)) return row;
        }

        return null;
    }

    /// <summary>What one kind thinks of another, or null when the matrix does not state it.</summary>
    /// <param name="self">The kind whose feelings are read.</param>
    /// <param name="other">The kind it is looking at.</param>
    public int? Band(string self, string other)
    {
        if (RowOf(self) is not { } row) return null;
        if (ColumnOf(other) is not { } column) return null;
        return row.BandAt(column);
    }

    /// <summary>Reads the matrix from an installation.</summary>
    /// <remarks>
    /// The file is tab-separated with no declared header rows of the kind the other tables use: its
    /// first record is the header, so it is read as its own shape rather than through the tabular
    /// reader whose header count is per table family.
    /// </remarks>
    /// <param name="install">The installation to read from.</param>
    /// <exception cref="LodFormatException">The matrix is missing, empty, or carries a cell that is not a band.</exception>
    public static HostilityTable Read(LodInstall install)
    {
        ArgumentNullException.ThrowIfNull(install);
        string[] records = Split(install.Read(Mm7TableSources.Hostility).AsText());
        if (records.Length < 2)
        {
            throw new LodFormatException(
                $"{Mm7TableSources.Hostility}: the matrix holds {records.Length} record(s), and a matrix needs a header row and at least one kind.");
        }

        string[] header = records[0].Split('\t');
        if (header.Length < 2)
        {
            throw new LodFormatException($"{Mm7TableSources.Hostility}: the header row holds {header.Length} column(s), so no kind is named.");
        }

        // The header's first cell is empty: it stands over the column of row names rather than naming a
        // kind itself, so the kind columns begin one cell in.
        string[] columns = [.. header.Skip(1).Select(cell => cell.Trim())];
        List<HostilityRow> rows = [];
        for (int index = 1; index < records.Length; index++)
        {
            string[] cells = records[index].Split('\t');
            string kind = cells[0].Trim();
            if (kind.Length == 0) continue;

            List<int> bands = [];
            for (int column = 1; column < cells.Length; column++)
            {
                string cell = cells[column].Trim();
                if (cell.Length == 0) continue;
                if (!int.TryParse(cell, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int band))
                {
                    throw new LodFormatException(
                        $"{Mm7TableSources.Hostility}: kind '{kind}' column '{Column(columns, column - 1)}' is '{cell}', not a hostility band.");
                }

                bands.Add(band);
            }

            rows.Add(new HostilityRow(kind, bands));
        }

        return new HostilityTable(
            TabularTable.Read(install, Mm7TableSources.Hostility, headerRowCount: 1),
            columns,
            [.. rows]);
    }

    private static string Column(string[] columns, int index) => index >= 0 && index < columns.Length ? columns[index] : index.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Splits the matrix into records, accepting either line ending.</summary>
    private static string[] Split(string text) =>
        [.. text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n').Where(record => record.Trim().Length > 0)];
}
