using System.Text;
using PartyRpg.Kit.Input;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Time;
using Rusty.Engine;

namespace PartyRpg.Kit.Sessions;

/// <summary>
/// The save controls a host declares, by the names a player's request to save arrives on.
/// </summary>
/// <remarks>
/// <para>
/// The names are data rather than vocabulary, exactly as the movement and creation controls are: the kit
/// claims what a product declares and invents no key of its own. A save request is a request and nothing
/// more: what a save contains, where it goes, and what makes it loadable are the persistence owner's rules.
/// </para>
/// <para>
/// A product offers two ways to ask: a digital intent for a key, and the kit's own action
/// (<see cref="SaveActions.Save"/>) on the payload contract the interface already claims its semantic actions on.
/// Both are read inside the one admitted update, so a key and a button ask for exactly the same save.
/// </para>
/// </remarks>
public sealed record SaveIntentNames
{
    /// <summary>Names the controls a save request arrives on.</summary>
    /// <param name="intent">The digital intent that asks the session to save.</param>
    /// <param name="actionContract">The payload contract the save action arrives on.</param>
    /// <exception cref="ArgumentException">A name is missing, so no event could ever be claimed for it.</exception>
    public SaveIntentNames(string intent, string actionContract)
    {
        Intent = Require(intent, nameof(intent));
        ActionContract = Require(actionContract, nameof(actionContract));
    }

    /// <summary>The digital intent that asks the session to save.</summary>
    public string Intent { get; }

    /// <summary>The payload contract that action arrives on.</summary>
    public string ActionContract { get; }

    private static string Require(string name, string parameterName) =>
        !string.IsNullOrWhiteSpace(name)
            ? name
            : throw new ArgumentException(
                $"The save control '{parameterName}' declares no name, so no event could ever be claimed for it.",
                parameterName);
}

/// <summary>The payload action a screen sends to ask for a save.</summary>
public static class SaveActions
{
    /// <summary>Asks the session to save, exactly as the save key does.</summary>
    public const string Save = "session.save";

    /// <summary>Asks the session to save from the explicit save/load screen.</summary>
    public const string MenuSave = "session.menu-save";
}

/// <summary>
/// A session's saves: the explicit boundary they are written at, the controls a player asks on, and what the
/// last request came to.
/// </summary>
/// <remarks>
/// A save happens when a player asks for one and at no other time. A request read from an admitted update is
/// settled before that update steps anything, so the document describes the session the player was looking at
/// when they asked, and every way it can fail is an outcome the panel shows rather than an exception thrown out
/// of the update: the request came from a player, not from a caller that demanded a write.
/// </remarks>
internal sealed class SaveRequests
{
    private readonly string _title;
    private readonly byte[]? _intent;
    private readonly string? _actionContract;
    private readonly SessionMenuState _menu;
    private SessionSave? _written;

    public SaveRequests(string title, SessionSaving? saving, SaveIntentNames? names, bool resumed, SessionMenuState menu)
    {
        ArgumentNullException.ThrowIfNull(menu);
        _title = title;
        _menu = menu;
        Store = saving?.Store;
        Boundary = saving is null ? null : new SessionSaveBoundary(saving.Store, saving.Slot);
        // The declared controls are read once, into the exact bytes an admitted event carries, so the reader
        // compares bytes rather than decoding a name on every event of every update.
        _intent = names is null ? null : Encoding.UTF8.GetBytes(names.Intent);
        _actionContract = names?.ActionContract;
        State = SaveSnapshot.None(
            available: Boundary is not null,
            resumed: resumed,
            slot: saving?.Slot ?? SessionSaveBoundary.DefaultSlot);
    }

    /// <summary>The store the session owns and releases with itself, or null when it has none.</summary>
    public ISessionSaveStore? Store { get; }

    /// <summary>The explicit boundary saves are written at, or null when the session has nowhere to write.</summary>
    public SessionSaveBoundary? Boundary { get; }

    /// <summary>What the last request came to, which the projection publishes.</summary>
    public SaveSnapshot State { get; private set; }

