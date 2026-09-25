namespace PartyRpg.Kit.Party;

/// <summary>One item instance a save records, with the custody that says where the party held it.</summary>
/// <remarks>
/// An instance is written once, and its custody is what the restored party places it by: an instance in the
/// shared pack goes back to the pack, and an equipped one goes back to the member and slot named here. That
/// is why no member record carries equipment of its own — there is one place an item's whereabouts is
/// written, so a save cannot record an item in two places or in none.
/// </remarks>
public sealed record ItemSave
{
    /// <summary>Records one item instance.</summary>
    /// <param name="id">The instance's durable identity.</param>
    /// <param name="definition">The content definition the instance is a copy of.</param>
    /// <param name="stackCount">How many of the definition the instance carried; at least one.</param>
    /// <param name="state">The instance's condition: identified, damaged, and enchanted.</param>
    /// <param name="custody">Where the party held the instance.</param>
    /// <exception cref="ArgumentOutOfRangeException">The stack count is below one, which is not an instance a party can hold.</exception>
    public ItemSave(
        ItemInstanceId id,
        ItemDefinitionId definition,
        int stackCount,
        ItemState state,
        ItemCustody custody)
    {
        if (stackCount < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(stackCount),
                stackCount,
                "A save records instances that carry at least one item; an empty instance is not something a party held.");
        }

        Id = id;
        Definition = definition;
        StackCount = stackCount;
        State = state;
        Custody = custody;
    }

    /// <summary>The instance's durable identity.</summary>
    public ItemInstanceId Id { get; }

    /// <summary>The content definition the instance is a copy of.</summary>
    public ItemDefinitionId Definition { get; }

    /// <summary>How many of the definition the instance carried.</summary>
    public int StackCount { get; }

    /// <summary>The instance's condition: identified, damaged, and enchanted.</summary>
    public ItemState State { get; }

    /// <summary>Where the party held the instance.</summary>
    public ItemCustody Custody { get; }
}
