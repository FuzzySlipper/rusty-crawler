using PartyRpg.Kit.Time;

namespace PartyRpg.Testing;

/// <summary>
/// The one clock a suite's world keeps time by, on the kit's own twelve-month calendar.
/// </summary>
/// <remarks>
/// The suites' clocks differ only in when they start and how many game seconds one admitted second is worth, so
/// those are the two things a suite states; the calendar and the daylight window are the same everywhere, and a
/// suite that needs another calendar builds its clock where it says why.
/// </remarks>
public static class TestClock
{
    /// <summary>The daylight window the suites' days run on: dawn at five, dusk at nine in the evening.</summary>
    public static DaylightWindow Daylight { get; } = new(new TimeOfDay(5, 0), new TimeOfDay(21, 0));

    /// <summary>The morning the suites' worlds start on: nine o'clock on the first day of 1168.</summary>
    public static GameDate Morning { get; } = new(1168, 1, 1, 9, 0, 0);

    /// <summary>
    /// A clock on the kit's twelve-month calendar that starts on <see cref="Morning"/> unless told otherwise.
    /// </summary>
    /// <param name="scale">Game seconds per admitted second; thirty unless a suite states otherwise.</param>
    /// <param name="start">When the clock starts.</param>
    public static GameClock Create(double scale = 30, GameDate? start = null) =>
        new(GameCalendar.TwelveMonthsOfFourWeeks, start ?? Morning, new GameTimeScale(scale), Daylight);

    /// <summary>A clock that starts at an hour of the first day of 1168.</summary>
    public static GameClock At(int hour, int minute = 0, double scale = 30) =>
        Create(scale, new GameDate(1168, 1, 1, hour, minute, 0));
}
