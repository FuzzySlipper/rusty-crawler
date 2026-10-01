using PartyRpg.Kit.Content;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Movement;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.World;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// A use that leads somewhere: a face clicked to enter a cave, a plate walked onto, a pad that sets the party down
/// elsewhere in its place — each judged by the one use workflow and travelled through the one transition path.
/// </summary>
public sealed class UseTravelTests
{
    private static readonly ContentLayout Layout = new("packs", "imports", "bundles");
    private static readonly PlaceId Home = new("1");
    private static readonly PlaceId Cave = new("2");

    [Fact]
    public void Using_a_face_that_leads_into_a_cave_takes_its_transition_through_the_one_path_after_recording_the_use()
    {
        RecordingRule cost = new();
        using SessionWorld world = World(cost, walk: null, out _);

        // The party faces the shrine standing a hundred units ahead along its second axis.
        InteractionResult? used = world.Interact(use: true);

        Assert.NotNull(used);
        Assert.True(used.IsApplied, used.Message);
        Assert.Equal(Cave, world.Place);
        Assert.Equal(new PlacePose(5, 6, 7, 0, 0), world.Party.PlacePose);
        Assert.True(world.Places.StateOf(Cave).Visited);

        // The journey was quoted once, as the kind the use stated, from where the party stood.
        TransitionRequest asked = Assert.Single(cost.Asked);
        Assert.Equal(TransitionKind.Entrance, asked.Kind);
        Assert.Equal(Home, asked.PartyPlace);

        // The use was recorded where it was made, and the result says where the party arrived.
        Assert.Equal("used", ((IInteractionWorld)world).States.StateOf(Home, new PlacementContentId("shrine", "shrine-0")).State);
        Assert.True(used.Journey?.Arrived);
        Assert.Contains("The party arrives in Cave.", used.Message, StringComparison.Ordinal);
        Assert.Same(used, world.LastInteraction);
    }

    [Fact]
    public void A_journey_the_cost_rule_refuses_leaves_the_party_where_it_stood_and_says_why_as_residue()
    {
        RecordingRule cost = new() { Refuse = TransitionKind.Entrance };
        using SessionWorld world = World(cost, walk: null, out _);

        InteractionResult? used = world.Interact(use: true);

        Assert.NotNull(used);
        Assert.True(used.IsApplied);
        Assert.Equal(Home, world.Place);
        Assert.False(used.Journey?.Arrived);
        Assert.Contains("The way on was refused: the test refused this journey", used.Residue, StringComparison.Ordinal);
    }

    [Fact]
    public void Walking_onto_a_plate_raises_its_use_and_the_journey_it_leads_to_is_taken()
    {
        RecordingRule cost = new();
        using SessionWorld world = World(cost, walk: [(100, 0, 0), (100, 0, 0)], out RecordingDiagnosticsService diagnostics);

        // The first step ends outside the plate's reach and the second inside it: treading on the plate is the use.
        world.Step(MovementIntent.Still, 1);
        Assert.Equal(Home, world.Place);
        world.Step(MovementIntent.Still, 1);

        Assert.Equal(Cave, world.Place);
        Assert.Equal(TransitionKind.Walking, Assert.Single(cost.Asked).Kind);
        Assert.Equal("trodden", ((IInteractionWorld)world).States.StateOf(Home, new PlacementContentId("plate", "plate-0")).State);
        Assert.Contains(diagnostics.Published, report => report.Code == "use-travelled");
    }

    [Fact]
    public void A_plate_is_never_offered_to_the_reticle()
    {
        using SessionWorld world = World(new RecordingRule(), walk: null, out _, pose: new PlacePose(0, 0, 0, 512, 0));

        // Facing the plate along the first axis holds nothing: what the party treads on is not aimed at.
        world.Interaction!.Update();
        Assert.Null(world.Interaction.FocusedTarget);
    }

    [Fact]
    public void A_pad_sets_the_party_down_elsewhere_in_its_own_place_without_crossing_anything()
    {
        RecordingRule cost = new();
        using SessionWorld world = World(cost, walk: [(-100, 0, 0), (-100, 0, 0)], out _);

        world.Step(MovementIntent.Still, 1);
        world.Step(MovementIntent.Still, 1);

        Assert.Equal(Home, world.Place);
        Assert.Equal(new PlacePose(900, 900, 0, 0, 0), world.Party.PlacePose);
        Assert.Empty(cost.Asked);
    }

