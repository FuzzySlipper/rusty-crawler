namespace PartyRpg.Kit.Party;

/// <summary>The named reasons an acting-member choice is refused.</summary>
public static class PartySelectionCodes
{
    /// <summary>The requested durable member does not belong to the party.</summary>
    public const string UnknownMember = "selected-member-unknown";
    /// <summary>The game's action rule says this member cannot act.</summary>
    public const string Incapable = "selected-member-incapable";
    /// <summary>No member can act.</summary>
    public const string NobodyAble = "selected-member-none-able";
}
