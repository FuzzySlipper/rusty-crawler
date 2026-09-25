using System.Globalization;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Services;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// Which places in this game are clocked, and what their doors keep: the schedule a door is locked by.
/// </summary>
/// <remarks>
/// <para>
/// <b>The hours are content's, read once.</b> A place keeps hours when its buildings do: the shipped
/// building table states an opening and a closing hour for every shop, guild, temple, tavern, bank, hall,
/// and stable it recognizes, and those hours reach a place through the counters standing in it. A place may
/// also state its own hours in its own entry, which is how a town — a region holding no counter at all —
/// can be clocked. Nothing here invents a window: a place content clocks nothing in is open at every hour,
/// and its doors are never locked.
/// </para>
/// <para>
/// <b>What a disagreement means.</b> A place whose buildings keep different hours has no single door
/// schedule, and this does not guess one: the place keeps no hours and the read reports the disagreement, so
/// the loss is visible — the doors of such a place simply never shut — rather than a door that locks at an
/// hour one of its two shops did not state. A place that wants one window states it in its own entry.
/// </para>
/// <para>
/// The reader is handed the game's own service policy rather than reading service definitions again, so a
/// counter's hours and its door's hours are one reading of one placement and cannot drift apart.
/// </para>
/// </remarks>
internal sealed class MightAndMagic7Schedules
{
    /// <summary>
    /// The requirement name a place's hours are asked for under, as a use's time-of-day requirement.
    /// </summary>
    /// <remarks>
    /// The requirement vocabulary's time-of-day kind already exists, and the two words content states in it
    /// are <c>day</c> and <c>night</c> — the clock's own halves. This word is the third reading of the same
    /// kind: the place's own hours, which a door's lock is stated as. The ruleset resolves the name, which
    /// is why the kit needs no vocabulary for it.
    /// </remarks>
    internal const string OpenRequirementName = "open";

    /// <summary>The place-entry field that states the hour a place opens.</summary>
    internal const string OpenHourField = "openHour";

    /// <summary>The place-entry field that states the hour a place closes, where 24 is the end of the day.</summary>
    internal const string ClosedHourField = "closedHour";

    private readonly List<string> _notes;

    private MightAndMagic7Schedules(PlaceSchedule schedule, List<string> notes)
    {
        Schedule = schedule;
        _notes = notes;
    }

    /// <summary>Which places are clocked, and when their doors stand open.</summary>
    internal PlaceSchedule Schedule { get; }

    /// <summary>
    /// What the read could not clock, in the order it found it: a loss worth reporting rather than a load
    /// worth refusing, because a place whose doors never shut is playable and a world that refuses to load
    /// is not.
    /// </summary>
    internal IReadOnlyList<string> Notes => _notes;

    /// <summary>Reads every place's hours out of the content the world is being built from.</summary>
    /// <param name="catalog">The validated content the product loaded, when it loaded any.</param>
    /// <param name="graph">The places of that content, which is what the read walks.</param>
    /// <param name="services">
    /// This game's service policy, which answers what a placement keeps; a placement keeping a counter whose
    /// hours are stated is what clocks the place it stands in. Without it only a place that states its own
    /// hours is clocked.
    /// </param>
    /// <returns>The clocks of this world, and what could not be clocked.</returns>
    /// <exception cref="ContentValidationException">A place states hours it cannot keep.</exception>
    internal static MightAndMagic7Schedules Read(ContentCatalog? catalog, PlaceGraph graph, IServiceRule? services)
    {
        ArgumentNullException.ThrowIfNull(graph);
        if (catalog is null) return new MightAndMagic7Schedules(PlaceSchedule.Empty, []);
        List<ContentValidationIssue> issues = [];
        List<PlaceHours> clocked = [];
        List<string> notes = [];
        PlacePopulationContent placements = PlacePopulationContent.Read(graph);
        Dictionary<PlaceId, PlaceDefinition> places = graph.Places.ToDictionary(place => place.Id);

        // The read walks the catalog rather than the graph so a defect can name the pack and document it is
        // in, the way every other content defect does; the graph is what says what each place is.
        foreach ((LoadedPack pack, ContentDocument document, ContentEntry entry) in catalog.Entries(PlaceGraphLoader.PlaceDefinitionKind))
        {
            if (!places.TryGetValue(new PlaceId(entry.Id), out PlaceDefinition? place)) continue;
            if (Stated(place, pack, document, issues) is { } stated)
            {
                clocked.Add(new PlaceHours(place.Id, stated));
                continue;
            }

            if (services is null) continue;
            (bool Found, bool Agreed, OpeningHours Window) kept = Kept(place, placements.PlacementsOf(place.Id), services);
            if (!kept.Found) continue;
            if (kept.Agreed)
            {
                clocked.Add(new PlaceHours(place.Id, kept.Window));
                continue;
            }

            notes.Add(
                $"place '{place.Id}' ({place.Name}) keeps no door schedule: the counters standing in it keep different hours, so no one window says when its doors are locked. A place that wants one states its own openHour and closedHour.");
        }

        if (issues.Count > 0)
        {
            throw new ContentValidationException(
                $"This game's schedules cannot be read: {issues[0].Message}",
                issues);
        }

        return new MightAndMagic7Schedules(new PlaceSchedule(clocked), notes);
    }

