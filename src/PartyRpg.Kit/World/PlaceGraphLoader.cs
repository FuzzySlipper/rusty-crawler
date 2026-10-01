using System.Text.Json;
using PartyRpg.Kit.Content;

namespace PartyRpg.Kit.World;

/// <summary>
/// Builds the world graph from a content catalog: places from the catalog's place definitions, and the
/// transitions between them from its travel links.
/// </summary>
/// <remarks>
/// The definition kinds are named here as strings because that is what content declares. The kit reads
/// the fields it owns — identity, destination, arrival, whether a counter sells the crossing as a passage,
/// and the route a sold crossing runs on — and leaves every other field to the ruleset, which is why a place
/// entry's encounter settings or map reference never appear in this assembly. How long a sold crossing
/// takes is not content at all: it is the game's <see cref="IFareDurationRule"/>, asked here per route. A
/// game whose counters' destinations are a rule rather than authored links states them through its
/// <see cref="IFareNetwork"/>, whose crossings are timed and checked here exactly like authored ones.
/// </remarks>
public static class PlaceGraphLoader
{
    /// <summary>The definition kind that carries places.</summary>
    public const string PlaceDefinitionKind = "place";

    /// <summary>The definition kind that carries transitions between places.</summary>
    public const string TransitionDefinitionKind = "travel-link";

    /// <summary>The field stating that a counter sells this crossing as a passage.</summary>
    public const string FareField = "fare";

    /// <summary>The field naming the route a crossing a counter sells runs on, which the game's rule times.</summary>
    public const string FareRouteField = "route";

    /// <summary>Loads the graph, failing with every problem found rather than the first.</summary>
    /// <param name="catalog">The validated content catalog to read.</param>
    /// <param name="fares">
    /// How long each route a counter sells takes, as the game's rule decides. Without one a world may still
    /// state walked crossings, and a crossing it sells as a passage is a defect named at load: nothing could
    /// say how long the journey takes.
    /// </param>
    /// <param name="network">
    /// Which crossings the game's counters sell, as the game's policy decides over the places read, or null
    /// when every sold crossing is one content authors.
    /// </param>
    public static PlaceGraph Load(ContentCatalog catalog, IFareDurationRule? fares = null, IFareNetwork? network = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        List<ContentValidationIssue> issues = [];
        List<PlaceDefinition> places = [];
        HashSet<PlaceId> known = [];
        foreach ((LoadedPack pack, ContentDocument document, ContentEntry entry) in catalog.Entries(PlaceDefinitionKind))
        {
            PlaceDefinition? place = ReadPlace(pack, document, entry, issues);
            if (place is null) continue;
            if (!known.Add(place.Id))
            {
                issues.Add(new ContentValidationIssue(
                    "place-id-reused",
                    $"place '{place.Id}' is declared more than once.",
                    pack.PackId,
                    document.DocumentId));
                continue;
            }

            places.Add(place);
        }

        // A transition is found by its id (an entrance names the one it takes) and a sold crossing by where it
        // leaves, where it goes and its route (a ticket names those), so each is held once. Two authored links
        // sharing an id are already refused by the catalog (entry-id-reused, per kind); what reaches here is a
        // crossing the game's fare network states under an id another transition holds, or two sold crossings
        // a ticket could not tell apart.
        List<PlaceTransition> transitions = [];
        HashSet<string> sources = new(StringComparer.Ordinal);
        HashSet<(PlaceId? From, PlaceId To, string Route)> sold = [];
        void Admit(PlaceTransition transition, string packId, string? documentId)
        {
            if (!sources.Add(transition.Source))
            {
                issues.Add(new ContentValidationIssue(
                    "transition-id-reused",
                    $"transition '{transition.Source}' is declared more than once.",
                    packId,
                    documentId));
                return;
            }

            if (transition.FareRoute is { } route && !sold.Add((transition.From, transition.To, route)))
            {
                issues.Add(new ContentValidationIssue(
                    "transition-fare-reused",
                    $"transition '{transition.Source}' sells a passage from place '{transition.From}' to place '{transition.To}' on route '{route}', which another sold crossing already sells, so a ticket naming that place and route could not say which was bought.",
                    packId,
                    documentId));
                return;
            }

            transitions.Add(transition);
        }

        foreach ((LoadedPack pack, ContentDocument document, ContentEntry entry) in catalog.Entries(TransitionDefinitionKind))
        {
            PlaceTransition? transition = ReadTransition(pack, document, entry, known, fares, issues);
            if (transition is not null) Admit(transition, pack.PackId, document.DocumentId);
        }

        if (network is not null)
        {
            foreach (PlaceTransition journey in network.Journeys(places))
            {
                if (Sold(journey, known, fares, issues) is { } crossing) Admit(crossing, crossing.Source, documentId: null);
            }
        }

        if (issues.Count > 0)
        {
            throw new ContentValidationException(
                $"The world graph cannot be built: {issues[0].Message}",
                issues);
        }

        PlaceGraph graph = PlaceGraph.From(places, transitions);
        ValidateArrivals(graph, issues);
        if (issues.Count > 0)
        {
            throw new ContentValidationException(
                $"The world graph cannot be built: {issues[0].Message}",
                issues);
        }

        return graph;
    }

