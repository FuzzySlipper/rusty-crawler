using System.Globalization;
using System.Text;
using PartyRpg.Kit;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Sessions;
using Rusty.Engine;
using Rusty.Engine.Debugging;

namespace PartyRpg.Host;

/// <summary>
/// The product's answers to the Engine's playtest commands — <c>playtest.observe</c>, <c>playtest.action</c>,
/// and <c>playtest.look</c> — each resolved from whatever session the product holds at the moment it is asked.
/// </summary>
/// <remarks>
/// <para>
/// <b>Observation is the projection's own reading.</b> What a harness observes is the session's snapshot — the
/// one the panel is built from — written by the kit's <see cref="PlaytestReadout"/>, so a live check reads where
/// the party stands, what it faces, and who is hostile without scraping the panel and without a second opinion.
/// </para>
/// <para>
/// <b>Actions are the declared intents and their keys.</b> Every digital intent the project file binds to a
/// key is an action, named by its intent and described with the physical key the engine was told — the same
/// declaration the panel names its keys from. Whether the session would take it now is the controls block's
/// own answer for the controls that block carries, the session's steering rule for the movement controls, and that rule
/// with the party's own flight for the controls that rise and sink,
/// so availability here and an enabled button on the panel cannot disagree. A query never presses anything: a
/// harness performs an action by pressing its key, and the session's ordinary input path decides what happens.
/// </para>
/// <para>
/// <b>Durations are input windows, not rule timings.</b> Movement here is continuous, so a held control has no
/// natural length; it is described with a short window a harness may override, and a pressed control with one
/// that covers the step that reads its edge. Neither is a claim about how long an act takes in game time.
/// </para>
/// </remarks>
internal static class ProductPlaytest
{
    /// <summary>The window a held control is described with: a short walk or turn a harness may lengthen.</summary>
    private const double HoldMilliseconds = 250;

    /// <summary>The window a pressed control is described with, long enough for the step that reads its edge.</summary>
    private const double TapMilliseconds = 100;

    /// <summary>The intents the session's steering rule decides, because the movement owner reads them.</summary>
    private static readonly HashSet<string> Steered = new(StringComparer.Ordinal)
    {
        ProductIdentity.MoveForwardIntent,
        ProductIdentity.MoveBackIntent,
        ProductIdentity.StrafeLeftIntent,
        ProductIdentity.StrafeRightIntent,
        ProductIdentity.JumpIntent,
    };

    /// <summary>The intents that rise and sink, which the session's steering rule and the party's flight decide.</summary>
    private static readonly HashSet<string> Rising = new(StringComparer.Ordinal)
    {
        ProductIdentity.AscendIntent,
        ProductIdentity.DescendIntent,
    };

    /// <summary>One declared control: its intent, its physical key, and whether the engine reads it held.</summary>
    internal sealed record Binding(string Intent, string Key, bool Hold);

    /// <summary>Reads the controls the engine's keyboard mappings declare, in declaration order.</summary>
    /// <param name="input">The input configuration the engine created the product with.</param>
    /// <returns>One binding per intent; an intent bound to several keys is described by its first.</returns>
    internal static IReadOnlyList<Binding> Bindings(ProductInputConfiguration input)
    {
        ArgumentNullException.ThrowIfNull(input);
        List<Binding> bindings = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (ProductInputMapping mapping in input.PhysicalMappings.Span)
        {
            if (mapping.TriggerKind != InputTriggerKind.Key || mapping.Keyboard == KeyboardControl.None) continue;
            string intent = Encoding.UTF8.GetString(mapping.Intent.Span);
            if (!seen.Add(intent)) continue;

            // The keyboard control's own name is the browser's physical key code — `KeyW`, `Space`, `Enter` —
            // which is what a harness presses.
            bindings.Add(new Binding(intent, mapping.Keyboard.ToString(), mapping.Edge == InputEdge.Held));
        }

        return bindings;
    }

