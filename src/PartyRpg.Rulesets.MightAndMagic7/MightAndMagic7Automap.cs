using System.Globalization;
using PartyRpg.Kit.Magic;
using PartyRpg.Kit.Maps;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// This game's own reading of its automap: how far a walking party sees, what each place's own map cells and
/// features are drawn as, which rung of the zoom ladder a place is shown at, and what a detection reveals.
/// </summary>
/// <remarks>
/// <para>
/// <b>What the automap is drawn from is the place's own map, not a second opinion about its geometry.</b> A
/// region's squares are the bands of the region's own height map at the map's own tile pitch, and an
/// interior's say whether the level's own minimap outlines pass through them — both emitted from the
/// operator's data by the importer (<c>docs/research/mm7-map-formats.md</c>, the place-map layout). The
/// original draws more than this and we say so: an outdoor automap is a pre-rendered landscape picture in the
/// shipped art, and an indoor one draws the level's outlines as lines shaded by their own height
/// (<c>OpenEnroth src/GUI/UI/UIGame.cpp:1380-1400</c>, where each outline is a line between two of the level's
/// vertices) — ours is a raster of the same outlines at
/// <see cref="InteriorCellSize"/> units, so it is coarser than the original's drawing and is marked ours.
/// </para>
/// <para>
/// <b>The manual's own two lines are what this reading follows.</b> The automap "is drawn as territory comes
/// into view, showing position and facing, with zoom (+/-)" and "Wizard Eye reveals creatures at Normal,
/// treasure at Expert, other points of interest at Master"
/// (<c>docs/research/mm7-manual-outline.md</c> p.116 from the manual p.18). Territory filling in as it is
/// seen, the party's own position and facing, a zoom ladder, and a detection that marks what it looks over
/// are therefore the four things this file states; the key bindings for the zoom are deliberately not ours to
/// claim, and the rung is picked from the place's own extent instead.
/// </para>
/// <para>
/// <b>Everything authored here is ours and says so.</b> The shipped tables state no sight radius, no cell
/// size, no zoom ladder, and no duration for a detection, so the values below are this game's and are marked
/// as such rather than presented as extracted facts.
/// </para>
/// </remarks>
internal sealed class MightAndMagic7Automap : IMapRule
{
    /// <summary>How wide one square of an interior's automap is, in the place's own units.</summary>
    /// <remarks>
    /// <b>Ours.</b> The shipped data states no cell size for an indoor map: the original draws outlines as
    /// lines, and a raster needs a square. This is a quarter of the outdoor tile pitch — fine enough that a
    /// room of a few hundred units is several squares across, and coarse enough that the largest level's map is
    /// a few tens of thousands of cells rather than millions. The importer rasterises at exactly this size, so
    /// the number is written in both places and the format spec records it.
    /// </remarks>
    internal const double InteriorCellSize = 128;

    /// <summary>How many cells from the party the walking party's view reaches.</summary>
    /// <remarks>
    /// <b>Ours.</b> The manual states that the automap fills in as territory comes into view and names no
    /// distance; six squares is a room's width in an interior and a short walk in a region, and it is a bound
    /// on one sweep rather than a distance a screen measures.
    /// </remarks>
    private const int Radius = 6;

    /// <summary>How many cells one sweep may add at most.</summary>
    /// <remarks>
    /// <b>Ours.</b> A bound over the radius: the largest sweep the square above allows is 169 cells, so this
    /// only ever trims a place whose own map is much finer than the sight it is seen with. It is stated so that
    /// one step's cost cannot depend on how large a place is.
    /// </remarks>
    private const int Sweep = 512;

    /// <summary>The rungs of this game's zoom ladder, as how many cells across a drawing shows.</summary>
    /// <remarks>
    /// <b>Ours.</b> The manual states that the automap zooms with two keys and states no magnifications; these
    /// are four window widths, and the rung a place is drawn at is the first one that holds the place. A place
    /// wider than the last rung is drawn as a window around the party and the rest of it comes into view as the
    /// party walks, which is what a corner automap is.
    /// </remarks>
    private static readonly int[] Ladder = [16, 32, 64, 128];

    private readonly Func<SessionWorld?> _world;

    /// <summary>Creates this game's automap reading.</summary>
    /// <param name="world">
    /// The world the party stands in, read when a sight line is asked about. It is a provider rather than the
    /// world itself because this reading is composed before the session's world exists on the path that creates
    /// its party, and because a product with no world has nothing to see through.
    /// </param>
    internal MightAndMagic7Automap(Func<SessionWorld?>? world = null) => _world = world ?? (() => null);

