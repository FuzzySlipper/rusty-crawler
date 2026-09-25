namespace PartyRpg.Kit.Time;

/// <summary>
/// The shape of a game year: how many days each month holds, how many days a week holds, and how many
/// hours a day holds.
/// </summary>
/// <remarks>
/// <para>
/// The shipped rule tables carry no calendar at all, so every constant here is authored rather than
/// extracted, and it is the calendar a product states rather than a number hidden in the clock. What the
/// shape decides is everything downstream: how long a day is, where a week begins, how a duration stated
/// in days or weeks becomes an amount of game time, and how a saved date reads back.
/// </para>
/// <para>
/// <b>Months hold whole weeks.</b> A day of the week is derived from a day of the month, with no anchor
/// date and no rule saying which weekday some year began on, because each month starts on the first day
/// of a week. That is what makes "the shop restocks on the first day of the week" a fact the calendar can
/// answer for any date, and it is the one structural rule this definition enforces.
/// </para>
/// <para>
/// <b>There is no leap rule.</b> Every year holds exactly the sum of its months, which keeps a date, a
/// respawn interval, and a duration in months consistent without a rule the shipped content never states.
/// A ruleset that needs a longer year states it in the months it already has.
/// </para>
/// <para>
/// Seconds, minutes, and hours divide each other by sixty in every calendar and are constants of
/// <see cref="GameDuration"/>; only the shape of the day and the year is authored here.
/// </para>
/// </remarks>
public sealed class GameCalendar
{
    /// <summary>
    /// The furthest year a date may name, which keeps a calendar's arithmetic inside the numbers elapsed
    /// game time is measured in. It is a guard rather than a story: a million years of game time is far
    /// beyond any product, and a date past it would be refused rather than wrapped.
    /// </summary>
    private const int FurthestYear = 1_000_000;

    private static readonly GameCalendar Authored = new(
        [28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28],
        daysPerWeek: 7,
        hoursPerDay: 24);

    private readonly int[] _daysPerMonth;
    private readonly int[] _monthStartDays;

