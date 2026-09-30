using PartyRpg.Kit.Content;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Services;
using PartyRpg.Kit.World;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// Boarding a passage a counter sold: the journey a fare takes through the one transition path, which
/// journey a ticket names, and everything that must happen instead when it names none.
/// </summary>
/// <remarks>
/// <para>
/// A fare is content's own fact about a crossing — that a counter sells it, and which route it runs on — the
/// game's rule's fact about the route — how many days its journey takes — and the party's own fact about a
/// journey — a passage naming a place and a length. These tests write the content in the shape the importer
/// writes it, so the routing is exercised without the operator's data, and they hold no ruleset: how long a
/// route takes is this suite's own fare rule, what boarding costs is the cost rule's answer, and this suite's
/// cost rule answers only whether it was asked, for what, and how often.
/// </para>
/// <para>
/// Nothing here walks: a fare carries no reach, and the point of the suite is that the way onto a coach is
/// not a way between places grown beside the one every crossing already takes.
/// </para>
/// </remarks>
public sealed class FareBoardingTests
{
    private static readonly ContentLayout Layout = new("packs", "imports", "bundles");

    /// <summary>Home, the region whose counters sell passages.</summary>
    private static readonly PlaceId Home = new("1");

    /// <summary>Town, reached from Home by two counters' journeys and by a road the party can walk.</summary>
    private static readonly PlaceId Town = new("2");

    /// <summary>Port, reached from Home only by boat.</summary>
    private static readonly PlaceId Port = new("3");

    /// <summary>Nowhere, which no counter reaches and no road leads to.</summary>
    private static readonly PlaceId Nowhere = new("4");

