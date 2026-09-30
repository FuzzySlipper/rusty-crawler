namespace PartyRpg.Kit.Party;

/// <summary>
/// The party's one shared pack: every item instance the party holds loose, and nothing a member wears.
/// </summary>
/// <remarks>
/// <para>
/// This is the deliberate divergence the product was designed around. The original gives every character a
/// pack and turns loot into a shuffle between four of them; here there is one container, so a character's
/// item state is its equipped figure and nothing else. Nothing in this type can attach an item to a person:
/// instances leave for a slot through the party, and the party is the only code that moves them.
/// </para>
/// <para>
/// Mutation is internal on purpose: the pack holds the state, and the party is the only code that decides what
/// entering it means.
/// </para>
/// </remarks>
public sealed class PartyInventory
{
    private readonly List<ItemInstance> _items = [];
    internal PartyInventory()
    {
    }

    /// <summary>The loose instances, in the order the party took them.</summary>
    public IReadOnlyList<ItemInstance> Items => _items;

    /// <summary>How many loose instances the pack holds.</summary>
    public int Count => _items.Count;

    /// <summary>Whether one instance lies in the pack.</summary>
    /// <param name="id">The instance's durable identity.</param>
    public bool Contains(ItemInstanceId id) => Find(id) is not null;

    /// <summary>Finds one loose instance by its durable identity, or null when the pack does not hold it.</summary>
    /// <param name="id">The instance's durable identity.</param>
    public ItemInstance? Find(ItemInstanceId id)
    {
        foreach (ItemInstance item in _items)
        {
            if (item.Id == id) return item;
        }

        return null;
    }

    /// <summary>Finds the first loose instance that is a copy of a definition, or null when none is.</summary>
    /// <param name="definition">The definition to look for.</param>
    public ItemInstance? Find(ItemDefinitionId definition)
    {
        foreach (ItemInstance item in _items)
        {
            if (item.Definition == definition) return item;
        }

        return null;
    }

    /// <summary>How many items of one definition the pack holds, counting every stack.</summary>
    /// <param name="definition">The definition to count.</param>
    public int TotalOf(ItemDefinitionId definition)
    {
        int total = 0;
        foreach (ItemInstance item in _items)
        {
            if (item.Definition == definition) total = checked(total + item.StackCount);
        }

        return total;
    }

    /// <summary>Puts an instance in the pack, which only the party does after its rules have admitted it.</summary>
    internal void Append(ItemInstance item)
    {
        _items.Add(item);
        item.Place(ItemCustody.InSharedInventory);
    }

    /// <summary>Takes an instance out of the pack, or answers null when the pack does not hold it.</summary>
    internal ItemInstance? Remove(ItemInstanceId id)
    {
        for (int index = 0; index < _items.Count; index++)
        {
            if (_items[index].Id != id) continue;
            ItemInstance item = _items[index];
            _items.RemoveAt(index);
            return item;
        }

        return null;
    }

    /// <summary>Removes an instance that has become empty, which a merge leaves behind.</summary>
    internal void Remove(ItemInstance item) => _items.Remove(item);
}
