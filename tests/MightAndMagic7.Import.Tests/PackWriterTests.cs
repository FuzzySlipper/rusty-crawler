using PartyRpg.Kit.Content;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.World;
using MightAndMagic7.Import.Collision;
using MightAndMagic7.Import.Events;
using MightAndMagic7.Import.Lod;
using MightAndMagic7.Import.Maps;
using MightAndMagic7.Import.Packs;
using MightAndMagic7.Import.Tables;
using MightAndMagic7.Import.Tool;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Xunit;
using ImportedPlaceGraph = MightAndMagic7.Import.World.PlaceGraph;

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
    public void Secret_door_metadata_comes_from_its_own_faces_and_map_table()
    {
        string install = SyntheticInstallation.Create(withMaps: true, withSecretDoors: true);
        string root = Path.Combine(Path.GetTempPath(), $"mm7-secrets-{Guid.NewGuid():N}");
        try
        {
            LodInstall source = LodInstall.Open(install);
            PackWriter.Write(source, root);
            MapStatsTable table = MapStatsTable.Read(source);
            using JsonDocument places = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "mm7-tables", "places.json")));
            int checkedDoors = 0;
            foreach (JsonElement place in places.RootElement.GetProperty("entries").EnumerateArray())
            {
                int id = int.Parse(place.GetProperty("id").GetString()!, CultureInfo.InvariantCulture);
                foreach (JsonElement target in place.GetProperty("placements").EnumerateArray())
                {
                    if (target.GetProperty("kind").GetString() != "door")
                    {
                        Assert.False(target.TryGetProperty("secret", out _));
                        continue;
                    }
                    Assert.True(target.GetProperty("secret").GetBoolean());
                    Assert.Equal(table.Maps.Single(map => map.Id == id).PerceptionDifficulty, target.GetProperty("perceptionDifficulty").GetInt32());
                    Assert.Equal(0, Assert.Single(target.GetProperty("secretFaces").EnumerateArray()).GetInt32());
                    checkedDoors++;
                }
            }
            Assert.Equal(63, checkedDoors);
        }
        finally
        {
            Directory.Delete(install, recursive: true);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void A_written_pack_is_a_pack_the_product_loads()
    {
        string installRoot = SyntheticInstallation.Create();
        string root = Path.Combine(Path.GetTempPath(), $"mm7-content-{Guid.NewGuid():N}");
        try
        {
            string imports = Path.Combine(root, "imports");
            PackWriteResult written = PackWriter.Write(LodInstall.Open(installRoot), imports, PackWriter.MapDetail.None);

            Assert.Equal(["mm7-tables", "mm7-world", "mm7-media"], written.PackIds);
            Assert.Contains("1.1", written.Provenance.BuildString);

            // A bundle that names the written packs is what the host would ship once the operator has
            // generated them; the loader must accept the packs, their provenance, and every reference.
            WriteBundle(root, written.PackIds);
            ContentBootstrapResult bootstrap = ContentBootstrap.Load(new FileContentSource(root), Layout, "imported");

            Assert.True(bootstrap.IsValid, string.Join("; ", bootstrap.Issues.Select(issue => issue.ToString())));
            Assert.Equal(3, bootstrap.Catalog.Packs.Count);
            ResolvedBundle selection = Assert.IsType<ResolvedBundle>(bootstrap.Selection);
            Assert.Equal(3, selection.Packs.Count);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Two_runs_over_the_same_installation_write_identical_bytes(bool secrets)
    {
        // The maps are decoded for this check, so the placement data they add is proved reproducible
        // along with everything else: two runs that agreed on the tables but disagreed on a door's
        // derived position — or on a container's, which is averaged over the faces that open it — would be
        // exactly the difference a pack must never carry.
        string installRoot = SyntheticInstallation.Create(withMaps: true, withContainers: !secrets, withSecretDoors: secrets);
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
    public void The_determinism_check_reads_only_what_the_write_produced()
    {
        // The documented write goes into the operator's imports root, which may already hold packs the
        // operator authored; those are not the writer's output and must not make two equal runs "differ".
        string installRoot = SyntheticInstallation.Create(withMaps: true);
        string first = Path.Combine(Path.GetTempPath(), $"mm7-root-a-{Guid.NewGuid():N}");
        string second = Path.Combine(Path.GetTempPath(), $"mm7-root-b-{Guid.NewGuid():N}");
        try
        {
            LodInstall install = LodInstall.Open(installRoot);
            Directory.CreateDirectory(Path.Combine(first, "operator-scenario"));
            File.WriteAllText(Path.Combine(first, "operator-scenario", "pack.json"), "{}");
            PackWriteResult written = PackWriter.Write(install, first);
            PackWriter.Write(install, second);

            Assert.False(PackWriter.AreIdentical(first, second));
            Assert.True(PackWriter.AreIdentical(first, second, written));

            File.AppendAllText(Path.Combine(second, "mm7-tables", "pack.json"), " ");
            Assert.False(PackWriter.AreIdentical(first, second, written));
        }
        finally
        {
            foreach (string directory in new[] { installRoot, first, second })
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void Two_operators_with_the_same_data_in_different_places_write_the_same_bytes()
    {
        // The same data at two paths is two operators: where the installation sits is not what was imported,
        // so the packs are byte for byte the same and name neither path.
        string oneOperator = SyntheticInstallation.Create(withMaps: true);
        string another = SyntheticInstallation.Create(withMaps: true);
        string first = Path.Combine(Path.GetTempPath(), $"mm7-operator-a-{Guid.NewGuid():N}");
        string second = Path.Combine(Path.GetTempPath(), $"mm7-operator-b-{Guid.NewGuid():N}");
        try
        {
            PackWriter.Write(LodInstall.Open(oneOperator), first);
            PackWriter.Write(LodInstall.Open(another), second);

            Assert.True(PackWriter.AreIdentical(first, second), "two installations of the same data produced different bytes");
            string manifest = File.ReadAllText(Path.Combine(first, "mm7-tables", "pack.json"));
            Assert.DoesNotContain(oneOperator, manifest, StringComparison.Ordinal);
            Assert.Contains("\"producer\": \"mm7import ", manifest, StringComparison.Ordinal);
        }
        finally
        {
            foreach (string directory in new[] { oneOperator, another, first, second })
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void A_document_an_earlier_import_left_in_a_pack_is_gone_after_the_next()
    {
        string installRoot = SyntheticInstallation.Create(withMaps: true);
        string output = Path.Combine(Path.GetTempPath(), $"mm7-stale-{Guid.NewGuid():N}");
        try
        {
            LodInstall install = LodInstall.Open(installRoot);
            PackWriter.Write(install, output);
            string stale = Path.Combine(output, "mm7-tables", "retired.json");
            File.WriteAllText(stale, "{}");
            string staged = Path.Combine(output, "a-staged-scenario", "pack.json");
            Directory.CreateDirectory(Path.GetDirectoryName(staged)!);
            File.WriteAllText(staged, "{}");

            PackWriter.Write(install, output);

            // The importer's own pack is written afresh, and a pack beside it that is not the importer's is kept.
            Assert.False(File.Exists(stale));
            Assert.True(File.Exists(staged));
        }
        finally
        {
            Directory.Delete(installRoot, recursive: true);
            if (Directory.Exists(output)) Directory.Delete(output, recursive: true);
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
            Assert.Contains("\"producer\": \"mm7import ", manifest);
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

            // Every place carries the map table's base fine — its "Perm" column (OpenEnroth
            // src/Engine/Tables/MapTable.cpp:73) — which the fixture states as the row's id modulo sixteen, so the
            // game's theft rule reads it off the place rather than off a table the runtime never sees.
            Assert.All(graph.Places, place => Assert.Equal(int.Parse(place.Id.Value, System.Globalization.CultureInfo.InvariantCulture) % 16, place.Source.GetInt32("stealFine")));
            Assert.Contains(region.EntryPoints, point => point.Id == "North Start");

            // Every place carries the automap raster the product draws from, and the document is what the
            // runtime reader expects: a grid, and two hexadecimal digits a square. The region's grid is the
            // map's own terrain square at its own pitch, and an interior's is finer and covers the level.
            Assert.Equal(76, bootstrap.Catalog.Entries("place-map").Count());
            using JsonDocument maps = JsonDocument.Parse(File.ReadAllText(Path.Combine(imports, "mm7-world", "place-map.json")));
            JsonElement regionMap = maps.RootElement.GetProperty("entries")
                .EnumerateArray()
                .First(entry => entry.GetProperty("id").GetString() == region.Id.Value);
            Assert.Equal("region", regionMap.GetProperty("kind").GetString());
            Assert.Equal(OutdoorMap.TerrainCellSize, regionMap.GetProperty("cellSize").GetInt32());
            Assert.Equal(OutdoorMap.TerrainCells, regionMap.GetProperty("columns").GetInt32());
            Assert.Equal(OutdoorMap.TerrainCells, regionMap.GetProperty("rows").GetInt32());
            Assert.Equal(
                OutdoorMap.TerrainCells * OutdoorMap.TerrainCells * 2,
                regionMap.GetProperty("kinds").GetString()!.Length);
            Assert.Contains(
                maps.RootElement.GetProperty("entries").EnumerateArray(),
                entry => entry.GetProperty("kind").GetString() == "interior"
                    && entry.GetProperty("kinds").GetString()!.Length
                        == entry.GetProperty("columns").GetInt32() * entry.GetProperty("rows").GetInt32() * 2);

            // The fixture's one inter-map move carries no position, so it names the destination's start
            // point, and resolving it gives the pose that point declares.
            PlaceTransition transition = Assert.Single(graph.Transitions.Where(edge => !edge.IsWorldIssued && edge.Arrival.IsEntryPoint));
            PlacePose arrival = graph.ResolveArrival(transition);
            PlaceEntryPoint start = graph.Require(transition.To).FindEntryPoint("Party Start")!;
            Assert.Equal(start.Pose, arrival);

            // Arrival is a decoration, so a place that has one has it among its placements too: the
            // entry point and the placement are two readings of the same decoded record, not two
            // records, and neither is allowed to quietly lose it. The region's delta also carries one
            // sprite object, which is a placement of its own whether or not it holds anything, and the
            // spawn record puts a creature on the field beside the spawn point it came from. The delta's second
            // actor is a creature the level is built holding, placed under its own index in the actor array.
            PlacePopulationContent placements = PlacePopulationContent.Read(graph);
            IReadOnlyList<PlacementDefinition> regionPlacements = placements.PlacementsOf(region.Id);
            Assert.Equal(7, regionPlacements.Count);
            PlacementDefinition actor = Assert.Single(regionPlacements, placement => placement.Content.Kind == "actor");
            Assert.Equal(("actors", 1), (actor.SourceField, actor.SourceIndex));
            Assert.Equal(3, regionPlacements.Count(placement => placement.Content.Kind == "decoration"));
            Assert.Equal(1, regionPlacements.Count(placement => placement.Content.Kind == "spawn"));
            Assert.Equal(1, regionPlacements.Count(placement => placement.Content.Kind == "encounter"));
            Assert.Equal(0, regionPlacements.Count(placement => placement.Content.Kind == "monster"));
            Assert.Equal(1, regionPlacements.Count(placement => placement.Content.Kind == "sprite"));
            Assert.Contains(regionPlacements, placement => placement.Content.Id == "decoration-0");

            // The encounter is what the spawn record asks for and nothing the importer chose: the slot, the
            // grade the record fixes, and the variant rows, which the ruleset resolves when it populates the place.
            PlacementDefinition encounter = regionPlacements.First(placement => placement.Content.Kind == "encounter");
            Assert.Equal("encounter-0", encounter.Content.Id);
            Assert.Equal(0, encounter.Source.GetInt32("spawn"));
            Assert.Equal(5, encounter.Source.GetInt32("encounter"));
            Assert.Equal("A", encounter.Source.GetString("grade"));
            Assert.Equal("Monster 2", encounter.Source.GetString("monsterKind"));
            Assert.Equal(3, encounter.Source.GetArray("variants").Count);
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
            // interiors hold one decoration, one spawn, one in-use door among the slots their own shape
            // declares, and the lights their own shape gives them. Stating the fixture's own numbers keeps a
            // decoder change from silently redefining what this test proves.
            Assert.Equal(76, expectedSpawns);
            Assert.Equal(102, expectedDecorations);
            Assert.Equal(63, expectedDoors);
            Assert.Equal(Enumerable.Range(1, 63).Sum(interior => SyntheticInstallation.Interior(interior).Lights), expectedLights);

            // Every one of the fixture's spawn records asks for an actor and names the graded slot A of its
            // map's second encounter, which puts exactly one creature on the field: the count a graded slot
            // spawns is the donor's fixed one, and only the three random slots draw a number.
            Assert.Equal(76, expectedSpawns);

            // The document itself: every place declares its placements and a count per kind, and the
            // counts are what a checker can verify without decoding a map.
            int spawns = 0;
            int encounters = 0;
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
                        Assert.False(placement.TryGetProperty("secret", out _));
                    }

                    spawns += counts.GetProperty("spawn").GetInt32();
                    encounters += counts.GetProperty("encounter").GetInt32();
                    decorations += counts.GetProperty("decoration").GetInt32();
                    doors += counts.GetProperty("door").GetInt32();
                    lights += counts.GetProperty("light").GetInt32();
                }
            }

            Assert.Equal(expectedSpawns, spawns);
            Assert.Equal(expectedSpawns, encounters);
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
            // An interior's lights are its own shape's, so the place's total is its four single placements
            // (a spawn, the encounter it asks for, a decoration, and the one door in use) and its lights.
            int interiorLights = SyntheticInstallation.Interior(int.Parse(interior.Id.Value, CultureInfo.InvariantCulture) - SyntheticInstallation.Regions).Lights;
            Assert.Equal(4 + interiorLights, content.PlacementsOf(interior.Id).Count);
            Assert.Equal(1, content.PlacementsOf(interior.Id).Count(placement => placement.Content.Kind == "spawn"));
            Assert.Equal(1, content.PlacementsOf(interior.Id).Count(placement => placement.Content.Kind == "encounter"));
            Assert.Equal(1, content.PlacementsOf(interior.Id).Count(placement => placement.Content.Kind == "door"));

            // A door stores no position of its own, so the record says its position came from the
            // vertices it moves rather than passing a derived point off as stored data.
            using (PlacePopulation population = new(graph, new PlaceStateLedger(graph, PlaceRespawnRule.FromContent())))
            {
                IReadOnlyList<PlacePopulationEntity> entities = population.Step(interior.Id, []);
                Assert.Equal(content.PlacementsOf(interior.Id).Select(placement => placement.Content), entities.Select(entity => entity.Content));
                Assert.Equal(4 + interiorLights, population.Diagnostics.EntityCount);
                Assert.Equal(1, population.Diagnostics.CountOf("spawn"));
                Assert.Equal(1, population.Diagnostics.CountOf("encounter"));
                Assert.Equal(1, population.Diagnostics.CountOf("decoration"));
                Assert.Equal(1, population.Diagnostics.CountOf("door"));
                Assert.Equal(interiorLights, population.Diagnostics.CountOf("light"));
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

    [Fact]
    public void Places_that_can_be_closed_carry_collision_and_the_places_that_cannot_carry_the_reason()
    {
        string installRoot = SyntheticInstallation.Create(withMaps: true);
        string root = Path.Combine(Path.GetTempPath(), $"mm7-geometry-{Guid.NewGuid():N}");
        try
        {
            string imports = Path.Combine(root, "imports");
            PackWriteResult written = PackWriter.Write(LodInstall.Open(installRoot), imports);

            // The fixture's thirteen regions tile their terrain, so each has ground; its sixty-three
            // interiors hold one face whose corners lie on a line, so none of them has any.
            Assert.Equal(76, written.Geometry.PlaceCount);
            Assert.Equal(13, written.Geometry.EmittedCount);
            Assert.Equal(63, written.Geometry.RefusedCount);
            Assert.Equal(13, written.Geometry.EmittedOf(MapKind.Outdoor));
            Assert.Equal(0, written.Geometry.EmittedOf(MapKind.Indoor));
            Assert.All(written.Geometry.Refused, place => Assert.Equal("no-solid-geometry", place.Refusal?.Code));
            Assert.Equal(127 * 127 * 13, written.Geometry.CountOf(CollisionSource.Terrain).Faces);
            Assert.Equal(127 * 127 * 2 * 13, written.Geometry.CountOf(CollisionSource.Terrain).Triangles);

            // The document holds one entry per emitted place and none for a refused one, and each entry's
            // own counts are the artifact's own arrays: a checker reads the document, not the summary.
            using (JsonDocument geometry = JsonDocument.Parse(File.ReadAllText(Path.Combine(imports, "mm7-world", "place-geometry.json"))))
            {
                JsonElement document = geometry.RootElement;
                Assert.Equal("place-geometry", document.GetProperty("documentId").GetString());
                Assert.Equal("place-geometry", document.GetProperty("definitionKind").GetString());

                JsonElement entries = document.GetProperty("entries");
                Assert.Equal(13, entries.GetArrayLength());
                HashSet<string> emitted = [.. written.Geometry.Places.Where(place => place.Emitted).Select(place => place.PlaceId.ToString(CultureInfo.InvariantCulture))];
                foreach (JsonElement entry in entries.EnumerateArray())
                {
                    Assert.Contains(entry.GetProperty("id").GetString()!, emitted);
                    JsonElement artifact = entry.GetProperty("artifact");
                    Assert.Equal(1, artifact.GetProperty("schemaVersion").GetInt32());
                    Assert.Equal(entry.GetProperty("vertices").GetInt32(), artifact.GetProperty("collision").GetProperty("positions").GetArrayLength());
                    Assert.Equal(entry.GetProperty("triangles").GetInt32(), artifact.GetProperty("collision").GetProperty("triangles").GetArrayLength());
                    Assert.Equal(127 * 127, entry.GetProperty("geometryCounts").GetProperty("terrain").GetProperty("faces").GetInt32());
                    Assert.Empty(artifact.GetProperty("navigation").GetProperty("cells").EnumerateArray());

                    // Each region's water row is written beside the artifact as its own named ground.
                    Assert.Equal(126, entry.GetProperty("waterSquares").GetInt32());
                    JsonElement surface = Assert.Single(entry.GetProperty("surfaces").EnumerateArray());
                    Assert.Equal("water", surface.GetProperty("surface").GetString());
                    Assert.Equal(126 * 2, surface.GetProperty("triangles").GetArrayLength());
                }
            }

            // The manifest lists the document, because a pack whose manifest omits it would leave the
            // geometry unloadable even though the file is there.
            string manifest = File.ReadAllText(Path.Combine(imports, "mm7-world", "pack.json"));
            Assert.Contains("\"path\": \"place-geometry.json\"", manifest);
            Assert.Contains("\"definitionKind\": \"place-geometry\"", manifest);

            // The seam the product reads it through: the kit's own geometry source finds the entry by the
            // place's id and hands the engine the artifact's bytes exactly as the document holds them.
            WriteBundle(root, ["mm7-tables", "mm7-world"]);
            ContentBootstrapResult bootstrap = ContentBootstrap.Load(new FileContentSource(root), Layout, "imported");
            Assert.True(bootstrap.IsValid, string.Join("; ", bootstrap.Issues.Select(issue => issue.ToString())));
            PlaceGraph graph = PlaceGraphLoader.Load(bootstrap.Catalog);
            ContentPlaceGeometry source = new(bootstrap.Catalog, "place-geometry", "artifact", "surfaces", "navigationRegion");

            PlaceDefinition region = graph.Places.First(place => place.Kind == PlaceKind.Region);
            PlaceGeometry? collision = source.For(region.Id);
            Assert.NotNull(collision);

            // The product reads the water the importer wrote as the place's named ground.
            Assert.Equal(["water"], collision.Surfaces.Names);
            Assert.Equal(126 * 2, collision.Surfaces.TriangleCount);
            Assert.Equal($"mm7-world/place-geometry/{region.Id.Value}.json", collision.Path);
            Assert.True(collision.Artifact.Length > 0);
            Assert.NotNull(collision.Navigation);
            Assert.Equal(512d, collision.Navigation.CellSize);

            using (JsonDocument document = JsonDocument.Parse(File.ReadAllText(Path.Combine(imports, "mm7-world", "place-geometry.json"))))
            {
                JsonElement entry = document.RootElement.GetProperty("entries").EnumerateArray()
                    .First(candidate => candidate.GetProperty("id").GetString() == region.Id.Value);
                Assert.Equal(entry.GetProperty("artifact").GetRawText(), Encoding.UTF8.GetString(collision.Artifact.Span));
            }

            // A place whose geometry was refused has no entry, so the source answers that it has none
            // rather than failing the read: the party enters a place with nothing to stand on, and the
            // movement owner says so instead of the world failing to load.
            PlaceDefinition interior = graph.Places.First(place => place.Kind == PlaceKind.Interior);
            Assert.Null(source.For(interior.Id));
        }
        finally
        {
            Directory.Delete(installRoot, recursive: true);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void A_link_whose_event_only_an_inert_face_carries_is_accounted_for_as_unreachable_with_the_face_named()
    {
        // The fixture's interior face carries event 11 with neither the clickable nor the pressure-plate attribute,
        // so the donor raises it neither by a click nor by a step: the link is accounted for as unreachable, with
        // the evidence, rather than turned into a reach the party walks into.
        IndoorMap indoor = Assert.IsType<IndoorMap>(MapDecoder.DecodeIndoor(
            LodFixture.Stored("d01.blv", MapDecoderTests.IndoorPayload()),
            LodFixture.Stored("d01.dlv", MapDecoderTests.IndoorDeltaPayload())));
        OutdoorMap outdoor = Assert.IsType<OutdoorMap>(MapDecoder.DecodeOutdoor(LodFixture.Stored("out01.odm", MapDecoderTests.OutdoorPayload())));
        IReadOnlyList<EvtProgram> programs = [EvtProgram.Read("D01.EVT", SyntheticInstallation.EvtProgram(11, "Out01.odm"))];
        ImportedPlaceGraph graph = ImportedPlaceGraph.Build(
            programs,
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["d01"] = 14, ["out01"] = 1 });
        IReadOnlyDictionary<int, DecodedMap> maps = new Dictionary<int, DecodedMap> { [14] = indoor, [1] = outdoor };
        Assert.Equal(11, indoor.Faces[0].EventId);
        Assert.Equal(0u, indoor.Faces[0].Attributes & (PlaceEntranceEmitter.ClickableAttribute | PlaceEntranceEmitter.PressurePlateAttribute));

        PlaceEntranceSummary summary = PlaceEntranceEmitter.Emit(graph, maps, programs);

        Assert.Empty(summary.Entrances);
        PlaceLinkAccount account = Assert.Single(summary.Accounts);
        Assert.Equal(0, account.LinkIndex);
        Assert.Equal(14, account.FromPlace);
        Assert.Equal(1, account.ToPlace);
        Assert.Equal(PlaceEntranceEmitter.Unreachable, account.Disposition);
        Assert.Contains("1 face(s) carrying it have neither the clickable nor the pressure-plate attribute", account.Detail, StringComparison.Ordinal);
        Assert.Equal(0, summary.TakenCount);
    }

    [Fact]
    public void A_pack_accounts_for_every_travel_link_and_writes_no_reach_nothing_raises()
    {
        string installRoot = SyntheticInstallation.Create(withMaps: true);
        string root = Path.Combine(Path.GetTempPath(), $"mm7-entrances-{Guid.NewGuid():N}");
        try
        {
            string imports = Path.Combine(root, "imports");
            PackWriteResult written = PackWriter.Write(LodInstall.Open(installRoot), imports);

            // The fixture's two links hang their events on numbers no face in its maps raises, so each is accounted
            // for as unreachable with its evidence rather than rounded to a position: nothing in that installation
            // can be used or trodden on to travel, and the pack says so instead of inventing a trigger.
            Assert.Empty(written.Entrances.Entrances);
            Assert.Equal([0, 1], written.Entrances.Accounts.Select(account => account.LinkIndex));
            Assert.All(written.Entrances.Accounts, account => Assert.Equal(PlaceEntranceEmitter.Unreachable, account.Disposition));

            using (JsonDocument document = JsonDocument.Parse(File.ReadAllText(Path.Combine(imports, "mm7-world", "place-entrances.json"))))
            {
                JsonElement root_ = document.RootElement;
                Assert.Equal("place-entrances", root_.GetProperty("documentId").GetString());
                Assert.Equal("place-entrance", root_.GetProperty("definitionKind").GetString());
                Assert.Empty(root_.GetProperty("entries").EnumerateArray());
            }

            // The graph states each link's disposition beside it.
            using (JsonDocument document = JsonDocument.Parse(File.ReadAllText(Path.Combine(imports, "mm7-world", "place-graph.json"))))
            {
                Assert.All(
                    document.RootElement.GetProperty("entries").EnumerateArray(),
                    entry => Assert.Equal(PlaceEntranceEmitter.Unreachable, entry.GetProperty("disposition").GetString()));
            }

            string manifest = File.ReadAllText(Path.Combine(imports, "mm7-world", "pack.json"));
            Assert.Contains("\"path\": \"place-entrances.json\"", manifest);
            Assert.Contains("\"definitionKind\": \"place-entrance\"", manifest);

            // The seam the product reads it through: the packs load as they stand, and the kit's own
            // entrance reader accepts what the writer wrote.
            WriteBundle(root, written.PackIds);
            ContentBootstrapResult bootstrap = ContentBootstrap.Load(new FileContentSource(root), Layout, "imported");
            Assert.True(bootstrap.IsValid, string.Join("; ", bootstrap.Issues.Select(issue => issue.ToString())));
            PlaceGraph graph = PlaceGraphLoader.Load(bootstrap.Catalog);
            Assert.Empty(PlaceEntranceLoader.Load(bootstrap.Catalog, graph));
        }
        finally
        {
            Directory.Delete(installRoot, recursive: true);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void The_decoded_maps_hold_the_chest_records_and_loose_objects_the_container_fixture_states()
    {
        using ContainerImport import = new();

        // What the decoder and the emitter found is the authority the pack is checked against: every
        // chest a face opens becomes one container, and every object the deltas carry becomes one
        // placement, so a change in either side shows up as a disagreement rather than as a count
        // nobody can check.
        IReadOnlyList<DecodedMap> decoded = [.. import.Report.Decoded.Select(outcome => outcome.Decoded).OfType<DecodedMap>()];
        Assert.Equal(76, decoded.Count);
        // Thirteen regions hold the outdoor delta's four records each, and sixty-three interiors hold
        // the container delta's two: the records are the runtime array, and only the ones a face opens
        // become containers.
        Assert.Equal((13 * 4) + (63 * 2), import.Report.Total.Chests);
        Assert.Equal(76, import.Report.Total.SpriteObjects);
    }

    [Fact]
    public void The_containers_a_write_reports_are_the_ones_the_emitter_places_over_the_same_maps()
    {
        using ContainerImport import = new();
        PlaceContainerSummary expected = import.Emitted();
        PackWriteResult written = import.Written;

        // The pack's own counts are what the writer emitted, and the container/object totals the write
        // reports are the ones the document carries.
        Assert.Equal(expected.ContainerCount, written.Containers.ContainerCount);
        Assert.Equal(expected.UnplacedRecords, written.Containers.UnplacedRecords);
        Assert.Equal(63 * 2, written.Containers.ContainerCount);

        // The emission the writer reports and the pack it wrote are one answer: the emitter run over the
        // same decoded maps produces the same containers, at the same positions, with the same contents.
        Assert.Equal(
            expected.Chests.Select(chest => (chest.PlaceId, chest.ChestIndex, chest.X, chest.Y, chest.Z)),
            written.Containers.Chests.Select(chest => (chest.PlaceId, chest.ChestIndex, chest.X, chest.Y, chest.Z)));
    }

    [Fact]
    public void A_place_whose_program_is_missing_is_refused_by_name_and_its_records_stay_unplaced()
    {
        using ContainerImport import = new();
        PackWriteResult written = import.Written;

        // The fixture wires a program for every interior and for two of its thirteen regions, so the
        // eleven regions whose program is not in the installation are refused by name rather than
        // placed from nothing, and every region's four records stay unplaced because no region's
        // program opens one.
        Assert.Equal(11, written.Containers.Refusals.Count);
        Assert.All(written.Containers.Refusals, refusal => Assert.Equal("program-not-found", refusal.Code));
        Assert.Equal(13 * 4, written.Containers.UnplacedRecords);
        // Every interior's delta holds one loose object and every region's holds one too, so the pack
        // carries an object placement for each of the seventy-six places.
        Assert.Equal(76, written.Containers.SpriteObjectCount);
    }

    [Fact]
    public void A_written_container_counts_the_references_and_traps_its_record_holds()
    {
        using ContainerImport import = new();
        PackWriteResult written = import.Written;

        // Two containers are placed in each interior and only the first holds anything: one named item
        // and one random reference, which is what the record stores. The second is a slot whose record
        // is all zeroes, and it is trapped like the first only where the map says so.
        Assert.Equal(63 * 2, written.Containers.ItemReferenceCount);
        Assert.Equal(63, written.Containers.RandomItemReferenceCount);
        Assert.Equal(63, written.Containers.TrappedCount);
    }

    [Fact]
    public void Every_place_counts_its_container_and_object_placements_as_the_write_reports_them()
    {
        using ContainerImport import = new();
        PackWriteResult written = import.Written;

        int containers = 0;
        int sprites = 0;
        foreach (JsonElement entry in import.PlaceEntries())
        {
            JsonElement counts = entry.GetProperty("placementCounts");
            JsonElement placements = entry.GetProperty("placements");
            Assert.Equal(placements.GetArrayLength(), counts.GetProperty("total").GetInt32());
            Assert.Equal(
                placements.GetArrayLength(),
                counts.EnumerateObject().Where(property => property.Name != "total").Sum(property => property.Value.GetInt32()));
            containers += counts.GetProperty("container").GetInt32();
            sprites += counts.GetProperty("sprite").GetInt32();
        }

        Assert.Equal(written.Containers.ContainerCount, containers);
        Assert.Equal(written.Containers.SpriteObjectCount, sprites);
    }

    [Fact]
    public void A_container_placement_states_its_sources_its_traps_and_what_its_record_holds()
    {
        using ContainerImport import = new();
        IReadOnlyList<JsonElement> containers = import.PlacementsOfKind("container");
        Assert.Equal(import.Written.Containers.ContainerCount, containers.Count);

        foreach (JsonElement placement in containers)
        {
            // A container says where its position came from, what its own record's flags are,
            // what the place's traps are, and what it holds — references included, exactly as
            // the map recorded them.
            Assert.Equal("event-face-centroid", placement.GetProperty("positionSource").GetString());
            Assert.Equal("chest-record", placement.GetProperty("contentsSource").GetString());
            Assert.Equal("chests", placement.GetProperty("sourceField").GetString());
            Assert.True(placement.GetProperty("trapDifficulty").GetInt32() > 0);
            Assert.True(placement.GetProperty("trapDamageDice").GetInt32() > 0);

            JsonElement[] contents = [.. placement.GetProperty("contents").EnumerateArray()];
            if (placement.GetProperty("sourceIndex").GetInt32() == 0)
            {
                Assert.Equal(1, placement.GetProperty("flags").GetInt32());
                Assert.Equal(2, contents.Length);
                Assert.Equal(0, contents[0].GetProperty("slot").GetInt32());
                Assert.Equal(220, contents[0].GetProperty("item").GetInt32());
                Assert.Equal(3, contents[1].GetProperty("slot").GetInt32());
                Assert.Equal(-3, contents[1].GetProperty("item").GetInt32());
            }
            else
            {
                // The delta's spare record, which the map's second face opens: a real
                // container with nothing in it and no trap of its own.
                Assert.Equal(0, placement.GetProperty("flags").GetInt32());
                Assert.Empty(contents);
            }
        }
    }

    [Fact]
    public void An_object_placement_states_what_it_holds_and_only_the_interiors_objects_hold_anything()
    {
        using ContainerImport import = new();

        IReadOnlyList<JsonElement> sprites = import.PlacementsOfKind("sprite");
        Assert.Equal(import.Written.Containers.SpriteObjectCount, sprites.Count);

        int stockedSprites = 0;
        foreach (JsonElement placement in sprites)
        {
            // An object says what it is and what it holds, which for a region's fixture
            // object is nothing at all: the placement is emitted whether or not the object
            // is something a party can reach for.
            Assert.Equal("spriteObjects", placement.GetProperty("sourceField").GetString());
            Assert.Equal("containing-item", placement.GetProperty("contentsSource").GetString());
            if (placement.GetProperty("containingItem").GetInt32() != 0) stockedSprites++;
        }

        // Sixty-three interiors hold the item their delta's object carries, and the regions' objects
        // hold nothing, which is the same distinction the summary states.
        Assert.Equal(63, stockedSprites);
        Assert.Equal(import.Written.Containers.StockedSpriteObjectCount, stockedSprites);
    }

    [Fact]
    public void The_products_reader_finds_an_interiors_containers_among_its_placements()
    {
        using ContainerImport import = new();

        // The product's own reader agrees with the document, and the containers are targets it
        // discovers: a place that holds containers declares them among its placements like anything else.
        WriteBundle(import.Root, ["mm7-tables", "mm7-world"]);
        ContentBootstrapResult bootstrap = ContentBootstrap.Load(new FileContentSource(import.Root), Layout, "imported");
        Assert.True(bootstrap.IsValid, string.Join("; ", bootstrap.Issues.Select(issue => issue.ToString())));
        PlaceGraph graph = PlaceGraphLoader.Load(bootstrap.Catalog);
        PlacePopulationContent content = PlacePopulationContent.Read(graph);
        PlaceDefinition interior = graph.Places.First(place => place.Kind == PlaceKind.Interior);
        Assert.Equal(5, content.PlacementsOf(interior.Id).Count);
        Assert.Equal(2, content.PlacementsOf(interior.Id).Count(placement => placement.Content.Kind == "container-surface"));
        Assert.Equal(2, content.PlacementsOf(interior.Id).Count(placement => placement.Content.Kind == "container"));
        Assert.Equal(1, content.PlacementsOf(interior.Id).Count(placement => placement.Content.Kind == "sprite"));
        Assert.Equal(0, content.PlacementsOf(interior.Id).Count(placement => placement.Content.Kind == "door"));
    }

    /// <summary>
    /// The container fixture imported once: the installation, the packs written from it, and the decoder's
    /// own report over the same maps, which the container tests each check a part of.
    /// </summary>
    private sealed class ContainerImport : IDisposable
    {
        private readonly string _installRoot;

        internal ContainerImport()
        {
            _installRoot = SyntheticInstallation.Create(withMaps: true, withContainers: true);
            Root = Path.Combine(Path.GetTempPath(), $"mm7-containers-{Guid.NewGuid():N}");
            Imports = Path.Combine(Root, "imports");
            Written = PackWriter.Write(LodInstall.Open(_installRoot), Imports);
            Report = MapDecoder.DecodeAll(LodInstall.Open(_installRoot));
        }

        /// <summary>The content root a bundle over the written packs is placed under.</summary>
        internal string Root { get; }

        /// <summary>Where the packs were written.</summary>
        internal string Imports { get; }

        /// <summary>What the write reported.</summary>
        internal PackWriteResult Written { get; }

        /// <summary>The decoder's own reading of the same installation.</summary>
        internal MapDecodeReport Report { get; }

        /// <summary>The container emitter run directly over the decoded maps, independently of the writer.</summary>
        internal PlaceContainerSummary Emitted()
        {
            LodInstall install = LodInstall.Open(_installRoot);
            Dictionary<int, DecodedMap> byPlace = [];
            foreach (MapDecodeOutcome outcome in Report.Decoded)
            {
                if (outcome.Decoded is not null) byPlace[outcome.Map.Id] = outcome.Decoded;
            }

            return PlaceContainerEmitter.Emit(
                byPlace,
                EvtProgram.ReadAll(install),
                PlaceMapNumbersTable.Read(Mm7Tables.Read(install)));
        }

        /// <summary>Every place entry of the written places document.</summary>
        internal IReadOnlyList<JsonElement> PlaceEntries()
        {
            using JsonDocument places = JsonDocument.Parse(File.ReadAllText(Path.Combine(Imports, "mm7-tables", "places.json")));
            return [.. places.RootElement.GetProperty("entries").EnumerateArray().Select(entry => entry.Clone())];
        }

        /// <summary>Every placement of one kind across the written places document.</summary>
        internal IReadOnlyList<JsonElement> PlacementsOfKind(string kind) =>
            [.. PlaceEntries()
                .SelectMany(entry => entry.GetProperty("placements").EnumerateArray())
                .Where(placement => string.Equals(placement.GetProperty("kind").GetString(), kind, StringComparison.Ordinal))];

        public void Dispose()
        {
            Directory.Delete(_installRoot, recursive: true);
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }

    /// <summary>Reads a payload as the decoder is handed one, without a container around it.</summary>

    private static void WriteBundle(string root, IReadOnlyList<string> packIds)    {
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
