using System.Numerics;
using PartyRpg.Kit.Movement;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.World;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// How one place's own coordinates and facing unit become the engine's world axes and radians.
/// </summary>
/// <remarks>
/// The facing rule here is the test's own choice of unit, which is the point: the kit holds no facing
/// unit, so the same arithmetic must serve whatever a world counts its turns in.
/// </remarks>
public sealed class PlaceSpaceTests
{
    private static readonly FacingRule Turns = new(unitsPerTurn: 2048, minimumPitch: -512, maximumPitch: 512);

    // A quarter of a turn, in the test world's own facing unit and in radians.
    private const double QuarterTurn = 512;
    private const double QuarterTurnRadians = Math.PI / 2;

    private static PlaceSpace Space() => PlaceSpace.HeightIsThird(Turns, QuarterTurnRadians);

    [Fact]
    public void A_place_position_reads_back_as_the_position_it_was_written_from()
    {
        PlaceSpace space = Space();
        PlacePose pose = new(12, -34, 56, Yaw: 100, Pitch: 20);

        Vector3 engine = space.Position(pose);
        PlacePose returned = space.Position(engine, pose);

        // The engine's second component is height, which is the place's third coordinate.
        Assert.Equal(12f, engine.X);
        Assert.Equal(56f, engine.Y);
        Assert.Equal(34f, engine.Z);
        Assert.Equal(pose, returned);
    }

    [Fact]
    public void A_place_facing_of_zero_lays_on_the_heading_the_caller_states()
    {
        PlaceSpace space = Space();

        Assert.Equal(QuarterTurnRadians, space.FacingRadians(0), 12);
    }

    [Fact]
    public void A_place_turns_toward_its_second_ground_axis_while_the_engine_heading_falls()
    {
        PlaceSpace space = Space();

        // The place's facing grows from its first ground axis toward its second, so a quarter turn of
        // place facing is one quarter turn of engine heading in the other direction.
        Assert.Equal(0, space.FacingRadians(QuarterTurn), 12);
        Assert.Equal(-QuarterTurnRadians, space.FacingRadians(2 * QuarterTurn), 12);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(QuarterTurn)]
    [InlineData(3 * QuarterTurn)]
    [InlineData(-QuarterTurn)]
    public void A_strafe_to_the_party_s_right_stays_on_the_party_s_right(double yaw)
    {
        PlaceSpace space = Space();

        // What the engine calls the party's right at the heading the rule produces, read back into the
        // place's own ground axes: engine X is the place's first axis and engine Z is the negated second.
        double heading = space.FacingRadians(yaw);
        double rightOnFirstAxis = Math.Cos(heading);
        double rightOnSecondAxis = -Math.Sin(heading);

        // The party's own right in its place: its facing turned a quarter turn toward its second axis.
        double facingRadians = yaw * space.RadiansPerFacingUnit;
        Assert.Equal(Math.Sin(facingRadians), rightOnFirstAxis, 6);
        Assert.Equal(-Math.Cos(facingRadians), rightOnSecondAxis, 6);
    }

