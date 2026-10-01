using System.Globalization;
using PartyRpg.Kit.Magic;
using PartyRpg.Kit.Maps;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Presentation;

/// <summary>One rectangle of the drawn map: a run of cells a screen fills with one colour.</summary>
/// <remarks>
/// The rectangle is in the drawing's own space and is not a game quantity: the projection has already chosen
/// the window, the scale, and where each run begins, so a screen places four numbers and decides nothing. Two
/// adjacent cells of one kind are one rectangle because a screen redraws on every publish, and a map drawn as
/// one shape per cell would cost a place's whole grid every time the party moved.
/// </remarks>
/// <param name="X">Where the run starts across the drawing.</param>
/// <param name="Y">Where the run starts down the drawing.</param>
/// <param name="Width">How wide the run is drawn.</param>
/// <param name="Height">How tall one row of cells is drawn.</param>
/// <param name="Kind">What the cells of this run are, in the game's own word.</param>
public sealed record MapCellSnapshot(double X, double Y, double Width, double Height, string Kind)
{
    /// <summary>Writes one run of seen cells.</summary>
    /// <param name="builder">The projection being built.</param>
    /// <returns>The run's node.</returns>
    internal uint Write(UiValueBuilder builder) =>
        builder.Object(
            ("x", builder.Number(X)),
            ("y", builder.Number(Y)),
            ("w", builder.Number(Width)),
            ("h", builder.Number(Height)),
            ("kind", builder.String(Kind)));
}

/// <summary>One thing the map marks, as a screen draws it.</summary>
/// <param name="Id">The mark's identity.</param>
/// <param name="Kind">What kind of thing it is, in the game's own words.</param>
/// <param name="Label">What it is called.</param>
/// <param name="X">Where it stands across the drawing.</param>
/// <param name="Y">Where it stands down the drawing.</param>
/// <param name="Detected">Whether a detection is what put it here, which a screen shows differently.</param>
public sealed record MapMarkSnapshot(
    string Id,
    string Kind,
    string Label,
    double X,
    double Y,
    bool Detected)
{
    /// <summary>Writes one mark on the drawing.</summary>
    /// <param name="builder">The projection being built.</param>
    /// <returns>The mark's node.</returns>
    internal uint Write(UiValueBuilder builder) =>
        builder.Object(
            ("id", builder.String(Id)),
            ("kind", builder.String(Kind)),
            ("label", builder.String(Label)),
            ("x", builder.Number(X)),
            ("y", builder.Number(Y)),
            ("detected", builder.Boolean(Detected)));
}

