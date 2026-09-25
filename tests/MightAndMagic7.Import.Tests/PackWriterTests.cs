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
        // derived position — or on a container's, which is averaged over the faces that open it — would be
        // exactly the difference a pack must never carry.
        string installRoot = SyntheticInstallation.Create(withMaps: true, withContainers: true);
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
            // records, and neither is allowed to quietly lose it. The region's delta also carries one
            // sprite object, which is a placement of its own whether or not it holds anything.
            PlacePopulationContent placements = PlacePopulationContent.Read(graph);
            IReadOnlyList<PlacementDefinition> regionPlacements = placements.PlacementsOf(region.Id);
            Assert.Equal(5, regionPlacements.Count);
            Assert.Equal(3, regionPlacements.Count(placement => placement.Content.Kind == "decoration"));
            Assert.Equal(1, regionPlacements.Count(placement => placement.Content.Kind == "spawn"));
            Assert.Equal(1, regionPlacements.Count(placement => placement.Content.Kind == "sprite"));
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
            ContentPlaceGeometry source = new(bootstrap.Catalog, "place-geometry", "artifact");

            PlaceDefinition region = graph.Places.First(place => place.Kind == PlaceKind.Region);
            PlaceGeometry? collision = source.For(region.Id);
            Assert.NotNull(collision);
            Assert.Equal($"mm7-world/place-geometry/{region.Id.Value}.json", collision.Path);
            Assert.True(collision.Artifact.Length > 0);

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
    public void Walking_into_a_places_transition_comes_from_the_event_face_the_map_carries()
    {
        // The fixture's interior face raises event 11, so a program whose move belongs to event 11 is a
        // road that face is the trigger for. The reach the emitter derives is the face's own geometry,
        // which is what the assertions recompute independently rather than take on trust.
        IndoorMap indoor = Assert.IsType<IndoorMap>(MapDecoder.DecodeIndoor(
            Payload("d01.blv", MapDecoderTests.IndoorPayload()),
            Payload("d01.dlv", MapDecoderTests.IndoorDeltaPayload())));
        OutdoorMap outdoor = Assert.IsType<OutdoorMap>(MapDecoder.DecodeOutdoor(Payload("out01.odm", MapDecoderTests.OutdoorPayload())));
        ImportedPlaceGraph graph = ImportedPlaceGraph.Build(
            [EvtProgram.Read("D01.EVT", SyntheticInstallation.EvtProgram(11, "Out01.odm"))],
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["d01"] = 14, ["out01"] = 1 });
        IReadOnlyDictionary<int, DecodedMap> maps = new Dictionary<int, DecodedMap> { [14] = indoor, [1] = outdoor };

        PlaceEntranceSummary summary = PlaceEntranceEmitter.Emit(graph, maps);

        PlaceEntrancePlacement entrance = Assert.Single(summary.Entrances);

        // The entrance takes the one link the program declares, stands in the map the program belongs to,
        // and reaches the whole face: an interior has no models, so the face index is the level's own.
        Assert.Equal(0, entrance.LinkIndex);
        Assert.Equal(14, entrance.FromPlace);
        Assert.Equal(1, entrance.ToPlace);
        Assert.Equal(11, entrance.EventId);
        Assert.Equal(PlaceEntranceKind.Entrance, entrance.Kind);
        Assert.Equal(0, entrance.SourceFaceIndex);
        Assert.Equal(-1, entrance.SourceModelIndex);
        Assert.Equal(string.Empty, entrance.SourceModelName);
        Assert.Equal(indoor.Faces[0].Attributes, entrance.Attributes);

        // The position and the radius are the trigger face's own: its corners' mean, and the farthest
        // corner from it. Nothing here is read from the destination the link names.
        MapFace face = indoor.Faces[0];
        Assert.Equal(face.Vertices.Average(vertex => (double)vertex.X), entrance.X, 6);
        Assert.Equal(face.Vertices.Average(vertex => (double)vertex.Y), entrance.Y, 6);
        Assert.Equal(face.Vertices.Average(vertex => (double)vertex.Z), entrance.Z, 6);
        Assert.Equal(
            face.Vertices.Max(vertex => Math.Sqrt(Math.Pow(vertex.X - entrance.X, 2) + Math.Pow(vertex.Y - entrance.Y, 2) + Math.Pow(vertex.Z - entrance.Z, 2))),
            entrance.Radius,
            6);

        // Every link was either reachable or refused by name, never dropped in silence.
        Assert.Empty(summary.Refusals);
        Assert.Equal(1, summary.LinkCount);
        Assert.Equal(1, summary.ReachCount);
    }

    [Fact]
    public void A_pack_says_where_a_transition_can_be_walked_into_and_which_links_nothing_can()
    {
        string installRoot = SyntheticInstallation.Create(withMaps: true);
        string root = Path.Combine(Path.GetTempPath(), $"mm7-entrances-{Guid.NewGuid():N}");
        try
        {
            string imports = Path.Combine(root, "imports");
            PackWriteResult written = PackWriter.Write(LodInstall.Open(installRoot), imports);

            // The fixture's two links hang their events on numbers no face in its maps raises, so they are
            // reported per link rather than rounded to a position: nothing in that installation can be
            // walked into a transition, and the pack says so instead of inventing a trigger.
            Assert.Empty(written.Entrances.Entrances);
            Assert.Equal(2, written.Entrances.UntriggerableCount);
            Assert.All(written.Entrances.Refusals, refusal => Assert.Equal("no-event-face", refusal.Code));
            Assert.Equal([0, 1], written.Entrances.Refusals.Select(refusal => refusal.LinkIndex));

            using (JsonDocument document = JsonDocument.Parse(File.ReadAllText(Path.Combine(imports, "mm7-world", "place-entrances.json"))))
            {
                JsonElement root_ = document.RootElement;
                Assert.Equal("place-entrances", root_.GetProperty("documentId").GetString());
                Assert.Equal("place-entrance", root_.GetProperty("definitionKind").GetString());
                Assert.Empty(root_.GetProperty("entries").EnumerateArray());
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
    public void Places_carry_the_containers_and_loose_objects_the_maps_hold_with_counts_that_match_them()
    {
        string installRoot = SyntheticInstallation.Create(withMaps: true, withContainers: true);
        string root = Path.Combine(Path.GetTempPath(), $"mm7-containers-{Guid.NewGuid():N}");
        try
        {
            string imports = Path.Combine(root, "imports");
            PackWriteResult written = PackWriter.Write(LodInstall.Open(installRoot), imports);

            // What the decoder and the emitter found is the authority the pack is checked against: every
            // chest a face opens becomes one container, and every object the deltas carry becomes one
            // placement, so a change in either side shows up as a disagreement rather than as a count
            // nobody can check.
            MapDecodeReport report = MapDecoder.DecodeAll(LodInstall.Open(installRoot));
            IReadOnlyList<DecodedMap> decoded = [.. report.Decoded.Select(outcome => outcome.Decoded).OfType<DecodedMap>()];
            Assert.Equal(76, decoded.Count);
            // Thirteen regions hold the outdoor delta's four records each, and sixty-three interiors hold
            // the container delta's two: the records are the runtime array, and only the ones a face opens
            // become containers.
            Assert.Equal((13 * 4) + (63 * 2), report.Total.Chests);
            Assert.Equal(76, report.Total.SpriteObjects);

            LodInstall install = LodInstall.Open(installRoot);
            Dictionary<int, DecodedMap> byPlace = [];
            foreach (MapDecodeOutcome outcome in report.Decoded)
            {
                if (outcome.Decoded is not null) byPlace[outcome.Map.Id] = outcome.Decoded;
            }

            PlaceContainerSummary expected = PlaceContainerEmitter.Emit(
                byPlace,
                EvtProgram.ReadAll(install),
                PlaceTrapNumbersTable.Read(Mm7Tables.Read(install)));

            // The pack's own counts are what the writer emitted, and the container/object totals the write
            // reports are the ones the document carries.
            Assert.Equal(expected.ContainerCount, written.Containers.ContainerCount);
            Assert.Equal(expected.UnplacedRecords, written.Containers.UnplacedRecords);
            Assert.Equal(63 * 2, written.Containers.ContainerCount);

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

            // Two containers are placed in each interior and only the first holds anything: one named item
            // and one random reference, which is what the record stores. The second is a slot whose record
            // is all zeroes, and it is trapped like the first only where the map says so.
            Assert.Equal(63 * 2, written.Containers.ItemReferenceCount);
            Assert.Equal(63, written.Containers.RandomItemReferenceCount);
            Assert.Equal(63, written.Containers.TrappedCount);

            int containers = 0;
            int sprites = 0;
            int stockedSprites = 0;
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
                    containers += counts.GetProperty("container").GetInt32();
                    sprites += counts.GetProperty("sprite").GetInt32();

                    foreach (JsonElement placement in placements.EnumerateArray())
                    {
                        string kind = placement.GetProperty("kind").GetString()!;
                        if (string.Equals(kind, "container", StringComparison.Ordinal))
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
                        else if (string.Equals(kind, "sprite", StringComparison.Ordinal))
                        {
                            // An object says what it is and what it holds, which for a region's fixture
                            // object is nothing at all: the placement is emitted whether or not the object
                            // is something a party can reach for.
                            Assert.Equal("spriteObjects", placement.GetProperty("sourceField").GetString());
                            Assert.Equal("containing-item", placement.GetProperty("contentsSource").GetString());
                            if (placement.GetProperty("containingItem").GetInt32() != 0) stockedSprites++;
                        }
                    }
                }
            }

            Assert.Equal(written.Containers.ContainerCount, containers);
            Assert.Equal(written.Containers.SpriteObjectCount, sprites);

            // Sixty-three interiors hold the item their delta's object carries, and the regions' objects
            // hold nothing, which is the same distinction the summary states.
            Assert.Equal(63, stockedSprites);
            Assert.Equal(written.Containers.StockedSpriteObjectCount, stockedSprites);

            // The product's own reader agrees with the document, and the containers are targets it
            // discovers: a place that holds containers declares them among its placements like anything else.
            WriteBundle(root, ["mm7-tables", "mm7-world"]);
            ContentBootstrapResult bootstrap = ContentBootstrap.Load(new FileContentSource(root), Layout, "imported");
            Assert.True(bootstrap.IsValid, string.Join("; ", bootstrap.Issues.Select(issue => issue.ToString())));
            PlaceGraph graph = PlaceGraphLoader.Load(bootstrap.Catalog);
            PlacePopulationContent content = PlacePopulationContent.Read(graph);
            PlaceDefinition interior = graph.Places.First(place => place.Kind == PlaceKind.Interior);
            Assert.Equal(3, content.PlacementsOf(interior.Id).Count);
            Assert.Equal(2, content.PlacementsOf(interior.Id).Count(placement => placement.Content.Kind == "container"));
            Assert.Equal(1, content.PlacementsOf(interior.Id).Count(placement => placement.Content.Kind == "sprite"));
            Assert.Equal(0, content.PlacementsOf(interior.Id).Count(placement => placement.Content.Kind == "door"));

            // The emission the writer reports and the pack it wrote are one answer: the emitter run over the
            // same decoded maps produces the same containers, at the same positions, with the same contents.
            Assert.Equal(
                expected.Chests.Select(chest => (chest.PlaceId, chest.ChestIndex, chest.X, chest.Y, chest.Z)),
                written.Containers.Chests.Select(chest => (chest.PlaceId, chest.ChestIndex, chest.X, chest.Y, chest.Z)));
        }
        finally
        {
            Directory.Delete(installRoot, recursive: true);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Reads a payload as the decoder is handed one, without a container around it.</summary>
    private static LodPayload Payload(string entryName, byte[] bytes) =>
        new(new LodEntry(entryName, 0, bytes.Length), bytes, LodPayloadKind.Verbatim);

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
