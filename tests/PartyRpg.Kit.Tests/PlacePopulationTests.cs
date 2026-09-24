using PartyRpg.Kit.Content;
using PartyRpg.Kit.World;
using Rusty.Engine.Entities;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// The population of a place: what content puts there, what a visit owns, and what leaving takes away.
/// </summary>
public sealed class PlacePopulationTests
{
    private static readonly ContentLayout Layout = new("packs", "imports", "bundles");

    /// <summary>A region holding one placement of each kind, on a three-day reset.</summary>
    private static readonly PlaceId Home = new("1");

    /// <summary>An interior holding three placements, on a ten-day reset.</summary>
    private static readonly PlaceId Cave = new("2");

    /// <summary>An interior content gives nothing at all.</summary>
    private static readonly PlaceId Crypt = new("3");

    [Fact]
    public void A_place_populates_from_the_placements_content_declares()
    {
        (PlaceGraph graph, PlaceStateLedger ledger) = World();
        using PlacePopulation population = new(graph, ledger);

        IReadOnlyList<PlacePopulationEntity> entities = population.Step(Home, []);

        Assert.Equal(Home, population.Place);
        Assert.Equal(4, entities.Count);
        Assert.Equal(
            [
                new PlacementContentId("spawn", "spawn-0"),
                new PlacementContentId("decoration", "decoration-0"),
                new PlacementContentId("light", "light-0"),
                new PlacementContentId("door", "door-0"),
            ],
            entities.Select(entity => entity.Content));

        // A placement's position and content identity come from content exactly as declared, and the
        // fields this layer has no opinion about stay readable on the entry behind it.
        Assert.Equal(new PlacePose(10, 20, 0, 0, 0), entities[0].Pose);
        Assert.Equal(new PlacementContentId("spawn", "spawn-0"), entities[0].Content);
        Assert.Equal("spawnPoints", entities[0].Placement.SourceField);
        Assert.Equal(0, entities[0].Placement.SourceIndex);
        Assert.Equal(12, entities[0].Placement.Source.GetInt32("radius"));
        Assert.Equal(new PlacePose(1, 2, 0, 512, 0), entities[1].Pose);
        Assert.Equal("Signpost", entities[1].Placement.Source.GetString("name"));

        // The placements a place declares are readable whether or not the party is standing in it.
        Assert.Equal(3, population.PlacementsOf(Cave).Count);
        Assert.Empty(population.PlacementsOf(Crypt));
    }

    [Fact]
    public void A_place_content_gives_no_placements_populates_nobody()
    {
        (PlaceGraph graph, PlaceStateLedger ledger) = World();
        using PlacePopulation population = new(graph, ledger);

        population.Step(Home, []);
        IReadOnlyList<PlacePopulationEntity> entities = population.Step(Crypt, []);

        Assert.Empty(entities);
        Assert.Equal(Crypt, population.Place);
        Assert.Equal(0, population.Diagnostics.EntityCount);
    }

    [Fact]
    public void The_population_counts_its_entities_by_content_kind()
    {
        (PlaceGraph graph, PlaceStateLedger ledger) = World();
        using PlacePopulation population = new(graph, ledger);

        population.Step(Home, []);
        PlacePopulationDiagnostics diagnostics = population.Diagnostics;

        Assert.Equal(Home, diagnostics.Place);
        Assert.Equal(4, diagnostics.EntityCount);
        Assert.Equal(1, diagnostics.CountOf("spawn"));
        Assert.Equal(1, diagnostics.CountOf("decoration"));
        Assert.Equal(1, diagnostics.CountOf("light"));
        Assert.Equal(1, diagnostics.CountOf("door"));
        Assert.Equal(0, diagnostics.CountOf("something-content-never-declared"));
        Assert.Equal(4, diagnostics.ByKind.Values.Sum());
    }

