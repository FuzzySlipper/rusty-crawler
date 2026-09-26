using System.Globalization;
using System.Text.Json;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Content;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// What every kind of monster thinks of every other kind, read from the content the importer wrote from
/// the shipped matrix.
/// </summary>
/// <remarks>
/// <para>
/// <b>The matrix is content and this is only its reading.</b> The shipped data carries
/// <c>hostile.txt</c>: one header row naming every kind of monster — the party's own row first — and one
/// row per kind holding a band for each column
/// (<c>OpenEnroth src/Engine/Tables/HostilityTable.cpp:15-22</c>). A band of zero is friendly and one to
/// four are the four bands that also serve as notice ranges
/// (<c>src/Engine/Objects/MonsterEnumFunctions.h:494-498</c>). The importer writes the header and the
/// non-zero bands; this reads them, so which kinds hate which is a fact about game data rather than a
/// table compiled into this ruleset.
/// </para>
/// <para>
/// <b>A monster row belongs to a kind, not to itself.</b> The shipped monsters come in groups of three
/// graded variants of one kind, and the matrix names the kind once: the donor's own mapping is
/// <c>(id - 1) / 3 + 1</c> (<c>src/Engine/Objects/MonsterEnumFunctions.h:38-40</c>), and the party's own
/// row and column are index zero (<c>src/Engine/Tables/HostilityTable.h:12-15</c>).
/// </para>
/// <para>
/// <b>A cell the data does not state is friendly.</b> The donor fills every relation with friendly before
/// it reads a cell, so an omitted band and a stated zero are one fact; a pack that carries no matrix at
/// all therefore states that nothing in this game is anybody's enemy, which is the honest reading of
/// content that has no such table rather than an invented feud.
/// </para>
/// </remarks>
internal sealed class MightAndMagic7Hostility
{
    /// <summary>The definition kind the matrix's rows are declared under.</summary>
    internal const string DefinitionKind = "hostility";

    /// <summary>The entry id the matrix's own header is written under.</summary>
    internal const string KindsId = "kinds";

    /// <summary>The entry field that states which column a row's bands were read in.</summary>
    internal const string KindField = "kind";

    /// <summary>The entry field that states what a kind thinks of the kinds it does not call friendly.</summary>
    internal const string HostilityField = "hostility";

    /// <summary>The header field that names every column.</summary>
    internal const string ColumnsField = "columns";

    private readonly Dictionary<int, IReadOnlyDictionary<int, int>> _bands;

    private MightAndMagic7Hostility(Dictionary<int, IReadOnlyDictionary<int, int>> bands, IReadOnlyList<string> columns)
    {
        _bands = bands;
        Columns = columns;
    }

    /// <summary>What each column index is, the party's own row first, empty when the pack states none.</summary>
    internal IReadOnlyList<string> Columns { get; }

    /// <summary>How many kinds state any band at all.</summary>
    internal int Kinds => _bands.Count;

    /// <summary>Nothing is anybody's enemy: content carries no such matrix.</summary>
    internal static MightAndMagic7Hostility Empty { get; } = new([], []);

