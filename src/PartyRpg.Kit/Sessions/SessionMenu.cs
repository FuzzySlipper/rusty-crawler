using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Presentation;

namespace PartyRpg.Kit.Sessions;

/// <summary>The visible product menu's screen.</summary>
public enum SessionMenuScreen
{
    /// <summary>The adventure is in front of the player; the menu launcher may still be shown.</summary>
    Adventure,

    /// <summary>The title menu is in front of the player.</summary>
    Title,

    /// <summary>The player must decide whether to leave an expedition with unsaved work.</summary>
    ConfirmReturn,

    /// <summary>The explicit save/load screen is in front of the expedition.</summary>
    SaveLoad,

    /// <summary>The player must decide whether to overwrite the one save slot.</summary>
    ConfirmOverwrite,

    /// <summary>The player must decide whether to discard unsaved work before loading.</summary>
    ConfirmLoad,
}

/// <summary>Ruleset/session-owned names for a saved document shown by the lifecycle menu.</summary>
/// <remarks>
/// The menu does not invent a place label or calendar. The session resolves the saved identity against the
/// world and calendar it already composes, while this value carries only the result across the Host boundary.
/// </remarks>
public sealed record SessionSaveMenuContext(string Place, string Calendar)
{
    /// <summary>A document's durable identities before a session adds its presentation names.</summary>
    public static SessionSaveMenuContext From(SessionSave save) => new(save.World.Pose.Place.Value, string.Empty);
}

/// <summary>The one save slot's player-readable context, projected by the menu.</summary>
/// <remarks>
/// The slot summary is read from the current save document, not from a second product cache. It gives a player
/// enough information to recognize the expedition before replacing the live one, while the document itself remains
/// the only durable state and the ruleset's existing codec remains the only reader.
/// </remarks>
public sealed record SessionSaveMenuSnapshot(
    bool Available,
    bool Present,
    string Slot,
    string Party,
    int Members,
    int Coins,
    int Provisions,
    string Place,
    string Calendar,
    string SavedAt,
    string State,
    string Code,
    string Message)
{
    /// <summary>The empty slot as a session with no persistence publishes it.</summary>
    public static SessionSaveMenuSnapshot Empty { get; } = new(
        Available: false,
        Present: false,
        Slot: SessionSaveBoundary.DefaultSlot,
        Party: string.Empty,
        Members: 0,
        Coins: 0,
        Provisions: 0,
        Place: string.Empty,
        Calendar: string.Empty,
        SavedAt: string.Empty,
        State: "none",
        Code: string.Empty,
        Message: string.Empty);

    /// <summary>Reads player-facing context from the one current save document.</summary>
    /// <param name="save">The document the existing persistence owner decoded.</param>
    /// <param name="savedAt">The calendar moment the save request reported, when the live session knew it.</param>
    public static SessionSaveMenuSnapshot From(SessionSave save, SessionSaveMenuContext context, string savedAt = "")
    {
        ArgumentNullException.ThrowIfNull(save);
        ArgumentNullException.ThrowIfNull(context);
        string party = string.Join(", ", save.Party.Members.Select(member => member.Seed.Name));
        return new(
            Available: true,
            Present: true,
            Slot: SessionSaveBoundary.DefaultSlot,
            Party: party,
            Members: save.Party.Members.Count,
            Coins: save.Party.Coins,
            Provisions: save.Party.FoodPortions,
            Place: context.Place,
            Calendar: context.Calendar,
            SavedAt: savedAt,
            State: "saved",
            Code: string.Empty,
            Message: string.Empty);
    }

    /// <summary>Writes the slot context inside the visible menu block.</summary>
    internal uint Write(Presentation.UiValueBuilder builder) =>
        builder.Object(
            ("available", builder.Boolean(Available)),
            ("present", builder.Boolean(Present)),
            ("slot", builder.String(Slot)),
            ("party", builder.String(Party)),
            ("members", builder.Number(Members)),
            ("coins", builder.Number(Coins)),
            ("provisions", builder.Number(Provisions)),
            ("place", builder.String(Place)),
            ("calendar", builder.String(Calendar)),
            ("savedAt", builder.String(SavedAt)),
            ("state", builder.String(State)),
            ("code", builder.String(Code)),
            ("message", builder.String(Message)));
}

