namespace PartyRpg.Kit.Time;

/// <summary>A point on the game calendar: the date, and the time of day within it.</summary>
/// <remarks>
/// <para>
/// A date is a civil value — year, month, day, hour, minute, second — and not a count of elapsed time,
/// because it is the form a journal entry, a service's hours, and a save all state it in. Which of those
/// fields a calendar actually holds is the calendar's answer: the constructor refuses a value that is
/// nonsense in any calendar (a year before the first one, a month below one, a minute or second outside
/// its hour), and <see cref="GameCalendar.RequireValid"/> refuses one that this calendar has no day or
/// hour for.
/// </para>
/// <para>
/// A date states whole seconds. The clock's own position is finer, but every duration in this family is
/// stated in whole seconds or larger, and a date is what effects and screens are told about.
/// </para>
/// </remarks>
public readonly record struct GameDate
{
    /// <summary>Creates a point on a calendar.</summary>
    /// <param name="year">The year, counted from the calendar's first year.</param>
    /// <param name="month">The month, counted from one.</param>
    /// <param name="day">The day of the month, counted from one.</param>
    /// <param name="hour">The hour of the day, counted from midnight.</param>
    /// <param name="minute">The minute of the hour.</param>
    /// <param name="second">The second of the minute.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A field is outside the range every calendar shares. A field this calendar narrows further — a month
    /// it does not have, a day its month does not have, an hour its day does not have — is refused by
    /// <see cref="GameCalendar.RequireValid"/>, because only the calendar knows those bounds.
    /// </exception>
    public GameDate(int year, int month, int day, int hour = 0, int minute = 0, int second = 0)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(year, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(month, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(day, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(hour);
        if (minute is < 0 or >= GameDuration.MinutesPerHour)
        {
            throw new ArgumentOutOfRangeException(nameof(minute), minute, $"A minute of an hour is counted from zero to {GameDuration.MinutesPerHour - 1}.");
        }

        if (second < 0 || second >= GameDuration.SecondsPerMinute)
        {
            throw new ArgumentOutOfRangeException(nameof(second), second, $"A second of a minute is counted from zero to {GameDuration.SecondsPerMinute - 1}.");
        }

        Year = year;
        Month = month;
        Day = day;
        Hour = hour;
        Minute = minute;
        Second = second;
    }

    /// <summary>The year, counted from the calendar's first year.</summary>
    public int Year { get; }

    /// <summary>The month, counted from one.</summary>
    public int Month { get; }

    /// <summary>The day of the month, counted from one.</summary>
    public int Day { get; }

    /// <summary>The hour of the day, counted from midnight.</summary>
    public int Hour { get; }

    /// <summary>The minute of the hour.</summary>
    public int Minute { get; }

    /// <summary>The second of the minute.</summary>
    public int Second { get; }
}