    [Fact]
    public void A_facing_unit_that_turns_through_nothing_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlaceSpace(0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlaceSpace(double.NaN, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlaceSpace(1, double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlaceSpace(1, 0, -0.5));
    }

    [Fact]
    public void The_engine_is_given_the_height_the_party_s_body_is_centred_at()
    {
        // The engine sweeps a body about its centre, and a party's pose says where it stands, so the
        // height a place pose carries is not the height the engine is asked to place the body at.
        PlaceSpace space = PlaceSpace.HeightIsThird(Turns, QuarterTurnRadians, bodyCentreHeight: 0.9);
        PlacePose standing = new(3, 4, 0, Yaw: 0, Pitch: 0);

        Vector3 engine = space.Position(standing);
        PlacePose returned = space.Position(engine, standing);

        // The engine's world is single precision, so reading a place position back out of it lands as
        // close as the engine's own numbers allow rather than exactly on the value that was written.
        Assert.Equal(0.9d, engine.Y, 5);
        Assert.Equal(3d, returned.X, 5);
        Assert.Equal(4d, returned.Y, 5);
        Assert.Equal(0d, returned.Z, 5);

        // Standing higher in the same place lifts the body by the same amount.
        Assert.Equal(3.4d, space.Position(standing with { Z = 2.5 }).Y, 5);
    }

    [Fact]
    public void A_position_that_is_not_a_number_is_refused_in_both_directions()
    {
        PlaceSpace space = Space();

        Assert.Throws<ArgumentOutOfRangeException>(() => space.Position(new PlacePose(double.NaN, 0, 0, 0, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => space.Position(new Vector3(float.NaN, 0, 0), PlacePose.Origin));
    }
}

/// <summary>
/// What a player's request means before the engine sees it.
/// </summary>
public sealed class MovementIntentTests
{
    [Fact]
    public void Walk_and_strafe_are_clamped_to_the_unit_square_the_engine_accepts()
    {
        // The engine refuses a step whose planar intent leaves the unit square, so a player holding two
        // directions at once must be clamped rather than allowed to produce a refused step.
        MovementIntent intent = new(forward: 3, strafe: -1.5, turnRate: 0);

        Assert.Equal(1, intent.Forward);
        Assert.Equal(-1, intent.Strafe);
    }

    [Fact]
    public void Walk_input_that_is_not_a_number_asks_for_no_walk()
    {
        // A device that reports a broken axis must not be able to stop the party moving altogether by
        // throwing out of the input path.
        MovementIntent intent = new(double.NaN, double.PositiveInfinity, turnRate: 0);

        Assert.Equal(0, intent.Forward);
        Assert.Equal(0, intent.Strafe);
    }

    [Fact]
    public void A_turn_rate_that_is_not_a_number_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MovementIntent(0, 0, double.NaN));
    }

    [Fact]
    public void An_intent_that_asks_for_nothing_is_still()
    {
        Assert.True(MovementIntent.Still.IsStill);
        Assert.False(new MovementIntent(0, 0, turnRate: 0, jumpHeld: true).IsStill);
        Assert.False(new MovementIntent(0, 0, turnRate: 1).IsStill);
    }
}

/// <summary>
/// What a surface does to the configuration the engine solves a step with, and what a fall costs.
/// </summary>
public sealed class MovementTuningTests
{
    private static CharacterControllerConfig Controller() => default(CharacterControllerConfig) with
    {
        Ground = default(CharacterGroundConfig) with
        {
            ForwardSpeed = 6,
            BackwardSpeed = 4,
            StrafeSpeed = 5,
            Acceleration = 40,
        },
        Vertical = default(CharacterVerticalConfig) with { JumpSpeed = 8, Gravity = 20 },
    };

    [Fact]
    public void A_surface_scales_the_speeds_and_the_jump_the_engine_solves_with()
    {
        MovementTuning tuning = new(Controller(), new FallPolicy(0, 0), [new SurfaceEffect("deep-water", 0.5, 0.25)]);

        CharacterControllerConfig slowed = tuning.ControllerOn(tuning.Surface("deep-water"));

        Assert.Equal(3, slowed.Ground.ForwardSpeed, 4);
        Assert.Equal(2, slowed.Ground.BackwardSpeed, 4);
        Assert.Equal(2.5, slowed.Ground.StrafeSpeed, 4);
        Assert.Equal(2, slowed.Vertical.JumpSpeed, 4);

        // Only what the ground governs changes: acceleration and gravity are the profile's, not the
        // surface's, so a slower surface does not also become a slippery or a lighter one.
        Assert.Equal(40, slowed.Ground.Acceleration, 4);
        Assert.Equal(20, slowed.Vertical.Gravity, 4);
    }

    [Fact]
    public void Ordinary_ground_leaves_the_controller_exactly_as_the_profile_states_it()
    {
        MovementTuning tuning = new(Controller(), new FallPolicy(0, 0));

        Assert.Equal(tuning.Controller, tuning.ControllerOn(SurfaceEffect.Ordinary));
    }

    [Fact]
    public void A_surface_the_profile_does_not_name_is_ordinary_ground()
    {
        MovementTuning tuning = new(Controller(), new FallPolicy(0, 0), [new SurfaceEffect("road", 1.25, 1)]);

        Assert.Equal(SurfaceEffect.Ordinary, tuning.Surface("unpriced"));
        Assert.Equal(SurfaceEffect.Ordinary, tuning.Surface(null));
        Assert.Equal(1.25, tuning.Surface("road").SpeedMultiplier, 4);
    }

