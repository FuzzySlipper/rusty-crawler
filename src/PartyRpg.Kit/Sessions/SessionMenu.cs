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
        Message: string.Empty);

    /// <summary>Writes the top-level menu block in the session projection.</summary>
    internal uint Write(Presentation.UiValueBuilder builder) =>
        builder.Object(
            ("visible", builder.Boolean(Visible)),
            ("screen", builder.String(Screen switch
            {
                SessionMenuScreen.Adventure => "adventure",
                SessionMenuScreen.Title => "title",
                SessionMenuScreen.ConfirmReturn => "confirm-return",
                _ => throw new ArgumentOutOfRangeException(nameof(Screen), Screen, "A menu screen has no wire name."),
            })),
            ("canNewGame", builder.Boolean(CanNewGame)),
            ("canContinue", builder.Boolean(CanContinue)),
            ("canReturnTitle", builder.Boolean(CanReturnTitle)),
            ("hasUnsaved", builder.Boolean(HasUnsaved)),
            ("state", builder.String(State)),
            ("code", builder.String(Code)),
            ("message", builder.String(Message)));
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
            Message: message);
        Revision++;
    }

    /// <summary>Shows the confirmation before leaving the current expedition.</summary>
    public void ShowReturnConfirmation(string message = "Unsaved progress will be lost. Return to the title screen?")
    {
        _snapshot = _snapshot with
        {
            Visible = true,
            Screen = SessionMenuScreen.ConfirmReturn,
            CanNewGame = false,
            CanContinue = false,
            CanReturnTitle = true,
            HasUnsaved = true,
            State = "confirm",
            Code = "return-unsaved",
            Message = message,
        };
        Revision++;
    }

    /// <summary>Hides the menu over the live adventure.</summary>
    public void ShowAdventure()
    {
        _snapshot = SessionMenuSnapshot.Adventure;
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

    /// <summary>Whether the name is owned by the Host menu lifecycle.</summary>
    public static bool IsMenuAction(string name) => name is
        NewGame or Continue or ReturnTitle or ConfirmReturnTitle or CancelReturnTitle;
}
