using PartyRpg.Kit.Content;
using PartyRpg.Kit.World;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// The one transition path: the departure check, the cost rule it always asks, and the arrival it resolves
/// — exercised over a loaded graph, in both directions and for every kind of travel.
/// </summary>
public sealed class TransitionExecutionTests
{
    private static readonly ContentLayout Layout = new("packs", "imports", "bundles");

    /// <summary>What the test rule quotes per kind, so a test can tell which answer a transition charged.</summary>
    private static readonly Dictionary<TransitionKind, TravelCost> Costs = new()
    {
        [TransitionKind.Walking] = new TravelCost(new TravelTime(3, TravelTimeUnit.Days), new Provisions(3, ProvisionUnit.Portions)),
        [TransitionKind.Entrance] = new TravelCost(new TravelTime(10, TravelTimeUnit.Minutes), new Provisions(1, ProvisionUnit.Portions)),
        [TransitionKind.PaidService] = new TravelCost(new TravelTime(6, TravelTimeUnit.Hours), new Provisions(2, ProvisionUnit.Portions)),
        [TransitionKind.Portal] = TravelCost.Free,
        [TransitionKind.Scripted] = new TravelCost(new TravelTime(1, TravelTimeUnit.Minutes), Provisions.None),
    };

    [Fact]
    public void Both_directions_between_two_places_take_the_same_path_and_charge_its_cost()
    {
        PlaceGraph graph = Graph();
        PlaceId home = new("1");
        PlaceId field = new("2");
        RecordingRule rule = new(CostFor);
        TransitionExecutive executive = new(rule);

        // Out: this transition names an arrival point, so the destination place decides where the party stands.
        TransitionResult outbound = executive.Take(new TransitionRequest(
            graph,
            TransitionOf(graph, home, "edge"),
            TransitionKind.Walking,
            home,
            new PlacePose(10, 20, 30, 512, 0)));

        Assert.True(outbound.Arrived);
        Assert.Equal(field, outbound.Place);
        Assert.Equal(new PlacePose(1, 2, 0, 0, 0), outbound.Pose);

        // Back: the same call, the other way, over a transition that carries its own pose.
        TransitionResult inbound = executive.Take(new TransitionRequest(
            graph,
            TransitionOf(graph, field, "field-home"),
            TransitionKind.Walking,
            outbound.Place,
            outbound.Pose));

        Assert.True(inbound.Arrived);
        Assert.Equal(home, inbound.Place);
        Assert.Equal(new PlacePose(100, 200, 0, 1024, 0), inbound.Pose);

        // One quote per leg: neither leg is a teleport, and each charged what the rule answered.
        Assert.Equal(2, rule.Asked.Count);
        Assert.Equal(Costs[TransitionKind.Walking], outbound.ChargedCost);
        Assert.Equal(Costs[TransitionKind.Walking], inbound.ChargedCost);
    }

    [Fact]
    public void Arrival_reads_a_named_point_from_the_destination_and_a_carried_pose_as_it_stands()
    {
        PlaceGraph graph = Graph();
        PlaceId home = new("1");
        PlacePose standing = new(9, 9, 0, 0, 0);
        TransitionExecutive executive = new(new RecordingRule(CostFor));

        // A named arrival resolves against the destination place, so the place stays the owner of its own point.
        TransitionResult named = executive.Take(new TransitionRequest(
            graph, TransitionOf(graph, home, "door-in"), TransitionKind.Entrance, home, standing));

        Assert.Equal(new PlaceId("3"), named.Place);
        Assert.Equal(new PlacePose(5, 6, 7, 64, 0), named.Pose);

        // A carried pose is used exactly as the transition states it, destination entry points or not.
        TransitionResult carried = executive.Take(new TransitionRequest(
            graph, TransitionOf(graph, home, "portal"), TransitionKind.Portal, home, standing));

        Assert.Equal(new PlaceId("3"), carried.Place);
        Assert.Equal(new PlacePose(42, 43, 44, 45, 46), carried.Pose);
    }

