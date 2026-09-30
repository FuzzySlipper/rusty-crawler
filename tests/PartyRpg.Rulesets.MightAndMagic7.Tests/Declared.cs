using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Sessions;

namespace PartyRpg.Rulesets.MightAndMagic7.Tests;

/// <summary>
/// The names this suite composes the ruleset's sessions over, in place of the ones a host declares.
/// </summary>
/// <remarks>
/// The ruleset reads the intents and contracts a host hands it and names none of its own, so its suite states the
/// names it hands over: the kit's own action names for every control that has one, and a stream and contracts of
/// this suite's own. That the product declares these same controls, and binds each to its key, is the host
/// suite's business (<c>PartyRpg.Host.Tests/ControlDeclarationTests</c>).
/// </remarks>
internal static class Declared
{
    internal const string UiStream = "test.hud";

    internal const string UiContract = "test.ui.snapshot";

    internal const string UiActionContract = "test.ui.action";

    internal const string UseIntent = UseActions.Use;

    internal const string AttackIntent = CombatActions.Attack;

    internal const string AttackAction = CombatActions.Attack;

    internal const string CastAction = CastActions.Cast;

    internal const string CreationAdvanceIntent = CreationActions.Advance;

    internal const string CreationAcceptIntent = CreationActions.Accept;

    internal const string ServiceLeaveIntent = ServiceActions.Leave;

    internal const string ConversationLeaveIntent = ConversationActions.Leave;

    internal const string RestIntent = RestActions.Rest;

    internal const string CampIntent = RestActions.Camp;

    internal const string WaitUntilDawnIntent = RestActions.WaitUntilDawn;

    internal const string WaitAnHourIntent = RestActions.WaitAnHour;

    internal const string WaitFiveMinutesIntent = RestActions.WaitFiveMinutes;

    internal const string TurnBasedToggleIntent = TurnActions.Toggle;

    internal const string TurnSkipIntent = TurnActions.Skip;

    internal const string TurnWaitIntent = TurnActions.Wait;
}
