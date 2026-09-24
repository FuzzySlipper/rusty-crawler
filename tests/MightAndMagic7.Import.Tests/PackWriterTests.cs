using PartyRpg.Kit.Content;
using PartyRpg.Kit.World;
using MightAndMagic7.Import.Lod;
using MightAndMagic7.Import.Maps;
using MightAndMagic7.Import.Tool;
using System.Text.Json;
using Xunit;

namespace MightAndMagic7.Import.Tests;

/// <summary>
/// The contract between the importer and the product: packs the importer writes are packs the product
/// loads. The importer never references the kit and the kit never references the importer, so this is
/// the only place the two shapes meet, and it is the test that keeps them from drifting apart.
/// </summary>
public sealed class PackWriterTests
{
    private static readonly ContentLayout Layout = new("content-packs", "imports", "bundles");

    [Fact]
    public void A_written_pack_is_a_pack_the_product_loads()
    {
        string installRoot = SyntheticInstallation.Create();
        string root = Path.Combine(Path.GetTempPath(), $"mm7-content-{Guid.NewGuid():N}");
        try
        {
            string imports = Path.Combine(root, "imports");
            PackWriteResult written = PackWriter.Write(LodInstall.Open(installRoot), imports, PackWriter.MapDetail.None);

            Assert.Equal(["mm7-tables", "mm7-world"], written.PackIds);
            Assert.Contains("1.1", written.Provenance.BuildString);

            // A bundle that names the written packs is what the host would ship once the operator has
            // generated them; the loader must accept the packs, their provenance, and every reference.
            WriteBundle(root, written.PackIds);
            ContentBootstrapResult bootstrap = ContentBootstrap.Load(new FileContentSource(root), Layout, "imported");

            Assert.True(bootstrap.IsValid, string.Join("; ", bootstrap.Issues.Select(issue => issue.ToString())));
            Assert.Equal(2, bootstrap.Catalog.Packs.Count);
            ResolvedBundle selection = Assert.IsType<ResolvedBundle>(bootstrap.Selection);
            Assert.Equal(2, selection.Packs.Count);
            Assert.Equal(76, bootstrap.Catalog.Entries("place").Count());
            Assert.Equal(36, bootstrap.Catalog.Entries("class").Count());
            Assert.Equal(37, bootstrap.Catalog.Entries("skill").Count());
            Assert.Equal(99, bootstrap.Catalog.Entries("spell").Count());
            Assert.Equal(276, bootstrap.Catalog.Entries("monster").Count());
            Assert.Equal(800, bootstrap.Catalog.Entries("item").Count());
            Assert.Equal(512, bootstrap.Catalog.Entries("quest").Count());
            // Two of the fixture's three moves link maps; the third names the placeholder destination
            // and stays on its own map, so it is not a link.
            Assert.Equal(2, bootstrap.Catalog.Entries("travel-link").Count());
        }
        finally
        {
            Directory.Delete(installRoot, recursive: true);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Two_runs_over_the_same_installation_write_identical_bytes()
    {
        // The maps are decoded for this check, so the placement data they add is proved reproducible
        // along with everything else: two runs that agreed on the tables but disagreed on a door's
        // derived position would be exactly the difference a pack must never carry.
        string installRoot = SyntheticInstallation.Create(withMaps: true);
        string first = Path.Combine(Path.GetTempPath(), $"mm7-run-a-{Guid.NewGuid():N}");
        string second = Path.Combine(Path.GetTempPath(), $"mm7-run-b-{Guid.NewGuid():N}");
        try
        {
            LodInstall install = LodInstall.Open(installRoot);
            PackWriter.Write(install, first);
            PackWriter.Write(install, second);

            Assert.True(PackWriter.AreIdentical(first, second), "two runs over the same installation produced different bytes");
        }
        finally
        {
            Directory.Delete(installRoot, recursive: true);
            if (Directory.Exists(first)) Directory.Delete(first, recursive: true);
            if (Directory.Exists(second)) Directory.Delete(second, recursive: true);
        }
    }

    [Fact]
    public void An_imported_pack_records_the_game_and_the_build_it_came_from()
    {
        string installRoot = SyntheticInstallation.Create();
        string imports = Path.Combine(Path.GetTempPath(), $"mm7-prov-{Guid.NewGuid():N}");
        try
        {
            PackWriter.Write(LodInstall.Open(installRoot), imports, PackWriter.MapDetail.None);
            string manifest = File.ReadAllText(Path.Combine(imports, "mm7-tables", "pack.json"));

            Assert.Contains("\"game\": \"mightandmagic7\"", manifest);
            Assert.Contains("\"build\":", manifest);
            Assert.Contains("containers", manifest);
            Assert.Contains("\"producer\": \"mm7import\"", manifest);
        }
        finally
        {
            Directory.Delete(installRoot, recursive: true);
            if (Directory.Exists(imports)) Directory.Delete(imports, recursive: true);
        }
    }

    [Fact]
    public void Places_carry_their_kind_and_arrival_points_and_transitions_name_where_they_arrive()
    {
        string installRoot = SyntheticInstallation.Create(withMaps: true);
        string root = Path.Combine(Path.GetTempPath(), $"mm7-world-{Guid.NewGuid():N}");
        try
        {
            string imports = Path.Combine(root, "imports");
            PackWriter.Write(LodInstall.Open(installRoot), imports);

            WriteBundle(root, ["mm7-tables", "mm7-world"]);
            ContentBootstrapResult bootstrap = ContentBootstrap.Load(new FileContentSource(root), Layout, "imported");
            Assert.True(bootstrap.IsValid, string.Join("; ", bootstrap.Issues.Select(issue => issue.ToString())));

            PlaceGraph graph = PlaceGraphLoader.Load(bootstrap.Catalog);

            // Thirteen rows named the outdoor payload and sixty-three the indoor one, so the graph has
            // both kinds and the arrival points the maps actually declare.
            Assert.Equal(76, graph.Places.Count);
            Assert.Equal(13, graph.Places.Count(place => place.Kind == PlaceKind.Region));
            Assert.Equal(63, graph.Places.Count(place => place.Kind == PlaceKind.Interior));
            PlaceDefinition region = graph.Places.First(place => place.Kind == PlaceKind.Region);
            Assert.Contains(region.EntryPoints, point => point.Id == "Party Start");
            Assert.Contains(region.EntryPoints, point => point.Id == "North Start");

            // The fixture's one inter-map move carries no position, so it names the destination's start
            // point, and resolving it gives the pose that point declares.
            PlaceTransition transition = Assert.Single(graph.Transitions.Where(edge => !edge.IsWorldIssued && edge.Arrival.IsEntryPoint));
            PlacePose arrival = graph.ResolveArrival(transition);
            PlaceEntryPoint start = graph.Require(transition.To).FindEntryPoint("Party Start")!;
            Assert.Equal(start.Pose, arrival);

            // Arrival is a decoration, so a place that has one has it among its placements too: the
            // entry point and the placement are two readings of the same decoded record, not two
            // records, and neither is allowed to quietly lose it.
            PlacePopulationContent placements = PlacePopulationContent.Read(graph);
            IReadOnlyList<PlacementDefinition> regionPlacements = placements.PlacementsOf(region.Id);
            Assert.Equal(4, regionPlacements.Count);
            Assert.Equal(3, regionPlacements.Count(placement => placement.Content.Kind == "decoration"));
            Assert.Equal(1, regionPlacements.Count(placement => placement.Content.Kind == "spawn"));
            Assert.Contains(regionPlacements, placement => placement.Content.Id == "decoration-0");
            PlacementDefinition firstDecoration = regionPlacements.First(placement => placement.Content.Kind == "decoration");
            Assert.Equal("decorations", firstDecoration.SourceField);
            Assert.Equal(0, firstDecoration.SourceIndex);
        }
        finally
        {
            Directory.Delete(installRoot, recursive: true);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Places_carry_the_placements_the_maps_hold_with_counts_that_match_them()
    {
        string installRoot = SyntheticInstallation.Create(withMaps: true);
        string root = Path.Combine(Path.GetTempPath(), $"mm7-placements-{Guid.NewGuid():N}");
        try
        {
            string imports = Path.Combine(root, "imports");
            PackWriter.Write(LodInstall.Open(installRoot), imports);

            // What the decoder found is the authority the pack is checked against, per kind, so the
            // assertions below cannot drift from the maps the fixture holds.
            MapDecodeReport report = MapDecoder.DecodeAll(LodInstall.Open(installRoot));
            IReadOnlyList<DecodedMap> decoded = [.. report.Decoded.Select(outcome => outcome.Decoded).OfType<DecodedMap>()];
            Assert.Equal(76, decoded.Count);
            int expectedSpawns = decoded.Sum(map => map.SpawnPoints.Count);
            int expectedDecorations = decoded.Sum(map => map.Decorations.Count);
            int expectedDoors = decoded.Sum(map => map.Doors.Count(door => door.InUse));
            int expectedLights = decoded.Sum(map => map.Lights.Count);

            // Thirteen regions of the fixture hold three decorations and one spawn each; its sixty-three
            // interiors hold one decoration, one spawn, one in-use door of the two fixed slots, and two
            // lights each. Stating the fixture's own numbers keeps a decoder change from silently
            // redefining what this test proves.
            Assert.Equal(76, expectedSpawns);
            Assert.Equal(102, expectedDecorations);
            Assert.Equal(63, expectedDoors);
            Assert.Equal(126, expectedLights);

            // The document itself: every place declares its placements and a count per kind, and the
            // counts are what a checker can verify without decoding a map.
            int spawns = 0;
            int decorations = 0;
            int doors = 0;
            int lights = 0;
            using (JsonDocument places = JsonDocument.Parse(File.ReadAllText(Path.Combine(imports, "mm7-tables", "places.json"))))
            {
                foreach (JsonElement entry in places.RootElement.GetProperty("entries").EnumerateArray())
                {
                    JsonElement counts = entry.GetProperty("placementCounts");
                    JsonElement placements = entry.GetProperty("placements");
                    Assert.Equal(placements.GetArrayLength(), counts.GetProperty("total").GetInt32());
                    Assert.Equal(
                        placements.GetArrayLength(),
                        counts.EnumerateObject().Where(property => property.Name != "total").Sum(property => property.Value.GetInt32()));

                    HashSet<string> identities = [];
                    foreach (JsonElement placement in placements.EnumerateArray())
                    {
                        // A placement is data: an identity within its place, the source field it came
                        // from, and a position. Nothing here needs a runtime or a decoded map to read.
                        string kind = placement.GetProperty("kind").GetString()!;
                        string id = placement.GetProperty("id").GetString()!;
                        Assert.True(identities.Add(id), $"place {entry.GetProperty("id").GetString()} declares '{id}' twice");
                        Assert.StartsWith(kind, id, StringComparison.Ordinal);
                        Assert.False(string.IsNullOrEmpty(placement.GetProperty("sourceField").GetString()));
                        Assert.True(placement.TryGetProperty("x", out _));
                        Assert.True(placement.TryGetProperty("y", out _));
                        Assert.True(placement.TryGetProperty("z", out _));
                    }

                    spawns += counts.GetProperty("spawn").GetInt32();
                    decorations += counts.GetProperty("decoration").GetInt32();
                    doors += counts.GetProperty("door").GetInt32();
                    lights += counts.GetProperty("light").GetInt32();
                }
            }

            Assert.Equal(expectedSpawns, spawns);
            Assert.Equal(expectedDecorations, decorations);
            Assert.Equal(expectedDoors, doors);
            Assert.Equal(expectedLights, lights);

            // The product's own reader agrees with the document, and a population built over the
            // imported world holds exactly what content declares for the place it stands in.
            WriteBundle(root, ["mm7-tables", "mm7-world"]);
            ContentBootstrapResult bootstrap = ContentBootstrap.Load(new FileContentSource(root), Layout, "imported");
            Assert.True(bootstrap.IsValid, string.Join("; ", bootstrap.Issues.Select(issue => issue.ToString())));
            PlaceGraph graph = PlaceGraphLoader.Load(bootstrap.Catalog);
            PlacePopulationContent content = PlacePopulationContent.Read(graph);
            PlaceDefinition interior = graph.Places.First(place => place.Kind == PlaceKind.Interior);
            Assert.Equal(5, content.PlacementsOf(interior.Id).Count);
            Assert.Equal(1, content.PlacementsOf(interior.Id).Count(placement => placement.Content.Kind == "spawn"));
            Assert.Equal(1, content.PlacementsOf(interior.Id).Count(placement => placement.Content.Kind == "door"));

            // A door stores no position of its own, so the record says its position came from the
            // vertices it moves rather than passing a derived point off as stored data.
            using (PlacePopulation population = new(graph, new PlaceStateLedger(graph, PlaceRespawnRule.FromContent())))
            {
                IReadOnlyList<PlacePopulationEntity> entities = population.Step(interior.Id, []);
                Assert.Equal(content.PlacementsOf(interior.Id).Select(placement => placement.Content), entities.Select(entity => entity.Content));
                Assert.Equal(5, population.Diagnostics.EntityCount);
                Assert.Equal(1, population.Diagnostics.CountOf("spawn"));
                Assert.Equal(1, population.Diagnostics.CountOf("decoration"));
                Assert.Equal(1, population.Diagnostics.CountOf("door"));
                Assert.Equal(2, population.Diagnostics.CountOf("light"));
            }

            string doorDocument = File.ReadAllText(Path.Combine(imports, "mm7-tables", "places.json"));
            Assert.Contains("\"positionSource\": \"vertexIds\"", doorDocument);
        }
        finally
        {
            Directory.Delete(installRoot, recursive: true);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static void WriteBundle(string root, IReadOnlyList<string> packIds)
    {
        string directory = Path.Combine(root, "bundles", "imported");
        Directory.CreateDirectory(directory);
        File.WriteAllText(
            Path.Combine(directory, "bundle.json"),
            $$"""
            {
              "schemaVersion": 1,
              "bundleId": "imported",
              "ruleset": "mightandmagic7",
              "contentPacks": [{{string.Join(", ", packIds.Select(id => $"\"{id}\""))}}],
              "description": "the packs an import wrote"
            }
            """);
    }
}
