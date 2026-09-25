using PartyRpg.Kit.Time;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// This game's time: the calendar it keeps, the date a session begins at, how fast its clock runs, and
/// which hours it calls daylight.
/// </summary>
/// <remarks>
/// <para>
/// Everything here is this game's policy, and the kit holds none of it: the clock is a mechanism that
/// moves when a caller moves it, and what a year is shaped like, when a game begins, and how fast an
/// admitted second becomes game time are answers only a ruleset can give. The values are the donor's
/// wherever it states one, and each is cited where it is declared.
/// </para>
/// <para>
/// <b>The clock is composed here, not taken from the context.</b> A calendar, a starting date, a rate, and
/// a lighting window are this game's, and the session is handed the one clock this composes rather than
/// being left to keep anything of its own. The world then reads its days from that same clock, so respawn
/// and travel time are measured in one game time.
/// </para>
/// </remarks>
internal static class MightAndMagic7Time
{
    /// <summary>
    /// The calendar this game's dates are stated against.
    /// </summary>
    /// <remarks>
    /// The shipped tables carry no calendar, so the shape is authored in the kit and stated here: twelve
    /// months of four seven-day weeks, which is the donor's civil time — month 1..12, week 1..4, day 1..28,
    /// 24-hour days (OpenEnroth <c>src/Core/Time/Time.h</c>). It agrees with the respawn intervals already
    /// imported, which are 168, 336, and 672 days: half a year, a year, and two years.
    /// </remarks>
    internal static readonly GameCalendar Calendar = GameCalendar.TwelveMonthsOfFourWeeks;

    /// <summary>
    /// The date and time a session of this game begins at.
    /// </summary>
    /// <remarks>
    /// OpenEnroth <c>src/Core/Time/Time.h:119</c> — "Game starts at 9am" — and the year is the epoch the
    /// donor counts from, <c>src/Core/Time/Time.h:5</c> (<c>gameStartingYear = 1168</c>), whose civil
    /// conversion lands on the first day of the first month. Which date a session begins on is scenario
    /// state, so a scenario that states its own start is expected to move this; until one does, this is the
    /// game's own opening moment rather than a date invented here.
    /// </remarks>
    internal static readonly GameDate Start = new(1168, 1, 1, 9, 0, 0);

    /// <summary>
    /// How much game time one second of admitted engine time is worth.
    /// </summary>
    /// <remarks>
    /// OpenEnroth <c>src/Core/Time/Duration.h:29</c> —
    /// <c>GAME_SECONDS_IN_REALTIME_SECOND = 30</c>, "Game time runs 30x faster than real time". Thirty
    /// game seconds is half a game minute per real second, so an eight-hour rest is a real-time minute and
    /// a walking session crosses a day boundary about every forty-eight real minutes.
    /// </remarks>
    internal static readonly GameTimeScale Scale = new(30);

    /// <summary>
    /// The hours this game calls daylight.
    /// </summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/Graphics/Outdoor.cpp:233-251</c> decides fog and night from the hour:
    /// night below 5, dawn from 5, full day from 6, dusk from 20, and night again from 21. The window is
    /// therefore dawn at 5:00 and dusk at 21:00, which is also the hour the manual's "Wait until dawn"
    /// option names (<c>docs/research/mm7-manual-outline.md</c>, rest menu, p.24).
    /// </remarks>
    internal static readonly DaylightWindow Daylight = new(new TimeOfDay(5, 0), new TimeOfDay(21, 0));

    /// <summary>Composes this game's one clock at the date a session begins at.</summary>
    internal static GameClock Compose() => new(Calendar, Start, Scale, Daylight);
}
