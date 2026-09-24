namespace PartyRpg.Kit.Movement;

/// <summary>
/// What a fall costs the party beyond a height the tuning allows.
/// </summary>
/// <remarks>
/// The engine reports how far the party actually fell and leaves the consequence to the product, so this
/// is policy rather than physics: a threshold under which a drop is free, and a rate above it. Both are
/// stated in the units the world is measured in — the threshold in the engine's own length unit, the
/// rate in damage per unit of fall — so a tuning profile can be read without knowing which world it
/// tunes. Nothing here applies damage; it states it, and whoever owns the party's health applies it.
/// </remarks>
public readonly record struct FallPolicy
{
    /// <summary>Creates a fall policy.</summary>
    /// <param name="threshold">The greatest drop, in the engine's length unit, that costs nothing.</param>
    /// <param name="damagePerUnit">
    /// Damage for each unit of drop beyond the threshold. Damage is in whatever unit the party's health
    /// is measured in, which is the ruleset's business and not the kit's.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">A value is not a number or is negative.</exception>
    public FallPolicy(double threshold, double damagePerUnit)
    {
        if (!double.IsFinite(threshold) || threshold < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(threshold),
                threshold,
                "A fall threshold must be a finite, non-negative length; a party cannot fall less than nothing.");
        }

        if (!double.IsFinite(damagePerUnit) || damagePerUnit < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(damagePerUnit),
                damagePerUnit,
                "Fall damage must be a finite, non-negative rate; a fall cannot mend a party.");
        }

        Threshold = threshold;
        DamagePerUnit = damagePerUnit;
    }

    /// <summary>The greatest drop that costs nothing, in the engine's length unit.</summary>
    public double Threshold { get; }

    /// <summary>Damage for each unit of drop beyond the threshold.</summary>
    public double DamagePerUnit { get; }

    /// <summary>A policy under which no drop is ever too great, for a world that has not tuned falls yet.</summary>
    /// <remarks>
    /// The threshold is the largest length a drop can have rather than an unbounded value, so the policy
    /// still answers every fall with a number and no caller has to handle an infinity.
    /// </remarks>
    public static FallPolicy Free { get; } = new(double.MaxValue, 0);

    /// <summary>The consequence of a fall of a given length.</summary>
    /// <param name="fallDistance">How far the party fell, in the engine's length unit.</param>
    /// <returns>The fall, with no damage when it is no deeper than the threshold.</returns>
    public FallOutcome Consequence(double fallDistance)
    {
        if (!double.IsFinite(fallDistance) || fallDistance <= 0) return FallOutcome.None;
        double excess = fallDistance - Threshold;
        return excess <= 0
            ? new FallOutcome(fallDistance, 0, 0)
            : new FallOutcome(fallDistance, excess, excess * DamagePerUnit);
    }
}

/// <summary>
/// What one landing cost the party, as the party's owners need to hear it.
/// </summary>
/// <remarks>
/// A fall is reported rather than applied: the party's health belongs to whoever owns it, and the kit
/// states the number that owner needs instead of reaching into state it does not hold.
/// </remarks>
/// <param name="Distance">How far the party fell, in the engine's length unit. Zero when it did not fall.</param>
/// <param name="Excess">How much of the fall went past the threshold, in the same unit.</param>
/// <param name="Damage">What the fall costs, in the unit the party's health is measured in.</param>
public readonly record struct FallOutcome(double Distance, double Excess, double Damage)
{
    /// <summary>A landing that cost nothing, because the party did not fall or fell within the threshold.</summary>
    public static FallOutcome None => default;

    /// <summary>Whether the fall went past the threshold, whether or not the tuning charges anything for it.</summary>
    public bool PastThreshold => Excess > 0;

    /// <summary>Whether the fall cost the party anything at all.</summary>
    public bool Costs => Damage > 0;
}
