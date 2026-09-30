using System.Globalization;
using System.Text;
using PartyRpg.Kit.World;
using PartyRpg.Kit.Persistence;

namespace PartyRpg.Kit.Maps;

/// <summary>What one party has seen of one place, as a save records it, under the product's one schema.</summary>
/// <remarks>
/// <para>
/// <b>The grid is recorded with the cells rather than asked of content on the way back.</b> The bits mean
/// nothing without the grid they were written on: which cell a bit is depends on where the grid starts and
/// how wide a cell is, so a map recorded against one place's own layout and read back against another's would
/// show a party's walk somewhere it never went. The place's own map is still what a screen draws — the ground
/// is placed where it was, and the map under it is the one content carries today.
/// </para>
/// <para>
/// <b>Nothing about the visit is here.</b> Where the party stood, what it was looking at, and what a
/// detection showed it are readings of a moment and are not part of what it has mapped, so a save carries the
/// cells and never the walk that filled them.
/// </para>
/// </remarks>
/// <param name="Place">The place this is a map of.</param>
/// <param name="OriginX">Where the recorded grid's first column starts along the place's first axis.</param>
/// <param name="OriginY">Where the recorded grid's first row starts along the place's second axis.</param>
/// <param name="CellSize">How wide one recorded cell is, in the place's own units.</param>
/// <param name="Columns">How many cells the recorded grid has along the first axis.</param>
/// <param name="Rows">How many cells the recorded grid has along the second axis.</param>
/// <param name="Seen">The cells the party has seen, as bits in row-major order, written as hexadecimal.</param>
public sealed record MapTerritorySave(
    string Place,
    double OriginX,
    double OriginY,
    double CellSize,
    int Columns,
    int Rows,
    string Seen)
{
    /// <summary>The cells the party has seen, as bits in row-major order.</summary>
    public string Seen { get; init; } = Seen ?? string.Empty;

    /// <summary>The grid the cells were seen on.</summary>
    public MapGrid Grid => new(OriginX, OriginY, CellSize, Columns, Rows);

    /// <summary>The index of every cell the party has seen, in the grid's own row-major order.</summary>
    /// <remarks>
    /// A bit past the grid's own count is ignored rather than read as a cell: what such a bit means is
    /// reported as a contradiction by <see cref="MapSave.Problems"/>, and a load must not invent a cell
    /// outside the grid it recorded.
    /// </remarks>
    /// <returns>The indices.</returns>
    public IEnumerable<int> SeenIndices()
    {
        int cells = Columns * Rows;
        for (int index = 0; index < Seen.Length; index++)
        {
            int value = Hex(Seen[index]);
            for (int bit = 0; bit < 4; bit++)
            {
                if ((value & (1 << bit)) == 0) continue;
                int cell = (index * 4) + bit;
                if (cell < cells) yield return cell;
            }
        }
    }

    /// <summary>Records one territory as a save writes it.</summary>
    /// <param name="territory">The party's map of one place.</param>
    /// <returns>The recorded map.</returns>
    /// <exception cref="ArgumentNullException">No territory was supplied.</exception>
    public static MapTerritorySave Record(MapTerritory territory)
    {
        ArgumentNullException.ThrowIfNull(territory);
        MapGrid grid = territory.Grid;
        StringBuilder seen = new((grid.Cells + 3) / 4);
        for (int index = 0; index < grid.Cells; index += 4)
        {
            int value = 0;
            for (int bit = 0; bit < 4 && index + bit < grid.Cells; bit++)
            {
                if (territory.IsSeen(index + bit)) value |= 1 << bit;
            }

            seen.Append("0123456789abcdef"[value]);
        }

        return new MapTerritorySave(
            territory.Place.Value,
            grid.OriginX,
            grid.OriginY,
            grid.CellSize,
            grid.Columns,
            grid.Rows,
            seen.ToString());
    }

    /// <summary>The value one hexadecimal digit stands for.</summary>
    /// <param name="digit">The digit.</param>
    /// <returns>Its value, or zero for a digit that is not one.</returns>
    private static int Hex(char digit) => digit switch
    {
        >= '0' and <= '9' => digit - '0',
        >= 'a' and <= 'f' => digit - 'a' + 10,
        >= 'A' and <= 'F' => digit - 'A' + 10,
        _ => 0,
    };
}

/// <summary>Every place one party holds a map of, as a save records them.</summary>
/// <remarks>
/// <para>
/// <b>This is party state and not place state.</b> What a party has seen of a place is the party's own
/// memory: a place whose population the world restores, whose containers it empties, or whose doors it
/// closes touches none of it, and a party that walks out of a place keeps the map it drew. The world's own
/// per-place state is a different section of the document and is deliberately not consulted here.
/// </para>
/// <para>
/// <b>The section is what the party has seen and never the place's own map again.</b> Every cell of a place —
/// what its terrain is, where its outlines run — is content, read back from the packs a load was composed
/// over, so a save carries a party's walk rather than a copy of the world's maps.
/// </para>
/// </remarks>
/// <param name="Places">The places the party holds a map of, in the order it first saw each.</param>
public sealed record MapSave(IReadOnlyList<MapTerritorySave>? Places = null)
{
    /// <summary>The places the party holds a map of, in the order it first saw each.</summary>
    public IReadOnlyList<MapTerritorySave> Places { get; init; } = Places ?? [];

