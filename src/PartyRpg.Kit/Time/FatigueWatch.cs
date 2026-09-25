using PartyRpg.Kit.Party;
using PartyRpg.Kit.Sessions;

namespace PartyRpg.Kit.Time;

/// <summary>
/// The debt of going without sleep, held as a deadline on the one clock rather than as a count in a step.
/// </summary>
/// <remarks>
/// <para>
/// <b>The clock brings it due.</b> A party that has not slept weakens when the clock reaches the point the
/// debt falls due, and that point is registered with <see cref="GameClock.ScheduleAfter"/> — so the state
/// arrives from game time itself, in whatever update or journey carried the clock past it, and never from an
/// update counting days for itself. A watch that counted updates would weaken a party that stood still and
/// spare one that travelled, which is exactly the drift the one clock exists to prevent.
/// </para>
/// <para>
/// <b>Sleep pays the debt; anything else lets it stand.</b> A completed sleep cancels the deadline before the
/// night is advanced, so the hours the party slept through cannot weaken it, and re-arms it from the moment
/// the party wakes. A wait, a broken night, and a journey leave it exactly where it was: the party that
/// stayed awake still owes the sleep.
/// </para>
/// <para>
/// The state applied is the ruleset's own condition with its own severity, and the interval is the ruleset's
/// answer for how long a party may stay awake. A watch applies it to every member, because a party that went
/// without sleep is tired together — the same shape the larder's own consequence takes.
/// </para>
/// </remarks>
public sealed class FatigueWatch : IGameTimeObserver
{
    private readonly GameClock _clock;
    private readonly PartyEntity _party;
    private readonly ActiveCondition _fatigue;
    private readonly GameDuration _interval;
    private DeadlineId? _held;
    private GameDate _armedAt;
    private GameDuration? _remaining;
    private int _landed;

    /// <summary>Creates the watch and registers the debt the party already owes.</summary>
    /// <param name="clock">The session's one clock, which the deadline is held on.</param>
    /// <param name="party">The party the state lands on.</param>
    /// <param name="fatigue">The state going too long without sleep puts on every member.</param>
    /// <param name="interval">How long the party may stay awake before it lands, which must be more than no time at all.</param>
    /// <exception cref="ArgumentNullException">The clock or the party is missing.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The interval is no time at all, which would land the state on every advance.</exception>
    public FatigueWatch(GameClock clock, PartyEntity party, ActiveCondition fatigue, GameDuration interval)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _party = party ?? throw new ArgumentNullException(nameof(party));
        if (interval.IsNone)
        {
            throw new ArgumentOutOfRangeException(
                nameof(interval),
                interval,
                "A debt that falls due after no time at all would weaken a party on its first step; an interval is a length of game time.");
        }