    /// <inheritdoc />
    public MapWords Words { get; } = new(
        Title: "Automap",
        Unavailable: "This session keeps no map: its ruleset states no automap.",
        NoWorld: "The party is nowhere yet, so there is nothing to draw.",
        Unmapped: "Content carries no map of this place, so there is nothing to draw of it.",
        Unseen: "None of this place has been walked yet, so its map is blank.",
        Empty: "The party holds no map of anywhere yet.",
        Seen: (seen, total) => string.Create(
            CultureInfo.InvariantCulture,
            $"{seen} of {total} squares walked ({Percent(seen, total)}%)"),
        Mapped: count => string.Create(
            CultureInfo.InvariantCulture,
            $"{count} place{(count == 1 ? string.Empty : "s")} mapped"));

    /// <inheritdoc />
    public int SightRadius => Radius;

    /// <inheritdoc />
    public int MaxCellsPerSweep => Sweep;

    /// <inheritdoc />
    /// <remarks>
    /// The answer comes from the collision scene the party itself walks in, which is the mover's own line of
    /// sight over the place's admitted artifact: a wall, a shut door, and a floor between two storeys stop the
    /// party's view for exactly the reason they stop its step. A product whose engine gave no spatial service
    /// has no mover, and then the party sees only the square it stands in.
    /// </remarks>
    public bool Sees(PlacePose from, PlacePose to) =>
        _world()?.Mover is { } mover && mover.InSight(MightAndMagic7Movement.Space.Position(from), MightAndMagic7Movement.Space.Position(to));

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <b>What a square of a place is drawn as, in this game's own words.</b> An interior's squares are either
    /// passed through by the level's own minimap outlines or not, which the importer wrote as one and zero; a
    /// region's are the band its own height map falls in, written as one to four. The words are what a screen
    /// styles a square by, so they are stated here rather than in a stylesheet's own vocabulary.
    /// </para>
    /// <para>
    /// A cell the game states nothing for is left unnamed rather than named as blank: the map owner draws no
    /// run for an unnamed kind, which is how a square the importer could say nothing about stays off a drawing
    /// it would otherwise be painted into.
    /// </para>
    /// </remarks>
    public string CellKind(PlaceKind place, byte kind) => place == PlaceKind.Interior
        ? kind switch
        {
            0 => "floor",
            1 => "wall",
            _ => string.Empty,
        }
        : kind switch
        {
            1 => "low",
            2 => "upland",
            3 => "highland",
            4 => "peak",
            _ => string.Empty,
        };

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <b>What a party would walk to, and nothing else.</b> A door, a container, a building somebody keeps, and
    /// a person standing in the open are what an automap is for; the level's own scenery — its thousands of
    /// decorations, its lights, its spawn points — is not marked, because a map that showed every tree would
    /// show nothing. A creature is deliberately not marked either: what is standing in a place is what a
    /// detection reveals, which is the difference between the map a party drew and the reading a spell gives it.
    /// </para>
    /// <para>
    /// The words are the wire's own vocabulary for a mark, and a screen styles them; the same words are what a
    /// detection's marks carry, which is what makes a revealed door and a walked-past door one kind of thing.
    /// </para>
    /// </remarks>
    public string Mark(PlacementDefinition placement) => placement.Content.Kind switch
    {
        "door" => "door",
        "container" => "container",
        "service" => "building",
        "residence" => "building",
        "person" => "person",
        _ => string.Empty,
    };

    /// <inheritdoc />
    /// <remarks>
    /// The place's own facing turns a whole circle in 2048 units, which is the rule this game's movement is
    /// composed with (<see cref="MightAndMagic7Movement.Facing"/>), so a quarter turn is 512 and the marker is
    /// turned from the same number the party walks by.
    /// </remarks>
    public double FacingDegrees(double yaw)
    {
        double degrees = yaw / MightAndMagic7Movement.Facing.UnitsPerTurn * 360;
        degrees %= 360;
        return degrees < 0 ? degrees + 360 : degrees;
    }

