namespace PartyRpg.Kit.Party;

/// <summary>The party's one purse: a single balance every member spends from.</summary>
/// <remarks>
/// There is no per-character currency anywhere in the party, which is the point: a price is paid once, by
/// the party, and a character that "has" money would be a second place a balance could drift. What things
/// cost, what a service charges, and whether a fare is affordable are the ruleset's answers; this holds the
/// number and refuses to let it go below zero.
/// </remarks>
public sealed class PartyPurse
{
    /// <summary>
    /// The change stamp this state took when it last changed, or when it was made: a reader that kept what it built
    /// beside this stamp reads the state again only when the stamp has moved (<see cref="ChangeStamp"/>).
    /// </summary>
    public long Stamp { get; private set; } = ChangeStamp.Next();

    /// <summary>Creates the party's purse.</summary>
    /// <param name="coins">What the party starts with, which cannot be negative.</param>
    /// <exception cref="ArgumentOutOfRangeException">The starting balance is negative.</exception>
    public PartyPurse(int coins = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(coins);
        Coins = coins;
    }

    /// <summary>What the party has.</summary>
    public int Coins { get; private set; }

    /// <summary>Adds to the purse, which a sale, a reward, or a deposit does.</summary>
    /// <param name="amount">How much to add, which cannot be negative.</param>
    /// <exception cref="ArgumentOutOfRangeException">The amount is negative, which would take money rather than add it.</exception>
    /// <exception cref="OverflowException">The balance would leave the numbers money is described in; a wrapped balance would be free money.</exception>
    public void Credit(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        Coins = checked(Coins + amount);
        Stamp = ChangeStamp.Next();
    }

    /// <summary>Whether the party can pay a price.</summary>
    /// <param name="amount">The price to judge, which cannot be negative.</param>
    /// <exception cref="ArgumentOutOfRangeException">The amount is negative, which is not a price.</exception>
    public bool CanAfford(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        return amount <= Coins;
    }

    /// <summary>Takes a price from the purse.</summary>
    /// <param name="amount">The price to pay, which cannot be negative.</param>
    /// <returns>Whether the party could pay; a refused debit leaves the purse untouched.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The amount is negative, which is not a price.</exception>
    public bool TryDebit(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        if (amount > Coins) return false;
        Coins -= amount;
        Stamp = ChangeStamp.Next();
        return true;
    }
}
