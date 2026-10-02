namespace PartyRpg.Kit.Party;

/// <summary>The instance removed from the party, or the named reason it stayed in its custody.</summary>
public sealed record ItemRemoval
{
    private ItemRemoval(ItemInstance? item, Refusal? refusal) { Item = item; Refusal = refusal; }

    /// <summary>The detached instance, or null when nothing left.</summary>
    public ItemInstance? Item { get; }

    /// <summary>Why nothing left, or null when the instance was removed.</summary>
    public Refusal? Refusal { get; }

    /// <summary>Whether the instance left the party.</summary>
    public bool Removed => Refusal is null;

    /// <summary>An instance that left the party.</summary>
    public static ItemRemoval Taken(ItemInstance item) => new(item ?? throw new ArgumentNullException(nameof(item)), null);

    /// <summary>An instance that stayed where it was.</summary>
    public static ItemRemoval Refused(Refusal refusal) => new(null, refusal ?? throw new ArgumentNullException(nameof(refusal)));
}
