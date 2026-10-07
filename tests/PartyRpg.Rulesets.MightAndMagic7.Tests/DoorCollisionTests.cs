using System.Numerics;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Movement;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.World;
using Rusty.Engine.Testing;
using Xunit;

namespace PartyRpg.Rulesets.MightAndMagic7.Tests;

public sealed class DoorCollisionTests
{
    [ImportedFact("place-events.json")]
    public void Conditional_chest_surfaces_own_their_collision_faces_without_exposing_the_stored_records()
    {
        var catalog = ImportedContent.Load().RequireValid();
        var graph = MightAndMagic7World.Graph(catalog);
        var placements = PlacePopulationContent.Read(graph);
        var geometry = new MightAndMagic7Geometry(catalog, graph, new InteractionLedger());
        foreach (var place in new[] { new PlaceId("31"), new PlaceId("51") })
        {
            var surface = placements.PlacementsOf(place).Single(p => p.Content.Kind == "container-surface" && p.Source.GetInt32("eventId") == 176);
            var targets = geometry.For(place)!.Collision!.Parts.SelectMany(part => part.Targets).ToArray();
            Assert.Contains(surface.Content, targets);
            Assert.DoesNotContain(targets, target => target.Kind == "container");
        }
    }

    [ImportedFact("classes.json")]
    public void An_imported_closed_door_fixture_owns_its_visible_surface_while_ordinary_sight_stays_blocked()
    {
        ContentCatalog catalog = ImportedContent.Load().RequireValid();
        PlaceGraph graph = MightAndMagic7World.Graph(catalog);
        PlaceId place = new("17");
        InteractionLedger ledger = new();
        MightAndMagic7Geometry source = new(catalog, graph, ledger);
        PlacementDefinition fixture = PlacePopulationContent.Read(graph).PlacementsOf(place).Single(p => p.Content.Id == "fixture-3");
        using EngineTestHost host = EngineTestHost.Create();
        host.Call(engine =>
        {
            PlacePose start = new(-832, -128, 2, 0, 0);
            PartyPoseOwner pose = new(new PartyPose(place, start), MightAndMagic7Movement.Facing);
            PartyMovement movement = new(engine.Spatial, pose, MightAndMagic7Movement.Space,
                MightAndMagic7Movement.Session, MightAndMagic7Movement.Tuning(engine.Spatial));
            using EnginePartyMover mover = new(engine.Spatial, movement, engine.Content, MightAndMagic7Movement.Navigation, source);
            mover.Enter(place);
            Vector3 from = MightAndMagic7Movement.Space.Position(start), to = MightAndMagic7Movement.Space.Position(fixture.Pose);
            Assert.False(mover.InSight(from, to));
            Assert.True(mover.InSight(from, to, fixture.Content));
            Assert.False(mover.InSight(from, to, new("fixture", "fixture-501")));
            for (int index = 0; index < 120; index++) mover.Step(new MovementIntent(1, 0), 1d / 60);
            Assert.InRange(pose.PlacePose.X, -700, -690);
        });
    }

    [ImportedFact("classes.json")]
    public void Every_imported_partition_admits_at_its_initial_state_and_with_all_doors_open()
    {
        ContentCatalog catalog = ImportedContent.Load().RequireValid();
        PlaceGraph graph = MightAndMagic7World.Graph(catalog);
        PlacePopulationContent placements = PlacePopulationContent.Read(graph);
        InteractionLedger ledger = new();
        MightAndMagic7Geometry source = new(catalog, graph, ledger);
        List<object> report = [];
        using EngineTestHost host = EngineTestHost.Create();
        host.Call(engine =>
        {
            PartyPoseOwner pose = new(new PartyPose(new PlaceId("1"), PlacePose.Origin), MightAndMagic7Movement.Facing);
            PartyMovement movement = new(engine.Spatial, pose, MightAndMagic7Movement.Space,
                MightAndMagic7Movement.Session, MightAndMagic7Movement.Tuning(engine.Spatial));
            using EnginePartyMover mover = new(engine.Spatial, movement, engine.Content, MightAndMagic7Movement.Navigation, source);
            foreach (var (_, _, entry) in catalog.Entries(MightAndMagic7World.GeometryDefinitionKind))
            {
                PlaceId place = new(entry.Id);
                var initial = mover.Enter(place);
                Assert.True(initial.Admitted);
                Assert.True(initial.CollisionTriangles > 0);
                var doors = placements.PlacementsOf(place).Where(placement => placement.Content.Kind == "door").ToArray();
                foreach (var door in doors) ledger.Record(place, door.Content, "open");
                var opened = mover.Enter(place);
                Assert.True(opened.Admitted);
                Assert.True(opened.CollisionTriangles > 0);
                report.Add(new { place = entry.Id, doors = doors.Length, initialCells = initial.NavigationCells,
                    openedCells = opened.NavigationCells, initial.NavigationReason, openReason = opened.NavigationReason });
            }
        });
        Assert.Equal(76, report.Count);
        if (Environment.GetEnvironmentVariable("CRAWLER_DOOR_REPORT") is { Length: > 0 } path)
            File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }

