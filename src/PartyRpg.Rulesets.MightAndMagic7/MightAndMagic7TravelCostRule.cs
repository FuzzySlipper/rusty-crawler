using PartyRpg.Kit;
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
/// counter sells is a passage: party-carried state naming the place it reaches and the route it was sold on. Taking a paid transition is boarding, and the ticket is torn as the party boards, so a
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
            _ => TravelCostQuote.Refused(new Refusal(
                MightAndMagic7Codes.TravelKindUnknown,
                $"Travel kind '{request.Kind}' has no cost policy.")),
        };
    }

    /// <summary>
    /// What boarding costs the party that holds a passage, or why it cannot board.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The passage names the place it reaches and the route it was sold on, so a ticket to one town does not
    /// pay for a journey to another, nor a coach ticket for the boat: a party that holds no passage on this
    /// crossing's route to its place is refused with the place named. The days the journey takes are not the
    /// ticket's: they are this game's rule for the route (<see cref="MightAndMagic7FareDays"/>), which timed the
    /// crossing when the world was built over the tuning the session loaded, so a ticket bought before a retune
    /// is honoured at today's length rather than refused for naming an old one.
    /// </para>
    /// <para>
    /// <b>A quote leaves the ticket intact.</b> The session tears it through <see cref="Arrived"/> only
    /// after the destination and its ground admit the party, so a refused journey can be retried.
    /// </para>
    /// </remarks>
    private TravelCostQuote Board(TransitionRequest request)
    {
        PlaceId destination = request.Transition.To;
        if (_party is null)
        {
            return TravelCostQuote.Refused(new Refusal(
                MightAndMagic7Codes.TravelNoParty,
                "A fare is bought by a party and this session holds none, so there is nobody to board."));
        }

        if (request.Transition.FareRoute is not { } route || request.Transition.FareDays is not { } days)
        {
            return TravelCostQuote.Refused(new Refusal(
                TravelCodes.TravelFareUnstated,
                $"The crossing '{request.Transition.Source}' to {destination} is taken as a bought journey and no counter sells it, so there is no route a passage could name."));
        }

        if (!string.Equals(_party.Passages.RouteTo(destination), route, StringComparison.Ordinal))
        {
            return TravelCostQuote.Refused(new Refusal(
                MightAndMagic7Codes.TravelFareUnpaid,
                $"A seat to {destination} by {route} is bought at a stable or a dock and the party holds no such passage; buying one at the counter is what pays for the journey."));
        }

        return TravelCostQuote.Payable(new TravelCost(
            new TravelTime(days, TravelTimeUnit.Days),
            Provisions.None));
    }

    /// <inheritdoc />
    public void Arrived(TransitionRequest request)
    {
        if (request.Kind == TransitionKind.PaidService) _party!.Passages.Spend(request.Transition.To);
    }
}

/// <summary>How long a journey a counter sells takes in this game: one length per network, as tuned.</summary>
/// <remarks>
/// <para>
/// A passage runs on one of two networks — a stable's coaches and a dock's boats — and which a counter sells on
/// follows from its kind (<see cref="RouteOf"/>). How many days a network's journey takes is this game's number
/// (<see cref="MightAndMagic7Tuning.CoachDays"/> and <see cref="MightAndMagic7Tuning.BoatDays"/>), read from the
/// tuning of the catalog the session loaded, so the days the counter quotes and the days the road charges the
/// clock are one answer, and a retune changes both without importing anything. The ticket carries the route and
/// not the days, so a retune never strands a passage the party already holds.
/// </para>
/// <para>
/// The donor times each of its thirty-five routes separately, one to seven days (OpenEnroth
/// <c>src/GUI/UI/Houses/Transport.cpp:38-78</c>); this game states one length per network, which is an
/// approximation recorded on the handles themselves.
/// </para>
/// </remarks>
internal sealed class MightAndMagic7FareDays : IFareDurationRule
{
    /// <summary>The route a stable's passage runs on.</summary>
    internal const string CoachRoute = "coach";

    /// <summary>The route a dock's passage runs on.</summary>
    internal const string BoatRoute = "boat";

    private readonly TuningProfile _tuning;

    /// <summary>Creates the rule over the tuning the session's content states.</summary>
    /// <param name="tuning">The selected tuning, which the two networks' lengths are read from.</param>
    internal MightAndMagic7FareDays(TuningProfile tuning) => _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));

    /// <summary>The rule over a catalog's own tuning, every handle it does not state at its default.</summary>
    /// <param name="catalog">The selected content, or null for every default.</param>
    internal static MightAndMagic7FareDays Read(ContentCatalog? catalog) => new(MightAndMagic7Tuning.Read(catalog));

    /// <summary>The route a counter of one service kind sells passages on, or null when the kind sells none.</summary>
    /// <param name="serviceKind">The counter's service kind.</param>
    internal static string? RouteOf(string serviceKind) =>
        string.Equals(serviceKind, MightAndMagic7ServiceKinds.Stables, StringComparison.Ordinal) ? CoachRoute
        : string.Equals(serviceKind, MightAndMagic7ServiceKinds.Boats, StringComparison.Ordinal) ? BoatRoute
        : null;

    /// <summary>How many days a journey on a route takes, or null when this game states no such route.</summary>
    /// <param name="route">The route's name.</param>
    internal int? DaysOf(string route) =>
        string.Equals(route, CoachRoute, StringComparison.Ordinal) ? _tuning.Whole(MightAndMagic7Tuning.CoachDays)
        : string.Equals(route, BoatRoute, StringComparison.Ordinal) ? _tuning.Whole(MightAndMagic7Tuning.BoatDays)
        : null;

    /// <inheritdoc />
    int? IFareDurationRule.DaysOf(PlaceId? from, PlaceId to, string route) => DaysOf(route);
}
