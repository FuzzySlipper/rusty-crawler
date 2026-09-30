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
/// takes is not content at all: it is the game's <see cref="IFareDurationRule"/>, asked here per route.
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
    public static PlaceGraph Load(ContentCatalog catalog, IFareDurationRule? fares = null)
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

        List<PlaceTransition> transitions = [];
        foreach ((LoadedPack pack, ContentDocument document, ContentEntry entry) in catalog.Entries(TransitionDefinitionKind))
        {
            PlaceTransition? transition = ReadTransition(pack, document, entry, known, fares, issues);
            if (transition is not null) transitions.Add(transition);
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

        List<PlaceEntryPoint> entryPoints = [];
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
        // takes: the ticket the counter writes carries that number, and the number and the destination
        // together are what tell one counter's journey from another counter's journey to the same place. A
        // crossing that states no fare is walked; a fare on no route, or on a route the rule does not time, is
        // a defect rather than a journey nobody can name.
        int? fareDays = null;
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

            if (fares?.DaysOf(from, to, route) is not { } days || days < 1)
            {
                issues.Add(new ContentValidationIssue(
                    "transition-fare-days-unstated",
                    fares is null
                        ? $"transition '{entry.Id}' is sold as a passage on route '{route}' and this world was built with no rule for how long a sold journey takes, so no ticket could name it."
                        : $"transition '{entry.Id}' is sold as a passage on route '{route}', which the game's rule states no positive whole number of days for, so no ticket could name it.",
                    pack.PackId,
                    document.DocumentId));
                return null;
            }

            fareDays = days;
        }

        return new PlaceTransition(from, to, arrival, entry.Id) { FareDays = fareDays };
    }

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
