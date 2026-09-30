using PartyRpg.Kit.Content;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Movement;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.World;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// What the world reports, and refuses, through its own entries: a walk-in crossing, a use, a boarding a counter
/// sold, and an arrival its destination will not admit — each asserted by the code it is reported under.
/// </summary>
/// <remarks>
/// Each case reaches its code through the entry a session calls — <see cref="SessionWorld.Step"/>,
/// <see cref="SessionWorld.Interact"/>, <see cref="SessionWorld.Board"/>, and <see cref="SessionWorld.Travel"/> —
/// over content written inline in the shape the importer writes, with this suite's own rules behind every seam.
/// </remarks>
public sealed class SessionWorldDiagnosticsTests
{
    private static readonly ContentLayout Layout = new("packs", "imports", "bundles");
    private static readonly PlaceId Home = new("1");
    private static readonly PlaceId Cave = new("2");
    private static readonly FacingRule Facing = new(unitsPerTurn: 2048, minimumPitch: -512, maximumPitch: 512);

    [Fact]
    public void Walking_into_an_entrance_reports_the_crossing_as_entrance_entered()
    {
        RecordingDiagnosticsService diagnostics = new();
        ContentCatalog catalog = Catalog(
            links:
            [
                """{ "id": "edge", "fromPlace": "1", "toPlace": "2", "entryPoint": "Party Start" }""",
            ],
            entrances:
            [
                """{ "id": "in-cave", "link": "edge", "fromPlace": "1", "kind": "entrance", "x": 200, "y": 0, "z": 0, "radius": 60 }""",
            ]);
        PlaceGraph graph = PlaceGraphLoader.Load(catalog);
        PartyPoseOwner pose = new(new PartyPose(Home, PlacePose.Origin), Facing);
        using SessionWorld world = new(
            graph,
            pose,
            new PlaceStateLedger(graph, PlaceRespawnRule.FromContent()),
            new FreeTravel(),
            mover: RecordingMover.Scripted(pose, [(100, 0, 0)]),
            diagnostics: diagnostics,
            entrances: PlaceEntranceLoader.Load(catalog, graph));

        // The first step ends short of the reach and reports nothing; the second walks into it and crosses.
        world.Step(MovementIntent.Still, 1);
        Assert.DoesNotContain(diagnostics.Published, report => report.Code == "entrance-entered");
        world.Step(MovementIntent.Still, 1);

        Assert.Equal(Cave, world.Place);
        DiagnosticsPublishRequest entered = Assert.Single(diagnostics.Published, report => report.Code == "entrance-entered");
        Assert.Equal("travel", entered.Source);
        Assert.Equal(DiagnosticsDisposition.Accepted, entered.Disposition);
    }

    [Fact]
    public void A_use_that_applies_reports_interaction_used_and_one_the_target_refuses_reports_interaction_refused()
    {
        RecordingDiagnosticsService diagnostics = new();
        ContentCatalog catalog = Catalog(
            links: [],
            placements: """{ "id": "door-0", "kind": "door", "x": 100, "y": 0, "z": 0 }""");
        PlaceGraph graph = PlaceGraphLoader.Load(catalog);
        using PartyEntity party = TestParty.OfFour();
        using SessionWorld world = new(
            graph,
            new PartyPoseOwner(new PartyPose(Home, PlacePose.Origin), Facing),
            new PlaceStateLedger(graph, PlaceRespawnRule.FromContent()),
            new FreeTravel(),
            diagnostics: diagnostics,
            partyEntity: party,
            interaction: new InteractionPolicy(
                new DoorRule(),
                PlaceSpace.HeightIsThird(Facing, Math.PI / 2),
                new InteractionTuning(0.20, 0.31)));

        // The closed door the party faces opens, and the world reports the use.
        InteractionResult opened = world.Interact(use: true)!;
        Assert.True(opened.IsApplied);
        Assert.Equal("interaction-used", Assert.Single(diagnostics.Published).Code);

        // The same door, now standing open, refuses a second opening, and the refusal is reported too.
        InteractionResult again = world.Interact(use: true)!;
        Assert.False(again.IsApplied);
        Assert.Equal("door-already-open", again.Code);
        Assert.Equal(["interaction-used", "interaction-refused"], diagnostics.Published.Select(report => report.Code));
    }

    [Fact]
    public void A_boarding_the_cost_rule_refuses_reports_fare_refused_and_leaves_the_party_where_it_stood()
    {
        RecordingDiagnosticsService diagnostics = new();
        using PartyEntity party = TestParty.OfFour();
        PlaceGraph graph = PlaceGraphLoader.Load(
            Catalog(links: ["""{ "id": "coach", "fromPlace": "1", "toPlace": "2", "entryPoint": "Party Start", "fare": true, "route": "coach" }"""]),
            new Routes());
        using SessionWorld world = FareWorld(graph, party, new FaresRefusedTravel(), diagnostics);
        party.Passages.Hold(Cave, 2);

        TransitionResult refused = world.Board(Cave);

        Assert.False(refused.Arrived);
        Assert.Equal(FaresRefusedTravel.RefusalCode, refused.Refusal!.Code);
        Assert.Equal(Home, world.Place);
        DiagnosticsPublishRequest reported = Assert.Single(diagnostics.Published);
        Assert.Equal("fare-refused", reported.Code);
        Assert.Equal("travel", reported.Source);
    }

