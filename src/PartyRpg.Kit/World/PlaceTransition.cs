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
/// therefore content's to state, and the one fact of that kind here is a fare: a crossing a counter sells
/// as a passage.
/// </para>
/// <para>
/// The days a fare takes are content's own statement of the journey, written where the passage is sold and
/// again on the transition it is taken over. They are read here because they are what makes a bought
/// passage name exactly one journey: a region can keep two counters that both reach the same place — a
/// coach and a boat — and the ticket's own days are what tells the counter's journey from the other's.
/// </para>
/// </remarks>
public sealed record PlaceTransition(PlaceId? From, PlaceId To, PlaceArrival Arrival, string Source)
{
    /// <summary>Whether the world issues this transition rather than a place.</summary>
    public bool IsWorldIssued => From is null;

    /// <summary>
    /// How many game days this crossing takes when a counter sells it as a passage, or null when no counter
    /// sells it.
    /// </summary>
    public int? FareDays { get; init; }

    /// <summary>Whether a counter sells this crossing as a passage rather than the party walking it.</summary>
    /// <remarks>
    /// A fare is not a walk-in and never becomes one: the party boards it at the counter that sells it, which
    /// is why a fare carries no reach and why this fact is separate from the kind of travel a caller takes.
    /// </remarks>
    public bool IsFare => FareDays is not null;

    /// <summary>This transition as a journey a counter sells, for content that authors one.</summary>
    /// <param name="days">How many game days the journey takes, which must be at least one.</param>
    /// <returns>The transition, with the fare stated.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The journey takes no time at all, which is not a journey.</exception>
    public PlaceTransition AsFare(int days)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(days, 1);
        return this with { FareDays = days };
    }

    /// <inheritdoc />
    public override string ToString() =>
        IsFare ? $"{Source} in {From} -> {To} (fare, {FareDays} day(s))" : $"{Source} in {From} -> {To}";
}
