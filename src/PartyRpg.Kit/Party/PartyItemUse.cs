namespace PartyRpg.Kit.Party;

/// <summary>A game's non-spell uses of actual carried items.</summary>
public interface IItemUseRule
{
    /// <summary>Names the supported ordinary action, or null for an item without one.</summary>
    string? ActionOf(ItemInstance item);
    /// <summary>Describes the item's compiled powers; this is presentation, never a second modifier store.</summary>
    string Describe(ItemInstance item);
    /// <summary>Reads the member's current permanent gifts and worn contributions for the panel.</summary>
    string Describe(PartyMember member);
    /// <summary>Applies the named use through the canonical party owners, or returns a refusal.</summary>
    ItemUseResult Use(PartyEntity party, PartyMember member, ItemInstance item);
}

/// <summary>The answer an ordinary item use left for the panel.</summary>
public sealed record ItemUseResult(bool Applied, string Code, string Message)
{
    /// <summary>A refusal in the mechanism's one vocabulary.</summary>
    public static ItemUseResult Refused(Refusal refusal) => new(false, refusal.Code, refusal.Message);
}

/// <summary>Ordinary item uses over the party's current inventory and the game's explicitly composed policy.</summary>
public sealed class PartyItemUse(PartyEntity party, IItemUseRule rule)
{
    /// <summary>The same party every inventory and equipment action addresses.</summary>
    public PartyEntity Party { get; } = party ?? throw new ArgumentNullException(nameof(party));
    /// <summary>The game's policy and words for item uses and powers.</summary>
    public IItemUseRule Rule { get; } = rule ?? throw new ArgumentNullException(nameof(rule));
    /// <summary>The last use's answer, which has no saved state of its own.</summary>
    public ItemUseResult? Last { get; private set; }
    /// <summary>Changes when a new answer is admitted.</summary>
    public long Stamp { get; private set; } = ChangeStamp.Next();
    /// <summary>Uses a real pack item on a real member, through the ruleset's canonical owners.</summary>
    public ItemUseResult Use(int member, ItemInstanceId item)
    {
        ItemUseResult result;
        if (member < 0 || member >= Party.Members.Count)
            result = ItemUseResult.Refused(new(ItemUseCodes.MemberAbsent, "Choose a member of this party."));
        else if (Party.FindItem(item) is not { } held || !held.Custody.IsInSharedInventory)
            result = ItemUseResult.Refused(new(ItemUseCodes.ItemAbsent, "Choose an item in the party's shared pack."));
        else if (Rule.ActionOf(held) is null)
            result = ItemUseResult.Refused(new(ItemUseCodes.Unsupported, "This item has no ordinary use in this ruleset."));
        else result = Rule.Use(Party, Party.Members[member], held);
        Last = result;
        Stamp = ChangeStamp.Next();
        return result;
    }
}

/// <summary>The named reasons ordinary carried-item use can refuse.</summary>
public static class ItemUseCodes
{
    /// <summary>The refusal code item-use-member-absent.</summary>
    public const string MemberAbsent = "item-use-member-absent";
    /// <summary>The refusal code item-use-item-absent.</summary>
    public const string ItemAbsent = "item-use-item-absent";
    /// <summary>The refusal code item-use-unsupported.</summary>
    public const string Unsupported = "item-use-unsupported";
    /// <summary>The refusal code item-use-screen-open.</summary>
    public const string ScreenOpen = "item-use-screen-open";
    /// <summary>The refusal code item-use-unowned.</summary>
    public const string OwnerAbsent = "item-use-unowned";
}
