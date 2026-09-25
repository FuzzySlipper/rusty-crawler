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
/// Only what lies here merges. An instance worn in a slot is a member's figure and keeps its own stack, so
/// equipping half a bundle is possible and the pack keeps the rest. Two instances merge only when they are
/// the same definition and the same state in every respect — a damaged item never disappears into a sound
/// one — and only as far as <see cref="IItemStackingRule"/> allows.
/// </para>
/// <para>
/// Mutation is internal on purpose: the pack holds the state, and the party owns the two rules (capacity
/// and stacking) that decide what entering it means.
/// </para>
/// </remarks>
public sealed class PartyInventory
{
    private readonly List<ItemInstance> _items = [];
    private readonly IInventoryCapacityRule? _capacity;
    private readonly IItemStackingRule? _stacking;

    internal PartyInventory(IInventoryCapacityRule? capacity, IItemStackingRule? stacking)
    {
        _capacity = capacity;
        _stacking = stacking;
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

    /// <summary>Asks the caller's capacity rule about taking items, or admits them when no rule was supplied.</summary>
    internal PartyRefusal? Judge(ItemDefinitionId definition, int count) =>
        _capacity?.Judge(_items, definition, count);

    /// <summary>How many of one definition may share an instance, never below one.</summary>
    internal int MaximumStack(ItemDefinitionId definition) => Math.Max(1, _stacking?.MaximumStack(definition) ?? 1);

    /// <summary>The loose stacks a merge may top up: same definition, same state in every respect.</summary>
    internal IReadOnlyList<ItemInstance> StacksMatching(ItemDefinitionId definition, ItemState state)
    {
        List<ItemInstance> matching = [];
        foreach (ItemInstance item in _items)
        {
            if (item.Definition == definition && item.State.Matches(state)) matching.Add(item);
        }

        return matching;
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
