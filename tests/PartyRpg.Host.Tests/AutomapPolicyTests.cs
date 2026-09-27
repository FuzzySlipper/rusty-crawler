using System.Globalization;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Maps;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.World;
using PartyRpg.Rulesets.MightAndMagic7;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Host.Tests;

/// <summary>
/// This game's automap against the real composition: the places' own maps read out of content, the drawing
/// the panel is given, the maps book that pages them, what a save and a load do to it, and the boundary a
/// detection has to stay inside.
/// </summary>
/// <remarks>
/// <para>
/// The kit's own suite proves the owner with maps it states itself. What only this suite can prove is that
/// this game's reading reaches it: the raster an importer writes is read back as a grid, the party's arrival
/// fills the square it arrived on, a place the world restores takes none of it away, a saved map is resumed
/// as the walked map rather than a full one, and the detection this game's own table states marks exactly the
/// points of interest the automap knows — including one standing on ground the party has never walked.
/// </para>
/// <para>
/// The composed case is written the way this game's own content is — the placed-map document the importer
/// writes, the placements a place carries, and the shipped detection spell by its own id — so the same suite
/// proves the shipped policy rather than a fixture invented for it.
/// </para>
/// </remarks>
public sealed class AutomapPolicyTests
{
    private static readonly CastIntentNames CastControls = new(
        ProductIdentity.CastAction,
        ProductIdentity.QuickSpellAction,
        ProductIdentity.UiActionContract);