    /// <summary>Whether a place's doors stand open at a point on the calendar.</summary>
    /// <param name="place">The place to ask about.</param>
    /// <param name="at">The date and time to judge.</param>
    internal bool IsOpenAt(PlaceId place, GameDate at) => Schedule.IsOpenAt(place, at);

    /// <summary>The window a place keeps, or null when it keeps none.</summary>
    /// <param name="place">The place to ask about.</param>
    internal OpeningHours? HoursOf(PlaceId place) => Schedule.HoursOf(place);

    /// <summary>Reads the hours a place states in its own entry, or null when it states none.</summary>
    private static OpeningHours? Stated(
        PlaceDefinition place,
        LoadedPack pack,
        ContentDocument document,
        List<ContentValidationIssue> issues)
    {
        int? open = place.Source.GetInt32(OpenHourField);
        int? closed = place.Source.GetInt32(ClosedHourField);
        if (open is null && closed is null) return null;
        if (open is not { } openHour || closed is not { } closedHour)
        {
            issues.Add(new ContentValidationIssue(
                "place-hours-incomplete",
                $"place '{place.Id}' states one of its opening and closing hours and not the other, so when its doors stand open cannot be known.",
                pack.PackId,
                document.DocumentId));
            return null;
        }

        try
        {
            return new OpeningHours(openHour, closedHour);
        }
        catch (ArgumentOutOfRangeException error)
        {
            issues.Add(new ContentValidationIssue(
                "place-hours-invalid",
                $"place '{place.Id}' states hours it cannot keep: {error.Message}",
                pack.PackId,
                document.DocumentId));
            return null;
        }
    }

    /// <summary>
    /// What the counters in a place keep: whether the place holds one at all, whether they agree, and the
    /// window they share.
    /// </summary>
    /// <remarks>
    /// The answer is three-valued on purpose. A place with no counter at all and a place whose counters
    /// disagree are different facts — the first is a place content clocks nothing in, the second is a place
    /// whose content contradicts itself — and only the caller knows what to say about each.
    /// </remarks>
    private static (bool Found, bool Agreed, OpeningHours Window) Kept(
        PlaceDefinition place,
        IReadOnlyList<PlacementDefinition> placements,
        IServiceRule services)
    {
        bool found = false;
        OpeningHours window = default;
        foreach (PlacementDefinition placement in placements)
        {
            if (services.Describe(new ServiceTargetRequest(place.Id, placement)) is not { Hours: { } hours }) continue;
            if (!found)
            {
                found = true;
                window = hours.Window;
                continue;
            }

            if (window != hours.Window) return (true, false, default);
        }

        return (found, true, window);
    }

    /// <summary>How a place's hours read to a person, or empty when it keeps none.</summary>
    internal string DescribeHours(PlaceId place) =>
        HoursOf(place) is { } hours ? hours.ToString() : string.Empty;

    /// <summary>When a place's doors next open or close, or empty when it keeps no hours or no such day exists.</summary>
    /// <param name="place">The place to ask about.</param>
    /// <param name="clock">The session's one clock, which the point is stated against.</param>
    internal string DescribeNextChange(PlaceId place, GameClock? clock)
    {
        if (clock is null || Schedule.NextChangeAfter(place, clock.Now, clock.Calendar) is not { } change) return string.Empty;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{change.Year:0000}-{change.Month:00}-{change.Day:00} {change.Hour:00}:{change.Minute:00}");
    }
}
