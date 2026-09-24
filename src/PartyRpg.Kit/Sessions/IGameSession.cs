using Rusty.Engine;

namespace PartyRpg.Kit.Sessions;

/// <summary>
/// One session of the game: composition, state ownership, and the engine-facing lifecycle the
/// product forwards to it.
/// </summary>
public interface IGameSession : IDisposable
{
    /// <summary>The session's current mode.</summary>
    SessionMode Mode { get; }

    /// <summary>Starts the session: it leaves <see cref="SessionMode.Starting"/> and begins advancing.</summary>
    void Start();

    /// <summary>Holds the session because the engine's lifecycle paused. A session already held is unaffected.</summary>
    void Pause();

    /// <summary>Releases the engine's lifecycle pause. A player's hold is not released by this call.</summary>
    void Resume();

    /// <summary>Holds the session at the player's request.</summary>
    void Hold();

    /// <summary>Releases the player's hold. An engine pause is not released by this call.</summary>
    void ReleaseHold();

    /// <summary>Publishes the current presentation, for a fresh attachment or an explicit repaint.</summary>
    void PublishInitial();

    /// <summary>Advances the session inside the engine-admitted update and republishes its presentation.</summary>
    ProductUpdateResult Update(ProductUpdate update);
}
