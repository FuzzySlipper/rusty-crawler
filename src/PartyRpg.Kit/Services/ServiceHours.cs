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
/// <b>Locking the door is a different owner's work.</b> This states when a counter serves; whether the door
/// of a closed building is locked, and what a party finds when it walks the town at night, belongs to the
/// schedule owner that closes doors. Checking the hours here is what keeps a service from selling through a
/// shut window in the meantime.
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
    public ServiceHours(int openHour, int closedHour)
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
                "A service that opens and closes at one hour has no window to serve in; a service that never closes states 0 to 24.");
        }

        OpenHour = openHour;
        ClosedHour = closedHour;
    }

    /// <summary>The hour of the day the service opens.</summary>
    public int OpenHour { get; }

    /// <summary>The hour of the day the service closes, where 24 is the end of the day.</summary>
    public int ClosedHour { get; }

    /// <summary>Whether the service serves at a point on the calendar.</summary>
    /// <remarks>
    /// The closing hour is excluded and the opening hour included, so a shop open 6 to 18 is shut at 18:00
    /// exactly. A window whose close is the next day — 18 to 6, or 5 to 2 — wraps past midnight, which is
    /// what the shipped data's night trade needs and what a naive range comparison would get backwards.
    /// </remarks>
    /// <param name="at">The date and time to judge.</param>
    public bool IsOpenAt(GameDate at) =>
        OpenHour < ClosedHour
            ? at.Hour >= OpenHour && at.Hour < ClosedHour
            : at.Hour >= OpenHour || at.Hour < ClosedHour;

    /// <summary>How the window reads to a person, as the two hours content stated.</summary>
    public override string ToString() =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{OpenHour:00}:00–{ClosedHour:00}:00");
}
