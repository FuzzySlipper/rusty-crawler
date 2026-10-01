using PartyRpg.Kit.Content;
using PartyRpg.Kit.World;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>The world graph: what content loads into it, and what it refuses.</summary>
public sealed class PlaceGraphTests
{
    private static readonly ContentLayout Layout = new("packs", "imports", "bundles");

    [Fact]
    public void A_graph_of_regions_and_interiors_loads_with_its_transitions()
    {
        PlaceGraph graph = Load(
            Places(
                """{ "id": "1", "kind": "region", "name": "Home", "entryPoints": [ { "id": "Party Start", "x": 10, "y": 20, "z": 30, "yaw": 512 } ] }""",
                """{ "id": "2", "kind": "interior", "name": "Cave", "entryPoints": [ { "id": "Party Start", "x": 1, "y": 2, "z": 3, "yaw": 0 } ] }"""),
            Links(
                """{ "id": "0", "fromPlace": "1", "toPlace": "2", "entryPoint": "Party Start" }""",
                """{ "id": "1", "fromPlace": "2", "toPlace": "1", "x": 100, "y": 200, "z": 0, "yaw": 1024, "pitch": 0 }"""));

        Assert.Equal(2, graph.Places.Count);
        Assert.Equal(PlaceKind.Region, graph.Require(new PlaceId("1")).Kind);
        Assert.Equal(PlaceKind.Interior, graph.Require(new PlaceId("2")).Kind);

        // Both directions come from the same mechanism: one code path answers "where can I go".
        PlaceTransition into = Assert.Single(graph.TransitionsFrom(new PlaceId("1")));
        PlaceTransition back = Assert.Single(graph.TransitionsFrom(new PlaceId("2")));
        Assert.Equal(new PlaceId("2"), into.To);
        Assert.Equal(new PlaceId("1"), back.To);

        // A named arrival resolves to the destination's own point; a carried pose is used as it stands.
        Assert.Equal(new PlacePose(1, 2, 3, 0, 0), graph.ResolveArrival(into));
        Assert.Equal(new PlacePose(100, 200, 0, 1024, 0), graph.ResolveArrival(back));
    }

    [Fact]
    public void A_transition_that_names_an_arrival_point_the_destination_lacks_fails_by_name()
    {
        ContentValidationException error = Assert.Throws<ContentValidationException>(() => Load(
            Places("""{ "id": "1", "kind": "region", "name": "Home", "entryPoints": [] }"""),
            Links("""{ "id": "0", "toPlace": "1", "entryPoint": "Party Start" }""")));

        Assert.Contains("Party Start", error.Message);
        Assert.Contains("entry-point-unknown", string.Join(",", error.Issues.Select(issue => issue.Code)));
    }

    [Fact]
    public void A_transition_to_a_place_no_pack_declares_fails_by_name()
    {
        ContentValidationException error = Assert.Throws<ContentValidationException>(() => Load(
            Places("""{ "id": "1", "kind": "region", "name": "Home" }"""),
            Links("""{ "id": "0", "fromPlace": "1", "toPlace": "99", "x": 1 }""")));

        Assert.Contains(error.Issues, issue => issue.Code == "transition-destination-unknown");
        Assert.Contains("99", error.Message);
    }

    [Fact]
    public void A_place_with_an_unknown_kind_is_refused_rather_than_guessed()
    {
        ContentValidationException error = Assert.Throws<ContentValidationException>(() => Load(
            Places("""{ "id": "1", "kind": "dungeon", "name": "Home" }"""),
            Links()));

        Assert.Contains("dungeon", error.Message);
        Assert.Contains("place-kind-unknown", string.Join(",", error.Issues.Select(issue => issue.Code)));
    }

    [Fact]
    public void A_world_issued_transition_belongs_to_no_place()
    {
        PlaceGraph graph = Load(
            Places("""{ "id": "1", "kind": "region", "name": "Home" }"""),
            Links("""{ "id": "0", "toPlace": "1", "x": 5, "y": 6, "z": 0 }"""));

        PlaceTransition worldIssued = Assert.Single(graph.WorldIssued);
        Assert.True(worldIssued.IsWorldIssued);
        Assert.Empty(graph.TransitionsFrom(new PlaceId("1")));
        Assert.Equal(new PlacePose(5, 6, 0, 0, 0), graph.ResolveArrival(worldIssued));
    }

    [Fact]
    public void A_destination_below_and_behind_the_origin_arrives_where_it_says()
    {
        // A sewer arrival stands below its map's zero height and west of its origin: the pose is the negative
        // numbers the link states, not wrapped ones.
        PlaceGraph graph = Load(
            Places("""{ "id": "1", "kind": "region", "name": "Home" }""", """{ "id": "2", "kind": "interior", "name": "Sewer" }"""),
            Links("""{ "id": "0", "fromPlace": "1", "toPlace": "2", "x": -1024, "y": 300, "z": -511, "yaw": 0, "pitch": 0 }"""));

        PlaceTransition down = Assert.Single(graph.TransitionsFrom(new PlaceId("1")));
        Assert.Equal(new PlacePose(-1024, 300, -511, 0, 0), graph.ResolveArrival(down));
    }