/// <summary>
/// The product menu state a session publishes beside its ordinary adventure projection.
/// </summary>
/// <remarks>
/// The Host owns the lifecycle decisions and mutates this small state object. The session only reads it when it
/// publishes, so the menu is presentation and lifecycle state rather than a second session, clock, or update loop.
/// </remarks>
public sealed record SessionMenuSnapshot(
    bool Visible,
    SessionMenuScreen Screen,
    bool CanNewGame,
    bool CanContinue,
    bool CanReturnTitle,
    bool HasUnsaved,
    string State,
    string Code,
    string Message)
{
    /// <summary>The one slot's saved party/place/calendar context.</summary>
    public SessionSaveMenuSnapshot Save { get; init; } = SessionSaveMenuSnapshot.Empty;

    /// <summary>The menu is hidden over the adventure.</summary>
    public static SessionMenuSnapshot Adventure { get; } = new(
        Visible: false,
        Screen: SessionMenuScreen.Adventure,
        CanNewGame: false,
        CanContinue: false,
        CanReturnTitle: true,
        HasUnsaved: false,
        State: "none",
        Code: string.Empty,
        Message: string.Empty)
    {
        Save = SessionSaveMenuSnapshot.Empty,
    };

    /// <summary>Writes the top-level menu block in the session projection.</summary>
    internal uint Write(Presentation.UiValueBuilder builder) =>
        builder.Object(
            ("visible", builder.Boolean(Visible)),
            ("screen", builder.String(Screen switch
            {
                SessionMenuScreen.Adventure => "adventure",
                SessionMenuScreen.Title => "title",
                SessionMenuScreen.ConfirmReturn => "confirm-return",
                SessionMenuScreen.SaveLoad => "save-load",
                SessionMenuScreen.ConfirmOverwrite => "confirm-overwrite",
                SessionMenuScreen.ConfirmLoad => "confirm-load",
                _ => throw new ArgumentOutOfRangeException(nameof(Screen), Screen, "A menu screen has no wire name."),
            })),
            ("canNewGame", builder.Boolean(CanNewGame)),
            ("canContinue", builder.Boolean(CanContinue)),
            ("canReturnTitle", builder.Boolean(CanReturnTitle)),
            ("hasUnsaved", builder.Boolean(HasUnsaved)),
            ("state", builder.String(State)),
            ("code", builder.String(Code)),
            ("message", builder.String(Message)),
            ("save", Save.Write(builder)));
}

/// <summary>The mutable menu seam shared by the Host and the session it composes.</summary>
public sealed class SessionMenuState
{
    private SessionMenuSnapshot _snapshot;

    /// <summary>Creates the menu in the adventure state unless an explicit initial state is supplied.</summary>
    public SessionMenuState(SessionMenuSnapshot? initial = null) => _snapshot = initial ?? SessionMenuSnapshot.Adventure;

    /// <summary>The state the next session projection reads.</summary>
    public SessionMenuSnapshot Snapshot => _snapshot;

    /// <summary>Changes whenever the Host asks the session to publish a different menu state.</summary>
    internal long Revision { get; private set; }

    /// <summary>
    /// Whether the visible menu owns the current admitted update's controls.
    /// </summary>
    /// <remarks>
    /// The Host sets this before it handles a menu action and clears it after the one session update. It is
    /// deliberately transient rather than part of the projection: a menu action may close a screen in the same
    /// update, but the input that activated it still belongs to that screen and must not reach the session below.
    /// </remarks>
    public bool ControlsOwnedForUpdate { get; private set; }

    /// <summary>Marks the current admitted update as owned by the visible menu.</summary>
    /// <param name="owned">Whether the menu owns the update's controls.</param>
    public void SetControlsOwnedForUpdate(bool owned) => ControlsOwnedForUpdate = owned;

    /// <summary>Shows the title menu and its latest lifecycle message.</summary>
    public void ShowTitle(bool canNewGame, bool canContinue, string state = "none", string code = "", string message = "")
    {
        _snapshot = new SessionMenuSnapshot(
            Visible: true,
            Screen: SessionMenuScreen.Title,
            CanNewGame: canNewGame,
            CanContinue: canContinue,
            CanReturnTitle: false,
            HasUnsaved: false,
            State: state,
            Code: code,
            Message: message)
        {
            Save = _snapshot.Save,
        };
        Revision++;
    }

