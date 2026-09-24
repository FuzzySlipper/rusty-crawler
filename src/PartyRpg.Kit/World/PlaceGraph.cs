using PartyRpg.Kit.Content;

namespace PartyRpg.Kit.World;

/// <summary>
/// The world as a graph of places and the transitions between them, loaded from content.
/// </summary>
/// <remarks>
/// One mechanism covers every place: an outdoor region connected at its edge and an interior reached
/// through a door differ in their map data, not in how the party moves between them. Nothing here knows
/// a place by name, and nothing special-cases a place; a place that needs different behavior needs a
/// content-driven rule, not an entry in this class.
/// </remarks>
public sealed class PlaceGraph
{
    private readonly Dictionary<PlaceId, PlaceDefinition> _places;
    private readonly Dictionary<PlaceId, List<PlaceTransition>> _outbound;
    private readonly List<PlaceTransition> _worldIssued;

    private PlaceGraph(
        IReadOnlyList<PlaceDefinition> places,
        IReadOnlyList<PlaceTransition> transitions,
        IReadOnlyList<PlaceTransition> worldIssued,
        Dictionary<PlaceId, List<PlaceTransition>> outbound)
    {
        Places = places;
        Transitions = transitions;
        _worldIssued = [.. worldIssued];
        _outbound = outbound;
        _places = places.ToDictionary(place => place.Id);
    }

    /// <summary>Every place, in the order content declared them.</summary>
    public IReadOnlyList<PlaceDefinition> Places { get; }

    /// <summary>Every transition the graph holds, world-issued ones included.</summary>
    public IReadOnlyList<PlaceTransition> Transitions { get; }

    /// <summary>The transitions that no place issues, such as a scripted arrival at the start of a game.</summary>
    public IReadOnlyList<PlaceTransition> WorldIssued => _worldIssued;

    /// <summary>Finds a place by id.</summary>
    public PlaceDefinition? Find(PlaceId id) => _places.GetValueOrDefault(id);

    /// <summary>Finds a place by id and fails with a message naming it when the graph has none.</summary>
    public PlaceDefinition Require(PlaceId id) =>
        Find(id) ?? throw new ContentValidationException(
            $"There is no place '{id}' in the world.",
            [new ContentValidationIssue("place-unknown", $"There is no place '{id}' in the world.", id.Value)]);

    /// <summary>Where the party can go from a place, in content order.</summary>
    public IReadOnlyList<PlaceTransition> TransitionsFrom(PlaceId from) =>
        _outbound.TryGetValue(from, out List<PlaceTransition>? transitions) ? transitions : [];

    /// <summary>
    /// The pose a transition arrives at, resolving a named arrival against the destination place.
    /// </summary>
    /// <remarks>
    /// A transition that names an arrival point the destination does not have fails here rather than
    /// being rounded to the destination's origin: that failure is a content defect, and a party that
    /// silently lands somewhere else is far harder to diagnose than a load that refuses.
    /// </remarks>
    public PlacePose ResolveArrival(PlaceTransition transition)
    {
        ArgumentNullException.ThrowIfNull(transition);
        if (transition.Arrival.Pose is { } pose) return pose;
        PlaceDefinition destination = Require(transition.To);
        string entryPointId = transition.Arrival.EntryPointId ?? string.Empty;
        PlaceEntryPoint? entryPoint = destination.FindEntryPoint(entryPointId);
        if (entryPoint is null)
        {
            throw new ContentValidationException(
                $"Transition '{transition.Source}' arrives at '{entryPointId}' in place '{destination.Id}', which has no such arrival point.",
                [
                    new ContentValidationIssue(
                        "entry-point-unknown",
                        $"Transition '{transition.Source}' arrives at '{entryPointId}' in place '{destination.Id}', which has no such arrival point.",
                        destination.Id.Value),
                ]);
        }

        return entryPoint.Pose;
    }

    /// <summary>Creates a graph from places and transitions that a loader has already validated.</summary>
    public static PlaceGraph From(IReadOnlyList<PlaceDefinition> places, IReadOnlyList<PlaceTransition> transitions)
    {
        ArgumentNullException.ThrowIfNull(places);
        ArgumentNullException.ThrowIfNull(transitions);
        Dictionary<PlaceId, List<PlaceTransition>> outbound = [];
        List<PlaceTransition> worldIssued = [];
        foreach (PlaceTransition transition in transitions)
        {
            if (transition.From is not { } from)
            {
                worldIssued.Add(transition);
                continue;
            }

            if (!outbound.TryGetValue(from, out List<PlaceTransition>? list))
            {
                list = [];
                outbound[from] = list;
            }

            list.Add(transition);
        }

        return new PlaceGraph(places, transitions, worldIssued, outbound);
    }
}
