namespace PartyRpg.Kit.Sessions;

/// <summary>
/// The session's own mode, resolved from the authorities that decide what an admitted update does with it:
/// whether a party has been created yet, and the two that can stop it advancing — the engine's lifecycle
/// pause and a hold the player asked for through the interface. They are deliberately separate inputs to one
/// mode: an engine resume must not release a player's hold, and a player's release must not undo an engine
/// pause.
/// </summary>
public enum SessionMode
{
    /// <summary>The session exists but has not been started.</summary>
    Starting,

    /// <summary>
    /// The session is creating its party: it steps no world, reads no movement, advances no clock, and its
    /// admitted update does nothing but drive the creation flow.
    /// </summary>
    Creating,

    /// <summary>The session is live and advances with admitted updates.</summary>
    Running,

    /// <summary>The session is held: admitted updates no longer advance its state.</summary>
    Paused,

    /// <summary>The session has been shut down and publishes nothing further.</summary>
    Stopped,
}
