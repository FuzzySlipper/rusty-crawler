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
    [Fact]
    public void The_declared_key_intent_toggles_and_a_release_does_not()
    {
        Assert.Equal(SessionCommand.ToggleHold, Router.CommandFor(Admitted.Digital(ToggleIntent, InputEdge.Pressed)));
        Assert.Equal(SessionCommand.None, Router.CommandFor(Admitted.Digital(ToggleIntent, InputEdge.Released, InputPhase.Released)));
        Assert.Equal(SessionCommand.None, Router.CommandFor(Admitted.Digital("some.other.intent", InputEdge.Pressed)));
    }

    [Fact]
    public void A_direct_interface_claim_on_the_declared_intent_toggles_although_it_has_no_edge()
    {
        // A direct claim is admitted with no edge at all; its phase and provenance are what identify it.
        Assert.Equal(
            SessionCommand.ToggleHold,
            Router.CommandFor(Admitted.Digital(ToggleIntent, InputEdge.None, InputPhase.DirectUi, InputProvenance.DirectUi)));
    }

    [Fact]
    public void The_declared_action_contract_names_the_session_action()
    {
        Assert.Equal(SessionCommand.Hold, Router.CommandFor(Admitted.Payload(ActionContract, """{"action":"session.pause"}""")));
        Assert.Equal(SessionCommand.Release, Router.CommandFor(Admitted.Payload(ActionContract, """{"action":"session.resume"}""")));
        Assert.Equal(SessionCommand.None, Router.CommandFor(Admitted.Payload(ActionContract, """{"action":"inventory"}""")));
    }

    [Fact]
    public void A_foreign_contract_or_malformed_payload_carries_no_command()
    {
        Assert.Equal(SessionCommand.None, Router.CommandFor(Admitted.Payload("other.contract.v1", """{"action":"session.pause"}""")));
        Assert.Equal(SessionCommand.None, Router.CommandFor(Admitted.Payload(ActionContract, "not json")));
        Assert.Equal(SessionCommand.None, Router.CommandFor(Admitted.Payload(ActionContract, "{}")));
        Assert.Equal(SessionCommand.None, Router.CommandFor(Admitted.Payload(ActionContract, "")));
    }

    [Fact]
    public void Applying_a_payload_holds_the_session_and_the_projection_says_so()
    {
        using RecordingUiProjectionChannel channel = new();
        using PartyRpgSession session = new(Composition, channel, new SessionOwners(), SessionParty.Nobody);
        session.Start();

        ProductInputEvent[] input = [Admitted.Payload(ActionContract, """{"action":"session.pause"}""")];
        Router.Apply(session, input);

        Assert.Equal(SessionMode.Paused, session.Mode);
        Assert.Equal("paused", channel.Latest().Field("session").Field("mode").AsString());

        ProductInputEvent[] resume = [Admitted.Payload(ActionContract, """{"action":"session.resume"}""")];
        Router.Apply(session, resume);

        Assert.Equal(SessionMode.Running, session.Mode);
        Assert.Equal("running", channel.Latest().Field("session").Field("mode").AsString());
    }

    [Fact]
    public void Applying_the_key_twice_returns_the_session_to_running()
    {
        using RecordingUiProjectionChannel channel = new();
        using PartyRpgSession session = new(Composition, channel, new SessionOwners(), SessionParty.Nobody);
        session.Start();

        ProductInputEvent[] press = [Admitted.Digital(ToggleIntent, InputEdge.Pressed)];
        Router.Apply(session, press);
        Assert.Equal(SessionMode.Paused, session.Mode);

        Router.Apply(session, press);
        Assert.Equal(SessionMode.Running, session.Mode);
    }

    [Fact]
    public void A_player_hold_outlives_an_engine_pause_and_resume()
    {
        using RecordingUiProjectionChannel channel = new();
        using PartyRpgSession session = new(Composition, channel, new SessionOwners(), SessionParty.Nobody);
        session.Start();

        Router.Apply(session, [Admitted.Payload(ActionContract, """{"action":"session.pause"}""")]);
        session.Pause();
        session.Resume();

        Assert.Equal(SessionMode.Paused, session.Mode);
        Assert.True(session.IsHeld);
    }
}
