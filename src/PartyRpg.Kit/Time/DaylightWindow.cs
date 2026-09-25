namespace PartyRpg.Kit.Time;

/// <summary>The part of a day the clock calls daylight.</summary>
/// <remarks>
/// <para>
/// Day and night are policy, not arithmetic: which hours are light enough to see by, and when the lamps
/// come on, is the ruleset's answer and not something a calendar can carry. The window is stated as the
/// two times of day the light changes at, and it is the clock's own derived fact — every owner that asks
/// whether it is day reads the same window the clock did, so a shop's hours and the outdoor lighting
/// cannot disagree about when morning was.
/// </para>
/// <para>
/// The window cannot wrap past midnight. A rule that lighting is on from dusk to dawn would make every
/// hour of the following day bright if it were read as a naive range, so the honest statement of a night
/// window is the daylight one beside it, which is what this refuses to guess at.
/// </para>
/// </remarks>
public readonly record struct DaylightWindow
{
    /// <summary>Creates a daylight window.</summary>
    /// <param name="dawn">The time of day daylight begins, which is included in the window.</param>
    /// <param name="dusk">The time of day daylight ends, which is excluded from the window.</param>
    /// <exception cref="ArgumentException">The window ends before it begins, so no hour would ever be daylight.</exception>
    public DaylightWindow(TimeOfDay dawn, TimeOfDay dusk)
    {
        if (dawn.Minutes >= dusk.Minutes)
        {
            throw new ArgumentException(
                $"Daylight cannot begin at {dawn.Hour:00}:{dawn.Minute:00} and end at {dusk.Hour:00}:{dusk.Minute:00}, because a window that ends before it begins would leave every hour of the day in the dark.",
                nameof(dusk));
        }

        Dawn = dawn;
        Dusk = dusk;
    }

    /// <summary>The time of day daylight begins, which is included in the window.</summary>
    public TimeOfDay Dawn { get; }

    /// <summary>The time of day daylight ends, which is excluded from the window.</summary>
    public TimeOfDay Dusk { get; }

    /// <summary>Whether a time of day falls inside the window.</summary>
    /// <param name="at">The time of day to judge.</param>
    public bool Contains(TimeOfDay at) => at.Minutes >= Dawn.Minutes && at.Minutes < Dusk.Minutes;

    /// <summary>Whether a point on the calendar falls inside the window.</summary>
    /// <param name="at">The date and time to judge.</param>
    public bool Contains(GameDate at) => Contains(new TimeOfDay(at.Hour, at.Minute));
}
