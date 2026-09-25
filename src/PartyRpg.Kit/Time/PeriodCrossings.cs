namespace PartyRpg.Kit.Time;

/// <summary>How many hour, day, week, month, and year boundaries an advance passed.</summary>
/// <remarks>
/// <para>
/// A boundary effect has to fire once for each boundary the clock crossed and never once per update, so
/// the crossings an advance produced are handed back as counts: a caller that opens a shop at each new day
/// can trust that thirty updates inside one day crossed nothing, and that a single advance of thirty days
/// crossed thirty days and not one. Nothing here counts a boundary twice, because each is the difference
/// of the unit indices on either side of the advance.
/// </para>
/// <para>
/// Weeks, months, and years are calendar boundaries and not multiples of days: a week begins on the
/// calendar's first day of the week, a month on its first day, and a year on its first month's first day.
/// They are counted here rather than left to each caller because a schedule phrased in months and an
/// effect phrased in years must agree about where a month or a year began.
/// </para>
/// </remarks>
/// <param name="Hours">How many hour boundaries the advance passed.</param>
/// <param name="Days">How many day boundaries, which are midnights, the advance passed.</param>
/// <param name="Weeks">How many week boundaries the advance passed.</param>
/// <param name="Months">How many month boundaries the advance passed.</param>
/// <param name="Years">How many year boundaries the advance passed.</param>
public readonly record struct PeriodCrossings(int Hours, int Days, int Weeks, int Months, int Years)
{
    /// <summary>An advance that crossed no boundary at all.</summary>
    public static PeriodCrossings None => default;

    /// <summary>Whether the advance crossed any boundary.</summary>
    public bool Any => Hours > 0 || Days > 0 || Weeks > 0 || Months > 0 || Years > 0;
}
