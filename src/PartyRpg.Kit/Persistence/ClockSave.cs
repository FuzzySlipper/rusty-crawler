using PartyRpg.Kit.Time;

namespace PartyRpg.Kit.Persistence;

/// <summary>
/// Where the one game clock stood when a session was saved: the game time that had elapsed since the
/// session began.
/// </summary>
/// <remarks>
/// <para>
/// The clock's position is state; the calendar it keeps, the date a session begins at, the rate admitted
/// engine time becomes game time at, and the hours it calls daylight are the ruleset's policy and are
/// supplied again when a session is composed. That is why a save records the elapsed span and not a date: a
/// date read against a changed starting date would silently shift every schedule, while elapsed time is
/// what the clock actually counted.
/// </para>
/// <para>
/// The remainder the admitted-interval conversion carries below a game millisecond is deliberately absent.
/// A game millisecond is the clock's own resolution, so dropping less than one of them at a save boundary
/// loses no time any duration in this family can state; carrying it would put a unit in the schema that
/// nothing reads.
/// </para>
/// <para>
/// Scheduled work is absent for a reason that fails loudly rather than quietly: nothing in the product owns
/// a deadline yet, so a clock that is holding one cannot be captured at all, and
/// <see cref="Capture"/> says so instead of writing a save that silently lost the schedule.
/// </para>
/// </remarks>
public sealed record ClockSave
{
    /// <summary>Records where the clock stood.</summary>
    /// <param name="elapsedMilliseconds">Game time elapsed since the session began, which cannot be negative.</param>
    /// <exception cref="ArgumentOutOfRangeException">The elapsed time is negative, which game time never is.</exception>
    public ClockSave(long elapsedMilliseconds)
    {
        if (elapsedMilliseconds < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(elapsedMilliseconds),
                elapsedMilliseconds,
                "Game time runs forward, so a save records an elapsed span of zero or more.");
        }

        ElapsedMilliseconds = elapsedMilliseconds;
    }

    /// <summary>Game time elapsed since the session began, exact to the game millisecond.</summary>
    public long ElapsedMilliseconds { get; }

    /// <summary>
    /// Reads the clock's position into the save's own terms.
    /// </summary>
    /// <remarks>
    /// A clock holding a deadline is refused here rather than captured, because a deadline's number means
    /// nothing without the owner that scheduled it and no owner exists: a save that kept the number would
    /// load a schedule nothing can act on, and a save that dropped it would lose work the session was told
    /// about. Whoever first schedules a deadline owns carrying it through a save.
    /// </remarks>
    /// <param name="clock">The clock to read.</param>
    /// <returns>Where the clock stood.</returns>
    /// <exception cref="ArgumentNullException">The clock is null.</exception>
    /// <exception cref="SessionSaveException">The clock is holding scheduled work that this schema cannot carry.</exception>
    public static ClockSave Capture(GameClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        if (clock.PendingDeadlines > 0)
        {
            throw new SessionSaveException(
                $"The session cannot be saved: the clock is holding {clock.PendingDeadlines} scheduled deadline(s), and nothing in the product owns what one means, so a save would either lose the schedule or restore one nobody can act on.",
                [$"the clock holds {clock.PendingDeadlines} pending deadline(s), which no owner could rebuild on load"]);
        }

        return new ClockSave(clock.Elapsed.Milliseconds);
    }

    /// <summary>
    /// Moves a freshly composed clock to the position this save recorded.
    /// </summary>
    /// <remarks>
    /// The clock arrives composed by the ruleset — its calendar, starting date, rate, and daylight are this
    /// game's policy — and this applies the game time the session had lived through. It is the same
    /// <see cref="GameClock.Advance"/> every other owner of time uses, so a restored clock crosses the
    /// boundaries and brings due the deadlines its span covers exactly as it would have in play.
    /// </remarks>
    /// <param name="clock">The clock to move.</param>
    /// <returns>Where the clock was, where it went, and what the span crossed.</returns>
    /// <exception cref="ArgumentNullException">The clock is null.</exception>
    /// <exception cref="OverflowException">The recorded span leaves the game time this kit can count.</exception>
    public ClockAdvance ApplyTo(GameClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        return clock.Advance(GameDuration.FromMilliseconds(ElapsedMilliseconds));
    }
}