    [Fact]
    public void An_identity_written_as_a_number_and_one_written_as_a_string_mean_the_same_place()
    {
        PlaceGraph graph = Load(
            Places(
                """{ "id": "1", "kind": "region", "name": "Home", "entryPoints": [ { "id": 7, "x": 1 } ] }""",
                """{ "id": "2", "kind": "interior", "name": "Cave", "entryPoints": [ { "id": "Party Start", "x": 2 } ] }"""),
            Links("""{ "id": "0", "fromPlace": 1, "toPlace": 2, "entryPoint": "party start" }"""));

        PlaceTransition transition = Assert.Single(graph.TransitionsFrom(new PlaceId("1")));
        Assert.Equal(new PlaceId("2"), transition.To);
        Assert.Equal(new PlacePose(2, 0, 0, 0, 0), graph.ResolveArrival(transition));
        Assert.Equal(new PlacePose(1, 0, 0, 0, 0), graph.Require(new PlaceId("1")).FindEntryPoint("7")!.Pose);
    }

    [Fact]
    public void A_place_whose_arrival_points_share_an_id_is_refused_naming_the_place_and_the_id()
    {
        // A start and a transition both name an arrival point by its id, so a place declaring one id twice would
        // answer both with whichever came first and leave the author's other point dead: the graph is refused, as
        // a place declared twice is, with the place and the id named.
        ContentValidationException error = Assert.Throws<ContentValidationException>(() => Load(
            Places(
                """{ "id": "1", "kind": "region", "name": "Home", "entryPoints": [ { "id": "West Start", "x": 1 }, { "id": "Party Start" }, { "id": "West Start", "x": 2 } ] }""",
                """{ "id": "2", "kind": "interior", "name": "Cave", "entryPoints": [ { "id": "North Start", "x": 1 }, { "id": "north start", "x": 2 } ] }"""),
            Links("""{ "id": "0", "toPlace": "1", "entryPoint": "West Start" }""")));

        Assert.Equal(new[] { "entry-point-id-reused", "entry-point-id-reused" }, error.Issues.Select(issue => issue.Code));
        Assert.Equal("place '1' declares arrival point 'West Start' more than once.", error.Issues[0].Message);
        Assert.Contains("The world graph cannot be built: place '1' declares arrival point 'West Start' more than once.", error.Message, StringComparison.Ordinal);

        // Ids that differ only in case are one id, because that is how a point is looked up; both spellings are named.
        Assert.StartsWith("place '2' declares arrival point 'north start' more than once", error.Issues[1].Message, StringComparison.Ordinal);
        Assert.Contains("'North Start'", error.Issues[1].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_place_whose_arrival_points_are_distinct_loads_each_of_them_unchanged()
    {
        PlaceGraph graph = Load(
            Places("""{ "id": "1", "kind": "region", "name": "Home", "entryPoints": [ { "id": "West Start", "x": 1 }, { "id": "East Start", "x": 2 }, { "id": "Party Start", "x": 3 } ] }"""),
            Links("""{ "id": "0", "toPlace": "1", "entryPoint": "east start" }"""));

        PlaceDefinition home = graph.Require(new PlaceId("1"));
        Assert.Equal(new[] { "West Start", "East Start", "Party Start" }, home.EntryPoints.Select(point => point.Id));
        Assert.Equal(new PlacePose(2, 0, 0, 0, 0), graph.ResolveArrival(Assert.Single(graph.WorldIssued)));
        Assert.Equal(new PlacePose(1, 0, 0, 0, 0), home.FindEntryPoint("WEST START")!.Pose);
        Assert.Null(home.FindEntryPoint("South Start"));
    }

    [Fact]
    public void A_definition_built_with_two_points_answering_to_one_id_refuses_the_lookup_rather_than_choosing()
    {
        // The loader never builds one, but a definition is a value anyone can construct: the lookup itself
        // refuses a name two points answer to, so no path can land a party at whichever came first.
        PlaceGraph graph = Load(Places("""{ "id": "1", "kind": "region", "name": "Home" }"""), Links());
        PlaceDefinition twice = graph.Require(new PlaceId("1")) with
        {
            EntryPoints = [new PlaceEntryPoint("West Start", PlacePose.Origin), new PlaceEntryPoint("west start", new PlacePose(1, 0, 0, 0, 0))],
        };

        ContentValidationException error = Assert.Throws<ContentValidationException>(() => twice.FindEntryPoint("West Start"));
        ContentValidationIssue ambiguous = Assert.Single(error.Issues);
        Assert.Equal("entry-point-ambiguous", ambiguous.Code);
        Assert.Contains("'West Start' and 'west start'", ambiguous.Message, StringComparison.Ordinal);

        // A graph built from such a definition refuses a transition naming it the same way, by name.
        PlaceGraph assembled = PlaceGraph.From([twice], []);
        Assert.Throws<ContentValidationException>(() => assembled.ResolveArrival(
            new PlaceTransition(null, twice.Id, PlaceArrival.AtEntryPoint("WEST START"), "start")));
    }

    [Fact]
    public void Asking_a_graph_for_a_place_it_does_not_have_names_the_place()
    {
        PlaceGraph graph = Load(Places("""{ "id": "1", "kind": "region", "name": "Home" }"""), Links());

        ContentValidationException error = Assert.Throws<ContentValidationException>(() => graph.Require(new PlaceId("42")));
        ContentValidationIssue unknown = Assert.Single(error.Issues);
        Assert.Equal("place-unknown", unknown.Code);
        Assert.Equal("42", unknown.PackId);
        Assert.Null(graph.Find(new PlaceId("42")));
    }

    private static PlaceGraph Load(string places, string links) =>
        PlaceGraphLoader.Load(ContentCatalogLoader.Load(
            new InMemoryContentSource()
                .Add("packs/world/pack.json", TestPacks.World)
                .Add("packs/world/places.json", TestPacks.Document("places", "place", places))
                .Add("packs/world/links.json", TestPacks.Document("links", "travel-link", links)),
            Layout).RequireValid());

    private static string Places(params string[] entries) => string.Join(",", entries);

    private static string Links(params string[] entries) => string.Join(",", entries);
}
