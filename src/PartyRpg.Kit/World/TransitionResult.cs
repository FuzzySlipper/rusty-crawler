namespace PartyRpg.Kit.World;

/// <summary>Where the party ended up after one transition, and what the transition charged it.</summary>
/// <remarks>
/// A result always says which place the party is in and which pose it holds, whether it travelled or not:
/// a refusal leaves the party exactly where it stood, so a caller can assign the result unconditionally
/// and never has to guess the party's position from the branch it took. Nothing here is remembered between
/// calls, which is what makes out-and-back a pair of identical calls rather than a special case.
/// </remarks>
public sealed record TransitionResult
{
    private TransitionResult(
        bool arrived,
        TransitionKind kind,
        PlaceId place,
        PlacePose pose,
        TravelCost chargedCost,
        TravelRefusal? refusal)
    {
        Arrived = arrived;
        Kind = kind;
        Place = place;
        Pose = pose;
        ChargedCost = chargedCost;
        Refusal = refusal;
    }

    /// <summary>The party arrived at the transition's destination with the pose the destination resolved to.</summary>
    /// <param name="kind">The kind of travel that was taken.</param>
    /// <param name="place">The place the party is now in.</param>
    /// <param name="pose">The pose the party holds in that place.</param>
    /// <param name="chargedCost">What the cost rule quoted and the transition charged.</param>
    public static TransitionResult Arrival(TransitionKind kind, PlaceId place, PlacePose pose, TravelCost chargedCost) =>
        new(true, kind, place, pose, chargedCost, null);

    /// <summary>The transition did not happen: the party still holds the place and pose it held.</summary>
    /// <param name="kind">The kind of travel that was refused.</param>
    /// <param name="place">The place the party never left.</param>
    /// <param name="pose">The pose the party still holds in that place.</param>
    /// <param name="refusal">Why the transition was refused.</param>
    /// <exception cref="ArgumentNullException">The refusal is null.</exception>
    public static TransitionResult Refused(TransitionKind kind, PlaceId place, PlacePose pose, TravelRefusal refusal) =>
        new(false, kind, place, pose, TravelCost.Free, refusal ?? throw new ArgumentNullException(nameof(refusal)));

    /// <summary>Whether the transition happened.</summary>
    public bool Arrived { get; }

    /// <summary>The kind of travel the caller asked for.</summary>
    public TransitionKind Kind { get; }

    /// <summary>The place the party is in after the call: the destination on arrival, and the place it never left on a refusal.</summary>
    public PlaceId Place { get; }

    /// <summary>The pose the party holds after the call.</summary>
    public PlacePose Pose { get; }

    /// <summary>What the transition charged: the rule's quote on arrival, and nothing on a refusal.</summary>
    public TravelCost ChargedCost { get; }

    /// <summary>The named reason the transition did not happen, or null when the party arrived.</summary>
    public TravelRefusal? Refusal { get; }
}
