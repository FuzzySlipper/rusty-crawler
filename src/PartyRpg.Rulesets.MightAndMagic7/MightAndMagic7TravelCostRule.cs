using PartyRpg.Kit.Content;
using PartyRpg.Kit.World;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// What moving between places costs in this game, quoted for every transition kind.
/// </summary>
/// <remarks>
/// <para>
/// <b>Walking a road costs a day on it.</b> A crossing between places is an overland journey in this
/// game's terms: the manual has reaching a map edge take "several days" and consume "1 food unit per day",
/// so a party that arrives has spent provisions and game time rather than sliding between maps for nothing
/// (<c>docs/research/mm7-manual-outline.md</c>, p.26). The number of days here is ours — the manual says
/// several and gives no figure — and it is stated once, with the food derived from the same daily rate the
/// camping rule charges, so the road and the camp cannot disagree about what a day eats.
/// </para>
/// <para>
/// <b>Paid and magical travel are refused by name, not made free.</b> A fare needs the services that sell
/// one — a stable, a dock, a coach — and a portal needs the spell and beacon owners; neither exists yet.
/// Refusing them by name is what keeps a transition from looking cheaper than it is: quoting nothing and
/// letting the party travel would be a journey nobody paid for and a defect nobody could see.
/// </para>
/// <para>
/// A scripted move is the world placing the party rather than the party walking anywhere, so it quotes
/// nothing: the scenario start already takes that path, and a script that moves the band across the world
/// is not a journey it made.
/// </para>
/// </remarks>
internal sealed class MightAndMagic7TravelCostRule : ITravelCostRule
{
    /// <summary>
    /// How many game days a crossing between places takes.
    /// </summary>
    /// <remarks>
    /// Ours, not the donor's: the manual says an overland crossing takes "several days" and states no
    /// number (p.26), so this is the shortest journey that is still a journey — one whole day, which is
    /// also the smallest span the donor's one-unit-per-day food rate can be charged over. A longer road
    /// costs more by raising this one value, and the provisions it quotes follow it.
    /// </remarks>
    internal const int DaysPerCrossing = 1;

    /// <summary>What one crossing of a place boundary charges: the day on the road and what it ate.</summary>
    internal static readonly TravelCost Crossing = new(
        new TravelTime(DaysPerCrossing, TravelTimeUnit.Days),
        new Provisions(
            MightAndMagic7Provisions.RationsPerDay * DaysPerCrossing,
            ProvisionUnit.Portions));

    /// <inheritdoc />
    public TravelCostQuote Quote(TransitionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Kind switch
        {
            TransitionKind.Walking or TransitionKind.Entrance => TravelCostQuote.Payable(Crossing),
            TransitionKind.Scripted => TravelCostQuote.Payable(TravelCost.Free),
            TransitionKind.PaidService => TravelCostQuote.Refused(new TravelRefusal(
                "travel-paid-unowned",
                "Paid travel needs the services that sell a fare — a stable, a dock, a coach — which arrive with the service owners; until then no fare is quoted and no coin is taken.")),
            TransitionKind.Portal => TravelCostQuote.Refused(new TravelRefusal(
                "travel-portal-unowned",
                "Magical travel needs the spell and beacon owners, which arrive with magic; until then a portal cannot be opened or charged.")),
            _ => TravelCostQuote.Refused(new TravelRefusal(
                "travel-kind-unknown",
                $"Travel kind '{request.Kind}' has no cost policy.")),
        };
    }
}