    [Fact]
    public void Two_surfaces_may_not_share_a_name()
    {
        Assert.Throws<ArgumentException>(() => new MovementTuning(
            Controller(),
            new FallPolicy(0, 0),
            [new SurfaceEffect("road", 1.25, 1), new SurfaceEffect("road", 0.5, 1)]));
    }

    [Fact]
    public void AFall_that_stays_within_the_threshold_costs_nothing()
    {
        FallPolicy policy = new(threshold: 10, damagePerUnit: 1.5);

        FallOutcome outcome = policy.Consequence(10);

        Assert.Equal(10, outcome.Distance, 4);
        Assert.Equal(0, outcome.Excess, 4);
        Assert.False(outcome.PastThreshold);
        Assert.False(outcome.Costs);
    }

    [Fact]
    public void AFall_past_the_threshold_costs_the_stated_rate_for_each_unit_beyond_it()
    {
        FallPolicy policy = new(threshold: 10, damagePerUnit: 1.5);

        FallOutcome outcome = policy.Consequence(28);

        Assert.True(outcome.PastThreshold);
        Assert.True(outcome.Costs);
        Assert.Equal(18, outcome.Excess, 4);
        Assert.Equal(27, outcome.Damage, 4);
    }

    [Fact]
    public void ARise_is_not_a_fall()
    {
        FallPolicy policy = new(threshold: 10, damagePerUnit: 1.5);

        Assert.Equal(FallOutcome.None, policy.Consequence(0));
        Assert.Equal(FallOutcome.None, policy.Consequence(-4));
        Assert.False(policy.Consequence(double.NaN).Costs);
    }

    [Fact]
    public void AFall_policy_that_is_not_a_pair_of_lengths_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FallPolicy(-1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FallPolicy(1, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FallPolicy(double.NaN, 0));
    }
}

/// <summary>
/// What one engine-resolved step does to the party: where the party ends up, what a blocked step does,
/// when a step-up is reported, and what a landing costs.
/// </summary>
/// <remarks>
/// Every case here hands movement an engine step value exactly as the spatial service returns it. The
/// collision that produced the value is the engine's and is not simulated here, so what these cases
/// prove is the kit's side of the boundary: it applies what the engine resolved, reports what the engine
/// said, and never substitutes an opinion of its own.
/// </remarks>
public sealed class PartyMotionTests
{
    private static readonly PlaceId Place = new("test-place");
    private static readonly PlaceId OtherPlace = new("other-place");
    private static readonly FacingRule Turns = new(unitsPerTurn: 2048, minimumPitch: -512, maximumPitch: 512);
    private static readonly PlaceSpace Space = PlaceSpace.HeightIsThird(Turns, Math.PI / 2);

    private static PartyPoseOwner PartyAt(double x, double y, double z, double yaw = 0) =>
        new(new PartyPose(Place, new PlacePose(x, y, z, yaw, 0)), Turns);

    private static PartyMotion MotionOn(PartyPoseOwner party, MovementTuning? tuning = null, SurfaceClassifier? surfaces = null) =>
        new(party, Space, tuning ?? new MovementTuning(default, new FallPolicy(10, 1.5)), surfaces);

    /// <summary>One engine step, in the shape the spatial service returns it.</summary>
    private static CharacterStepReceipt Step(
        Vector3 from,
        Vector3 to,
        bool grounded = true,
        float peakHeight = 0,
        CharacterStep step = default,
        CharacterBlockFlags blocked = CharacterBlockFlags.None,
        CharacterGround ground = default,
        float fallOriginHeight = 0,
        CharacterContact contact = default) =>
        new(
            Movement: default,
            Tether: default,
            Generation: 1,
            Entity: 7,
            CommandSequence: 1,
            TransformBefore: new Transform(from, Quaternion.Identity, Vector3.One),
            Transform: new Transform(to, Quaternion.Identity, Vector3.One),
            Motion: new CharacterMotion(
                ControlledVelocity: Vector3.Zero,
                ExternalVelocity: Vector3.Zero,
                Grounded: grounded,
                Stance: CharacterStance.Standing,
                JumpBufferRemaining: 0,
                CoyoteRemaining: 0,
                LandingLockoutRemaining: 0,
                SupportEntityPresent: false,
                SupportEntity: 0,
                SupportLocalAnchor: Vector3.Zero,
                SupportPreviousTranslation: Vector3.Zero,
                SupportPreviousRotation: Quaternion.Identity,
                SupportPointVelocity: Vector3.Zero,
                FallOriginY: fallOriginHeight,
                PeakY: peakHeight,
                LastCommandSequence: 1,
                CollisionWorldHash: 11),
            WishVelocity: Vector3.Zero,
            Displacement: to - from,
            Contact: contact,
            Ground: ground,
            FloorProbe: default,
            Stance: new CharacterStanceFact(CharacterStance.Standing, CharacterStance.Standing, false),
            Step: step,
            Platform: default,
            BlockFlags: blocked,
            ContactCount: 0,
            CastCount: 1,
            RecoveryPasses: 0,
            RecoveryDistance: 0);

