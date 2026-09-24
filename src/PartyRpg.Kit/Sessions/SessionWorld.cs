using PartyRpg.Kit.Party;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Sessions;

/// <summary>
/// A source of elapsed game time for the world's own bookkeeping.
/// </summary>
/// <remarks>
/// The clock and the calendar belong to the party foundation, which is the next stone; this seam exists
/// so the world can be told what day it is without growing a second time source of its own. Until a
/// clock is wired, the world simply does not advance, and nothing pretends it has.
/// </remarks>
public interface IWorldTimeSource
{
    /// <summary>Whole game days elapsed since the session began, day zero being its first day.</summary>
    int ElapsedGameDays { get; }
}

/// <summary>
/// The live world inside a session: where the party is, what state each place is in, and the one path
/// that moves the party between places.
/// </summary>
/// <remarks>
/// Every mechanism here is the general one: a place is a place, and travel is travel, whether the party
/// walked to a region's edge, stepped through a door, or arrived by a scripted move at the start of the
/// game. Arriving and travelling both mark the place visited, so knowledge accrues the same way
/// wherever the party goes.
/// </remarks>
public sealed class SessionWorld
{
    private readonly TransitionExecutive _transitions;
    private readonly IWorldTimeSource? _time;

    /// <summary>Creates the world a session steps.</summary>
    /// <param name="graph">The places and the transitions between them.</param>
    /// <param name="party">The party's one position and facing.</param>
    /// <param name="places">Per-place runtime state.</param>
    /// <param name="costRule">The rule every transition is quoted through.</param>
    /// <param name="time">Where elapsed game days come from, when a clock has been wired.</param>
    public SessionWorld(
        PlaceGraph graph,
        PartyPoseOwner party,
        PlaceStateLedger places,
        ITravelCostRule costRule,
        IWorldTimeSource? time = null)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(places);
        _transitions = new TransitionExecutive(costRule);
        _time = time;
        Graph = graph;
        Party = party;
        Places = places;
        Places.MarkVisited(party.Place);
    }

    /// <summary>The places and the transitions between them.</summary>
    public PlaceGraph Graph { get; }

    /// <summary>The party's one position and facing.</summary>
    public PartyPoseOwner Party { get; }

    /// <summary>Per-place runtime state.</summary>
    public PlaceStateLedger Places { get; }

    /// <summary>The place the party is in.</summary>
    public PlaceId Place => Party.Place;

    /// <summary>
    /// Takes a transition, or refuses it. Arriving moves the party and marks the destination visited;
    /// a refusal leaves the party exactly where it was.
    /// </summary>
    public TransitionResult Travel(PlaceTransition transition, TransitionKind kind)
    {
        ArgumentNullException.ThrowIfNull(transition);
        TransitionResult result = _transitions.Take(new TransitionRequest(Graph, transition, kind, Party.Place, Party.PlacePose));
        if (!result.Arrived) return result;

        // An arrival the place will not admit is a refusal, not a half-done move: the party stays put and
        // the cost the rule quoted is never applied, because the caller applies a cost only on arrival.
        try
        {
            Party.Enter(result.Place, result.Pose);
        }
        catch (ArgumentException error)
        {
            return TransitionResult.Refused(kind, Party.Place, Party.PlacePose, new TravelRefusal(
                "place-refused-arrival",
                $"The destination {result.Place} refused the arrival: {error.Message}"));
        }

        Places.MarkVisited(result.Place);
        return result;
    }

    /// <summary>
    /// Puts the party where a scenario starts it.
    /// </summary>
    /// <remarks>
    /// Starting is a placement, not travel: there is no place to leave and no journey to charge for, so
    /// this does not go through the transition path and no cost is quoted. Every move *between* places
    /// does, including the world-issued transitions content declares — those are taken with
    /// <see cref="Travel"/> like any other.
    /// </remarks>
    public void ArriveAt(PlaceId place, PlacePose pose)
    {
        Party.Enter(place, pose);
        Places.MarkVisited(place);
    }

    /// <summary>
    /// Advances the world to the day its time source reports, returning the places whose population was
    /// restored. Without a time source the world does not advance, and says so by doing nothing.
    /// </summary>
    public IReadOnlyList<PlaceState> AdvanceTime() =>
        _time is null ? [] : Places.AdvanceTo(_time.ElapsedGameDays);

    /// <summary>What the panel shows about the world.</summary>
    public WorldSnapshot Snapshot
    {
        get
        {
            PlaceDefinition place = Graph.Require(Party.Place);
            return new WorldSnapshot(
                place.Id.Value,
                place.Name,
                SessionProjection.WireName(place.Kind),
                Party.PlacePose,
                Places.States.Count(state => state.Visited),
                Graph.Places.Count);
        }
    }
}
