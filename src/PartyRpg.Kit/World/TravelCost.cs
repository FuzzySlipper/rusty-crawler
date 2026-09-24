namespace PartyRpg.Kit.World;

/// <summary>What one transition charges the party: elapsed time and provisions, each with its unit.</summary>
/// <remarks>
/// The kit states a cost; it does not apply one. The session's clock and the party's food are owned by
/// the owners that hold them, so a transition reports the cost the rule quoted and the caller hands each
/// part to its owner. Coin is deliberately absent for the same reason: a fare is priced and judged
/// affordable by the cost rule, and the purse it leaves belongs to the party owner, not to the world.
/// </remarks>
/// <param name="Time">How much game time the transition takes.</param>
/// <param name="Food">How many provisions the party spends making the transition.</param>
public readonly record struct TravelCost(TravelTime Time, Provisions Food)
{
    /// <summary>A transition that costs neither time nor food.</summary>
    public static TravelCost Free => new(TravelTime.None, Provisions.None);

    /// <summary>Whether the transition charges nothing at all.</summary>
    public bool IsFree => Time.IsNone && Food.IsNone;
}
