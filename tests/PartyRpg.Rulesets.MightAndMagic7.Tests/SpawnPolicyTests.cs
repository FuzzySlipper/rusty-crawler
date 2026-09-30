using PartyRpg.Kit.Content;
using PartyRpg.Kit.World;
using Xunit;

namespace PartyRpg.Rulesets.MightAndMagic7.Tests;

/// <summary>
/// This game's resolution of the encounters a level's spawn records ask for: which grade each creature is,
/// how many stand on the field, and that the answer is the same every time the place is populated.
/// </summary>
/// <remarks>
/// The importer writes an encounter — a slot, a grade when the record fixes one, a count range, a difficulty,
/// and the variant rows — and chooses nothing; these cases hold the ruleset to making the choice through the
/// engine's keyed random service under the place and the spawn record, so a save and a load see the same
/// population without the save carrying it.
/// </remarks>
public sealed class SpawnPolicyTests
{
    private static readonly ContentLayout Layout = new("packs", "imports", "bundles");

    private static readonly PlaceId Home = new("1");

    [Fact]
    public void A_place_populated_twice_from_the_same_key_holds_the_same_creatures()
    {
        ContentCatalog catalog = Catalog();
        PlaceGraph graph = PlaceGraphLoader.Load(catalog);

        // Two sessions' worth of resolution — a fresh composition each, as a load is — over the same content and
        // the same engine seed: nothing is remembered between them, and the key is the only state.
        IReadOnlyList<string> first = Populate(graph, MightAndMagic7Spawns.Compose(catalog, new KeyedTestRandom()));
        IReadOnlyList<string> second = Populate(graph, MightAndMagic7Spawns.Compose(catalog, new KeyedTestRandom()));
        Assert.Equal(first, second);

        // The random slot drew its count from its own range, and every creature of it a grade the map's odds
        // allow; the graded record put exactly one creature of its own grade on the field.
        using PlacePopulation population = new(
            graph,
            new PlaceStateLedger(graph, PlaceRespawnRule.FromContent()),
            composer: null,
            MightAndMagic7Spawns.Compose(catalog, new KeyedTestRandom()));
        IReadOnlyList<PlacePopulationEntity> creatures = [.. population.Step(Home, []).Where(entity => entity.Content.Kind == "monster")];
        List<PlacePopulationEntity> drawn = [.. creatures.Where(entity => entity.Placement.Source.GetInt32("spawn") == 3)];
        Assert.InRange(drawn.Count, 2, 6);
        Assert.All(drawn, creature =>
        {
            Assert.Contains(creature.Placement.Source.GetString("grade"), new[] { "A", "B", "C" });
            Assert.Equal("difficulty-odds", creature.Placement.Source.GetString("gradeSource"));
            Assert.Equal("slot-range", creature.Placement.Source.GetString("countSource"));
            Assert.Equal(drawn.Count, creature.Placement.Source.GetInt32("quantity"));
            Assert.Equal("encounter-3", creature.Placement.Source.GetString("encounterPlacement"));
        });
        Assert.Equal(new PlacePose(100, 200, 0, 0, 0), drawn[0].Pose);
        Assert.NotEqual(drawn[0].Pose, drawn[1].Pose);

        PlacePopulationEntity graded = Assert.Single(creatures, entity => entity.Placement.Source.GetInt32("spawn") == 4);
        Assert.Equal("5", graded.Placement.Source.GetId("monster"));
        Assert.Equal("B", graded.Placement.Source.GetString("grade"));
        Assert.Equal("spawn-slot", graded.Placement.Source.GetString("gradeSource"));
        Assert.Equal("spawn-slot", graded.Placement.Source.GetString("countSource"));

        // The encounter itself is not a thing in the world: it stands as the creatures it resolved to.
        Assert.DoesNotContain(population.Entities, entity => entity.Content.Kind == MightAndMagic7Spawns.EncounterPlacementKind);

        // An errand's count of "every one in that place" is the same resolution the population made.
        Dictionary<string, int> counted = MightAndMagic7Spawns.Compose(catalog, new KeyedTestRandom()).Count(catalog)[Home.Value];
        Assert.Equal(creatures.Count, counted.Values.Sum());
        Assert.Equal(
            creatures.GroupBy(entity => entity.Placement.Source.GetId("monster")).ToDictionary(group => group.Key, group => group.Count()),
            counted);
    }

    [Fact]
    public void A_different_spawn_record_draws_under_its_own_key()
    {
        ContentCatalog catalog = Catalog();
        MightAndMagic7Spawns spawns = MightAndMagic7Spawns.Compose(catalog, new KeyedTestRandom());
        PlacementDefinition encounter = PlacePopulationContent.Read(PlaceGraphLoader.Load(catalog))
            .PlacementsOf(Home)
            .Single(placement => placement.Content.Id == "encounter-3");

        // The same record in another place is another key: every draw it makes names that place, so the two
        // resolutions are asked under different keys even when they happen to agree.
        KeyedTestRandom recorder = new();
        MightAndMagic7Spawns.Compose(catalog, recorder).Resolve(new PlaceId("9"), encounter.Content.Id, encounter.Source.Payload);
        Assert.NotEmpty(recorder.Keys);
        Assert.All(recorder.Keys, key => Assert.StartsWith($"{MightAndMagic7Spawns.RollScope}|9/3/", key, StringComparison.Ordinal));
        Assert.Same(
            spawns.Resolve(Home, encounter.Content.Id, encounter.Source.Payload),
            spawns.Resolve(Home, encounter.Content.Id, encounter.Source.Payload));
    }