    /// <summary>Shows the confirmation before leaving the current expedition.</summary>
    public void ShowReturnConfirmation(bool unsaved = true, string? message = null)
    {
        _snapshot = _snapshot with
        {
            Visible = true,
            Screen = SessionMenuScreen.ConfirmReturn,
            CanNewGame = false,
            CanContinue = false,
            CanReturnTitle = true,
            HasUnsaved = unsaved,
            State = "confirm",
            Code = unsaved ? "return-unsaved" : "return-title",
            Message = message ?? (unsaved
                ? "Unsaved progress will be lost. Return to the title screen?"
                : "Return to the title screen?"),
        };
        Revision++;
    }

    /// <summary>Shows the explicit save/load screen without replacing the live session.</summary>
    public void ShowSaveLoad(bool unsaved)
    {
        _snapshot = _snapshot with
        {
            Visible = true,
            Screen = SessionMenuScreen.SaveLoad,
            CanNewGame = false,
            CanContinue = false,
            CanReturnTitle = true,
            HasUnsaved = unsaved,
            State = _snapshot.Save.State,
            Code = _snapshot.Save.Code,
            Message = _snapshot.Save.Message,
        };
        Revision++;
    }

    /// <summary>Shows the deliberate overwrite decision for the one existing slot.</summary>
    public void ShowOverwriteConfirmation()
    {
        _snapshot = _snapshot with
        {
            Visible = true,
            Screen = SessionMenuScreen.ConfirmOverwrite,
            CanNewGame = false,
            CanContinue = false,
            CanReturnTitle = true,
            State = "confirm",
            Code = "save-overwrite",
            Message = $"Slot '{_snapshot.Save.Slot}' already contains an expedition. Overwrite it?",
        };
        Revision++;
    }

    /// <summary>Shows the deliberate discard decision before loading over unsaved work.</summary>
    public void ShowLoadConfirmation()
    {
        _snapshot = _snapshot with
        {
            Visible = true,
            Screen = SessionMenuScreen.ConfirmLoad,
            CanNewGame = false,
            CanContinue = false,
            CanReturnTitle = true,
            HasUnsaved = true,
            State = "confirm",
            Code = "load-unsaved",
            Message = "Unsaved progress will be discarded. Load the saved expedition?",
        };
        Revision++;
    }

    /// <summary>Records a save document in the menu's transient projection cache.</summary>
    /// <remarks>The document remains durable only in the existing Engine store.</remarks>
    public void RecordSaved(SessionSave save, SessionSaveMenuContext context, string savedAt)
    {
        SessionSaveMenuSnapshot summary = SessionSaveMenuSnapshot.From(save, context, savedAt) with
        {
            Message = $"Saved the expedition to slot '{SessionSaveBoundary.DefaultSlot}'.",
        };
        bool confirmingOverwrite = _snapshot.Screen == SessionMenuScreen.ConfirmOverwrite;
        _snapshot = _snapshot with
        {
            Save = summary,
            Screen = confirmingOverwrite ? SessionMenuScreen.SaveLoad : _snapshot.Screen,
            HasUnsaved = false,
            State = _snapshot.Screen is SessionMenuScreen.SaveLoad or SessionMenuScreen.ConfirmOverwrite ? "saved" : _snapshot.State,
            Code = _snapshot.Screen is SessionMenuScreen.SaveLoad or SessionMenuScreen.ConfirmOverwrite ? string.Empty : _snapshot.Code,
            Message = _snapshot.Screen is SessionMenuScreen.SaveLoad or SessionMenuScreen.ConfirmOverwrite
                ? $"Saved the expedition to slot '{summary.Slot}'."
                : _snapshot.Message,
        };
        Revision++;
    }

    /// <summary>Records a refused save while preserving the existing usable session and slot context.</summary>
    public void RecordSaveFailure(bool available, string slot, string code, string message)
    {
        _snapshot = _snapshot with
        {
            Save = _snapshot.Save with
            {
                Available = available,
                Slot = slot,
                State = "failed",
                Code = code,
                Message = message,
            },
            State = _snapshot.Screen is SessionMenuScreen.SaveLoad or SessionMenuScreen.ConfirmOverwrite ? "failed" : _snapshot.State,
            Code = _snapshot.Screen is SessionMenuScreen.SaveLoad or SessionMenuScreen.ConfirmOverwrite ? code : _snapshot.Code,
            Message = _snapshot.Screen is SessionMenuScreen.SaveLoad or SessionMenuScreen.ConfirmOverwrite ? message : _snapshot.Message,
        };
        Revision++;
    }

