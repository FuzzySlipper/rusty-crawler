namespace PartyRpg.Kit.Party;

/// <summary>What one pickup did: where the items landed, how many were taken, or why nothing was taken.</summary>
/// <remarks>
/// A pickup can merge into a stack that was already in the pack, start one, or be refused by the capacity
/// rule, so the answer names the instance the items landed in rather than assuming a new one exists. The
/// count is carried because a stack the pack already held may have taken only part of what was offered.
/// </remarks>
public sealed record ItemAcquisition
{
    private ItemAcquisition(ItemInstance? item, int count, PartyRefusal? refusal)
    {
        Item = item;
        Count = count;
        Refusal = refusal;
    }

    /// <summary>The party took the items.</summary>
    /// <param name="item">The instance the items landed in — the stack they merged into, or the new one they started.</param>
    /// <param name="count">How many items were taken.</param>
    public static ItemAcquisition Taken(ItemInstance item, int count)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new ItemAcquisition(item, count, null);
    }

    /// <summary>The party took nothing.</summary>
    /// <param name="refusal">Why the items were refused.</param>
    /// <exception cref="ArgumentNullException">The refusal is null.</exception>
    public static ItemAcquisition Refused(PartyRefusal refusal) =>
        new(null, 0, refusal ?? throw new ArgumentNullException(nameof(refusal)));

    /// <summary>The refusal, or null when the party took the items.</summary>
    public PartyRefusal? Refusal { get; }

    /// <summary>The instance the items landed in, which exists only when they were taken.</summary>
    public ItemInstance? Item { get; }

    /// <summary>How many items were taken; zero when they were refused.</summary>
    public int Count { get; }

    /// <summary>Whether the party took the items.</summary>
    public bool Admitted => Refusal is null;

    /// <inheritdoc />
    public override string ToString() => Refusal is null ? $"took {Count} into {Item}" : $"refused: {Refusal}";
}
