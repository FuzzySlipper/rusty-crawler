using PartyRpg.Kit.Time;

namespace PartyRpg.Kit.Presentation;

/// <summary>Where the game clock stands, as the panel shows it.</summary>
/// <remarks>
/// <para>
/// The clock is the one owner of game time, so every value here is read from it at the moment the
/// projection is built: nothing in this kit keeps a second count of days, hours, or elapsed time beside
/// it. A session whose ruleset has not composed a clock publishes <see cref="None"/>, which the panel
/// shows as not knowing rather than as the first day of a calendar nobody declared.
/// </para>
/// <para>
/// The date and the time of day are published as the calendar's own numbers rather than as a named date.
/// What a month is called is presentation meaning, and the ruleset owns it; until it supplies names, the
/// wire carries what every calendar can spell and the panel prints unchanged what it was given.
/// </para>
/// </remarks>
/// <param name="Present">Whether the session has a clock at all.</param>
/// <param name="Date">The date on the clock's calendar, as year, month, and day.</param>
/// <param name="Time">The time of day, whole minutes.</param>
/// <param name="Daylight">Which half of the daylight window the clock stands in, as the wire spells it.</param>
/// <param name="ElapsedDays">Whole game days elapsed since the session began, which is what respawn is measured in.</param>
public readonly record struct ClockSnapshot(bool Present, string Date, string Time, string Daylight, int ElapsedDays)
{
    /// <summary>The clock of a session whose ruleset composed none.</summary>
    public static ClockSnapshot None => new(false, string.Empty, string.Empty, string.Empty, 0);

    /// <summary>Reads the clock as the panel needs it.</summary>
    /// <param name="clock">The session's one clock, or null when it has none.</param>
    /// <returns>Where the clock stands, or the not-known value.</returns>
    public static ClockSnapshot From(GameClock? clock)
    {
        if (clock is null) return None;
        GameDate now = clock.Now;
        return new ClockSnapshot(
            true,
            $"{now.Year:0000}-{now.Month:00}-{now.Day:00}",
            // The panel is a HUD and the calendar's smallest unit is a second, so publishing to the minute
            // is a presentation choice: the clock keeps the seconds, and a reader that needs them reads it.
            $"{now.Hour:00}:{now.Minute:00}",
            clock.IsDaylight ? "day" : "night",
            clock.ElapsedGameDays);
    }
}