    private static CharacterGround GroundOn(ulong entity, Vector3 point) =>
        new(
            Present: true,
            Point: point,
            Normal: Vector3.UnitY,
            SnappedDistance: 0,
            SourceKind: CharacterCollisionSourceKind.ActiveEntity,
            SourceEntity: entity,
            SourceInstance: 0,
            SourceAsset: 0,
            SourceGeometryHash: 1,
            SourceVoxelX: 0,
            SourceVoxelY: 0,
            SourceVoxelZ: 0);

    [Fact]
    public void A_step_a_wall_blocked_still_moves_the_party_where_the_engine_slid_it()
    {
        PartyPoseOwner party = PartyAt(0, 0, 0);
        PartyMotion motion = MotionOn(party);

        // The engine resolved a blocked step: the party hit a wall and was projected along it, so the
        // engine's own displacement has a lateral component even though nothing was asked for sideways.
        MovementOutcome outcome = motion.Admit(Step(
            from: new Vector3(0, 1, 0),
            to: new Vector3(2, 1, 0),
            blocked: CharacterBlockFlags.Wall));

        Assert.True(outcome.WasBlocked);
        Assert.Equal(2d, outcome.Displacement.X, 4);
        Assert.Equal(2d, outcome.Pose.X, 4);
        Assert.Equal(0d, outcome.Pose.Y, 4);

        // Sliding, not sticking: a party pinned by the wall would still be standing at its start.
        Assert.Equal(2d, motion.Position.X, 4);
    }

    [Fact]
    public void A_step_up_the_engine_took_is_reported_with_the_height_it_rose()
    {
        PartyPoseOwner party = PartyAt(0, 0, 0);
        PartyMotion motion = MotionOn(party);

        MovementOutcome outcome = motion.Admit(Step(
            from: new Vector3(0, 0, 0),
            to: new Vector3(0.4f, 0.4f, 0),
            step: new CharacterStep(Present: true, Attempted: true, Accepted: true, Rise: 0.4f)));

        Assert.True(outcome.SteppedUp);
        Assert.False(outcome.WasBlocked);

        // The engine put the party on the ledge; the height is the engine's, not a rise added here.
        Assert.Equal(0.4d, outcome.Pose.Z, 4);
        Assert.Equal(0.4d, motion.Position.Y, 4);
    }

    [Fact]
    public void A_step_the_engine_refused_does_not_raise_the_party()
    {
        PartyPoseOwner party = PartyAt(0, 0, 0);
        PartyMotion motion = MotionOn(party);

        // The engine attempted the ledge and would not take it: the party is left against it, and the
        // kit must not report a step or lift the party to the height the ledge would have had.
        MovementOutcome outcome = motion.Admit(Step(
            from: new Vector3(0, 0, 0),
            to: new Vector3(0, 0, 0),
            step: new CharacterStep(Present: true, Attempted: true, Accepted: false, Rise: 0.9f),
            blocked: CharacterBlockFlags.WallSteepSlope));

        Assert.False(outcome.SteppedUp);
        Assert.True(outcome.WasBlocked);
        Assert.Equal(0d, outcome.Pose.Z, 4);
    }

