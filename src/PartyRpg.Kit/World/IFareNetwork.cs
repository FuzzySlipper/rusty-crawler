namespace PartyRpg.Kit.World;

/// <summary>Which journeys a game's counters sell, as the game's own policy decides it over the places loaded.</summary>
/// <remarks>
/// <para>
/// Content states which counters sell passages and where they stand; which destinations each one sells is a
/// rule — every coach town reaching every other, a port reaching only its neighbours — and that rule belongs to
/// the game's ruleset rather than to whatever wrote the content. The world graph asks it once, after the places
/// are read, and every crossing it answers with is a fare like one content authors: timed by the
/// <see cref="IFareDurationRule"/>, checked against the places, and boarded through the one transition path.
/// </para>
/// <para>
/// A crossing it returns leaves a place (a counter stands somewhere), names its route through
/// <see cref="PlaceTransition.FareRoute"/>, and leaves its days unset: how long a route takes is the duration
/// rule's single answer, so the network cannot state a second one.
/// </para>
/// </remarks>
public interface IFareNetwork
{
    /// <summary>Every crossing the game's counters sell between the places the world holds.</summary>
    /// <param name="places">The places the world graph was read with, which is what a crossing may join.</param>
    /// <returns>The sold crossings, each naming its route and no days, in a deterministic order.</returns>
    IReadOnlyList<PlaceTransition> Journeys(IReadOnlyList<PlaceDefinition> places);
}
