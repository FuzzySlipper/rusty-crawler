namespace PartyRpg.Kit.Party;

/// <summary>One character's pool of a resource that is spent and restored: its current and maximum values.</summary>
/// <remarks>
/// What the pool is worth — how much a hit takes, what healing restores, what happens at zero — is ruleset
/// policy; the pool is the number. Current never exceeds maximum and never falls below zero, so a caller
/// asking "can this be spent" and a caller reading the result cannot disagree about the state in between.
/// </remarks>
public readonly record struct ResourcePool
{
    /// <summary>Creates a resource pool.</summary>
    /// <param name="current">How much is available now.</param>
    /// <param name="maximum">How much the pool holds when full.</param>
    /// <exception cref="ArgumentOutOfRangeException">The maximum is negative, or the current value is outside the pool.</exception>
    public ResourcePool(int current, int maximum)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximum);
        if (current < 0 || current > maximum)
        {
            throw new ArgumentOutOfRangeException(
                nameof(current),
                current,
                $"A pool of at most {maximum} cannot hold {current}; what is available lies between zero and the maximum.");
        }

        Current = current;
        Maximum = maximum;
    }

    /// <summary>A pool with nothing in it and no capacity.</summary>
    public static ResourcePool Empty => default;

    /// <summary>A pool filled to a capacity.</summary>
    /// <param name="maximum">How much the pool holds when full, which cannot be negative.</param>
    /// <exception cref="ArgumentOutOfRangeException">The maximum is negative.</exception>
    public static ResourcePool Full(int maximum) => new(maximum, maximum);

    /// <summary>How much is available now.</summary>
    public int Current { get; }

    /// <summary>How much the pool holds when full.</summary>
    public int Maximum { get; }

    /// <summary>Whether the pool is empty.</summary>
    public bool IsEmpty => Current == 0;

    /// <summary>Whether the pool is full.</summary>
    public bool IsFull => Current == Maximum;

    /// <summary>The same pool, spent by an amount and never below empty.</summary>
    /// <param name="amount">How much to take, which cannot be negative.</param>
    /// <exception cref="ArgumentOutOfRangeException">The amount is negative, which would restore rather than spend.</exception>
    public ResourcePool Spent(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        return new ResourcePool((int)Math.Max(0, Current - (long)amount), Maximum);
    }

    /// <summary>The same pool, restored by an amount and never past full.</summary>
    /// <param name="amount">How much to restore, which cannot be negative.</param>
    /// <exception cref="ArgumentOutOfRangeException">The amount is negative, which would spend rather than restore.</exception>
    public ResourcePool Restored(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        return new ResourcePool((int)Math.Min(Maximum, Current + (long)amount), Maximum);
    }

    /// <summary>The same pool with a new capacity, keeping what is available inside it.</summary>
    /// <param name="maximum">The new capacity, which cannot be negative.</param>
    /// <exception cref="ArgumentOutOfRangeException">The maximum is negative.</exception>
    public ResourcePool WithMaximum(int maximum)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximum);
        return new ResourcePool(Math.Min(Current, maximum), maximum);
    }

    /// <summary>The same pool, filled to its capacity.</summary>
    public ResourcePool Filled() => new(Maximum, Maximum);

    /// <inheritdoc />
    public override string ToString() =>
        $"{Current}/{Maximum}";
}
