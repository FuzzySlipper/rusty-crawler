using PartyRpg.Kit;
using PartyRpg.Kit.World;

namespace PartyRpg.Testing;

/// <summary>A travel cost rule that charges nothing for any crossing, so a suite tests its own mechanism and not a journey.</summary>
public sealed class FreeTravel : ITravelCostRule
{
    /// <inheritdoc />
    public TravelCostQuote Quote(TransitionRequest request) => TravelCostQuote.Payable(TravelCost.Free);
}

/// <summary>
/// A travel cost rule that lets the party walk and cross for nothing and refuses every journey bought from a
/// counter, which is how a suite reaches the paid path's own refusal.
/// </summary>
public sealed class FaresRefusedTravel : ITravelCostRule
{
    /// <summary>The code every refused fare carries.</summary>
    public const string RefusalCode = "test-fare-refused";

    /// <inheritdoc />
    public TravelCostQuote Quote(TransitionRequest request) =>
        request.Kind is TransitionKind.PaidService
            ? TravelCostQuote.Refused(new Refusal(RefusalCode, "This suite refuses travel bought from a counter."))
            : TravelCostQuote.Payable(TravelCost.Free);
}
