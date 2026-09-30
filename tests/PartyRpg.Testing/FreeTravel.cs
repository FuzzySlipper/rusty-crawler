using PartyRpg.Kit.World;

namespace PartyRpg.Testing;

/// <summary>A travel cost rule that charges nothing for any crossing, so a suite tests its own mechanism and not a journey.</summary>
public sealed class FreeTravel : ITravelCostRule
{
    /// <inheritdoc />
    public TravelCostQuote Quote(TransitionRequest request) => TravelCostQuote.Payable(TravelCost.Free);
}
