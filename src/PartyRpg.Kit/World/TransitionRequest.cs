namespace PartyRpg.Kit.World;

/// <summary>One transition a caller is about to take, together with where the party stands as it asks.</summary>
/// <remarks>
/// The request carries the graph the transition came from so a cost rule can price by destination — the
/// place's kind, the content it was read from — without being handed a second way to find it. It is also
/// the whole input to the one transition path: the departure check, the cost rule, and the arrival
/// resolution all read the same value, so a rule can never be quoted about a different transition than
/// the one that is taken, and the caller can never move a party it did not describe.
/// </remarks>
public sealed record TransitionRequest
{
    /// <summary>Creates a request to take one transition.</summary>
    /// <param name="graph">The graph the transition belongs to.</param>
    /// <param name="transition">The transition to take.</param>
    /// <param name="kind">What kind of travel the caller is taking.</param>
    /// <param name="partyPlace">The place the party is in now.</param>
    /// <param name="partyPose">The pose the party holds now, in <paramref name="partyPlace"/>.</param>
    /// <exception cref="ArgumentNullException">The graph or the transition is null.</exception>
    public TransitionRequest(
        PlaceGraph graph,
        PlaceTransition transition,
        TransitionKind kind,
        PlaceId partyPlace,
        PlacePose partyPose)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(transition);
        Graph = graph;
        Transition = transition;
        Kind = kind;
        PartyPlace = partyPlace;
        PartyPose = partyPose;
    }

    /// <summary>The graph the transition belongs to.</summary>
    public PlaceGraph Graph { get; }

    /// <summary>The transition to take.</summary>
    public PlaceTransition Transition { get; }

    /// <summary>The kind of travel the caller is taking.</summary>
    public TransitionKind Kind { get; }

    /// <summary>The place the party is in when it asks.</summary>
    public PlaceId PartyPlace { get; }

    /// <summary>The pose the party holds in <see cref="PartyPlace"/> when it asks.</summary>
    public PlacePose PartyPose { get; }
}
