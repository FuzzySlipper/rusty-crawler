using System.Text;
using PartyRpg.Kit.Input;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Services;
using PartyRpg.Kit.Sessions;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// One update's input read once: its digital events, its semantic actions parsed a single time, and the actions
/// nobody took reported rather than dropped.
/// </summary>
public sealed class ActionInboxTests
{
    private const string Contract = "test.actions";

    [Fact]
    public void An_update_is_read_once_into_its_keys_and_its_actions()
    {
        ActionInbox inbox = new(
        [
            Admitted.Digital("test.leave"),
            Payload("""{"action":"service.buy","target":"7","count":2}"""),
            // Nothing a reader could act on is kept: bytes that are not JSON, a payload that names no action, and
            // a payload that is not an object.
            Payload("not json"),
            Payload("""{"target":"7"}"""),
            Payload("""["service.buy"]"""),
            Payload("""{"action":"conversation.topic","target":3}""", "other.actions"),
        ]);

        Assert.True(inbox.Activated(Encoding.UTF8.GetBytes("test.leave")));
        Assert.False(inbox.Activated(Encoding.UTF8.GetBytes("test.use")));
        Assert.Equal(["service.buy", "conversation.topic"], inbox.Actions.Select(action => action.Name));

        UiAction buy = inbox.Actions[0];
        Assert.Equal("7", buy.Text("target"));
        Assert.Equal(2, buy.Int("count"));
        Assert.Null(buy.Int("target"));
        Assert.Equal(string.Empty, buy.Text("member"));

        // A number where text is asked for reads as its own digits, and a field of another shape reads as absent.
        Assert.Equal("3", inbox.Actions[1].Text("target"));
    }

    [Fact]
    public void A_reader_takes_only_its_own_contract_and_names_and_what_is_left_is_visible()
    {
        ActionInbox inbox = new(
        [
            Payload("""{"action":"service.buy","target":"7"}"""),
            Payload("""{"action":"service.haggle"}"""),
            Payload("""{"action":"service.buy","target":"8"}""", "other.actions"),
        ]);

        IReadOnlyList<UiAction> taken = inbox.Take(Contract, "service.buy");

        Assert.Equal("7", Assert.Single(taken).Text("target"));
        Assert.True(taken[0].Claimed);

        // What nobody took on this session's own contract is left for the session to report; another contract's
        // actions belong to somebody else.
        UiAction left = Assert.Single(inbox.Unclaimed(new HashSet<string>(StringComparer.Ordinal) { Contract }));
        Assert.Equal("service.haggle", left.Name);
    }

    [Fact]
    public void A_session_reports_an_action_nothing_in_it_took()
    {
        RecordingDiagnosticsService diagnostics = new();
        using RecordingUiProjectionChannel channel = new();
        using PartyRpgSession session = new(
            new SessionComposition(new RulesetId("test.ruleset"), "Test"),
            channel,
            new SessionOwners(diagnostics: diagnostics),
            SessionParty.Nobody,
            controls: new SessionControls { Service = new ServiceIntentNames("test.leave", Contract) });
        session.Start();

        // A purchase with no counter open, and a name no mechanism knows: neither is silently dropped. The host's
        // own pause belongs to the host's router, and is not the session's to report.
        session.Update(Update(
            Payload("""{"action":"service.buy","target":"7"}"""),
            Payload("""{"action":"party.dance"}"""),
            Payload("""{"action":"session.pause"}""")));

        Assert.Equal(
            ["service.buy", "party.dance"],
            diagnostics.Published
                .Where(report => report.Code == "action-unclaimed")
                .Select(report => report.Message.Split('\'')[1]));
        Assert.All(
            diagnostics.Published.Where(report => report.Code == "action-unclaimed"),
            report => Assert.Equal(DiagnosticsSeverity.Warning, report.Severity));
    }

    /// <summary>One admitted step carrying the input given.</summary>
    private static ProductUpdate Update(params ProductInputEvent[] input) => Admitted.Update(1, 1, input);

    /// <summary>One payload action, on this suite's contract unless a case names another.</summary>
    private static ProductInputEvent Payload(string json, string contract = Contract) => Admitted.Payload(contract, json);
}