    /// <summary>Creates a calendar definition.</summary>
    /// <param name="daysPerMonth">How many days each month of the year holds, in calendar order.</param>
    /// <param name="daysPerWeek">How many days a week holds, which each month must be a whole number of.</param>
    /// <param name="hoursPerDay">How many hours a day holds.</param>
    /// <exception cref="ArgumentNullException">No month structure was supplied.</exception>
    /// <exception cref="ArgumentException">
    /// The year has no months, a month holds no days or does not hold a whole number of weeks, or the year
    /// is longer than the game time this kit can count.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">A week or a day holds no unit at all.</exception>
    public GameCalendar(IReadOnlyList<int> daysPerMonth, int daysPerWeek, int hoursPerDay)
    {
        ArgumentNullException.ThrowIfNull(daysPerMonth);
        ArgumentOutOfRangeException.ThrowIfLessThan(daysPerWeek, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(hoursPerDay, 1);
        if (daysPerMonth.Count == 0)
        {
            throw new ArgumentException("A calendar's year holds at least one month.", nameof(daysPerMonth));
        }

        int[] months = new int[daysPerMonth.Count];
        int[] starts = new int[daysPerMonth.Count];
        int dayOfYear = 0;
        for (int month = 0; month < months.Length; month++)
        {
            int days = daysPerMonth[month];
            if (days < 1)
            {
                throw new ArgumentException(
                    $"Month {month + 1} of the year holds {days} days, so a date in it could never be a date.",
                    nameof(daysPerMonth));
            }

            // Whole-week months are the rule that lets a weekday be derived from a day of the month, so a
            // month that breaks it is refused here rather than producing a weekday nobody authored.
            if (days % daysPerWeek != 0)
            {
                throw new ArgumentException(
                    $"Month {month + 1} of the year holds {days} days, which is not a whole number of {daysPerWeek}-day weeks, so which day of the week a date in it falls on would need a rule this calendar does not state.",
                    nameof(daysPerMonth));
            }

            if (days > int.MaxValue - dayOfYear)
            {
                throw new ArgumentException(
                    $"The calendar's year holds more days than the game time this kit can count.",
                    nameof(daysPerMonth));
            }

            starts[month] = dayOfYear;
            months[month] = days;
            dayOfYear += days;
        }

        // A year long enough that a date at the furthest year would leave the numbers elapsed game time is
        // measured in is refused here, rather than overflowing the instant somebody adds a month to it.
        long hoursPerYear = (long)dayOfYear * hoursPerDay;
        long hoursThatFit = long.MaxValue / GameDuration.SecondsPerHour / GameDuration.MillisecondsPerSecond / FurthestYear;
        if (hoursPerYear > hoursThatFit)
        {
            throw new ArgumentException(
                $"A year of {dayOfYear} days of {hoursPerDay} hours is longer than the game time this kit can count.",
                nameof(daysPerMonth));
        }

        _daysPerMonth = months;
        _monthStartDays = starts;
        DaysPerWeek = daysPerWeek;
        HoursPerDay = hoursPerDay;
        DaysPerYear = dayOfYear;
        DayMilliseconds = (long)hoursPerDay * GameDuration.SecondsPerHour * GameDuration.MillisecondsPerSecond;
    }

    /// <summary>
    /// The calendar this family's authored content is shaped around: twelve months of four seven-day
    /// weeks, so a year is 336 days.
    /// </summary>
    /// <remarks>
    /// The shape is ours, and it is chosen to agree with the content already authored: the reset intervals
    /// the imported places carry are stated in whole days and are whole multiples of this year, so a place
    /// that resets after half a year, a year, or two years resets after a whole number of months under this
    /// calendar rather than part way through one. Month names, feast days, and starting dates are not here:
    /// what a date is called is presentation meaning, and which date a session begins on is scenario state.
    /// </remarks>
    public static GameCalendar TwelveMonthsOfFourWeeks => Authored;

    /// <summary>How many days each month of the year holds, in calendar order.</summary>
    public IReadOnlyList<int> DaysPerMonth => _daysPerMonth;

    /// <summary>How many months the year holds.</summary>
    public int MonthsInYear => _daysPerMonth.Length;

    /// <summary>How many days a week holds.</summary>
    public int DaysPerWeek { get; }

    /// <summary>How many hours a day holds.</summary>
    public int HoursPerDay { get; }

    /// <summary>How many days the year holds, which is every month added up.</summary>
    public int DaysPerYear { get; }

    /// <summary>How long a day of this calendar is.</summary>
    internal long DayMilliseconds { get; }

    /// <summary>How many days a month holds.</summary>
    /// <param name="month">The month, counted from one.</param>
    /// <returns>The number of days in that month.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The calendar has no such month.</exception>
    public int MonthLength(int month)
    {
        if (month < 1 || month > MonthsInYear)
        {
            throw new ArgumentOutOfRangeException(nameof(month), month, $"A year of this calendar holds {MonthsInYear} months.");
        }

        return _daysPerMonth[month - 1];
    }

    /// <summary>How many whole weeks a month holds.</summary>
    /// <param name="month">The month, counted from one.</param>
    /// <returns>The number of weeks in that month.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The calendar has no such month.</exception>
    public int WeeksInMonth(int month) => MonthLength(month) / DaysPerWeek;

    /// <summary>How long a number of days is, which only this calendar can answer.</summary>
    /// <param name="days">The number of days, which cannot be negative.</param>
    /// <returns>The game time those days cover.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The number of days is negative.</exception>
    /// <exception cref="OverflowException">The span is longer than the game time this kit can count.</exception>
    public GameDuration Days(long days)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(days);
        return GameDuration.FromMilliseconds(checked(days * DayMilliseconds));
    }

    /// <summary>How long a number of weeks is, which only this calendar can answer.</summary>
    /// <param name="weeks">The number of weeks, which cannot be negative.</param>
    /// <returns>The game time those weeks cover.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The number of weeks is negative.</exception>
    /// <exception cref="OverflowException">The span is longer than the game time this kit can count.</exception>
    public GameDuration Weeks(long weeks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(weeks);
        return Days(checked(weeks * DaysPerWeek));
    }

    /// <summary>How long an amount of game time stated in a unit is.</summary>
    /// <remarks>
    /// This is the one place a duration stated as a number and a unit becomes an amount of time, because
    /// seconds, minutes, and hours are absolute while days and weeks are this calendar's. Every owner that
    /// reads a duration out of content — a travel charge, a spell's duration, a rest — states the unit and
    /// lets the calendar resolve it, so no two owners can disagree about how long a day is.
    /// </remarks>
    /// <param name="amount">The amount, which cannot be negative.</param>
    /// <param name="unit">The unit the amount is stated in.</param>
    /// <returns>The game time the amount covers.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The amount is negative, or the unit is not a unit.</exception>
    /// <exception cref="OverflowException">The span is longer than the game time this kit can count.</exception>
    public GameDuration Duration(long amount, GameDurationUnit unit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        return unit switch
        {
            GameDurationUnit.Seconds => GameDuration.FromSeconds(amount),
            GameDurationUnit.Minutes => GameDuration.FromMinutes(amount),
            GameDurationUnit.Hours => GameDuration.FromHours(amount),
            GameDurationUnit.Days => Days(amount),
            GameDurationUnit.Weeks => Weeks(amount),
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unknown unit of game time."),
        };
    }