/// <summary>The drawing itself: the window the map shows, and everything in it.</summary>
/// <remarks>
/// <para>
/// <b>Every coordinate here is already the drawing's own.</b> The window is a square the projection chose, the
/// cells are runs of what the party has seen inside it, and the party's own marker is where it stands in that
/// square — so a screen writes the numbers into a shape and computes no scale, no offset, and no position.
/// That is what makes a reload draw the same map: the whole drawing is a function of the state it was built
/// from and of nothing a screen remembers.
/// </para>
/// <para>
/// <b>Unseen ground is absent rather than drawn empty.</b> A cell the party has not seen contributes no
/// rectangle at all, which is the difference between a map that has not been walked and a map of a room with
/// nothing in it.
/// </para>
/// </remarks>
/// <param name="Rung">Which rung of the game's zoom ladder the window is drawn at.</param>
/// <param name="Rungs">How many rungs the ladder has.</param>
/// <param name="Cells">How many cells across the window shows.</param>
/// <param name="Size">The side of the square the drawing fills, in its own units.</param>
/// <param name="Drawn">The runs of seen cells the window shows.</param>
/// <param name="Marks">What the map marks, the place's own features first and what a detection revealed after.</param>
/// <param name="PartyX">Where the party stands across the drawing.</param>
/// <param name="PartyY">Where the party stands down the drawing.</param>
/// <param name="Facing">Which way the party faces, in degrees across the drawing.</param>
public sealed record MapDrawingSnapshot(
    int Rung,
    int Rungs,
    int Cells,
    double Size,
    IReadOnlyList<MapCellSnapshot> Drawn,
    IReadOnlyList<MapMarkSnapshot> Marks,
    double PartyX,
    double PartyY,
    double Facing)
{
    /// <summary>
    /// How large one mark and the party's own marker are drawn: half a cell of the window, in the drawing's
    /// own units.
    /// </summary>
    /// <remarks>
    /// The window always shows at least one cell, so the radius is a positive number whatever the zoom: a
    /// screen that divided the size by a published count of cells would be handed a hole the first time a
    /// count arrived as nothing, which is why the division is made here, once, over a count that cannot be.
    /// </remarks>
    public double MarkRadius => Size / Math.Max(1, Cells) / 2;

    /// <summary>
    /// The party's marker as the three corners of a triangle pointing up the drawing, in the drawing's own
    /// space, before the facing turns it: apex, then the right and the left of its base.
    /// </summary>
    public IReadOnlyList<double> PartyPoints =>
    [
        PartyX, PartyY - MarkRadius,
        PartyX + MarkRadius, PartyY + MarkRadius,
        PartyX - MarkRadius, PartyY + MarkRadius,
    ];

    /// <summary>Writes the drawing: numbers in the drawing's own space, and only numbers.</summary>
    /// <param name="builder">The projection being built.</param>
    /// <returns>The drawing's node.</returns>
    internal uint Write(UiValueBuilder builder) =>
        builder.Object(
            ("rung", builder.Number(Rung)),
            ("rungs", builder.Number(Rungs)),
            ("cells", builder.Number(Cells)),
            ("size", builder.Number(Size)),
            ("partyX", builder.Number(PartyX)),
            ("partyY", builder.Number(PartyY)),
            ("facing", builder.Number(Facing)),
            // How large a mark and the party's marker are drawn, and the marker's own corners: a screen places
            // them and divides nothing, so no published count can hand it a hole.
            ("markRadius", builder.Number(MarkRadius)),
            ("partyPoints", builder.Array([.. PartyPoints.Select(builder.Number)])),
            ("cellsDrawn", builder.Array([.. Drawn.Select(run => run.Write(builder))])),
            ("marks", builder.Array([.. Marks.Select(mark => mark.Write(builder))])));
}

