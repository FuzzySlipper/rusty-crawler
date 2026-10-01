using System.Globalization;
using System.Text.Json;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.World;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>One step of a place's map event, as the importer normalized it.</summary>
/// <remarks>
/// The fields are the importer's own (<c>MightAndMagic7.Import.Packs.PlaceEventStep</c>): every step has its
/// number and its instruction's word, and only the fields its instruction carries are present — a field a step
/// does not carry reads as empty or zero here, and the step's word says which of them mean anything.
/// </remarks>
/// <param name="Step">The step's number, which a jump names.</param>
/// <param name="Op">The instruction's word, the donor's own name for it (<c>compare</c>, <c>add</c>, <c>status-text</c>).</param>
/// <param name="Variable">The variable family a comparison or assignment names, or empty.</param>
/// <param name="Which">The member of the family, or empty.</param>
/// <param name="Index">The slot of a numbered family, such as a map variable's.</param>
/// <param name="Value">The value compared with or written.</param>
/// <param name="Target">The step a comparison or jump goes to.</param>
/// <param name="Targets">The steps a random jump chooses among.</param>
/// <param name="Text">The line a status step prints.</param>
/// <param name="Who">Which characters a choice or a harm names.</param>
/// <param name="Member">The character's position when one is named.</param>
/// <param name="Kind">The kind of harm.</param>
/// <param name="Amount">How much harm.</param>
/// <param name="Period">A timer's period word.</param>
/// <param name="HalfMinutes">An interval timer's period in half minutes.</param>
internal sealed record MapEventStep(
    int Step,
    string Op,
    string Variable,
    string Which,
    int Index,
    int Value,
    int? Target,
    IReadOnlyList<int> Targets,
    string Text,
    string Who,
    int Member,
    string Kind,
    int Amount,
    string Period,
    int HalfMinutes)
{
    /// <summary>The id of the door a door step moves, as the place's door placement carries it.</summary>
    internal int Door { get; init; }

    /// <summary>What a door step does: <c>open</c>, <c>close</c> or <c>toggle</c>.</summary>
    internal string Action { get; init; } = string.Empty;

    /// <summary>The treasure level an item gift draws at.</summary>
    internal int Level { get; init; }

    /// <summary>The item tag by kind a gift's draw is narrowed to, or empty.</summary>
    internal string ItemKind { get; init; } = string.Empty;

    /// <summary>The item tag by skill a gift's draw is narrowed to, or empty.</summary>
    internal string ItemSkill { get; init; } = string.Empty;

    /// <summary>The item a gift gives outright, or zero when the draw stands.</summary>
    internal int Item { get; init; }

    /// <summary>The spell a cast step casts.</summary>
    internal int Spell { get; init; }

    /// <summary>The mastery a cast step casts at, or a skill jump waits for.</summary>
    internal string Mastery { get; init; } = string.Empty;

    /// <summary>The skill rank a cast step casts at, or a skill jump waits for.</summary>
    internal int Rank { get; init; }

    /// <summary>The person a conversation or topic step names.</summary>
    internal int Person { get; init; }

    /// <summary>The bit a flag step sets or clears.</summary>
    internal long Flag { get; init; }

    /// <summary>The event a topic step makes a person's slot raise, zero for none.</summary>
    internal int Raises { get; init; }

    /// <summary>The group of creatures a flag step names.</summary>
    internal int Group { get; init; }

    /// <summary>Whether a flag step sets its bit rather than clearing it.</summary>
    internal bool On { get; init; }

    /// <summary>The variable a step reads or writes, as one identity a timer and a fixture can share.</summary>
    /// <remarks>
    /// A numbered family's slot and a family whose value names the thing — a quest bit, a party bit, a note, an
    /// item — are part of the identity, so a timer that refills map variable 0 is the one a fixture reading map
    /// variable 0 waits on, and not the one refilling variable 1.
    /// </remarks>
    internal string VariableKey => Variable switch
    {
        "" => string.Empty,
        MightAndMagic7MapEvents.MapVariable => string.Create(CultureInfo.InvariantCulture, $"{Variable}:{Index}"),
        "quest-bit" or "member-bit" or "autonote" or "item" => string.Create(CultureInfo.InvariantCulture, $"{Variable}:{Value}"),
        _ => Which.Length > 0 ? $"{Variable}:{Which}" : Variable,
    };
}

