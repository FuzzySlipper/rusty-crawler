namespace PartyRpg.Kit.Sessions;

/// <summary>
/// A source of elapsed game time for the world's own bookkeeping.
/// </summary>
/// <remarks>
/// The one clock satisfies this seam, and it is the source a product runs on: a place's population is
/// restored against the day count the clock reports, so respawn is measured in the same game time travel,
/// rest, and the admitted update all move. The seam exists so the world asks for a day count rather than
/// for a clock, which is what lets a test drive respawn with a day source of its own. A world without a
/// source does not advance, and says so by doing nothing.
/// </remarks>
public interface IWorldTimeSource
{
    /// <summary>Whole game days elapsed since the session began, day zero being its first day.</summary>
    int ElapsedGameDays { get; }
}
