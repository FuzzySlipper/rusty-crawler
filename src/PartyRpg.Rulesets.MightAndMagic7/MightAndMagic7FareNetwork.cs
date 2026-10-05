using System.Globalization;
using System.Text.Json;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.World;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// Which passages this game's stables and docks sell: every coach town reaches every other coach town, and
/// every port every other port.
/// </summary>
/// <remarks>
/// <para>
/// <b>Content says which counters sell passages and where they stand; this says where they go.</b> The
/// imported pack carries a counter's kind (a stable or a dock) and its placement in a place, and nothing
/// about its destinations. The original keeps its coach and boat routes in the executable, and the donor
/// records them as a table keyed by house — thirty-five routes, each with its own weekday schedule, length
/// and, for the island's routes, a quest gate (OpenEnroth <c>src/GUI/UI/Houses/Transport.cpp:38-95</c>). This game states
/// the simpler network the shipped world supports instead: a counter sells a passage to every other place
/// that keeps a counter of its own kind, every day, on the kind's one route. That is an approximation of the
/// structure (stables and docks form two networks over the towns that keep them) and ours in its detail; the
/// donor's per-route schedules and gates are not carried, and the starting island's dock, which the donor
/// lists with no route at all (<c>Transport.cpp:87</c>), sells passages here like every other port.
/// </para>
/// <para>
/// <b>One reading serves the counter and the world.</b> The counter's offers (<see cref="MightAndMagic7Services"/>)
/// and the world graph's sold crossings (<see cref="IFareNetwork.Journeys"/>) are both read from this network, so
/// what a stable offers and what the road it sells will carry are one answer. How long each route takes is
/// <see cref="MightAndMagic7FareDays"/>, never this.
/// </para>
/// <para>
/// <b>Where a passage lands</b> is the destination's own arrival point for a party's start when it states one,
/// its first arrival point otherwise, and its origin when it states none — all the destination's data rather
/// than coordinates this rule chooses. The donor lands each route at its own position (the table above); this
/// game has one landing per place.
/// </para>
/// </remarks>
internal sealed class MightAndMagic7FareNetwork : IFareNetwork
{
    /// <summary>The arrival point a passage prefers: the one a place states for the party's own start.</summary>
    internal const string PartyStartPoint = "Party Start";

    /// <summary>The field a service placement names its counter by.</summary>
    private const string CounterField = "houseId";

    private readonly IReadOnlyList<Counter> _counters;
    private readonly IReadOnlyDictionary<string, string> _placeNames;
    private readonly Dictionary<string, List<PlaceId>> _destinations;

    private MightAndMagic7FareNetwork(IReadOnlyList<Counter> counters, IReadOnlyDictionary<string, string> placeNames, Dictionary<string, List<PlaceId>>? destinations = null)
    {
        _counters = counters;
        _placeNames = placeNames;
        _destinations = destinations ?? [];
    }

    /// <summary>A network with no counter in it, for a session that loaded no content.</summary>
    internal static MightAndMagic7FareNetwork Empty { get; } = new([], new Dictionary<string, string>());

    /// <summary>Reads which counters sell passages, on which route, and where each stands.</summary>
    /// <remarks>
    /// A counter stands where a place's service placement names it, which is what the party walks up to; a
    /// counter no place stands sells nothing. Content that authors a sold crossing of its own is refused by
    /// name: this game's fares are this network's, and a pack written when the importer still derived them
    /// would otherwise sell every journey twice.
    /// </remarks>
    /// <param name="catalog">The validated content, or null when none was loaded.</param>
    /// <exception cref="ContentValidationException">Content authors a sold crossing of its own, or stands one passage counter in two places.</exception>
    internal static MightAndMagic7FareNetwork Read(ContentCatalog? catalog)
    {
        if (catalog is null) return Empty;
        List<ContentValidationIssue> issues = [];
        foreach ((LoadedPack pack, ContentDocument document, ContentEntry link) in catalog.Entries(PlaceGraphLoader.TransitionDefinitionKind))
        {
            if (link.GetBoolean(PlaceGraphLoader.FareField) is not true) continue;
            issues.Add(new ContentValidationIssue(
                "fare-link-authored",
                $"travel link '{link.Id}' is authored as a sold passage, and this game's passages are its fare network's over the counters content places; a pack written before the network moved to the ruleset is written again by the importer.",
                pack.PackId,
                document.DocumentId));
        }

        if (issues.Count > 0)
        {
            throw new ContentValidationException($"This game's fare network cannot be read: {issues[0].Message}", issues);
        }

        Dictionary<string, string> routes = new(StringComparer.Ordinal);
        foreach ((_, _, ContentEntry service) in catalog.Entries(MightAndMagic7Services.DefinitionKind))
        {
            if (MightAndMagic7FareDays.RouteOf(service.GetString("kind")) is { } route) routes[service.Id] = route;
        }

        // A counter is found by its identity (a party at the counter asks what it sells), and what it sells is
        // every other stop from the place it stands in. One counter placed twice in its own place is one
        // counter with two doors; one standing in two places would sell from whichever was read first, so it is
        // refused by name.
        Dictionary<string, string> names = new(StringComparer.Ordinal);
        Dictionary<string, Counter> standing = new(StringComparer.Ordinal);
        List<Counter> counters = [];
        foreach ((LoadedPack pack, ContentDocument document, ContentEntry place) in catalog.Entries(PlaceGraphLoader.PlaceDefinitionKind))
        {
            names[place.Id] = place.GetString("name") is { Length: > 0 } name ? name : place.Id;
            foreach (JsonElement placement in place.GetArray(PlacePopulationContent.PlacementsField))
            {
                if (!string.Equals(ContentEntry.ReadString(placement, PlacePopulationContent.KindField), MightAndMagic7Services.PlacementKind, StringComparison.Ordinal)) continue;
                string id = ContentEntry.ReadId(placement, CounterField);
                if (!routes.TryGetValue(id, out string? route)) continue;
                Counter counter = new(id, new PlaceId(place.Id), route);
                if (!standing.TryAdd(id, counter))
                {
                    if (standing[id].Place == counter.Place) continue;
                    issues.Add(new ContentValidationIssue(
                        "fare-counter-placed-twice",
                        $"the counter '{id}' that sells passages on route '{route}' stands in place '{standing[id].Place}' and in place '{place.Id}', so where a passage bought at it leaves from could not be told.",
                        pack.PackId,
                        document.DocumentId));
                    continue;
                }

                counters.Add(counter);
            }
        }

        if (issues.Count > 0)
        {
            throw new ContentValidationException($"This game's fare network cannot be read: {issues[0].Message}", issues);
        }

        // Counters are walked in the order their ids state, which is the building table's own order, so the
        // network and every list read from it come out the same however the packs were laid out.
        counters.Sort((left, right) =>
        {
            int order = Order(left.Service).CompareTo(Order(right.Service));
            return order != 0 ? order : string.CompareOrdinal(left.Service, right.Service);
        });
        Dictionary<string, List<PlaceId>> destinations = [];
        foreach ((LoadedPack pack, ContentDocument document, ContentEntry entry) in catalog.Entries("service-destination"))
        {
            string service = entry.GetId("service"), destination = entry.GetId("toPlace");
            if (!standing.TryGetValue(service, out Counter? seller) || !names.ContainsKey(destination) || seller.Place.Value == destination)
                throw new ContentValidationException("A passage must name a placed travel counter and another loaded destination.",
                    [new("service-destination-invalid", $"Passage '{entry.Id}' names counter '{service}' and destination '{destination}'.", pack.PackId, document.DocumentId)]);
            if (!destinations.TryGetValue(service, out var stops)) destinations[service] = stops = [];
            stops.Add(new PlaceId(destination));
        }
        return new MightAndMagic7FareNetwork(counters, names, destinations);
    }

