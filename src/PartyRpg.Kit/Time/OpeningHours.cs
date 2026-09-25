namespace PartyRpg.Kit.Time;

/// <summary>The hours something keeps: the hour of the day it opens and the hour it closes.</summary>
/// <remarks>
/// <para>
/// <b>One window, read by everything that keeps hours.</b> A shop's counter, a guild's hall, and the
/// doors of the buildings around them all open and close at hours their content states, and all of them
/// are the same window read against the one clock. A window is a pair of hours rather than two flags, so
/// "closed" is never a state somebody set: it is what the clock reads outside the pair, which is why a
/// door cannot stay open past its hour because a screen forgot to close it.
/// </para>
/// <para>
/// The window wraps past midnight when its close is before its open — the shipped building table carries
/// night trade at 18:00 to 06:00 — and the opening hour is included while the closing hour is excluded,
/// so a place that closes at 18:00 is shut at 18:00 exactly. A window that opens and closes at the same
/// hour is refused rather than given a meaning it does not have: something that never closes states 0 to
/// 24, and something that never opens is not a window at all.
/// </para>
/// <para>
/// The window is stated in whole hours, which is the resolution content carries. Its next change is a
/// point on the calendar rather than a countdown, so a schedule is a game-time deadline the clock can
/// be asked about, never a number of frames an update happens to have run.
/// </para>
/// </remarks>
public readonly record struct OpeningHours
{
    /// <summary>Creates a window.</summary>
    /// <param name="openHour">The hour of the day it opens, counted from zero.</param>
    /// <param name="closedHour">The hour of the day it closes, counted from zero, where 24 is the end of the day.</param>
    /// <exception cref="ArgumentOutOfRangeException">An hour is outside a day, or the two hours are the same.</exception>
    public OpeningHours(int openHour, int closedHour)
    {
        if (openHour is < 0 or > 24)
        {
            throw new ArgumentOutOfRangeException(
                nameof(openHour),
                openHour,
                "An opening hour is an hour of the day, counted from zero, with 24 naming the end of the day.");
        }

        if (closedHour is < 0 or > 24)
        {
            throw new ArgumentOutOfRangeException(
                nameof(closedHour),
                closedHour,
                "A closing hour is an hour of the day, counted from zero, with 24 naming the end of the day.");
        }

        if (openHour == closedHour)
        {
            throw new ArgumentOutOfRangeException(
                nameof(closedHour),
                closedHour,
                "A window that opens and closes at one hour has no hours to be open in; something that never closes states 0 to 24.");
        }

        OpenHour = openHour;
        ClosedHour = closedHour;
    }

    /// <summary>The hour of the day it opens.</summary>
    public int OpenHour { get; }

    /// <summary>The hour of the day it closes, where 24 is the end of the day.</summary>
    public int ClosedHour { get; }

    /// <summary>Whether it is open at a point on the calendar.</summary>
    /// <remarks>
    /// The closing hour is excluded and the opening hour included, so a window open 6 to 18 is shut at
    /// 18:00 exactly. A close that falls before the open wraps past midnight, which is what night trade
    /// needs and what a naive range comparison would get backwards.
    /// </remarks>
    /// <param name="at">The date and time to judge.</param>
    public bool IsOpenAt(GameDate at) =>
        OpenHour < ClosedHour
            ? at.Hour >= OpenHour && at.Hour < ClosedHour
            : at.Hour >= OpenHour || at.Hour < ClosedHour;

    /// <summary>
    /// The point on the calendar the window next changes at, or null when the calendar holds no such day.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the deadline a schedule states: the next hour the thing opens or closes at, as a point in
    /// game time. Nothing here counts how much time is left, because a countdown would have to be
    /// re-derived on every update and would be a second answer to what the clock already knows.
    /// </para>
    /// <para>
    /// Both stated hours are candidate instants — the state flips at exactly those two — so the answer is
    /// the earliest of them that stands after the moment asked about, today or tomorrow. An hour of 24 is
    /// the end of its day, which the calendar's own arithmetic lands on the following midnight.
    /// </para>
    /// </remarks>
    /// <param name="calendar">The calendar the point is stated against, which is what says how long a day is.</param>
    /// <param name="at">The point on the calendar to look forward from.</param>
    /// <returns>The next change, or null when the calendar's own range ends before one.</returns>
    /// <exception cref="ArgumentNullException">No calendar was supplied.</exception>
    public GameDate? NextChangeAfter(GameCalendar calendar, GameDate at)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        GameDate midnight = new(at.Year, at.Month, at.Day);
        long now = calendar.AbsoluteMilliseconds(at);
        long next = long.MaxValue;
        foreach (int dayOffset in (int[])[0, 1])
        {
            GameDate day = calendar.Add(midnight, calendar.Days(dayOffset));
            foreach (int hour in (int[])[OpenHour, ClosedHour])
            {
                long candidate = calendar.AbsoluteMilliseconds(calendar.Add(day, GameDuration.FromHours(hour)));
                if (candidate > now && candidate < next) next = candidate;
            }
        }

        return next == long.MaxValue ? null : calendar.FromAbsoluteMilliseconds(next);
    }

    /// <summary>How the window reads to a person, as the two hours content stated.</summary>
    public override string ToString() =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{OpenHour:00}:00–{ClosedHour:00}:00");
}
