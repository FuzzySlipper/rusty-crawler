using System.Text;
using MightAndMagic7.Import.Lod;
using MightAndMagic7.Import.Tables;

namespace MightAndMagic7.Import.Events;

/// <summary>
/// One map's own lines of text: the hints its event faces show and the status lines its events print.
/// </summary>
/// <remarks>
/// <para>
/// A map's events name their text by a number, and the number indexes this map's own string table — a run of
/// NUL-terminated strings in the rules archive under the map's file stem with a <c>.str</c> extension
/// (OpenEnroth <c>src/Engine/Engine.cpp:1453-1469</c>, which splits the entry on NUL into the level's strings,
/// trimmed and with surrounding quotes removed, that the interpreter indexes at
/// <c>src/Engine/Evt/EvtInterpreter.cpp:413</c>). The global program has no table
/// of its own, which the inventory records (<c>docs/research/mm7-data-inventory.md</c>, <i>Containers</i>).
/// </para>
/// <para>
/// The text is single-byte, and is read as Latin-1 like every other text this importer reads from the
/// archives.
/// </para>
/// </remarks>
public sealed class MapStrings
{
    private readonly string[] _lines;

    private MapStrings(string name, string[] lines)
    {
        Name = name;
        _lines = lines;
    }

    /// <summary>The entry the lines were read from.</summary>
    public string Name { get; }

    /// <summary>How many lines the table holds.</summary>
    public int Count => _lines.Length;

    /// <summary>The line at an index, or empty when the table has no such line.</summary>
    /// <remarks>
    /// The donor reads an index past the table's end as an empty line rather than failing (OpenEnroth
    /// <c>src/Engine/Evt/EvtInterpreter.cpp:413</c>), and so does this reader: an instruction naming a line
    /// the table does not hold says nothing, which the caller can see and report.
    /// </remarks>
    /// <param name="index">The line's index.</param>
    /// <returns>The line.</returns>
    public string Line(int index) => index >= 0 && index < _lines.Length ? _lines[index] : string.Empty;

    /// <summary>Reads a string table from an entry's bytes.</summary>
    /// <param name="name">The entry's name.</param>
    /// <param name="bytes">The entry's bytes.</param>
    /// <returns>The table.</returns>
    public static MapStrings Read(string name, byte[] bytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(bytes);
        string text = Encoding.Latin1.GetString(bytes);
        string[] lines = text.Split('\0');

        // The last string is terminated like every other one, so the split leaves one empty piece after it;
        // that piece is the terminator and not a line.
        if (lines.Length > 0 && lines[^1].Length == 0) lines = lines[..^1];
        return new MapStrings(name, [.. lines.Select(Clean)]);
    }

    /// <summary>A line as the donor keeps it: trimmed, and without the quotes a table wraps a line in.</summary>
    private static string Clean(string line)
    {
        string trimmed = line.Trim();
        return trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"' ? trimmed[1..^1] : trimmed;
    }

    /// <summary>Reads every map's string table, keyed by the map file stem it belongs to.</summary>
    /// <param name="install">The installation.</param>
    /// <returns>The tables, by stem, ignoring case.</returns>
    public static IReadOnlyDictionary<string, MapStrings> ReadAll(LodInstall install)
    {
        ArgumentNullException.ThrowIfNull(install);
        LodArchive archive = install.Archive(Mm7TableSources.RulesArchive);
        Dictionary<string, MapStrings> tables = new(StringComparer.OrdinalIgnoreCase);
        foreach (LodEntry entry in archive.Entries)
        {
            if (!entry.Name.EndsWith(".str", StringComparison.OrdinalIgnoreCase)) continue;
            tables[Path.GetFileNameWithoutExtension(entry.Name)] = Read(entry.Name, archive.Read(entry).Bytes);
        }

        return tables;
    }
}
