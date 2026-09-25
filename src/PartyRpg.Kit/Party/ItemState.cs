namespace PartyRpg.Kit.Party;

/// <summary>
/// What is true of one item instance beyond its kind: whether it is identified, how damaged it is, and
/// which enchantments it carries.
/// </summary>
/// <remarks>
/// <para>
/// This is mutable item state kept as an immutable value so a captured save cannot be changed by playing
/// on: every change produces a new state and the instance replaces the one it held. Identifying, taking
/// damage, repairing, and enchanting are therefore the instance's own business, while where the instance
/// lies and how many share it belong to the container that holds it.
/// </para>
/// <para>
/// Value equality is element-wise — including the enchantments — because the shared inventory merges
/// stacks of things that are the same in every respect and must not merge a damaged item into a sound one.
/// <see cref="Matches"/> is the same comparison under a name that reads as what it decides.
/// </para>
/// </remarks>
public readonly struct ItemState : IEquatable<ItemState>
{
    private readonly ItemEnchantment[] _enchantments;

    /// <summary>Creates an item state.</summary>
    /// <param name="isIdentified">Whether the item's true nature is known to the party.</param>
    /// <param name="damage">How damaged the item is; zero is sound.</param>
    /// <param name="enchantments">The enchantments the instance carries, in the order they were added.</param>
    /// <exception cref="ArgumentOutOfRangeException">The damage is negative, which is not a state an item can be in.</exception>
    public ItemState(bool isIdentified = false, int damage = 0, IReadOnlyList<ItemEnchantment>? enchantments = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(damage);
        IsIdentified = isIdentified;
        Damage = damage;
        _enchantments = enchantments is null ? [] : [.. enchantments];
    }

    /// <summary>An item nobody has identified yet, sound, and carrying nothing.</summary>
    public static ItemState Unidentified => default;

    /// <summary>Whether the party knows what the item is.</summary>
    public bool IsIdentified { get; }

    /// <summary>How damaged the item is; zero is sound. What damage eventually breaks is the ruleset's policy.</summary>
    public int Damage { get; }

    /// <summary>The enchantments the instance carries, in the order they were added.</summary>
    public IReadOnlyList<ItemEnchantment> Enchantments => _enchantments ?? [];

    /// <summary>The same state, identified.</summary>
    public ItemState Identified() => IsIdentified ? this : new ItemState(true, Damage, Enchantments);

    /// <summary>The same state, carrying the given damage rather than its own.</summary>
    /// <param name="damage">The damage to record, which cannot be negative.</param>
    /// <exception cref="ArgumentOutOfRangeException">The damage is negative.</exception>
    public ItemState WithDamage(int damage)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(damage);
        return new ItemState(IsIdentified, damage, Enchantments);
    }

    /// <summary>The same state, damaged further.</summary>
    /// <param name="amount">How much damage to add, which cannot be negative.</param>
    /// <exception cref="ArgumentOutOfRangeException">The amount is negative.</exception>
    /// <exception cref="OverflowException">The damage would leave the numbers a state is described in.</exception>
    public ItemState Damaged(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        return new ItemState(IsIdentified, checked(Damage + amount), Enchantments);
    }

    /// <summary>The same state, repaired by an amount and never past sound.</summary>
    /// <param name="amount">How much damage to repair, which cannot be negative.</param>
    /// <exception cref="ArgumentOutOfRangeException">The amount is negative.</exception>
    public ItemState Repaired(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        return new ItemState(IsIdentified, Math.Max(0, Damage - amount), Enchantments);
    }

    /// <summary>
    /// The same state, carrying the given enchantment. An enchantment already present is replaced rather
    /// than duplicated, because two magnitudes of one enchantment on one item have no meaning a rule could
    /// read without the kit inventing a stacking rule for them.
    /// </summary>
    /// <param name="enchantment">The enchantment to carry.</param>
    public ItemState Enchanted(ItemEnchantment enchantment)
    {
        List<ItemEnchantment> enchantments = [];
        bool replaced = false;
        foreach (ItemEnchantment existing in Enchantments)
        {
            if (existing.Enchantment == enchantment.Enchantment)
            {
                enchantments.Add(enchantment);
                replaced = true;
                continue;
            }

            enchantments.Add(existing);
        }

        if (!replaced) enchantments.Add(enchantment);
        return new ItemState(IsIdentified, Damage, enchantments);
    }

    /// <summary>The same state, without the given enchantment.</summary>
    /// <param name="enchantment">The enchantment to remove.</param>
    public ItemState Disenchanted(EnchantmentId enchantment)
    {
        List<ItemEnchantment> enchantments = [];
        foreach (ItemEnchantment existing in Enchantments)
        {
            if (existing.Enchantment != enchantment) enchantments.Add(existing);
        }

        return new ItemState(IsIdentified, Damage, enchantments);
    }

    /// <summary>Whether two states are the same in every respect, which is what lets two stacks merge.</summary>
    /// <param name="other">The state to compare with.</param>
    public bool Matches(ItemState other)
    {
        if (IsIdentified != other.IsIdentified || Damage != other.Damage) return false;

        IReadOnlyList<ItemEnchantment> mine = Enchantments;
        IReadOnlyList<ItemEnchantment> theirs = other.Enchantments;
        if (mine.Count != theirs.Count) return false;
        for (int index = 0; index < mine.Count; index++)
        {
            if (!mine[index].Equals(theirs[index])) return false;
        }

        return true;
    }

    /// <inheritdoc />
    public bool Equals(ItemState other) => Matches(other);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ItemState other && Matches(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        HashCode hash = new();
        hash.Add(IsIdentified);
        hash.Add(Damage);
        foreach (ItemEnchantment enchantment in Enchantments) hash.Add(enchantment);
        return hash.ToHashCode();
    }

    /// <summary>Whether two states are the same in every respect.</summary>
    /// <param name="left">One state.</param>
    /// <param name="right">The other state.</param>
    public static bool operator ==(ItemState left, ItemState right) => left.Matches(right);

    /// <summary>Whether two states differ in any respect.</summary>
    /// <param name="left">One state.</param>
    /// <param name="right">The other state.</param>
    public static bool operator !=(ItemState left, ItemState right) => !left.Matches(right);

    /// <inheritdoc />
    public override string ToString() =>
        $"{(IsIdentified ? "identified" : "unidentified")}, damage {Damage}, {Enchantments.Count} enchantment(s)";
}
