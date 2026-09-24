using PartyRpg.Kit.Party;
using PartyRpg.Kit.World;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// The party's one pose, the view derived from it, and the boundaries a pose may cross: entering a place,
/// walking and turning inside it, and resuming from a captured pose.
/// </summary>
/// <remarks>
/// The facing rule here is the test's own choice of unit, which is the point: the kit holds no unit, so a
/// test can state one and the same arithmetic must serve it.
/// </remarks>
public sealed class PartyPoseTests
{
    private static readonly PlaceId Place = new("test-place");
    private static readonly PlaceId OtherPlace = new("other-place");
    private static readonly FacingRule Turns = new(unitsPerTurn: 2048, minimumPitch: -512, maximumPitch: 512);

    private static PartyPoseOwner PartyAt(double x, double y, double z, double yaw = 0, double pitch = 0) =>
        new(new PartyPose(Place, new PlacePose(x, y, z, yaw, pitch)), Turns);

    [Fact]
    public void A_composed_party_holds_the_pose_it_was_given()
    {
        PartyPoseOwner party = PartyAt(10, 20, 30, yaw: 512, pitch: -100);

        Assert.Equal(Place, party.Place);
        Assert.Equal(new PlacePose(10, 20, 30, 512, -100), party.PlacePose);
        Assert.Equal(new PartyPose(Place, new PlacePose(10, 20, 30, 512, -100)), party.Capture());
    }

    [Fact]
    public void Entering_a_place_sets_the_place_and_the_pose_together()
    {
        PartyPoseOwner party = PartyAt(0, 0, 0, yaw: 100, pitch: 200);

        party.Enter(OtherPlace, new PlacePose(1, 2, 3, 400, -100));

        // The arrival pose is the one the world's graph resolved, place included: nothing of the place the
        // party left survives into the place it arrived in.
        Assert.Equal(OtherPlace, party.Place);
        Assert.Equal(new PlacePose(1, 2, 3, 400, -100), party.PlacePose);
    }

    [Fact]
    public void Entering_normalizes_the_facing_through_the_callers_rule()
    {
        PartyPoseOwner party = PartyAt(0, 0, 0);

        party.Enter(Place, new PlacePose(4, 5, 6, Yaw: 2048 + 300, Pitch: 9000));

        // A content pose can name a yaw beyond a turn and a pitch beyond what the party may look; the
        // stored pose is always one the party could have reached by turning.
        Assert.Equal(new PlacePose(4, 5, 6, 300, 512), party.PlacePose);
    }

    [Fact]
    public void Moving_translates_the_party_and_leaves_the_facing_alone()
    {
        PartyPoseOwner party = PartyAt(10, 20, 30, yaw: 512, pitch: -100);

        party.Move(3, -4, 5);

        Assert.Equal(new PlacePose(13, 16, 35, 512, -100), party.PlacePose);
        Assert.Equal(Place, party.Place);
    }

    [Fact]
    public void Turning_changes_the_facing_and_leaves_the_position_alone()
    {
        PartyPoseOwner party = PartyAt(10, 20, 30);

        party.Turn(40, 25);

        Assert.Equal(new PlacePose(10, 20, 30, 40, 25), party.PlacePose);
    }

    [Fact]
    public void A_full_turn_of_yaw_wraps_in_both_directions()
    {
        PartyPoseOwner party = PartyAt(0, 0, 0, yaw: 2040);

        party.Turn(20, 0);
        Assert.Equal(12, party.PlacePose.Yaw);

        party.Turn(-30, 0);
        Assert.Equal(2030, party.PlacePose.Yaw);
    }

    [Fact]
    public void A_wrapped_yaw_always_lands_inside_the_turn()
    {
        // Rounding can leave a yaw a hair below zero, whose wrap lands exactly on the turn: that is the
        // same facing as none, and it must not escape the range a caller can compare against.
        Assert.Equal(0d, Turns.WrapYaw(-1e-18));

        double[] yaws = [-2048, -1, 0, 1, 2047.5, 4096, 1e9, double.MaxValue];
        foreach (double yaw in yaws)
        {
            double wrapped = Turns.WrapYaw(yaw);
            Assert.True(
                wrapped >= 0 && wrapped < Turns.UnitsPerTurn,
                $"A yaw of {yaw} wrapped to {wrapped}, which is outside one turn.");
        }
    }

    [Fact]
    public void Pitch_is_clamped_at_both_ends_of_the_callers_rule()
    {
        PartyPoseOwner party = PartyAt(0, 0, 0);

        party.Turn(0, 100000);
        Assert.Equal(512, party.PlacePose.Pitch);

        party.Turn(0, -100000);
        Assert.Equal(-512, party.PlacePose.Pitch);
    }

