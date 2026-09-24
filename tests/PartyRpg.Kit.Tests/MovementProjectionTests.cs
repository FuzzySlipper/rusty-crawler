using System.Numerics;
using PartyRpg.Kit.Movement;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.World;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// What the panel is told about the party's last movement step, and what it is told when there has not
/// been one.
/// </summary>
/// <remarks>
/// The outcomes here are built in the shape the movement owner reports them, because that is the whole
/// input to this projection: whether the engine refused a step, what a landing cost, and how high a
/// step-up raised the party are the owner's facts, and the movement suite proves the owner reads them
/// from the engine. What these cases prove is that the projection publishes the owner's answer — a
/// blocked step and an unheard key must not leave the panel with the same thing to show.
/// </remarks>
public sealed class MovementProjectionTests
{
    private static readonly SessionComposition Composition = new(new RulesetId("test.ruleset"), "Test Ruleset");

    [Fact]
    public void A_grounded_step_publishes_the_footing_and_no_obstacle()
    {
        ProjectedNode movement = Movement(MovementSnapshot.From(Outcome()));

        Assert.Equal("grounded", movement.Field("motion").AsString());
        Assert.Equal("none", movement.Field("blocked").AsString());
        Assert.Equal(0d, movement.Field("stepRise").AsNumber());
        Assert.Equal(0d, movement.Field("fallDistance").AsNumber());
        Assert.Equal(0d, movement.Field("fallDamage").AsNumber());
    }

    [Fact]
    public void An_airborne_step_publishes_that_the_party_is_in_the_air()
    {
        // Grounded and airborne are one word rather than a flag the panel has to weigh against a
        // "has moved" flag, so a party in the air can never be shown as standing.
        ProjectedNode movement = Movement(MovementSnapshot.From(Outcome(grounded: false)));

        Assert.Equal("airborne", movement.Field("motion").AsString());
    }

    [Fact]
    public void A_step_the_world_refused_publishes_the_obstacle_that_refused_it()
    {
        ProjectedNode movement = Movement(MovementSnapshot.From(Outcome(blocked: CharacterBlockFlags.Wall)));

        // The party is still on its feet against the wall: being stopped and being in the air are two
        // different answers to two different questions.
        Assert.Equal("grounded", movement.Field("motion").AsString());
        Assert.Equal("wall", movement.Field("blocked").AsString());
    }

    [Theory]
    [InlineData(CharacterBlockFlags.None, "none")]
    [InlineData(CharacterBlockFlags.Wall, "wall")]
    [InlineData(CharacterBlockFlags.Ceiling, "ceiling")]
    [InlineData(CharacterBlockFlags.SteepSlope, "steep-slope")]
    [InlineData(CharacterBlockFlags.SolverBudget, "solver-budget")]
    [InlineData(CharacterBlockFlags.StartSolid, "start-solid")]
    // A party that began inside geometry is the headline whatever else the step also met, and the
    // obstacles the engine names together still become one reason a person can act on.
    [InlineData(CharacterBlockFlags.WallCeiling, "wall")]
    [InlineData(CharacterBlockFlags.WallSteepSlope, "wall")]
    [InlineData(CharacterBlockFlags.CeilingSteepSlope, "steep-slope")]
    [InlineData(CharacterBlockFlags.WallStartSolid, "start-solid")]
    // An obstacle this wire has no word for is still a refusal, and reporting it as free would tell the
    // player their way is clear when the engine said otherwise.
    [InlineData((CharacterBlockFlags)64, "blocked")]
    public void What_refused_the_step_becomes_one_word(CharacterBlockFlags blocked, string expected)
    {
        Assert.Equal(expected, SessionProjection.WireName(blocked));
        Assert.Equal(expected, Movement(MovementSnapshot.From(Outcome(blocked: blocked))).Field("blocked").AsString());
    }

    [Fact]
    public void A_step_up_the_engine_accepted_publishes_the_height_it_raised_the_party_by()
    {
        MovementOutcome outcome = Outcome(
            step: new CharacterStep(Present: true, Attempted: true, Accepted: true, Rise: 0.4f));

        Assert.Equal(0.4d, Movement(MovementSnapshot.From(outcome)).Field("stepRise").AsNumber(), 4);
    }