    /// <summary>
    /// Reads the matrix out of the content the product loaded, refusing rows a fight could not use.
    /// </summary>
    /// <param name="catalog">The validated content, when the product loaded any.</param>
    /// <returns>This game's hostility matrix.</returns>
    /// <exception cref="ContentValidationException">A row is not a number, is repeated, or states a band this game does not know. Every problem is named.</exception>
    internal static MightAndMagic7Hostility Read(ContentCatalog? catalog)
    {
        if (catalog is null) return Empty;
        List<ContentValidationIssue> issues = [];
        Dictionary<int, IReadOnlyDictionary<int, int>> bands = [];
        List<string> columns = [];
        foreach ((LoadedPack pack, ContentDocument document, ContentEntry entry) in catalog.Entries(DefinitionKind))
        {
            void Defect(string code, string message) => issues.Add(new ContentValidationIssue(code, message, pack.PackId, document.DocumentId));

            if (string.Equals(entry.Id, KindsId, StringComparison.Ordinal))
            {
                foreach (JsonElement column in entry.GetArray(ColumnsField))
                {
                    columns.Add(column.ValueKind == JsonValueKind.String ? column.GetString() ?? string.Empty : string.Empty);
                }

                continue;
            }

            if (entry.GetInt32(KindField) is not { } kind)
            {
                Defect("hostility-kind-missing", $"hostility row '{entry.Id}' states no column, so which kinds its bands are about cannot be read.");
                continue;
            }

            if (bands.ContainsKey(kind))
            {
                Defect("hostility-kind-reused", $"hostility column {kind} is stated more than once, so which kind's feelings it holds would depend on the order the rows were written.");
                continue;
            }

            bands[kind] = ReadBands(entry, pack, document, issues);
        }

        if (issues.Count > 0)
        {
            throw new ContentValidationException(
                $"This game's hostility matrix cannot be read: {issues[0].Message}",
                issues);
        }

        return new MightAndMagic7Hostility(bands, [.. columns]);
    }

    /// <summary>
    /// What one kind thinks of another, in the band the shipped matrix states.
    /// </summary>
    /// <remarks>
    /// A band the row does not state is friendly, which is the donor's own reading: it fills every
    /// relation with friendly before it reads a cell.
    /// </remarks>
    /// <param name="self">The kind whose feelings are read.</param>
    /// <param name="other">The kind it is looking at.</param>
    /// <returns>The band: zero is friendly, one to four are the four degrees of enmity.</returns>
    internal int Band(int self, int other) =>
        _bands.TryGetValue(self, out IReadOnlyDictionary<int, int>? row) && row.TryGetValue(other, out int band) ? band : 0;

    /// <summary>Whether one kind treats another as an enemy at all.</summary>
    /// <param name="self">The kind whose feelings are read.</param>
    /// <param name="other">The kind it is looking at.</param>
    internal bool IsEnemy(int self, int other) => Band(self, other) != 0;

    /// <summary>The kind a monster row belongs to, by the donor's own grouping of graded variants.</summary>
    /// <param name="monsterId">The monster table row.</param>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/Objects/MonsterEnumFunctions.h:38-40</c>: three graded variants share one
    /// kind, numbered from one, and the party's own row is zero.
    /// </remarks>
    internal static int KindOf(int monsterId) => monsterId <= 0 ? 0 : ((monsterId - 1) / 3) + 1;

    /// <summary>Reads one row's bands, refusing a band this game has no meaning for.</summary>
    private static IReadOnlyDictionary<int, int> ReadBands(
        ContentEntry entry,
        LoadedPack pack,
        ContentDocument document,
        List<ContentValidationIssue> issues)
    {
        Dictionary<int, int> bands = [];
        if (!entry.Payload.TryGetProperty(HostilityField, out JsonElement hostility) || hostility.ValueKind != JsonValueKind.Object)
        {
            return bands;
        }

        foreach (JsonProperty property in hostility.EnumerateObject())
        {
            if (!int.TryParse(property.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int column) ||
                property.Value.ValueKind != JsonValueKind.Number ||
                !property.Value.TryGetInt32(out int band))
            {
                issues.Add(new ContentValidationIssue(
                    "hostility-band-unreadable",
                    $"hostility row '{entry.Id}' states '{property.Name}': '{property.Value}', which is not a column and a band.",
                    pack.PackId,
                    document.DocumentId));
                continue;
            }

            if (band is < 0 or > 4)
            {
                issues.Add(new ContentValidationIssue(
                    "hostility-band-unknown",
                    $"hostility row '{entry.Id}' states band {band} toward column {column}, and this game knows friendly and four degrees of enmity.",
                    pack.PackId,
                    document.DocumentId));
                continue;
            }

            bands[column] = band;
        }

        return bands;
    }
}