    [Fact]
    public void Canonical_door_and_face_group_changes_update_the_same_native_scene_and_restore_from_the_ledger()
    {
        ContentCatalog catalog = Catalog();
        PlaceGraph graph = MightAndMagic7World.Graph(catalog);
        InteractionLedger ledger = new();
        MightAndMagic7Geometry source = new(catalog, graph, ledger);
        PlaceId place = new("1");
        PlacementContentId door = new("door", "gate");
        using EngineTestHost host = EngineTestHost.Create();
        host.Call(engine =>
        {
            PartyPoseOwner pose = new(new PartyPose(place, new PlacePose(256, -256, 2, 0, 0)), MightAndMagic7Movement.Facing);
            PartyMovement movement = new(engine.Spatial, pose, MightAndMagic7Movement.Space,
                MightAndMagic7Movement.Session, MightAndMagic7Movement.Tuning(engine.Spatial));
            EnginePartyMover mover = new(engine.Spatial, movement, engine.Content, MightAndMagic7Movement.Navigation, source);
            using SessionWorld world = new(graph, pose, new PlaceStateLedger(graph, PlaceRespawnRule.FromContent()),
                new MightAndMagic7TravelCostRule(null), mover: mover, interactionState: ledger);
            Vector3 left = new(256, 96, 256), right = new(1280, 96, 256);
            Assert.False(mover.InSight(left, right));
            Assert.True(mover.InSight(left, new Vector3(768, 96, 256), door));
            Assert.True(mover.InSight(left, new Vector3(768, 96, 256), new("fixture", "lever")));
            Assert.False(mover.InSight(left, right, new("container", "behind-gate")));
            // The base floor is not the door's surface, even if the requested target owns another mesh.
            Assert.False(mover.InSight(new(256, 96, 256), new(256, -96, 256), door));
            for (int index = 0; index < 180; index++) mover.Step(new MovementIntent(1, 0), 1d / 60);
            Assert.InRange(pose.PlacePose.X, 256, 768);
            ledger.Record(place, door, "open");
            Assert.True(mover.InSight(left, right));
            Assert.True(mover.Current!.NavigationCells > 0);
            for (int index = 0; index < 90; index++) mover.Step(new MovementIntent(1, 0), 1d / 60);
            Assert.True(pose.PlacePose.X > 800, $"An opened doorway still blocked: {pose.PlacePose}.");
            ledger.Record(place, door, "closed");
            Assert.False(mover.InSight(left, right));
            for (int index = 0; index < 180; index++) mover.Step(new MovementIntent(-1, 0), 1d / 60);
            Assert.True(pose.PlacePose.X > 768, $"A closed doorway let the party back through: {pose.PlacePose}.");
            ledger.Keep(place, new Dictionary<string, long> { [MightAndMagic7Geometry.PassableKey(7)] = 1 });
            Assert.True(mover.InSight(left, right));
            var captured = ledger.Capture();
            MightAndMagic7Geometry resumed = new(catalog, graph, new InteractionLedger(captured));
            Assert.Equal(2, resumed.For(place)!.Collision!.Triangles.Length);
            ledger.Keep(place, new Dictionary<string, long> { [MightAndMagic7Geometry.PassableKey(7)] = 0 });
            Assert.False(mover.InSight(left, right));
            ledger.Record(place, door, "open");
            ledger.Forget(place);
            Assert.False(mover.InSight(left, right));
        });
    }

    [Fact]
    public void Each_corner_moves_only_with_its_own_door_and_unrelated_uses_do_not_rebuild_geometry()
    {
        ContentCatalog catalog = Catalog(partial: true);
        PlaceGraph graph = MightAndMagic7World.Graph(catalog);
        InteractionLedger ledger = new();
        MightAndMagic7Geometry source = new(catalog, graph, ledger);
        PlaceId place = new("1");
        var closed = source.For(place)!;
        ledger.Record(place, new("container", "chest"), "empty");
        Assert.Same(closed, source.For(place));
        ledger.Record(place, new("door", "gate"), "open");
        var open = source.For(place)!;
        Assert.NotSame(closed, open);
        Assert.Equal(closed.Collision!.Positions.Span[0], open.Collision!.Positions.Span[0]);
        Assert.Equal(closed.Collision.Positions.Span[4], open.Collision.Positions.Span[4]);
        Assert.Equal(closed.Collision.Positions.Span[5] + new Vector3(0, 512, 0), open.Collision.Positions.Span[5]);
        var restore = new InteractionLedger(ledger.Capture());
        Assert.Equal(open.Collision.Positions.ToArray(), new MightAndMagic7Geometry(catalog, graph, restore).For(place)!.Collision!.Positions.ToArray());
    }