    [Fact]
    public void Two_counters_journeys_of_the_length_the_ticket_names_are_refused_as_travel_fare_ambiguous()
    {
        using PartyEntity party = TestParty.OfFour();
        PlaceGraph graph = PlaceGraphLoader.Load(
            Catalog(links:
            [
                """{ "id": "coach", "fromPlace": "1", "toPlace": "2", "entryPoint": "Party Start", "fare": true, "route": "coach" }""",
                """{ "id": "caravan", "fromPlace": "1", "toPlace": "2", "entryPoint": "Party Start", "fare": true, "route": "caravan" }""",
            ]),
            new Routes());
        using SessionWorld world = FareWorld(graph, party, new FreeTravel(), diagnostics: null);

        // Both counters' journeys reach the cave in the two days the passage names, so which was bought cannot be
        // told from the ticket: the world refuses by name rather than choosing one.
        party.Passages.Hold(Cave, 2);
        TransitionResult refused = world.Board(Cave);

        Assert.False(refused.Arrived);
        Assert.Equal(TravelCodes.TravelFareAmbiguous, refused.Refusal!.Code);
        Assert.Equal(Home, world.Place);
    }

    [Fact]
    public void An_arrival_the_destination_will_not_admit_is_refused_as_place_refused_arrival()
    {
        PlaceGraph graph = PlaceGraphLoader.Load(
            Catalog(links: ["""{ "id": "edge", "fromPlace": "1", "toPlace": "2", "entryPoint": "Party Start" }"""]));

        // The cave's own rule admits no pose inside it, so the arrival the transition resolves is refused there.
        PartyPoseOwner pose = new(
            new PartyPose(Home, PlacePose.Origin),
            Facing,
            (PlaceId place, PlacePose at, out PlacePose admitted) =>
            {
                admitted = at;
                return place != Cave;
            });
        using SessionWorld world = new(
            graph,
            pose,
            new PlaceStateLedger(graph, PlaceRespawnRule.FromContent()),
            new FreeTravel());

        TransitionResult refused = world.Travel(Assert.Single(graph.TransitionsFrom(Home)), TransitionKind.Entrance);

        Assert.False(refused.Arrived);
        Assert.Equal(TravelCodes.PlaceRefusedArrival, refused.Refusal!.Code);
        Assert.Equal(Home, world.Place);
        Assert.False(world.Places.StateOf(Cave).Visited);
    }

    /// <summary>A world over the fare graph, holding the party whose passage is boarded.</summary>
    private static SessionWorld FareWorld(PlaceGraph graph, PartyEntity party, ITravelCostRule rule, IDiagnosticsService? diagnostics) =>
        new(
            graph,
            new PartyPoseOwner(new PartyPose(Home, PlacePose.Origin), Facing),
            new PlaceStateLedger(graph, PlaceRespawnRule.FromContent()),
            rule,
            diagnostics: diagnostics,
            partyEntity: party);

    /// <summary>Two places — a region and a cave — joined by the links and entrances a case states.</summary>
    private static ContentCatalog Catalog(string[] links, string[]? entrances = null, string placements = "")
    {
        List<(string DocumentId, string DefinitionKind)> documents = [TestPacks.Places, TestPacks.Links];
        if (entrances is not null) documents.Add(("entrances", "place-entrance"));
        InMemoryContentSource source = new InMemoryContentSource()
            .Add("packs/world/pack.json", TestPacks.Manifest("world", [.. documents]))
            .Add("packs/world/places.json", TestPacks.Document("places", "place",
                $$"""{ "id": "1", "kind": "region", "name": "Home", "respawnDays": 7, "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ], "placements": [ {{placements}} ] }""",
                """{ "id": "2", "kind": "interior", "name": "Cave", "respawnDays": 7, "entryPoints": [ { "id": "Party Start", "x": 5, "y": 6, "z": 7, "yaw": 0 } ] }"""))
            .Add("packs/world/links.json", TestPacks.Document("links", "travel-link", links));
        if (entrances is not null) source.Add("packs/world/entrances.json", TestPacks.Document("entrances", "place-entrance", entrances));
        return ContentCatalogLoader.Load(source, Layout).RequireValid();
    }

    /// <summary>This suite's fare rule: every route it names takes two days.</summary>
    private sealed class Routes : IFareDurationRule
    {
        public int? DaysOf(PlaceId? from, PlaceId to, string route) => route is "coach" or "caravan" ? 2 : null;
    }

    /// <summary>A door that opens once and refuses to open again while it stands open.</summary>
    private sealed class DoorRule : IInteractionRule
    {
        public InteractionTargetDefinition? Describe(InteractionTargetRequest request) =>
            string.Equals(request.Placement.Content.Kind, "door", StringComparison.Ordinal)
                ? new InteractionTargetDefinition(
                    new InteractionTargetKind("door"),
                    "A door",
                    InteractionVerb.Open,
                    reach: 512,
                    state: request.State.Length > 0 ? request.State : "closed")
                : null;

        public InteractionTrap? Trap(InteractionTargetDefinition target, InteractionContext context) => null;

        public Verdict Judge(InteractionRequirement requirement, InteractionContext context) => Verdict.Met;

        public InteractionOutcome Apply(InteractionTargetDefinition target, InteractionContext context) =>
            string.Equals(target.State, "open", StringComparison.Ordinal)
                ? InteractionOutcome.Refused(new Refusal("door-already-open", "It already stands open."))
                : InteractionOutcome.Applied("open", "It swings open.");
    }
}
