namespace PartyRpg.Kit.Party;

/// <summary>
/// What one equipment change did: what the slot now holds, what went back to the shared pack, or why
/// nothing changed.
/// </summary>
/// <remarks>
/// Equipping into a filled slot displaces what was there, and unequipping empties one, so the same answer
/// covers both: <see cref="Equipped"/> is what the slot holds afterwards, and <see cref="Displaced"/> is
/// the instance that moved from the slot back into the shared pack. A refusal leaves the party exactly as
/// it was — including the case where the pack has no room for what would be displaced, which would
/// otherwise lose an item in the middle of a swap.
/// </remarks>
public sealed record EquipmentChange
{
    private EquipmentChange(ItemInstance? equipped, ItemInstance? displaced, PartyRefusal? refusal)
    {
        Equipped = equipped;
        Displaced = displaced;
        Refusal = refusal;
    }

    /// <summary>The change happened.</summary>
    /// <param name="equipped">What the slot holds afterwards, or null when it was emptied.</param>
    /// <param name="displaced">What left the slot for the shared pack, or null when nothing did.</param>
    public static EquipmentChange Changed(ItemInstance? equipped, ItemInstance? displaced) =>
        new(equipped, displaced, null);

    /// <summary>Nothing changed.</summary>
    /// <param name="refusal">Why the change was refused.</param>
    /// <exception cref="ArgumentNullException">The refusal is null.</exception>
    public static EquipmentChange Refused(PartyRefusal refusal) =>
        new(null, null, refusal ?? throw new ArgumentNullException(nameof(refusal)));

    /// <summary>The refusal, or null when the change happened.</summary>
    public PartyRefusal? Refusal { get; }

    /// <summary>What the slot holds afterwards, or null when it is empty or the change was refused.</summary>
    public ItemInstance? Equipped { get; }

    /// <summary>What left the slot for the shared pack, or null when nothing did.</summary>
    public ItemInstance? Displaced { get; }

    /// <summary>Whether the change happened.</summary>
    public bool Admitted => Refusal is null;

    /// <inheritdoc />
    public override string ToString() => Refusal is null
        ? $"equipped {Equipped?.ToString() ?? "nothing"}, displaced {Displaced?.ToString() ?? "nothing"}"
        : $"refused: {Refusal}";
}
