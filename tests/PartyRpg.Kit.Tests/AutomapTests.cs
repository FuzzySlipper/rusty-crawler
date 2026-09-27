using System.Text.Json;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Journal;
using PartyRpg.Kit.Knowledge;
using PartyRpg.Kit.Magic;
using PartyRpg.Kit.Maps;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// What a party has mapped: the owner that fills it as the party walks, the drawing projected from it, the
/// maps book that reads the same owner, and the boundary a detection has to stay inside.
/// </summary>
/// <remarks>
/// <para>
/// The maps this suite drives are the mechanism with places its own test states, which is the point of the
/// seam: the kit holds no terrain, no cell size, and no game's zoom ladder, so this test states an eight by
/// eight grid over two places and demands the same mechanism keep what a party walks of them. What only this
/// suite can prove is that walking fills the map progressively, that ground the party has not seen stays
/// unseen, that one sweep adds no more than the rule allows, that a place the world restores clears none of
/// what the party has mapped, that a save and a load draw the walked map rather than a full one, and that the
/// maps book's page per place is the same owner's count the drawing is built from.
/// </para>
/// <para>
/// The detection boundary is proven by a rule whose claim this suite states itself: the marks a detection adds
/// are exactly the ones its own reveal names, no square becomes walked because a spell looked at it, and
/// nothing is written down — a detection is a reading of the world at the moment it is taken.
/// </para>
/// </remarks>
public sealed class AutomapTests
{
    private static readonly PlaceId Region = new("1");
    private static readonly PlaceId Interior = new("2");
    private static readonly ContentLayout Layout = new("packs", "imports", "bundles");

    [Fact]
    public void Walking_a_place_fills_its_map_progressively_and_unseen_ground_stays_unseen()
    {
        PartyMaps maps = Maps(Region);

        // The party arrives in the first square of a place whose own map is eight by eight squares, and its own
        // square and the ones its sight reaches are on the map; the far corner is not.
        Assert.True(maps.Observe(Region, new PlacePose(1, 1, 0, 0, 0)));
        MapTerritory territory = Territory(maps, Region);
        int first = territory.SeenCount;
        Assert.True(first > 1);
        Assert.True(territory.IsSeen(new MapCell(0, 0)));
        Assert.False(territory.IsSeen(new MapCell(7, 7)));

        // Walking to the far corner fills what lies along the way and leaves the rest unseen: the map grows with
        // the walking rather than arriving whole.
        Assert.True(maps.Observe(Region, new PlacePose(30, 30, 0, 0, 0)));
        int second = territory.SeenCount;
        Assert.True(second > first);
        Assert.True(second < territory.Grid.Cells);
        Assert.True(territory.IsSeen(new MapCell(7, 7)));

        // Standing still, and shifting inside one square, change nothing: the sweep runs when the party's own
        // square changes, which is what keeps a held key's cost the same at the thousandth update as the first.
        Assert.False(maps.Observe(Region, new PlacePose(31, 31, 0, 0, 0)));
        Assert.Equal(second, territory.SeenCount);
    }

    [Fact]
    public void What_the_party_cannot_see_is_not_added_even_inside_its_sight_radius()
    {
        // The sight line is the game's own answer, and this one is a wall along the row the party stands in: it
        // sees the row and none of the rows behind the wall, however close they are.
        PartyMaps maps = Maps([Region], sight: (_, to) => to.Y <= 4);
        Assert.True(maps.Observe(Region, new PlacePose(1, 1, 0, 0, 0)));
        MapTerritory territory = Territory(maps, Region);
        Assert.True(territory.IsSeen(new MapCell(0, 0)));
        Assert.True(territory.IsSeen(new MapCell(3, 0)));
        Assert.False(territory.IsSeen(new MapCell(0, 1)));
        Assert.Equal(4, territory.SeenCount);

        // A party whose game states no sight line at all — a product with no spatial service — has a map of the
        // squares it stood in and nothing more, which is the honest reading rather than a map of what it might
        // have seen.
        PartyMaps blind = Maps([Region], sight: (_, _) => false);
        Assert.True(blind.Observe(Region, new PlacePose(1, 1, 0, 0, 0)));
        Assert.Equal(1, Territory(blind, Region).SeenCount);
    }