/// <summary>One map event a place carries.</summary>
/// <param name="Place">The place whose program holds it.</param>
/// <param name="Id">The event's number in that program.</param>
/// <param name="Label">The hint it shows, which is what a fixture raising it is called; empty when it has none.</param>
/// <param name="Steps">Its steps, in program order.</param>
/// <param name="Raised">Whether something in the place raises it when used.</param>
internal sealed record MapEvent(PlaceId Place, int Id, string Label, IReadOnlyList<MapEventStep> Steps, bool Raised)
{
    /// <summary>The first step with a number, which is the one the donor runs (OpenEnroth <c>src/Engine/Evt/EvtInterpreter.cpp:140-146</c>).</summary>
    /// <param name="step">The step's number.</param>
    /// <returns>The step, or null when the event has none with that number, which ends a run.</returns>
    internal MapEventStep? At(int step)
    {
        foreach (MapEventStep candidate in Steps)
        {
            if (candidate.Step == step) return candidate;
        }

        return null;
    }

    /// <summary>The steps that start a timer's own steps.</summary>
    internal IEnumerable<MapEventStep> Timers => Steps.Where(step => MightAndMagic7MapEvents.IsTimer(step.Op));

    /// <summary>The steps a timer runs: the ones after it, up to the event's end or its next trigger.</summary>
    /// <param name="timer">The timer step.</param>
    internal IEnumerable<MapEventStep> BodyOf(MapEventStep timer)
    {
        bool after = false;
        foreach (MapEventStep step in Steps)
        {
            if (ReferenceEquals(step, timer))
            {
                after = true;
                continue;
            }

            if (!after) continue;
            if (MightAndMagic7MapEvents.IsTrigger(step.Op)) yield break;
            yield return step;
        }
    }
}

/// <summary>One row of the discovery table: a note a party can keep, and the table's category for it.</summary>
/// <param name="Number">The note's number, which an event step names.</param>
/// <param name="Text">The note as the table words it.</param>
/// <param name="Category">The table's category word: <c>stat</c>, <c>obelisk</c>, <c>potion</c>, <c>teacher</c>, <c>misc</c>.</param>
internal readonly record struct Discovery(int Number, string Text, string Category);

/// <summary>One line of the history table: what the party's history book says when an event writes the slot.</summary>
/// <param name="Slot">The slot a step's <c>history</c> variable names.</param>
/// <param name="Text">
/// The line as the importer normalized it: the table's words with <c>{date}</c> where the day it was written
/// goes and <c>{member:1}</c> to <c>{member:4}</c> where a character's name goes.
/// </param>
/// <param name="Title">The page title the table gives it, or empty.</param>
internal readonly record struct HistoryLine(int Slot, string Text, string Title);

/// <summary>
/// The map events this game's places carry, the timers that keep what their fixtures give, and the discovery
/// table their notes are rows of — read once from content.
/// </summary>
/// <remarks>
/// <para>
/// <b>What content carries.</b> The importer writes each place's fixture events and timed events as
/// <c>place-event</c> entries — the steps of the event in the donor's own instruction words, the variables
/// named by family, the text a step prints already resolved — and the discovery table as <c>discovery</c>
/// entries. Nothing in either says what a step does in play: that is <see cref="MightAndMagic7Fixtures"/>'s,
/// which interprets the steps this game's fixtures use and refuses every other one by name.
/// </para>
/// <para>
/// <b>A content defect fails the load.</b> A step with no number or no word, a discovery with no text, and two
/// entries for one event are defects of the pack that declared them, named all at once while the world is built
/// rather than met by a party at a well.
/// </para>
/// </remarks>
internal sealed class MightAndMagic7MapEvents
{
    /// <summary>The definition kind a place's own map event is declared under.</summary>
    internal const string PlaceEventDefinitionKind = "place-event";