    [Theory]
    [InlineData(TransitionKind.Walking, "1", "edge")]
    [InlineData(TransitionKind.Entrance, "1", "door-in")]
    [InlineData(TransitionKind.PaidService, "1", "coach")]
    [InlineData(TransitionKind.Portal, "1", "portal")]
    [InlineData(TransitionKind.Scripted, "2", "start")]
    public void Every_kind_of_travel_asks_the_cost_rule(TransitionKind kind, string partyPlace, string source)
    {
        PlaceGraph graph = Graph();
        PlaceId place = new(partyPlace);
        PlaceTransition transition = kind == TransitionKind.Scripted
            ? graph.WorldIssued.Single(candidate => candidate.Source == source)
            : TransitionOf(graph, place, source);
        RecordingRule rule = new(CostFor);
        TransitionExecutive executive = new(rule);

        TransitionResult result = executive.Take(new TransitionRequest(
            graph, transition, kind, place, new PlacePose(4, 4, 0, 0, 0)));

        Assert.True(result.Arrived);
        Assert.Equal(transition.To, result.Place);
        Assert.Equal(Costs[kind], result.ChargedCost);

        // The recording rule proves the path consulted the contract, and was asked about this transition.
        TransitionRequest asked = Assert.Single(rule.Asked);
        Assert.Equal(kind, asked.Kind);
        Assert.Same(transition, asked.Transition);
    }

    [Fact]
    public void A_transition_the_party_cannot_pay_for_is_refused_by_name_and_moves_nobody()
    {
        PlaceGraph graph = Graph();
        PlaceId home = new("1");
        PlacePose standing = new(9, 9, 0, 0, 0);
        RecordingRule rule = new(_ => TravelCostQuote.Refused(
            new TravelRefusal("travel-cost-unpayable", "The fare is 50 and the party holds 12.")));
        TransitionExecutive executive = new(rule);

        TransitionResult result = executive.Take(new TransitionRequest(
            graph, TransitionOf(graph, home, "coach"), TransitionKind.PaidService, home, standing));

        Assert.False(result.Arrived);
        Assert.Equal("travel-cost-unpayable", result.Refusal!.Code);
        Assert.Contains("50", result.Refusal.Message);
        Assert.Equal(home, result.Place);
        Assert.Equal(standing, result.Pose);
        Assert.True(result.ChargedCost.IsFree);
        Assert.Single(rule.Asked);
    }

    [Fact]
    public void A_cost_rule_that_answers_nothing_stops_the_transition_instead_of_making_it_free()
    {
        PlaceGraph graph = Graph();
        PlaceId home = new("1");
        RecordingRule rule = new(_ => null!);
        TransitionExecutive executive = new(rule);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => executive.Take(
            new TransitionRequest(graph, TransitionOf(graph, home, "edge"), TransitionKind.Walking, home, PlacePose.Origin)));

