using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Party;

/// <summary>What a creation flow hands the factory: the members it built and what the party starts with.</summary>
/// <remarks>
/// A party is created once per game and never grows from nothing, so creation is an explicit value rather
/// than a sequence of calls a caller could stop halfway through: either the whole party is described, or
/// none of it exists. Starting values are carried here because they are creation's choices; the policies a
/// party obeys — equipment gating, capacity, stacking, the hired limit — belong to the factory, where the
/// ruleset and its tuning compose them.
/// </remarks>
public sealed record PartyCreation
{
    /// <summary>Describes a party about to be created.</summary>
    /// <param name="members">The members, in the order the party stands in; at least one.</param>
    /// <param name="coins">What the purse starts with, which cannot be negative.</param>
    /// <param name="foodPortions">What the larder starts with, which cannot be negative.</param>
    /// <param name="foodUnit">The unit the larder measures provisions in.</param>
    /// <param name="reputation">What the world initially thinks of the party.</param>
    /// <param name="fame">How widely the party is initially known.</param>
    /// <exception cref="ArgumentException">The party has no members.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A starting amount is negative.</exception>
    public PartyCreation(
        IReadOnlyList<MemberCreation> members,
        int coins = 0,
        int foodPortions = 0,
        ProvisionUnit foodUnit = ProvisionUnit.Portions,
        int reputation = 0,
        int fame = 0)
    {
        ArgumentNullException.ThrowIfNull(members);
        if (members.Count == 0)
        {
            throw new ArgumentException(
                "A party has at least one member; creation that produced nobody is not a party to create.",
                nameof(members));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(coins);
        ArgumentOutOfRangeException.ThrowIfNegative(foodPortions);
        Members = members;
        Coins = coins;
        FoodPortions = foodPortions;
        FoodUnit = foodUnit;
        Reputation = reputation;
        Fame = fame;
    }

    /// <summary>The members, in the order the party stands in.</summary>
    public IReadOnlyList<MemberCreation> Members { get; }

    /// <summary>What the purse starts with.</summary>
    public int Coins { get; }

    /// <summary>What the larder starts with.</summary>
    public int FoodPortions { get; }

    /// <summary>The unit the larder measures provisions in.</summary>
    public ProvisionUnit FoodUnit { get; }

    /// <summary>What the world initially thinks of the party.</summary>
    public int Reputation { get; }

    /// <summary>How widely the party is initially known.</summary>
    public int Fame { get; }
}
