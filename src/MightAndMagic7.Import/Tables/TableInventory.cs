using System.Security.Cryptography;
using MightAndMagic7.Import.Lod;

namespace MightAndMagic7.Import.Tables;

/// <summary>One text table found in the rules archive.</summary>
/// <param name="Name">The entry name.</param>
/// <param name="ByteLength">Decoded payload length.</param>
/// <param name="Sha256">SHA-256 of the decoded payload.</param>
/// <param name="Kind">How the container stored it.</param>
public readonly record struct TextTableEntry(string Name, int ByteLength, string Sha256, LodPayloadKind Kind);

/// <summary>A table name that more than one archive holds.</summary>
/// <param name="Name">The shared entry name.</param>
/// <param name="Archives">The archives that hold it.</param>
public readonly record struct AmbiguousTable(string Name, IReadOnlyList<string> Archives);

/// <summary>A rule family the shipped data does not carry.</summary>
/// <param name="Name">The rule family.</param>
/// <param name="Why">Where the game keeps it instead.</param>
/// <param name="Owner">Who has to author it for this product.</param>
public readonly record struct DataGap(string Name, string Why, string Owner);

/// <summary>
/// What the rules archive actually contains: every text table it holds, the table names that are
/// ambiguous across archives, and the rule families the data does not carry at all.
/// </summary>
public sealed class TableInventory
{
    private TableInventory(IReadOnlyList<TextTableEntry> textTables, IReadOnlyList<AmbiguousTable> ambiguousTables)
    {
        TextTables = textTables;
        AmbiguousTables = ambiguousTables;
    }

    /// <summary>Every text table in the rules archive, ordered by name.</summary>
    public IReadOnlyList<TextTableEntry> TextTables { get; }

    /// <summary>Table names another archive also holds, which is why sources are declared.</summary>
    public IReadOnlyList<AmbiguousTable> AmbiguousTables { get; }

    /// <summary>
    /// Rule families a designer would expect in these tables and will not find. The product has to
    /// author them; nothing may pretend they were extracted.
    /// </summary>
    public static IReadOnlyList<DataGap> KnownGaps { get; } =
    [
        new(
            "class and rank skill ceilings",
            "Compiled into the game's executable, not in any table; which skills a class may learn and how far each may be trained exists only there.",
            "the ruleset, informed by the donor's transcription"),
        new(
            "guild spell-level gating",
            "Compiled into the executable; the tables record which guilds exist, not which spell levels each tier sells.",
            "the ruleset"),
        new(
            "calendar constants",
            "The clock and its calendar live in the executable and in localization strings; no table carries months, weeks, or hours per day.",
            "the ruleset"),
        new(
            "race creation ranges",
            "Per-race starting attributes live in the executable; the tables carry no race table.",
            "the ruleset"),
    ];

    /// <summary>
    /// Whether a payload is text: no NUL bytes, and a sampled prefix that is almost entirely printable.
    /// </summary>
    public static bool LooksLikeText(byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (payload.Length == 0) return false;
        ReadOnlySpan<byte> sample = payload.AsSpan(0, Math.Min(payload.Length, 4096));
        int printable = 0;
        foreach (byte value in sample)
        {
            if (value == 0) return false;
            if (value == 9 || value == 10 || value == 13 || (value >= 32 && value <= 126) || value >= 160) printable++;
        }

        return printable / (double)sample.Length > 0.95;
    }

    /// <summary>Reads the inventory of an installation's rules archive.</summary>
    public static TableInventory Read(LodInstall install)
    {
        ArgumentNullException.ThrowIfNull(install);
        LodArchive rules = install.Archive(Mm7TableSources.RulesArchive);
        List<TextTableEntry> textTables = [];
        foreach (LodEntry entry in rules.Entries)
        {
            LodPayload payload;
            try
            {
                payload = rules.Read(entry);
            }
            catch (LodFormatException)
            {
                // An entry this reader cannot decode is not a text table; the container report
                // records decode failures separately.
                continue;
            }

            // The container marks every entry in the rules archive as a "text" entry, including the
            // event programs, string blobs, and binary tables it holds. A table is text when its bytes
            // say so as well.
            if (payload.Kind is not (LodPayloadKind.Text or LodPayloadKind.DeflatedText)) continue;
            if (!LooksLikeText(payload.Bytes)) continue;
            textTables.Add(new TextTableEntry(
                entry.Name,
                payload.Bytes.Length,
                Convert.ToHexString(SHA256.HashData(payload.Bytes)).ToLowerInvariant(),
                payload.Kind));
        }

        List<AmbiguousTable> ambiguous = [];
        foreach (TextTableEntry table in textTables)
        {
            IReadOnlyList<string> archives = install.ArchivesContaining(table.Name);
            if (archives.Count > 1) ambiguous.Add(new AmbiguousTable(table.Name, archives));
        }

        return new TableInventory(
            [.. textTables.OrderBy(table => table.Name, StringComparer.OrdinalIgnoreCase)],
            [.. ambiguous.OrderBy(table => table.Name, StringComparer.OrdinalIgnoreCase)]);
    }
}
