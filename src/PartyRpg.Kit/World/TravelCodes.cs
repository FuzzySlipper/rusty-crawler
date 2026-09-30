namespace PartyRpg.Kit.World;

/// <summary>The codes a transition is refused with.</summary>
/// <remarks>
/// A code is what a caller and a test branch on and the message beside it is what a person reads, so every
/// refusal this mechanism can give is named here once rather than spelled at the point of use.
/// </remarks>
public static class TravelCodes
{
    /// <summary>The refusal code <c>place-ground-refused</c>.</summary>
    public const string PlaceGroundRefused = "place-ground-refused";

    /// <summary>The refusal code <c>place-refused-arrival</c>.</summary>
    public const string PlaceRefusedArrival = "place-refused-arrival";

    /// <summary>The refusal code <c>travel-fare-ambiguous</c>.</summary>
    public const string TravelFareAmbiguous = "travel-fare-ambiguous";

    /// <summary>The refusal code <c>travel-fare-unrouted</c>.</summary>
    public const string TravelFareUnrouted = "travel-fare-unrouted";

    /// <summary>The refusal code <c>travel-fare-unstated</c>.</summary>
    public const string TravelFareUnstated = "travel-fare-unstated";
}
