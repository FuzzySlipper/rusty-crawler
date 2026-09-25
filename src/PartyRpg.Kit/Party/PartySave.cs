using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Party;

/// <summary>
/// Everything a party is, as a save records it: the durable shape a restore rebuilds from.
/// </summary>
/// <remarks>
/// <para>
/// This is the durable save identity of the whole party, as opposed to content identity (the definitions
/// its members and items reference) and runtime identity (the engine entities a live party is made of,
/// which appear nowhere here because they are rebuilt on load). A save names people and items by the
/// identities the party minted, records where each item was held, and records the identity cursors — so a
/// restored party carries on without ever minting an identity this save already used.
/// </para>
/// <para>
/// The policies a party obeys are absent on purpose: gating, capacity, stacking, and the hired limit are
/// the ruleset's and its tuning's, supplied when the party is built, not state the game accumulated. That
/// is also why a restore does not re-judge what it loads — a save already recorded what the party held, and
/// re-running a changed rule could refuse a party the product itself saved.
/// </para>
/// </remarks>
public sealed record PartySave
{
    /// <summary>Records a party's durable state.</summary>
    /// <param name="nextMemberValue">The member identity cursor: the identity a restored party mints next.</param>
    /// <param name="nextItemValue">The item identity cursor: the identity a restored party mints next.</param>
    /// <param name="members">The members, in the order the party stands in; at least one.</param>
    /// <param name="items">Every item instance the party held, each with the custody it was held in.</param>
    /// <param name="coins">What the purse held, which cannot be negative.</param>
    /// <param name="foodPortions">What the larder held, which cannot be negative.</param>
    /// <param name="foodUnit">The unit the larder measured provisions in.</param>
    /// <param name="reputation">What the world thought of the party.</param>
    /// <param name="fame">How widely the party was known.</param>
    /// <param name="followers">The followers travelling with the party.</param>
    /// <param name="effects">The effects acting on the party.</param>
    /// <exception cref="ArgumentException">The save records no members.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A recorded amount is negative.</exception>
    public PartySave(
        ulong nextMemberValue,
        ulong nextItemValue,
        IReadOnlyList<PartyMemberSave> members,
        IReadOnlyList<ItemSave> items,
        int coins = 0,
        int foodPortions = 0,
        ProvisionUnit foodUnit = ProvisionUnit.Portions,
        int reputation = 0,
        int fame = 0,
        IReadOnlyList<PartyFollower>? followers = null,
        IReadOnlyList<PartyEffect>? effects = null)
    {
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(items);
        if (members.Count == 0)
        {
            throw new ArgumentException(
                "A save records a party with at least one member; a party with nobody in it is not one the game ever had.",
                nameof(members));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(coins);
        ArgumentOutOfRangeException.ThrowIfNegative(foodPortions);
        NextMemberValue = nextMemberValue;
        NextItemValue = nextItemValue;
        Members = members;
        Items = items;
        Coins = coins;
        FoodPortions = foodPortions;
        FoodUnit = foodUnit;
        Reputation = reputation;
        Fame = fame;
        Followers = followers ?? [];
        Effects = effects ?? [];
    }

    /// <summary>The member identity cursor a restored party mints from.</summary>
    public ulong NextMemberValue { get; }

    /// <summary>The item identity cursor a restored party mints from.</summary>
    public ulong NextItemValue { get; }

    /// <summary>The members, in the order the party stands in.</summary>
    public IReadOnlyList<PartyMemberSave> Members { get; }

    /// <summary>Every item instance the party held, each with the custody it was held in.</summary>
    public IReadOnlyList<ItemSave> Items { get; }

    /// <summary>What the purse held.</summary>
    public int Coins { get; }

    /// <summary>What the larder held.</summary>
    public int FoodPortions { get; }

    /// <summary>The unit the larder measured provisions in.</summary>
    public ProvisionUnit FoodUnit { get; }

    /// <summary>What the world thought of the party.</summary>
    public int Reputation { get; }

    /// <summary>How widely the party was known.</summary>
    public int Fame { get; }

    /// <summary>The followers travelling with the party.</summary>
    public IReadOnlyList<PartyFollower> Followers { get; }

    /// <summary>The effects acting on the party.</summary>
    public IReadOnlyList<PartyEffect> Effects { get; }
}
