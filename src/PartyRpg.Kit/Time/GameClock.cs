using PartyRpg.Kit.Sessions;

namespace PartyRpg.Kit.Time;

/// <summary>
/// The one game clock: where the product is on its calendar, and the only owner that moves it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Time moves when, and only when, a caller moves it.</b> The clock holds no timer, starts no thread,
/// reads no wall clock, and has no second loop; it is advanced by explicit calls from inside the one
/// admitted update, so a paused session, a headless test, and a running product all pass game time at
/// exactly the rate their caller said. Travel, rest, waiting, camping, training, and service time all
/// arrive through <see cref="Advance"/>; the admitted update's own interval arrives through
/// <see cref="AdvanceAdmittedSeconds"/>, which is the same advance with the interval stated in the unit
/// the engine measures and converted at this clock's own <see cref="Scale"/>.
/// </para>
/// <para>
/// <b>Effects are returned, never published.</b> An advance reports the boundaries it crossed and the
/// deadlines it brought due; the caller hands each to its owner. Nothing else in the product may keep a
/// schedule, count frames, or hold a deadline of its own.
/// </para>
/// <para>
/// <b>Time runs forward.</b> There is no way to move the clock back, no negative duration, and no
/// difference measured backwards: every schedule, respawn, and duration in the product is a point the
/// clock has not reached yet, and a clock that could rewind would silently re-fire them.
/// </para>
/// <para>
/// The clock is stepped inside the one admitted update and is not thread-safe by design; a lock would only
/// make a second thread's use of it look safe, and a second thread driving game time is a second clock.
/// </para>
/// </remarks>
public sealed class GameClock : IWorldTimeSource
{
    private readonly List<Deadline> _deadlines = [];
    private readonly long _startMilliseconds;
    private long _elapsedMilliseconds;
    private long _nextDeadline;
    private double _unconvertedRealMilliseconds;