        Assert.Contains("answered nothing", error.Message);
        Assert.Single(rule.Asked);
    }

    [Fact]
    public void A_transition_the_world_does_not_issue_cannot_be_taken_except_as_magical_travel()
    {
        PlaceGraph graph = Graph();
        PlaceId home = new("1");
        PlaceTransition invented = new(home, new PlaceId("3"), PlaceArrival.AtPose(new PlacePose(1, 1, 0, 0, 0)), "invented");
        RecordingRule rule = new(CostFor);
        TransitionExecutive executive = new(rule);

        // Walked, bought, or scripted, a transition the graph does not hold is a caller defect: the road the
        // party never walked cannot be taken, and the cost rule is not even asked about it.
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => executive.Take(
            new TransitionRequest(graph, invented, TransitionKind.Walking, home, PlacePose.Origin)));

        Assert.Contains("invented", error.Message);
        Assert.Empty(rule.Asked);

        // A portal is the one kind of travel its caller issues rather than a place: it stands where the party
        // is and reaches a place the world holds, so the graph need not declare the edge. What is still
        // checked is the departure and the destination, which is what keeps magical travel from arriving
        // somewhere the world has never heard of.
        TransitionResult recalled = executive.Take(
            new TransitionRequest(graph, invented, TransitionKind.Portal, home, PlacePose.Origin));

        Assert.True(recalled.Arrived);
        Assert.Equal(new PlaceId("3"), recalled.Place);
        Assert.Single(rule.Asked);

        InvalidOperationException elsewhere = Assert.Throws<InvalidOperationException>(() => executive.Take(
            new TransitionRequest(graph, invented, TransitionKind.Portal, new PlaceId("2"), PlacePose.Origin)));
        Assert.Contains("leaves place", elsewhere.Message);

        ContentValidationException unknown = Assert.Throws<ContentValidationException>(() => executive.Take(
            new TransitionRequest(
                graph,
                new PlaceTransition(home, new PlaceId("99"), PlaceArrival.AtPose(PlacePose.Origin), "invented elsewhere"),
                TransitionKind.Portal,
                home,
                PlacePose.Origin)));
        Assert.Contains("99", unknown.Message);
    }

    [Fact]
    public void A_transition_that_leaves_another_place_cannot_be_taken()
    {
        PlaceGraph graph = Graph();
        PlaceId home = new("1");
        PlaceId field = new("2");
        RecordingRule rule = new(CostFor);
        TransitionExecutive executive = new(rule);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => executive.Take(
            new TransitionRequest(graph, TransitionOf(graph, field, "field-home"), TransitionKind.Walking, home, PlacePose.Origin)));

        Assert.Contains("'1'", error.Message);
        Assert.Contains("'2'", error.Message);
        Assert.Empty(rule.Asked);
    }

    [Fact]
    public void A_transition_a_place_issues_cannot_be_taken_as_scripted_travel()
    {
        PlaceGraph graph = Graph();
        PlaceId home = new("1");
        RecordingRule rule = new(CostFor);
        TransitionExecutive executive = new(rule);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => executive.Take(
            new TransitionRequest(graph, TransitionOf(graph, home, "edge"), TransitionKind.Scripted, home, PlacePose.Origin)));

        Assert.Contains("scripted", error.Message);
        Assert.Empty(rule.Asked);
    }

    [Fact]
    public void The_transition_path_cannot_be_built_without_a_cost_rule()
    {
        Assert.Throws<ArgumentNullException>(() => new TransitionExecutive(null!));
    }

    [Fact]
    public void A_cost_that_would_give_time_or_food_back_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            _ = new TravelTime(-1, TravelTimeUnit.Days);
        });
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            _ = new Provisions(-1, ProvisionUnit.Portions);
        });
    }

    private static TravelCostQuote CostFor(TransitionRequest request) => TravelCostQuote.Payable(Costs[request.Kind]);

    private static PlaceTransition TransitionOf(PlaceGraph graph, PlaceId from, string source) =>
        graph.TransitionsFrom(from).Single(transition => transition.Source == source);

    private static PlaceGraph Graph() =>
        PlaceGraphLoader.Load(ContentCatalogLoader.Load(
            new InMemoryContentSource()
                .Add("packs/world/pack.json", Manifest())
                .Add("packs/world/places.json", Document("places", "place", Places(
                    """{ "id": "1", "kind": "region", "name": "Home", "entryPoints": [ { "id": "Party Start", "x": 10, "y": 20, "z": 30, "yaw": 512 } ] }""",
                    """{ "id": "2", "kind": "region", "name": "Field", "entryPoints": [ { "id": "West Gate", "x": 1, "y": 2, "z": 0 } ] }""",
                    """{ "id": "3", "kind": "interior", "name": "Cave", "entryPoints": [ { "id": "Mouth", "x": 5, "y": 6, "z": 7, "yaw": 64 } ] }""")))
                .Add("packs/world/links.json", Document("links", "travel-link", Links(
                    """{ "id": "edge", "fromPlace": "1", "toPlace": "2", "entryPoint": "West Gate" }""",
                    """{ "id": "field-home", "fromPlace": "2", "toPlace": "1", "x": 100, "y": 200, "z": 0, "yaw": 1024, "pitch": 0 }""",
                    """{ "id": "door-in", "fromPlace": "1", "toPlace": "3", "entryPoint": "Mouth" }""",
                    """{ "id": "coach", "fromPlace": "1", "toPlace": "2", "entryPoint": "West Gate" }""",
                    """{ "id": "portal", "fromPlace": "1", "toPlace": "3", "x": 42, "y": 43, "z": 44, "yaw": 45, "pitch": 46 }""",
                    """{ "id": "start", "toPlace": "1", "x": 50, "y": 60, "z": 0 }"""))),
            Layout).RequireValid());

    private static string Places(params string[] entries) => string.Join(",", entries);

    private static string Links(params string[] entries) => string.Join(",", entries);

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

    private static string Document(string documentId, string definitionKind, string entries) =>
        $$"""
        {
          "documentId": "{{documentId}}",
          "definitionKind": "{{definitionKind}}",
          "entries": [ {{entries}} ]
        }
        """;

    /// <summary>A cost rule that answers a fixed quote and records every request it was asked about.</summary>
    private sealed class RecordingRule : ITravelCostRule
    {
        private readonly Func<TransitionRequest, TravelCostQuote> _answer;

        internal RecordingRule(Func<TransitionRequest, TravelCostQuote> answer) => _answer = answer;

        /// <summary>Every request the path asked about, in the order it asked.</summary>
        internal List<TransitionRequest> Asked { get; } = [];

        public TravelCostQuote Quote(TransitionRequest request)
        {
            Asked.Add(request);
            return _answer(request);
        }
    }
}
