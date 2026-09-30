using System.Text;
using PartyRpg.Kit.Input;
using Rusty.Engine;

namespace PartyRpg.Kit.Sessions;

/// <summary>The controls a product declares for pacing a fight: the toggle and the two turn actions.</summary>
/// <remarks>
/// <para>
/// Names are data rather than vocabulary, exactly as the movement, act, service, rest, and conversation
/// controls are: the kit claims what a product declares and invents no key of its own. The toggle is its own
/// control because switching the pacing is its own act — one flag decides the pacing and the game's
/// manual gives it a key of its own (the manual's own account of the toggle, p.33: "Enter toggles real-time
/// and turn-based at any moment").
/// </para>
/// <para>
/// <b>Skipping and waiting are separate controls</b> because they have different consequences: a skipped turn
/// forfeits the round and owes the action that was not taken, and a waited turn is deferred to the end of the
/// round and owes nothing. One "pass" control would have to guess which of the two a player meant.
/// </para>
/// <para>
/// Attacking is deliberately not declared here: the act control is the fight's own, and in a paced fight it
/// means the same act for the actor whose turn it is. What this record adds is only what a paced fight needs
/// beyond acting.
/// </para>
/// </remarks>
public sealed record TurnIntentNames
{
    /// <summary>Creates the declared pace control names.</summary>
    /// <param name="toggle">The intent that switches between real-time and turn-based pacing.</param>
    /// <param name="skip">The intent that forfeits the current actor's turn for the rest of the round.</param>
    /// <param name="wait">The intent that defers the current actor's turn to the end of the round.</param>
    /// <param name="actionContract">The payload contract a screen's own pace controls arrive on.</param>
    /// <exception cref="ArgumentException">A name is missing, so no event could ever be claimed for it.</exception>
    public TurnIntentNames(string toggle, string skip, string wait, string actionContract)
    {
        Toggle = Require(toggle, nameof(toggle));
        Skip = Require(skip, nameof(skip));
        Wait = Require(wait, nameof(wait));
        ActionContract = Require(actionContract, nameof(actionContract));
    }

    /// <summary>The intent that switches the pacing.</summary>
    public string Toggle { get; }

    /// <summary>The intent that forfeits the current actor's turn for the rest of the round.</summary>
    public string Skip { get; }

    /// <summary>The intent that defers the current actor's turn to the end of the round.</summary>
    public string Wait { get; }

    /// <summary>The payload contract a screen's own pace controls arrive on.</summary>
    public string ActionContract { get; }

    private static string Require(string name, string parameterName) =>
        !string.IsNullOrWhiteSpace(name)
            ? name
            : throw new ArgumentException(
                $"The pace control '{parameterName}' declares no name, so no event could ever be claimed for it.",
                parameterName);
}

/// <summary>The pace actions a screen sends, on the payload contract the product declares.</summary>
/// <remarks>
/// These are wire names, not rules: the screen reports what the player asked for, the session reads it, and
/// the fight decides what it means for the actor whose turn it is. Every one of them is also the name of a
/// declared intent, so a key and a button ask for exactly the same act, and a name here that the session
/// never reads is a control that does nothing — which is why the suite exercises each of them.
/// </remarks>
public static class TurnActions
{
    /// <summary>Switches between real-time and turn-based pacing.</summary>
    public const string Toggle = "combat.turn-based";

    /// <summary>Forfeits the current actor's turn for the rest of the round.</summary>
    public const string Skip = "combat.turn-skip";

    /// <summary>Defers the current actor's turn to the end of the round.</summary>
    public const string Wait = "combat.turn-wait";
}

/// <summary>What one update's admitted input asks of a paced fight.</summary>
/// <remarks>
/// This is what the player committed, not what the fight did with it: the toggle is applied where the mode
/// is resolved, and a skipped or waited turn is applied to whoever holds the turn when the update gets
/// there. An action that arrives when nobody holds a turn is therefore not an error — it names a turn the
/// session is not taking — and the session reports it as the refusal it is.
/// </remarks>
/// <param name="Toggle">Whether the player asked to switch the pacing.</param>
/// <param name="Skip">Whether the player asked to forfeit the current turn for the rest of the round.</param>
/// <param name="Wait">Whether the player asked to defer the current turn to the end of the round.</param>
public readonly record struct TurnControls(bool Toggle, bool Skip, bool Wait)
{
    /// <summary>Nothing was asked of the pacing.</summary>
    public static TurnControls None => default;

    /// <summary>Whether anything at all was asked.</summary>
    public bool Any => Toggle || Skip || Wait;
}

/// <summary>The turn controls a paced fight is taken with, read from the admitted input of each update.</summary>
/// <remarks>Each is a decision rather than a state, so a press asks and a held key asks nothing more.</remarks>
public sealed class TurnInput
{
    private readonly byte[] _toggle;
    private readonly byte[] _skip;
    private readonly byte[] _wait;
    private readonly string _actionContract;

    /// <summary>Creates the reader for the declared turn controls.</summary>
    /// <param name="names">The turn controls the host declared.</param>
    public TurnInput(TurnIntentNames names)
    {
        ArgumentNullException.ThrowIfNull(names);
        _toggle = Encoding.UTF8.GetBytes(names.Toggle);
        _skip = Encoding.UTF8.GetBytes(names.Skip);
        _wait = Encoding.UTF8.GetBytes(names.Wait);
        _actionContract = names.ActionContract;
    }

    /// <summary>The turn controls this update carried.</summary>
    /// <param name="inbox">The update's input.</param>
    public TurnControls Read(ActionInbox inbox)
    {
        ArgumentNullException.ThrowIfNull(inbox);
        bool toggle = inbox.Activated(_toggle);
        bool skip = inbox.Activated(_skip);
        bool wait = inbox.Activated(_wait);
        foreach (UiAction action in inbox.Take(_actionContract, name => name is TurnActions.Toggle or TurnActions.Skip or TurnActions.Wait))
        {
            toggle |= action.Name == TurnActions.Toggle;
            skip |= action.Name == TurnActions.Skip;
            wait |= action.Name == TurnActions.Wait;
        }

        return new TurnControls(toggle, skip, wait);
    }
}