    /// <summary>A party that has mapped nothing.</summary>
    public static MapSave None { get; } = new();

    /// <summary>Whether this records nothing, which is what a party that has just begun has mapped.</summary>
    public bool IsEmpty => Places.Count == 0;

    /// <summary>
    /// Every contradiction between these maps and the session they would be resumed into.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The problems are contradictions rather than rules the product might refuse, because a load must not
    /// re-judge a map the product itself drew: a place the world does not have came from something else, a
    /// grid that is not a grid was written by something that does not map the way this build does, a cell
    /// beyond the grid's own count is a bit nothing could have set, and two maps of one place mean the party
    /// would hold two answers about the same ground.
    /// </para>
    /// <para>
    /// <b>A map whose place content no longer carries a map for is deliberately not one of them.</b> What the
    /// party saw is a fact about the party, and a place whose own map has been redrawn is a reason to draw the
    /// ground it saw differently rather than to refuse the expedition that holds it.
    /// </para>
    /// </remarks>
    /// <param name="places">The world's places, which the recorded maps must belong to.</param>
    /// <param name="limit">How many places a map keeper of this build may hold.</param>
    /// <returns>Every problem found, in the order the maps are recorded.</returns>
    /// <exception cref="ArgumentNullException">The world's places are null.</exception>
    public IReadOnlyList<SaveProblem> Problems(PlaceGraph places, int limit)
    {
        ArgumentNullException.ThrowIfNull(places);
        List<SaveProblem> problems = [];
        if (Places.Count > limit)
        {
            problems.Add(new SaveProblem(
                SaveCodes.SaveMapsOversize,
                string.Empty,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the party holds maps of {Places.Count} places and this build keeps at most {limit}, so the document was written by something that does not bound what a party maps")));
        }

        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (MapTerritorySave recorded in Places)
        {
            if (!seen.Add(recorded.Place))
            {
                problems.Add(new SaveProblem(
                    SaveCodes.SaveMapTwice,
                    recorded.Place,
                    $"place '{recorded.Place}' is mapped twice, so the party would hold two answers about the same ground"));
            }

            if (places.Find(new PlaceId(recorded.Place)) is null)
            {
                problems.Add(new SaveProblem(
                    SaveCodes.SaveMapPlaceUnknown,
                    recorded.Place,
                    $"place '{recorded.Place}' is recorded as mapped, and the world has no such place"));
                continue;
            }

            if (!Grid(recorded, out string? defective))
            {
                problems.Add(new SaveProblem(SaveCodes.SaveMapGridDefective, recorded.Place, $"place '{recorded.Place}' is mapped on {defective}"));
                continue;
            }

            int digits = (recorded.Columns * recorded.Rows + 3) / 4;
            if (recorded.Seen.Length != digits)
            {
                problems.Add(new SaveProblem(
                    SaveCodes.SaveMapCellsMisaligned,
                    recorded.Place,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"place '{recorded.Place}' is mapped on a {recorded.Columns}x{recorded.Rows} grid and records {recorded.Seen.Length} digits where {digits} cells need {digits}, so the bits would not line up with the ground")));
                continue;
            }

            if (recorded.Seen.Any(digit => !IsHex(digit)))
            {
                problems.Add(new SaveProblem(
                    SaveCodes.SaveMapCellsNotHex,
                    recorded.Place,
                    $"place '{recorded.Place}' records cells that are not hexadecimal, so nothing says what it saw"));
                continue;
            }
        }

        return problems;
    }

    /// <summary>Whether one recorded map's grid is a grid at all.</summary>
    private static bool Grid(MapTerritorySave recorded, out string? defective)
    {
        if (!double.IsFinite(recorded.OriginX) || !double.IsFinite(recorded.OriginY) ||
            !double.IsFinite(recorded.CellSize) || recorded.CellSize <= 0)
        {
            defective = string.Create(
                CultureInfo.InvariantCulture,
                $"a grid of cell size {recorded.CellSize} at ({recorded.OriginX:0.##},{recorded.OriginY:0.##}), which is not a grid");
            return false;
        }

        if (recorded.Columns <= 0 || recorded.Rows <= 0 || (long)recorded.Columns * recorded.Rows > MapGrid.MaxCells)
        {
            defective = string.Create(
                CultureInfo.InvariantCulture,
                $"a {recorded.Columns}x{recorded.Rows} grid, which holds no cells or more than this build maps");
            return false;
        }

        defective = null;
        return true;
    }

    /// <summary>Whether one character is a hexadecimal digit.</summary>
    private static bool IsHex(char digit) =>
        digit is (>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F');
}