    [Fact]
    public void The_facing_rule_carries_the_unit_the_caller_states_and_not_one_the_kit_bakes_in()
    {
        // The same numbers are different facings under different rules: the caller tells the kit what the
        // world's data means, and the kit converts nothing on its own.
        PartyPoseOwner degrees = new(
            new PartyPose(Place, PlacePose.Origin),
            new FacingRule(unitsPerTurn: 360, minimumPitch: -89, maximumPitch: 89));

        degrees.Enter(Place, new PlacePose(0, 0, 0, Yaw: 370, Pitch: 200));
        Assert.Equal(10, degrees.PlacePose.Yaw);
        Assert.Equal(89, degrees.PlacePose.Pitch);

        PartyPoseOwner turns = PartyAt(0, 0, 0);
        turns.Turn(370, 0);
        Assert.Equal(370, turns.PlacePose.Yaw);
    }

    [Fact]
    public void A_place_rule_that_refuses_a_pose_leaves_the_party_where_it_was()
    {
        static bool Refuse(PlaceId place, PlacePose pose, out PlacePose admitted)
        {
            // This test place simply has nothing beyond the eighth unit of its first axis, and a pose the
            // place has no ground for is refused rather than quietly moved somewhere else.
            admitted = pose;
            return pose.X <= 8;
        }

        PartyPoseOwner party = new(new PartyPose(Place, new PlacePose(1, 2, 3, 4, 5)), Turns, Refuse);
        PartyPose before = party.Capture();

        Assert.Throws<ArgumentException>(() => party.Enter(OtherPlace, new PlacePose(9, 9, 9, 0, 0)));

        Assert.Equal(before, party.Capture());
        Assert.Equal(Place, party.Place);
    }

    [Fact]
    public void A_place_rule_may_reset_a_pose_the_place_does_not_hold()
    {
        // The rule stands in for the place's own bounds, which the kit does not hold: a pose outside this
        // test place's box comes back to a spot inside it, and a pose inside is kept as it arrived.
        static bool InsideTheBox(PlaceId place, PlacePose pose, out PlacePose admitted)
        {
            admitted = pose.X is >= 0 and <= 100 && pose.Y is >= 0 and <= 100 ? pose : pose with { X = 5, Y = 6 };
            return true;
        }

        PartyPoseOwner party = new(new PartyPose(Place, PlacePose.Origin), Turns, InsideTheBox);

        party.Enter(Place, new PlacePose(500, 6, 0, 0, 0));
        Assert.Equal(new PlacePose(5, 6, 0, 0, 0), party.PlacePose);

        party.Enter(Place, new PlacePose(50, 60, 0, 0, 0));
        Assert.Equal(new PlacePose(50, 60, 0, 0, 0), party.PlacePose);
    }

    [Fact]
    public void The_place_rule_is_asked_about_the_pose_the_party_would_hold()
    {
        PlacePose seen = default;
        bool Observe(PlaceId place, PlacePose pose, out PlacePose admitted)
        {
            seen = pose;
            admitted = pose;
            return true;
        }

        PartyPoseOwner party = new(new PartyPose(Place, PlacePose.Origin), Turns, Observe);

        party.Enter(Place, new PlacePose(1, 2, 3, Yaw: 4096 + 7, Pitch: -5000));

        // A rule that measures a place's bounds must judge the facing the party would actually hold, so the
        // facing rule is applied before the place has its say.
        Assert.Equal(new PlacePose(1, 2, 3, 7, -512), seen);
    }

