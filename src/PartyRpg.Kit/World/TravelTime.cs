namespace PartyRpg.Kit.World;

/// <summary>How much game time a transition takes, and the unit the amount is stated in.</summary>
/// <remarks>
/// A travel time is a charge and never a credit: a negative amount would run the one shared clock
/// backwards for every schedule, duration, and respawn watching it, so it is refused where it is built
/// rather than compensated for by whoever applies it.
/// </remarks>
public readonly record struct TravelTime
{
    /// <summary>Creates an elapsed-time charge.</summary>
    /// <param name="amount">The amount of game time the transition takes, which cannot be negative.</param>
    /// <param name="unit">The unit the amount is stated in.</param>
    /// <exception cref="ArgumentOutOfRangeException">The amount is negative.</exception>
    public TravelTime(long amount, TravelTimeUnit unit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        Amount = amount;
        Unit = unit;
    }

    /// <summary>The amount of game time.</summary>
    public long Amount { get; }

    /// <summary>The unit the amount is stated in.</summary>
    public TravelTimeUnit Unit { get; }

    /// <summary>Whether this charge takes no time at all.</summary>
    public bool IsNone => Amount == 0;

    /// <summary>No elapsed time, for a transition that is instant.</summary>
    public static TravelTime None => new(0, TravelTimeUnit.Minutes);
}
