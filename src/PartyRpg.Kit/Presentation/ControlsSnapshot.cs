using System.Text;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Input;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Sessions;
using Rusty.Engine;

namespace PartyRpg.Kit.Presentation;

/// <summary>The keys a host bound its controls to, as a person reads them.</summary>
/// <remarks>
/// <para>
/// <b>The host is the only owner of a key.</b> Which key presses which control is declared in the host's
/// project and handed to the product by the engine at creation; the host reads that declaration once and
/// states it here per control, so a screen that tells a player "save with the F key" prints what the engine
/// was actually told rather than a letter a screen remembered. A control the host bound to no key has an empty
/// label, and a screen then names the button alone.
/// </para>
/// <para>
/// The labels are words for a person, not the engine's own names: <see cref="Label"/> is the one reading, so a
/// letter is its letter and a named key is its name.
/// </para>
/// </remarks>
public sealed record ControlKeys
{
    /// <summary>No control bound to any key: a screen names every button alone.</summary>
    public static ControlKeys None { get; } = new();

    /// <summary>The key that holds or releases the session.</summary>
    public string Pause { get; init; } = string.Empty;

    /// <summary>The key that asks for a save.</summary>
    public string Save { get; init; } = string.Empty;

    /// <summary>The key that uses what the party faces.</summary>
    public string Use { get; init; } = string.Empty;

    /// <summary>The key that orders the party to attack.</summary>
    public string Attack { get; init; } = string.Empty;

    /// <summary>The key that cycles the member an ordinary attack commands.</summary>
    public string NextMember { get; init; } = string.Empty;

    /// <summary>The key that switches the fight's pacing.</summary>
    public string TurnBased { get; init; } = string.Empty;

    /// <summary>The key that skips the current turn.</summary>
    public string TurnSkip { get; init; } = string.Empty;

    /// <summary>The key that defers the current turn to the round's end.</summary>
    public string TurnWait { get; init; } = string.Empty;

    /// <summary>The key that rests and heals.</summary>
    public string Rest { get; init; } = string.Empty;

    /// <summary>The key that makes camp.</summary>
    public string Camp { get; init; } = string.Empty;

    /// <summary>The key that waits until dawn.</summary>
    public string WaitDawn { get; init; } = string.Empty;

    /// <summary>The key that waits an hour.</summary>
    public string WaitHour { get; init; } = string.Empty;

    /// <summary>The key that waits five minutes.</summary>
    public string WaitFiveMinutes { get; init; } = string.Empty;

    /// <summary>The key that leaves a counter.</summary>
    public string ServiceLeave { get; init; } = string.Empty;

    /// <summary>The key that leaves a conversation.</summary>
    public string ConversationLeave { get; init; } = string.Empty;

    /// <summary>The key that confirms the creation step being made.</summary>
    public string CreationAdvance { get; init; } = string.Empty;

    /// <summary>The key that accepts a finished party.</summary>
    public string CreationAccept { get; init; } = string.Empty;

    /// <summary>What a person calls one keyboard control: a letter or a digit as itself, a named key by its name.</summary>
    /// <param name="key">The engine's keyboard control.</param>
    /// <returns>The label, empty for no key at all.</returns>
    public static string Label(KeyboardControl key)
    {
        if (key == KeyboardControl.None) return string.Empty;
        string name = key.ToString();
        if (name.Length == 4 && name.StartsWith("Key", StringComparison.Ordinal)) return name[3..];
        if (name.Length == 6 && name.StartsWith("Digit", StringComparison.Ordinal)) return name[5..];

        // A named key is its own words: `Enter`, `Space`, `Arrow Up`, `Shift Left`.
        StringBuilder words = new(name.Length + 4);
        foreach (char letter in name)
        {
            if (char.IsUpper(letter) && words.Length > 0) words.Append(' ');
            words.Append(letter);
        }

        return words.ToString();
    }
}

/// <summary>One control a panel offers: the action it sends, whether the product would take it now, and its key.</summary>
/// <param name="Action">The payload action the control sends, empty when there is nothing for it to ask.</param>
/// <param name="Enabled">Whether the product would take the control now, which is when a screen offers it.</param>
/// <param name="Key">The key the host bound it to, as a person reads it, empty when none.</param>
public sealed record ControlSnapshot(string Action, bool Enabled, string Key)
{
    /// <summary>Writes one control: its action, whether it is offered, and its key.</summary>
    /// <param name="builder">The projection being built.</param>
    /// <returns>The control's node.</returns>
    internal uint Write(UiValueBuilder builder) =>
        builder.Object(
            ("action", builder.String(Action)),
            ("enabled", builder.Boolean(Enabled)),
            ("key", builder.String(Key)));
}