    [Fact]
    public void AFall_past_the_threshold_produces_the_stated_consequence()
    {
        PartyPoseOwner party = PartyAt(0, 0, 0);
        PartyMotion motion = MotionOn(party, new MovementTuning(default, new FallPolicy(threshold: 10, damagePerUnit: 1.5)));

        // First step: the engine reports the party in the air, having reached its highest point.
        motion.Admit(Step(from: new Vector3(0, 30, 0), to: new Vector3(0, 29, 0), grounded: false, peakHeight: 30));

        // Second step: the engine lands the party. Both heights are capsule centres, so their difference
        // is the drop the engine measured.
        MovementOutcome landing = motion.Admit(Step(from: new Vector3(0, 2, 0), to: new Vector3(0, 2, 0), peakHeight: 2));

        Assert.True(landing.Grounded);
        Assert.Equal(28d, landing.Fall.Distance, 3);
        Assert.Equal(18d, landing.Fall.Excess, 3);
        Assert.Equal(27d, landing.Fall.Damage, 3);
    }

    [Fact]
    public void AFall_within_the_threshold_lands_without_a_consequence()
    {
        PartyPoseOwner party = PartyAt(0, 0, 0);
        PartyMotion motion = MotionOn(party, new MovementTuning(default, new FallPolicy(threshold: 10, damagePerUnit: 1.5)));

        motion.Admit(Step(from: new Vector3(0, 8, 0), to: new Vector3(0, 7, 0), grounded: false, peakHeight: 8));
        MovementOutcome landing = motion.Admit(Step(from: new Vector3(0, 6, 0), to: new Vector3(0, 6, 0), peakHeight: 6));

        Assert.Equal(2d, landing.Fall.Distance, 3);
        Assert.False(landing.Fall.PastThreshold);
        Assert.False(landing.Fall.Costs);
    }

    [Fact]
    public void A_leap_is_asked_of_the_next_command_and_its_landing_is_not_a_fall()
    {
        PartyPoseOwner party = PartyAt(0, 0, 0);
        PartyMotion motion = MotionOn(party, new MovementTuning(default, new FallPolicy(threshold: 1, damagePerUnit: 1)));

        // Nothing has stood the party on anything yet, so there is nothing to leap from.
        Assert.False(motion.Grounded);
        Assert.False(motion.Leap(2));
        motion.Admit(Step(from: new Vector3(0, 0, 0), to: new Vector3(0, 0, 0)));
        Assert.True(motion.Grounded);

        // The leap rides the next command as a jump at the stated multiple, and is spent by the step that took it.
        Assert.True(motion.Leap(2));
        Assert.Equal(2d, motion.PendingLeap);
        Assert.True(motion.Command(MovementIntent.Still, 0.1).JumpPressed);
        motion.Admit(Step(from: new Vector3(0, 0, 0), to: new Vector3(0, 5, 0), grounded: false, peakHeight: 30));
        Assert.Null(motion.PendingLeap);
        Assert.False(motion.Leap(2));

        // It comes down far past the threshold, and that landing is not a fall.
        MovementOutcome landing = motion.Admit(Step(from: new Vector3(0, 5, 0), to: new Vector3(0, 0, 0), peakHeight: 0));
        Assert.Equal(FallOutcome.None, landing.Fall);

        // An ordinary drop afterwards is a fall again.
        motion.Admit(Step(from: new Vector3(0, 30, 0), to: new Vector3(0, 29, 0), grounded: false, peakHeight: 30));
        MovementOutcome dropped = motion.Admit(Step(from: new Vector3(0, 2, 0), to: new Vector3(0, 2, 0), peakHeight: 2));
        Assert.True(dropped.Fall.PastThreshold);
    }

    [Fact]
    public void A_run_that_never_left_the_ground_is_never_a_fall()
    {
        PartyPoseOwner party = PartyAt(0, 0, 40);
        PartyMotion motion = MotionOn(party, new MovementTuning(default, new FallPolicy(threshold: 0, damagePerUnit: 100)));

        motion.Admit(Step(from: new Vector3(0, 40, 0), to: new Vector3(1, 40, 0)));
        MovementOutcome outcome = motion.Admit(Step(from: new Vector3(1, 40, 0), to: new Vector3(2, 40, 0)));

        // The threshold here is zero, so any drop at all would be charged. Walking costs nothing.
        Assert.True(outcome.Grounded);
        Assert.Equal(FallOutcome.None, outcome.Fall);
    }

