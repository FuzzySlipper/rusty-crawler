namespace PartyRpg.Kit.Sessions;

/// <summary>
/// The session's own mode. It is deliberately not the engine's lifecycle state: a session can be
/// paused by the player through an interface action while the engine keeps admitting updates, and
/// that difference has to be owned somewhere.
/// </summary>
public enum SessionMode
{
    /// <summary>The session exists but has not been started.</summary>
    Starting,

    /// <summary>The session is live and advances with admitted updates.</summary>
    Running,

    /// <summary>The session is held: admitted updates no longer advance its state.</summary>
    Paused,

    /// <summary>The session has been shut down and publishes nothing further.</summary>
    Stopped,
}