    private static PlaceDefinition? ReadPlace(
        LoadedPack pack,
        ContentDocument document,
        ContentEntry entry,
        List<ContentValidationIssue> issues)
    {
        string kindText = entry.GetString("kind");
        PlaceKind kind;
        if (string.Equals(kindText, "region", StringComparison.OrdinalIgnoreCase))
        {
            kind = PlaceKind.Region;
        }
        else if (string.Equals(kindText, "interior", StringComparison.OrdinalIgnoreCase))
        {
            kind = PlaceKind.Interior;
        }
        else
        {
            issues.Add(new ContentValidationIssue(
                "place-kind-unknown",
                $"place '{entry.Id}' declares kind '{kindText}', which is neither region nor interior.",
                pack.PackId,
                document.DocumentId));
            return null;
        }

        // An arrival point is found by its id — a start, a transition and a sold crossing all name one — so a
        // place declaring one id twice would answer every one of them with whichever came first, and the
        // author's other point would be dead. It is refused by name, as a place declared twice is. Ids that
        // differ only in case are one id, because that is how a point is looked up.
        List<PlaceEntryPoint> entryPoints = [];
        Dictionary<string, string> declared = new(PlaceDefinition.EntryPointIds);
        foreach (JsonElement point in entry.GetArray("entryPoints"))
        {
            string id = ContentEntry.ReadId(point, "id");
            if (id.Length == 0)
            {
                issues.Add(new ContentValidationIssue(
                    "entry-point-id-missing",
                    $"place '{entry.Id}' declares an arrival point with no id.",
                    pack.PackId,
                    document.DocumentId));
                continue;
            }

            if (!declared.TryAdd(id, id))
            {
                string spelled = string.Equals(declared[id], id, StringComparison.Ordinal)
                    ? string.Empty
                    : $" (as '{declared[id]}' and as '{id}', which name one point because arrival points are looked up without regard to case)";
                issues.Add(new ContentValidationIssue(
                    "entry-point-id-reused",
                    $"place '{entry.Id}' declares arrival point '{id}' more than once{spelled}.",
                    pack.PackId,
                    document.DocumentId));
                continue;
            }

            entryPoints.Add(new PlaceEntryPoint(id, ReadPose(
                point,
                ContentEntry.ReadDouble(point, "x") ?? 0,
                ContentEntry.ReadDouble(point, "y") ?? 0,
                ContentEntry.ReadDouble(point, "z") ?? 0)));
        }

        return new PlaceDefinition(
            new PlaceId(entry.Id),
            kind,
            entry.GetString("name") is { Length: > 0 } name ? name : entry.Id,
            entryPoints,
            entry);
    }

    private static PlaceTransition? ReadTransition(
        LoadedPack pack,
        ContentDocument document,
        ContentEntry entry,
        HashSet<PlaceId> knownPlaces,
        IFareDurationRule? fares,
        List<ContentValidationIssue> issues)
    {
        string toText = entry.GetId("toPlace");
        if (toText.Length == 0)
        {
            issues.Add(new ContentValidationIssue(
                "transition-destination-missing",
                $"transition '{entry.Id}' names no destination.",
                pack.PackId,
                document.DocumentId));
            return null;
        }

        PlaceId to = new(toText);
        if (!knownPlaces.Contains(to))
        {
            issues.Add(new ContentValidationIssue(
                "transition-destination-unknown",
                $"transition '{entry.Id}' goes to place '{to}', which no pack declares.",
                pack.PackId,
                document.DocumentId));
            return null;
        }

        PlaceId? from = null;
        string fromText = entry.GetId("fromPlace");
        if (fromText.Length > 0)
        {
            from = new PlaceId(fromText);
            if (!knownPlaces.Contains(from.Value))
            {
                issues.Add(new ContentValidationIssue(
                    "transition-origin-unknown",
                    $"transition '{entry.Id}' leaves place '{from}', which no pack declares.",
                    pack.PackId,
                    document.DocumentId));
                return null;
            }
        }

        // A named arrival point is checked when the graph resolves it, so the message can name the
        // destination place as well as the transition.
        PlaceArrival arrival = entry.GetId("entryPoint") is { Length: > 0 } entryPointId
            ? PlaceArrival.AtEntryPoint(entryPointId)
            : PlaceArrival.AtPose(ReadPose(
                entry.Payload,
                entry.GetDouble("x") ?? 0,
                entry.GetDouble("y") ?? 0,
                entry.GetDouble("z") ?? 0));

        // A transition a counter sells runs on a route, and the game's rule says how many days that route
        // takes: the ticket the counter writes carries the route, and the route and the destination together
        // are what tell one counter's journey from another counter's journey to the same place. A crossing
        // that states no fare is walked; a fare on no route, or on a route the rule does not time, is a defect
        // rather than a journey nobody can name.
        PlaceTransition transition = new(from, to, arrival, entry.Id);
        if (entry.GetBoolean(FareField) is true)
        {
            string route = entry.GetString(FareRouteField);
            if (route.Length == 0)
            {
                issues.Add(new ContentValidationIssue(
                    "transition-fare-route-missing",
                    $"transition '{entry.Id}' is sold as a passage and names no '{FareRouteField}', so nothing could say how long the journey takes.",
                    pack.PackId,
                    document.DocumentId));
                return null;
            }

            if (Timed(transition, route, fares) is not { } days)
            {
                issues.Add(Untimed(transition, route, fares, pack.PackId, document.DocumentId));
                return null;
            }

            transition = transition.AsFare(route, days);
        }

        return transition;
    }