    /// <inheritdoc />
    /// <remarks>
    /// The rung is the first one wide enough to hold the place, so a room is drawn whole and a region is drawn
    /// as the window the last rung shows. Choosing it from the place rather than from a key is ours: the
    /// manual's own zoom is the player's, and nothing in this build binds it yet.
    /// </remarks>
    public MapZoom Zoom(MapGrid grid)
    {
        int extent = Math.Max(grid.Columns, grid.Rows);
        int rung = 0;
        while (rung < Ladder.Length - 1 && Ladder[rung] < extent) rung++;
        return new MapZoom(rung + 1, Ladder.Length, Ladder[rung]);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <b>A detection adds marks and nothing else.</b> No square of the place becomes walked because a spell
    /// looked at it, and nothing here is written down: the marks are recomputed from the world on every read,
    /// they last exactly as long as the effect the casting left — a deadline on the session's one clock — and a
    /// lapsed detection marks nothing at all. That is what makes the reveal a reading rather than knowledge,
    /// and it is the same boundary the knowledge owner drew when it decided a detection writes no note.
    /// </para>
    /// <para>
    /// What each scope marks is what the spell's own row says it looks over: the places scope marks this
    /// place's own points of interest, wherever they stand, because a spell that reports over the places is the
    /// one the original draws them for; life marks everything alive here; and minds marks those of them that
    /// answer to a name. A creature that is down is not alive and is marked by nothing.
    /// </para>
    /// </remarks>
    public MapReveal? Reveal(MapDetectionView view)
    {
        if (view.Running is not { Count: > 0 } running) return null;

        List<MapMark> marks = [];
        List<string> says = [];
        List<string> scopes = [];
        List<GameDate?> ends = [];
        foreach (RunningSpellEffect effect in running)
        {
            DetectionScope scope = SpellEffectIds.ScopeOf(effect.Effect);
            if (scope == DetectionScope.None) continue;
            ends.Add(effect.EndsAt);
            scopes.Add(Scope(scope));
            switch (scope)
            {
                case DetectionScope.Places:
                    foreach (PlacementDefinition placement in view.Placements)
                    {
                        if (Mark(placement).Length == 0) continue;
                        marks.Add(Of(placement, Mark(placement)));
                    }

                    says.Add("the places it knows and this place's own points of interest");
                    break;
                case DetectionScope.Life:
                    int living = 0;
                    foreach (PlacePopulationEntity entity in view.Population)
                    {
                        if (!entity.IsAlive) continue;
                        marks.Add(Of(entity, Kind(entity)));
                        living++;
                    }

                    says.Add(string.Create(CultureInfo.InvariantCulture, $"{living} living thing(s) standing here"));
                    break;
                default:
                    int minds = 0;
                    foreach (PlacePopulationEntity entity in view.Population)
                    {
                        if (!entity.IsAlive || Named(entity).Length == 0) continue;
                        marks.Add(Of(entity, "mind"));
                        minds++;
                    }

                    says.Add(string.Create(CultureInfo.InvariantCulture, $"{minds} mind(s) answering here by name"));
                    break;
            }
        }

        if (marks.Count == 0 && says.Count == 0) return null;
        // When the last of them lapses is what the panel shows: each scope's marks end with its own effect, and
        // the latest deadline is the moment nothing a detection revealed is left marked.
        GameDate? latest = null;
        foreach (GameDate? end in ends)
        {
            if (end is { } moment && (latest is not { } held || LaterThan(moment, held))) latest = moment;
        }

        return new MapReveal(
            string.Join(", ", scopes),
            $"A detection is marking {string.Join("; ", says)}.",
            latest,
            marks);
    }

    /// <summary>One mark for a place's own feature.</summary>
    private static MapMark Of(PlacementDefinition placement, string kind) => new(
        placement.Content.ToString(),
        kind,
        Named(placement) is { Length: > 0 } name ? name : placement.Content.ToString(),
        placement.Pose.X,
        placement.Pose.Y,
        Detected: true);

    /// <summary>One mark for something standing in the place.</summary>
    private static MapMark Of(PlacePopulationEntity entity, string kind) => new(
        entity.Content.ToString(),
        kind,
        Named(entity) is { Length: > 0 } name ? name : entity.Content.ToString(),
        entity.Pose.X,
        entity.Pose.Y,
        Detected: true);

    /// <summary>Whether one moment on the calendar is later than another.</summary>
    private static bool LaterThan(GameDate moment, GameDate held) =>
        (moment.Year, moment.Month, moment.Day, moment.Hour, moment.Minute, moment.Second)
        .CompareTo((held.Year, held.Month, held.Day, held.Hour, held.Minute, held.Second)) > 0;

    /// <summary>What one detection scope is called on the wire, which is what a screen styles its marks by.</summary>
    private static string Scope(DetectionScope scope) => scope switch
    {
        DetectionScope.Places => "places",
        DetectionScope.Life => "life",
        _ => "minds",
    };

    /// <summary>What kind of thing stands there, in the map's own vocabulary.</summary>
    private static string Kind(PlacePopulationEntity entity) => entity.Content.Kind switch
    {
        "person" => "person",
        "monster" => "creature",
        _ => "creature",
    };

    /// <summary>What a placement is called, as content names it, or empty when it names nothing.</summary>
    private static string Named(PlacePopulationEntity entity) => Named(entity.Placement);

    /// <summary>What a placement is called, as content names it, or empty when it names nothing.</summary>
    private static string Named(PlacementDefinition placement) => placement.Source.GetString("name");

    /// <summary>How much of a place has been walked, as a whole percentage of its own map.</summary>
    private static int Percent(int seen, int total) =>
        total <= 0 ? 0 : (int)Math.Round(seen * 100.0 / total, MidpointRounding.AwayFromZero);
}
