namespace PartyRpg.Kit.Combat;

/// <summary>What a target resists of one kind of harm, as the ruleset read it.</summary>
/// <remarks>
/// <para>
/// Two states and not a number, because the shipped bestiary's own encoding has two: a resistance is a value
/// that makes harm less likely to land whole, and full immunity is a value that makes it land not at all.
/// The kit carries the reading so that an immune target takes nothing whatever the ruleset's arithmetic is,
/// and a resistant one can be measured against the same attack on a target that resists nothing.
/// </para>
/// <para>
/// <b>How a value becomes less harm is the ruleset's.</b> A game may halve per successful check, subtract a
/// percentage, or cap the damage; the kit hands the reading back to <see cref="ICombatResolutionRule"/> with
/// the damage and lets the rule decide, which is why this type states no arithmetic of its own.
/// </para>
/// </remarks>
public readonly record struct Resistance
{
    private Resistance(bool isImmune, int points)
    {
        IsImmune = isImmune;
        Points = points;
    }

    /// <summary>Full immunity: harm of this kind lands for nothing at all.</summary>
    public static Resistance Immune { get; } = new(isImmune: true, points: 0);

    /// <summary>Resistance of a stated weight, which the ruleset turns into less harm.</summary>
    /// <param name="points">The resistance as the game states it; zero is no resistance at all.</param>
    /// <returns>The reading.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The weight is negative, which no resistance table states.</exception>
    public static Resistance Of(int points)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(points);
        return new Resistance(isImmune: false, points);
    }

    /// <summary>Whether this target takes none of this kind of harm.</summary>
    public bool IsImmune { get; }

    /// <summary>How much resistance the target has, zero when it is immune or resists nothing.</summary>
    public int Points { get; }

    /// <inheritdoc />
    public override string ToString() => IsImmune
        ? "immune"
        : Points.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
