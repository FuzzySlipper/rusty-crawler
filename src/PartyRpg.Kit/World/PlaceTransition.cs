namespace PartyRpg.Kit.World;

/// <summary>
/// One way out of a place: where it goes, where the party arrives, and what the transition is.
/// </summary>
/// <param name="From">The place the transition leaves, or null when the world itself issues it.</param>
/// <param name="To">The place the transition arrives at.</param>
/// <param name="Arrival">Where in the destination the party arrives.</param>
/// <param name="Source">What declared the transition, for diagnostics and for content that authors one.</param>
/// <remarks>
/// <para>
/// A transition is a way between places and not a way of travelling between them: walking a road and buying
/// a seat on a coach can share one edge and still be different journeys. What a transition <em>is</em> is
/// therefore stated with it, and the one fact of that kind here is a fare: a crossing a counter sells as a
/// passage, on a named route.
/// </para>
/// <para>
/// The route is what makes a bought passage name exactly one journey: a region can keep two counters on
/// different routes that both reach the same place — a coach and a boat — and the ticket carries the route it
/// was sold on, so it tells one journey from the other without carrying a number a retune could change. The
/// days a fare takes are the game's rule's answer for that route (<see cref="IFareDurationRule"/>), asked when
/// the world graph is built over the tuning the session loaded, so the counter's offer and the road's charge
/// are one answer and a ticket bought under an older tuning is honoured at today's.
/// </para>
/// </remarks>
public sealed record PlaceTransition(PlaceId? From, PlaceId To, PlaceArrival Arrival, string Source)
{
    /// <summary>Whether the world issues this transition rather than a place.</summary>
    public bool IsWorldIssued => From is null;

    /// <summary>The route a counter sells this crossing on as a passage, or null when no counter sells it.</summary>
    public string? FareRoute { get; init; }

    /// <summary>
    /// How many game days this crossing takes when a counter sells it, as the game's rule times its route, or
    /// null when no counter sells it or it has not been timed yet.
    /// </summary>
    public int? FareDays { get; init; }

    /// <summary>Whether a counter sells this crossing as a passage rather than the party walking it.</summary>
    /// <remarks>
    /// A fare is not a walk-in and never becomes one: the party boards it at the counter that sells it, which
    /// is why a fare carries no reach and why this fact is separate from the kind of travel a caller takes.
    /// </remarks>
    public bool IsFare => FareRoute is not null;

    /// <summary>This transition as a journey a counter sells on a route, timed by the game's rule.</summary>
    /// <param name="route">The route the passage runs on, which the ticket carries.</param>
    /// <param name="days">How many game days the journey takes, which must be at least one.</param>
    /// <returns>The transition, with the fare stated.</returns>
    /// <exception cref="ArgumentException">The route is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The journey takes no time at all, which is not a journey.</exception>
    public PlaceTransition AsFare(string route, int days)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(route);
        ArgumentOutOfRangeException.ThrowIfLessThan(days, 1);
        return this with { FareRoute = route, FareDays = days };
    }

    /// <inheritdoc />
    public override string ToString() =>
        IsFare ? $"{Source} in {From} -> {To} (fare on {FareRoute}, {FareDays} day(s))" : $"{Source} in {From} -> {To}";
}