        _fatigue = fatigue;
        _interval = interval;
        // A session begins with the debt unpaid: the party that has just set out is due to sleep one
        // interval from now, which is the donor's own reading of a party that starts play without rest.
        Arm();
    }

    /// <summary>How long the party may stay awake between sleeps.</summary>
    public GameDuration Interval => _interval;

    /// <summary>The state going too long without sleep puts on every member.</summary>
    public ActiveCondition Fatigue => _fatigue;

    /// <summary>How many times the deadline has landed since the session began.</summary>
    public int Landed => _landed;

    /// <summary>
    /// The point on the calendar the debt next falls due at, or null when the watch is holding no deadline
    /// because the party is asleep.
    /// </summary>
    /// <remarks>
    /// The clock's own deadline is the authority; this is where it lands, derived from the interval the watch
    /// is holding, so a panel can say when the party next needs to sleep.
    /// </remarks>
    public GameDate? Due => _held is null ? null : _clock.Calendar.Add(_clock.Now, _interval);

    /// <summary>Whether the party currently carries the state.</summary>
    /// <remarks>
    /// Read from the members rather than remembered here, so a cure, a temple, or the larder's own rule
    /// clearing the state cannot leave this answer claiming it still acts.
    /// </remarks>
    public bool IsWeak
    {
        get
        {
            foreach (PartyMember member in _party.Members)
            {
                if (!member.Conditions.Has(_fatigue.Condition)) return false;
            }

            return _party.Members.Count > 0;
        }
    }

    /// <summary>Whether this watch is holding a deadline.</summary>
    /// <param name="deadline">The handle the clock reported.</param>
    public bool Holds(DeadlineId deadline) => _held == deadline;

    /// <summary>
    /// Takes the debt off the clock, remembering how much of it is left, so a capture that cannot carry a
    /// schedule can read the session.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The one current save schema records the game time a clock has lived through and refuses a clock that
    /// is holding a deadline, because a deadline's number means nothing without its owner. This watch is an
    /// owner, and it is the first: rather than weakening that refusal, a session that is about to be captured
    /// takes its own deadline off the clock and puts it back at the point it was due, so the running session
    /// is unchanged by a save.
    /// </para>
    /// <para>
    /// <b>The debt itself is not in the document.</b> A save carries the party's own state — including the
    /// state this debt puts on it — and the game time the clock has lived, but not when the next sleep falls
    /// due; a resumed session therefore arms a fresh debt. That is a stated loss with a named receiver: the
    /// save schema needs a section for schedules the moment a schedule must survive a load, and this is it.
    /// </para>
    /// </remarks>
    public void Suspend()
    {
        if (_held is not { } held) return;
        long since = _clock.Calendar.Between(_armedAt, _clock.Now).Milliseconds;
        _remaining = GameDuration.FromMilliseconds(Math.Max(0, _interval.Milliseconds - since));
        _clock.Cancel(held);
        _held = null;
    }

    /// <summary>Puts a suspended debt back where it stood, so a capture left the session exactly as it was.</summary>
    public void Resume()
    {
        if (_held is not null || _remaining is not { } remaining) return;
        _remaining = null;
        _armedAt = _clock.Now;
        // A debt that was already due when it was suspended comes due on the next advance rather than being
        // pushed a whole interval away, which is what a party that stood at the due point would have seen.
        _held = _clock.ScheduleAfter(remaining);
    }

    /// <summary>
    /// Takes one advance of the session's one clock and lands the state when the debt fell due inside it.
    /// </summary>
    /// <param name="advance">Where the clock was, where it went, and what it brought due.</param>
    /// <exception cref="ArgumentNullException">The advance is null.</exception>
    public void Observe(ClockAdvance advance)
    {
        ArgumentNullException.ThrowIfNull(advance);
        if (_held is not { } held) return;
        foreach (DeadlineDue due in advance.Due)
        {
            if (due.Deadline != held) continue;
            Land();
            return;
        }
    }

    /// <summary>
    /// Forgets the debt because the party is going to sleep: the sleep re-arms it from the moment it wakes.
    /// </summary>
    /// <remarks>
    /// Paying is not the same as clearing: the state on the members is ended by the recovery a completed
    /// sleep applies, and this only stops the clock from bringing a debt due in the middle of the night.
    /// </remarks>
    public void Pay()
    {
        _remaining = null;
        if (_held is not { } held) return;
        _clock.Cancel(held);
        _held = null;
    }

    /// <summary>Registers the debt again, due one interval from where the clock now stands.</summary>
    public void Arm()
    {
        _armedAt = _clock.Now;
        _remaining = null;
        _held = _clock.ScheduleAfter(_interval);
    }

    /// <summary>Lands the state on every member and re-arms the debt from the moment it fired.</summary>
    private void Land()
    {
        foreach (PartyMember member in _party.Members) member.Conditions.Apply(_fatigue);
        _landed++;
        // The debt re-arms from the instant the clock actually reached rather than from the point it
        // missed, which is the same rule the clock's own repeating deadlines follow: a party that slept
        // through three days owes one night's sleep, not three.
        Arm();
    }
}
