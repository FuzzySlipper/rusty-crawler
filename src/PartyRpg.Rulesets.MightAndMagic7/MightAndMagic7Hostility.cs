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
/// <b>A cell is what its column's kind thinks of its row's kind.</b> The donor stores the cell at row <c>r</c> and
/// column <c>c</c> as <c>relations[c][r]</c> (<c>HostilityTable.cpp:21</c>) and reads <c>relations[self][other]</c>
/// (<c>src/Engine/Objects/Actor.cpp:2165</c>, <c>:2641</c>), so a kind's own feelings are its column and the
/// party's row says which kinds hate the party. The shipped data agrees: the party's row names the bats, dragons,
/// goblins and ghosts, and the party's column is friendly from top to bottom.
/// </para>
/// <para>
/// <b>A monster row belongs to a kind, not to itself.</b> The shipped monsters come in groups of three
/// graded variants of one kind, and the matrix names the kind once. Which kind a row belongs to is stated on
/// the row itself (<c>hostilityKind</c>), which the importer derives from the table's own grouping; the party's
/// own row and column are index zero (<c>src/Engine/Tables/HostilityTable.h:12-15</c>).
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
    /// <param name="self">The kind whose feelings are read: the column.</param>
    /// <param name="other">The kind it is looking at: the row.</param>
    /// <returns>The band: zero is friendly, one to four are the four degrees of enmity.</returns>
    internal int Band(int self, int other) =>
        _bands.TryGetValue(other, out IReadOnlyDictionary<int, int>? row) && row.TryGetValue(self, out int band) ? band : 0;

    /// <summary>What one kind thinks of the party, which is the party's own row of the matrix.</summary>
    /// <param name="kind">The kind whose feelings are read.</param>
    /// <returns>The band: zero is friendly, one to four are the four degrees of enmity.</returns>
    internal int TowardParty(int kind) => Band(kind, PartyKind);

    /// <summary>The matrix's own index for the party: <c>HostilityTable.h:12-15</c>.</summary>
    internal const int PartyKind = 0;

    /// <summary>
    /// The race a peasant kind belongs to, or null for a kind that is not a peasant's: what makes two peasants of
    /// different kinds one faction when one of them is wronged.
    /// </summary>
    /// <remarks>
    /// The shipped matrix gives every peasant its own kind — by race, sex and dress — and the donor's code, not its
    /// data, says which kinds are peasants and of which race: dwarves are kinds 39 to 44, elves 45 to 50, humans 51 to
    /// 62 and goblins 78 to 83 (OpenEnroth <c>src/Engine/Objects/MonsterEnums.h:346-390</c> and <c>:404-414</c>,
    /// <c>isPeasant</c> in <c>src/Engine/Objects/MonsterEnumFunctions.h:48-54</c>, the races in
    /// <c>src/Engine/Objects/MonsterEnumFunctions.cpp:60-104</c>). The operator's matrix header names the same
    /// kinds in the same places (<c>hostile.txt</c>: "Peasant Dwarf ..." at 39 to 44, "Peasant Elf ..." at 45 to 50,
    /// "Peasant Human ..." at 51 to 62, "Peasant Goblin ..." at 78 to 83). Faithful.
    /// </remarks>
    /// <param name="kind">A kind's index in the matrix.</param>
    /// <returns>A number standing for the race, the same for every peasant kind of one race; null otherwise.</returns>
    internal static int? PeasantRace(int kind) => kind switch
    {
        >= 39 and <= 44 => 1,
        >= 45 and <= 50 => 2,
        >= 51 and <= 62 => 3,
        >= 78 and <= 83 => 4,
        _ => null,
    };

    /// <summary>Whether one kind treats another as an enemy at all.</summary>
    /// <param name="self">The kind whose feelings are read.</param>
    /// <param name="other">The kind it is looking at.</param>
    internal bool IsEnemy(int self, int other) => Band(self, other) != 0;

    /// <summary>Whether a kind is one of the undead, which a turning and a binding of the dead are aimed at.</summary>
    /// <remarks>
    /// The donor names its undead by kind — ghost, lich, skeleton warrior, vampire, wight, zombie, and ghoul
    /// (<c>OpenEnroth/src/Engine/Objects/MonsterEnumFunctions.cpp:278-288</c>, <c>MONSTER_SUPERTYPE_UNDEAD</c>) — and
    /// those are the shipped matrix's own column names for the kinds, so a kind is read as undead when its column is
    /// one of them. A matrix that names no kinds has no undead.
    /// </remarks>
    /// <param name="kind">The kind's column index.</param>
    internal bool IsUndead(int kind) =>
        kind > 0 && kind < Columns.Count && UndeadKinds.Contains(Columns[kind]);

    /// <summary>The undead kinds, by the matrix's own column names.</summary>
    private static readonly HashSet<string> UndeadKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "Ghost", "Lich", "Skeleton Warrior", "Vampire", "Wight", "Zombie", "Ghoul",
    };


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
