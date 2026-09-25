using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Time;

/// <summary>One place's hours, as content states them: the place, and the window it keeps.</summary>
/// <remarks>
/// A place keeps hours when its buildings do: the counter inside it is open at the hours its definition
/// states, and the doors of the building are locked outside them. The pair is content identity and a window,
/// never a second kind of schedule, so a town, a shop, and a guild hall are all answered by the same table.
/// </remarks>
/// <param name="Place">The place whose doors and counters keep these hours.</param>
/// <param name="Hours">The window the place is open in.</param>
public sealed record PlaceHours(PlaceId Place, OpeningHours Hours);

/// <summary>
/// Which places are clocked, and when their doors stand open.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is a book of clock reads, not a second clock.</b> Every question is answered against a point on
/// the calendar the caller supplies — the session's one clock's own <see cref="GameClock.Now"/> — and the
/// only thing kept here is content's answer about which place keeps which window. A place that states no
/// hours is open at every hour, because nothing has said otherwise: inventing a default window would lock
/// doors content never closed.
/// </para>
/// <para>
/// A door outside its place's hours is refused by the interaction mechanism, not hidden by a screen: the
/// ruleset reads this table when it describes the door and states the unmet requirement, so what a player
/// sees is the sentence naming the hours rather than a menu entry that politely disappeared. The same read
/// answers the panel, so a shut shop and a screen that forgot to redraw cannot disagree.
/// </para>
/// <para>
/// The next change is stated as a point on the calendar, so a schedule is a game-time deadline: a caller can
/// say when a door reopens, and a test can hold it to the hour rather than to the number of updates that
/// happened to pass.
/// </para>
/// </remarks>
public sealed class PlaceSchedule
{
    private readonly Dictionary<PlaceId, OpeningHours> _hours;

    /// <summary>Creates a schedule from the places content clocks.</summary>
    /// <param name="places">The places that keep hours, each at most once.</param>
    /// <exception cref="ArgumentNullException">No places were supplied.</exception>
    /// <exception cref="ArgumentException">A place is listed twice, which would give it two windows.</exception>
    public PlaceSchedule(IEnumerable<PlaceHours> places)
    {
        ArgumentNullException.ThrowIfNull(places);
        _hours = [];
        foreach (PlaceHours place in places)
        {
            if (!_hours.TryAdd(place.Place, place.Hours))
            {
                throw new ArgumentException(
                    $"Place '{place.Place}' keeps hours more than once, so when its doors stand open would be ambiguous.",
                    nameof(places));
            }
        }
    }

    /// <summary>A schedule that clocks nothing: every place is open at every hour.</summary>
    public static PlaceSchedule Empty { get; } = new(Array.Empty<PlaceHours>());

    /// <summary>How many places keep hours.</summary>
    public int Count => _hours.Count;

    /// <summary>The window a place keeps, or null when it keeps none.</summary>
    /// <remarks>
    /// A place that keeps no hours answers null rather than a window of no hours: the two are different
    /// facts, and a schedule that handed back a default would clock every place in the world at midnight.
    /// </remarks>
    /// <param name="place">The place to ask about.</param>
    public OpeningHours? HoursOf(PlaceId place) =>
        _hours.TryGetValue(place, out OpeningHours hours) ? hours : null;

    /// <summary>Whether a place's doors stand open at a point on the calendar.</summary>
    /// <remarks>
    /// A place that keeps no hours is open: a schedule states when something is shut, and a place nothing
    /// has clocked is not shut at any hour of the day.
    /// </remarks>
    /// <param name="place">The place to ask about.</param>
    /// <param name="at">The date and time to judge.</param>
    public bool IsOpenAt(PlaceId place, GameDate at) =>
        _hours.TryGetValue(place, out OpeningHours hours) ? hours.IsOpenAt(at) : true;

    /// <summary>
    /// How a place reads at a point on the calendar: <c>open</c>, <c>closed</c>, or empty when it keeps no
    /// hours at all.
    /// </summary>
    /// <remarks>
    /// A place that keeps no hours reads as empty rather than as open, because the two are different facts
    /// about the world: a panel that showed a clocked shop at midnight and an unclocked ruin the same way
    /// would hide which buildings the game keeps hours for.
    /// </remarks>
    /// <param name="place">The place to ask about.</param>
    /// <param name="at">The date and time to judge.</param>
    public string StateOf(PlaceId place, GameDate at) =>
        _hours.TryGetValue(place, out OpeningHours hours) ? hours.IsOpenAt(at) ? "open" : "closed" : string.Empty;

    /// <summary>
    /// The point on the calendar a place's state next changes at, or null when it keeps no hours or the
    /// calendar holds no such day.
    /// </summary>
    /// <param name="place">The place to ask about.</param>
    /// <param name="at">The point on the calendar to look forward from.</param>
    /// <param name="calendar">The calendar the point is stated against.</param>
    /// <exception cref="ArgumentNullException">No calendar was supplied.</exception>
    public GameDate? NextChangeAfter(PlaceId place, GameDate at, GameCalendar calendar) =>
        _hours.TryGetValue(place, out OpeningHours hours) ? hours.NextChangeAfter(calendar, at) : null;
}
