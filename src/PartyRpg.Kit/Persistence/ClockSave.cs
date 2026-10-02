using System.Globalization;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.Party;

namespace PartyRpg.Kit.Persistence;

/// <summary>The elapsed game time and schedules held at an explicit save boundary.</summary>
/// <remarks>
/// Calendar, start date, rate and daylight are composed again by the ruleset. Due times use elapsed game
/// milliseconds, so loading never grants a duration afresh. Transient deadline handles are not saved;
/// each explicitly composed owner captures its meaning and receives it back after the clock and party
/// have been restored. The sub-millisecond admitted-time conversion remainder is deliberately omitted.
/// </remarks>
public sealed record ClockSave
{
    /// <summary>Records where the clock stood.</summary>
    /// <param name="elapsedMilliseconds">Game time elapsed since the session began, which cannot be negative.</param>
    /// <param name="deadlines">Schedules held in registration order.</param>
    /// <exception cref="ArgumentOutOfRangeException">The elapsed time is negative, which game time never is.</exception>
    public ClockSave(long elapsedMilliseconds, IReadOnlyList<DeadlineSave>? deadlines = null)
    {
        if (elapsedMilliseconds < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(elapsedMilliseconds),
                elapsedMilliseconds,
                "Game time runs forward, so a save records an elapsed span of zero or more.");
        }

        ElapsedMilliseconds = elapsedMilliseconds;
        Deadlines = deadlines ?? [];
    }

    /// <summary>Game time elapsed since the session began, exact to the game millisecond.</summary>
    public long ElapsedMilliseconds { get; }

    /// <summary>The held schedules in their original registration order.</summary>
    public IReadOnlyList<DeadlineSave> Deadlines { get; }

    /// <summary>
    /// Reads the clock's position into the save's own terms.
    /// </summary>
    /// <remarks>Every pending deadline needs an owner and durable meaning, or capture refuses it by name.</remarks>
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
        List<DeadlineSave> carried = [];
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
            else if (owner.CaptureDeadline(deadline, clock) is not { } saved)
            {
                problems.Add(new SaveProblem(
                    SaveCodes.SaveDeadlineUncarried,
                    Subject(deadline),
                    $"{owner.Describe(deadline)} is a moment the save cannot carry yet and nothing rebuilds on load"));
            }
            else carried.Add(saved);
        }

        if (problems.Count > 0)
        {
            throw new SessionSaveException($"The session cannot be saved: {string.Join("; ", problems)}.", problems);
        }

        return new ClockSave(clock.Elapsed.Milliseconds, carried);
    }

    /// <summary>Judges carried schedule structure and its references to party state.</summary>
    public IReadOnlyList<SaveProblem> Problems(PartySave party)
    {
        List<SaveProblem> problems = [];
        HashSet<(DeadlineKind, string, PartyMemberId?)> seen = [];
        foreach (DeadlineSave deadline in Deadlines)
        {
            void Problem(string text) => problems.Add(new SaveProblem(SaveCodes.SaveDeadlineInvalid, deadline.Subject, text));
            if (!Enum.IsDefined(deadline.Kind)) Problem($"unknown deadline kind {deadline.Kind}");
            if (!seen.Add((deadline.Kind, deadline.Subject, deadline.Member))) Problem($"duplicate {deadline.Kind} deadline '{deadline.Subject}' on {deadline.Member}");
            if (deadline.DueElapsedMilliseconds < ElapsedMilliseconds) Problem($"{deadline.Kind} '{deadline.Subject}' is due at {deadline.DueElapsedMilliseconds}, before saved clock {ElapsedMilliseconds}");
            if (deadline.RepeatMilliseconds is > 0 && deadline.DueElapsedMilliseconds > long.MaxValue - deadline.RepeatMilliseconds.Value)
                Problem($"{deadline.Kind} '{deadline.Subject}' cannot repeat beyond the clock's range");
            if (deadline.RepeatMilliseconds is <= 0) Problem($"{deadline.Kind} '{deadline.Subject}' cannot repeat at interval {deadline.RepeatMilliseconds}");
            if (deadline.Kind == DeadlineKind.SpellEffect)
            {
                if (deadline.RepeatMilliseconds is not null) Problem($"spell effect '{deadline.Subject}' expires once rather than repeating");
                IReadOnlyList<PartyEffect>? effects = deadline.Member is { } member
                    ? party.Members.FirstOrDefault(on => on.Id == member)?.Effects : party.Effects;
                if (effects is null || !effects.Any(effect => effect.Effect.Value == deadline.Subject))
                    Problem($"deadline for effect '{deadline.Subject}' names a carrier {deadline.Member} that does not hold it");
            }
            else if (deadline.Member is not null) Problem($"{deadline.Kind} '{deadline.Subject}' cannot name a member");
            if (deadline.Kind == DeadlineKind.Fatigue && deadline.Subject.Length != 0) Problem("the debt of sleep cannot name an effect or service");
            if (deadline.Kind is DeadlineKind.Fatigue or DeadlineKind.ServiceRestock && deadline.RepeatMilliseconds is null) Problem($"{deadline.Kind} needs its repeat interval");
        }
        return problems;
    }

    /// <summary>Hands judged schedules back to the already composed owners, preserving registration order.</summary>
    public void RestoreDeadlines(IReadOnlyList<IDeadlineOwner> owners, PartyRpg.Kit.Party.PartyEntity party)
    {
        foreach (DeadlineSave deadline in Deadlines)
            if (!owners.Any(owner => owner.RestoreDeadline(deadline, party)))
                throw new SessionSaveException($"No owner can restore {deadline.Kind} '{deadline.Subject}'.",
                    [new SaveProblem(SaveCodes.SaveDeadlineUnowned, deadline.Subject, $"no composed owner restores {deadline.Kind}")]);
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