    /// <summary>The definition kind a row of the discovery table is declared under.</summary>
    internal const string DiscoveryDefinitionKind = "discovery";

    /// <summary>The definition kind a line of the history table is declared under.</summary>
    internal const string HistoryDefinitionKind = "history-line";

    /// <summary>The variable family a place's own persistent counters are named by.</summary>
    internal const string MapVariable = "map-variable";

    private readonly Dictionary<(string Place, int Event), MapEvent> _events;
    private readonly Dictionary<string, List<MapEvent>> _timedByPlace;
    private readonly Dictionary<int, Discovery> _discoveries;
    private readonly Dictionary<int, HistoryLine> _history;

    private MightAndMagic7MapEvents(
        Dictionary<(string Place, int Event), MapEvent> events,
        Dictionary<int, Discovery> discoveries,
        Dictionary<int, HistoryLine>? history = null)
    {
        _events = events;
        _discoveries = discoveries;
        _history = history ?? [];
        _timedByPlace = [];
        foreach (MapEvent mapEvent in events.Values.OrderBy(mapEvent => mapEvent.Place.Value, StringComparer.Ordinal).ThenBy(mapEvent => mapEvent.Id))
        {
            if (!mapEvent.Timers.Any()) continue;
            if (!_timedByPlace.TryGetValue(mapEvent.Place.Value, out List<MapEvent>? list)) _timedByPlace[mapEvent.Place.Value] = list = [];
            list.Add(mapEvent);
        }
    }

    /// <summary>A world that carries no map events and no discovery table.</summary>
    internal static MightAndMagic7MapEvents None { get; } = new([], []);

    /// <summary>How many events the content carries.</summary>
    internal int Count => _events.Count;

    /// <summary>How many rows the discovery table holds.</summary>
    internal int DiscoveryCount => _discoveries.Count;

    /// <summary>Every event the content carries, by place and number.</summary>
    internal IEnumerable<MapEvent> Events => _events.Values;

    /// <summary>Whether a step's word is one of the triggers that start steps the map runs rather than a use.</summary>
    /// <remarks>
    /// The donor ends a run when it meets one (OpenEnroth <c>src/Engine/Evt/EvtInterpreter.cpp:425-448</c>):
    /// what follows a trigger is run by the map — on a timer, on load, on leaving — not by the party's use.
    /// </remarks>
    /// <param name="op">The step's word.</param>
    internal static bool IsTrigger(string op) => op is "on-timer" or "on-long-timer" or "on-map-reload" or "on-map-leave" or "on-date-timer";

    /// <summary>Whether a step's word is a timer trigger.</summary>
    /// <param name="op">The step's word.</param>
    internal static bool IsTimer(string op) => op is "on-timer" or "on-long-timer";

    /// <summary>The event a place's program holds under a number, or null when the content carries none.</summary>
    /// <param name="place">The place.</param>
    /// <param name="eventId">The event's number.</param>
    internal MapEvent? Find(PlaceId place, int eventId) =>
        _events.GetValueOrDefault((place.Value, eventId));

    /// <summary>The discovery a number names, or null when the table holds none.</summary>
    /// <param name="number">The note's number.</param>
    internal Discovery? DiscoveryOf(int number) =>
        _discoveries.TryGetValue(number, out Discovery discovery) ? discovery : null;

    /// <summary>The history line a slot names, or null when the table holds none for it.</summary>
    /// <param name="slot">The slot a step's <c>history</c> variable names.</param>
    internal HistoryLine? HistoryOf(int slot) =>
        _history.TryGetValue(slot, out HistoryLine line) ? line : null;

