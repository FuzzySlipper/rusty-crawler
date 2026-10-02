using System.Numerics;
using System.Text;
using System.Diagnostics;
using System.Text.Json;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Movement;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.World;
using PartyRpg.Rulesets.MightAndMagic7;
using Rusty.Engine;
using Rusty.Engine.Entities;
using Rusty.Engine.Testing;
using Xunit;

namespace PartyRpg.Rulesets.MightAndMagic7.Tests;

/// <summary>The real Engine derives a route for the game's actual body and the canonical movers use it.</summary>
public sealed class NavigationAdmissionTests
{
    [ImportedFact("classes.json")]
    public void Every_imported_geometry_publishes_navigation_or_names_why_collision_alone_remains()
    {
        ContentCatalog catalog = ImportedContent.Load().RequireValid();
        ContentPlaceGeometry source = new(catalog, MightAndMagic7World.GeometryDefinitionKind,
            MightAndMagic7World.GeometryArtifactProperty, MightAndMagic7World.GeometrySurfacesProperty,
            MightAndMagic7World.GeometryNavigationProperty);
        List<object> report = [];
        using EngineTestHost host = EngineTestHost.Create();
        host.Call(engine =>
        {
            PartyPoseOwner pose = new(new PartyPose(new PlaceId("probe"), PlacePose.Origin), MightAndMagic7Movement.Facing);
            PartyMovement movement = new(engine.Spatial, pose, MightAndMagic7Movement.Space,
                MightAndMagic7Movement.Session, MightAndMagic7Movement.Tuning(engine.Spatial));
            using EnginePartyMover mover = new(engine.Spatial, movement, engine.Content, MightAndMagic7Movement.Navigation, source);
            foreach ((_, _, ContentEntry entry) in catalog.Entries(MightAndMagic7World.GeometryDefinitionKind))
            {
                Stopwatch cost = Stopwatch.StartNew();
                PlaceGeometryAdmission admitted = mover.Enter(new PlaceId(entry.Id));
                Assert.True(admitted.Admitted);
                Assert.True(admitted.CollisionTriangles > 0);
                Assert.True(admitted.NavigationCells > 0 || !string.IsNullOrEmpty(admitted.NavigationReason));
                report.Add(new { place = entry.Id, admitted.NavigationCells, admitted.NavigationReason, milliseconds = cost.Elapsed.TotalMilliseconds });
            }
        });
        Assert.NotEmpty(report);
        if (Environment.GetEnvironmentVariable("CRAWLER_NAVIGATION_REPORT") is { Length: > 0 } path)
            File.WriteAllText(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    }

    [Fact]
    public void The_actual_body_reaches_around_a_wall_and_the_party_keeps_the_same_collision()
    {
        using EngineTestHost host = EngineTestHost.Create();
        host.Call(engine =>
        {
            PlaceId place = new("navigation-test");
            PartyPoseOwner pose = new(new PartyPose(place, new PlacePose(256, -256, 2, 0, 0)), MightAndMagic7Movement.Facing);
            MovementTuning tuning = MightAndMagic7Movement.Tuning(engine.Spatial);
            PartyMovement movement = new(engine.Spatial, pose, MightAndMagic7Movement.Space, MightAndMagic7Movement.Session, tuning);
            using EnginePartyMover mover = new(engine.Spatial, movement, engine.Content, MightAndMagic7Movement.Navigation, new Geometry());
            PlaceGeometryAdmission admitted = mover.Enter(place);
            Assert.True(admitted.NavigationCells > 0, admitted.NavigationReason);
            Assert.Equal(6ul, admitted.CollisionTriangles);
            using EngineCreatureMotion creatures = new(engine.Spatial, mover, MightAndMagic7Movement.Space, tuning.Controller, 1000);
            PlacePose from = pose.PlacePose;
            PlacePose target = new(1280, -256, 2, 0, 0);
            CombatantId creature = CombatantId.Of(new EntityId(11));
            CombatantId party = CombatantId.Of(new EntityId(12));
            bool wentAround = false;
            for (int i = 0; i < 900; i++)
            {
                CreatureMoveOutcome result = creatures.Move(new CreatureMoveRequest(creature, from, party, target, CreatureMovePurpose.Toward, 300, 1.0 / 60));
                Assert.False(result.IsHeld, result.Refusal?.Message);
                from = result.Pose;
                wentAround |= from.Y < -1024;
                if (Math.Abs(from.X - target.X) < 100 && Math.Abs(from.Y - target.Y) < 100) break;
            }
            Assert.True(wentAround, $"The creature never cleared the wall end: {from}.");
            Assert.InRange(Math.Abs(from.X - target.X), 0, 100);
            Assert.InRange(Math.Abs(from.Y - target.Y), 0, 100);
            mover.Step(new MovementIntent(forward: 1, strafe: 0), 1.0 / 60);
            Assert.True(double.IsFinite(pose.PlacePose.Z));
        });
    }

    private sealed class Geometry : IPlaceGeometrySource
    {
        public PlaceGeometry For(PlaceId place) => new("test/wall.json", Encoding.UTF8.GetBytes("""
            {"schemaVersion":1,"staticMeshArtifactId":"test-wall","bounds":{"min":[0,0,0],"max":[1536,512,1536]},
             "collision":{"positions":[[0,0,0],[0,0,1536],[1536,0,1536],[1536,0,0],[768,0,0],[768,512,0],[768,512,1024],[768,0,1024]],
                          "triangles":[[0,1,2],[0,2,3],[4,5,6],[4,6,7],[6,5,4],[7,6,4]]},
             "navigation":{"id":"test-nav","config":{"schemaVersion":1,"cellSize":128,"levelQuantum":16,"maximumSlopeDegrees":50,"requiredHeadroom":192,"supportProbeDrop":16},"cells":[]}}
            """), navigation: new PlaceNavigationRegion(Vector3.Zero, new Vector3(1536, 512, 1536), 128));
    }
}
