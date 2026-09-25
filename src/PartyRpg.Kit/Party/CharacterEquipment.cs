using System.Diagnostics.CodeAnalysis;

namespace PartyRpg.Kit.Party;

/// <summary>
/// What one member wears and wields: slot to instance, and nothing else.
/// </summary>
/// <remarks>
/// <para>
/// This is the whole of a character's item state. There is no carried list, no pack, and no second
/// container that could quietly become one: a member owns what its figure holds, every other instance in
/// the party lies in the one shared pack, and the party is the only code that moves an instance between
/// the two. Slots are names the game declares, so a figure with more slots, or with two slots that mean
/// the same thing, needs content and rules, not a change here.
/// </para>
/// <para>
/// Mutation is internal on purpose: equip and unequip are party operations, because only the party can
/// judge whether a slot may be filled and where a displaced item goes.
/// </para>
/// </remarks>
public sealed class CharacterEquipment
{
    private readonly List<EquippedItem> _items = [];

    internal CharacterEquipment()
    {
    }

    /// <summary>The occupied slots, in the order the slots were first filled.</summary>
    public IReadOnlyList<EquippedItem> Items => _items;

    /// <summary>How many slots are occupied.</summary>
    public int Count => _items.Count;

    /// <summary>Whether a slot is occupied.</summary>
    /// <param name="slot">The slot to look at.</param>
    public bool Has(EquipmentSlot slot) => IndexOf(slot) >= 0;

    /// <summary>Reads the instance in a slot.</summary>
    /// <param name="slot">The slot to read.</param>
    /// <param name="item">The instance in that slot, when one is there.</param>
    /// <returns>Whether the slot is occupied.</returns>
    public bool TryGet(EquipmentSlot slot, [NotNullWhen(true)] out ItemInstance? item)
    {
        int index = IndexOf(slot);
        item = index >= 0 ? _items[index].Item : null;
        return index >= 0;
    }

    /// <summary>Reads the instance in a slot, or null when the slot is empty.</summary>
    /// <param name="slot">The slot to read.</param>
    public ItemInstance? ItemIn(EquipmentSlot slot)
    {
        int index = IndexOf(slot);
        return index >= 0 ? _items[index].Item : null;
    }

    /// <summary>Puts an instance in a slot, which must be empty because the party detaches what it displaces first.</summary>
    /// <param name="slot">The slot to fill.</param>
    /// <param name="item">The instance to wear.</param>
    /// <exception cref="InvalidOperationException">The slot already holds an instance, which would be a second item in one place.</exception>
    internal void Attach(EquipmentSlot slot, ItemInstance item)
    {
        if (IndexOf(slot) >= 0)
        {
            throw new InvalidOperationException(
                $"Slot '{slot}' already holds an item, so nothing was worn there; the party detaches what it displaces before it fills a slot.");
        }

        _items.Add(new EquippedItem(slot, item));
    }

    /// <summary>Takes whatever a slot holds out of it, or answers null when the slot is empty.</summary>
    /// <param name="slot">The slot to empty.</param>
    internal ItemInstance? Detach(EquipmentSlot slot)
    {
        int index = IndexOf(slot);
        if (index < 0) return null;
        ItemInstance item = _items[index].Item;
        _items.RemoveAt(index);
        return item;
    }

    private int IndexOf(EquipmentSlot slot)
    {
        for (int index = 0; index < _items.Count; index++)
        {
            if (_items[index].Slot == slot) return index;
        }

        return -1;
    }
}
