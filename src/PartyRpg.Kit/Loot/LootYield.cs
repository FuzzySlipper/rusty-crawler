using PartyRpg.Kit.Party;

namespace PartyRpg.Kit.Loot;

/// <summary>One thing a generation produced: a content definition, and how many of it.</summary>
/// <param name="Definition">The definition the party is handed.</param>
/// <param name="Count">How many of it, which is at least one.</param>
public readonly record struct LootItem(ItemDefinitionId Definition, int Count = 1)
{
    /// <inheritdoc />
    public override string ToString() => Count == 1 ? Definition.ToString() : $"{Count} × {Definition}";
}

/// <summary>What one generation produced: the things found, and the coin that came with them.</summary>
/// <remarks>
/// <para>
/// This is what a generation answers with, and it is the whole of what a container, a body, or any other
/// source of loot hands on. The count of a thing is a count of a definition rather than a minted instance:
/// the party's own acquisition path is what mints an instance's durable identity, and nothing here may
/// create one.
/// </para>
/// <para>
/// <b>Coin is a number and not a thing.</b> A purse is the party's own account, so what a generation found
/// is an amount to credit rather than an item to carry; whoever applies a yield settles it through the
/// party's own accounts.
/// </para>
/// </remarks>
/// <param name="Items">The things found, in the order they were drawn.</param>
/// <param name="Coins">How much coin came with them, which may be nothing.</param>
public sealed record LootYield(IReadOnlyList<LootItem> Items, int Coins)
{
    /// <summary>A generation that found nothing at all.</summary>
    public static LootYield Nothing { get; } = new([], 0);

    /// <summary>Whether nothing was found, which is a fact a report has to be able to state.</summary>
    public bool IsEmpty => Items.Count == 0 && Coins == 0;

    /// <summary>How many things were found altogether, counting the copies of each.</summary>
    public int Count
    {
        get
        {
            int total = 0;
            foreach (LootItem item in Items) total += item.Count;
            return total;
        }
    }

    /// <inheritdoc />
    public override string ToString()
    {
        if (IsEmpty) return "nothing";
        List<string> parts = [.. Items.Select(item => item.ToString())];
        if (Coins > 0) parts.Add($"{Coins} coin");
        return string.Join(", ", parts);
    }
}
