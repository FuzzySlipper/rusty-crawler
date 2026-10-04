using PartyRpg.Kit.Magic;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Maps;

/// <summary>What a game calls the automap and how it words what the map holds, or does not.</summary>
/// <remarks>
/// The words belong to the ruleset rather than to the kit, exactly as every other piece of presentation
/// meaning does: the original's own manual names the automap and the book it fills, and a kit that spelled
/// those itself would be carrying one game's vocabulary into every other one.
/// </remarks>
/// <param name="Title">What this game calls the automap.</param>
/// <param name="Unavailable">
/// What it says when the session holds no map owner at all — the mechanism is not there, which is a
/// different fact from a place nothing has been seen of.
/// </param>
/// <param name="NoWorld">
/// What it says when the session maps and holds no world yet: there is no place to draw, which is a different
/// fact from a ruleset that states no automap at all.
/// </param>
/// <param name="Unmapped">
/// What it says when the session maps, and the place the party stands in is one content states no map for:
/// the automap cannot draw a place whose own map the content does not carry.
/// </param>
/// <param name="Unseen">What it says when the place is mapped and the party has not seen any of it yet.</param>
/// <param name="Empty">What it says when the party holds no map of anywhere yet.</param>
/// <param name="Seen">What it says about how much of the place has been seen, given the cells and the whole.</param>
/// <param name="Mapped">What it says about how many places the party holds a map of.</param>
public readonly record struct MapWords(
    string Title,
    string Unavailable,
    string NoWorld,
    string Unmapped,
    string Unseen,
    string Empty,
    Func<int, int, string> Seen,
    Func<int, string> Mapped);

/// <summary>How the edges of a map drawing correspond to the directions of the game world.</summary>
/// <remarks>
/// A drawing's first axis runs left to right and its second runs top to bottom. The ruleset supplies the words
/// for those four edges because a source map may keep its second axis in the opposite order from a screen. The
/// kit carries the answer with the drawing; it does not turn a game's map coordinates into another coordinate
/// system or ask a screen to guess which way is north.
/// </remarks>
/// <param name="Top">The direction at the top edge of the drawing.</param>
/// <param name="Bottom">The direction at the bottom edge of the drawing.</param>
/// <param name="Left">The direction at the left edge of the drawing.</param>
/// <param name="Right">The direction at the right edge of the drawing.</param>
public readonly record struct MapOrientation(
    string Top,
    string Bottom,
    string Left,
    string Right)
{
    /// <summary>The empty orientation used by a session that has no map rule or drawing.</summary>
    public static MapOrientation Empty => new(string.Empty, string.Empty, string.Empty, string.Empty);
}

/// <summary>Which rung of a game's zoom ladder one place is drawn at.</summary>
/// <remarks>
/// The ladder is the game's and the rung is chosen from the place's own extent, so a place small enough to
/// fit is drawn whole and a region is drawn as the window its widest rung shows. Nothing on a screen chooses
/// this: a projection publishes the rung it drew with, which is what keeps a reload drawing the same map.
/// </remarks>
/// <param name="Rung">Which rung this is, counted from the nearest.</param>
/// <param name="Rungs">How many rungs the ladder has.</param>
/// <param name="Cells">How many cells across the window that rung draws.</param>
public readonly record struct MapZoom(int Rung, int Rungs, int Cells);

/// <summary>One point the automap marks: a place's own feature, or something a detection revealed.</summary>
/// <remarks>
/// A mark carries its own position in the place's own units, its identity in the owner's own terms, and the
/// kind word the game gives it, which is what lets a screen draw it without knowing what it is. Nothing here
/// says whether the party has seen it: a mark is only ever built where the map is already showing that
/// ground, or by a detection whose whole claim is that the party can see it.
/// </remarks>
/// <param name="Id">The mark's identity, which is the owner's own identity for what it names.</param>
/// <param name="Kind">What kind of thing it is, in the game's own words.</param>
/// <param name="Label">What it is called.</param>
/// <param name="X">Where it stands along the place's first axis.</param>
/// <param name="Y">Where it stands along the place's second axis.</param>
/// <param name="Detected">Whether a detection is what put it on the map.</param>
public readonly record struct MapMark(
    string Id,
    string Kind,
    string Label,
    double X,
    double Y,
    bool Detected = false);

/// <summary>What a detection the party is carrying reveals on the map, as the game states it.</summary>
/// <remarks>
/// <para>
/// <b>This is a reading and never a record.</b> It is recomputed from the world every time the map is read,
/// it is not written into what the party has seen, and nothing about it survives the effect that revealed it:
/// a party that walks out of the place, or whose spell runs out, is left with exactly the map it had.
/// </para>
/// <para>
/// <b>What a detection adds is exactly what it names.</b> The marks below are the whole of it — no cell of
/// the place becomes seen because a detection looked at it — which is what makes "a detection reveals what it
/// claims and nothing more" a fact this owner can be held to rather than a promise.
/// </para>
/// </remarks>
/// <param name="Scope">What this detection looks over, in the game's own word.</param>
/// <param name="Message">What the game says it reveals.</param>
/// <param name="EndsAt">When the clock ends it, or null when nothing has said when.</param>
/// <param name="Marks">Everything it puts on the map, and nothing else.</param>
public sealed record MapReveal(
    string Scope,
    string Message,
    GameDate? EndsAt,
    IReadOnlyList<MapMark> Marks);