    /// <summary>Creates the playtest module over the product's current session.</summary>
    /// <param name="bindings">The declared controls.</param>
    /// <param name="session">Reads the session the product holds now.</param>
    /// <returns>The module the generated catalog dispatches the playtest commands to.</returns>
    internal static PlaytestDebugModule Module(IReadOnlyList<Binding> bindings, Func<IGameSession> session)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(session);
        return new PlaytestDebugModule(
            () => DebugCommandResult.Success(PlaytestReadout.Observe(session().Inspect())),
            id => Action(bindings, session().Inspect(), id),
            [.. bindings.Select(binding => binding.Intent)],
            (yaw, pitch) => Look(session(), yaw, pitch));
    }

    /// <summary>Describes one declared action as the session would take it now.</summary>
    /// <param name="bindings">The declared controls.</param>
    /// <param name="snapshot">The session as its projection reads it.</param>
    /// <param name="id">The intent the action is named by.</param>
    /// <returns>The action's key, window, and current availability.</returns>
    internal static PlaytestAction Action(IReadOnlyList<Binding> bindings, SessionSnapshot snapshot, string id)
    {
        if (bindings.FirstOrDefault(binding => binding.Intent == id) is not { } bound)
        {
            return new PlaytestAction(id, string.Empty, 0, Hold: false, Available: false,
                Reason: $"'{id}' is not a declared keyboard control; the controls are {string.Join(", ", bindings.Select(binding => binding.Intent))}.");
        }

        (bool available, string? reason) = Availability(snapshot, id);
        return new PlaytestAction(
            id,
            bound.Key,
            bound.Hold ? HoldMilliseconds : TapMilliseconds,
            bound.Hold,
            available,
            reason);
    }

    /// <summary>Whether the session would take a control now, and why not when it would not.</summary>
    private static (bool Available, string? Reason) Availability(SessionSnapshot snapshot, string intent)
    {
        if (intent is ProductIdentity.TurnLeftIntent or ProductIdentity.TurnRightIntent)
        {
            return PlaytestReadout.Turning(snapshot) is { } refused ? (false, refused.Message) : (true, null);
        }

        if (Rising.Contains(intent))
        {
            return PlaytestReadout.Rising(snapshot) is { } grounded ? (false, grounded.Message) : (true, null);
        }

        if (Steered.Contains(intent))
        {
            return PlaytestReadout.Steering(snapshot) is { } refused ? (false, refused.Message) : (true, null);
        }

        ControlsSnapshot controls = ControlsSnapshot.Read(snapshot);
        ControlSnapshot? control = intent switch
        {
            ProductIdentity.PauseToggleIntent => controls.Pause,
            ProductIdentity.SaveIntent => controls.Save,
            ProductIdentity.UseIntent => controls.Use,
            ProductIdentity.NextTargetIntent => controls.NextTarget,
            ProductIdentity.AttackIntent => controls.Attack,
            ProductIdentity.NextMemberIntent => controls.NextMember,
            ProductIdentity.TurnBasedToggleIntent => controls.TurnBased,
            ProductIdentity.TurnSkipIntent => controls.TurnSkip,
            ProductIdentity.TurnWaitIntent => controls.TurnWait,
            ProductIdentity.RestIntent => controls.Rest,
            ProductIdentity.CampIntent => controls.Camp,
            ProductIdentity.WaitUntilDawnIntent => controls.WaitDawn,
            ProductIdentity.WaitAnHourIntent => controls.WaitHour,
            ProductIdentity.WaitFiveMinutesIntent => controls.WaitFiveMinutes,
            ProductIdentity.ServiceLeaveIntent => controls.ServiceLeave,
            ProductIdentity.ConversationLeaveIntent => controls.ConversationLeave,
            ProductIdentity.CreationAdvanceIntent => controls.CreationAdvance,
            ProductIdentity.CreationAcceptIntent => controls.CreationAccept,
            _ => null,
        };

        return control switch
        {
            null => (false, $"'{intent}' is declared but the session states no answer for it."),
            { Enabled: true } => (true, null),
            _ => (false, $"The session would not take '{intent}' while it is {SessionProjection.WireName(snapshot.Mode)}: its controls block does not offer it now."),
        };
    }

    /// <summary>Turns the party by a relative look and reports the facing it now holds.</summary>
    private static DebugCommandResult Look(IGameSession session, double yawDegrees, double pitchDegrees)
    {
        if (session.Look(yawDegrees, pitchDegrees) is { } refused)
        {
            return DebugCommandResult.Failure(DebugCommandStatus.Failed, $"{refused.Code}: {refused.Message}");
        }

        PartyRpg.Kit.World.PlacePose pose = session.Inspect().World.Pose;
        return DebugCommandResult.Success(string.Create(
            CultureInfo.InvariantCulture,
            $$"""{"yaw":{{pose.Yaw}},"pitch":{{pose.Pitch}},"lookAdvancesTime":false}"""));
    }
}
