namespace PartyRpg.Kit.Time;

/// <summary>The unit an amount of game time is stated in.</summary>
/// <remarks>
/// <para>
/// A duration travels with its unit rather than as a bare number, because "three days" and "three hours"
/// are the same number and very different amounts of time. Seconds, minutes, and hours are absolute:
/// they divide an hour the same way in every calendar. Days and weeks are not — how long a day is is the
/// calendar's answer, and how many days a week holds is the calendar's too — so a duration in those units
/// is resolved by <see cref="GameCalendar.Duration"/> rather than by the duration itself.
/// </para>
/// <para>
/// Months and years are deliberately absent. In a calendar whose months are all the same length they
/// would be fixed durations, but a calendar is free to make them differ, and a unit whose length depends
/// on where it starts is a date to move to, which is what <see cref="GameClock.ScheduleAt"/> is for.
/// </para>
/// </remarks>
public enum GameDurationUnit
{
    /// <summary>Elapsed seconds.</summary>
    Seconds,

    /// <summary>Elapsed minutes.</summary>
    Minutes,

    /// <summary>Elapsed hours.</summary>
    Hours,

    /// <summary>Elapsed days, as long as the calendar says a day is.</summary>
    Days,

    /// <summary>Elapsed weeks, as long as the calendar says a week is.</summary>
    Weeks,
}