    /// <summary>How many lines the history table holds.</summary>
    internal int HistoryCount => _history.Count;

    /// <summary>
    /// The timers of a place that keep what one of its events reads: every timer whose own steps write a
    /// variable the event compares.
    /// </summary>
    /// <remarks>
    /// The donor runs a map's timers on the map's own clock whether or not anybody uses anything
    /// (OpenEnroth <c>src/Engine/Evt/Processor.cpp:89-140</c>). This game runs a timer when a fixture whose
    /// event waits on it is used — the only moment what the timer kept can be read — so a well that a timer of
    /// another event refills is refilled as the party walks up to it, and a timer whose steps nothing reads is
    /// never run.
    /// </remarks>
    /// <param name="mapEvent">The event.</param>
    /// <returns>Each timer and the event that holds it, in place order.</returns>
    internal IReadOnlyList<(MapEvent Owner, MapEventStep Timer)> TimersFor(MapEvent mapEvent)
    {
        ArgumentNullException.ThrowIfNull(mapEvent);
        HashSet<string> read = [.. mapEvent.Steps.Where(step => step.Op == "compare").Select(step => step.VariableKey)];
        read.Remove(string.Empty);
        if (read.Count == 0 || !_timedByPlace.TryGetValue(mapEvent.Place.Value, out List<MapEvent>? timed)) return [];

        List<(MapEvent, MapEventStep)> timers = [];
        foreach (MapEvent owner in timed)
        {
            foreach (MapEventStep timer in owner.Timers)
            {
                if (owner.BodyOf(timer).Any(step => step.Op is "add" or "subtract" or "set" && read.Contains(step.VariableKey)))
                {
                    timers.Add((owner, timer));
                }
            }
        }

        return timers;
    }