    [Fact]
    public void The_first_step_of_a_session_is_never_charged_as_a_fall()
    {
        // A party placed high above its ground starts with no motion behind it, so the engine's first
        // resolved step is a landing from nowhere. Charging it would cost the party for being placed.
        PartyPoseOwner party = PartyAt(0, 0, 400);
        PartyMotion motion = MotionOn(party, new MovementTuning(default, new FallPolicy(threshold: 1, damagePerUnit: 10)));

        MovementOutcome outcome = motion.Admit(Step(from: new Vector3(0, 400, 0), to: new Vector3(0, 0, 0)));

        Assert.True(outcome.Grounded);
        Assert.Equal(FallOutcome.None, outcome.Fall);
    }

    [Fact]
    public void The_party_s_pose_is_the_only_place_movement_holds_a_position()
    {
        PartyPoseOwner party = PartyAt(0, 0, 0);
        PartyMotion motion = MotionOn(party);

        MovementOutcome outcome = motion.Admit(Step(from: new Vector3(0, 1, 0), to: new Vector3(3, 1, 4)));

        // What movement reports is what the pose owner holds, not a copy it kept.
        Assert.Equal(party.Capture().Pose, outcome.Pose);

        // When the owner moves the party without movement — a transition, or a restored save — movement
        // reads the new place and position on the next ask rather than continuing from where it was.
        party.Enter(OtherPlace, new PlacePose(-8, 9, 10, Yaw: 256, Pitch: 0));

        Assert.Equal(OtherPlace, party.Place);
        Assert.Equal(-8d, motion.Position.X, 4);
        Assert.Equal(10d, motion.Position.Y, 4);
        Assert.Equal(-9d, motion.Position.Z, 4);

        // And a command built after the move carries the restored facing rather than the old one.
        CharacterControllerCommand command = motion.Command(MovementIntent.Still, 0.05);
        Assert.Equal(Space.FacingRadians(256), command.HeadingYawRadians, 4);
    }

    [Fact]
    public void A_command_carries_the_intent_the_heading_the_party_holds_and_the_step_s_time()
    {
        PartyPoseOwner party = PartyAt(0, 0, 0, yaw: 0);
        PartyMotion motion = MotionOn(party);

        CharacterControllerCommand command = motion.Command(new MovementIntent(0.5, -1, turnRate: 512), elapsedSeconds: 0.05);

        Assert.Equal(-1d, command.PlanarIntent.X, 4);
        Assert.Equal(0.5d, command.PlanarIntent.Y, 4);
        Assert.Equal(0.05d, command.StepSeconds, 6);
        Assert.Equal(1ul, command.Sequence);

        // The turn is the party's own: it is on the pose before the heading is read, so the engine solves
        // the step in the direction the party will actually be facing.
        Assert.Equal(512 * 0.05, party.PlacePose.Yaw, 6);
        Assert.Equal(Space.FacingRadians(party.PlacePose.Yaw), (double)command.HeadingYawRadians, 4);

        // And the pose owner is still the only place that yaw is held.
        Assert.Equal(512 * 0.05, party.Capture().Pose.Yaw, 6);
    }