    [Fact]
    public void An_entrance_that_raises_a_placement_its_place_does_not_hold_is_refused_when_the_world_is_built()
    {
        ArgumentException refused = Assert.Throws<ArgumentException>(() => World(new RecordingRule(), walk: null, out _, raises: "missing-0"));
        Assert.Contains("does not hold", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_entrance_naming_both_a_transition_and_a_placement_is_a_content_defect()
    {
        ContentCatalog catalog = Catalog(
            """{ "id": "both", "link": "edge", "raises": "plate-0", "raisesKind": "plate", "fromPlace": "1", "kind": "entrance", "x": 0, "y": 0, "z": 0, "radius": 10 }""");
        PlaceGraph graph = PlaceGraphLoader.Load(catalog);

        ContentValidationException refused = Assert.Throws<ContentValidationException>(() => PlaceEntranceLoader.Load(catalog, graph));
        Assert.Contains(refused.Issues, issue => issue.Code == "entrance-form-ambiguous");
    }

    private static SessionWorld World(
        RecordingRule cost,
        IReadOnlyList<(double X, double Y, double Z)>? walk,
        out RecordingDiagnosticsService diagnostics,
        PlacePose? pose = null,
        string raises = "plate-0")
    {
        ContentCatalog catalog = Catalog(
            $$"""{ "id": "onto-plate", "raises": "{{raises}}", "raisesKind": "plate", "fromPlace": "1", "x": 200, "y": 0, "z": 0, "radius": 60 }""",
            """{ "id": "onto-pad", "raises": "pad-0", "raisesKind": "pad", "fromPlace": "1", "x": -200, "y": 0, "z": 0, "radius": 60 }""");
        PlaceGraph graph = PlaceGraphLoader.Load(catalog);
        FacingRule facing = new(unitsPerTurn: 2048, minimumPitch: -512, maximumPitch: 512);
        PartyPoseOwner party = new(new PartyPose(Home, pose ?? new PlacePose(0, 0, 0, 0, 0)), facing);
        diagnostics = new RecordingDiagnosticsService();
        return new SessionWorld(
            graph,
            party,
            new PlaceStateLedger(graph, PlaceRespawnRule.FromContent()),
            cost,
            mover: walk is null ? null : RecordingMover.Scripted(party, walk),
            diagnostics: diagnostics,
            entrances: PlaceEntranceLoader.Load(catalog, graph),
            interaction: new InteractionPolicy(
                new TravelRule(),
                PlaceSpace.HeightIsThird(facing, radiansAtZeroFacing: 0),
                new InteractionTuning(acquisitionAngleRadians: 0.20, releaseAngleRadians: 0.31)));
    }

    private static ContentCatalog Catalog(params string[] entrances) =>
        ContentCatalogLoader.Load(
            new InMemoryContentSource()
                .Add("packs/world/pack.json", TestPacks.Manifest("world", TestPacks.Places, TestPacks.Links, ("entrances", "place-entrance")))
                .Add("packs/world/places.json", TestPacks.Document("places", "place",
                    """
                    { "id": "1", "kind": "region", "name": "Home", "respawnDays": 7,
                      "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
                      "placements": [
                        { "id": "shrine-0", "kind": "shrine", "x": 0, "y": 100, "z": 0 },
                        { "id": "plate-0", "kind": "plate", "x": 200, "y": 0, "z": 0 },
                        { "id": "pad-0", "kind": "pad", "x": -200, "y": 0, "z": 0 } ] }
                    """,
                    """{ "id": "2", "kind": "interior", "name": "Cave", "respawnDays": 7, "entryPoints": [ { "id": "Party Start", "x": 5, "y": 6, "z": 7, "yaw": 0 } ] }"""))
                .Add("packs/world/links.json", TestPacks.Document("links", "travel-link",
                    """{ "id": "edge", "fromPlace": "1", "toPlace": "2", "entryPoint": "Party Start" }""",
                    """{ "id": "back", "fromPlace": "2", "toPlace": "1", "x": 10, "y": 20, "z": 0, "yaw": 0 }"""))
                .Add("packs/world/entrances.json", TestPacks.Document("entrances", "place-entrance", entrances)),
            Layout).RequireValid();

    /// <summary>
    /// A game's answer: the shrine is used and leads into the cave, the plate is trodden on and leads there as a
    /// walk, and the pad sets the party down elsewhere in its own place.
    /// </summary>
    private sealed class TravelRule : IInteractionRule
    {
        public InteractionTargetDefinition? Describe(InteractionTargetRequest request) => request.Placement.Content.Kind switch
        {
            "shrine" => new(new InteractionTargetKind("shrine"), "A shrine", InteractionVerb.Pull, reach: 512, request.State),
            "plate" => new(new InteractionTargetKind("plate"), "A plate", InteractionVerb.Tread, reach: 512, request.State),
            "pad" => new(new InteractionTargetKind("pad"), "A pad", InteractionVerb.Tread, reach: 512, request.State),
            _ => null,
        };

        public InteractionTrap? Trap(InteractionTargetDefinition target, InteractionContext context) => null;

        public Verdict Judge(InteractionRequirement requirement, InteractionContext context) => Verdict.Met;

        public InteractionOutcome Apply(InteractionTargetDefinition target, InteractionContext context) => target.Kind.Value switch
        {
            "shrine" => InteractionOutcome.Applied(
                "used",
                "The shrine hums.",
                travels: new InteractionTravel(context.PlaceTransitions.Single(transition => transition.Source == "edge"), TransitionKind.Entrance)),
            "plate" => InteractionOutcome.Applied(
                "trodden",
                "The plate gives way.",
                travels: new InteractionTravel(context.PlaceTransitions.Single(transition => transition.Source == "edge"), TransitionKind.Walking)),
            _ => InteractionOutcome.Applied("trodden", "The pad glows.", relocates: new InteractionRelocation(900, 900, 0, null)),
        };
    }

    /// <summary>The cost contract as the test configures it: it records every question and can refuse one kind.</summary>
    private sealed class RecordingRule : ITravelCostRule
    {
        internal List<TransitionRequest> Asked { get; } = [];

        internal TransitionKind? Refuse { get; set; }

        public TravelCostQuote Quote(TransitionRequest request)
        {
            Asked.Add(request);
            return Refuse == request.Kind
                ? TravelCostQuote.Refused(new Refusal("test-refused", "the test refused this journey"))
                : TravelCostQuote.Payable(TravelCost.Free);
        }
    }
}