    [Fact]
    public void Content_identity_is_not_runtime_identity()
    {
        (PlaceGraph graph, PlaceStateLedger ledger) = World();
        using PlacePopulation population = new(graph, ledger);

        IReadOnlyList<PlacePopulationEntity> first = population.Step(Home, []);
        EntityId[] runtime = [.. first.Select(entity => entity.Id)];

        // Every entity has its own runtime identity, and the engine's kind metadata is the content kind
        // rather than another runtime number.
        Assert.Equal(4, runtime.Distinct().Count());
        Assert.Equal(4, first.Select(entity => entity.Content).Distinct().Count());
        Assert.All(first, entity => Assert.Equal(entity.Content.Kind, entity.Actor.TypeId.Value));

        // Leaving and coming back produces the same content identities in the same order, and runtime
        // identities that are new: the runtime half belongs to the visit, and nothing durable can be
        // keyed by it.
        population.Step(Cave, []);
        IReadOnlyList<PlacePopulationEntity> second = population.Step(Home, []);
        Assert.Equal(first.Select(entity => entity.Content), second.Select(entity => entity.Content));
        Assert.DoesNotContain(second, entity => runtime.Contains(entity.Id));
    }

    [Fact]
    public void Re_entry_produces_the_same_population_and_never_two_of_anything()
    {
        (PlaceGraph graph, PlaceStateLedger ledger) = World();
        using PlacePopulation population = new(graph, ledger);

        IReadOnlyList<PlacePopulationEntity> first = population.Step(Home, []);
        PlacementContentId[] declared = [.. first.Select(entity => entity.Content)];

        population.Step(Cave, []);
        IReadOnlyList<PlacePopulationEntity> again = population.Step(Home, []);

        Assert.Equal(declared, again.Select(entity => entity.Content));
        Assert.Equal(declared.Length, again.Count);
        // The store's own count is the proof nothing was duplicated: a second population left behind
        // would show up here even if the list this class hands out looked right.
        Assert.Equal(declared.Length, population.Diagnostics.EntityCount);
    }

    [Fact]
    public void Leaving_destroys_everything_the_visit_created()
    {
        (PlaceGraph graph, PlaceStateLedger ledger) = World();
        using PlacePopulation population = new(graph, ledger);

        PlacePopulationEntity departed = population.Step(Home, [])[0];
        Assert.True(departed.IsAlive);

        population.Step(Cave, []);

        // The entity the party left behind is dead, the store holds only what the new place declares,
        // and a reference to the old entity reports the truth instead of stale components.
        Assert.False(departed.IsAlive);
        Assert.Equal(3, population.Diagnostics.EntityCount);
        Assert.DoesNotContain(population.Entities, entity => entity.Id == departed.Id);

        population.Dispose();
        Assert.Empty(population.Entities);
        Assert.Equal(0, population.Diagnostics.EntityCount);
        Assert.Null(population.Place);
    }

    [Fact]
    public void An_emptied_place_stays_empty_until_the_world_restores_it()
    {
        (PlaceGraph graph, PlaceStateLedger ledger) = World();
        using PlacePopulation population = new(graph, ledger);

        population.Step(Home, []);
        ledger.MarkVisited(Home);
        ledger.MarkCleared(Home);

        population.Step(Cave, []);
        population.Step(Home, []);

        Assert.Empty(population.Entities);
        Assert.Equal(0, population.Diagnostics.EntityCount);

        // The world restoring the place is what brings its population back, and the party is standing in
        // it, so the rebuild happens in the same update that reports the restore.
        IReadOnlyList<PlaceState> restored = ledger.AdvanceTo(3);
        Assert.Equal(Home, Assert.Single(restored).Place);
        population.Step(Home, restored);

        Assert.Equal(4, population.Entities.Count);
        Assert.Equal(4, population.Diagnostics.EntityCount);
        Assert.Equal(1, ledger.StateOf(Home).RespawnCount);
    }

    [Fact]
    public void A_restore_of_another_place_leaves_this_visit_alone()
    {
        (PlaceGraph graph, PlaceStateLedger ledger) = World();
        using PlacePopulation population = new(graph, ledger);
        ledger.MarkVisited(Cave);
        ledger.MarkCleared(Cave);

        EntityId[] standing = [.. population.Step(Home, []).Select(entity => entity.Id)];

        IReadOnlyList<PlaceState> restored = ledger.AdvanceTo(10);
        Assert.Equal(Cave, Assert.Single(restored).Place);
        population.Step(Home, restored);

        // Nothing is alive for a place the party is not in, so a restore elsewhere is not this visit's
        // business: the entities standing here keep the identities they were created with.
        Assert.Equal(standing, population.Entities.Select(entity => entity.Id));
    }