    /// <summary>Creates the clock a session runs on.</summary>
    /// <param name="calendar">The calendar the clock keeps, which is what makes a date mean anything.</param>
    /// <param name="start">The date and time the session begins at, which is scenario state.</param>
    /// <param name="scale">How much game time one second of admitted engine time is worth.</param>
    /// <param name="daylight">The part of the day the clock calls daylight, which is what day and night are derived from.</param>
    /// <exception cref="ArgumentNullException">No calendar was supplied.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The calendar does not hold the starting date, or an hour of the daylight window.</exception>
    public GameClock(GameCalendar calendar, GameDate start, GameTimeScale scale, DaylightWindow daylight)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        calendar.RequireValid(start, nameof(start));
        calendar.RequireValid(daylight.Dawn, nameof(daylight));
        calendar.RequireValid(daylight.Dusk, nameof(daylight));
        Calendar = calendar;
        Start = start;
        Scale = scale;
        Daylight = daylight;
        _startMilliseconds = calendar.AbsoluteMilliseconds(start);
    }

    /// <summary>The calendar the clock keeps.</summary>
    public GameCalendar Calendar { get; }

    /// <summary>The date and time the session begins at.</summary>
    public GameDate Start { get; }

    /// <summary>How much game time one second of admitted engine time is worth.</summary>
    public GameTimeScale Scale { get; }

    /// <summary>The part of the day the clock calls daylight.</summary>
    public DaylightWindow Daylight { get; }

    /// <summary>Where on the calendar the clock stands now.</summary>
    public GameDate Now => Calendar.FromAbsoluteMilliseconds(AbsoluteNow);

    /// <summary>How much game time has passed since the session began.</summary>
    public GameDuration Elapsed => GameDuration.FromMilliseconds(_elapsedMilliseconds);

    /// <summary>
    /// Whole game days elapsed since the session began, day zero being its first day.
    /// </summary>
    /// <remarks>
    /// This is the day count the world's own bookkeeping reads — a place's population is restored after a
    /// whole number of days — so the clock satisfies the world's time seam rather than a test double
    /// standing in for one. It is a count of elapsed time and not a date: a session that began at noon
    /// reaches its second day at noon, exactly twenty-four hours later.
    /// </remarks>
    public int ElapsedGameDays
    {
        get
        {
            long days = _elapsedMilliseconds / Calendar.DayMilliseconds;
            // The seam counts days in a whole number. A session long enough to overflow it is not a
            // session, so the count saturates rather than wrapping into a schedule that fires for the
            // wrong reason.
            return days > int.MaxValue ? int.MaxValue : (int)days;
        }
    }

    /// <summary>Whether the clock currently stands in daylight.</summary>
    public bool IsDaylight => Daylight.Contains(Now);

    /// <summary>Whether the clock currently stands in the dark, which is what lighting and night hours read.</summary>
    public bool IsNight => !IsDaylight;

    /// <summary>How many deadlines the clock is holding.</summary>
    public int PendingDeadlines => _deadlines.Count;

    /// <summary>
    /// Moves the clock forward by an amount of game time, which is the one way time advances.
    /// </summary>
    /// <remarks>
    /// An advance of no time does nothing at all: it crosses no boundary, brings no deadline due, and
    /// leaves the clock exactly where it was, so an update that admits no steps cannot fire a day or wake
    /// a sleeping effect. Every effect an advance did produce is in the report it returns, once per
    /// crossing.
    /// </remarks>
    /// <param name="elapsed">The game time to advance by.</param>
    /// <returns>Where the clock was, where it went, and what the advance crossed and brought due.</returns>
    /// <exception cref="OverflowException">The advance leaves the game time this kit can count.</exception>
    public ClockAdvance Advance(GameDuration elapsed)
    {
        if (elapsed.IsNone) return ClockAdvance.Still(Now);

        long from = AbsoluteNow;
        long to = checked(from + elapsed.Milliseconds);

        // Everything the report needs is read before the clock moves, so an advance that cannot be reported
        // — a date past the furthest year this calendar can name — leaves the clock where it was rather than
        // moving it into a state nobody was told about.
        GameDate fromDate = Calendar.FromAbsoluteMilliseconds(from);
        GameDate toDate = Calendar.FromAbsoluteMilliseconds(to);

        // The boundaries are counted from the clock's own dates, so an advance of twenty-five hours crosses
        // one day boundary and twenty-five hour boundaries, and never one per update it happened to arrive
        // in.
        PeriodCrossings crossings = Calendar.Crossed(fromDate, toDate);
        IReadOnlyList<DeadlineDue> due = Bring(to);
        _elapsedMilliseconds += elapsed.Milliseconds;
        return new ClockAdvance(fromDate, toDate, elapsed, crossings, due);
    }

    /// <summary>
    /// Moves the clock forward by an interval the admitted update measured, converted at this clock's own
    /// scale.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the same advance as <see cref="Advance"/>, in the unit the engine measures. It exists so the
    /// conversion from engine time to game time happens in one place: a caller that converted for itself
    /// would be a second derivation of game time, and the two would drift apart the moment the scale
    /// changed.
    /// </para>
    /// <para>
    /// The part of the interval below the clock's resolution is carried into the next call rather than
    /// rounded away, so a stream of very short admitted steps advances exactly as much game time as one
    /// long step of the same total length. The carried remainder is clock state, not a second clock: it
    /// moves only when a caller states an interval.
    /// </para>
    /// </remarks>
    /// <param name="admittedSeconds">The interval the admitted update covers, in engine seconds.</param>
    /// <returns>Where the clock was, where it went, and what the advance crossed and brought due.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The interval is negative, unmeasurable, or longer than the game time this kit can count.</exception>
    /// <exception cref="OverflowException">The advance leaves the game time this kit can count.</exception>
    public ClockAdvance AdvanceAdmittedSeconds(double admittedSeconds)
    {
        if (!double.IsFinite(admittedSeconds) || admittedSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(admittedSeconds),
                admittedSeconds,
                "An admitted interval is a length of engine time the engine measured, so one that is negative or unmeasurable is a defect rather than time running backwards.");
        }

        if (admittedSeconds == 0) return ClockAdvance.Still(Now);

        double milliseconds = (admittedSeconds * Scale.GameSecondsPerRealSecond * GameDuration.MillisecondsPerSecond) + _unconvertedRealMilliseconds;
        if (milliseconds >= long.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(admittedSeconds),
                admittedSeconds,
                "That admitted interval is longer than the game time this kit can count.");
        }

        long whole = (long)Math.Floor(milliseconds);
        _unconvertedRealMilliseconds = milliseconds - whole;
        return whole > 0 ? Advance(GameDuration.FromMilliseconds(whole)) : ClockAdvance.Still(Now);
    }

    /// <summary>Registers something that should happen after an amount of game time.</summary>
    /// <param name="delay">How much game time must pass first, which may be none, meaning the next advance.</param>
    /// <returns>The handle the deadline is reported under.</returns>
    public DeadlineId ScheduleAfter(GameDuration delay) => Hold(checked(AbsoluteNow + delay.Milliseconds), null);

    /// <summary>Registers something that should happen at a point on the calendar.</summary>
    /// <param name="at">The point on the calendar the deadline is due at.</param>
    /// <returns>The handle the deadline is reported under.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The calendar does not hold the date, or the clock has already passed it.</exception>
    public DeadlineId ScheduleAt(GameDate at)
    {
        Calendar.RequireValid(at, nameof(at));
        long due = Calendar.AbsoluteMilliseconds(at);
        if (due < AbsoluteNow)
        {
            throw new ArgumentOutOfRangeException(
                nameof(at),
                at,
                $"The clock has already passed {at}, so a deadline there would come due at once for a reason the calendar does not show.");
        }

        return Hold(due, null);
    }

    /// <summary>Registers something that should happen again and again at one interval.</summary>
    /// <remarks>
    /// A repeating deadline fires at most once per advance and re-arms from the moment it fired, so an
    /// advance over a long absence brings it due once — exactly as a place's population is restored once —
    /// instead of firing an unbounded number of times for intervals that were missed while nobody was
    /// looking. Something that must happen at a point on the calendar rather than after an interval is
    /// registered with <see cref="ScheduleAt"/> each time it fires.
    /// </remarks>
    /// <param name="interval">How much game time passes between firings, which must be more than none.</param>
    /// <param name="firstAfter">How long until the first firing, which is one interval when none is stated.</param>
    /// <returns>The handle the deadline is reported under.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The interval is no time at all.</exception>
    public DeadlineId ScheduleEvery(GameDuration interval, GameDuration? firstAfter = null)
    {
        if (interval.IsNone)
        {
            throw new ArgumentOutOfRangeException(
                nameof(interval),
                interval,
                "A schedule that repeats every no time at all would come due on every advance and could never be met.");
        }

        return Hold(checked(AbsoluteNow + (firstAfter ?? interval).Milliseconds), interval);
    }

    /// <summary>Drops a deadline, so it never comes due.</summary>
    /// <param name="deadline">The handle the deadline was registered under.</param>
    /// <returns>Whether the clock was holding it; a handle that was never issued, already fired, or already dropped is not.</returns>
    public bool Cancel(DeadlineId deadline)
    {
        int index = _deadlines.FindIndex(held => held.Id == deadline.Value);
        if (index < 0) return false;
        _deadlines.RemoveAt(index);
        return true;
    }

    /// <summary>Where the clock stands as an instant, from the calendar's own first day.</summary>
    private long AbsoluteNow => checked(_startMilliseconds + _elapsedMilliseconds);

    /// <summary>Registers a deadline at an instant, keeping the handle's number unique for the session.</summary>
    private DeadlineId Hold(long due, GameDuration? interval)
    {
        Deadline held = new(_nextDeadline++, due, interval);
        _deadlines.Add(held);
        return new DeadlineId(held.Id);
    }

    /// <summary>
    /// Brings every deadline the clock has now passed due, once each, and re-arms or drops them.
    /// </summary>
    /// <remarks>
    /// Deadlines are taken in the order they were due, and ones due at the same instant in the order they
    /// were registered, so a report never depends on the order the clock happens to hold them in. A
    /// repeating deadline re-arms from the instant the clock actually reached rather than from the point it
    /// missed, which is what keeps one long advance from firing it over and over.
    /// </remarks>
    private IReadOnlyList<DeadlineDue> Bring(long to)
    {
        List<Deadline> brought = [.. _deadlines.Where(deadline => deadline.Due <= to)];
        if (brought.Count == 0) return [];

        brought.Sort(static (left, right) =>
            left.Due != right.Due ? left.Due.CompareTo(right.Due) : left.Id.CompareTo(right.Id));

        List<DeadlineDue> due = [];
        foreach (Deadline deadline in brought)
        {
            due.Add(new DeadlineDue(
                new DeadlineId(deadline.Id),
                Calendar.FromAbsoluteMilliseconds(deadline.Due),
                Calendar.FromAbsoluteMilliseconds(to),
                GameDuration.FromMilliseconds(to - deadline.Due)));

            if (deadline.Interval is { } interval) deadline.Due = checked(to + interval.Milliseconds);
            else _deadlines.Remove(deadline);
        }

        return due;
    }

    /// <summary>One thing the clock owes at a point in game time, held until the clock passes it.</summary>
    private sealed class Deadline
    {
        internal Deadline(long id, long due, GameDuration? interval)
        {
            Id = id;
            Due = due;
            Interval = interval;
        }

        /// <summary>This deadline's own number, which its handle carries.</summary>
        internal long Id { get; }

        /// <summary>The instant it is due at, which a repeating deadline moves forward as it fires.</summary>
        internal long Due { get; set; }

        /// <summary>How often it repeats, or null when it happens once.</summary>
        internal GameDuration? Interval { get; }
    }
}
