using PartyRpg.Kit.Content;
using PartyRpg.Kit.World;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// What moving between places costs in this game, quoted for every transition kind.
/// </summary>
/// <remarks>
/// Walking and stepping through an entrance cost nothing here because time passes while the party
/// moves, which the clock charges continuously rather than per transition. Paid and magical travel
/// cost days, and both need owners that do not exist yet — the party's purse arrives with the party
/// foundation, and stables, docks, portals, and beacons arrive with services and magic. Until then
/// those kinds are refused *by name* rather than quietly travelling free, so a transition never looks
/// cheaper than it is.
/// </remarks>
internal sealed class MightAndMagic7TravelCostRule : ITravelCostRule
{
    /// <inheritdoc />
    public TravelCostQuote Quote(TransitionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Kind switch
        {
            TransitionKind.Walking or TransitionKind.Entrance or TransitionKind.Scripted => TravelCostQuote.Payable(TravelCost.Free),
            TransitionKind.PaidService => TravelCostQuote.Refused(new TravelRefusal(
                "travel-paid-unpaid",
                "Paid travel needs the party's purse, which the party foundation provides; until then a fare cannot be paid or checked.")),
            TransitionKind.Portal => TravelCostQuote.Refused(new TravelRefusal(
                "travel-portal-unowned",
                "Magical travel needs the spell and beacon owners, which arrive with magic; until then a portal cannot be opened or charged.")),
            _ => TravelCostQuote.Refused(new TravelRefusal(
                "travel-kind-unknown",
                $"Travel kind '{request.Kind}' has no cost policy.")),
        };
    }
}