    [Fact]
    public void This_game_draws_what_its_party_walks_and_marks_the_points_of_interest_it_passes()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(Content());
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            ProductTestContext.RulesetContext(context, ui) with { Cast = CastControls });
        session.Start();

        // The party starts where the scenario says, and the first update puts it there: the square it arrived
        // on is on its map, and the door standing in that square is marked because the ground under it has
        // been walked. The chest beyond what it has seen is not marked at all.
        session.Update(ProductTestContext.Update(1, 1));
        ProjectedNode map = Map(ui);
        Assert.True(map.Field("available").AsBoolean());
        Assert.True(map.Field("mapped").AsBoolean());
        Assert.Equal("Automap", map.Field("title").AsString());
        Assert.Equal("1", map.Field("place").AsString());
        Assert.Equal("Erathia", map.Field("name").AsString());
        Assert.Equal("region", map.Field("kind").AsString());
        double walked = map.Field("seen").AsNumber();
        Assert.Equal(64d, map.Field("total").AsNumber());
        Assert.InRange(walked, 1, 63);
        Assert.Equal(Walked(walked, 64), map.Field("state").AsString());
        Assert.Equal(["door:a-shut-door"], MarkIds(map));
        Assert.Empty(DetectedIds(map));

        // The drawing is the window the game's ladder shows at the place's own extent, the party's own square
        // in its own space, and one rectangle for the square it has walked: everything a screen needs, and
        // nothing a screen has to work out.
        ProjectedNode drawing = map.Field("drawing");
        // Eight squares across is smaller than the nearest rung of the game's own ladder, so the place is drawn
        // whole at that rung: one rung of four, sixteen squares across the window.
        Assert.Equal(1d, drawing.Field("rung").AsNumber());
        Assert.Equal(4d, drawing.Field("rungs").AsNumber());
        Assert.Equal(16d, drawing.Field("cells").AsNumber());
        Assert.Equal(MapSnapshot.DrawingSize, drawing.Field("size").AsNumber());
        ProjectedNode runs = drawing.Field("cellsDrawn");
        Assert.Equal(
            walked,
            Enumerable.Range(0, runs.Length())
                .Select(runs.Item)
                .Sum(run => run.Field("w").AsNumber() / run.Field("h").AsNumber()));
        Assert.All(
            Enumerable.Range(0, drawing.Field("cellsDrawn").Length()),
            position => Assert.Equal("low", drawing.Field("cellsDrawn").Item(position).Field("kind").AsString()));

        // The maps book is the same owner's page: one row for the place, and its count is the drawing's own.
        ProjectedNode page = Book(ui, "maps");
        Assert.True(page.Field("available").AsBoolean());
        Assert.Equal("1 place mapped", page.Field("state").AsString());
        Assert.Equal("Erathia", page.Field("rows").Item(0).Field("label").AsString());
        Assert.Equal("region", page.Field("rows").Item(0).Field("detail").AsString());
        Assert.Equal(Walked(walked, 64), page.Field("rows").Item(0).Field("state").AsString());
        Assert.True(page.Field("rows").Item(0).Field("marked").AsBoolean());
    }

    [Fact]
    public void A_detection_marks_exactly_what_this_game_says_it_looks_over_and_walks_nothing()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(Content());
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            ProductTestContext.RulesetContext(context, ui) with { Cast = CastControls });
        session.Start();
        session.Update(ProductTestContext.Update(1, 1));

        // Wizard Eye is the shipped table's own row 12, and this game reads it as a report over the places: the
        // automap marks this place's own points of interest, which is the door the party has walked past and
        // the chest standing on ground it has never seen.
        session.Update(ProductTestContext.Update(
            2,
            1,
            ProductTestContext.Payload("""{"action":"party.cast","member":0,"spell":"12","target":""}""")));
        ProjectedNode magic = ProjectedNode.Of(ui.Latest().Value).Field("magic");
        Assert.Equal("detection", magic.Field("effect").AsString());
        Assert.Contains("the map reads it until", magic.Field("message").AsString(), StringComparison.OrdinalIgnoreCase);

        ProjectedNode map = Map(ui);
        Assert.Equal("places", map.Field("detection").AsString());
        Assert.Contains("points of interest", map.Field("detectionMessage").AsString(), StringComparison.Ordinal);
        Assert.NotEqual(string.Empty, map.Field("detectionEnds").AsString());
        Assert.Equal(
            ["container:a-far-chest", "door:a-shut-door"],
            DetectedIds(map));

        // And it walked nothing: the squares the party has seen are exactly the one it arrived on, and the
        // book reads the same count as the drawing. What a detection adds is a reading of the world at the
        // moment it is taken, not ground the party has covered.
        double walked = map.Field("seen").AsNumber();
        Assert.Equal(Walked(walked, 64), map.Field("state").AsString());
        Assert.Equal(Walked(walked, 64), Book(ui, "maps").Field("rows").Item(0).Field("state").AsString());
        Assert.Equal(["door:a-shut-door"], MarkIds(map));
    }

    [Fact]
    public void A_map_is_resumed_as_the_ground_the_party_walked_and_a_reset_takes_none_of_it()
    {
        InMemoryPersistenceService persistence = new();
        (string Path, string Text)[] content = Content();
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(persistence, content);
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            ProductTestContext.RulesetContext(context, ui));
        session.Start();

        // The party arrives in the region and then moves to an interior, whose own map is walked the same way:
        // what a save carries is two places rather than one. Both arrivals go through the world's own path, so
        // each place is marked visited and its own population schedule starts where the party first stood.
        session.Update(ProductTestContext.Update(1, 1));
        ((MightAndMagic7Session)session).World!.ArriveAt(new PlaceId("1"), PlacePose.Origin);
        session.Update(ProductTestContext.Update(2, 1));
        ((MightAndMagic7Session)session).World!.ArriveAt(new PlaceId("2"), PlacePose.Origin);
        session.Update(ProductTestContext.Update(3, 1));
        Assert.Equal("The cellar", Map(ui).Field("name").AsString());
        Assert.Equal("interior", Map(ui).Field("kind").AsString());

        SessionSave document = MightAndMagic7Ruleset.Instance.Save(session);
        Assert.Equal(2, document.Maps.Places.Count);
        Assert.All(document.Maps.Places, place => Assert.Equal(64, place.Columns * place.Rows));

        // The world restores the region's population as game time passes, which is a change to what the world
        // currently is: the party's map of it is exactly as it was.
        session.Update(ProductTestContext.Update(4, admittedSteps: 2 * 2880, stepSeconds: 1.0));
        PlaceState state = ((MightAndMagic7Session)session).World!.Places.StateOf(new PlaceId("1"));
        Assert.True(state.RespawnCount >= 1, "the place's population was restored by the clock");

        (ProductCreateContext resumedContext, RecordingUiService resumedUi) = ProductTestContext.Create(persistence, content);
        using IGameSession resumed = MightAndMagic7Ruleset.Instance.ResumeSession(
            ProductTestContext.RulesetContext(resumedContext, resumedUi) with { Start = SessionStart.Resume });
        resumed.Start();

        // The resumed map is the ground the party walked and never the whole place: the squares it never stood
        // in are still unseen, and the book reads the same two pages it did before.
        ProjectedNode map = Map(resumedUi);
        Assert.Equal("2", map.Field("place").AsString());
        double walked = map.Field("seen").AsNumber();
        Assert.InRange(walked, 1, 63);
        Assert.Equal(64d, map.Field("total").AsNumber());
        Assert.Equal(Walked(walked, 64), map.Field("state").AsString());
        ProjectedNode page = Book(resumedUi, "maps");
        Assert.Equal("2 places mapped", page.Field("state").AsString());
        Assert.Equal(
            [Walked(walked, 64), Walked(walked, 64)],
            [page.Field("rows").Item(0).Field("state").AsString(), page.Field("rows").Item(1).Field("state").AsString()]);
        Assert.Equal("interior", page.Field("rows").Item(1).Field("detail").AsString());
    }

    [Fact]
    public void A_detection_this_game_leaves_running_holds_the_clock_and_so_a_save_is_refused_by_name()
    {
        InMemoryPersistenceService persistence = new();
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(persistence, Content());
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            ProductTestContext.RulesetContext(context, ui) with { Cast = CastControls });
        session.Start();
        session.Update(ProductTestContext.Update(1, 1));

        // This game's detections are timed effects like its wards: the shipped row states what they look over
        // and nothing about how long they run, so this build gives them the donor's own hours-per-level
        // duration, and the deadline is registered with the session's one clock.
        session.Update(ProductTestContext.Update(
            2,
            1,
            ProductTestContext.Payload("""{"action":"party.cast","member":0,"spell":"12","target":""}""")));
        Assert.Equal("places", Map(ui).Field("detection").AsString());

        // A deadline in a save is the one thing this build does not carry yet: the clock refuses to be written
        // with work it cannot hand back, so the save is refused by name while the detection runs — the same
        // answer a ward gets, and the reason the automap test above saves before it casts.
        SessionSaveException refused = Assert.Throws<SessionSaveException>(() => MightAndMagic7Ruleset.Instance.Save(session));
        Assert.Contains("scheduled deadline(s)", refused.Message, StringComparison.Ordinal);
        Assert.Contains("no owner could rebuild on load", string.Join(" ", refused.Problems), StringComparison.Ordinal);
        Assert.Null(persistence.Payload("sessions", "session"));
    }

    /// <summary>The sentence this game words a map's progress with, computed the way the game computes it.</summary>
    private static string Walked(double seen, int total) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{seen:0} of {total} squares walked ({(int)Math.Round(seen * 100.0 / total, MidpointRounding.AwayFromZero)}%)");

    /// <summary>The automap block as the panel reads it.</summary>
    private static ProjectedNode Map(RecordingUiService ui) => ProjectedNode.Of(ui.Latest().Value).Field("map");

    /// <summary>One of the five books as the panel reads it.</summary>
    private static ProjectedNode Book(RecordingUiService ui, string kind)
    {
        ProjectedNode books = ProjectedNode.Of(ui.Latest().Value).Field("journal").Field("books");
        for (int position = 0; position < books.Length(); position++)
        {
            ProjectedNode book = books.Item(position);
            if (book.Field("kind").AsString() == kind) return book;
        }

        throw new KeyNotFoundException($"The journal carried no '{kind}' book.");
    }

    /// <summary>What the automap marks as the place's own features, which is what the party has walked past.</summary>
    private static string[] MarkIds(ProjectedNode map) => Marks(map, detected: false);

    /// <summary>What a detection put on the automap, which is what it claims and nothing else.</summary>
    private static string[] DetectedIds(ProjectedNode map) => Marks(map, detected: true);

    private static string[] Marks(ProjectedNode map, bool detected)
    {
        ProjectedNode marks = map.Field("drawing").Field("marks");
        return
        [
            .. Enumerable.Range(0, marks.Length())
                .Select(marks.Item)
                .Where(mark => mark.Field("detected").AsBoolean() == detected)
                .Select(mark => mark.Field("id").AsString())
                .Order(StringComparer.Ordinal),
        ];
    }

    /// <summary>
    /// The content this suite reads: two places of this game's own shape with the automap raster an import
    /// writes for them, a door the party walks past, a chest on ground it has not, and a party that knows the
    /// shipped detection spell.
    /// </summary>
    private static (string Path, string Text)[] Content() =>
    [
        ProductTestContext.Bundle("partyrpg-default", "world"),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/pack.json",
            """
            {
              "schemaVersion": 1,
              "packId": "world",
              "kind": "definitions",
              "provenance": { "description": "authored for a test" },
              "documents": [
                { "path": "places.json", "documentId": "places", "definitionKind": "place" },
                { "path": "place-map.json", "documentId": "place-map", "definitionKind": "place-map" },
                { "path": "spells.json", "documentId": "spells", "definitionKind": "spell" },
                { "path": "skills.json", "documentId": "skills", "definitionKind": "skill" },
                { "path": "start.json", "documentId": "start", "definitionKind": "scenario-start" },
                { "path": "party.json", "documentId": "party", "definitionKind": "scenario-party" }
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/places.json",
            """
            {
              "documentId": "places",
              "definitionKind": "place",
              "entries": [
                { "id": "1", "kind": "region", "name": "Erathia", "respawnDays": 1,
                  "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
                  "placements": [
                    { "id": "a-shut-door", "kind": "door", "name": "a shut door", "x": 100, "y": 0, "z": 0 },
                    { "id": "a-far-chest", "kind": "container", "name": "a far chest", "x": 600, "y": 0, "z": 0 } ] },
                { "id": "2", "kind": "interior", "name": "The cellar", "respawnDays": 1,
                  "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ] }
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/place-map.json",
            $$"""
            {
              "documentId": "place-map",
              "definitionKind": "place-map",
              "entries": [
                { "id": "1", "kind": "region", "cellSize": 128, "origin": [ 0, 0 ], "columns": 8, "rows": 8,
                  "kinds": "{{Cells(64, open: PlaceMapsCells.LowGround, wall: [])}}" },
                { "id": "2", "kind": "interior", "cellSize": 128, "origin": [ 0, 0 ], "columns": 8, "rows": 8,
                  "kinds": "{{Cells(64, open: PlaceMapsCells.Floor, wall: [0, 9])}}" }
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/spells.json",
            """
            {
              "documentId": "spells",
              "definitionKind": "spell",
              "entries": [ { "id": "12", "school": "Air", "level": 1, "name": "Wizard Eye", "resist": "0" } ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/skills.json",
            """
            {
              "documentId": "skills",
              "definitionKind": "skill",
              "entries": [ { "id": "Air" } ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/start.json",
            """
            { "documentId": "start", "definitionKind": "scenario-start", "entries": [ { "id": "start", "place": "1", "entryPoint": "Party Start" } ] }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/party.json",
            """
            {
              "documentId": "party",
              "definitionKind": "scenario-party",
              "entries": [
                {
                  "id": "party", "coins": 0, "food": 6, "reputation": 0, "fame": 0,
                  "members": [
                    { "name": "Aelina", "race": "Elf", "class": "Sorcerer", "level": 5, "hitPoints": 30,
                      "spellPoints": 40,
                      "attributes": [ { "id": "Might", "value": 9 }, { "id": "Intellect", "value": 40 },
                                      { "id": "Personality", "value": 15 }, { "id": "Endurance", "value": 20 },
                                      { "id": "Accuracy", "value": 20 }, { "id": "Speed", "value": 25 },
                                      { "id": "Luck", "value": 13 } ],
                      "skills": [ { "id": "Air", "level": 3, "tier": 1, "pointsSpent": 3 } ],
                      "spells": [ "12" ], "conditions": [] }
                  ]
                }
              ]
            }
            """),
    ];

    /// <summary>One place's own automap raster, as the importer writes it: two digits a square.</summary>
    private static string Cells(int cells, string open, int[] wall)
    {
        string[] squares = new string[cells];
        for (int index = 0; index < cells; index++)
        {
            squares[index] = wall.Contains(index) ? PlaceMapsCells.Outline : open;
        }

        return string.Concat(squares);
    }

    /// <summary>The two numbers the placed-map layout gives a region's and an interior's squares.</summary>
    /// <remarks>
    /// A region's square is the band of its own height map and an interior's says whether one of the level's own
    /// outlines passes through it, so the region is drawn as relief and the interior as a floor plan. This
    /// fixture writes a low region and an interior with two wall squares, which is enough for the drawing to
    /// have two kinds in it.
    /// </remarks>
    private static class PlaceMapsCells
    {
        /// <summary>A region's square: the first band of its own height map, drawn as low ground.</summary>
        internal const string LowGround = "01";

        /// <summary>An interior's square with none of the level's own outlines through it.</summary>
        internal const string Floor = "00";

        /// <summary>An interior's square with one of the level's own outlines through it.</summary>
        internal const string Outline = "01";
    }
}
