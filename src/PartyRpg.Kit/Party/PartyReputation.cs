namespace PartyRpg.Kit.Party;

/// <summary>The party's one standing with the world: its reputation and its fame.</summary>
/// <remarks>
/// Both are party-scoped because the world judges the band rather than a member: a shop's prices, a guild's
/// welcome, and a ruler's offer are answers about the party. Fame and reputation are kept apart because a
/// game uses them differently — one is what people have heard, the other what they think — and both are
/// plain numbers here. Whether either is bounded, and what a value means, is the ruleset's policy over its
/// own tables.
/// </remarks>
public sealed class PartyReputation
{
    /// <summary>Creates the party's standing.</summary>
    /// <param name="reputation">What the world thinks of the party.</param>
    /// <param name="fame">How widely the party is known.</param>
    public PartyReputation(int reputation = 0, int fame = 0)
    {
        Reputation = reputation;
        Fame = fame;
    }

    /// <summary>What the world thinks of the party.</summary>
    public int Reputation { get; private set; }

    /// <summary>How widely the party is known.</summary>
    public int Fame { get; private set; }

    /// <summary>Changes reputation by a delta, which a deed, a quest, or a crime does.</summary>
    /// <param name="delta">How much to change it by, which may be negative.</param>
    /// <exception cref="OverflowException">The change would leave the numbers reputation is described in.</exception>
    public void ChangeReputation(int delta) => Reputation = checked(Reputation + delta);

    /// <summary>Changes fame by a delta.</summary>
    /// <param name="delta">How much to change it by, which may be negative.</param>
    /// <exception cref="OverflowException">The change would leave the numbers fame is described in.</exception>
    public void ChangeFame(int delta) => Fame = checked(Fame + delta);
}
