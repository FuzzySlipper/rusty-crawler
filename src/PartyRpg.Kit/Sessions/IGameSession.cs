using PartyRpg.Kit.Presentation;
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

    /// <summary>Advances the session inside the engine-admitted update and republishes its presentation.</summary>
    ProductUpdateResult Update(ProductUpdate update);

    /// <summary>
    /// Reads the session's facts as its projection reads them, without stepping or publishing anything.
    /// </summary>
    /// <remarks>
    /// This is what a playtest harness observes between updates. The world is read live rather than from the
    /// last update's copy, so a look taken since that update is already in the pose it reports.
    /// </remarks>
    SessionSnapshot Inspect();

    /// <summary>
    /// Turns the party's facing by a relative look, between admitted updates and without advancing anything.
    /// </summary>
    /// <param name="yawDegrees">How far to turn, in degrees; positive turns right.</param>
    /// <param name="pitchDegrees">How far to look up, in degrees; positive looks up.</param>
    /// <returns>Why the look was refused, or null when the party turned.</returns>
    Refusal? Look(double yawDegrees, double pitchDegrees);
}
