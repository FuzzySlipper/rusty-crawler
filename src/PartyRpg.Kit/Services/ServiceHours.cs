using PartyRpg.Kit.Time;

namespace PartyRpg.Kit.Services;

/// <summary>The hours a service keeps, as content states them: the hour it opens and the hour it closes.</summary>
/// <remarks>
/// <para>
/// Business hours are content, and they are a game-time window rather than a pair of flags: the shipped
/// building table states eight distinct pairs across its service rows, including windows that wrap past
/// midnight (a tavern open 18:00 to 06:00) and a window that closes at 24:00. A service whose hours are
/// stated is closed outside them, and the service mechanism asks this type against the session's one
/// clock, so a shop that closes while the party browses refuses the next transaction rather than staying
/// open because a screen is.
/// </para>
/// <para>
/// <b>The window itself belongs to the clock, not to services.</b> A building's hours are one window read
/// by two owners: the counter inside it, and the doors of the place that keeps them. This is that window
/// under the name a service states it by — <see cref="Window"/> is the same value the schedule reads — so
/// a shop and its door cannot disagree about when the shop was open.
/// </para>
/// <para>
/// The window is stated in whole hours of the day, which is the resolution the shipped content carries. A
/// window that opens and closes at the same hour is refused rather than given a meaning it does not have:
/// a service that never closes states 0 to 24.
/// </para>
/// </remarks>
public sealed record ServiceHours
{
    /// <summary>Creates a service's hours.</summary>
    /// <param name="openHour">The hour of the day the service opens, counted from zero.</param>
    /// <param name="closedHour">The hour of the day the service closes, counted from zero, where 24 is the end of the day.</param>
    /// <exception cref="ArgumentOutOfRangeException">An hour is outside a day, or the two hours are the same.</exception>
    public ServiceHours(int openHour, int closedHour) => Window = new OpeningHours(openHour, closedHour);

    /// <summary>Creates a service's hours from the window they are.</summary>
    /// <param name="window">The hours a place keeps, which is the same window a schedule reads.</param>
    public ServiceHours(OpeningHours window) => Window = window;

    /// <summary>The window itself: the hours a place keeps, which a schedule reads too.</summary>
    public OpeningHours Window { get; }

    /// <summary>The hour of the day the service opens.</summary>
    public int OpenHour => Window.OpenHour;

    /// <summary>The hour of the day the service closes, where 24 is the end of the day.</summary>
    public int ClosedHour => Window.ClosedHour;

    /// <summary>Whether the service serves at a point on the calendar.</summary>
    /// <remarks>
    /// The closing hour is excluded and the opening hour included, so a shop open 6 to 18 is shut at 18:00
    /// exactly. A window whose close is the next day — 18 to 6, or 5 to 2 — wraps past midnight, which is
    /// what the shipped data's night trade needs and what a naive range comparison would get backwards.
    /// </remarks>
    /// <param name="at">The date and time to judge.</param>
    public bool IsOpenAt(GameDate at) => Window.IsOpenAt(at);

    /// <summary>How the window reads to a person, as the two hours content stated.</summary>
    public override string ToString() => Window.ToString();
}
