using System.Text;
using PartyRpg.Kit.Input;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// The input path from an admitted engine event to a player hold. This is the product half of the DOM
/// action round trip; the browser leg belongs to the engine transport.
/// </summary>
public sealed class SessionInputRouterTests
{
    private const string ToggleIntent = "session.pause-toggle";
    private const string ActionContract = "crawler.ui.action.v1";
    private static readonly SessionComposition Composition = new(new RulesetId("test.ruleset"), "Test Ruleset");

    private static readonly SessionInputRouter Router = new(ToggleIntent, ActionContract);

    // The engine copies five byte blocks per event in constructor order: label, mapping id, intent,
    // payload contract, payload data. Only the last three matter to this router.
    private static ProductInputEvent Digital(
        string intent,
        InputEdge edge,
        InputPhase phase = InputPhase.Pressed,
        InputProvenance provenance = InputProvenance.Physical) => new(
        InputEventKind.MappedDigital, edge, default, default, default, default, default, default, default, default,
        InputValueKind.Digital, phase, provenance, default, default, default, 0f, 0f,
        ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, Encoding.UTF8.GetBytes(intent),
        ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty);

    private static ProductInputEvent Payload(string contract, string json) => new(
        InputEventKind.DirectProductPayload, InputEdge.None, default, default, default, default, default, default, default, default,
        InputValueKind.ProductPayload, InputPhase.DirectUi, InputProvenance.DirectUi, default, default, default, 0f, 0f,
        ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty,
        Encoding.UTF8.GetBytes(contract), Encoding.UTF8.GetBytes(json));

    [Fact]
    public void The_declared_key_intent_toggles_and_a_release_does_not()
    {
        Assert.Equal(SessionCommand.ToggleHold, Router.CommandFor(Digital(ToggleIntent, InputEdge.Pressed)));
        Assert.Equal(SessionCommand.None, Router.CommandFor(Digital(ToggleIntent, InputEdge.Released, InputPhase.Released)));
        Assert.Equal(SessionCommand.None, Router.CommandFor(Digital("some.other.intent", InputEdge.Pressed)));
    }

    [Fact]
    public void A_direct_interface_claim_on_the_declared_intent_toggles_although_it_has_no_edge()
    {
        // A direct claim is admitted with no edge at all; its phase and provenance are what identify it.
        Assert.Equal(
            SessionCommand.ToggleHold,
            Router.CommandFor(Digital(ToggleIntent, InputEdge.None, InputPhase.DirectUi, InputProvenance.DirectUi)));
    }

    [Fact]
    public void The_declared_action_contract_names_the_session_action()
    {
        Assert.Equal(SessionCommand.Hold, Router.CommandFor(Payload(ActionContract, """{"action":"session.pause"}""")));
        Assert.Equal(SessionCommand.Release, Router.CommandFor(Payload(ActionContract, """{"action":"session.resume"}""")));
        Assert.Equal(SessionCommand.None, Router.CommandFor(Payload(ActionContract, """{"action":"inventory"}""")));
    }

    [Fact]
    public void A_foreign_contract_or_malformed_payload_carries_no_command()
    {
        Assert.Equal(SessionCommand.None, Router.CommandFor(Payload("other.contract.v1", """{"action":"session.pause"}""")));
        Assert.Equal(SessionCommand.None, Router.CommandFor(Payload(ActionContract, "not json")));
        Assert.Equal(SessionCommand.None, Router.CommandFor(Payload(ActionContract, "{}")));
        Assert.Equal(SessionCommand.None, Router.CommandFor(Payload(ActionContract, "")));
    }

    [Fact]
    public void Applying_a_payload_holds_the_session_and_the_projection_says_so()
    {
        using RecordingUiProjectionChannel channel = new();
        using PartyRpgSession session = new(Composition, channel);
        session.Start();

        ProductInputEvent[] input = [Payload(ActionContract, """{"action":"session.pause"}""")];
        Router.Apply(session, input);

        Assert.Equal(SessionMode.Paused, session.Mode);
        Assert.Equal("paused", channel.Latest().Field("session").Field("mode").AsString());

        ProductInputEvent[] resume = [Payload(ActionContract, """{"action":"session.resume"}""")];
        Router.Apply(session, resume);

        Assert.Equal(SessionMode.Running, session.Mode);
        Assert.Equal("running", channel.Latest().Field("session").Field("mode").AsString());
    }

    [Fact]
    public void Applying_the_key_twice_returns_the_session_to_running()
    {
        using RecordingUiProjectionChannel channel = new();
        using PartyRpgSession session = new(Composition, channel);
        session.Start();

        ProductInputEvent[] press = [Digital(ToggleIntent, InputEdge.Pressed)];
        Router.Apply(session, press);
        Assert.Equal(SessionMode.Paused, session.Mode);

        Router.Apply(session, press);
        Assert.Equal(SessionMode.Running, session.Mode);
    }

    [Fact]
    public void A_player_hold_outlives_an_engine_pause_and_resume()
    {
        using RecordingUiProjectionChannel channel = new();
        using PartyRpgSession session = new(Composition, channel);
        session.Start();

        Router.Apply(session, [Payload(ActionContract, """{"action":"session.pause"}""")]);
        session.Pause();
        session.Resume();

        Assert.Equal(SessionMode.Paused, session.Mode);
        Assert.True(session.IsHeld);
    }
}
