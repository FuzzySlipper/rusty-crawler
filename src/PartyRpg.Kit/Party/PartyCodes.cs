namespace PartyRpg.Kit.Party;

/// <summary>The codes the party refuses a change to its own things and accounts with.</summary>
/// <remarks>
/// A code is what a caller and a test branch on and the message beside it is what a person reads, so every
/// refusal this mechanism can give is named here once rather than spelled at the point of use.
/// </remarks>
public static class PartyCodes
{
    /// <summary>The refusal code <c>item-already-held</c>.</summary>
    public const string ItemAlreadyHeld = "item-already-held";

    /// <summary>The refusal code <c>item-no-charges</c>.</summary>
    public const string ItemNoCharges = "item-no-charges";

    /// <summary>The refusal code <c>item-not-held</c>.</summary>
    public const string ItemNotHeld = "item-not-held";

    /// <summary>The refusal code <c>larder-short</c>.</summary>
    public const string LarderShort = "larder-short";

    /// <summary>The refusal code <c>purse-and-larder-short</c>.</summary>
    public const string PurseAndLarderShort = "purse-and-larder-short";

    /// <summary>The refusal code <c>purse-short</c>.</summary>
    public const string PurseShort = "purse-short";

    /// <summary>The refusal code <c>slot-empty</c>.</summary>
    public const string SlotEmpty = "slot-empty";
}
