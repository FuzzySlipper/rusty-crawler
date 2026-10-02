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
    private const double SettleReach = 1000;

    [Fact]
    public void Collision_derived_navigation_uses_the_party_scene_and_actual_body_and_reports_derived_cells()
    {
        ScriptedSpatialService spatial = new() { NavigationCells = 23 };
        CharacterControllerConfig body = spatial.DefaultCharacterControllerConfig() with
        {
            Shape = spatial.DefaultCharacterControllerConfig().Shape with { StandingHeight = 192 },
            Surface = default(CharacterSurfaceConfig) with { MaximumStepHeight = 42 },
        };
        (EnginePartyMover mover, _) = Movers(spatial, body, derive: true);
        PlaceGeometryAdmission admitted = mover.Enter(Place);
        CollisionNavigationReplaceRequest request = Assert.Single(spatial.NavigationReplacements);
        Assert.Same(Assert.Single(spatial.Admissions).Session, request.Session);
        Assert.Equal(body, request.Config.Character);
        Assert.Equal(0.101 * 192 / spatial.DefaultCharacterControllerConfig().Shape.StandingHeight, request.Config.SnapAbove, 3);
        Assert.True(request.Config.SnapAbove < body.Surface.MaximumStepHeight);
        Assert.Equal(65536u, request.Config.MaximumCells);
        Assert.NotEqual(mover.Navigation.SteeringBudget, request.Config.MaximumCells);
        Assert.True(request.WorldMin.Y < request.WorldMax.Y);
        Assert.Equal(23ul, admitted.NavigationCells);
        mover.Step(default, 1.0 / 60);
        Assert.Same(request.Session, Assert.Single(spatial.Steps).Session);
        mover.Dispose();
    }

    [Fact]
    public void A_derivation_budget_refusal_preserves_collision_and_party_movement_but_names_the_pursuit_hold()
    {
        ScriptedSpatialService spatial = new()
        {
            DeriveNavigation = _ => throw new EngineCallException("Spatial", "ReplaceCollisionNavigation", 0,
                new[] { new EngineDiagnostic("CSHARP_COLLISION_NAVIGATION_BUDGET", "region exceeds budget", "Spatial") }),
        };
        (EnginePartyMover mover, EngineCreatureMotion creatures) = Movers(spatial, spatial.DefaultCharacterControllerConfig(), derive: true);
        PlaceGeometryAdmission admitted = mover.Enter(Place);
        Assert.True(admitted.Admitted);
        Assert.Equal(0ul, admitted.NavigationCells);
        Assert.Contains("65536", admitted.NavigationReason);
        mover.Step(default, 1.0 / 60);
        Assert.Single(spatial.Steps);
        CreatureMoveOutcome held = creatures.Move(Toward(PlacePose.Origin, new PlacePose(100, 0, 0, 0, 0)));
        Assert.True(held.IsHeld);
        Assert.Equal(CreatureMoveCodes.Navigation, held.Refusal!.Code);
        Assert.Equal(2, spatial.Steps.Count);
        Assert.Equal(Vector2.Zero, spatial.Steps[^1].Command.PlanarIntent);
        creatures.Move(Toward(held.Pose, new PlacePose(100, 0, 0, 0, 0)));
        Assert.Equal(2, spatial.Steps.Count);
        mover.Dispose();
    }

    [Fact]
    public void Navigation_queries_feet_and_the_character_solver_keeps_the_body_centre()
    {
        ScriptedSpatialService spatial = Navigable();
        (EnginePartyMover mover, EngineCreatureMotion creatures) = Movers(spatial, spatial.DefaultCharacterControllerConfig(), centreHeight: 96);
        mover.Enter(Place);
        creatures.Move(Toward(new PlacePose(100, 0, 2, 0, 0), new PlacePose(100, 100, 2, 0, 0)));
        Assert.Equal(2f, Assert.Single(spatial.NavigationSteps).From.Y);
        Assert.Equal(2f, spatial.NavigationSteps[0].Target.Y);
        Assert.Equal(98f, Assert.Single(spatial.Steps).Position.Y);
        mover.Dispose();
    }

    [Fact]
    public void A_buried_creature_in_a_place_without_navigation_is_stood_clear_before_pursuit_holds()
    {
        ScriptedSpatialService spatial = new()
        {
            Answer = (request, receipt) => request.Position.Y < 96 ? throw Embedded() : receipt,
            Rays = request => default(SpatialHit) with { Present = true, Point = request.Origin with { Y = 96 } },
        };
        (EnginePartyMover mover, EngineCreatureMotion creatures) = Movers(spatial);
        mover.Enter(Place);
        CreatureMoveOutcome placed = creatures.Move(Toward(PlacePose.Origin, new PlacePose(100, 0, 96, 0, 0)));
        Assert.Equal(96d, placed.Pose.Z);
        Assert.Equal(CreatureMoveCodes.Navigation, placed.Refusal!.Code);
        Assert.All(spatial.Steps, step => Assert.Equal(Vector2.Zero, step.Command.PlanarIntent));
        Assert.Equal(2, spatial.Steps.Count);
        creatures.Move(Toward(placed.Pose, new PlacePose(100, 0, 96, 0, 0)));
        Assert.Equal(2, spatial.Steps.Count);
        mover.Dispose();
    }

    [Fact]
    public void A_buried_initial_navigation_start_gets_a_stationary_settling_step_then_a_fresh_feet_query()
    {
        ScriptedSpatialService spatial = new()
        {
            NavigationCells = 12,
            Navigation = request => Answer(request.From.Y < 96 ? NavigationPathOutcome.StartNotWalkable : NavigationPathOutcome.Reached, request.Target),
            Answer = (request, receipt) => request.Position.Y < 96 ? throw Embedded() : receipt,
            Rays = request => default(SpatialHit) with { Present = true, Point = request.Origin with { Y = 96 } },
        };
        (EnginePartyMover mover, EngineCreatureMotion creatures) = Movers(spatial);
        mover.Enter(Place);
        CreatureMoveOutcome placed = creatures.Move(Toward(PlacePose.Origin, new PlacePose(100, 0, 96, 0, 0)));
        Assert.Equal(96d, placed.Pose.Z);
        Assert.All(spatial.Steps, step => Assert.Equal(Vector2.Zero, step.Command.PlanarIntent));
        creatures.Move(Toward(placed.Pose, new PlacePose(100, 0, 96, 0, 0)));
        Assert.Equal(96f, spatial.NavigationSteps[^1].From.Y);
        Assert.Equal(new Vector2(0, 1), spatial.Steps[^1].Command.PlanarIntent);
        mover.Dispose();
    }

    [Fact]
    public void A_place_s_geometry_the_party_s_steps_and_a_creature_s_steps_all_go_to_the_one_scene()
    {
        ScriptedSpatialService spatial = Navigable();
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

        CreatureMoveOutcome outcome = creatures.Move(Toward(new PlacePose(100, 0, 0, 0, 0), new PlacePose(100, 100, 0, 0, 0)));

        Assert.Empty(spatial.NavigationSteps);
        Assert.Equal(Vector2.Zero, Assert.Single(spatial.Steps).Command.PlanarIntent);
        Assert.Equal(CreatureMoveCodes.Navigation, outcome.Refusal!.Code);
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
    public void A_navigation_answer_that_found_no_path_holds_then_retries_when_the_target_changes()
    {
        ScriptedSpatialService spatial = new()
        {
            NavigationCells = 12,
            Navigation = _ => Answer(NavigationPathOutcome.NoPath, new Vector3(500, 0, 0)),
        };
        (EnginePartyMover mover, EngineCreatureMotion creatures) = Movers(spatial);
        mover.Enter(Place);

        CreatureMoveOutcome outcome = creatures.Move(Toward(new PlacePose(100, 0, 0, 0, 0), new PlacePose(100, 100, 0, 0, 0)));

        Assert.Single(spatial.NavigationSteps);
        Assert.Empty(spatial.Steps);
        Assert.Equal(CreatureMoveCodes.Navigation, outcome.Refusal!.Code);
        Assert.Contains("NoPath", outcome.Refusal.Message);
        spatial.Navigation = request => Answer(NavigationPathOutcome.Reached, request.Target);
        creatures.Move(Toward(new PlacePose(100, 0, 0, 0, 0), new PlacePose(100, 150, 0, 0, 0)));
        Assert.Equal(2, spatial.NavigationSteps.Count);
        Assert.Single(spatial.Steps);
        mover.Dispose();
    }

    [Fact]
    public void A_creature_content_stood_under_the_ground_is_stood_on_it_and_steps_from_there()
    {
        // The engine refuses a step whose body starts below a surface at height 96 — a record at height zero under
        // a hillside, as Harmondale's goblin spawns are — and the ground over the creature's feet is that surface.
        const float Ground = 96;
        List<SpatialRaycastRequest> rays = [];
        ScriptedSpatialService spatial = new()
        {
            NavigationCells = 12,
            Navigation = request => Answer(NavigationPathOutcome.Reached, request.Target),
            Answer = (request, receipt) => request.Position.Y < Ground ? throw Embedded() : receipt,
            Rays = request =>
            {
                rays.Add(request);
                return default(SpatialHit) with { Present = true, Point = request.Origin with { Y = Ground } };
            },
        };
        (EnginePartyMover mover, EngineCreatureMotion creatures) = Movers(spatial);
        mover.Enter(Place);

        CreatureMoveOutcome outcome = creatures.Move(Toward(new PlacePose(100, 0, 0, 0, 0), new PlacePose(100, 100, 0, 0, 0)));

        // The ray is the engine's, cast up the creature's own column from its feet in the scene it is stepped in.
        SpatialRaycastRequest ray = Assert.Single(rays);
        Assert.Same(mover.Session, ray.Session);
        Assert.Equal(new Vector3(100, 0, 0), ray.Origin);
        Assert.Equal(Vector3.UnitY, ray.Direction);
        Assert.Equal(SettleReach, ray.MaxDistance);

        // It stands on that ground, has moved to get there, and its next step carries on from it.
        Assert.False(outcome.IsHeld);
        Assert.True(outcome.Moved);
        Assert.Equal(Ground, outcome.Pose.Z, precision: 3);
        Assert.Equal(2, spatial.Steps.Count);
        Assert.Equal(default(CharacterMotion), spatial.Steps[1].Motion);
        Assert.Empty(creatures.Held);

        creatures.Move(Toward(outcome.Pose, new PlacePose(100, 100, 0, 0, 0)));
        Assert.Equal(3, spatial.Steps.Count);
        Assert.Single(rays);
        mover.Dispose();
    }

    [Fact]
    public void A_creature_the_engine_cannot_step_and_no_ground_stands_clear_is_held_by_name_not_a_fault()
    {
        // The engine refuses every step, and the ray up the creature's column meets nothing: there is no ground
        // over its feet to stand it on. The creature is held where it stands, by name, and the session goes on.
        int rays = 0;
        ScriptedSpatialService spatial = new()
        {
            NavigationCells = 12,
            Navigation = request => Answer(NavigationPathOutcome.Reached, request.Target),
            Answer = (_, _) => throw Embedded(),
            Rays = _ =>
            {
                rays++;
                return default;
            },
        };
        (EnginePartyMover mover, EngineCreatureMotion creatures) = Movers(spatial);
        mover.Enter(Place);
        PlacePose start = new(100, 0, -40, 0, 0);

        CreatureMoveOutcome outcome = creatures.Move(Toward(start, new PlacePose(100, 100, 0, 0, 0)));

        Assert.True(outcome.IsHeld);
        Assert.False(outcome.Moved);
        Assert.Equal(start, outcome.Pose);
        Assert.Equal(CreatureMoveCodes.Embedded, outcome.Refusal!.Code);
        Assert.Contains("unresolved-character-controller-penetration", outcome.Refusal.Message, StringComparison.Ordinal);
        Assert.Same(outcome.Refusal, creatures.Held[Creature]);

        // Held is held: the engine is not asked again for an answer that can only be the same refusal.
        int steps = spatial.Steps.Count;
        Assert.True(creatures.Move(Toward(start, new PlacePose(100, 100, 0, 0, 0))).IsHeld);
        Assert.Equal(steps, spatial.Steps.Count);
        Assert.Equal(1, rays);

        // A creature that leaves the field is forgotten with its hold, and is asked again when it comes back.
        creatures.Forget(Creature);
        Assert.Empty(creatures.Held);
        creatures.Move(Toward(start, new PlacePose(100, 100, 0, 0, 0)));
        Assert.Equal(steps + 1, spatial.Steps.Count);
        mover.Dispose();
    }

    [Fact]
    public void A_creature_stood_on_ground_that_still_cannot_step_is_held_and_any_other_engine_failure_is_not_swallowed()
    {
        // The ground over the feet is found but the body still does not fit there: held, with the engine's sentence.
        ScriptedSpatialService spatial = new()
        {
            NavigationCells = 12,
            Navigation = request => Answer(NavigationPathOutcome.Reached, request.Target),
            Answer = (_, _) => throw Embedded(),
            Rays = request => default(SpatialHit) with { Present = true, Point = request.Origin with { Y = 50 } },
        };
        (EnginePartyMover mover, EngineCreatureMotion creatures) = Movers(spatial);
        mover.Enter(Place);

        CreatureMoveOutcome outcome = creatures.Move(Toward(new PlacePose(100, 0, 0, 0, 0), new PlacePose(100, 100, 0, 0, 0)));
        Assert.True(outcome.IsHeld);
        Assert.Equal(0, outcome.Pose.Z);
        Assert.Equal(2, spatial.Steps.Count);
        mover.Dispose();

        // A refusal that is not about where the body stands is not a creature's to absorb: it still surfaces.
        ScriptedSpatialService broken = new()
        {
            NavigationCells = 12,
            Navigation = request => Answer(NavigationPathOutcome.Reached, request.Target),
            Answer = (_, _) => throw new EngineCallException(
                "Spatial",
                "ProposeCharacterStep",
                0,
                new[] { new EngineDiagnostic("invalid-character-controller-config", "the profile is not valid", "Spatial") }),
        };
        (EnginePartyMover other, EngineCreatureMotion stepping) = Movers(broken);
        other.Enter(Place);
        Assert.Throws<EngineCallException>(() => stepping.Move(Toward(new PlacePose(100, 0, 0, 0, 0), new PlacePose(100, 100, 0, 0, 0))));
        other.Dispose();
    }

    [Fact]
    public void Two_creatures_of_different_paces_cover_different_distances_in_the_same_step()
    {
        // The engine walks a body at the ground speeds of the profile its step is solved with; this double does the
        // same on open ground, so how far each creature goes is the pace its own request handed the engine.
        ScriptedSpatialService spatial = new() { NavigationCells = 12, Navigation = request => Answer(NavigationPathOutcome.Reached, request.Target), StepEnds = ScriptedSpatialService.AtProfilePace };
        CharacterControllerConfig profile = spatial.DefaultCharacterControllerConfig() with
        {
            Ground = default(CharacterGroundConfig) with { ForwardSpeed = 300, BackwardSpeed = 270, StrafeSpeed = 300, Acceleration = 3000 },
        };
        (EnginePartyMover mover, EngineCreatureMotion creatures) = Movers(spatial, profile);
        mover.Enter(Place);
        CombatantId slow = CombatantId.Of(new EntityId(20));
        CombatantId fast = CombatantId.Of(new EntityId(21));
        PlacePose target = new(0, 10000, 0, 0, 0);
        const double Step = 0.5;

        CreatureMoveOutcome walked = creatures.Move(new CreatureMoveRequest(slow, PlacePose.Origin, Party, target, CreatureMovePurpose.Toward, Speed: 100, Step));
        CharacterControllerConfig slowProfile = spatial.Steps[^1].Config;
        CreatureMoveOutcome ran = creatures.Move(new CreatureMoveRequest(fast, PlacePose.Origin, Party, target, CreatureMovePurpose.Toward, Speed: 400, Step));
        CharacterControllerConfig fastProfile = spatial.Steps[^1].Config;

        Assert.Equal(50, walked.MovedBy, precision: 2);
        Assert.Equal(200, ran.MovedBy, precision: 2);

        // Only the pace is the creature's own: the ground speeds and the acceleration scale together, and the body
        // and everything else is the profile it was composed with.
        Assert.Equal(100f, slowProfile.Ground.ForwardSpeed, 3);
        Assert.Equal(90f, slowProfile.Ground.BackwardSpeed, 3);
        Assert.Equal(1000f, slowProfile.Ground.Acceleration, 3);
        Assert.Equal(400f, fastProfile.Ground.ForwardSpeed, 3);
        Assert.Equal(profile.Shape, fastProfile.Shape);
        Assert.Equal(profile.Surface, fastProfile.Surface);

        // A creature that states no pace walks at the profile's own rather than standing still.
        CreatureMoveOutcome unstated = creatures.Move(new CreatureMoveRequest(Creature, PlacePose.Origin, Party, target, CreatureMovePurpose.Toward, Speed: 0, Step));
        Assert.Equal(150, unstated.MovedBy, precision: 2);
        mover.Dispose();
    }

    /// <summary>The engine's refusal of a step whose body it could not resolve out of collision, as it is raised.</summary>
    private static EngineCallException Embedded() => new(
        "Spatial",
        "ProposeCharacterStep",
        0,
        new[]
        {
            new EngineDiagnostic(
                "unresolved-character-controller-penetration",
                "unresolved-character-controller-penetration: UnresolvedPenetration { depth: 45.22667 }",
                "Spatial"),
        });

    private static (EnginePartyMover Mover, EngineCreatureMotion Creatures) Movers(ScriptedSpatialService spatial) =>
        Movers(spatial, spatial.DefaultCharacterControllerConfig());

    private static (EnginePartyMover Mover, EngineCreatureMotion Creatures) Movers(ScriptedSpatialService spatial, CharacterControllerConfig profile, bool derive = false, double centreHeight = 0)
    {
        PlaceSpace space = PlaceSpace.HeightIsThird(Facing, 0, centreHeight);
        PartyPoseOwner party = new(new PartyPose(Place, PlacePose.Origin), Facing);
        PartyMovement movement = new(spatial, party, space, new SpatialSessionConfig(0.5, 16, VoxelSurfaceMode.GreedyCubes), new MovementTuning(profile, FallPolicy.Free));
        EnginePartyMover mover = new(spatial, movement, new ScriptedContentService(), new PlaceNavigationPolicy(0, 16, 0, 512, 1024, 65536), new OnePlace(derive));
        return (mover, new EngineCreatureMotion(spatial, mover, space, profile, SettleReach));
    }

    private static CreatureMoveRequest Toward(PlacePose from, PlacePose target) =>
        new(Creature, from, Party, target, CreatureMovePurpose.Toward, Speed: 1, ElapsedSeconds: 1.0 / 60);

    private static ScriptedSpatialService Navigable() => new()
    {
        NavigationCells = 12,
        Navigation = request => Answer(NavigationPathOutcome.Reached, request.Target),
    };

    private static NavigationStepResult Answer(NavigationPathOutcome outcome, Vector3 waypoint) =>
        default(NavigationStepResult) with { Outcome = outcome, NextWaypoint = waypoint };

    /// <summary>The heading the last step a creature proposed walks along.</summary>
    private static double Heading(ScriptedSpatialService spatial) => spatial.Steps[^1].Command.HeadingYawRadians;

    /// <summary>A geometry source with an artifact for the one place these cases enter.</summary>
    private sealed class OnePlace(bool derive) : IPlaceGeometrySource
    {
        public PlaceGeometry? For(PlaceId place) =>
            place == Place ? new PlaceGeometry("places/1/collision.json", new byte[] { 1 }, navigation:
                derive ? new PlaceNavigationRegion(Vector3.Zero, new Vector3(512, 0, 512), 128) : null) : null;
    }
}