    [Fact]
    public void One_sweep_adds_no_more_cells_than_the_game_states()
    {
        // The bound is the game's, and the mechanism enforces it rather than trusting a place to be small: a
        // rule that allows two cells a sweep adds two, however wide its radius is. The square the party stands
        // in is not part of the sweep — entering it is what walking does — so the first look adds it and one
        // more beside it.
        PartyMaps maps = new(new TestRule { Radius = 8, Sweep = 2 }, new TestMaps(Region));
        Assert.True(maps.Observe(Region, new PlacePose(1, 1, 0, 0, 0)));
        MapTerritory territory = Territory(maps, Region);
        Assert.Equal(3, territory.SeenCount);

        // And what a party can ever see of one place is that place's own map: a long walk fills a finite map
        // rather than growing one.
        for (int step = 0; step < 40; step++)
        {
            maps.Observe(Region, new PlacePose(1 + (step % 8 * 4), 1 + (step / 8 * 4), 0, 0, 0));
        }

        Assert.True(territory.SeenCount < territory.Grid.Cells);
        Assert.Equal(40 + 2 - 40, territory.SeenCount > 2 ? 2 : territory.SeenCount);
    }

    [Fact]
    public void A_place_the_world_restores_clears_none_of_what_the_party_has_mapped()
    {
        GameClock clock = Clock();
        PartyMaps maps = Maps(Region);
        SessionWorld world = World(clock);
        world.ArriveAt(Region, new PlacePose(1, 1, 0, 0, 0));
        Assert.True(maps.Observe(Region, world.Party.PlacePose));
        MapTerritory territory = Territory(maps, Region);
        int seen = territory.SeenCount;

        // The world empties the place and the clock restores its population, which is a change to what the world
        // currently is: the population comes back and the party's own map is exactly as it was, because the two
        // are different owners and only one of them the clock moves.
        world.Places.MarkCleared(Region);
        clock.Advance(GameDuration.FromHours(24 * 30));
        IReadOnlyList<PlaceState> restored = world.AdvanceTime();
        Assert.Contains(restored, state => state.Place == Region);
        Assert.False(world.Places.StateOf(Region).Cleared);
        Assert.Equal(seen, territory.SeenCount);
        Assert.True(territory.IsSeen(new MapCell(0, 0)));
    }

    [Fact]
    public void A_map_survives_a_save_and_a_load_shows_what_was_walked_and_not_everything()
    {
        PartyMaps maps = Maps(Region, Interior);
        Assert.True(maps.Observe(Region, new PlacePose(1, 1, 0, 0, 0)));
        Assert.True(maps.Observe(Interior, new PlacePose(1, 1, 0, 0, 0)));
        int walked = Territory(maps, Region).SeenCount;
        Assert.True(walked > 0 && walked < Territory(maps, Region).Grid.Cells);

        // The document is the whole of what the party has mapped, and it carries the grid the squares were seen
        // on rather than a date or a visit: what a load needs is where the ground was.
        MapSave captured = maps.Capture();
        Assert.Equal(2, captured.Places.Count);
        Assert.Empty(captured.Problems(Graph(), PartyMaps.MaxPlaces));

        PartyMaps loaded = new(new TestRule(), new TestMaps(Region, Interior), captured);
        Assert.Equal(2, loaded.PlacesMapped);
        Assert.Equal(walked, Territory(loaded, Region).SeenCount);
        Assert.Equal(Territory(maps, Interior).SeenCount, Territory(loaded, Interior).SeenCount);

        // The loaded map is the walked one rather than a full one: the squares the party never stood in are
        // still unseen, which is the difference a save has to keep.
        Assert.False(Territory(loaded, Region).IsSeen(new MapCell(7, 7)));
        Assert.Equal(
            Territory(maps, Region).Seen().ToArray(),
            Territory(loaded, Region).Seen().ToArray());
    }

