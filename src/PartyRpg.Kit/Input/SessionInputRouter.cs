using System.Text;
using PartyRpg.Kit.Sessions;
using Rusty.Engine;

namespace PartyRpg.Kit.Input;

/// <summary>One session-lifecycle request read from an admitted input event.</summary>
public enum SessionCommand
{
    /// <summary>The event carries no session request.</summary>
    None,

    /// <summary>Hold the session.</summary>
    Pause,

    /// <summary>Release a held session.</summary>
    Resume,

    /// <summary>Hold a running session or release a held one, whichever applies.</summary>
    TogglePause,
}

/// <summary>
/// Reads admitted input into session commands and applies them. The names of the product's intents
/// and payload contract are supplied at construction, so this mechanism stays rules- and
/// product-agnostic while the host keeps ownership of its own declared names.
/// </summary>
public sealed class SessionInputRouter
{
    private readonly byte[] _pauseToggleIntentUtf8;
    private readonly byte[] _uiActionContractUtf8;

    /// <summary>Creates a router for a product's declared pause-toggle intent and UI action contract.</summary>
    public SessionInputRouter(string pauseToggleIntent, string uiActionContract)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pauseToggleIntent);
        ArgumentException.ThrowIfNullOrWhiteSpace(uiActionContract);
        _pauseToggleIntentUtf8 = Encoding.UTF8.GetBytes(pauseToggleIntent);
        _uiActionContractUtf8 = Encoding.UTF8.GetBytes(uiActionContract);
    }

    /// <summary>
    /// Reads the session command an admitted input event carries. A digital press of the declared
    /// intent toggles; a payload on the declared contract names the action explicitly. Anything else,
    /// including a malformed payload, carries no command.
    /// </summary>
    public SessionCommand CommandFor(in ProductInputEvent inputEvent)
    {
        if (inputEvent.ValueKind == InputValueKind.Digital
            && inputEvent.Edge == InputEdge.Pressed
            && inputEvent.Intent.Span.SequenceEqual(_pauseToggleIntentUtf8))
        {
            return SessionCommand.TogglePause;
        }

        if (inputEvent.ValueKind == InputValueKind.ProductPayload
            && inputEvent.PayloadContract.Span.SequenceEqual(_uiActionContractUtf8))
        {
            return UiActionPayload.Parse(inputEvent.PayloadData.Span)?.Name switch
            {
                UiActionPayload.PauseSession => SessionCommand.Pause,
                UiActionPayload.ResumeSession => SessionCommand.Resume,
                _ => SessionCommand.None,
            };
        }

        return SessionCommand.None;
    }

    /// <summary>Applies every session command carried by an admitted input slice, in order.</summary>
    public void Apply(IGameSession session, ReadOnlySpan<ProductInputEvent> input)
    {
        ArgumentNullException.ThrowIfNull(session);
        foreach (ProductInputEvent inputEvent in input) Apply(session, CommandFor(inputEvent));
    }

    /// <summary>Applies one command to a session. A command that does not fit the current mode does nothing.</summary>
    public static void Apply(IGameSession session, SessionCommand command)
    {
        ArgumentNullException.ThrowIfNull(session);
        switch (command)
        {
            case SessionCommand.Pause:
                session.Pause();
                break;
            case SessionCommand.Resume:
                session.Resume();
                break;
            case SessionCommand.TogglePause:
                if (session.Mode == SessionMode.Running) session.Pause();
                else if (session.Mode == SessionMode.Paused) session.Resume();
                break;
            default:
                break;
        }
    }
}