/// <summary>What the map asks about a detection, read from the owners that hold each fact.</summary>
/// <remarks>
/// The kit hands the game the state a detection could be read from and asks one question; which spells are
/// detections, what each looks over, and how long each lasts are the game's answers, exactly as which
/// placements the automap marks are.
/// </remarks>
/// <param name="Place">The place the party stands in.</param>
/// <param name="Pose">Where the party stands and faces in it.</param>
/// <param name="Placements">Everything content places in that place.</param>
/// <param name="Population">What is standing in that place now.</param>
/// <param name="Running">The effects the party is carrying, which is where a running detection is found.</param>
public readonly record struct MapDetectionView(
    PlaceId Place,
    PlacePose Pose,
    IReadOnlyList<PlacementDefinition> Placements,
    IReadOnlyList<PlacePopulationEntity> Population,
    IReadOnlyList<RunningSpellEffect> Running);

/// <summary>
/// What this game states about its automap: how far a party sees as it walks, what a place's own map cells
/// and features read as, how the map is zoomed, and what a detection reveals.
/// </summary>
/// <remarks>
/// <para>
/// This is the ruleset's whole contribution to the map owner, and it is deliberately answers rather than
/// actions: the owner decides which cells become seen and when, how much it keeps, and what a save records;
/// the game decides what a party can see, what a cell of its own maps means, and what a detection puts on
/// the map. That is the same split the knowledge owner makes with its own rule, and for the same reason —
/// a rule that wrote cells itself would be a second writer of one fact.
/// </para>
/// <para>
/// <b>A game that states no map has none.</b> A session whose ruleset answers no map rule composes no map
/// owner, and what its projection publishes says the mechanism is not there rather than showing an empty
/// rectangle a player would read as an unmapped room.
/// </para>
/// </remarks>
public interface IMapRule
{
    /// <summary>What this game calls the automap, and how it words what the map holds.</summary>
    MapWords Words { get; }

    /// <summary>Which world directions the drawing's four edges name.</summary>
    MapOrientation Orientation { get; }

    /// <summary>
    /// How many cells from the party's own cell the walking party can add to the map, before a wall or a
    /// closed door stops the sight line.
    /// </summary>
    /// <remarks>
    /// This is the sight radius, and it is a bound rather than a distance a screen measures: the owner asks
    /// the engine's own collision about each cell inside it and adds none outside it, so one step can never
    /// cost more than the square this states.
    /// </remarks>
    int SightRadius { get; }

    /// <summary>How many cells one sweep around the party may add, at most.</summary>
    /// <remarks>
    /// A second bound over the first: a place's own map is finite, but a sweep that walked all of it in one
    /// step would make a single step's cost depend on how large the place is. Cells are revealed nearest
    /// first, so what this trims is always the far end of what the party could see.
    /// </remarks>
    int MaxCellsPerSweep { get; }

    /// <summary>What one cell of a place's own map reads as, or empty when the game states nothing for it.</summary>
    /// <remarks>
    /// A cell the game states nothing for is still seen — it counts, it is drawn as ground rather than as a
    /// feature — and it is the kind word a screen styles it by, so an empty answer is a plain cell rather than
    /// an invisible one.
    /// </remarks>
    /// <param name="place">What kind of place the cell belongs to, which is what the cell's own number means.</param>
    /// <param name="kind">The kind the place's own map states.</param>
    /// <returns>The word the automap draws it as.</returns>
    string CellKind(PlaceKind place, byte kind);

    /// <summary>What kind of mark a place's own feature is drawn as, or empty when the automap marks none.</summary>
    /// <param name="placement">The thing content places in the place.</param>
    /// <returns>The mark's kind word, or empty.</returns>
    string Mark(PlacementDefinition placement);

    /// <summary>Which way the party faces on the drawing, in degrees, from the yaw the place stores.</summary>
    /// <remarks>
    /// A place's yaw is in the game's own facing unit, and the kit deliberately names no unit and converts
    /// nothing (<see cref="PartyRpg.Kit.Party.FacingRule"/>), so the game that knows the unit is the one that
    /// says how far the marker on the map is turned. The answer is degrees because a drawing is; a game whose
    /// facing turns a whole circle in 2048 units answers a quarter turn for 512.
    /// </remarks>
    /// <param name="yaw">The facing the party holds, in the place's own unit.</param>
    /// <returns>How far the party's marker is turned on the drawing, in degrees.</returns>
    double FacingDegrees(double yaw);

    /// <summary>Which rung of the zoom ladder the window over one place is drawn at.</summary>
    /// <param name="grid">The grid the place is mapped on.</param>
    /// <returns>The rung, how many there are, and how many cells it shows.</returns>
    MapZoom Zoom(MapGrid grid);

    /// <summary>
    /// Whether nothing solid stands between two points of the place the party is in, in the place's own
    /// coordinates.
    /// </summary>
    /// <remarks>
    /// <b>This is how the party sees as it walks, and it is the engine's own collision.</b> The game composes
    /// the scene the party walks in, so it is the game that can say what stands between two points of it; the
    /// answer a wall, a closed door, and a floor between two storeys give is the same one that stops the
    /// party's step. A game that states nothing here — a product with no spatial service — has the party see
    /// only the cell it stands in, which is the honest map of a world with no geometry to look through.
    /// </remarks>
    /// <param name="from">Where the party stands, in the place's own coordinates.</param>
    /// <param name="to">What it is looking toward.</param>
    /// <returns>Whether the sight line is clear.</returns>
    bool Sees(PlacePose from, PlacePose to);

    /// <summary>What a detection the party is carrying reveals, or null when none is running.</summary>
    /// <param name="view">The state a detection could be read from.</param>
    /// <returns>What it reveals, or null.</returns>
    MapReveal? Reveal(MapDetectionView view);
}