    [Fact]
    public void A_passage_is_boarded_through_the_one_path_and_arrives_where_the_ticket_reaches()
    {
        RecordingCostRule rule = new();
        RecordingDiagnosticsService diagnostics = new();
        using PartyEntity party = Party();
        using SessionWorld world = World(party, rule, diagnostics);
        party.Passages.Hold(Town, 2);

        TransitionResult boarded = world.Board(Town);

        // The journey is the counter's own: the transition content states as a two-day fare from here, taken
        // as travel bought from a service rather than as a walk.
        Assert.True(boarded.Arrived);
        Assert.Equal(TransitionKind.PaidService, boarded.Kind);
        Assert.Equal(Town, world.Place);
        TransitionRequest asked = Assert.Single(rule.Asked);
        Assert.Equal(TransitionKind.PaidService, asked.Kind);
        Assert.Equal("coach", asked.Transition.Source);
        Assert.Equal(Home, asked.PartyPlace);

        // The arrival is the destination's own point, and the place the party reached is one it has been to.
        Assert.Equal(new PlacePose(5, 6, 7, 0, 0), world.Party.PlacePose);
        Assert.True(world.Places.StateOf(Town).Visited);

        // A journey bought at a counter leaves no step behind it, so the world says what happened.
        DiagnosticsPublishRequest reported = Assert.Single(diagnostics.Published);
        Assert.Equal("fare-boarded", reported.Code);
        Assert.Contains("Town", reported.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Two_counters_journeys_to_one_place_are_told_apart_by_the_days_the_ticket_names()
    {
        RecordingCostRule rule = new();
        using PartyEntity party = Party();
        using SessionWorld world = World(party, rule);

        // Both counters reach Town from Home and the road does too, so the place alone does not name the
        // journey: the ticket's own length does, and the road the party could have walked is never it.
        party.Passages.Hold(Town, 2);
        Assert.True(world.Board(Town).Arrived);
        Assert.Equal("coach", rule.Asked[^1].Transition.Source);

        // Back to Home, and the other counter's passage is the other journey.
        world.ArriveAt(Home, PlacePose.Origin);
        party.Passages.Hold(Town, 3);
        Assert.True(world.Board(Town).Arrived);
        Assert.Equal("caravan", rule.Asked[^1].Transition.Source);
    }

    [Fact]
    public void A_ticket_whose_days_no_journey_states_is_refused_rather_than_guessed_at()
    {
        RecordingCostRule rule = new();
        using PartyEntity party = Party();
        using SessionWorld world = World(party, rule);
        party.Passages.Hold(Town, 5);

        TransitionResult refused = world.Board(Town);

        // Nothing moved and nothing was asked: a ticket naming a journey content does not state is a defect
        // to report, not a licence to take whichever journey is nearest.
        Assert.False(refused.Arrived);
        Assert.Equal("travel-fare-unstated", refused.Refusal!.Code);
        Assert.Equal(Home, world.Place);
        Assert.Empty(rule.Asked);
    }

    [Fact]
    public void A_journey_no_counter_sells_from_where_the_party_stands_is_refused_by_name()
    {
        RecordingCostRule rule = new();
        using PartyEntity party = Party();
        using SessionWorld world = World(party, rule);
        party.Passages.Hold(Nowhere, 2);

        TransitionResult refused = world.Board(Nowhere);

        Assert.False(refused.Arrived);
        Assert.Equal("travel-fare-unrouted", refused.Refusal!.Code);
        Assert.Contains("Nowhere", refused.Refusal.Message, StringComparison.Ordinal);
        Assert.Equal(Home, world.Place);
        Assert.Empty(rule.Asked);
    }

    [Fact]
    public void A_party_holding_no_passage_is_handed_to_the_cost_rule_which_owns_that_refusal()
    {
        RecordingCostRule rule = new() { Refuse = TransitionKind.PaidService };
        using PartyEntity party = Party();
        using SessionWorld world = World(party, rule);

        // The journey is still asked about: what boarding costs, and why a party with no ticket cannot board,
        // are the cost rule's answers and not this world's, so a world that refused first would be a second
        // owner of the same fact.
        TransitionResult refused = world.Board(Town);

        Assert.False(refused.Arrived);
        Assert.Equal("test-refused", refused.Refusal!.Code);
        Assert.Equal(TransitionKind.PaidService, rule.Asked[^1].Kind);
        Assert.Equal(Home, world.Place);
        Assert.Equal(PlacePose.Origin, world.Party.PlacePose);
    }

    [Fact]
    public void A_crossing_a_counter_sells_states_the_journey_and_a_road_states_none()
    {
        ContentCatalog catalog = Catalog();
        PlaceGraph graph = PlaceGraphLoader.Load(catalog, Routes.Instance);

        PlaceTransition coach = graph.Transitions.Single(transition => transition.Source == "coach");
        Assert.True(coach.IsFare);
        Assert.Equal(2, coach.FareDays);
        Assert.Contains("fare", coach.ToString(), StringComparison.Ordinal);

        PlaceTransition caravan = graph.Transitions.Single(transition => transition.Source == "caravan");
        Assert.True(caravan.IsFare);
        Assert.Equal(3, caravan.FareDays);

        // The road the party can walk is not a fare: nothing sells it, and it states no journey.
        PlaceTransition road = graph.Transitions.Single(transition => transition.Source == "road");
        Assert.False(road.IsFare);
        Assert.Null(road.FareDays);
    }

    [Fact]
    public void A_crossing_sold_as_a_passage_on_no_route_is_a_content_defect()
    {
        // A fare nobody could name is refused where it is read rather than sold as a ticket to nowhere: content
        // must say which route it runs on, because the route is what the game's rule times.
        ContentValidationException error = Assert.Throws<ContentValidationException>(() => PlaceGraphLoader.Load(
            Load(
                """
                { "id": "coach", "fromPlace": "1", "toPlace": "2", "entryPoint": "Party Start", "fare": true }
                """),
            Routes.Instance));

        Assert.Contains(error.Issues, issue => issue.Code == "transition-fare-route-missing");
    }

    [Fact]
    public void A_crossing_on_a_route_no_rule_times_is_a_content_defect()
    {
        // The route is named and the game's rule states no length for it — or the world was built with no rule
        // at all — so no ticket could name the journey, and the world says so where it reads the crossing.
        ContentCatalog unknown = Load(
            """
            { "id": "coach", "fromPlace": "1", "toPlace": "2", "entryPoint": "Party Start", "fare": true, "route": "balloon" }
            """);
        ContentValidationException error = Assert.Throws<ContentValidationException>(() => PlaceGraphLoader.Load(unknown, Routes.Instance));
        Assert.Contains(error.Issues, issue => issue.Code == "transition-fare-days-unstated");

        ContentValidationException unruled = Assert.Throws<ContentValidationException>(() => PlaceGraphLoader.Load(Catalog()));
        Assert.Contains(unruled.Issues, issue => issue.Code == "transition-fare-days-unstated");
    }

    [Fact]
    public void How_long_a_sold_crossing_takes_is_the_rules_answer_not_the_contents()
    {
        // The same content, built under two rules: the journey's length follows the rule, and the content that
        // names the route is not written again.
        ContentCatalog catalog = Catalog();
        PlaceGraph slow = PlaceGraphLoader.Load(catalog, new Routes(coach: 6));
        Assert.Equal(6, slow.Transitions.Single(transition => transition.Source == "coach").FareDays);
        Assert.Equal(2, PlaceGraphLoader.Load(catalog, Routes.Instance).Transitions.Single(transition => transition.Source == "coach").FareDays);
    }

    /// <summary>The test's own fare rule: a length for each route this suite's content names.</summary>
    private sealed class Routes(int coach = 2) : IFareDurationRule
    {
        /// <summary>The coach at two days and the caravan and the boat at three.</summary>
        internal static readonly Routes Instance = new();

        public int? DaysOf(PlaceId? from, PlaceId to, string route) => route switch
        {
            "coach" => coach,
            "caravan" or "boat" => 3,
            _ => null,
        };
    }

    /// <summary>The test's own cost rule: it refuses what a test tells it to and records every question.</summary>
    private sealed class RecordingCostRule : ITravelCostRule
    {
        /// <summary>The kind of travel this rule refuses, or null when it prices everything.</summary>
        internal TransitionKind? Refuse { get; set; }

        /// <summary>Every transition the world asked about, in order.</summary>
        internal List<TransitionRequest> Asked { get; } = [];

        public TravelCostQuote Quote(TransitionRequest request)
        {
            Asked.Add(request);
            return Refuse == request.Kind
                ? TravelCostQuote.Refused(new Refusal("test-refused", $"A test refuses {request.Kind} travel."))
                : TravelCostQuote.Payable(TravelCost.Free);
        }
    }

    /// <summary>A party of one, with a purse, a pack, and a larder, which is where a passage is written.</summary>
    private static PartyEntity Party() =>
        new PartyEntityFactory().Create(
            new PartyCreation(
                [
                    new MemberCreation(new PartyMemberSeed(
                        "Tester",
                        new RaceId("testfolk"),
                        new ClassId("fighter"),
                        [new AttributeScore(new AttributeId("vigour"), 12)],
                        skills: [],
                        spells: [],
                        experience: 0,
                        level: 1,
                        skillPoints: 0,
                        classRank: 1,
                        conditions: [],
                        hitPoints: ResourcePool.Full(10),
                        spellPoints: ResourcePool.Full(5))),
                ],
                coins: 0,
                foodPortions: 4,
                ProvisionUnit.Portions,
                reputation: 0,
                fame: 0));

    /// <summary>
    /// A world over four places: one whose two counters and one road all reach Town, one port only a boat
    /// reaches, and one nothing reaches.
    /// </summary>
    private static SessionWorld World(
        PartyEntity party,
        ITravelCostRule rule,
        IDiagnosticsService? diagnostics = null)
    {
        PlaceGraph graph = PlaceGraphLoader.Load(Catalog(), Routes.Instance);
        PartyPoseOwner owner = new(
            new PartyPose(Home, PlacePose.Origin),
            new FacingRule(unitsPerTurn: 2048, minimumPitch: -512, maximumPitch: 512));
        return new SessionWorld(
            graph,
            owner,
            new PlaceStateLedger(graph, PlaceRespawnRule.FromContent()),
            rule,
            time: null,
            mover: null,
            diagnostics,
            entrances: null,
            clock: null,
            resources: null,
            partyEntity: party);
    }

    private static ContentCatalog Catalog() => Load(
        """
        { "id": "coach", "fromPlace": "1", "toPlace": "2", "entryPoint": "Party Start", "fare": true, "route": "coach" }
        """,
        """
        { "id": "caravan", "fromPlace": "1", "toPlace": "2", "entryPoint": "Party Start", "fare": true, "route": "caravan" }
        """,
        """
        { "id": "boat", "fromPlace": "1", "toPlace": "3", "entryPoint": "Party Start", "fare": true, "route": "boat" }
        """,
        """
        { "id": "road", "fromPlace": "1", "toPlace": "2", "entryPoint": "Party Start" }
        """);

    private static ContentCatalog Load(params string[] links) => ContentCatalogLoader.Load(
        new InMemoryContentSource()
            .Add("packs/world/pack.json", Manifest)
            .Add("packs/world/places.json", Places)
            .Add("packs/world/links.json", TestPacks.Document("links", "travel-link", links)),
        Layout).RequireValid();

    private const string Places =
        """
        { "documentId": "places", "definitionKind": "place", "entries": [
          { "id": "1", "kind": "region", "name": "Home", "respawnDays": 1,
            "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ] },
          { "id": "2", "kind": "region", "name": "Town", "respawnDays": 1,
            "entryPoints": [ { "id": "Party Start", "x": 5, "y": 6, "z": 7, "yaw": 0 } ] },
          { "id": "3", "kind": "region", "name": "Port", "respawnDays": 1,
            "entryPoints": [ { "id": "Party Start", "x": 8, "y": 9, "z": 10, "yaw": 0 } ] },
          { "id": "4", "kind": "region", "name": "Nowhere", "respawnDays": 1,
            "entryPoints": [ { "id": "Party Start", "x": 11, "y": 12, "z": 13, "yaw": 0 } ] }
        ] }
        """;

    private const string Manifest =
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
}