    [Fact]
    public void A_save_that_contradicts_the_world_or_its_own_grid_is_refused_with_every_problem_named()
    {
        MapSave save = new(
        [
            new MapTerritorySave("1", 0, 0, 64, 8, 8, "0000000000000000"),
            new MapTerritorySave("1", 0, 0, 64, 8, 8, "0000000000000000"),
            new MapTerritorySave("99", 0, 0, 64, 8, 8, "0000000000000000"),
            new MapTerritorySave("2", 0, 0, 0, 8, 8, "0000000000000000"),
            new MapTerritorySave("2", 0, 0, 64, 8, 8, "00"),
        ]);

        IReadOnlyList<string> problems = save.Problems(Graph(), PartyMaps.MaxPlaces);
        Assert.Contains(problems, problem => problem.Contains("mapped twice", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.Contains("the world has no such place", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.Contains("not a grid", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.Contains("would not line up", StringComparison.Ordinal));

        // A save that only holds what the party really walked is not refused, and the section is judged by the
        // one rule that keeps it finite: what a party maps is its own bound rather than a content limit.
        Assert.Empty(new MapSave([new MapTerritorySave("1", 0, 0, 4, 8, 8, "ffffffffffffffff")]).Problems(Graph(), PartyMaps.MaxPlaces));
        Assert.Contains(
            new MapSave([new MapTerritorySave("1", 0, 0, 4, 8, 8, "ffffffffffffffff")]).Problems(Graph(), limit: 0),
            problem => problem.Contains("holds maps of 1 places", StringComparison.Ordinal));
    }

    [Fact]
    public void The_automap_is_drawn_from_the_party_own_state_with_its_square_and_facing()
    {
        GameClock clock = Clock();
        PartyMaps maps = Maps(Region);
        SessionWorld world = World(clock);
        world.ArriveAt(Region, new PlacePose(1, 1, 0, 0, 0));
        maps.Observe(Region, world.Party.PlacePose);

        MapSnapshot map = MapSnapshot.From(maps, world);
        Assert.True(map.Available);
        Assert.True(map.Mapped);
        Assert.Equal(Region.Value, map.Place);
        Assert.Equal("the meadow", map.Name);
        Assert.Equal(16, map.Seen);
        Assert.Equal(64, map.Total);

        // The drawing is numbers in its own space: one run per kind of square the party has seen, the party's own
        // position and facing among them, and no rectangle at all for ground it has not seen.
        MapDrawingSnapshot drawing = Assert.IsType<MapDrawingSnapshot>(map.Drawing);
        Assert.Equal(MapSnapshot.DrawingSize, drawing.Size);
        Assert.InRange(drawing.PartyX, 0, MapSnapshot.DrawingSize);
        Assert.InRange(drawing.PartyY, 0, MapSnapshot.DrawingSize);
        Assert.Equal(0, drawing.Facing);
        Assert.All(drawing.Drawn, run => Assert.True(run.Width > 0 && run.Height > 0));
        Assert.All(drawing.Drawn, run => Assert.Contains(run.Kind, new[] { "grass", "water" }));
        Assert.Equal(16, drawing.Drawn.Sum(run => (int)Math.Round(run.Width / run.Height)));

        // The place's own feature on ground the party has walked is marked, and the one standing beyond what it
        // has seen is not: a place's contents are not drawn from the doorway.
        Assert.Equal(["door:a-door"], drawing.Marks.Select(mark => mark.Id));

        // A place content carries no map for is not a place with an empty map, and a session that holds no map
        // owner at all says that instead.
        PartyMaps none = Maps();
        Assert.False(MapSnapshot.From(none, world).Mapped);
        Assert.Equal("no map of this place", MapSnapshot.From(none, world).State);
        Assert.False(MapSnapshot.From(maps: null, world).Available);
    }

    [Fact]
    public void A_detection_marks_exactly_what_it_claims_and_fills_no_square()
    {
        GameClock clock = Clock();
        PartyMaps maps = Maps(Region);
        SessionWorld world = World(clock);
        world.ArriveAt(Region, new PlacePose(1, 1, 0, 0, 0));
        maps.Observe(Region, world.Party.PlacePose);
        int seen = Territory(maps, Region).SeenCount;
        PartyKnowledge knowledge = new(new TestKnowledge(), clock);

        // Nothing is running, so the map marks only the place's own features: the door the party has walked up
        // to, and nothing beyond what it has seen.
        MapSnapshot quiet = MapSnapshot.From(maps, world, new TestRunning());
        Assert.Empty(quiet.Detection);
        Assert.Equal(["door:a-door"], quiet.Drawing!.Value.Marks.Select(mark => mark.Id));

        // A detection whose own claim is two things adds exactly those two, flagged as revealed, and the ground
        // the party has seen is untouched: a spell that looked at a place does not walk it, and nothing is
        // written down for it.
        MapSnapshot detected = MapSnapshot.From(maps, world, new TestRunning("detect.two"));
        Assert.Equal("life", detected.Detection);
        Assert.Equal("life is marked", detected.DetectionMessage);
        Assert.Equal(
            ["creature:a-crawler", "creature:another-crawler"],
            detected.Drawing!.Value.Marks.Where(mark => mark.Detected).Select(mark => mark.Id).Order(StringComparer.Ordinal));
        Assert.Equal(2, detected.Drawing!.Value.Marks.Count(mark => mark.Detected));
        Assert.Equal(seen, Territory(maps, Region).SeenCount);
        Assert.Empty(knowledge.Notes);

        // A detection whose claim is one thing adds one, and one that claims nothing adds nothing: what reaches
        // the map is the detection's own answer rather than the mechanism's opinion about what is useful.
        MapSnapshot one = MapSnapshot.From(maps, world, new TestRunning("detect.one"));
        Assert.Equal("one mark", one.Detection);
        Assert.Equal(["the nearest crawler"], one.Drawing!.Value.Marks.Where(mark => mark.Detected).Select(mark => mark.Label));
        MapSnapshot empty = MapSnapshot.From(maps, world, new TestRunning("detect.none"));
        Assert.DoesNotContain(empty.Drawing!.Value.Marks, mark => mark.Detected);
        Assert.Equal(seen, Territory(maps, Region).SeenCount);
    }

    [Fact]
    public void The_maps_book_pages_are_the_same_owners_count_as_the_drawing()
    {
        GameClock clock = Clock();
        PartyMaps maps = Maps(Region, Interior);
        SessionWorld world = World(clock);
        world.ArriveAt(Region, new PlacePose(1, 1, 0, 0, 0));
        maps.Observe(Region, world.Party.PlacePose);
        maps.Observe(Interior, new PlacePose(1, 1, 0, 0, 0));

        PartyJournal journal = new(new TestJournal(), clock);
        JournalSnapshot books = JournalSnapshot.From(journal, quests: null, world, clock, knowledge: null, maps);
        JournalBookSnapshot page = books.Books.Single(book => book.Kind == "maps");

        // One page per place the party holds a map of, and each page's count is the owner's own: the same count
        // the drawing of that place is built from, which is what keeps a book and a map from disagreeing.
        Assert.True(page.Available);
        Assert.Equal("2 places mapped", page.State);
        Assert.Equal(["1", "2"], page.Rows.Select(row => row.Id));
        Assert.Equal(["region", "interior"], page.Rows.Select(row => row.Detail));
        Assert.Equal("16 of 64 squares walked (25%)", page.Rows[0].State);
        Assert.Equal(["16 of 64 squares walked (25%)", "16 of 64 squares walked (25%)"], page.Rows.Select(row => row.State));
        Assert.True(page.Rows[0].Marked);
        Assert.False(page.Rows[1].Marked);
        Assert.Equal("map", page.Rows[0].Source);

        // A book with no map owner names the owner it waits for rather than showing a party that has walked
        // nowhere, and the notes book beside it is a different owner's answer.
        JournalBookSnapshot without = JournalSnapshot.From(journal, quests: null, world, clock).Books.Single(book => book.Kind == "maps");
        Assert.False(without.Available);
        Assert.Contains("no map owner", without.State, StringComparison.Ordinal);
        Assert.Equal("maps", page.Kind);
    }

    /// <summary>The party's map of one place, which the test asserts about.</summary>
    private static MapTerritory Territory(PartyMaps maps, PlaceId place) =>
        maps.Find(place) ?? throw new InvalidOperationException($"The party holds no map of place '{place}'.");

    /// <summary>The clock these tests run on: a session that began on the first day of 1168, at nine.</summary>
    private static GameClock Clock() => new(
        GameCalendar.TwelveMonthsOfFourWeeks,
        new GameDate(1168, 1, 1, 9, 0, 0),
        new GameTimeScale(30),
        new DaylightWindow(new TimeOfDay(5, 0), new TimeOfDay(21, 0)));

    /// <summary>The map owner under this suite's rule, over the places' own maps this suite states.</summary>
    private static PartyMaps Maps(params PlaceId[] places) => Maps(places, sight: null);

    /// <summary>The map owner under this suite's rule and one stated sight line.</summary>
    private static PartyMaps Maps(PlaceId[] places, Func<PlacePose, PlacePose, bool>? sight) =>
        new(new TestRule { Sight = sight }, new TestMaps(places));

    /// <summary>The world the party stands in, which the drawing and the reset read.</summary>
    private static SessionWorld World(GameClock clock)
    {
        ContentCatalog catalog = ContentCatalogLoader.Load(
            new InMemoryContentSource()
                .Add("packs/world/pack.json", Manifest())
                .Add("packs/world/places.json", Document(
                    "places",
                    "place",
                    """{ "id": "1", "kind": "region", "name": "the meadow", "respawnDays": 7, "entryPoints": [ { "id": "Party Start", "x": 1, "y": 1, "z": 0, "yaw": 0 } ], "placements": [ { "id": "a-door", "kind": "door", "name": "a shut door", "x": 1, "y": 2, "z": 0 }, { "id": "far-fount", "kind": "fountain", "name": "a far fountain", "x": 30, "y": 30, "z": 0 } ] }""",
                    """{ "id": "2", "kind": "interior", "name": "the cellar", "respawnDays": 7, "entryPoints": [ { "id": "Party Start", "x": 1, "y": 1, "z": 0, "yaw": 0 } ] }"""))
                .Add("packs/world/links.json", Document(
                    "links",
                    "travel-link",
                    """{ "id": "0", "fromPlace": "1", "toPlace": "2", "entryPoint": "Party Start" }""")),
            Layout).RequireValid();

        PlaceGraph graph = PlaceGraphLoader.Load(catalog);
        return new SessionWorld(
            graph,
            new PartyPoseOwner(new PartyPose(Region, new PlacePose(1, 1, 0, 0, 0)), new FacingRule(unitsPerTurn: 2048, minimumPitch: -512, maximumPitch: 512)),
            new PlaceStateLedger(graph, PlaceRespawnRule.FromContent()),
            new FreeTravel(),
            clock);
    }

    /// <summary>The graph alone, for the tests that judge a save against a world.</summary>
    private static PlaceGraph Graph() => PlaceGraph.From(
        [
            new PlaceDefinition(
                Region,
                PlaceKind.Region,
                "the meadow",
                [],
                new ContentEntry("1", JsonDocument.Parse("""{ "id": "1", "kind": "region" }""").RootElement)),
            new PlaceDefinition(
                Interior,
                PlaceKind.Interior,
                "the cellar",
                [],
                new ContentEntry("2", JsonDocument.Parse("""{ "id": "2", "kind": "interior" }""").RootElement)),
        ],
        []);

    private static string Manifest() =>
        """
        {
          "schemaVersion": 1,
          "packId": "world",
          "kind": "definitions",
          "provenance": { "description": "authored for a test" },
          "documents": [
            { "path": "places.json", "documentId": "places", "definitionKind": "place" },
            { "path": "links.json", "documentId": "links", "definitionKind": "travel-link" }
          ]
        }
        """;

    private static string Document(string documentId, string definitionKind, params string[] entries) =>
        $$"""
        { "documentId": "{{documentId}}", "definitionKind": "{{definitionKind}}", "entries": [ {{string.Join(",", entries)}} ] }
        """;

    /// <summary>The places' own maps this suite states: eight by eight squares, one in seven of them water.</summary>
    private sealed class TestMaps : IPlaceMapSource
    {
        private readonly Dictionary<PlaceId, PlaceMap> _maps = [];

        internal TestMaps(params PlaceId[] places)
        {
            foreach (PlaceId place in places)
            {
                byte[] kinds = new byte[64];
                for (int index = 0; index < kinds.Length; index++) kinds[index] = index % 7 == 0 ? (byte)1 : (byte)0;
                _maps[place] = new PlaceMap(place, new MapGrid(0, 0, 4, 8, 8), kinds);
            }
        }

        public PlaceMap? For(PlaceId place) => _maps.GetValueOrDefault(place);
    }

    /// <summary>This suite's reading of its own maps: two kinds, a radius, a sweep bound, and one rung.</summary>
    private sealed class TestRule : IMapRule
    {
        internal Func<PlacePose, PlacePose, bool>? Sight { get; init; }

        internal int Radius { get; init; } = 3;

        internal int Sweep { get; init; } = 64;

        public MapWords Words { get; } = new(
            "Maps",
            "no map owner in this build",
            "the party is nowhere yet",
            "no map of this place",
            "nothing walked here yet",
            "nowhere mapped yet",
            (walked, total) => $"{walked} of {total} squares walked ({(total == 0 ? 0 : (int)Math.Round(walked * 100.0 / total))}%)",
            count => $"{count} place{(count == 1 ? string.Empty : "s")} mapped");

        public int SightRadius => Radius;

        public int MaxCellsPerSweep => Sweep;

        /// <summary>Whether nothing stands between two points, as this suite states it or as always clear.</summary>
        public bool Sees(PlacePose from, PlacePose to) => Sight?.Invoke(from, to) ?? true;

        public string CellKind(PlaceKind place, byte kind) => kind == 0 ? "grass" : "water";

        public string Mark(PlacementDefinition placement) =>
            placement.Content.Kind == "door" ? "door" : string.Empty;

        /// <summary>This suite's facing unit: a whole turn in 2048 units, as this game's own rule states.</summary>
        public double FacingDegrees(double yaw) => (yaw / 2048 * 360) % 360;

        public MapZoom Zoom(MapGrid grid) => new(1, 1, Math.Max(grid.Columns, grid.Rows));

        /// <summary>
        /// This suite's detection: the marks it claims are named by the effect the party carries.
        /// </summary>
        /// <remarks>
        /// The rule reads the running effect's own identity, so this suite can state a claim of one mark, two
        /// marks, or none and demand that exactly that reaches the map. The marks stand where the party has
        /// walked nothing, which is the boundary under test: a detection reveals what it names and never ground.
        /// </remarks>
        public MapReveal? Reveal(MapDetectionView view)
        {
            List<MapMark> marks = [];
            string scope = string.Empty;
            foreach (RunningSpellEffect effect in view.Running)
            {
                switch (effect.Effect.Value)
                {
                    case "detect.two":
                        scope = "life";
                        marks.Add(new MapMark("creature:a-crawler", "creature", "a crawler", 6, 6, Detected: true));
                        marks.Add(new MapMark("creature:another-crawler", "creature", "another crawler", 10, 10, Detected: true));
                        break;
                    case "detect.one":
                        scope = "one mark";
                        marks.Add(new MapMark("creature:nearest", "creature", "the nearest crawler", 6, 6, Detected: true));
                        break;
                    case "detect.none":
                        scope = "nothing";
                        break;
                    default:
                        continue;
                }
            }

            return scope.Length == 0
                ? null
                : new MapReveal(scope, $"{scope} is marked", EndsAt: null, marks);
        }
    }

    /// <summary>This suite's journal policy, which only has to name its books.</summary>
    private sealed class TestJournal : IJournalRule
    {
        public JournalBookWords Book(JournalBookKind book) => new(
            JournalBook.Word(book),
            "nothing",
            book == JournalBookKind.Maps ? "no map owner in this test" : "no owner");

        public string Phrase(JournalEntryKind kind) => kind.ToString();

        public bool WorthRecording(JournalEvent journalEvent) => true;
    }

    /// <summary>This suite's knowledge policy, which nothing here reports a discovery to.</summary>
    private sealed class TestKnowledge : IKnowledgeRule
    {
        public string Phrase(KnowledgeKind kind) => kind.ToString();

        public bool WorthLearning(KnowledgeReport report) => true;
    }

    /// <summary>A road that costs nothing, which is what a world built for a map test needs.</summary>
    private sealed class FreeTravel : ITravelCostRule
    {
        public TravelCostQuote Quote(TransitionRequest request) => TravelCostQuote.Payable(TravelCost.Free);
    }

    /// <summary>The effects this suite says the party carries, as the map reads them.</summary>
    private sealed class TestRunning : IRunningSpellEffects
    {
        private readonly List<RunningSpellEffect> _running = [];

        internal TestRunning(params string[] effects)
        {
            foreach (string effect in effects) _running.Add(new RunningSpellEffect(new EffectId(effect), 0, EndsAt: null));
        }

        public IReadOnlyList<RunningSpellEffect> Running => _running;

        public bool IsRunning(EffectId effect)
        {
            foreach (RunningSpellEffect running in _running)
            {
                if (running.Effect == effect) return true;
            }

            return false;
        }
    }
}