    /// <summary>Records that the one slot was empty or could not be read.</summary>
    public void RecordLoadFailure(bool available, string slot, string code, string message, bool present = false)
    {
        _snapshot = _snapshot with
        {
            Save = _snapshot.Save with
            {
                Available = available,
                Present = present,
                Slot = slot,
                State = "failed",
                Code = code,
                Message = message,
            },
            State = _snapshot.Screen is SessionMenuScreen.SaveLoad or SessionMenuScreen.ConfirmLoad ? "failed" : _snapshot.State,
            Code = _snapshot.Screen is SessionMenuScreen.SaveLoad or SessionMenuScreen.ConfirmLoad ? code : _snapshot.Code,
            Message = _snapshot.Screen is SessionMenuScreen.SaveLoad or SessionMenuScreen.ConfirmLoad ? message : _snapshot.Message,
        };
        Revision++;
    }

    /// <summary>Records an empty slot while leaving the current expedition untouched.</summary>
    public void RecordEmptySlot(bool available, string slot, string message)
    {
        _snapshot = _snapshot with
        {
            Save = _snapshot.Save with
            {
                Available = available,
                Present = false,
                Slot = slot,
                State = "none",
                Code = string.Empty,
                Message = message,
                Party = string.Empty,
                Members = 0,
                Coins = 0,
                Provisions = 0,
                Place = string.Empty,
                Calendar = string.Empty,
                SavedAt = string.Empty,
            },
        };
        Revision++;
    }

    /// <summary>Updates the visible unsaved marker without changing the menu screen.</summary>
    public void SetUnsaved(bool unsaved)
    {
        if (_snapshot.HasUnsaved == unsaved) return;
        _snapshot = _snapshot with { HasUnsaved = unsaved };
        Revision++;
    }

    /// <summary>Hides the menu over the live adventure.</summary>
    public void ShowAdventure()
    {
        _snapshot = _snapshot with
        {
            Visible = false,
            Screen = SessionMenuScreen.Adventure,
            CanNewGame = false,
            CanContinue = false,
            CanReturnTitle = true,
            HasUnsaved = false,
            State = "none",
            Code = string.Empty,
            Message = string.Empty,
        };
        Revision++;
    }
}

/// <summary>Lifecycle actions sent by the visible title/menu surface.</summary>
public static class SessionMenuActions
{
    /// <summary>Starts a fresh expedition through the existing creation/session path.</summary>
    public const string NewGame = "session.new-game";

    /// <summary>Loads the current save through the existing ruleset persistence owner.</summary>
    public const string Continue = "session.continue";

    /// <summary>Opens the leave-unsaved confirmation.</summary>
    public const string ReturnTitle = "session.return-title";

    /// <summary>Confirms returning to the title menu.</summary>
    public const string ConfirmReturnTitle = "session.confirm-return-title";

    /// <summary>Closes the leave-unsaved confirmation and resumes the expedition.</summary>
    public const string CancelReturnTitle = "session.cancel-return-title";

    /// <summary>Opens the explicit save/load screen over the expedition.</summary>
    public const string OpenSaveLoad = "session.open-save-load";

    /// <summary>Closes the explicit save/load screen.</summary>
    public const string CloseSaveLoad = "session.close-save-load";

    /// <summary>Asks to load the one saved expedition.</summary>
    public const string Load = "session.load";

    /// <summary>Confirms discarding current progress before loading.</summary>
    public const string ConfirmLoad = "session.confirm-load";

    /// <summary>Cancels the discard decision and returns to the save/load screen.</summary>
    public const string CancelLoad = "session.cancel-load";

    /// <summary>Cancels overwriting the one existing save slot.</summary>
    public const string CancelOverwrite = "session.cancel-overwrite";

    /// <summary>Whether the name is owned by the Host menu lifecycle.</summary>
    public static bool IsMenuAction(string name) => name is
        NewGame or Continue or ReturnTitle or ConfirmReturnTitle or CancelReturnTitle or
        OpenSaveLoad or CloseSaveLoad or Load or ConfirmLoad or CancelLoad or CancelOverwrite;
}