    [Fact]
    public void A_fixture_cannot_claim_another_cluster_with_the_same_event_as_its_own_visible_surface()
    {
        ContentCatalog catalog = Catalog(clusters: true);
        PlaceGraph graph = MightAndMagic7World.Graph(catalog);
        PlaceId place = new("1");
        MightAndMagic7Geometry source = new(catalog, graph, new InteractionLedger());
        using EngineTestHost host = EngineTestHost.Create();
        host.Call(engine =>
        {
            PartyPoseOwner pose = new(new PartyPose(place, new PlacePose(256, -256, 2, 0, 0)), MightAndMagic7Movement.Facing);
            PartyMovement movement = new(engine.Spatial, pose, MightAndMagic7Movement.Space,
                MightAndMagic7Movement.Session, MightAndMagic7Movement.Tuning(engine.Spatial));
            using EnginePartyMover mover = new(engine.Spatial, movement, engine.Content, MightAndMagic7Movement.Navigation, source);
            mover.Enter(place);
            Vector3 eye = new(256, 96, 256);
            Assert.True(mover.InSight(eye, new(768, 96, 256), new("fixture", "lever")));
            Assert.False(mover.InSight(eye, new(1024, 96, 256), new("fixture", "lever-1")));
            Assert.True(mover.InSight(new(900, 96, 256), new(1024, 96, 256), new("fixture", "lever-1")));
        });
    }

    private static ContentCatalog Catalog(bool partial = false, bool clusters = false) => ContentCatalogLoader.Load(new InMemoryContentSource()
        .Add("packs/world/pack.json", """
            {"schemaVersion":1,"packId":"world","kind":"definitions","provenance":{"description":"authored door geometry"},
             "documents":[{"path":"places.json","documentId":"places","definitionKind":"place"},
                          {"path":"geometry.json","documentId":"geometry","definitionKind":"place-geometry"}]}
            """)
        .Add("packs/world/places.json", """
            {"documentId":"places","definitionKind":"place","entries":[{"id":"1","kind":"interior","name":"Hall",
             "entryPoints":[],"placements":[{"id":"gate","kind":"door","doorId":1,"state":2,"x":768,"y":-256,"z":0},
             {"id":"lever","kind":"fixture","eventId":7,"x":768,"y":-256,"z":96}]}]}
            """.Replace("\"placements\":[", clusters ? "\"placements\":[{\"id\":\"lever-1\",\"kind\":\"fixture\",\"eventId\":7,\"x\":1024,\"y\":-256,\"z\":96}," : "\"placements\":[", StringComparison.Ordinal))
        .Add("packs/world/geometry.json", Geometry(partial).Replace("\"faces\":[", clusters ? """
            "faces":[{"group":7,"event":7,"fixture":"lever-1","passable":false,"corners":[
             {"rest":[1024,0,0],"travel":[0,0,0]},{"rest":[1024,512,0],"travel":[0,0,0]},
             {"rest":[1024,512,1536],"travel":[0,0,0]},{"rest":[1024,0,1536],"travel":[0,0,0]}],
             "triangles":[[0,1,2],[0,2,3],[2,1,0],[3,2,0]]},
            """ : "\"faces\":[", StringComparison.Ordinal)), new ContentLayout("packs", "imports", "bundles")).RequireValid();

    private static string Geometry(bool partial) => """
            {"documentId":"geometry","definitionKind":"place-geometry","entries":[{"id":"1",
             "artifact":{},"navigationRegion":{"minimum":[0,0,0],"maximum":[1536,1024,1536],"cellSize":128},
             "collisionLayout":{"positions":[[0,0,0],[0,0,1536],[1536,0,1536],[1536,0,0]],"triangles":[[0,1,2],[0,2,3]],
               "faces":[{"group":7,"event":7,"fixture":"lever","passable":false,"corners":[
                 {"rest":[768,512,0],"door":"gate","travel":[0,-512,0]},
                 {"rest":[768,1024,0],"door":"gate","travel":[0,-512,0]},
                 {"rest":[768,1024,1536],"door":"gate","travel":[0,-512,0]},
                 {"rest":[768,512,1536],"door":"gate","travel":[0,-512,0]}],
                 "triangles":[[0,1,2],[0,2,3],[2,1,0],[3,2,0]]}]}}]}
            """.Replace("\"rest\":[768,512,0],\"door\":\"gate\",\"travel\":[0,-512,0]",
            partial ? "\"rest\":[768,512,0],\"travel\":[0,0,0]" : "\"rest\":[768,512,0],\"door\":\"gate\",\"travel\":[0,-512,0]", StringComparison.Ordinal);
}