    /// <summary>Reads the map events and the discovery table the loaded content carries.</summary>
    /// <param name="catalog">The validated content, when any loaded.</param>
    /// <returns>The reading; <see cref="None"/> for a product without content.</returns>
    /// <exception cref="ContentValidationException">A step, an event, or a discovery cannot be read.</exception>
    internal static MightAndMagic7MapEvents Read(ContentCatalog? catalog)
    {
        if (catalog is null) return None;
        List<ContentValidationIssue> issues = [];
        Dictionary<(string Place, int Event), MapEvent> events = [];
        foreach ((LoadedPack pack, ContentDocument document, ContentEntry entry) in catalog.Entries(PlaceEventDefinitionKind))
        {
            string place = entry.GetId("place");
            int? number = entry.GetInt32("event");
            if (place.Length == 0 || number is not { } eventId)
            {
                issues.Add(Issue("map-event-unplaced", $"map event '{entry.Id}' names no place or no event number, so no fixture could raise it.", pack, document));
                continue;
            }

            List<MapEventStep> steps = [];
            foreach (JsonElement element in entry.GetArray("steps"))
            {
                string op = ContentEntry.ReadString(element, "op");
                if (op.Length == 0 || ContentEntry.ReadDouble(element, "step") is not { } stepNumber)
                {
                    issues.Add(Issue("map-event-step-unnamed", $"map event '{entry.Id}' holds a step with no number or no instruction word, so a run could not say what it does.", pack, document));
                    continue;
                }

                steps.Add(new MapEventStep(
                    (int)stepNumber,
                    op,
                    ContentEntry.ReadString(element, "variable"),
                    ContentEntry.ReadString(element, "which"),
                    Whole(element, "index"),
                    Whole(element, "value"),
                    ContentEntry.ReadDouble(element, "target") is { } target ? (int)target : null,
                    Targets(element),
                    ContentEntry.ReadString(element, "text"),
                    ContentEntry.ReadString(element, "who"),
                    Whole(element, "member"),
                    ContentEntry.ReadString(element, "kind"),
                    Whole(element, "amount"),
                    ContentEntry.ReadString(element, "period"),
                    Whole(element, "halfMinutes"))
                {
                    Door = Whole(element, "door"),
                    Action = ContentEntry.ReadString(element, "action"),
                    Level = Whole(element, "level"),
                    ItemKind = ContentEntry.ReadString(element, "itemKind"),
                    ItemSkill = ContentEntry.ReadString(element, "itemSkill"),
                    Item = Whole(element, "item"),
                    Spell = Whole(element, "spell"),
                    Mastery = ContentEntry.ReadString(element, "mastery"),
                    Rank = Whole(element, "rank"),
                    Person = Whole(element, "person"),
                    Flag = ContentEntry.ReadDouble(element, "flag") is { } flag ? (long)flag : 0,
                    Raises = Whole(element, "raises"),
                    Group = Whole(element, "group"),
                    On = element.TryGetProperty("on", out JsonElement on) && on.ValueKind == JsonValueKind.True,
                });
            }

            MapEvent mapEvent = new(new PlaceId(place), eventId, entry.GetString("label"), steps, entry.GetBoolean("raised") ?? false);
            if (!events.TryAdd((place, eventId), mapEvent))
            {
                issues.Add(Issue("map-event-duplicated", $"place '{place}' carries event {eventId} twice, so which steps a fixture raising it runs would be a coin toss.", pack, document));
            }
        }

        Dictionary<int, Discovery> discoveries = [];
        foreach ((LoadedPack pack, ContentDocument document, ContentEntry entry) in catalog.Entries(DiscoveryDefinitionKind))
        {
            string text = entry.GetString("text");
            if (!int.TryParse(entry.Id, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) || text.Length == 0)
            {
                issues.Add(Issue("discovery-unreadable", $"discovery '{entry.Id}' is not a numbered row with a note, so a step setting it would write nothing a party could read.", pack, document));
                continue;
            }

            if (!discoveries.TryAdd(number, new Discovery(number, text, entry.GetString("category").ToLowerInvariant())))
            {
                issues.Add(Issue("discovery-duplicated", $"discovery {number} is declared twice, so which note it writes would be a coin toss.", pack, document));
            }
        }

        Dictionary<int, HistoryLine> history = [];
        foreach ((LoadedPack pack, ContentDocument document, ContentEntry entry) in catalog.Entries(HistoryDefinitionKind))
        {
            string text = entry.GetString("text");
            if (!int.TryParse(entry.Id, NumberStyles.None, CultureInfo.InvariantCulture, out int slot) || text.Length == 0)
            {
                issues.Add(Issue("history-line-unreadable", $"history line '{entry.Id}' is not a numbered slot with a line, so a step writing it would write nothing a party could read.", pack, document));
                continue;
            }

            if (!history.TryAdd(slot, new HistoryLine(slot, text, entry.GetString("title"))))
            {
                issues.Add(Issue("history-line-duplicated", $"history line {slot} is declared twice, so which line a step writes would be a coin toss.", pack, document));
            }
        }

        if (issues.Count > 0)
        {
            throw new ContentValidationException($"The places' map events cannot be read: {issues[0].Message}", issues);
        }

        return new MightAndMagic7MapEvents(events, discoveries, history);
    }

    private static int Whole(JsonElement element, string property) =>
        ContentEntry.ReadDouble(element, property) is { } value && value >= int.MinValue && value <= int.MaxValue ? (int)value : 0;

    private static IReadOnlyList<int> Targets(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty("targets", out JsonElement targets) ||
            targets.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        List<int> read = [];
        foreach (JsonElement target in targets.EnumerateArray())
        {
            if (target.ValueKind == JsonValueKind.Number && target.TryGetInt32(out int step)) read.Add(step);
        }

        return read;
    }

    private static ContentValidationIssue Issue(string code, string message, LoadedPack pack, ContentDocument document) =>
        new(code, message, pack.PackId, document.DocumentId);
}
