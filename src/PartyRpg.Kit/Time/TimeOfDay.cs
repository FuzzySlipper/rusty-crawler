namespace PartyRpg.Kit.Time;

/// <summary>A time of day on the game clock, independent of any date.</summary>
/// <remarks>
/// Hours and minutes are civil: an hour is counted from midnight, and a minute from the top of its hour.
/// The upper bound on the hour is the calendar's, because a calendar may shape a day with fewer hours
/// than the twenty-four every author assumes; <see cref="GameCalendar.RequireValid(TimeOfDay, string)"/>
/// is what refuses an hour a day does not have. Windows stated in this type — a service's opening hours,
/// the daylight the lighting reads — are therefore policy a ruleset can state and a calendar can refuse.
/// </remarks>
public readonly record struct TimeOfDay
{
    /// <summary>Creates a time of day.</summary>
    /// <param name="hour">The hour, counted from midnight, which cannot be negative.</param>
    /// <param name="minute">The minute of the hour.</param>
    /// <exception cref="ArgumentOutOfRangeException">The hour is negative, or the minute is outside its hour.</exception>
    public TimeOfDay(int hour, int minute)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(hour);
        if (minute is < 0 or >= GameDuration.MinutesPerHour)
        {
            throw new ArgumentOutOfRangeException(nameof(minute), minute, $"A minute of an hour is counted from zero to {GameDuration.MinutesPerHour - 1}.");
        }

        Hour = hour;
        Minute = minute;
    }

    /// <summary>The hour, counted from midnight.</summary>
    public int Hour { get; }

    /// <summary>The minute of the hour.</summary>
    public int Minute { get; }

    /// <summary>The time of day as a count of minutes from midnight, which is how a window is compared.</summary>
    public int Minutes => (Hour * GameDuration.MinutesPerHour) + Minute;
}