    [Fact]
    public void A_step_that_covers_no_time_is_refused()
    {
        PartyMotion motion = MotionOn(PartyAt(0, 0, 0));

        Assert.Throws<ArgumentOutOfRangeException>(() => motion.Command(MovementIntent.Still, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => motion.Command(MovementIntent.Still, double.NaN));
    }

    [Fact]
    public void Ground_the_classifier_names_becomes_that_surface_and_is_kept_while_airborne()
    {
        MovementTuning tuning = new(
            default(CharacterControllerConfig) with
            {
                Ground = default(CharacterGroundConfig) with { ForwardSpeed = 6 },
            },
            new FallPolicy(0, 0),
            [new SurfaceEffect("deep-water", 0.5, 0.25)]);
        PartyPoseOwner party = PartyAt(0, 0, 0);
        PartyMotion motion = MotionOn(
            party,
            tuning,
            (CharacterGround ground, out string surfaceId) =>
            {
                surfaceId = "deep-water";
                return ground.SourceEntity == 4;
            });

        MovementOutcome ashore = motion.Admit(Step(
            from: new Vector3(0, 1, 0),
            to: new Vector3(0, 1, 0),
            ground: GroundOn(entity: 4, point: new Vector3(0, 0, 0))));

        Assert.Equal("deep-water", ashore.Surface.Id);

        // A party in the air is over the ground it left, so a jump out of water keeps water's speeds
        // until it lands on something else.
        MovementOutcome airborne = motion.Admit(Step(
            from: new Vector3(0, 1, 0),
            to: new Vector3(0, 2, 0),
            grounded: false,
            peakHeight: 2));

        Assert.Equal("deep-water", airborne.Surface.Id);
        Assert.Equal(3d, tuning.ControllerOn(airborne.Surface).Ground.ForwardSpeed, 4);
    }

    /// <summary>A flight a test switches on and off, standing in for a game's answer.</summary>
    private sealed class FlightSwitch : IFlightRule
    {
        public bool MayFly { get; set; }
    }

    private static readonly FlightTuning Flight = new(speed: 40, acceleration: 160, drag: 0, ceiling: 100);

    private static CharacterContact GroundContact(Vector3 point) =>
        new(
            Present: true,
            Kind: CharacterContactKind.Ground,
            StartSolid: false,
            Point: point,
            Normal: Vector3.UnitY,
            TimeOfImpact: 0.5f,
            SourceKind: CharacterCollisionSourceKind.StaticMesh,
            SourceEntity: 0,
            SourceInstance: 1,
            SourceAsset: 1,
            SourceGeometryHash: 1,
            SourceVoxelX: 0,
            SourceVoxelY: 0,
            SourceVoxelZ: 0);

    [Fact]
    public void A_party_that_may_fly_rises_on_its_rise_hovers_without_one_and_walks_again_where_it_lands()
    {
        FlightSwitch rule = new();
        PartyPoseOwner party = PartyAt(0, 0, 0);
        PartyMotion motion = new(party, Space, new MovementTuning(default, new FallPolicy(10, 1), flight: Flight), flight: rule);
        motion.Admit(Step(from: Vector3.Zero, to: Vector3.Zero));

        // Without a flight a rise asks for nothing: the party walks and may jump.
        CharacterControllerCommand walking = motion.Command(new MovementIntent(0, 0, jumpPressed: true, vertical: 1), 0.1);
        Assert.Equal(CharacterMovementMode.Walking, walking.Movement.Mode);
        Assert.True(walking.JumpPressed);
        Assert.False(motion.Flying);

        // With one, standing still on the ground is still walking; the rise is what takes off.
        rule.MayFly = true;
        Assert.True(motion.MayFly);
        Assert.Equal(CharacterMovementMode.Walking, motion.Command(MovementIntent.Still, 0.1).Movement.Mode);
        CharacterControllerCommand rising = motion.Command(new MovementIntent(1, 0, jumpPressed: true, vertical: 1), 0.1);
        Assert.Equal(CharacterMovementMode.Flying, rising.Movement.Mode);
        Assert.Equal(1f, rising.Movement.VerticalIntent);
        Assert.Equal(40f, rising.Movement.Speed);
        Assert.Equal(160f, rising.Movement.Acceleration);
        Assert.False(rising.JumpPressed);

        // The engine carries the party up; a flight is not a fall, and the fall a later landing would measure begins
        // where the party is held rather than at the highest point it reached.
        MovementOutcome up = motion.Admit(Step(from: Vector3.Zero, to: new Vector3(0, 50, 0), grounded: false, peakHeight: 70));
        Assert.True(up.Flying);
        Assert.Equal(FallOutcome.None, up.Fall);
        Assert.Equal(50f, motion.Continuation.PeakY);
        Assert.Equal(50f, motion.Continuation.FallOriginY);

        // Letting go hovers: still flying, asking for no rise.
        CharacterControllerCommand hovering = motion.Command(MovementIntent.Still, 0.1);
        Assert.Equal(CharacterMovementMode.Flying, hovering.Movement.Mode);
        Assert.Equal(0f, hovering.Movement.VerticalIntent);

        // A sink that meets the ground lands the party, and it walks again.
        CharacterControllerCommand sinking = motion.Command(new MovementIntent(0, 0, vertical: -1), 0.1);
        Assert.Equal(-1f, sinking.Movement.VerticalIntent);
        MovementOutcome down = motion.Admit(Step(
            from: new Vector3(0, 50, 0),
            to: new Vector3(0, 1, 0),
            grounded: false,
            peakHeight: 50,
            contact: GroundContact(Vector3.Zero)));
        Assert.False(down.Flying);
        Assert.False(motion.Flying);
        Assert.Equal(1f, motion.Continuation.PeakY);
        Assert.Equal(CharacterMovementMode.Walking, motion.Command(MovementIntent.Still, 0.1).Movement.Mode);

        // The engine snaps the walking party onto the ground from where the flight left it: a drop of the last unit, nothing
        // like the fifty it flew at.
        MovementOutcome landed = motion.Admit(Step(from: new Vector3(0, 1, 0), to: Vector3.Zero, peakHeight: 1));
        Assert.False(landed.Fall.PastThreshold);
        Assert.Equal(1d, landed.Fall.Distance, 4);
    }

    [Fact]
    public void A_flight_that_ends_in_the_air_drops_the_party_from_where_it_ended_under_the_fall_rule()
    {
        FlightSwitch rule = new() { MayFly = true };
        PartyPoseOwner party = PartyAt(0, 0, 0);
        PartyMotion motion = new(party, Space, new MovementTuning(default, new FallPolicy(10, 1), flight: Flight), flight: rule);
        motion.Admit(Step(from: Vector3.Zero, to: Vector3.Zero));

        motion.Command(new MovementIntent(0, 0, vertical: 1), 0.1);
        motion.Admit(Step(from: Vector3.Zero, to: new Vector3(0, 90, 0), grounded: false, peakHeight: 90));
        motion.Command(new MovementIntent(0, 0, vertical: -1), 0.1);
        motion.Admit(Step(from: new Vector3(0, 90, 0), to: new Vector3(0, 30, 0), grounded: false, peakHeight: 90));
        Assert.Equal(30f, motion.Continuation.PeakY);

        // The flight ends while the party is in the air: the next command walks, so gravity has it.
        rule.MayFly = false;
        Assert.Equal(CharacterMovementMode.Walking, motion.Command(new MovementIntent(0, 0, vertical: 1), 0.1).Movement.Mode);
        Assert.False(motion.Flying);

        // It lands from the thirty it was held at, not from the ninety it once reached, and that is a fall past the
        // threshold.
        MovementOutcome landed = motion.Admit(Step(from: new Vector3(0, 30, 0), to: Vector3.Zero, peakHeight: 30));
        Assert.True(landed.Fall.PastThreshold);
        Assert.Equal(30d, landed.Fall.Distance, 4);
    }

    [Fact]
    public void A_flying_party_at_its_ceiling_rises_no_further_but_may_still_sink()
    {
        FlightSwitch rule = new() { MayFly = true };
        PartyPoseOwner party = PartyAt(0, 0, 100);
        PartyMotion motion = new(party, Space, new MovementTuning(default, new FallPolicy(10, 1), flight: Flight), flight: rule);

        // In the air under a flight, a rise takes the party into flight, and at the ceiling it asks for no height.
        CharacterControllerCommand atCeiling = motion.Command(new MovementIntent(0, 0, vertical: 1), 0.1);
        Assert.Equal(CharacterMovementMode.Flying, atCeiling.Movement.Mode);
        Assert.Equal(0f, atCeiling.Movement.VerticalIntent);
        Assert.Equal(-1f, motion.Command(new MovementIntent(0, 0, vertical: -1), 0.1).Movement.VerticalIntent);

        // A profile without flight never flies, whatever the rule says.
        PartyMotion grounded = new(PartyAt(0, 0, 0), Space, new MovementTuning(default, new FallPolicy(10, 1)), flight: rule);
        Assert.False(grounded.MayFly);
        Assert.Equal(CharacterMovementMode.Walking, grounded.Command(new MovementIntent(0, 0, vertical: 1), 0.1).Movement.Mode);
    }

    [Fact]
    public void Ground_the_classifier_does_not_recognise_is_ordinary_ground()
    {
        MovementTuning tuning = new(default, new FallPolicy(0, 0), [new SurfaceEffect("deep-water", 0.5, 0.25)]);
        PartyMotion motion = MotionOn(PartyAt(0, 0, 0), tuning, (CharacterGround ground, out string surfaceId) =>
        {
            surfaceId = string.Empty;
            return false;
        });

        MovementOutcome outcome = motion.Admit(Step(
            from: new Vector3(0, 1, 0),
            to: new Vector3(0, 1, 0),
            ground: GroundOn(entity: 9, point: new Vector3(0, 0, 0))));

        Assert.Equal(SurfaceEffect.Ordinary, outcome.Surface);
    }
}
