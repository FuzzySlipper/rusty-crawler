namespace PartyRpg.Kit.Party;

/// <summary>
/// One item the party holds: a copy of a content definition, with a durable identity and its own state.
/// </summary>
/// <remarks>
/// <para>
/// An instance is not a definition. The definition is content's answer to "what kind of thing is this" and
/// never changes; the instance is the thing itself, and <see cref="Id"/> is what keeps one artifact
/// distinct from another copy of the same kind through a save. The state it carries — identified, damaged,
/// enchanted — belongs to the instance for the same reason.
/// </para>
/// <para>
/// An instance created outside the party starts <see cref="ItemCustody.Detached"/>: it lies on the ground,
/// in a container, or in a shop's stock, and no party holds it until one takes it. Once taken, the party's
/// shared pack or one member's slot holds it, and the custody recorded here says which.
/// </para>
/// </remarks>
public sealed class ItemInstance
{
    /// <summary>Creates an item instance.</summary>
    /// <param name="id">The instance's durable identity, which a save round-trips.</param>
    /// <param name="definition">The content definition this is a copy of.</param>
    /// <param name="stackCount">How many of the definition this instance carries, which must be at least one.</param>
    /// <param name="state">The instance's condition, or the unidentified, sound, unenchanted state when omitted.</param>
    /// <exception cref="ArgumentOutOfRangeException">The stack count is below one.</exception>
    public ItemInstance(
        ItemInstanceId id,
        ItemDefinitionId definition,
        int stackCount = 1,
        ItemState? state = null)
    {
        if (stackCount < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(stackCount),
                stackCount,
                "An instance carries at least one item; an empty instance is a thing that should not exist rather than an item in the pack.");
        }

        Id = id;
        Definition = definition;
        StackCount = stackCount;
        State = state ?? ItemState.Unidentified;
    }

    /// <summary>The instance's durable identity: what a save round-trips and what every reference names.</summary>
    public ItemInstanceId Id { get; }

    /// <summary>The content definition this instance is a copy of, which never changes.</summary>
    public ItemDefinitionId Definition { get; }

    /// <summary>How many of the definition this instance carries; at least one.</summary>
    public int StackCount { get; private set; }

    /// <summary>The instance's condition: identified, damaged, and enchanted.</summary>
    public ItemState State { get; private set; }

    /// <summary>Where the party holds this instance, or that the party does not hold it at all.</summary>
    public ItemCustody Custody { get; private set; } = ItemCustody.Detached;

    /// <summary>Whether a party holds this instance at all.</summary>
    public bool IsHeld => !Custody.IsDetached;

    /// <summary>Records that the party knows what this item is.</summary>
    public void Identify() => State = State.Identified();

    /// <summary>Damages the instance further. When damage breaks an item is the ruleset's policy, not this layer's.</summary>
    /// <param name="amount">How much damage to add, which cannot be negative.</param>
    /// <exception cref="ArgumentOutOfRangeException">The amount is negative.</exception>
    public void TakeDamage(int amount) => State = State.Damaged(amount);

    /// <summary>Repairs the instance, never past sound.</summary>
    /// <param name="amount">How much damage to repair, which cannot be negative.</param>
    /// <exception cref="ArgumentOutOfRangeException">The amount is negative.</exception>
    public void Repair(int amount) => State = State.Repaired(amount);

    /// <summary>
    /// Spends one of the instance's charges.
    /// </summary>
    /// <remarks>
    /// Only the party spends a charge, because only the party can take the item away when the last one goes:
    /// what is left is the game's own reading of the item's row less what the instance records as spent, and
    /// a second writer of that count would be a second answer to how full the item is.
    /// </remarks>
    internal void SpendCharge() => State = State.WithChargeSpent();

    /// <summary>Adds or replaces one enchantment on the instance.</summary>
    /// <param name="enchantment">The enchantment to carry.</param>
    public void Enchant(ItemEnchantment enchantment) => State = State.Enchanted(enchantment);

    /// <summary>Removes one enchantment from the instance.</summary>
    /// <param name="enchantment">The enchantment to remove.</param>
    public void Disenchant(EnchantmentId enchantment) => State = State.Disenchanted(enchantment);

    /// <summary>Records where the party holds the instance. Only the party moves items, so only it calls this.</summary>
    internal void Place(ItemCustody custody) => Custody = custody;

    /// <summary>Takes items out of this instance's stack, which a merge in the shared pack does.</summary>
    /// <param name="count">How many to take, which must leave at least one behind.</param>
    internal void RemoveFromStack(int count)
    {
        if (count < 1 || count >= StackCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(count),
                count,
                "Taking items from a stack has to leave a non-empty instance behind; an emptied stack is removed, not kept.");
        }

        StackCount -= count;
    }

    /// <summary>Adds items to this instance's stack, which a merge in the shared pack does.</summary>
    /// <param name="count">How many to add, which must be at least one.</param>
    internal void AddToStack(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        StackCount = checked(StackCount + count);
    }

    /// <inheritdoc />
    public override string ToString() => $"{Definition} x{StackCount} ({Id}) in {Custody}";
}