    /// <summary>
    /// Whether this update's admitted input asks for a save on either declared control.
    /// </summary>
    /// <remarks>
    /// A malformed payload carries no request: an input channel must not throw on hostile bytes. Several requests
    /// in one update are one save, because they are one moment.
    /// </remarks>
    public bool Asked(ActionInbox inbox)
    {
        if (_intent is null || _actionContract is null) return false;
        // An explicit Save/Load screen owns save controls, and ordinary adventure play keeps its deliberate
        // quick-save policy. A title, return confirmation, or a screen-closing update owns the input without
        // offering a save boundary, so a stale F key cannot write behind the visible transition.
        bool saveBoundaryOpen = !_menu.ControlsOwnedForUpdate ||
            _menu.Snapshot.Screen is SessionMenuScreen.SaveLoad or SessionMenuScreen.ConfirmOverwrite;
        bool confirmationPending = _menu.Snapshot.Screen is
            SessionMenuScreen.ConfirmOverwrite or
            SessionMenuScreen.ConfirmReturn or
            SessionMenuScreen.ConfirmLoad;
        bool asked = saveBoundaryOpen && !confirmationPending && inbox.Activated(_intent);
        IReadOnlyList<UiAction> actions = inbox.Take(_actionContract, name => name is SaveActions.Save or SaveActions.MenuSave);
        // The Host turns the first menu-save press into the overwrite screen before this session reads the
        // update. The confirmation button uses the same canonical action after the Host returns to SaveLoad,
        // so only that second press reaches this boundary.
        bool menuSave = saveBoundaryOpen && !confirmationPending && actions.Any(action => action.Name == SaveActions.MenuSave);
        bool ordinarySave = saveBoundaryOpen && !confirmationPending && actions.Any(action => action.Name == SaveActions.Save);
        return menuSave || ordinarySave || asked;
    }

    /// <summary>Saves the session if it can, and records what happened in <see cref="State"/>.</summary>
    /// <remarks>
    /// Every failure keeps its own name: a session composed without a store, a session holding something the
    /// save cannot carry, and a write the store refused are different losses a player acts on differently.
    /// </remarks>
    public void Attempt(PartyRpgSession session, GameClock? clock)
    {
        _written = null;
        State = Outcome(session, clock);
        if (State.IsSaved && _written is { } written)
            _menu.RecordSaved(written, session.DescribeSave(written), State.At);
        else if (State.IsFailed)
            _menu.RecordSaveFailure(State.Available, State.Slot, State.Code, State.Message);
    }

    /// <summary>Marks the live session changed after its last successful save.</summary>
    public void MarkChanged()
    {
        if (State.Dirty) return;
        State = State.Changed();
    }

    private SaveSnapshot Outcome(PartyRpgSession session, GameClock? clock)
    {
        if (Boundary is null)
        {
            return State with
            {
                State = SaveState.Failed,
                At = string.Empty,
                Code = "save-unavailable",
                Message = $"The session in '{_title}' cannot be saved: it was composed without a save store, so there is nowhere to write one. The host selects the persistence root before the product is created.",
            };
        }

        try
        {
            _written = Boundary.Save(session);
        }
        catch (EngineCallException error)
        {
            // The engine's own service refused the write after its store was open; the player can try again.
            return State with
            {
                State = SaveState.Failed,
                At = string.Empty,
                Code = "save-failed",
                Message = $"The session could not be written to slot '{State.Slot}': {error.Message}",
            };
        }
        catch (SessionSaveException error)
        {
            return State with
            {
                State = SaveState.Failed,
                At = string.Empty,
                Code = error.Kind switch
                {
                    SessionSaveFailure.Refused => "save-refused",
                    SessionSaveFailure.Unavailable => "save-unavailable",
                    _ => "save-failed",
                },
                Message = error.Message,
            };
        }

        ClockSnapshot read = ClockSnapshot.From(clock);
        string at = read.Present ? $"{read.Date} {read.Time}" : string.Empty;
        return State with
        {
            State = SaveState.Saved,
            At = at,
            Code = string.Empty,
            Message = at.Length > 0
                ? $"Saved the session to slot '{State.Slot}' at {at}."
                : $"Saved the session to slot '{State.Slot}'.",
            Dirty = false,
        };
    }
}
