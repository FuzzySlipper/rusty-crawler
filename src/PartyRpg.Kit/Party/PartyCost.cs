using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Party;

/// <summary>What one charge asks of the party: coins and provisions, each drawn from its own account.</summary>
/// <remarks>
/// <para>
/// A service, a fare, a training fee, and a temple donation all ask the party for something, and what they
/// ask for comes out of the party's own accounts rather than out of a character's pocket. Stating both
/// parts in one value is what lets a single settlement path pay a charge whole or refuse it whole: a charge
/// that could be met in coin and not in food would otherwise leave the party half paid.
/// </para>
/// <para>
/// <b>Game time is deliberately absent.</b> What a journey takes is the one shared clock's business, so a
/// transition's elapsed time is handed to the clock's owner and never to a party account; the two accounts
/// here are the ones the party itself holds.
/// </para>
/// </remarks>
public readonly record struct PartyCost
{
    /// <summary>Creates a charge.</summary>
    /// <param name="coins">How many coins the charge asks for, which cannot be negative.</param>
    /// <param name="provisions">How much food the charge asks for, which cannot be negative.</param>
    /// <exception cref="ArgumentOutOfRangeException">An amount is negative, which would pay the party rather than charge it.</exception>
    public PartyCost(int coins, Provisions provisions = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(coins);
        Coins = coins;
        Food = provisions;
    }

    /// <summary>How many coins the charge asks for.</summary>
    public int Coins { get; }

    /// <summary>How much food the charge asks for, with the unit it is stated in.</summary>
    public Provisions Food { get; }

    /// <summary>Whether the charge asks for nothing at all.</summary>
    public bool IsFree => Coins == 0 && Food.IsNone;

    /// <summary>A charge of coins alone, which is what a price, a fare, or a fee is.</summary>
    /// <param name="coins">How many coins, which cannot be negative.</param>
    /// <exception cref="ArgumentOutOfRangeException">The amount is negative.</exception>
    public static PartyCost OfGold(int coins) => new(coins);

    /// <summary>A charge of provisions alone, which is what a road or a camp asks.</summary>
    /// <param name="provisions">How much food, in the unit it is stated in.</param>
    public static PartyCost OfFood(Provisions provisions) => new(0, provisions);

    /// <summary>A charge that asks for nothing, which is what a free service or a walked road is.</summary>
    public static PartyCost Free => new(0, Provisions.None);
}
