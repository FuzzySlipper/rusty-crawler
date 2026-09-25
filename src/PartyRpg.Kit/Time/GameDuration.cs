namespace PartyRpg.Kit.Time;

/// <summary>An amount of game time, exact to the game millisecond and never negative.</summary>
/// <remarks>
/// <para>
/// A duration is a count of milliseconds rather than a count of seconds because the one clock is advanced
/// by an admitted step of a fraction of a second, and a unit coarser than that would round away part of
/// every step. Milliseconds are the floor: finer units would be precision no schedule in this family can
/// state, and a duration that can name a length no owner can ask for is precision pretending to be a
/// guarantee. The unit is the same in every calendar — only a day's length and a week's length come from
/// the calendar, which is why those two units are resolved by <see cref="GameCalendar.Duration"/>.
/// </para>
/// <para>
/// A duration runs forward. Every amount here is a charge, an interval, or an elapsed span, so a negative
/// one is refused where it is built instead of being carried until some sum happens to come out right.
/// </para>
/// </remarks>
public readonly record struct GameDuration
{
    /// <summary>How many milliseconds one second of game time holds.</summary>
    public const long MillisecondsPerSecond = 1000;

    /// <summary>How many seconds one minute of game time holds.</summary>
    public const long SecondsPerMinute = 60;

    /// <summary>How many seconds one hour of game time holds.</summary>
    public const long SecondsPerHour = 3600;

    /// <summary>How many minutes one hour of game time holds, which is how the clock states a time of day.</summary>
    public const int MinutesPerHour = 60;

    private readonly long _milliseconds;

    private GameDuration(long milliseconds) => _milliseconds = milliseconds;

    /// <summary>No time at all, which is what an instant action costs and what a stopped clock reports.</summary>
    public static GameDuration None => default;

    /// <summary>Creates a duration from the clock's own resolution.</summary>
    /// <param name="milliseconds">How many milliseconds of game time the duration covers, which cannot be negative.</param>
    /// <returns>The duration.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The amount is negative, which is not a span of time.</exception>
    public static GameDuration FromMilliseconds(long milliseconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(milliseconds);
        return new GameDuration(milliseconds);
    }

    /// <summary>Creates a duration of whole seconds.</summary>
    /// <param name="seconds">How many seconds of game time the duration covers, which cannot be negative.</param>
    /// <returns>The duration.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The amount is negative, which is not a span of time.</exception>
    /// <exception cref="OverflowException">The amount is longer than the game time this kit can count.</exception>
    public static GameDuration FromSeconds(long seconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(seconds);
        return FromMilliseconds(checked(seconds * MillisecondsPerSecond));
    }

    /// <summary>Creates a duration of whole minutes.</summary>
    /// <param name="minutes">How many minutes of game time the duration covers, which cannot be negative.</param>
    /// <returns>The duration.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The amount is negative, which is not a span of time.</exception>
    /// <exception cref="OverflowException">The amount is longer than the game time this kit can count.</exception>
    public static GameDuration FromMinutes(long minutes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minutes);
        return FromSeconds(checked(minutes * SecondsPerMinute));
    }

    /// <summary>Creates a duration of whole hours.</summary>
    /// <param name="hours">How many hours of game time the duration covers, which cannot be negative.</param>
    /// <returns>The duration.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The amount is negative, which is not a span of time.</exception>
    /// <exception cref="OverflowException">The amount is longer than the game time this kit can count.</exception>
    public static GameDuration FromHours(long hours)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(hours);
        return FromSeconds(checked(hours * SecondsPerHour));
    }

    /// <summary>How many milliseconds of game time the duration covers, which is its exact value.</summary>
    public long Milliseconds => _milliseconds;

    /// <summary>How many seconds the duration covers, as a number a caller may divide further.</summary>
    public double TotalSeconds => _milliseconds / (double)MillisecondsPerSecond;

    /// <summary>Whether the duration covers no time at all.</summary>
    public bool IsNone => _milliseconds == 0;

    /// <summary>Adds two durations, which is how a chain of charges becomes one advance.</summary>
    /// <param name="left">The first duration.</param>
    /// <param name="right">The second duration.</param>
    /// <returns>The two durations together.</returns>
    /// <exception cref="OverflowException">The sum is longer than the game time this kit can count.</exception>
    public static GameDuration operator +(GameDuration left, GameDuration right) =>
        new(checked(left._milliseconds + right._milliseconds));
}
