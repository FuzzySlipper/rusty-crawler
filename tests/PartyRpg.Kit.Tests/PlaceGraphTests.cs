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
    public void Asking_a_graph_for_a_place_it_does_not_have_names_the_place()
    {
        PlaceGraph graph = Load(Places("""{ "id": "1", "kind": "region", "name": "Home" }"""), Links());

        ContentValidationException error = Assert.Throws<ContentValidationException>(() => graph.Require(new PlaceId("42")));
        Assert.Contains("42", error.Message);
        Assert.Null(graph.Find(new PlaceId("42")));
    }

    private static PlaceGraph Load(string places, string links) =>
        PlaceGraphLoader.Load(ContentCatalogLoader.Load(
            new InMemoryContentSource()
                .Add("packs/world/pack.json", Manifest())
                .Add("packs/world/places.json", Document("places", "place", places))
                .Add("packs/world/links.json", Document("links", "travel-link", links)),
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
}