    /// <summary>The routes this game sells passages on, in the order the network walks them.</summary>
    internal static IReadOnlyList<string> Routes { get; } = [MightAndMagic7FareDays.CoachRoute, MightAndMagic7FareDays.BoatRoute];

    /// <summary>The places a route serves: every place where a counter on it stands, each once, in counter order.</summary>
    /// <param name="route">The route's name.</param>
    internal IReadOnlyList<PlaceId> Stops(string route) =>
        [.. _counters.Where(counter => string.Equals(counter.Route, route, StringComparison.Ordinal)).Select(counter => counter.Place).Distinct()];

    /// <summary>The passages one counter sells: where each goes, what the place is called, and the route it runs on.</summary>
    /// <param name="service">The counter's identity.</param>
    /// <returns>Every destination, in the network's order; none when the counter sells no passage or stands nowhere.</returns>
    /// <remarks>The network holds each counter once — one standing in two places is refused when it is read — so the lookup never chooses.</remarks>
    internal IReadOnlyList<Passage> SoldBy(string service)
    {
        if (_counters.FirstOrDefault(counter => string.Equals(counter.Service, service, StringComparison.Ordinal)) is not { } sold) return [];
        return
        [
            .. Stops(sold.Route).Concat(_destinations.GetValueOrDefault(service) ?? []).Distinct()
                .Where(place => place != sold.Place)
                .Select(place => new Passage(place, _placeNames.GetValueOrDefault(place.Value, place.Value), sold.Route)),
        ];
    }

    /// <inheritdoc />
    public IReadOnlyList<PlaceTransition> Journeys(IReadOnlyList<PlaceDefinition> places)
    {
        ArgumentNullException.ThrowIfNull(places);
        Dictionary<PlaceId, PlaceDefinition> known = [];
        foreach (PlaceDefinition place in places) known.TryAdd(place.Id, place);

        // One crossing per pair of stops on a route, however many counters of the kind a town keeps: the
        // ticket names a place and a route, so two crossings alike in both would be a journey nobody could
        // tell from the other.
        List<PlaceTransition> journeys = [];
        HashSet<(PlaceId, PlaceId, string)> added = [];
        foreach (Counter counter in _counters)
        foreach (Passage passage in SoldBy(counter.Service))
        {
            if (!known.ContainsKey(passage.Place) || !added.Add((counter.Place, passage.Place, passage.Route))) continue;
            journeys.Add(new PlaceTransition(counter.Place, passage.Place, Landing(known[passage.Place]),
                string.Create(CultureInfo.InvariantCulture, $"fare-{passage.Route}-{counter.Place}-{passage.Place}"))
                { FareRoute = passage.Route });
        }

        return journeys;
    }

    /// <summary>Where a passage lands in its destination, from the destination's own arrival points.</summary>
    private static PlaceArrival Landing(PlaceDefinition destination) =>
        (destination.FindEntryPoint(PartyStartPoint) ?? destination.EntryPoints.FirstOrDefault()) is { } point
            ? PlaceArrival.AtEntryPoint(point.Id)
            : PlaceArrival.AtPose(PlacePose.Origin);

    private static int Order(string id) =>
        int.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out int order) ? order : int.MaxValue;

    /// <summary>One counter that sells passages: which it is, where it stands, and the route it sells on.</summary>
    private sealed record Counter(string Service, PlaceId Place, string Route);

    /// <summary>One passage a counter sells.</summary>
    /// <param name="Place">The place it reaches.</param>
    /// <param name="Name">What that place is called.</param>
    /// <param name="Route">The route it runs on, which the ticket carries.</param>
    internal readonly record struct Passage(PlaceId Place, string Name, string Route);
}