/// <summary>Every control a panel offers outside a list, with whether the product would take it now.</summary>
/// <remarks>
/// <para>
/// <b>Whether a control is offered is the product's answer, not a screen's.</b> Each rule here reads the same
/// session state the rest of the projection publishes — the mode, what the party faces, the round a fight is in
/// — and says whether pressing the control now is something the session would act on. A screen prints the
/// answer as an enabled or a disabled button and works none of it out, which is what keeps a control that
/// cannot work from looking like one that can without a second copy of the rule in the presentation.
/// </para>
/// <para>
/// The controls that name a row — a spell, a lot on a shelf, a mixture — carry their own answers on the rows
/// they belong to; these are the ones that stand on their own.
/// </para>
/// </remarks>
public sealed record ControlsSnapshot(
    ControlSnapshot Pause,
    ControlSnapshot Save,
    ControlSnapshot Use,
    ControlSnapshot Attack,
    ControlSnapshot TurnBased,
    ControlSnapshot TurnSkip,
    ControlSnapshot TurnWait,
    ControlSnapshot Rest,
    ControlSnapshot Camp,
    ControlSnapshot WaitDawn,
    ControlSnapshot WaitHour,
    ControlSnapshot WaitFiveMinutes,
    ControlSnapshot ServiceLeave,
    ControlSnapshot ConversationLeave,
    ControlSnapshot CreationAdvance,
    ControlSnapshot CreationAccept)
{
    /// <summary>The control that cycles the party's selected acting member.</summary>
    public ControlSnapshot NextMember { get; init; } = new(CombatActions.NextMember, false, string.Empty);

    /// <summary>Reads every control's answer from the session the snapshot describes.</summary>
    /// <param name="snapshot">The session as the projection publishes it.</param>
    /// <returns>Each control's action, whether it is offered, and its key.</returns>
    public static ControlsSnapshot Read(SessionSnapshot snapshot)
    {
        ControlKeys keys = snapshot.Keys;
        SessionMode mode = snapshot.Mode;

        // The pause control asks for whichever of the two a press would do: a paced fight is a running session
        // waiting on a turn, so it holds like any running one, and a session that is starting, creating, or
        // stopped has nothing to hold or release.
        ControlSnapshot pause = mode switch
        {
            SessionMode.Running or SessionMode.TurnBased => new(UiActionPayload.PauseSession, true, keys.Pause),
            SessionMode.Paused => new(UiActionPayload.ResumeSession, true, keys.Pause),
            _ => new(string.Empty, false, keys.Pause),
        };

        // A running, held, or turn-waiting session can save where a store exists. A paced fight is carried
        // through the same boundary; a party still being made has no playing state to capture.
        bool saves = snapshot.Save.Available && mode is SessionMode.Running or SessionMode.Paused or SessionMode.TurnBased;

        // A use is an instant, so a held session may still use what it faces: what decides it is whether the
        // session holds the mechanism, whether anything is faced, and whether a party exists to use it.
        InteractionSnapshot interaction = snapshot.Interaction;
        bool uses = interaction.Available
            && !string.IsNullOrEmpty(interaction.Label)
            && mode is not (SessionMode.Creating or SessionMode.Stopped);

        // The act control is offered exactly when the fight would take the order. Outside a round that is when
        // somebody may act; inside one it is the party's own turn, or its movement phase, which the act ends —
        // and there it stays offered even while the acting member still owes recovery, because the refusal is
        // then the answer a player has to see.
        CombatSnapshot combat = snapshot.Combat;
        bool round = combat.Turn is { Phase: TurnPhase.Action or TurnPhase.Movement };
        bool acts = round
            ? combat.Turn is { Phase: TurnPhase.Movement } or { PlayerTurn: true }
            : combat.Members.Any(member => member.Selected);
        bool paced = combat.Pacing == CombatPacing.TurnBased;

        // Skipping and waiting pass a turn, so they are offered while a paced round is under way and at no
        // other time: outside one there is no turn of the player's to pass.
        bool passes = combat.Available && paced && round;
        bool rests = snapshot.Rest.Available;
        bool creating = snapshot.Creation is { Active: true };

        return new ControlsSnapshot(
            pause,
            new(SaveActions.Save, saves, keys.Save),
            new(UseActions.Use, uses, keys.Use),
            new(CombatActions.Attack, combat.Available && acts, keys.Attack),
            new(TurnActions.Toggle, combat.Available, keys.TurnBased),
            new(TurnActions.Skip, passes, keys.TurnSkip),
            new(TurnActions.Wait, passes, keys.TurnWait),
            new(RestActions.Rest, rests, keys.Rest),
            new(RestActions.Camp, rests, keys.Camp),
            new(RestActions.WaitUntilDawn, rests, keys.WaitDawn),
            new(RestActions.WaitAnHour, rests, keys.WaitHour),
            new(RestActions.WaitFiveMinutes, rests, keys.WaitFiveMinutes),
            new(ServiceActions.Leave, snapshot.Service.Open, keys.ServiceLeave),
            new(ConversationActions.Leave, snapshot.Conversation.Open, keys.ConversationLeave),
            new(CreationActions.Advance, creating, keys.CreationAdvance),
            new(CreationActions.Accept, creating, keys.CreationAccept))
        {
            NextMember = new(CombatActions.NextMember, combat.Available && combat.Members.Count > 0, keys.NextMember),
        };
    }

    /// <summary>Writes the controls block: each stand-alone control's action, whether it is offered, and its key.</summary>
    /// <param name="builder">The projection being built.</param>
    /// <returns>The block's node.</returns>
    internal uint Write(UiValueBuilder builder) =>
        builder.Object(
            ("pause", Pause.Write(builder)),
            ("save", Save.Write(builder)),
            ("use", Use.Write(builder)),
            ("attack", Attack.Write(builder)),
            ("nextMember", NextMember.Write(builder)),
            ("turnBased", TurnBased.Write(builder)),
            ("turnSkip", TurnSkip.Write(builder)),
            ("turnWait", TurnWait.Write(builder)),
            ("rest", Rest.Write(builder)),
            ("camp", Camp.Write(builder)),
            ("waitDawn", WaitDawn.Write(builder)),
            ("waitHour", WaitHour.Write(builder)),
            ("waitFiveMinutes", WaitFiveMinutes.Write(builder)),
            ("serviceLeave", ServiceLeave.Write(builder)),
            ("conversationLeave", ConversationLeave.Write(builder)),
            ("creationAdvance", CreationAdvance.Write(builder)),
            ("creationAccept", CreationAccept.Write(builder)));
}
