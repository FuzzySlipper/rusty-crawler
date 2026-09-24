using System.Security.Cryptography;
using System.Text;
using MightAndMagic7.Import.Lod;

namespace MightAndMagic7.Import.Tables;

/// <summary>One data row of a tab-separated table.</summary>
/// <param name="Number">The row's 1-based number within the data rows.</param>
/// <param name="Fields">The row's fields, in column order.</param>
public readonly record struct TabularRow(int Number, string[] Fields)
{
    /// <summary>The field at a column index, or an empty string when the row is short.</summary>
    public string Field(int index) => index >= 0 && index < Fields.Length ? Fields[index] : string.Empty;
}

/// <summary>
/// A tab-separated table read from one container entry, with the provenance needed to prove which
/// bytes it came from.
/// </summary>
public sealed class TabularTable
{
    private TabularTable(LodSource source, string entryVersionString, byte[] payload, string[] headerRows, TabularRow[] rows)
    {
        Source = source;
        ArchiveVersionString = entryVersionString;
        Sha256 = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        ByteLength = payload.Length;
        HeaderRows = headerRows;
        Rows = rows;
    }

    /// <summary>The declared source this table was read from.</summary>
    public LodSource Source { get; }

    /// <summary>The version string the owning archive declares. It does not identify the game.</summary>
    public string ArchiveVersionString { get; }

    /// <summary>SHA-256 of the entry's decoded bytes, so a reader can be checked byte for byte.</summary>
    public string Sha256 { get; }

    /// <summary>Decoded payload length in bytes.</summary>
    public int ByteLength { get; }

    /// <summary>The table's own header rows, exactly as they appear.</summary>
    public IReadOnlyList<string> HeaderRows { get; }

    /// <summary>The data rows.</summary>
    public IReadOnlyList<TabularRow> Rows { get; }

    /// <summary>Reads a table from an installation through its declared source.</summary>
    /// <param name="install">The installation to read from.</param>
    /// <param name="source">The declared source.</param>
    /// <param name="headerRowCount">How many leading rows are the table's own header.</param>
    public static TabularTable Read(LodInstall install, LodSource source, int headerRowCount)
    {
        ArgumentNullException.ThrowIfNull(install);
        ArgumentNullException.ThrowIfNull(source);
        if (headerRowCount < 0) throw new ArgumentOutOfRangeException(nameof(headerRowCount));

        LodPayload payload = install.Read(source);
        string text = payload.AsText();
        List<string[]> records = SplitRecords(text);
        if (records.Count < headerRowCount)
        {
            throw new LodFormatException(
                $"{source}: the table has {records.Count} rows but {headerRowCount} header rows were declared.");
        }

        string[] headerRows = [.. records.Take(headerRowCount).Select(fields => string.Join('\t', fields))];
        List<TabularRow> rows = [];
        for (int index = headerRowCount; index < records.Count; index++)
        {
            string[] fields = records[index];
            // Padding and the trailing newline are not rows.
            if (fields.All(field => field.Length == 0)) continue;
            rows.Add(new TabularRow(rows.Count + 1, fields));
        }

        return new TabularTable(source, install.Archive(source.ArchiveName).VersionString, payload.Bytes, headerRows, [.. rows]);
    }

    /// <summary>
    /// Splits the rows into identified data rows and annotation rows.
    /// </summary>
    /// <remarks>
    /// The tables carry rows that are not data: at least one legend row is written where a monster's
    /// name belongs, with no id. Silently dropping them would hide data, so they are returned for the
    /// import report instead.
    /// </remarks>
    public (IReadOnlyList<TabularRow> Data, IReadOnlyList<TabularRow> Annotations) Partition()
    {
        List<TabularRow> data = [];
        List<TabularRow> annotations = [];
        foreach (TabularRow row in Rows)
        {
            if (row.Field(0).Trim().Length == 0) annotations.Add(row);
            else data.Add(row);
        }

        return (data, annotations);
    }

    /// <summary>
    /// Splits a tab-separated table into records, honouring double-quoted fields that may contain the
    /// separator, newlines, and doubled quotes for a literal quote.
    /// </summary>
    public static List<string[]> SplitRecords(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        List<string[]> records = [];
        List<string> fields = [];
        StringBuilder field = new();
        bool quoted = false;

        for (int index = 0; index < text.Length; index++)
        {
            char character = text[index];
            if (quoted)
            {
                if (character == '"')
                {
                    if (index + 1 < text.Length && text[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                    }
                    else
                    {
                        quoted = false;
                    }
                }
                else
                {
                    field.Append(character);
                }

                continue;
            }

            switch (character)
            {
                case '"':
                    quoted = true;
                    break;
                case '\t':
                    fields.Add(field.ToString());
                    field.Clear();
                    break;
                case '\r':
                    break;
                case '\n':
                    fields.Add(field.ToString());
                    field.Clear();
                    records.Add([.. fields]);
                    fields.Clear();
                    break;
                default:
                    field.Append(character);
                    break;
            }
        }

        if (field.Length > 0 || fields.Count > 0)
        {
            fields.Add(field.ToString());
            records.Add([.. fields]);
        }

        return records;
    }
}
