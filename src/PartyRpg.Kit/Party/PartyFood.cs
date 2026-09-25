using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Party;

/// <summary>The party's one food supply, stated in the unit the world charges provisions in.</summary>
/// <remarks>
/// <para>
/// Food is party-scoped for the same reason the purse is: the party eats together, and a per-character
/// larder would be four numbers where the game has one. The unit is carried rather than assumed so that a
/// travel cost quoted in one unit is refused by a larder that measures another, instead of being silently
/// reinterpreted — the kit owns no conversion between units, because the ratio belongs to whoever defined
/// them.
/// </para>
/// <para>
/// What a party with no food suffers is a ruleset rule over conditions and time; this only refuses to let a
/// larder go below empty, and reports a spend it could not make rather than pretending the road was paid
/// for.
/// </para>
/// </remarks>
public sealed class PartyFood
{
    /// <summary>Creates the party's larder.</summary>
    /// <param name="portions">What the party starts with, which cannot be negative.</param>
    /// <param name="unit">The unit the amount is stated in.</param>
    /// <exception cref="ArgumentOutOfRangeException">The starting amount is negative.</exception>
    public PartyFood(int portions = 0, ProvisionUnit unit = ProvisionUnit.Portions)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(portions);
        Portions = portions;
        Unit = unit;
    }

    /// <summary>What the party has.</summary>
    public int Portions { get; private set; }

    /// <summary>The unit the amount is stated in.</summary>
    public ProvisionUnit Unit { get; }

    /// <summary>Whether the party has nothing left.</summary>
    public bool IsEmpty => Portions == 0;

    /// <summary>Adds to the larder, which buying provisions or finding them does.</summary>
    /// <param name="provisions">How much to add, which cannot be negative.</param>
    /// <exception cref="ArgumentOutOfRangeException">The amount is negative, which would eat rather than add.</exception>
    /// <exception cref="OverflowException">The larder would leave the numbers it is described in.</exception>
    public void Credit(int provisions)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(provisions);
        Portions = checked(Portions + provisions);
    }

    /// <summary>Spends a travel cost's provisions, which is where the world's quoted food arrives.</summary>
    /// <param name="provisions">The charge the world quoted, with the unit it was quoted in.</param>
    /// <returns>Whether the larder could pay; a refused spend leaves it untouched.</returns>
    /// <exception cref="ArgumentException">The charge is stated in a unit this larder does not measure.</exception>
    public bool TrySpend(Provisions provisions)
    {
        if (provisions.Unit != Unit)
        {
            throw new ArgumentException(
                $"The charge is stated in {provisions.Unit} while this larder measures {Unit}; the kit converts between units of food for nobody.",
                nameof(provisions));
        }

        return TryDebit(provisions.Amount);
    }

    /// <summary>Takes provisions from the larder.</summary>
    /// <param name="provisions">How much to take, which cannot be negative.</param>
    /// <returns>Whether the larder held enough; a refused debit leaves it untouched.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The amount is negative.</exception>
    public bool TryDebit(int provisions)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(provisions);
        if (provisions > Portions) return false;
        Portions -= provisions;
        return true;
    }
}