    /// <summary>Whether a date is a point this calendar holds.</summary>
    /// <param name="date">The date to judge.</param>
    public bool IsValid(GameDate date) =>
        date.Year <= FurthestYear &&
        date.Month <= MonthsInYear &&
        date.Day <= _daysPerMonth[date.Month - 1] &&
        date.Hour < HoursPerDay;

    /// <summary>Refuses a date this calendar does not hold.</summary>
    /// <param name="date">The date to check.</param>
    /// <param name="parameterName">The parameter the date arrived in, so the refusal names its caller.</param>
    /// <exception cref="ArgumentOutOfRangeException">The calendar has no such month, day, hour, or year.</exception>
    public void RequireValid(GameDate date, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parameterName);
        if (date.Month > MonthsInYear)
        {
            throw new ArgumentOutOfRangeException(parameterName, date, $"A year of this calendar holds {MonthsInYear} months.");
        }

        if (date.Day > _daysPerMonth[date.Month - 1])
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                date,
                $"Month {date.Month} of this calendar holds {_daysPerMonth[date.Month - 1]} days.");
        }

        if (date.Hour >= HoursPerDay)
        {
            throw new ArgumentOutOfRangeException(parameterName, date, $"A day of this calendar holds {HoursPerDay} hours.");
        }

        if (date.Year > FurthestYear)
        {
            throw new ArgumentOutOfRangeException(parameterName, date, $"Year {date.Year} is further ahead than the game time this kit can count.");
        }
    }

    /// <summary>Refuses a time of day this calendar's day does not hold.</summary>
    /// <param name="at">The time of day to check.</param>
    /// <param name="parameterName">The parameter the time arrived in, so the refusal names its caller.</param>
    /// <exception cref="ArgumentOutOfRangeException">The calendar's day has no such hour.</exception>
    public void RequireValid(TimeOfDay at, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parameterName);
        if (at.Hour >= HoursPerDay)
        {
            throw new ArgumentOutOfRangeException(parameterName, at, $"A day of this calendar holds {HoursPerDay} hours.");
        }
    }

    /// <summary>Which day of the year a date falls on, counted from one.</summary>
    /// <param name="date">The date to read.</param>
    /// <exception cref="ArgumentOutOfRangeException">The calendar does not hold the date.</exception>
    public int DayOfYear(GameDate date)
    {
        RequireValid(date, nameof(date));
        return _monthStartDays[date.Month - 1] + date.Day;
    }

    /// <summary>Which day of the week a date falls on, counted from one.</summary>
    /// <remarks>
    /// No anchor date is needed: a month holds whole weeks, so the first day of every month is the first
    /// day of a week, and the weekday is a fact about the day of the month and nothing else.
    /// </remarks>
    /// <param name="date">The date to read.</param>
    /// <exception cref="ArgumentOutOfRangeException">The calendar does not hold the date.</exception>
    public int DayOfWeek(GameDate date)
    {
        RequireValid(date, nameof(date));
        return (int)(DayIndex(date) % DaysPerWeek) + 1;
    }

    /// <summary>Which week of its month a date falls in, counted from one.</summary>
    /// <param name="date">The date to read.</param>
    /// <exception cref="ArgumentOutOfRangeException">The calendar does not hold the date.</exception>
    public int WeekOfMonth(GameDate date)
    {
        RequireValid(date, nameof(date));
        return ((date.Day - 1) / DaysPerWeek) + 1;
    }

    /// <summary>Moves a date forward by an amount of game time.</summary>
    /// <param name="from">The date to move from.</param>
    /// <param name="elapsed">The game time to move by.</param>
    /// <returns>The date the elapsed time reaches.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The calendar does not hold the starting date.</exception>
    /// <exception cref="OverflowException">The move leaves the game time this kit can count.</exception>
    public GameDate Add(GameDate from, GameDuration elapsed)
    {
        RequireValid(from, nameof(from));
        return FromAbsoluteMilliseconds(checked(AbsoluteMilliseconds(from) + elapsed.Milliseconds));
    }

    /// <summary>How much game time lies between two dates.</summary>
    /// <param name="from">The date to measure from.</param>
    /// <param name="to">The date to measure to, which cannot be earlier than the one measured from.</param>
    /// <returns>The game time between them.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The calendar does not hold a date, or the second one is earlier.</exception>
    public GameDuration Between(GameDate from, GameDate to)
    {
        RequireValid(from, nameof(from));
        RequireValid(to, nameof(to));
        long difference = AbsoluteMilliseconds(to) - AbsoluteMilliseconds(from);
        if (difference < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(to),
                to,
                $"The calendar cannot measure from {from} to {to}: game time runs forward, so the second date cannot be earlier than the first.");
        }

        return GameDuration.FromMilliseconds(difference);
    }

    /// <summary>How many boundaries of each period lie between two dates, each counted once.</summary>
    /// <remarks>
    /// A boundary belongs to the span when it lies after the first date and no later than the second, which
    /// is what makes an advance of exactly one day cross one day boundary rather than none or two. Each
    /// count is the difference of the period's index on either side, so no boundary can be counted twice
    /// however long the span is.
    /// </remarks>
    /// <param name="from">The date the span begins at.</param>
    /// <param name="to">The date the span ends at, which cannot be earlier than the one it begins at.</param>
    /// <returns>The boundaries the span passed.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The calendar does not hold a date, or the second one is earlier.</exception>
    /// <exception cref="OverflowException">The span crosses more boundaries than this kit can count.</exception>
    public PeriodCrossings Crossed(GameDate from, GameDate to)
    {
        RequireValid(from, nameof(from));
        RequireValid(to, nameof(to));
        long fromMilliseconds = AbsoluteMilliseconds(from);
        long toMilliseconds = AbsoluteMilliseconds(to);
        if (toMilliseconds < fromMilliseconds)
        {
            throw new ArgumentOutOfRangeException(
                nameof(to),
                to,
                $"The calendar cannot count boundaries from {from} to {to}: game time runs forward, so the second date cannot be earlier than the first.");
        }

        long hourMilliseconds = GameDuration.SecondsPerHour * GameDuration.MillisecondsPerSecond;
        long fromDay = DayIndex(from);
        long toDay = DayIndex(to);
        return new PeriodCrossings(
            checked((int)((toMilliseconds / hourMilliseconds) - (fromMilliseconds / hourMilliseconds))),
            checked((int)(toDay - fromDay)),
            checked((int)((toDay / DaysPerWeek) - (fromDay / DaysPerWeek))),
            checked(((to.Year - from.Year) * MonthsInYear) + (to.Month - from.Month)),
            to.Year - from.Year);
    }

    /// <summary>
    /// How many whole days from the calendar's own first day a date lies, which is the index boundaries
    /// are counted on.
    /// </summary>
    internal long DayIndex(GameDate date) =>
        ((long)(date.Year - 1) * DaysPerYear) + _monthStartDays[date.Month - 1] + (date.Day - 1);

    /// <summary>
    /// How many milliseconds from the calendar's own first day a date lies, which is what makes two
    /// instants comparable and what the clock's position is measured in.
    /// </summary>
    internal long AbsoluteMilliseconds(GameDate date)
    {
        long secondsOfDay =
            ((long)date.Hour * GameDuration.SecondsPerHour) +
            (date.Minute * GameDuration.SecondsPerMinute) +
            date.Second;
        return (DayIndex(date) * DayMilliseconds) + (secondsOfDay * GameDuration.MillisecondsPerSecond);
    }

    /// <summary>Reads the date an instant from the calendar's first day falls at, down to the second.</summary>
    internal GameDate FromAbsoluteMilliseconds(long milliseconds)
    {
        // The calendar's own timeline begins at its first day, so an instant before it is not a date this
        // calendar can read; dividing a negative instant would quietly produce one anyway.
        ArgumentOutOfRangeException.ThrowIfNegative(milliseconds);
        long days = milliseconds / DayMilliseconds;
        long withinDay = milliseconds % DayMilliseconds;
        int year = checked((int)(days / DaysPerYear)) + 1;
        int dayOfYear = (int)(days % DaysPerYear);
        int month = MonthOf(dayOfYear);
        int day = dayOfYear - _monthStartDays[month - 1] + 1;
        int hour = (int)(withinDay / (GameDuration.SecondsPerHour * GameDuration.MillisecondsPerSecond));
        int minute = (int)(withinDay / (GameDuration.SecondsPerMinute * GameDuration.MillisecondsPerSecond) % GameDuration.MinutesPerHour);
        int second = (int)(withinDay / GameDuration.MillisecondsPerSecond % GameDuration.SecondsPerMinute);
        return new GameDate(year, month, day, hour, minute, second);
    }

    /// <summary>Finds the month a day of the year falls in, counted from one.</summary>
    private int MonthOf(int dayOfYear)
    {
        int month = MonthsInYear;
        while (month > 1 && _monthStartDays[month - 1] > dayOfYear) month--;
        return month;
    }
}
