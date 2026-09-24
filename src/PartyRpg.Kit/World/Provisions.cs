namespace PartyRpg.Kit.World;

/// <summary>How much food a transition spends, and the unit the amount is stated in.</summary>
/// <remarks>
/// Like time, provisions are a charge and never a credit: a party does not gain food by travelling, and
/// a negative amount would make a food store grow on every step. Refusing it here keeps that arithmetic
/// out of the owner that holds the store.
/// </remarks>
public readonly record struct Provisions
{
    /// <summary>Creates a provision charge.</summary>
    /// <param name="amount">The number of units the transition spends, which cannot be negative.</param>
    /// <param name="unit">The unit the amount is stated in.</param>
    /// <exception cref="ArgumentOutOfRangeException">The amount is negative.</exception>
    public Provisions(int amount, ProvisionUnit unit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        Amount = amount;
        Unit = unit;
    }

    /// <summary>The number of units.</summary>
    public int Amount { get; }

    /// <summary>The unit the amount is stated in.</summary>
    public ProvisionUnit Unit { get; }

    /// <summary>Whether this charge spends no food at all.</summary>
    public bool IsNone => Amount == 0;

    /// <summary>No food, for a transition that is not provisioned.</summary>
    public static Provisions None => new(0, ProvisionUnit.Portions);
}
