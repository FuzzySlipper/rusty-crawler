using PartyRpg.Kit.Content;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// The one game clock and calendar: the shape of a year, the one way time advances, the boundaries an
/// advance crossed, the deadlines it brought due, day and night, and the day count the world respawns on.
/// </summary>
/// <remarks>
/// Every constant these tests assert on is the test's own choice where the kit allows one — the starting
/// date, the lighting window, the rate game time runs at — and the authored calendar's shape where the
/// product has one. Nothing here needs an engine, a running session, or a wall clock: time moves only when
/// a test moves it.
/// </remarks>
public sealed class ClockTests
{
    /// <summary>The ambient sources a second clock would have to be built from.</summary>
    private static readonly string[] AmbientTimeSources =
    [
        "DateTime",
        "DateTimeOffset",
        "Stopwatch",
        "TimeProvider",
        "Timer",
        "Thread",
        "System.Threading",
        "System.Timers",
        "Task.Delay",
        "Environment.TickCount",
    ];

    private static readonly ContentLayout Layout = new("packs", "imports", "bundles");

    /// <summary>A region that restores its population after three game days.</summary>
    private static readonly PlaceId Home = new("1");

    private static GameCalendar Calendar => GameCalendar.TwelveMonthsOfFourWeeks;

    [Fact]
    public void The_authored_calendar_shapes_a_year_as_twelve_four_week_months()
    {
        // The shipped tables carry no calendar, so these constants are ours; what makes them usable is
        // that months, weeks, and years are whole multiples of each other, which is what keeps the reset
        // intervals content already states in days consistent with a duration stated in months.
        Assert.Equal(12, Calendar.MonthsInYear);
        Assert.Equal(7, Calendar.DaysPerWeek);
        Assert.Equal(24, Calendar.HoursPerDay);
        Assert.Equal(336, Calendar.DaysPerYear);
        Assert.Equal(28, Calendar.MonthLength(1));
        Assert.Equal(4, Calendar.WeeksInMonth(1));

        // A day and a week are amounts of game time only the calendar can resolve.
        Assert.Equal(GameDuration.FromHours(24), Calendar.Days(1));
        Assert.Equal(GameDuration.FromHours(168), Calendar.Weeks(1));
        Assert.Equal(GameDuration.FromHours(72), Calendar.Duration(3, GameDurationUnit.Days));
        Assert.Equal(GameDuration.FromHours(5), Calendar.Duration(5, GameDurationUnit.Hours));
        Assert.Equal(GameDuration.FromMinutes(90), Calendar.Duration(90, GameDurationUnit.Minutes));
        Assert.Equal(GameDuration.FromSeconds(45), Calendar.Duration(45, GameDurationUnit.Seconds));
    }

