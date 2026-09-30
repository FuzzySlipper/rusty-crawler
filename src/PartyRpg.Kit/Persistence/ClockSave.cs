using System.Globalization;
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
/// Scheduled work is absent and fails loudly rather than quietly: a deadline its owner rebuilds on load is
/// left out, and any other refuses the save by name in <see cref="Capture"/> instead of writing a save that
/// silently lost the schedule. Carrying such deadlines is #8617.
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
    /// The schema records the game time the clock has lived through and no deadlines, so every deadline the
    /// clock holds is asked of the owners that set it. One an owner rebuilds on load — a shelf's restock, the
    /// debt of sleep — is left out, and what that costs is stated by the owner. One an owner cannot rebuild, or
    /// one nobody holds, refuses the save with each named, because a document that dropped it would load a
    /// session that had silently lost it.
    /// </remarks>
    /// <param name="clock">The clock to read.</param>
    /// <param name="owners">The owners of the deadlines the clock may be holding.</param>
    /// <returns>Where the clock stood.</returns>
    /// <exception cref="ArgumentNullException">The clock or the owners are null.</exception>
    /// <exception cref="SessionSaveException">The clock holds a deadline the schema cannot carry and nobody rebuilds.</exception>
    public static ClockSave Capture(GameClock clock, IReadOnlyList<IDeadlineOwner> owners)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(owners);
        List<SaveProblem> problems = [];
        foreach (DeadlineId deadline in clock.Pending)
        {
            IDeadlineOwner? owner = owners.FirstOrDefault(candidate => candidate.Holds(deadline));
            if (owner is null)
            {
                problems.Add(new SaveProblem(
                    SaveCodes.SaveDeadlineUnowned,
                    Subject(deadline),
                    $"the clock holds deadline {deadline}, which no owner in the session holds, so a load could not rebuild it"));
            }
            else if (!owner.RebuildsOnLoad(deadline))
            {
                problems.Add(new SaveProblem(
                    SaveCodes.SaveDeadlineUncarried,
                    Subject(deadline),
                    $"{owner.Describe(deadline)} is a moment the save cannot carry yet and nothing rebuilds on load"));
            }
        }

        if (problems.Count > 0)
        {
            throw new SessionSaveException($"The session cannot be saved: {string.Join("; ", problems)}.", problems);
        }

        return new ClockSave(clock.Elapsed.Milliseconds);
    }

    /// <summary>Reads a clock that no owner shares deadlines with, so every deadline it holds refuses the save.</summary>
    /// <param name="clock">The clock to read.</param>
    /// <returns>Where the clock stood.</returns>
    public static ClockSave Capture(GameClock clock) => Capture(clock, []);

    /// <summary>The subject a deadline's problem is about: the deadline's own number on the clock.</summary>
    private static string Subject(DeadlineId deadline) => deadline.Value.ToString(CultureInfo.InvariantCulture);

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
