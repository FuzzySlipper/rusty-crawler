namespace PartyRpg.Kit.Party;

/// <summary>The game's division of found gold, before its kept share enters the shared purse.</summary>
public interface IFoundGoldRule
{
    /// <summary>States the total after any finding bonus and the share taken from it.</summary>
    FoundGoldDivision Divide(PartyEntity party, int found);
}

/// <summary>A finding and the portion that its rule gives away. Ordinary sales and refunds are not findings.</summary>
public readonly record struct FoundGoldDivision
{
    /// <summary>Creates a whole division, with no negative or overdrawn share.</summary>
    public FoundGoldDivision(int total, int share)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(total);
        if (share < 0 || share > total) throw new ArgumentOutOfRangeException(nameof(share));
        Total = total;
        Share = share;
    }

    /// <summary>The finding after the game's bonuses.</summary>
    public int Total { get; }
    /// <summary>The portion taken before the shared purse receives it.</summary>
    public int Share { get; }
    /// <summary>What the party keeps.</summary>
    public int Kept => Total - Share;
}
