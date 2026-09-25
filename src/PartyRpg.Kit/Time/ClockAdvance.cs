namespace PartyRpg.Kit.Time;

/// <summary>What one advance of the game clock did.</summary>
/// <remarks>
/// <para>
/// The advance returns its effects instead of publishing them, so nothing happens to another owner behind
/// that owner's back: the caller reads which boundaries were crossed and which deadlines came due, and
/// hands each one to whoever owns it. That also keeps an advance re-entrant-safe — no effect runs while
/// the clock is halfway through moving — and makes "the day changed" a value a test can assert on rather
/// than a callback a test has to intercept.
/// </para>
/// <para>
/// Nothing in the report is a duration to be applied twice: an hour boundary reported here has already
/// been crossed, and a service opening on it opens once.
/// </para>
/// </remarks>
/// <param name="From">Where on the calendar the clock stood before the advance.</param>
/// <param name="To">Where on the calendar the clock stands now.</param>
/// <param name="Elapsed">How much game time the advance covered, which is the amount the caller stated.</param>
/// <param name="Crossings">How many hour, day, week, month, and year boundaries the advance passed.</param>
/// <param name="Due">The deadlines the advance brought due, in the order they were due.</param>
public sealed record ClockAdvance(
    GameDate From,
    GameDate To,
    GameDuration Elapsed,
    PeriodCrossings Crossings,
    IReadOnlyList<DeadlineDue> Due)
{
    /// <summary>An advance that moved the clock nowhere, which crossed nothing and brought nothing due.</summary>
    /// <param name="at">The point on the calendar the clock still stands at.</param>
    /// <returns>The report of an advance that did nothing.</returns>
    public static ClockAdvance Still(GameDate at) => new(at, at, GameDuration.None, PeriodCrossings.None, []);

    /// <summary>Whether the clock moved at all.</summary>
    public bool Moved => !Elapsed.IsNone;
}
