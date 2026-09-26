using PartyRpg.Kit.Content;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Services;
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
/// <b>Paid travel is a fare the party has bought.</b> A coach and a boat are sold at a counter, and what a
/// counter sells is a passage: party-carried state naming the place it reaches and how many days the
/// journey takes. Taking a paid transition is boarding, and the ticket is torn as the party boards, so a
/// fare bought once pays for one journey rather than for every later one. A paid transition the party holds
/// no passage for is refused by name, naming the counter that sells one — a journey is not made cheaper by
/// being unaffordable.
/// </para>
/// <para>
/// <b>The party's own larder and clock are what pays.</b> The quote states time and provisions, which the
/// session hands to the owners of the clock and the larder; a fare's own coin was already taken at the
/// counter through the party's one settlement path, so boarding charges no coin a second time.
/// </para>
/// <para>
/// <b>A portal crosses no ground.</b> Magical travel is opened by the spell that paid for it, in spell
/// points rather than in days, so a transition taken as a portal quotes a journey of no time and eats
/// nothing: the party arrives where it named, and the road it did not walk is the reason there is no road
/// cost. What a portal may reach is the spell's own business — this rule only prices the crossing it is
/// handed, exactly as it prices a walk and a fare.
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

    private readonly PartyEntity? _party;

    /// <summary>Creates the rule over the party whose bought passages it honours.</summary>
    /// <param name="party">
    /// The party whose bought passages are read and torn, or null when no party was composed. Without one
    /// there is nothing that could hold a fare, so paid travel is refused by name rather than taken free.
    /// </param>
    internal MightAndMagic7TravelCostRule(PartyEntity? party = null) => _party = party;

    /// <inheritdoc />
    public TravelCostQuote Quote(TransitionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Kind switch
        {
            TransitionKind.Walking or TransitionKind.Entrance => TravelCostQuote.Payable(Crossing),
            TransitionKind.Scripted => TravelCostQuote.Payable(TravelCost.Free),
            TransitionKind.PaidService => Board(request),
            TransitionKind.Portal => TravelCostQuote.Payable(TravelCost.Free),
            _ => TravelCostQuote.Refused(new TravelRefusal(
                "travel-kind-unknown",
                $"Travel kind '{request.Kind}' has no cost policy.")),
        };
    }

    /// <summary>
    /// What boarding costs the party that holds a passage, or why it cannot board.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The passage names the place it reaches, so a ticket to one town does not pay for a journey to
    /// another: a party that holds the wrong one is refused with the place it named. The days the journey
    /// takes are the ticket's own, which the counter that sold it wrote from the route it sells, so the
    /// price of the journey and the journey itself come from one route rather than from two tables that
    /// could drift.
    /// </para>
    /// <para>
    /// <b>The ticket is torn when the journey is quoted.</b> A transition path quotes exactly once per
    /// journey — the executive asks the rule and then resolves the arrival — so this is the boarding. A
    /// request that contradicts the world throws before the rule is consulted at all, so a fare is never
    /// spent on a journey nobody could take.
    /// </para>
    /// </remarks>
    private TravelCostQuote Board(TransitionRequest request)
    {
        PlaceId destination = request.Transition.To;
        if (_party is null)
        {
            return TravelCostQuote.Refused(new TravelRefusal(
                "travel-no-party",
                "A fare is bought by a party and this session holds none, so there is nobody to board."));
        }

        int days = ServicePassage.DaysTo(_party, destination);
        if (days <= 0)
        {
            return TravelCostQuote.Refused(new TravelRefusal(
                "travel-fare-unpaid",
                $"A seat to {destination} is bought at a stable or a dock and the party holds no passage to it; buying one at the counter is what pays for the journey."));
        }

        ServicePassage.Spend(_party, destination);
        return TravelCostQuote.Payable(new TravelCost(
            new TravelTime(days, TravelTimeUnit.Days),
            Provisions.None));
    }
}