    [Fact]
    public void A_pose_that_is_not_made_of_numbers_is_refused()
    {
        PartyPoseOwner party = PartyAt(1, 2, 3);
        PartyPose before = party.Capture();

        Assert.Throws<ArgumentOutOfRangeException>(() => party.Enter(Place, new PlacePose(double.NaN, 0, 0, 0, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => party.Restore(new PartyPose(OtherPlace, new PlacePose(0, 0, double.PositiveInfinity, 0, 0))));
        Assert.Throws<ArgumentOutOfRangeException>(() => party.Turn(double.NaN, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => party.Move(0, 0, double.NegativeInfinity));
        Assert.ThrowsAny<ArgumentException>(() => party.Enter(new PlaceId("  "), PlacePose.Origin));

        Assert.Equal(before, party.Capture());
    }

    [Fact]
    public void A_move_that_would_not_land_on_a_position_is_refused()
    {
        PartyPoseOwner party = PartyAt(double.MaxValue, 0, 0);

        Assert.Throws<InvalidOperationException>(() => party.Move(double.MaxValue, 0, 0));

        Assert.Equal(double.MaxValue, party.PlacePose.X);
    }

    [Fact]
    public void The_view_is_the_partys_pose_plus_the_offsets()
    {
        PartyPoseOwner party = PartyAt(10, 20, 30, yaw: 512, pitch: 100);
        PartyViewOffsets offsets = new(lookPitch: 50, eyeHeight: 6, bobOffset: 1.5);

        PartyView view = party.DeriveView(offsets);

        // Position, facing, and unit all come from the pose: the offsets lift the eye, add the look pitch,
        // and change nothing else.
        Assert.Equal(new PartyView(Place, 10, 20, 37.5, 512, 150), view);
    }

    [Fact]
    public void The_same_pose_always_derives_the_same_view()
    {
        PartyPoseOwner party = PartyAt(10, 20, 30, yaw: 512, pitch: 100);
        PartyViewOffsets offsets = new(lookPitch: 50, eyeHeight: 6, bobOffset: 1.5);
        PartyView first = party.DeriveView(offsets);

        // Deriving a view carries nothing over from the previous one: there is no bob, look pitch, or
        // anything else that could accumulate over an update the party did not move in.
        for (int i = 0; i < 64; i++) Assert.Equal(first, party.DeriveView(offsets));

        Assert.Equal(new PartyView(Place, 10, 20, 36, 512, 150), party.DeriveView(new PartyViewOffsets(50, 6, 0)));
    }

    [Fact]
    public void Turning_and_moving_the_party_moves_the_view()
    {
        PartyPoseOwner party = PartyAt(0, 0, 0);
        PartyViewOffsets offsets = new(lookPitch: 0, eyeHeight: 5, bobOffset: 0);
        PartyView before = party.DeriveView(offsets);

        party.Move(1, 2, 3);
        party.Turn(90, 0);

        // The view the party had is a value that changed with nothing: only the pose moved, and the view
        // derived from it is the whole of what a renderer is handed.
        Assert.Equal(new PartyView(Place, 0, 0, 5, 0, 0), before);
        Assert.Equal(new PartyView(Place, 1, 2, 8, 90, 0), party.DeriveView(offsets));
        Assert.Equal(PartyView.Derive(party.Capture(), offsets), party.DeriveView(offsets));
    }

    [Fact]
    public void Deriving_a_view_leaves_the_party_where_it_is()
    {
        PartyPoseOwner party = PartyAt(10, 20, 30, yaw: 512, pitch: 100);
        PartyPose before = party.Capture();

        party.DeriveView(new PartyViewOffsets(lookPitch: 1000, eyeHeight: 1000, bobOffset: 1000));

        // No path leads from a view back into the party: deriving one cannot be a way to move the party.
        Assert.Equal(before, party.Capture());
    }

    [Fact]
    public void A_captured_pose_is_a_snapshot_and_not_a_window_on_the_party()
    {
        PartyPoseOwner party = PartyAt(10, 20, 30);
        PartyPose saved = party.Capture();

        party.Move(5, 5, 5);
        party.Turn(100, 0);

        // What a save recorded must keep saying where the party was, or a later write would rewrite the
        // save it was written from.
        Assert.Equal(new PlacePose(10, 20, 30, 0, 0), saved.Pose);
        Assert.Equal(new PlacePose(15, 25, 35, 100, 0), party.PlacePose);
    }

    [Fact]
    public void A_captured_pose_restores_into_the_same_place_and_facing()
    {
        PartyPoseOwner party = PartyAt(10, 20, 30, yaw: 512, pitch: 100);
        PartyPose saved = party.Capture();

        party.Enter(OtherPlace, new PlacePose(99, 99, 99, 1000, 400));
        party.Move(1, 1, 1);
        party.Restore(saved);

        Assert.Equal(saved, party.Capture());
        Assert.Equal(Place, party.Place);
        Assert.Equal(new PlacePose(10, 20, 30, 512, 100), party.PlacePose);
    }

    [Fact]
    public void A_party_reloaded_from_a_saved_pose_derives_the_same_view()
    {
        PartyPoseOwner played = PartyAt(10, 20, 30, yaw: 512, pitch: 100);
        PartyViewOffsets offsets = new(lookPitch: 25, eyeHeight: 6, bobOffset: 0.5);
        played.Move(4, -2, 0);
        played.Turn(60, 20);
        PartyView expected = played.DeriveView(offsets);
        PartyPose saved = played.Capture();

        // A resumed session builds its owner from the captured pose alone, and the player sees the view the
        // saved party had, in the place and at the facing it was saved in.
        PartyPoseOwner resumed = new(saved, Turns);

        Assert.Equal(saved, resumed.Capture());
        Assert.Equal(expected, resumed.DeriveView(offsets));
    }
}