    [Fact]
    public void Without_a_random_service_a_graded_record_stands_and_a_drawn_one_is_reported_rather_than_invented()
    {
        ContentCatalog catalog = Catalog();
        PlaceGraph graph = PlaceGraphLoader.Load(catalog);
        MightAndMagic7Spawns spawns = MightAndMagic7Spawns.Compose(catalog, random: null);
        using PlacePopulation population = new(graph, new PlaceStateLedger(graph, PlaceRespawnRule.FromContent()), composer: null, spawns);

        PlacePopulationEntity creature = Assert.Single(population.Step(Home, []), entity => entity.Content.Kind == "monster");
        Assert.Equal("5", creature.Placement.Source.GetId("monster"));
        string note = Assert.Single(spawns.Unresolved);
        Assert.Contains("encounter-3", note, StringComparison.Ordinal);
        Assert.Contains("no random service", note, StringComparison.Ordinal);
    }

    [Fact]
    public void An_encounter_naming_a_row_the_monster_table_does_not_carry_is_refused_at_composition()
    {
        ContentCatalog catalog = Catalog(variantC: "99");

        ContentValidationException error = Assert.Throws<ContentValidationException>(
            () => MightAndMagic7Spawns.Compose(catalog, new KeyedTestRandom()));
        Assert.Contains(error.Issues, issue => issue.Code == "encounter-monster-unknown" && issue.Message.Contains("'99'", StringComparison.Ordinal));
    }

    /// <summary>Every creature a population over the place holds, as the fields a save and a fight would read.</summary>
    private static IReadOnlyList<string> Populate(PlaceGraph graph, MightAndMagic7Spawns spawns)
    {
        using PlacePopulation population = new(graph, new PlaceStateLedger(graph, PlaceRespawnRule.FromContent()), composer: null, spawns);
        return
        [
            .. population.Step(Home, [])
                .Where(entity => entity.Content.Kind == "monster")
                .Select(entity => $"{entity.Content}|{entity.Placement.Source.GetId("monster")}|{entity.Placement.Source.GetString("grade")}|{entity.Pose}"),
        ];
    }

    /// <summary>A place holding one random encounter and one graded one, over three variant rows.</summary>
    private static ContentCatalog Catalog(string variantC = "6") => ContentCatalogLoader.Load(
        new PolicyContentSource()
            .Add(
                "packs/world/pack.json",
                """
                {
                  "schemaVersion": 1,
                  "packId": "world",
                  "kind": "definitions",
                  "provenance": { "description": "authored for a test" },
                  "documents": [
                    { "path": "places.json", "documentId": "places", "definitionKind": "place" },
                    { "path": "monsters.json", "documentId": "monsters", "definitionKind": "monster" }
                  ]
                }
                """)
            .Add(
                "packs/world/places.json",
                $$"""
                { "documentId": "places", "definitionKind": "place", "entries": [
                  { "id": "1", "kind": "region", "name": "Home", "respawnDays": 3,
                    "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
                    "placements": [
                      { "id": "spawn-3", "kind": "spawn", "sourceField": "spawnPoints", "sourceIndex": 3, "x": 100, "y": 200, "z": 0 },
                      { "id": "encounter-3", "kind": "encounter", "sourceField": "spawnPoints", "sourceIndex": 3, "x": 100, "y": 200, "z": 0,
                        "spawn": 3, "encounter": 2, "slot": 2, "monsterKind": "Beast", "difficulty": 3, "appearMin": 2, "appearMax": 6,
                        "group": 0, "attributes": 0, "radius": 64,
                        "variants": [
                          { "grade": "A", "monster": 4, "monsterName": "Beast A" },
                          { "grade": "B", "monster": 5, "monsterName": "Beast B" },
                          { "grade": "C", "monster": {{variantC}}, "monsterName": "Beast C" } ] },
                      { "id": "encounter-4", "kind": "encounter", "sourceField": "spawnPoints", "sourceIndex": 4, "x": 300, "y": 0, "z": 0,
                        "spawn": 4, "encounter": 8, "slot": 2, "grade": "B", "monsterKind": "Beast", "difficulty": 3, "appearMin": 2, "appearMax": 6,
                        "group": 0, "attributes": 0, "radius": 64,
                        "variants": [
                          { "grade": "A", "monster": 4, "monsterName": "Beast A" },
                          { "grade": "B", "monster": 5, "monsterName": "Beast B" },
                          { "grade": "C", "monster": {{variantC}}, "monsterName": "Beast C" } ] } ] } ] }
                """)
            .Add(
                "packs/world/monsters.json",
                """
                { "documentId": "monsters", "definitionKind": "monster", "entries": [
                  { "id": "4", "name": "Beast A" }, { "id": "5", "name": "Beast B" }, { "id": "6", "name": "Beast C" } ] }
                """),
        Layout).RequireValid();
}
