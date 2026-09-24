using System.Text;
using PartyRpg.Kit.Input;
using PartyRpg.Kit.Movement;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// How a control that is a *state* reaches the movement owner.
/// </summary>
/// <remarks>
/// The engine reports a held key as a held state: an event arrives every update the key stays down, and
/// nothing arrives once it comes up. That is a different shape from a press/release pair, and getting it
/// wrong is invisible in a unit test that only ever sends presses — which is exactly how the product
/// shipped a keyboard that did not move the party while its steps advanced.
/// </remarks>
public sealed class MovementInputHeldTests
{
    private static readonly MovementIntentNames Names = new(
        "test.move-forward",
        "test.move-back",
        "test.strafe-left",
        "test.strafe-right",
        "test.turn-left",
        "test.turn-right",
        "test.jump");

    [Fact]
    public void A_held_intent_walks_while_it_is_reported_and_stops_when_it_is_not()
    {
        MovementInput input = new(Names, turnRatePerSecond: 512);
        ProductInputEvent forward = Digital("test.move-forward", InputEdge.Held);

        Assert.Equal(1, input.Read([forward]).Forward);
        Assert.Equal(1, input.Read([forward]).Forward);

        // The key is up: no event arrives at all, and the party stops rather than walking on.
        MovementIntent stopped = input.Read([]);
        Assert.Equal(0, stopped.Forward);
        Assert.Equal(0, stopped.Strafe);
        Assert.Equal(0, stopped.TurnRate);
        Assert.False(stopped.JumpHeld);
    }

    [Fact]
    public void A_pressed_intent_still_latches_until_it_is_released()
    {
        MovementInput input = new(Names, turnRatePerSecond: 512);

        Assert.Equal(1, input.Read([Digital("test.move-forward", InputEdge.Pressed)]).Forward);
        Assert.Equal(1, input.Read([]).Forward);
        Assert.Equal(0, input.Read([Digital("test.move-forward", InputEdge.Released)]).Forward);
    }

    [Fact]
    public void Turning_arrives_as_a_held_state_too()
    {
        MovementInput input = new(Names, turnRatePerSecond: 512);
        Assert.NotEqual(0, input.Read([Digital("test.turn-left", InputEdge.Held)]).TurnRate);
        Assert.Equal(0, input.Read([]).TurnRate);
    }

    private static ProductInputEvent Digital(string intent, InputEdge edge) => new(
        InputEventKind.MappedDigital, edge, default, default, default, default, default, default, default, default,
        InputValueKind.Digital, InputPhase.Pressed, InputProvenance.Physical, default, default, default, 0f, 0f,
        ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, Encoding.UTF8.GetBytes(intent),
        ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty);
}
