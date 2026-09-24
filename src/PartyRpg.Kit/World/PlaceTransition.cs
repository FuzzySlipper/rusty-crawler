namespace PartyRpg.Kit.World;

/// <summary>
/// One way out of a place: where it goes, where the party arrives, and what the transition is.
/// </summary>
/// <param name="From">The place the transition leaves, or null when the world itself issues it.</param>
/// <param name="To">The place the transition arrives at.</param>
/// <param name="Arrival">Where in the destination the party arrives.</param>
/// <param name="Source">What declared the transition, for diagnostics and for content that authors one.</param>
public sealed record PlaceTransition(PlaceId? From, PlaceId To, PlaceArrival Arrival, string Source)
{
    /// <summary>Whether the world issues this transition rather than a place.</summary>
    public bool IsWorldIssued => From is null;
}
