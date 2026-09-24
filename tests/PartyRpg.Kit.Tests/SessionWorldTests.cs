using PartyRpg.Kit.Content;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.World;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>The live world inside a session: travel, arrival, per-place state, and what it publishes.</summary>
public sealed class SessionWorldTests
{
    private static readonly ContentLayout Layout = new("packs", "imports", "bundles");
    private const double StepSeconds = 1.0 / 60.0;

    [Fact]
    public void Travelling_moves_the_party_marks_the_place_visited_and_publishes_it()
    {
        using RecordingUiProjectionChannel channel = new();
        FakeTimeSource time = new();
        SessionWorld world = World(time);
        using PartyRpgSession session = new(new SessionComposition(new RulesetId("test.ruleset"), "Test"), channel, world);

        // Before anything moves, the session still publishes the world it holds.
        Assert.Equal("1", channel.Latest().Field("world").Field("place").AsString());

        // The start is a scripted arrival through the same path as any other transition.
        world.ArriveAt(new PlaceId("1"), world.Graph.ResolveArrival(new PlaceTransition(null, new PlaceId("1"), PlaceArrival.AtEntryPoint("Party Start"), "start")));

        ProjectedNode published = channel.Latest().Field("world");
        Assert.Equal("1", published.Field("place").AsString());
        Assert.Equal("Home", published.Field("name").AsString());
        Assert.Equal("region", published.Field("kind").AsString());
        Assert.Equal(1.0, published.Field("visited").AsNumber());
        Assert.Equal(2.0, published.Field("places").AsNumber());

        // Walking out and back uses one path in both directions.
        PlaceTransition outbound = Assert.Single(world.Graph.TransitionsFrom(new PlaceId("1")));
        TransitionResult arrived = world.Travel(outbound, TransitionKind.Walking);
        Assert.True(arrived.Arrived);
        Assert.Equal(new PlaceId("2"), world.Place);
        Assert.True(world.Places.StateOf(new PlaceId("2")).Visited);
        session.PublishWorld();
        Assert.Equal("2", channel.Latest().Field("world").Field("place").AsString());
        Assert.Equal("interior", channel.Latest().Field("world").Field("kind").AsString());

        PlaceTransition back = Assert.Single(world.Graph.TransitionsFrom(new PlaceId("2")));
        Assert.True(world.Travel(back, TransitionKind.Entrance).Arrived);
        Assert.Equal(new PlaceId("1"), world.Place);
    }

    [Fact]
    public void A_refused_travel_leaves_the_party_exactly_where_it_was()
    {
        FakeTimeSource time = new();
        SessionWorld world = World(time);
        world.ArriveAt(new PlaceId("1"), Pose().Pose);
        PlacePose before = world.Party.PlacePose;

        PlaceTransition outbound = Assert.Single(world.Graph.TransitionsFrom(new PlaceId("1")));
        TransitionResult refused = world.Travel(outbound, TransitionKind.PaidService);

        Assert.False(refused.Arrived);
        Assert.Equal("test-unaffordable", refused.Refusal?.Code);
        Assert.Equal(new PlaceId("1"), world.Place);
        Assert.Equal(before, world.Party.PlacePose);
    }

    [Fact]
    public void Respawn_fires_from_the_worlds_day_source_and_never_erases_knowledge()
    {
        using RecordingUiProjectionChannel channel = new();
        FakeTimeSource time = new();
        SessionWorld world = World(time);
        world.ArriveAt(new PlaceId("1"), Pose().Pose);
        using PartyRpgSession session = new(new SessionComposition(new RulesetId("test.ruleset"), "Test"), channel, world);

        world.Places.MarkCleared(new PlaceId("1"));

        // Ten updates inside the same day change nothing.
        for (ulong step = 1; step <= 10; step++) session.Update(Update(step, 1));
        Assert.Equal(0, world.Places.ElapsedGameDays);

        // The next day is what the source reports, and the place's population comes back.
        time.ElapsedGameDays = 7;
        session.Update(Update(20, 1));

        PlaceState state = world.Places.StateOf(new PlaceId("1"));
        Assert.Equal(7, world.Places.ElapsedGameDays);
        Assert.False(state.Cleared);
        Assert.Equal(1, state.RespawnCount);
        Assert.True(state.Visited);
        Assert.True(state.Discovered);
    }

    private static PartyPose Pose() => new(new PlaceId("1"), new PlacePose(10, 20, 0, 512, 0));

    private static SessionWorld World(FakeTimeSource time)
    {
        ContentCatalog catalog = ContentCatalogLoader.Load(
            new InMemoryContentSource()
                .Add("packs/world/pack.json", Manifest())
                .Add("packs/world/places.json", Document("places", "place",
                    """{ "id": "1", "kind": "region", "name": "Home", "respawnDays": 7, "entryPoints": [ { "id": "Party Start", "x": 10, "y": 20, "z": 0, "yaw": 512 } ] }""",
                    """{ "id": "2", "kind": "interior", "name": "Cave", "respawnDays": 7, "entryPoints": [ { "id": "Party Start", "x": 1, "y": 2, "z": 3, "yaw": 0 } ] }"""))
                .Add("packs/world/links.json", Document("links", "travel-link",
                    """{ "id": "0", "fromPlace": "1", "toPlace": "2", "entryPoint": "Party Start" }""",
                    """{ "id": "1", "fromPlace": "2", "toPlace": "1", "x": 10, "y": 20, "z": 0, "yaw": 512 }""")),
            Layout).RequireValid();

        PlaceGraph graph = PlaceGraphLoader.Load(catalog);
        return new SessionWorld(
            graph,
            new PartyPoseOwner(Pose(), new FacingRule(unitsPerTurn: 2048, minimumPitch: -512, maximumPitch: 512)),
            new PlaceStateLedger(graph, PlaceRespawnRule.FromContent()),
            new TestCostRule(),
            time);
    }

    private static ProductUpdate Update(ulong step, uint admitted)
    {
        ProductUpdateFacts facts = new(
            ProductUpdateMode.Realtime,
            ProductLifecycleState.Running,
            1,
            1,
            0,
            step,
            60,
            admitted,
            0,
            StepSeconds);
        return new ProductUpdate(facts, ReadOnlySpan<ProductInputEvent>.Empty);
    }

    private static string Manifest() =>
        """
        {
          "schemaVersion": 1,
          "packId": "world",
          "kind": "definitions",
          "provenance": { "description": "test content" },
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

    /// <summary>A day source the test moves by hand, standing in for the clock a later stone wires.</summary>
    private sealed class FakeTimeSource : IWorldTimeSource
    {
        public int ElapsedGameDays { get; set; }
    }

    /// <summary>Walking is free; anything paid is refused by name, which is what a purse-less party sees.</summary>
    private sealed class TestCostRule : ITravelCostRule
    {
        public TravelCostQuote Quote(TransitionRequest request) =>
            request.Kind is TransitionKind.PaidService
                ? TravelCostQuote.Refused(new TravelRefusal("test-unaffordable", "the party has no purse yet"))
                : TravelCostQuote.Payable(TravelCost.Free);
    }
}