    [Fact]
    public void A_placement_without_an_identity_or_with_a_reused_one_is_refused_by_name()
    {
        ContentValidationException missing = Assert.Throws<ContentValidationException>(() => PopulationOver(
            """{ "id": "1", "kind": "region", "name": "Home", "placements": [ { "kind": "spawn", "x": 1 } ] }"""));
        Assert.Contains("placement-identity-missing", string.Join(",", missing.Issues.Select(issue => issue.Code)));

        ContentValidationException reused = Assert.Throws<ContentValidationException>(() => PopulationOver(
            """
            { "id": "1", "kind": "region", "name": "Home", "placements": [
                { "id": "spawn-0", "kind": "spawn", "x": 1 },
                { "id": "spawn-0", "kind": "spawn", "x": 2 } ] }
            """));
        Assert.Contains("placement-identity-reused", string.Join(",", reused.Issues.Select(issue => issue.Code)));
        Assert.Contains("'1'", reused.Message);
    }

    [Fact]
    public void Stepping_into_a_place_the_world_does_not_have_is_refused_without_emptying_the_visit()
    {
        (PlaceGraph graph, PlaceStateLedger ledger) = World();
        using PlacePopulation population = new(graph, ledger);
        IReadOnlyList<PlacePopulationEntity> standing = population.Step(Home, []);

        ContentValidationException error = Assert.Throws<ContentValidationException>(
            () => population.Step(new PlaceId("99"), []));

        Assert.Contains("99", error.Message);
        Assert.Equal(Home, population.Place);
        Assert.Equal(standing.Select(entity => entity.Id), population.Entities.Select(entity => entity.Id));
    }

    /// <summary>The world these tests populate: three places, two of which content fills.</summary>
    private static (PlaceGraph Graph, PlaceStateLedger Ledger) World() => WorldOver(Places(
        """
        { "id": "1", "kind": "region", "name": "Home", "respawnDays": 3, "placements": [
            { "id": "spawn-0", "kind": "spawn", "sourceField": "spawnPoints", "sourceIndex": 0, "x": 10, "y": 20, "z": 0, "radius": 12, "type": 3, "treasureLevelOrMonsterIndex": 4 },
            { "id": "decoration-0", "kind": "decoration", "sourceField": "decorations", "sourceIndex": 0, "x": 1, "y": 2, "z": 0, "yaw": 512, "name": "Signpost" },
            { "id": "light-0", "kind": "light", "sourceField": "lights", "sourceIndex": 0, "x": 3, "y": 4, "z": 64, "radius": 400, "brightness": 8 },
            { "id": "door-0", "kind": "door", "sourceField": "doors", "sourceIndex": 0, "x": 5, "y": 6, "z": 0, "positionSource": "vertexIds", "doorId": 77 } ] }
        """,
        """
        { "id": "2", "kind": "interior", "name": "Cave", "respawnDays": 10, "placements": [
            { "id": "spawn-0", "kind": "spawn", "sourceField": "spawnPoints", "sourceIndex": 0, "x": 7, "y": 8, "z": 0 },
            { "id": "spawn-1", "kind": "spawn", "sourceField": "spawnPoints", "sourceIndex": 1, "x": 9, "y": 10, "z": 0 },
            { "id": "decoration-0", "kind": "decoration", "sourceField": "decorations", "sourceIndex": 0, "x": 11, "y": 12, "z": 0 } ] }
        """,
        """{ "id": "3", "kind": "interior", "name": "Crypt" }"""));

    /// <summary>A world over places written inline, so a content defect is what construction refuses.</summary>
    private static (PlaceGraph Graph, PlaceStateLedger Ledger) WorldOver(string places)
    {
        PlaceGraph graph = PlaceGraphLoader.Load(ContentCatalogLoader.Load(
            new InMemoryContentSource()
                .Add("packs/world/pack.json", Manifest())
                .Add("packs/world/places.json", Document("places", "place", places)),
            Layout).RequireValid());
        return (graph, new PlaceStateLedger(graph, PlaceRespawnRule.FromContent()));
    }

    /// <summary>A population over places written inline, which is where their placements are read.</summary>
    private static PlacePopulation PopulationOver(string places)
    {
        (PlaceGraph graph, PlaceStateLedger ledger) = WorldOver(places);
        return new PlacePopulation(graph, ledger);
    }

    private static string Places(params string[] entries) => string.Join(",", entries);

    private static string Manifest() =>
        """
        {
          "schemaVersion": 1,
          "packId": "world",
          "kind": "definitions",
          "provenance": { "description": "test content" },
          "documents": [
            { "path": "places.json", "documentId": "places", "definitionKind": "place" }
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
