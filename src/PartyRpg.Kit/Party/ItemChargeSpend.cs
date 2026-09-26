namespace PartyRpg.Kit.Party;

/// <summary>What spending one of an item's charges did: what is left, whether the item went with it, or why nothing was spent.</summary>
/// <remarks>
/// <para>
/// A charge is a use the item pays for, so the answer names the three states a caller acts on: the item
/// still holds charges and how many, the item spent its last one and has left the party, or nothing was
/// spent because the item was not held or holds none. The count is the pack's own reading — the game's
/// capacity for the item's kind less what the instance records — so a caller never keeps a second count.
/// </para>
/// <para>
/// <b>An item that empties leaves through the party's own custody.</b> <see cref="Emptied"/> means the
/// instance was detached by the same route a release takes, so a discharged wand does not sit in a member's
/// figure waiting for somebody to notice; a caller that receives it holds an instance no party owns.
/// </para>
/// </remarks>
public sealed record ItemChargeSpend
{
    private ItemChargeSpend(int left, bool emptied, ItemInstance? item, PartyRefusal? refusal)
    {
        ChargesLeft = left;
        Vanished = emptied;
        Item = item;
        Refusal = refusal;
    }

    /// <summary>A charge was spent and the item still holds some.</summary>
    /// <param name="left">How many charges the item holds now, which is at least one.</param>
    /// <param name="item">The instance the charge was spent from.</param>
    /// <returns>The spend.</returns>
    /// <exception cref="ArgumentNullException">No instance was supplied.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The count left is not positive.</exception>
    public static ItemChargeSpend Held(int left, ItemInstance item)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentOutOfRangeException.ThrowIfLessThan(left, 1);
        return new ItemChargeSpend(left, emptied: false, item, refusal: null);
    }

    /// <summary>A charge was spent and that was the last: the item has left the party.</summary>
    /// <param name="item">The instance that emptied, now held by nobody.</param>
    /// <returns>The spend.</returns>
    /// <exception cref="ArgumentNullException">No instance was supplied.</exception>
    public static ItemChargeSpend Emptied(ItemInstance item) =>
        new(0, emptied: true, item ?? throw new ArgumentNullException(nameof(item)), refusal: null);

    /// <summary>Nothing was spent.</summary>
    /// <param name="refusal">Why nothing was spent.</param>
    /// <returns>The spend.</returns>
    /// <exception cref="ArgumentNullException">No refusal was supplied.</exception>
    public static ItemChargeSpend Refused(PartyRefusal refusal) =>
        new(0, emptied: false, item: null, refusal ?? throw new ArgumentNullException(nameof(refusal)));

    /// <summary>Whether a charge was spent.</summary>
    public bool Spent => Refusal is null;

    /// <summary>How many charges the item holds afterwards; zero when it emptied or nothing was spent.</summary>
    public int ChargesLeft { get; }

    /// <summary>Whether the item spent its last charge and left the party with this spend.</summary>
    public bool Vanished { get; }

    /// <summary>The instance the charge was spent from, or null when nothing was spent.</summary>
    public ItemInstance? Item { get; }

    /// <summary>The refusal, or null when a charge was spent.</summary>
    public PartyRefusal? Refusal { get; }

    /// <inheritdoc />
    public override string ToString() => Refusal is not null
        ? $"refused: {Refusal}"
        : Vanished
            ? $"spent the last charge of {Item}"
            : $"{ChargesLeft} charge(s) left in {Item}";
}
