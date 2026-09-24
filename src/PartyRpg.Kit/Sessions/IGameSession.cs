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

    /// <summary>Holds the session. A session that is not running is unaffected.</summary>
    void Pause();

    /// <summary>Releases a held session. A session that is not paused is unaffected.</summary>
    void Resume();

    /// <summary>Publishes the current presentation, for a fresh attachment or an explicit repaint.</summary>
    void PublishInitial();

    /// <summary>Advances the session inside the engine-admitted update and republishes its presentation.</summary>
    ProductUpdateResult Update(ProductUpdate update);
}