    /// <summary>Checks and times one crossing the game's fare network sells, or names why it cannot be sold.</summary>
    /// <remarks>
    /// A network's crossing is held to what an authored fare is held to — known places, a route, and a length
    /// the rule states — and to the two things only a network could get wrong: it leaves a place, because a
    /// counter stands somewhere, and it states no days, because the duration rule is their one owner.
    /// </remarks>
    private static PlaceTransition? Sold(
        PlaceTransition journey,
        HashSet<PlaceId> knownPlaces,
        IFareDurationRule? fares,
        List<ContentValidationIssue> issues)
    {
        string? problem =
            journey.From is not { } from ? $"fare '{journey.Source}' is sold by no place, and a counter stands somewhere."
            : !knownPlaces.Contains(from) ? $"fare '{journey.Source}' leaves place '{from}', which no pack declares."
            : !knownPlaces.Contains(journey.To) ? $"fare '{journey.Source}' goes to place '{journey.To}', which no pack declares."
            : string.IsNullOrWhiteSpace(journey.FareRoute) ? $"fare '{journey.Source}' names no route, so nothing could say how long the journey takes."
            : journey.FareDays is not null ? $"fare '{journey.Source}' states its own days, and how long a route takes is the duration rule's answer alone."
            : null;
        if (problem is not null)
        {
            issues.Add(new ContentValidationIssue("transition-fare-invalid", problem, journey.Source));
            return null;
        }

        string route = journey.FareRoute!;
        if (Timed(journey, route, fares) is not { } days)
        {
            issues.Add(Untimed(journey, route, fares, journey.Source, documentId: null));
            return null;
        }

        return journey.AsFare(route, days);
    }

    /// <summary>How many days the rule states a sold crossing takes, or null when it states no positive number.</summary>
    private static int? Timed(PlaceTransition transition, string route, IFareDurationRule? fares) =>
        fares?.DaysOf(transition.From, transition.To, route) is { } days && days >= 1 ? days : null;

    /// <summary>The defect a sold crossing nobody can time is named by.</summary>
    private static ContentValidationIssue Untimed(PlaceTransition transition, string route, IFareDurationRule? fares, string packId, string? documentId) =>
        new(
            "transition-fare-days-unstated",
            fares is null
                ? $"transition '{transition.Source}' is sold as a passage on route '{route}' and this world was built with no rule for how long a sold journey takes, so no ticket could name it."
                : $"transition '{transition.Source}' is sold as a passage on route '{route}', which the game's rule states no positive whole number of days for, so no ticket could name it.",
            packId,
            documentId);

    private static void ValidateArrivals(PlaceGraph graph, List<ContentValidationIssue> issues)
    {
        foreach (PlaceTransition transition in graph.Transitions)
        {
            if (!transition.Arrival.IsEntryPoint) continue;
            string entryPointId = transition.Arrival.EntryPointId ?? string.Empty;
            PlaceDefinition destination = graph.Require(transition.To);
            if (destination.FindEntryPoint(entryPointId) is not null) continue;
            issues.Add(new ContentValidationIssue(
                "entry-point-unknown",
                $"transition '{transition.Source}' arrives at '{entryPointId}' in place '{destination.Id}', which has no such arrival point.",
                destination.Id.Value));
        }
    }

    private static PlacePose ReadPose(JsonElement element, double x, double y, double z) =>
        new(x, y, z, ContentEntry.ReadDouble(element, "yaw") ?? 0, ContentEntry.ReadDouble(element, "pitch") ?? 0);
}