/// <summary>Where the party is on its own map, and what that map shows.</summary>
/// <remarks>
/// <para>
/// <b>The automap is a projection of state and owns none.</b> What the party has seen is the map owner's, the
/// place's own map is content's, where the party stands is the world's, and what a detection reveals is read
/// from the world at the moment the projection is built. Nothing here is kept: a screen renders what it is
/// given, and the same state draws the same map twice.
/// </para>
/// <para>
/// <b>A session with no map owner is not a session with an empty map.</b> A ruleset that states no map, a
/// place content carries no map for, and a mapped place the party has not seen a cell of are three different
/// facts, and each says so in the game's own words rather than showing an empty rectangle a player would read
/// as an unmapped room.
/// </para>
/// <para>
/// <b>The five books read the same owner.</b> The maps book's page per place counts what the map owner holds
/// rather than keeping a second list, so the book and the automap cannot disagree about where the party has
/// been.
/// </para>
/// </remarks>
/// <param name="Available">Whether the session holds a map owner at all.</param>
/// <param name="Mapped">Whether the place the party stands in is one the session can map.</param>
/// <param name="Title">What the game calls the automap.</param>
/// <param name="Place">The place the map is of, empty when the session has no world.</param>
/// <param name="Name">What that place is called.</param>
/// <param name="Kind">The place's kind, as the wire spells it.</param>
/// <param name="State">What the game says about the map: how much is seen, or why it holds nothing.</param>
/// <param name="Seen">How many cells of the place the party has seen.</param>
/// <param name="Total">How many cells the place's own map holds.</param>
/// <param name="Detection">What a detection is looking over, in the game's own word, empty when none runs.</param>
/// <param name="DetectionMessage">What it is revealing, in the game's own sentence, empty when none runs.</param>
/// <param name="DetectionEnds">When that detection lapses, as a point on the calendar, empty when nothing says.</param>
/// <param name="Drawing">The drawing, or null when there is nothing to draw.</param>
public sealed record MapSnapshot(
    bool Available,
    bool Mapped,
    string Title,
    string Place,
    string Name,
    string Kind,
    string State,
    int Seen,
    int Total,
    string Detection = "",
    string DetectionMessage = "",
    string DetectionEnds = "",
    MapDrawingSnapshot? Drawing = null)
{
    /// <summary>No map owner: the session's ruleset stated no automap, so there is no map.</summary>
    public static MapSnapshot None => new(false, false, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, 0, 0);

    /// <summary>
    /// The side of the square a drawing fills, in the drawing's own units.
    /// </summary>
    /// <remarks>
    /// A structural constant rather than a tuning value: a drawing is a square of this many units and a screen
    /// scales it to whatever space it has, so the numbers a projection publishes do not depend on the size of
    /// anybody's window.
    /// </remarks>
    public const double DrawingSize = 1000;

    /// <summary>Reads the party's map of the place it stands in, and what a detection reveals on it.</summary>
    /// <param name="maps">The owner of what the party has mapped, or null when the session holds none.</param>
    /// <param name="world">The world the party stands in, or null when the session holds none.</param>
    /// <param name="running">The effects the party carries, or null when its ruleset stated no magic.</param>
    /// <returns>The automap as a panel reads it.</returns>
    public static MapSnapshot From(PartyMaps? maps, SessionWorld? world, IRunningSpellEffects? running = null)
    {
        if (maps is null) return None;
        IMapRule rule = maps.Rule;
        MapWords words = rule.Words;
        // A session that maps but holds no world yet — one still making its party — says exactly that: the
        // mechanism is there and there is nowhere to draw, which is not the same fact as a ruleset that stated
        // no automap, and a screen must be able to tell them apart.
        if (world is null)
        {
            return new MapSnapshot(true, false, words.Title, string.Empty, string.Empty, string.Empty, words.NoWorld, 0, 0);
        }

        PlaceDefinition place = world.Graph.Require(world.Place);
        if (maps.Maps.For(world.Place) is not { } map)
        {
            return new MapSnapshot(true, false, words.Title, place.Id.Value, place.Name, SessionProjection.WireName(place.Kind), words.Unmapped, 0, 0);
        }

        MapTerritory? territory = maps.Find(world.Place);
        int seen = territory?.SeenCount ?? 0;
        string state = seen == 0 ? words.Unseen : words.Seen(seen, map.Grid.Cells);

        // The party's own pose decides the window, and the window decides everything the drawing says: this is
        // what makes the drawing a function of state rather than a thing a screen scrolled to. The party's own
        // place and facing are read first, because a drawing without them is not a drawing: a shape whose
        // numbers are not numbers is one no screen could place, and the panel's own sentence is the honest
        // answer where the arithmetic cannot supply one.
        MapZoom zoom = rule.Zoom(map.Grid);
        PlacePose pose = world.Party.PlacePose;
        int cells = Math.Max(1, zoom.Cells);
        double cell = DrawingSize / cells;
        MapCell here = map.Grid.CellAt(pose.X, pose.Y);
        int left = here.Column - (cells / 2);
        int top = here.Row - (cells / 2);
        double? partyX = Placeable(pose.X, map.Grid.OriginX, left, map.Grid.CellSize, cell);
        double? partyY = Placeable(pose.Y, map.Grid.OriginY, top, map.Grid.CellSize, cell);
        double facing = rule.FacingDegrees(pose.Yaw);

        // What a detection reveals is read here, from the same state the rest of the projection is built from,
        // and it is deliberately read before the drawing is filled so its marks can be marked as revealed.
        MapReveal? reveal = rule.Reveal(DetectionView(world, running));

        MapDrawingSnapshot? drawing = partyX is { } x && partyY is { } y && double.IsFinite(facing)
            ? new MapDrawingSnapshot(
                zoom.Rung,
                zoom.Rungs,
                cells,
                DrawingSize,
                territory is null ? [] : Runs(map, territory, rule, place.Kind, left, top, cells, cell),
                Marks(
                    map,
                    territory,
                    rule,
                    world.Population.PlacementsOf(world.Place),
                    left,
                    top,
                    cells,
                    cell,
                    reveal),
                x,
                y,
                facing)
            : null;

        return new MapSnapshot(
            true,
            true,
            words.Title,
            place.Id.Value,
            place.Name,
            SessionProjection.WireName(place.Kind),
            state,
            seen,
            map.Grid.Cells,
            reveal?.Scope ?? string.Empty,
            reveal?.Message ?? string.Empty,
            reveal?.EndsAt is { } ends ? Date(ends) : string.Empty,
            drawing);
    }

    /// <summary>Whether a detection is marking anything on the place the party stands in now.</summary>
    /// <remarks>
    /// A detection marks what lives here wherever it has walked to, so a drawing that carries one is a reading of
    /// this moment rather than of the map: a reader that keeps a drawing between publishes asks this first, and
    /// keeps nothing while it is true. The answer is the game's own <see cref="IMapRule.Reveal"/>, asked of the
    /// same view the drawing asks it of.
    /// </remarks>
    /// <param name="maps">The party's maps.</param>
    /// <param name="world">The live world, when the session has one.</param>
    /// <param name="running">What spells have left running, when the game reports it.</param>
    internal static bool Detecting(PartyMaps? maps, SessionWorld? world, IRunningSpellEffects? running) =>
        maps is not null &&
        world is not null &&
        maps.Maps.For(world.Place) is not null &&
        maps.Rule.Reveal(DetectionView(world, running)) is not null;

    /// <summary>What a detection over the party's place is asked of: where it stands and what stands there.</summary>
    private static MapDetectionView DetectionView(SessionWorld world, IRunningSpellEffects? running) => new(
        world.Place,
        world.Party.PlacePose,
        world.Population.PlacementsOf(world.Place),
        world.Population.Entities,
        running?.Running ?? []);

    /// <summary>
    /// Where one of the party's own coordinates falls across or down the drawing, or null when it has no place
    /// there at all.
    /// </summary>
    /// <remarks>
    /// The arithmetic is the party's own position read against the window, and it answers nothing — rather than
    /// a hole a screen would be asked to draw — when the position is not a number, or when reading it against
    /// the place's grid is not one either.
    /// </remarks>
    private static double? Placeable(double point, double origin, int start, double cellSize, double cell)
    {
        double place = ((point - origin - (start * cellSize)) / cellSize) * cell;
        return double.IsFinite(place) ? place : null;
    }

    /// <summary>
    /// The runs of seen cells the window shows, one rectangle per run of one kind.
    /// </summary>
    /// <remarks>
    /// A row is walked left to right and adjacent cells the game draws the same way become one rectangle, so
    /// what a projection carries is the shape of the map rather than one entry per cell. A run is only ever
    /// made of cells the party has seen: ground it has not seen contributes nothing, which is what keeps an
    /// unwalked map empty instead of painted.
    /// </remarks>
    private static List<MapCellSnapshot> Runs(
        PlaceMap map,
        MapTerritory territory,
        IMapRule rule,
        PlaceKind place,
        int left,
        int top,
        int cells,
        double cell)
    {
        List<MapCellSnapshot> drawn = [];
        for (int row = 0; row < cells; row++)
        {
            int gridRow = top + row;
            if (gridRow < 0 || gridRow >= map.Grid.Rows) continue;
            int runStart = -1;
            string runKind = string.Empty;
            for (int column = 0; column <= cells; column++)
            {
                int gridColumn = left + column;
                bool inside = column < cells && gridColumn >= 0 && gridColumn < map.Grid.Columns;
                string kind = inside && territory.IsSeen(new MapCell(gridColumn, gridRow))
                    ? rule.CellKind(place, map.KindAt(new MapCell(gridColumn, gridRow)))
                    : string.Empty;
                if (kind.Length > 0 && kind == runKind)
                {
                    continue;
                }

                if (runStart >= 0)
                {
                    drawn.Add(new MapCellSnapshot(runStart * cell, row * cell, (column - runStart) * cell, cell, runKind));
                }

                runStart = kind.Length > 0 ? column : -1;
                runKind = kind;
            }
        }

        return drawn;
    }

    /// <summary>
    /// What the map marks: the place's own features on ground the party has seen, then what a detection
    /// revealed.
    /// </summary>
    /// <remarks>
    /// A place's feature is only drawn once the ground it stands on is on the map, which is the whole reason
    /// the automap fills in rather than showing a place's contents from the doorway. A detection's marks are
    /// the opposite by design: what it reveals is what the party can see now, so they are drawn wherever they
    /// stand — and they are the only thing on the drawing that is not the party's own memory.
    /// </remarks>
    private static List<MapMarkSnapshot> Marks(
        PlaceMap map,
        MapTerritory? territory,
        IMapRule rule,
        IReadOnlyList<PlacementDefinition> placements,
        int left,
        int top,
        int cells,
        double cell,
        MapReveal? reveal)
    {
        List<MapMarkSnapshot> marks = [];
        if (territory is not null)
        {
            foreach (PlacementDefinition placement in placements)
            {
                string kind = rule.Mark(placement);
                if (kind.Length == 0) continue;

                // The ground it stands on has to be on the map first: a place's own features are what the party
                // walks past, not what the doorway shows it.
                if (!territory.IsSeen(map.Grid.CellAt(placement.Pose.X, placement.Pose.Y))) continue;
                if (Project(map, placement.Pose.X, placement.Pose.Y, left, top, cells, cell) is not { } at) continue;
                marks.Add(new MapMarkSnapshot(
                    placement.Content.ToString(),
                    kind,
                    Label(placement),
                    at.X,
                    at.Y,
                    Detected: false));
            }
        }

        if (reveal is null) return marks;
        foreach (MapMark mark in reveal.Marks)
        {
            if (Project(map, mark.X, mark.Y, left, top, cells, cell) is not { } at) continue;
            marks.Add(new MapMarkSnapshot(mark.Id, mark.Kind, mark.Label, at.X, at.Y, Detected: true));
        }

        return marks;
    }

    /// <summary>Where a point of the place falls in the drawing, or null when the window does not show it.</summary>
    /// <remarks>
    /// A point that is not a point is not shown: every comparison against a hole is false, so a mark whose own
    /// position is not a number would otherwise pass the window's own test and be drawn nowhere in particular.
    /// </remarks>
    private static (double X, double Y)? Project(
        PlaceMap map,
        double x,
        double z,
        int left,
        int top,
        int cells,
        double cell)
    {
        if (!double.IsFinite(x) || !double.IsFinite(z)) return null;
        double column = ((x - map.Grid.OriginX) / map.Grid.CellSize) - left;
        double row = ((z - map.Grid.OriginY) / map.Grid.CellSize) - top;
        if (!double.IsFinite(column) || !double.IsFinite(row)) return null;
        if (column < 0 || column > cells || row < 0 || row > cells) return null;
        return (column * cell, row * cell);
    }

    /// <summary>What a placement is called, as content names it, or its own identity when it names nothing.</summary>
    private static string Label(PlacementDefinition placement)
    {
        string name = placement.Source.GetString("name");
        return name.Length > 0 ? name : placement.Content.ToString();
    }

    /// <summary>The calendar's own form for a day, as every other block publishes dates in.</summary>
    private static string Date(Time.GameDate date) =>
        date.MinuteText;

    /// <summary>Writes the automap block: what the party has mapped of the place it stands in.</summary>
    /// <remarks>
    /// <para>
    /// The drawing is sent as numbers in the drawing's own space — where each run of seen cells starts, how
    /// wide it is, what kind it is, where the party stands and which way it faces — so a screen places shapes
    /// and computes no scale, no offset, and no position of its own. Unseen ground contributes no shape at
    /// all, and what a detection revealed is sent as marks flagged as revealed, which is the whole of what a
    /// detection puts on the map.
    /// </para>
    /// <para>
    /// <b>A block with no drawing is published as no drawing.</b> There is nothing to draw whenever the
    /// session holds no map owner, holds no world yet, or stands in a place content states no map for, and the
    /// block says so with an absent drawing rather than one of zero extent — a screen handed a window of no
    /// cells and a size of nothing would be handed a shape it can only divide into holes.
    /// </para>
    /// </remarks>
    /// <param name="builder">The projection being built.</param>
    /// <returns>The block's node.</returns>
    internal uint Write(UiValueBuilder builder) =>
        builder.Object(
            ("available", builder.Boolean(Available)),
            ("mapped", builder.Boolean(Mapped)),
            ("title", builder.String(Title)),
            ("place", builder.String(Place)),
            ("name", builder.String(Name)),
            ("kind", builder.String(Kind)),
            ("state", builder.String(State)),
            ("seen", builder.Number(Seen)),
            ("total", builder.Number(Total)),
            ("detection", builder.String(Detection)),
            ("detectionMessage", builder.String(DetectionMessage)),
            ("detectionEnds", builder.String(DetectionEnds)),
            ("drawing", Drawing is { } shape ? shape.Write(builder) : builder.Null()));
}
