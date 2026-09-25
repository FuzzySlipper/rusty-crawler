namespace PartyRpg.Kit.Time;

/// <summary>How much game time one second of admitted engine time is worth.</summary>
/// <remarks>
/// <para>
/// A party-RPG day is far longer than a day of play, so the one clock runs faster than the admitted
/// update that drives it. That rate is tuning — a ruleset states it, the way it states a walk speed or a
/// rest cost — and it is deliberately not a constant here: a product that wants a minute of game time per
/// minute of play states one, and a product that wants time to race states thirty.
/// </para>
/// <para>
/// The rate lives on the clock rather than at each call site so that the admitted interval is converted
/// in exactly one place. A caller that did its own conversion would be the second place game time is
/// derived from real time, and the two would drift apart the moment one of them changed.
/// </para>
/// </remarks>
public readonly record struct GameTimeScale
{
    /// <summary>Creates a scale.</summary>
    /// <param name="gameSecondsPerRealSecond">How many game seconds one second of admitted engine time is worth.</param>
    /// <exception cref="ArgumentOutOfRangeException">The rate is not a finite, positive amount.</exception>
    public GameTimeScale(double gameSecondsPerRealSecond)
    {
        if (!double.IsFinite(gameSecondsPerRealSecond) || gameSecondsPerRealSecond <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(gameSecondsPerRealSecond),
                gameSecondsPerRealSecond,
                "Game time advances at a finite, positive rate, so a scale that is zero, negative, or unmeasurable would stop or reverse the clock.");
        }

        GameSecondsPerRealSecond = gameSecondsPerRealSecond;
    }

    /// <summary>Game time passes with the admitted clock, for a product that does not scale it.</summary>
    public static GameTimeScale RealTime => new(1);

    /// <summary>How many game seconds one second of admitted engine time is worth.</summary>
    public double GameSecondsPerRealSecond { get; }
}
