using System.Text.Json.Serialization;

namespace PartyRpg.Kit.Party;

/// <summary>
/// What is true of one item instance beyond its kind: whether it is identified, how damaged it is, which
/// enchantments it carries, how many charges of it have been spent, and how strong a consumable it is.
/// </summary>
/// <remarks>
/// <para>
/// This is mutable item state kept as an immutable value so a captured save cannot be changed by playing
/// on: every change produces a new state and the instance replaces the one it held. Identifying, taking
/// damage, repairing, enchanting, spending a charge, and coming out of a mixture at a strength are
/// therefore the instance's own business, while where the instance lies and how many share it belong to the
/// container that holds it.
/// </para>
/// <para>
/// <b>A charge is held by the item and recorded as what has been spent.</b> How many charges a kind of
/// thing holds when it is full is a reading of the game's own item table — the pack holds items, not item
/// definitions — so what the instance records is the uses it has paid for. What is left is the row's own
/// figure less this, which keeps an item found in a chest and one restored from a save the same arithmetic
/// and keeps the row the one place a wand's capacity is stated.
/// </para>
/// <para>
/// Value equality is element-wise — including the enchantments and the charges spent — because the shared
/// inventory merges stacks of things that are the same in every respect and must not merge a damaged item
/// into a sound one or a used wand into a fresh one. <see cref="Matches"/> is the same comparison under a
/// name that reads as what it decides.
/// </para>
/// </remarks>
public readonly struct ItemState : IEquatable<ItemState>
{
    private readonly ItemEnchantment[] _enchantments;

    /// <summary>Creates an item state.</summary>
    /// <param name="isIdentified">Whether the item's true nature is known to the party.</param>
    /// <param name="damage">How damaged the item is; zero is sound.</param>
    /// <param name="enchantments">The enchantments the instance carries, in the order they were added.</param>
    /// <param name="chargesSpent">How many charges of the item have been spent; zero when none has.</param>
    /// <param name="potency">
    /// How strong the instance is where its own kind of thing is read at a strength: a potion's own power,
    /// zero when nothing has stated one. It belongs to the instance rather than to the kind because two
    /// potions of one kind are the same drink at two strengths — the game mints them with a drawn strength
    /// and a mixture states its own — and because a save has to bring back the one the party is holding.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">The damage or the charges spent are negative, which is not a state an item can be in.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The potency is negative, which is not a strength anything can be read at.</exception>
    /// <remarks>
    /// A save reads this value back through this constructor: the metadata the product writes saves with
    /// binds a document's field to a constructor parameter, so a value created empty would silently lose it.
    /// </remarks>
    [JsonConstructor]
    public ItemState(
        bool isIdentified = false,
        int damage = 0,
        IReadOnlyList<ItemEnchantment>? enchantments = null,
        int chargesSpent = 0,
        int potency = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(damage);
        ArgumentOutOfRangeException.ThrowIfNegative(chargesSpent);
        ArgumentOutOfRangeException.ThrowIfNegative(potency);
        IsIdentified = isIdentified;
        Damage = damage;
        _enchantments = enchantments is null ? [] : [.. enchantments];
        ChargesSpent = chargesSpent;
        Potency = potency;
    }

    /// <summary>An item nobody has identified yet, sound, and carrying nothing.</summary>
    public static ItemState Unidentified => default;

    /// <summary>Whether the party knows what the item is.</summary>
    public bool IsIdentified { get; }

    /// <summary>How damaged the item is; zero is sound. What damage eventually breaks is the ruleset's policy.</summary>
    public int Damage { get; }

    /// <summary>The enchantments the instance carries, in the order they were added.</summary>
    public IReadOnlyList<ItemEnchantment> Enchantments => _enchantments ?? [];

    /// <summary>
    /// How many charges of the item have been spent: a wand's own count of the uses it has paid for.
    /// </summary>
    /// <remarks>
    /// What is left is this read against the game's own row for the item's kind, which is the one place a
    /// capacity is stated; an item whose kind holds no charges leaves this at zero whatever it does.
    /// </remarks>
    public int ChargesSpent { get; }

    /// <summary>
    /// How strong the instance is where its kind of thing is read at a strength: a potion's own power.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A strength belongs to the item rather than to the item's kind, and has to: a potion a shop drew and one
    /// a character mixed are the same drink at different strengths, and what the party carries is the one that
    /// was made. A game that states such a strength on its item rows keeps it on the instance for exactly that
    /// reason, and records it beside the instance in its own saved items.
    /// </para>
    /// <para>
    /// Nothing in the kit reads this. What a strength is worth to an effect, and which of a game's own items
    /// are read at one at all, are that game's answers.
    /// </para>
    /// </remarks>
    public int Potency { get; }

    /// <summary>The same state, read at the given strength.</summary>
    /// <param name="potency">The strength to record, which cannot be negative.</param>
    /// <exception cref="ArgumentOutOfRangeException">The strength is negative.</exception>
    public ItemState WithPotency(int potency)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(potency);
        return new ItemState(IsIdentified, Damage, Enchantments, ChargesSpent, potency);
    }

    /// <summary>The same state, identified.</summary>
    public ItemState Identified() => IsIdentified ? this : new ItemState(true, Damage, Enchantments, ChargesSpent, Potency);

    /// <summary>The same state, carrying the given damage rather than its own.</summary>
    /// <param name="damage">The damage to record, which cannot be negative.</param>
    /// <exception cref="ArgumentOutOfRangeException">The damage is negative.</exception>
    public ItemState WithDamage(int damage)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(damage);
        return new ItemState(IsIdentified, damage, Enchantments, ChargesSpent, Potency);
    }

    /// <summary>The same state, with one more charge spent.</summary>
    /// <exception cref="OverflowException">The count would leave the numbers a state is described in.</exception>
    public ItemState WithChargeSpent() => new(IsIdentified, Damage, Enchantments, checked(ChargesSpent + 1), Potency);

    /// <summary>
    /// The same state, with the given number of charges spent, which is how a recharge gives uses back.
    /// </summary>
    /// <param name="spent">How many charges are spent; zero is a full item.</param>
    /// <exception cref="ArgumentOutOfRangeException">The count is negative.</exception>
    public ItemState WithChargesSpent(int spent)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(spent);
        return new ItemState(IsIdentified, Damage, Enchantments, spent, Potency);
    }

    /// <summary>The same state, damaged further.</summary>
    /// <param name="amount">How much damage to add, which cannot be negative.</param>
    /// <exception cref="ArgumentOutOfRangeException">The amount is negative.</exception>
    /// <exception cref="OverflowException">The damage would leave the numbers a state is described in.</exception>
    public ItemState Damaged(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        return new ItemState(IsIdentified, checked(Damage + amount), Enchantments, ChargesSpent, Potency);
    }

    /// <summary>The same state, repaired by an amount and never past sound.</summary>
    /// <param name="amount">How much damage to repair, which cannot be negative.</param>
    /// <exception cref="ArgumentOutOfRangeException">The amount is negative.</exception>
    public ItemState Repaired(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        return new ItemState(IsIdentified, Math.Max(0, Damage - amount), Enchantments, ChargesSpent, Potency);
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
        return new ItemState(IsIdentified, Damage, enchantments, ChargesSpent, Potency);
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

        return new ItemState(IsIdentified, Damage, enchantments, ChargesSpent, Potency);
    }

    /// <summary>Whether two states are the same in every respect, which is what lets two stacks merge.</summary>
    /// <param name="other">The state to compare with.</param>
    public bool Matches(ItemState other)
    {
        // A strength is part of what an instance is, so two potions of one kind at two strengths are two
        // things: merging them would leave one of the two drinks with the other's strength.
        if (IsIdentified != other.IsIdentified || Damage != other.Damage || ChargesSpent != other.ChargesSpent || Potency != other.Potency) return false;

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
        hash.Add(ChargesSpent);
        hash.Add(Potency);
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
        $"{(IsIdentified ? "identified" : "unidentified")}, damage {Damage}, {Enchantments.Count} enchantment(s), {ChargesSpent} charge(s) spent, potency {Potency}";
}
