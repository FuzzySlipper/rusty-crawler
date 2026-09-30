using System.Numerics;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Movement;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.World;
using Rusty.Engine;
using Rusty.Engine.Entities;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// The engine-backed movers over a scripted spatial service: which scene each request goes to, how a creature's
/// steering reads the engine's navigation answer, and who releases the scene.
/// </summary>
/// <remarks>
/// The spatial service here collides with nothing; it records. What these cases prove is the shape of what the
/// product asks the engine, which is the part a double can see: that a place's geometry, the party's steps and
/// every creature's steps all go to one scene, and that the scene is released once, by its owner.
/// </remarks>
public sealed class EngineMovementTests
{
    private static readonly FacingRule Facing = new(unitsPerTurn: 2048, minimumPitch: -512, maximumPitch: 512);
    private static readonly PlaceSpace Space = PlaceSpace.HeightIsThird(Facing, radiansAtZeroFacing: 0);
    private static readonly PlaceId Place = new("1");
    private static readonly CombatantId Creature = CombatantId.Of(new EntityId(7));
    private static readonly CombatantId Party = CombatantId.Of(new EntityId(8));

    [Fact]
    public void A_place_s_geometry_the_party_s_steps_and_a_creature_s_steps_all_go_to_the_one_scene()
    {
        ScriptedSpatialService spatial = new();
        (EnginePartyMover mover, EngineCreatureMotion creatures) = Movers(spatial);

        mover.Enter(Place);
        mover.Step(default, 1.0 / 60);
        creatures.Move(Toward(new PlacePose(0, 0, 0, 0, 0), new PlacePose(0, 100, 0, 0, 0)));

        SpatialSession scene = Assert.Single(spatial.Sessions).Session;
        Assert.Same(scene, mover.Session);
        Assert.Same(scene, Assert.Single(spatial.Admissions).Session);
        Assert.Equal(2, spatial.Steps.Count);
        Assert.All(spatial.Steps, step => Assert.Same(scene, step.Session));

        mover.Dispose();
        creatures.Dispose();
    }

    [Fact]
    public void The_creature_mover_releases_nothing_and_the_party_s_mover_releases_the_scene_once()
    {
        ScriptedSpatialService spatial = new();
        (EnginePartyMover mover, EngineCreatureMotion creatures) = Movers(spatial);
        ulong scene = spatial.Sessions[0].Session.Handle.Value;

        creatures.Dispose();
        Assert.Empty(spatial.Released);

        mover.Dispose();
        mover.Dispose();
        Assert.Equal([scene], spatial.Released);
    }

    [Fact]
    public void A_place_admitted_with_no_navigation_is_not_asked_for_a_waypoint()
    {
        ScriptedSpatialService spatial = new() { NavigationCells = 0 };
        (EnginePartyMover mover, EngineCreatureMotion creatures) = Movers(spatial);
        mover.Enter(Place);

        creatures.Move(Toward(new PlacePose(100, 0, 0, 0, 0), new PlacePose(100, 100, 0, 0, 0)));

        Assert.Empty(spatial.NavigationSteps);
        Assert.Equal(0, Heading(spatial), precision: 5);
        mover.Dispose();
    }

    [Fact]
    public void A_waypoint_the_engine_reached_is_steered_at_even_where_it_is_the_origin()
    {
        ScriptedSpatialService spatial = new()
        {
            NavigationCells = 12,
            Navigation = _ => Answer(NavigationPathOutcome.Reached, Vector3.Zero),
        };
        (EnginePartyMover mover, EngineCreatureMotion creatures) = Movers(spatial);
        mover.Enter(Place);

        // The creature stands east of the origin and its target is north of it: walking at the target is a
        // heading of zero, and walking at the waypoint the engine found — the origin — is a quarter turn west.
        creatures.Move(Toward(new PlacePose(100, 0, 0, 0, 0), new PlacePose(100, 100, 0, 0, 0)));

        NavigationStepRequest asked = Assert.Single(spatial.NavigationSteps);
        Assert.Same(mover.Session, asked.Session);
        Assert.Equal(-Math.PI / 2, Heading(spatial), precision: 5);
        mover.Dispose();
    }

    [Fact]
    public void A_navigation_answer_that_found_no_path_leaves_the_creature_walking_at_its_target()
    {
        ScriptedSpatialService spatial = new()
        {
            NavigationCells = 12,
            Navigation = _ => Answer(NavigationPathOutcome.NoPath, new Vector3(500, 0, 0)),
        };
        (EnginePartyMover mover, EngineCreatureMotion creatures) = Movers(spatial);
        mover.Enter(Place);

        creatures.Move(Toward(new PlacePose(100, 0, 0, 0, 0), new PlacePose(100, 100, 0, 0, 0)));

        Assert.Single(spatial.NavigationSteps);
        Assert.Equal(0, Heading(spatial), precision: 5);
        mover.Dispose();
    }

    private static (EnginePartyMover Mover, EngineCreatureMotion Creatures) Movers(ScriptedSpatialService spatial)
    {
        PartyPoseOwner party = new(new PartyPose(Place, PlacePose.Origin), Facing);
        PartyMovement movement = new(spatial, party, Space, new SpatialSessionConfig(0.5, 16, VoxelSurfaceMode.GreedyCubes));
        EnginePartyMover mover = new(spatial, movement, new ScriptedContentService(), new OnePlace());
        return (mover, new EngineCreatureMotion(spatial, mover, Space, spatial.DefaultCharacterControllerConfig()));
    }

    private static CreatureMoveRequest Toward(PlacePose from, PlacePose target) =>
        new(Creature, from, Party, target, CreatureMovePurpose.Toward, Speed: 1, ElapsedSeconds: 1.0 / 60);

    private static NavigationStepResult Answer(NavigationPathOutcome outcome, Vector3 waypoint) =>
        new(default, outcome, waypoint, default, 0, 0, 0, 0, 0);

    /// <summary>The heading the last step a creature proposed walks along.</summary>
    private static double Heading(ScriptedSpatialService spatial) => spatial.Steps[^1].Command.HeadingYawRadians;

    /// <summary>A geometry source with an artifact for the one place these cases enter.</summary>
    private sealed class OnePlace : IPlaceGeometrySource
    {
        public PlaceGeometry? For(PlaceId place) =>
            place == Place ? new PlaceGeometry("places/1/collision.json", new byte[] { 1 }) : null;
    }
}