    [Fact]
    public void A_calendar_whose_month_is_not_a_whole_number_of_weeks_is_refused()
    {
        // A month that is not whole weeks would need a rule saying which weekday some month began on, and
        // this calendar states no such rule, so the definition is refused rather than guessed at.
        ArgumentException fractional = Assert.Throws<ArgumentException>(
            () => new GameCalendar([30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30], daysPerWeek: 7, hoursPerDay: 24));
        Assert.Contains("whole number of 7-day weeks", fractional.Message);

        ArgumentException empty = Assert.Throws<ArgumentException>(() => new GameCalendar([28, 0], daysPerWeek: 7, hoursPerDay: 24));
        Assert.Contains("Month 2", empty.Message);

        Assert.Throws<ArgumentException>(() => new GameCalendar([], daysPerWeek: 7, hoursPerDay: 24));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameCalendar([28], daysPerWeek: 0, hoursPerDay: 24));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameCalendar([28], daysPerWeek: 7, hoursPerDay: 0));
    }

    [Fact]
    public void The_calendar_derives_the_weekday_week_and_day_of_year_from_a_date()
    {
        // No anchor date is needed: whole-week months mean the first day of every month is the first day of
        // a week, and whole-week years mean the first day of every year is too.
        Assert.Equal(1, Calendar.DayOfWeek(new GameDate(100, 1, 1)));
        Assert.Equal(1, Calendar.WeekOfMonth(new GameDate(100, 1, 1)));
        Assert.Equal(1, Calendar.DayOfYear(new GameDate(100, 1, 1)));

        Assert.Equal(7, Calendar.DayOfWeek(new GameDate(100, 1, 7)));
        Assert.Equal(1, Calendar.WeekOfMonth(new GameDate(100, 1, 7)));
        Assert.Equal(1, Calendar.DayOfWeek(new GameDate(100, 1, 8)));
        Assert.Equal(2, Calendar.WeekOfMonth(new GameDate(100, 1, 8)));

        Assert.Equal(1, Calendar.DayOfWeek(new GameDate(100, 2, 1)));
        Assert.Equal(29, Calendar.DayOfYear(new GameDate(100, 2, 1)));
        Assert.Equal(1, Calendar.DayOfWeek(new GameDate(101, 1, 1)));
        Assert.Equal(1, Calendar.DayOfYear(new GameDate(101, 1, 1)));
    }

    [Fact]
    public void A_date_outside_the_calendar_is_refused_by_name()
    {
        ArgumentOutOfRangeException noMonth = Assert.Throws<ArgumentOutOfRangeException>(
            () => Calendar.RequireValid(new GameDate(100, 13, 1), "date"));
        Assert.Contains("12 months", noMonth.Message);

        ArgumentOutOfRangeException noDay = Assert.Throws<ArgumentOutOfRangeException>(
            () => Calendar.RequireValid(new GameDate(100, 2, 29), "date"));
        Assert.Contains("28 days", noDay.Message);

        ArgumentOutOfRangeException noHour = Assert.Throws<ArgumentOutOfRangeException>(
            () => Calendar.RequireValid(new GameDate(100, 1, 1, 24), "date"));
        Assert.Contains("24 hours", noHour.Message);

        Assert.False(Calendar.IsValid(new GameDate(100, 2, 29)));
        Assert.True(Calendar.IsValid(new GameDate(100, 12, 28, 23, 59, 59)));

        // A minute or a second outside its hour is nonsense in every calendar, so the date refuses it
        // before a calendar ever sees it.
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameDate(100, 1, 1, 0, 60));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameDate(100, 1, 1, 0, 0, 60));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameDate(0, 1, 1));
    }

    [Fact]
    public void Adding_time_to_a_date_and_differencing_two_dates_cross_months_and_years()
    {
        GameDate endOfYear = new(100, 12, 28, 23, 0);
        GameDate newYear = Calendar.Add(endOfYear, GameDuration.FromHours(2));

        Assert.Equal(new GameDate(101, 1, 1, 1, 0), newYear);
        Assert.Equal(GameDuration.FromHours(2), Calendar.Between(endOfYear, newYear));
        Assert.Equal(new GameDate(100, 6, 11), Calendar.Add(new GameDate(100, 6, 10), GameDuration.FromHours(24)));
        Assert.Equal(GameDuration.FromHours(48), Calendar.Between(new GameDate(100, 1, 1), new GameDate(100, 1, 3)));

        // The boundaries of a span belong to it when they lie after its first instant and no later than
        // its last, so a span that ends exactly on a boundary has crossed it.
        PeriodCrossings acrossMidnight = Calendar.Crossed(new GameDate(100, 1, 1, 23, 0), new GameDate(100, 1, 2, 1, 0));
        Assert.Equal(new PeriodCrossings(Hours: 2, Days: 1, Weeks: 0, Months: 0, Years: 0), acrossMidnight);

        PeriodCrossings acrossYear = Calendar.Crossed(new GameDate(100, 12, 28, 23, 0), new GameDate(101, 1, 1));
        Assert.Equal(1, acrossYear.Days);
        Assert.Equal(1, acrossYear.Hours);
        Assert.Equal(1, acrossYear.Months);
        Assert.Equal(1, acrossYear.Years);
    }

    [Fact]
    public void A_duration_or_a_difference_that_runs_backwards_is_refused()
    {
        // Every duration is a charge, an interval, or an elapsed span, so a negative one is refused where
        // it is built; the clock has no way to move backwards because it has no negative amount to move by.
        Assert.Throws<ArgumentOutOfRangeException>(() => GameDuration.FromMilliseconds(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => GameDuration.FromSeconds(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => GameDuration.FromMinutes(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => GameDuration.FromHours(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Calendar.Days(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Calendar.Weeks(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Calendar.Duration(-1, GameDurationUnit.Hours));
        Assert.Throws<ArgumentOutOfRangeException>(() => Calendar.Between(new GameDate(101, 1, 1), new GameDate(100, 1, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Calendar.Crossed(new GameDate(101, 1, 1), new GameDate(100, 1, 1)));

        GameClock clock = Clock();
        Assert.Equal(GameDuration.None, clock.Elapsed);
        Assert.Equal(0, clock.ElapsedGameDays);
    }

    [Fact]
    public void A_calendar_may_shape_months_of_different_lengths_and_still_keep_whole_weeks()
    {
        // The authored calendar is uniform, but the definition is not: any month that holds whole weeks is
        // legal, and the arithmetic has to follow the lengths a ruleset states rather than assume one.
        GameCalendar uneven = new([7, 14, 21], daysPerWeek: 7, hoursPerDay: 24);

        Assert.Equal(42, uneven.DaysPerYear);
        Assert.Equal(GameDuration.FromHours(24), uneven.Days(1));
        Assert.Equal(GameDuration.FromHours(168), uneven.Weeks(1));

        // A month ends where its own length says, and the next one begins there.
        Assert.Equal(new GameDate(100, 2, 1), uneven.Add(new GameDate(100, 1, 7), GameDuration.FromHours(24)));
        Assert.Equal(new GameDate(100, 3, 1), uneven.Add(new GameDate(100, 2, 14), GameDuration.FromHours(24)));
        Assert.Equal(new GameDate(101, 1, 1), uneven.Add(new GameDate(100, 3, 21), GameDuration.FromHours(24)));
        Assert.Equal(15, uneven.DayOfYear(new GameDate(100, 2, 8)));

        // Every month still begins on the first day of a week, so a weekday is always derivable.
        Assert.Equal(1, uneven.DayOfWeek(new GameDate(100, 1, 1)));
        Assert.Equal(1, uneven.DayOfWeek(new GameDate(100, 2, 1)));
        Assert.Equal(1, uneven.DayOfWeek(new GameDate(100, 3, 1)));
        Assert.Equal(1, uneven.DayOfWeek(new GameDate(101, 1, 1)));
    }

    [Fact]
    public void Nothing_but_an_advance_moves_the_clock()
    {
        GameClock clock = Clock();
        GameDate before = clock.Now;

        // Reading the clock, holding a deadline, and dropping it again are not time passing.
        DeadlineId dropped = clock.ScheduleAfter(Calendar.Days(1));
        Assert.True(clock.Cancel(dropped));
        for (int read = 0; read < 100; read++) Assert.Equal(before, clock.Now);
        Assert.Equal(GameDuration.None, clock.Elapsed);
        Assert.Equal(before, clock.Now);

        // An advance of no time crosses nothing and brings nothing due, even a deadline waiting for exactly
        // this instant: the clock has not moved, so it has passed nothing.
        DeadlineId now = clock.ScheduleAfter(GameDuration.None);
        ClockAdvance still = clock.Advance(GameDuration.None);
        Assert.False(still.Moved);
        Assert.Equal(before, still.From);
        Assert.Equal(before, still.To);
        Assert.Equal(PeriodCrossings.None, still.Crossings);
        Assert.Empty(still.Due);
        Assert.Equal(before, clock.Now);
        Assert.Equal(1, clock.PendingDeadlines);

        // The next advance that moves any time at all crosses the point it was waiting at.
        DeadlineDue due = Assert.Single(clock.Advance(GameDuration.FromSeconds(1)).Due);
        Assert.Equal(now, due.Deadline);
        Assert.Equal(before, due.Scheduled);
        Assert.Equal(GameDuration.FromSeconds(1), due.Late);
    }

    [Fact]
    public void An_hour_boundary_is_crossed_once_however_the_advance_is_split()
    {
        // Half past nine in one advance, and the same half hour in thirty: the boundary at ten o'clock is
        // crossed once either way, and only by the advance that reaches it.
        GameClock single = Clock(hour: 9, minute: 30);
        ClockAdvance advance = single.Advance(GameDuration.FromMinutes(30));
        Assert.Equal(1, advance.Crossings.Hours);
        Assert.Equal(new GameDate(100, 1, 1, 10, 0), advance.To);

        GameClock split = Clock(hour: 9, minute: 30);
        int crossed = 0;
        for (int minute = 0; minute < 30; minute++) crossed += split.Advance(GameDuration.FromMinutes(1)).Crossings.Hours;

        Assert.Equal(1, crossed);
        Assert.Equal(single.Now, split.Now);
        Assert.Equal(0, split.Advance(GameDuration.FromSeconds(59)).Crossings.Hours);
    }

    [Fact]
    public void A_day_boundary_is_crossed_once_however_far_the_advance_runs()
    {
        GameClock clock = Clock(hour: 22);
        ClockAdvance advance = clock.Advance(GameDuration.FromHours(25));

        // One midnight in twenty-five hours, counted as one day and twenty-five hours: an advance that
        // crossed a day once per hour would restore a place's population twenty-five times.
        Assert.Equal(1, advance.Crossings.Days);
        Assert.Equal(25, advance.Crossings.Hours);
        Assert.Equal(new GameDate(100, 1, 1, 22, 0), advance.From);
        Assert.Equal(new GameDate(100, 1, 2, 23, 0), advance.To);

        GameClock week = Clock();
        ClockAdvance travel = week.Advance(Calendar.Weeks(1));
        Assert.Equal(7, travel.Crossings.Days);
        Assert.Equal(168, travel.Crossings.Hours);
        Assert.Equal(1, travel.Crossings.Weeks);
        Assert.Equal(0, travel.Crossings.Months);
        Assert.Equal(new GameDate(100, 1, 8, 9, 0), travel.To);
    }

    [Fact]
    public void The_day_count_is_whole_days_of_elapsed_game_time()
    {
        // The world's seam counts elapsed days, not dates: a session that begins at noon reaches its second
        // day at noon, exactly one day later, whatever the calendar shows.
        GameClock clock = Clock(hour: 12);
        clock.Advance(GameDuration.FromHours(23));

        Assert.Equal(0, clock.ElapsedGameDays);
        Assert.Equal(new GameDate(100, 1, 2, 11, 0), clock.Now);

        clock.Advance(GameDuration.FromHours(1));
        Assert.Equal(1, clock.ElapsedGameDays);

        clock.Advance(Calendar.Days(2));
        Assert.Equal(3, clock.ElapsedGameDays);
        Assert.Equal(new GameDate(100, 1, 4, 12, 0), clock.Now);
    }

    [Fact]
    public void The_whole_elapsed_days_the_world_respawns_on_come_from_the_clock()
    {
        // The world's own time seam, satisfied by the clock itself rather than by a stand-in: the world is
        // built over the clock, so what restores a place's population is the clock reaching its third day.
        GameClock clock = Clock();
        PlaceGraph graph = World();
        PartyPoseOwner party = new(
            new PartyPose(Home, new PlacePose(10, 20, 0, 512, 0)),
            new FacingRule(unitsPerTurn: 2048, minimumPitch: -512, maximumPitch: 512));
        using SessionWorld world = new(graph, party, new PlaceStateLedger(graph, PlaceRespawnRule.FromContent()), new FreeTravel(), clock);
        world.Places.MarkCleared(Home);

        // Twenty-three hours is still the session's first day, so the place's three days have not elapsed.
        clock.Advance(GameDuration.FromHours(23));
        Assert.Equal(0, ((IWorldTimeSource)clock).ElapsedGameDays);
        Assert.Empty(world.AdvanceTime());
        Assert.True(world.Places.StateOf(Home).Cleared);

        // The clock's third day is when the place comes back, and it comes back once.
        clock.Advance(Calendar.Days(3));
        PlaceState restored = Assert.Single(world.AdvanceTime());
        Assert.Equal(3, clock.ElapsedGameDays);
        Assert.Equal(Home, restored.Place);
        Assert.False(world.Places.StateOf(Home).Cleared);
        Assert.True(world.Places.StateOf(Home).Visited);
    }

    [Fact]
    public void Admitted_real_time_becomes_game_time_at_the_clocks_own_scale()
    {
        // Thirty game seconds per real second, admitted in sixty-four steps of a sixty-fourth of a second:
        // the clock's resolution is finer than a step, so no part of the interval is lost or counted twice.
        GameClock clock = Clock(scale: 30);
        for (int step = 0; step < 64; step++) clock.AdvanceAdmittedSeconds(1.0 / 64.0);

        Assert.Equal(GameDuration.FromSeconds(30), clock.Elapsed);
        Assert.Equal(new GameDate(100, 1, 1, 9, 0, 30), clock.Now);

        // An interval that is nothing moves nothing, and one the engine could not have measured is refused
        // rather than folded into the clock as negative or unmeasurable time.
        Assert.False(clock.AdvanceAdmittedSeconds(0).Moved);
        Assert.Throws<ArgumentOutOfRangeException>(() => clock.AdvanceAdmittedSeconds(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => clock.AdvanceAdmittedSeconds(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => clock.AdvanceAdmittedSeconds(double.PositiveInfinity));

        Assert.Equal(GameDuration.FromSeconds(30), clock.Elapsed);
        Assert.Equal(new GameDate(100, 1, 1, 9, 0, 30), clock.Now);
    }

    [Fact]
    public void A_deadline_fires_once_when_the_clock_passes_it()
    {
        GameClock clock = Clock();
        DeadlineId training = clock.ScheduleAfter(GameDuration.FromHours(3));

        // Two hours is not the point yet.
        Assert.Empty(clock.Advance(GameDuration.FromHours(2)).Due);
        Assert.Equal(1, clock.PendingDeadlines);

        // The hour that reaches it brings it due exactly once, and the sixty minutes that hour is made of
        // bring it due once between them rather than once per update.
        List<DeadlineDue> fired = [];
        for (int minute = 0; minute < 60; minute++) fired.AddRange(clock.Advance(GameDuration.FromMinutes(1)).Due);

        DeadlineDue due = Assert.Single(fired);
        Assert.Equal(training, due.Deadline);
        Assert.Equal(new GameDate(100, 1, 1, 12, 0), due.Scheduled);
        Assert.Equal(due.Scheduled, due.Fired);
        Assert.Equal(GameDuration.None, due.Late);
        Assert.Equal(0, clock.PendingDeadlines);

        // A later advance passes the same point again and brings nothing due again.
        Assert.Empty(clock.Advance(GameDuration.FromHours(1)).Due);
    }

    [Fact]
    public void A_deadline_the_clock_passed_long_ago_fires_once_and_reports_how_late_it_is()
    {
        GameClock clock = Clock();
        DeadlineId wake = clock.ScheduleAfter(GameDuration.FromHours(3));

        DeadlineDue due = Assert.Single(clock.Advance(Calendar.Days(10)).Due);

        Assert.Equal(wake, due.Deadline);
        Assert.Equal(new GameDate(100, 1, 1, 12, 0), due.Scheduled);
        Assert.Equal(clock.Now, due.Fired);
        Assert.Equal(GameDuration.FromHours(237), due.Late);
        Assert.Equal(0, clock.PendingDeadlines);
    }

    [Fact]
    public void A_repeating_deadline_fires_once_per_advance_and_re_arms_from_when_it_fired()
    {
        GameClock clock = Clock();
        DeadlineId restock = clock.ScheduleEvery(Calendar.Days(1));

        // Ten days in one advance bring it due once, because it re-arms from the moment it fired rather
        // than from the interval it missed.
        DeadlineDue first = Assert.Single(clock.Advance(Calendar.Days(10)).Due);
        Assert.Equal(restock, first.Deadline);
        Assert.Equal(new GameDate(100, 1, 2, 9, 0), first.Scheduled);
        Assert.Equal(clock.Now, first.Fired);
        Assert.Equal(Calendar.Days(9), first.Late);
        Assert.Equal(1, clock.PendingDeadlines);

        // A day later it is due again, on time, which is what a schedule rather than a one-off looks like.
        DeadlineDue second = Assert.Single(clock.Advance(Calendar.Days(1)).Due);
        Assert.Equal(restock, second.Deadline);
        Assert.Equal(GameDuration.None, second.Late);
        Assert.Equal(1, clock.PendingDeadlines);
    }

    [Fact]
    public void Deadlines_due_at_the_same_instant_fire_in_the_order_they_were_registered()
    {
        GameClock clock = Clock();
        DeadlineId first = clock.ScheduleAfter(GameDuration.FromHours(1));
        DeadlineId second = clock.ScheduleAfter(GameDuration.FromHours(1));
        DeadlineId earlier = clock.ScheduleAfter(GameDuration.FromMinutes(30));

        IReadOnlyList<DeadlineDue> due = clock.Advance(GameDuration.FromHours(2)).Due;

        Assert.Equal(new[] { earlier, first, second }, due.Select(fired => fired.Deadline));
    }

    [Fact]
    public void A_deadline_registered_for_a_point_on_the_calendar_fires_when_the_clock_reaches_it()
    {
        GameClock clock = Clock();
        DeadlineId opening = clock.ScheduleAt(new GameDate(100, 1, 1, 12, 0));

        Assert.Empty(clock.Advance(GameDuration.FromHours(2)).Due);
        DeadlineDue due = Assert.Single(clock.Advance(GameDuration.FromHours(1)).Due);
        Assert.Equal(opening, due.Deadline);
        Assert.Equal(clock.Now, due.Fired);

        // A point the clock has already passed is refused: what a point in the past would mean is the
        // caller's to say, and a deadline there would come due at once for a reason the calendar cannot show.
        Assert.Throws<ArgumentOutOfRangeException>(() => clock.ScheduleAt(new GameDate(100, 1, 1, 9, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => clock.ScheduleAt(new GameDate(100, 13, 1)));
    }

    [Fact]
    public void A_cancelled_deadline_never_fires()
    {
        GameClock clock = Clock();
        DeadlineId deadline = clock.ScheduleAfter(GameDuration.FromHours(1));

        Assert.True(clock.Cancel(deadline));
        Assert.False(clock.Cancel(deadline));
        Assert.Equal(0, clock.PendingDeadlines);
        Assert.Empty(clock.Advance(Calendar.Days(2)).Due);
    }

    [Fact]
    public void A_repeating_schedule_must_repeat_by_something()
    {
        // A schedule that repeats every no time at all would be due on every advance and could never be met.
        Assert.Throws<ArgumentOutOfRangeException>(() => Clock().ScheduleEvery(GameDuration.None));
    }

    [Fact]
    public void Day_and_night_come_from_the_clock_and_the_windows_policy()
    {
        GameClock clock = Clock(hour: 5, minute: 59);
        Assert.True(clock.IsNight);
        Assert.False(clock.IsDaylight);

        // Dawn is the first minute of daylight, and dusk the first minute of dark.
        clock.Advance(GameDuration.FromMinutes(1));
        Assert.Equal(new GameDate(100, 1, 1, 6, 0), clock.Now);
        Assert.True(clock.IsDaylight);
        Assert.False(clock.IsNight);

        clock.Advance(GameDuration.FromHours(12));
        Assert.Equal(new GameDate(100, 1, 1, 18, 0), clock.Now);
        Assert.False(clock.IsDaylight);
        Assert.True(clock.IsNight);
    }

    [Fact]
    public void A_clock_refuses_a_start_or_a_lighting_window_its_calendar_does_not_hold()
    {
        // A date the calendar has no day for cannot be the session's start.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GameClock(Calendar, new GameDate(100, 2, 29), GameTimeScale.RealTime, Daylight()));

        // Nor can a lighting window whose hour the calendar's own day does not have.
        GameCalendar shortDays = new([4], daysPerWeek: 1, hoursPerDay: 20);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GameClock(shortDays, new GameDate(100, 1, 1), GameTimeScale.RealTime, new DaylightWindow(new TimeOfDay(6, 0), new TimeOfDay(23, 0))));

        // A window that ends before it begins would leave every hour of the day dark.
        Assert.Throws<ArgumentException>(() => new DaylightWindow(new TimeOfDay(18, 0), new TimeOfDay(6, 0)));

        // And a rate that is not a finite, positive amount would stop or reverse the clock.
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameTimeScale(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameTimeScale(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameTimeScale(double.PositiveInfinity));
    }

    [Fact]
    public void A_travel_charge_becomes_game_time_through_the_calendar()
    {
        // The way layer states a charge with its unit and refuses to guess how long a day is; the calendar
        // the clock keeps is the one place that ratio is applied.
        Assert.Equal(GameDuration.FromMinutes(90), TravelTimeConversion.Elapsed(Calendar, new TravelTime(90, TravelTimeUnit.Minutes)));
        Assert.Equal(GameDuration.FromHours(6), TravelTimeConversion.Elapsed(Calendar, new TravelTime(6, TravelTimeUnit.Hours)));
        Assert.Equal(Calendar.Days(3), TravelTimeConversion.Elapsed(Calendar, new TravelTime(3, TravelTimeUnit.Days)));

        GameClock clock = Clock();
        clock.Advance(TravelTimeConversion.Elapsed(Calendar, new TravelTime(3, TravelTimeUnit.Days)));

        Assert.Equal(new GameDate(100, 1, 4, 9, 0), clock.Now);
        Assert.Equal(3, clock.ElapsedGameDays);
    }

    [Fact]
    public void The_kit_holds_no_second_clock_timer_or_thread()
    {
        // The clock is the only owner of time in the product, so no source in the kit may reach for an
        // ambient one. The scan reads the kit's own sources rather than what the compiler produced, because
        // a timer that arrived from a package would be exactly as much a second clock as one written here.
        string kit = Path.Combine(RepositoryRoot(), "src", "PartyRpg.Kit");
        string[] sources =
        [
            .. Directory.EnumerateFiles(kit, "*.cs", SearchOption.AllDirectories)
                .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)),
        ];
        Assert.NotEmpty(sources);

        foreach (string source in sources)
        {
            string text = File.ReadAllText(source);
            foreach (string ambient in AmbientTimeSources)
            {
                Assert.False(
                    text.Contains(ambient, StringComparison.Ordinal),
                    $"{Path.GetFileName(source)} names '{ambient}': the kit's one clock is advanced by its callers, and a timer, a thread, or a wall clock beside it would be a second clock.");
            }
        }
    }

    /// <summary>The lighting policy these tests run the clock with, which is a ruleset's to state.</summary>
    private static DaylightWindow Daylight() => new(new TimeOfDay(6, 0), new TimeOfDay(18, 0));

    /// <summary>A clock at the given time of day, on the calendar's own first day.</summary>
    private static GameClock Clock(int hour = 9, int minute = 0, double scale = 30) =>
        new(Calendar, new GameDate(100, 1, 1, hour, minute), new GameTimeScale(scale), Daylight());

    /// <summary>The place the respawn seam is proved against: a region that resets after three game days.</summary>
    private static PlaceGraph World() => PlaceGraphLoader.Load(
        ContentCatalogLoader.Load(
            new InMemoryContentSource()
                .Add("packs/world/pack.json", Manifest())
                .Add("packs/world/places.json", Document(
                    "places",
                    "place",
                    """{ "id": "1", "kind": "region", "name": "Home", "respawnDays": 3 }""")),
            Layout).RequireValid());

    /// <summary>The repository root above the test assembly, which is where the kit's sources are.</summary>
    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")) &&
                Directory.Exists(Path.Combine(directory.FullName, "src")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find the repository root above the test assembly.");
    }

    private static string Manifest() =>
        """
        {
          "schemaVersion": 1,
          "packId": "world",
          "kind": "definitions",
          "provenance": { "description": "test content" },
          "documents": [
            { "path": "places.json", "documentId": "places", "definitionKind": "place" }
          ]
        }
        """;

    private static string Document(string documentId, string definitionKind, string entries) =>
        $$"""
        {
          "documentId": "{{documentId}}",
          "definitionKind": "{{definitionKind}}",
          "entries": [ {{entries}} ]
        }
        """;

    /// <summary>Walking costs nothing here, so the clock is the only thing this world's journey charges.</summary>
    private sealed class FreeTravel : ITravelCostRule
    {
        public TravelCostQuote Quote(TransitionRequest request) => TravelCostQuote.Payable(TravelCost.Free);
    }
}
