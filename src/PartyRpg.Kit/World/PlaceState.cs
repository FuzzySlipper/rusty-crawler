namespace PartyRpg.Kit.World;

/// <summary>
/// What the world remembers about one place: everything the party has learned about it, and the state
/// of the population that lives there.
/// </summary>
/// <remarks>
/// Knowledge and population share a record because they are one place's state, but they move
/// independently: restoring a population replaces the population and leaves everything the party
/// learned untouched. Keeping them in one immutable record makes that independence explicit — a reset
/// produces a new state with three fields changed, and a reader holding an older state cannot watch it
/// change underneath.
/// </remarks>
/// <param name="Place">The place this is the state of.</param>
/// <param name="Visited">Whether the party has been in the place.</param>
/// <param name="Discovered">Whether the party knows the place exists, which visiting implies.</param>
/// <param name="Cleared">Whether the party has emptied the place and its population has not come back.</param>
/// <param name="RespawnCount">How many times the ledger has restored this place's population.</param>
/// <param name="LastResetDay">
/// The elapsed game day the population was last established, or null when it never was. It is null
/// rather than zero so the first day of a session is never confused with a place the party has yet to
/// enter, and it is a day rather than a timestamp because the per-place content states its interval in
/// whole game days.
/// </param>
public sealed record PlaceState(
    PlaceId Place,
    bool Visited,
    bool Discovered,
    bool Cleared,
    int RespawnCount,
    int? LastResetDay)
{
    /// <summary>
    /// The state of a place the party has never seen, whose population has never been established.
    /// </summary>
    /// <remarks>
    /// A place the ledger holds no state for reads as this rather than as an error: gameplay constantly
    /// asks about places the party has not been to, and "nothing has happened here" is the honest
    /// answer.
    /// </remarks>
    public static PlaceState Untouched(PlaceId place) => new(place, false, false, false, 0, null);
}
