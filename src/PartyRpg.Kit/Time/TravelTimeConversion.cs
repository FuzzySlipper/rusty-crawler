using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Time;

/// <summary>Turns the world's stated travel charge into the amount of game time the clock is advanced by.</summary>
/// <remarks>
/// <para>
/// A transition states how long it takes in a unit that travels with the amount — minutes, hours, or days —
/// and the way layer deliberately refuses to guess how long a day is, because that ratio belongs to the
/// calendar the clock keeps. This is that ratio's one application, so whoever takes a transition charges
/// the clock exactly what the cost rule quoted, and two callers cannot disagree about how long three days
/// of travel is.
/// </para>
/// <para>
/// The conversion is stated here rather than as an advance on the clock: the clock knows nothing about
/// travel, and a journey is only one of the owners that hands it elapsed time.
/// </para>
/// </remarks>
public static class TravelTimeConversion
{
    /// <summary>How much game time a transition's charge is worth.</summary>
    /// <param name="calendar">The calendar the clock keeps, which is what says how long a day is.</param>
    /// <param name="time">The charge the cost rule quoted.</param>
    /// <returns>The game time the charge covers, which an arrival charges to the clock.</returns>
    /// <exception cref="ArgumentNullException">No calendar was supplied.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The charge states a unit this kit cannot convert.</exception>
    public static GameDuration Elapsed(GameCalendar calendar, TravelTime time)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        return calendar.Duration(time.Amount, time.Unit switch
        {
            TravelTimeUnit.Minutes => GameDurationUnit.Minutes,
            TravelTimeUnit.Hours => GameDurationUnit.Hours,
            TravelTimeUnit.Days => GameDurationUnit.Days,
            _ => throw new ArgumentOutOfRangeException(
                nameof(time),
                time,
                "The charge states a unit of travel time this kit cannot convert into game time."),
        });
    }
}