    [Fact]
    public void A_step_up_the_engine_refused_is_not_a_rise_the_party_took()
    {
        // The engine reports the ledge it would have climbed and then left the party against it, so the
        // height alone would report a step-up nobody took; the refusal is published as the obstacle.
        MovementOutcome outcome = Outcome(
            blocked: CharacterBlockFlags.SteepSlope,
            step: new CharacterStep(Present: true, Attempted: true, Accepted: false, Rise: 0.9f));

        ProjectedNode movement = Movement(MovementSnapshot.From(outcome));

        Assert.Equal(0d, movement.Field("stepRise").AsNumber());
        Assert.Equal("steep-slope", movement.Field("blocked").AsString());
    }

    [Theory]
    [InlineData(2, 0)]
    [InlineData(28, 27)]
    public void A_landing_publishes_the_drop_and_what_the_tuning_charged_for_it(double drop, double damage)
    {
        // The threshold and the rate are the tuning's, applied before movement reports the step; a fall
        // within the threshold is still a fall the panel can show, with nothing charged for it.
        FallOutcome fall = new FallPolicy(threshold: 10, damagePerUnit: 1.5).Consequence(drop);

        ProjectedNode movement = Movement(MovementSnapshot.From(Outcome(fall: fall)));

        Assert.Equal(drop, movement.Field("fallDistance").AsNumber(), 3);
        Assert.Equal(damage, movement.Field("fallDamage").AsNumber(), 3);
    }

    [Fact]
    public void A_session_that_has_not_moved_publishes_no_movement_facts_at_all()
    {
        // The session builds its snapshot without movement facts while no mover feeds it, and the default
        // is the empty value rather than a quiet step: the panel must be able to say that it does not know
        // what the last step did.
        SessionSnapshot snapshot = new(Composition, SessionMode.Running, 3, 180, 180, World());

        Assert.Equal(MovementSnapshot.None, snapshot.Movement);

        ProjectedNode movement = Published(snapshot).Field(SessionProjection.MovementField);

        Assert.Equal("none", movement.Field("motion").AsString());
        Assert.Equal("none", movement.Field("blocked").AsString());
        Assert.Equal(0d, movement.Field("stepRise").AsNumber());
        Assert.Equal(0d, movement.Field("fallDistance").AsNumber());
        Assert.Equal(0d, movement.Field("fallDamage").AsNumber());
    }

    [Fact]
    public void Movement_facts_leave_the_session_and_the_world_as_they_were()
    {
        // A session without movement still shows its mode, its admitted simulation, and where the party
        // is; the movement block is carried beside those and replaces nothing.
        ProjectedNode root = Published(Snapshot(MovementSnapshot.From(Outcome(blocked: CharacterBlockFlags.Wall))));

        Assert.Equal("running", root.Field("session").Field("mode").AsString());
        Assert.Equal(12.5d, root.Field("session").Field("simulationSeconds").AsNumber(), 3);
        Assert.Equal(750d, root.Field("session").Field("admittedSteps").AsNumber());
        Assert.Equal("test-place", root.Field(SessionProjection.WorldField).Field("place").AsString());
        Assert.Equal("region", root.Field(SessionProjection.WorldField).Field("kind").AsString());
        Assert.Equal(76d, root.Field(SessionProjection.WorldField).Field("places").AsNumber());
    }

    private static ProjectedNode Published(SessionSnapshot snapshot)
    {
        UiValue value = SessionProjection.Build(snapshot);
        return new ProjectedNode(value, value.Root);
    }

    private static ProjectedNode Movement(MovementSnapshot movement) =>
        Published(Snapshot(movement)).Field(SessionProjection.MovementField);

    private static SessionSnapshot Snapshot(MovementSnapshot movement) =>
        new(Composition, SessionMode.Running, 12.5, 750, 751, World(), movement);

    private static WorldSnapshot World() =>
        new("test-place", "Test Place", "region", new PlacePose(1234, 5678, 0, Yaw: 512, Pitch: 0), 1, 76);

    /// <summary>One step's outcome, in the shape the movement owner reports it.</summary>
    private static MovementOutcome Outcome(
        bool grounded = true,
        CharacterBlockFlags blocked = CharacterBlockFlags.None,
        CharacterStep step = default,
        FallOutcome fall = default) =>
        new(
            Pose: PlacePose.Origin,
            Displacement: Vector3.Zero,
            Grounded: grounded,
            Step: step,
            Blocked: blocked,
            Stance: default,
            Surface: SurfaceEffect.Ordinary,
            Fall: fall);
}
