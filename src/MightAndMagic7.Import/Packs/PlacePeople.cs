using System.Globalization;
using MightAndMagic7.Import.Maps;
using MightAndMagic7.Import.Tables;

namespace MightAndMagic7.Import.Packs;

/// <summary>One thing a person can be asked about, as the content pack carries it.</summary>
/// <remarks>
/// The line is the first text the topic table names, and the count is how many the table names altogether:
/// the original picks between them by the state of its event programs, and a reader that was handed one
/// line as if it were the whole answer could not tell a plain line from one of eight. The requirement
/// column travels beside them exactly as the table writes it.
/// </remarks>
/// <param name="Id">The topic's identity in content, which a conversation command names.</param>
/// <param name="Label">The topic as a person reads it.</param>
/// <param name="Requires">The table's own requirement column; zero when the topic requires nothing.</param>
/// <param name="Text">
/// The first line the topic's texts hold, which is what the person says; empty when the table names none and the
/// global program's event of the topic's number speaks instead.
/// </param>
/// <param name="TextCount">How many lines the topic table names for this topic.</param>
public sealed record PlacePersonTopic(string Id, string Label, int Requires, string Text, int TextCount)
{
    /// <summary>
    /// The global program's event the topic raises when it is chosen — the event of the topic's own number
    /// (OpenEnroth <c>src/GUI/UI/NPCTopics.cpp:546-559</c>, <c>:662-666</c>) — or null when the program holds none.
    /// </summary>
    public int? Event { get; init; }
}

/// <summary>One person the content carries: who they are and everything they can say.</summary>
/// <remarks>
/// The person is the NPC table's own row, kept whole enough to hold a conversation: the name the game
/// displays, the portrait it draws, the two greetings the greeting table gives them, the building the table
/// places them in, and every topic the topic table assigns to them with its answer text resolved. A person
/// is one entry however many places they stand in, which is why the placements name them rather than the
/// other way round.
/// </remarks>
/// <param name="Id">The person's identity in content, which a placement names.</param>
/// <param name="NpcId">The NPC table row the person is, so a reader can follow the entry back to the table.</param>
/// <param name="Name">The name the game displays.</param>
/// <param name="Portrait">The portrait the table names, as the table writes it.</param>
/// <param name="Greeting">What the person says when met, empty when the table gives none.</param>
/// <param name="GreetingAgain">What the person says on later meetings, empty when the table gives none.</param>
/// <param name="House">The building the table places them in, zero when it places them nowhere.</param>
/// <param name="TopicSlots">
/// The six dialogue event columns of the row by position, a zero where a slot raises nothing: the event a slot
/// raises is a topic table row (OpenEnroth <c>src/GUI/UI/NPCTopics.cpp:546-559</c>), and a map event changes
/// it by the slot's position (<c>src/Engine/Evt/EvtInterpreter.cpp:449-468</c>).
/// </param>
/// <param name="CanJoin">Whether the table says this person may join the party.</param>
/// <param name="Topics">Every topic the person owns, in table order.</param>
/// <param name="SourceRow">The row's position in the NPC table, so a reader can follow an entry back to it.</param>
public sealed record PlacePerson(
    string Id,
    int NpcId,
    string Name,
    string Portrait,
    string Greeting,
    string GreetingAgain,
    int House,
    IReadOnlyList<int> TopicSlots,
    bool CanJoin,
    IReadOnlyList<PlacePersonTopic> Topics,
    int SourceRow)
{
    /// <summary>How many dialogue event numbers the row states, which is what the original scripts behind their replies.</summary>
    public int DialogueEvents => TopicSlots.Count(slot => slot != 0);
}

/// <summary>One person standing in a place at a position of their own.</summary>
/// <remarks>
/// These are the delta's own people: an actor record whose identity names an NPC row, standing where the
/// record puts them. They are placements of their own because the map states where they are, which is what
/// makes a person on a road different from a person behind a door.
/// </remarks>
/// <param name="PlaceId">The place the person stands in.</param>
/// <param name="PlacementId">The placement's identity within the place.</param>
/// <param name="PersonId">The person the placement holds.</param>
/// <param name="X">Where the person stands along the place's first axis.</param>
/// <param name="Y">Where the person stands along the place's second axis.</param>
/// <param name="Z">Where the person stands in height.</param>
/// <param name="Yaw">Which way the person faces, in the game's own angle units.</param>
/// <param name="SourceActorIndex">The actor record's index in the delta's own array.</param>
/// <param name="SourceActorName">The name the actor record carries, empty when it states none.</param>
/// <param name="MonsterId">
/// The monster row the record's own monster info names, which is what this person is as far as a fight is
/// concerned; zero when the record states none.
/// </param>
/// <param name="Group">
/// The actor record's own group, zero when it belongs to none: what a map event that turns a group hostile or
/// counts a group's dead names it by (OpenEnroth <c>src/Engine/Objects/Actor.cpp:3823-3851</c> and
/// <c>2849-2863</c>).
/// </param>
/// <param name="Attributes">
/// The actor record's attribute bits as the record stores them, which carry its standing toward the party: the
/// aggressor bit <c>0x80000</c> makes the actor the party's enemy whatever its kind (OpenEnroth
/// <c>src/Engine/Objects/ActorEnums.h:109</c>, <c>src/Engine/Objects/Actor.cpp:2154-2155</c>).
/// </param>
/// <param name="HostilityGroup">
/// The kind the record says the person counts as toward everyone else, as the record stores it: zero is its own
/// row's kind, 9999 the party's own, anything else a kind's index in the hostility matrix
/// (<c>src/Engine/Snapshots/EntitySnapshots.cpp:1494-1499</c>).
/// </param>
public sealed record PlacePersonPlacement(
    int PlaceId,
    string PlacementId,
    string PersonId,
    double X,
    double Y,
    double Z,
    double Yaw,
    int SourceActorIndex,
    string SourceActorName,
    int MonsterId,
    int Group = 0,
    int Attributes = 0,
    int HostilityGroup = 0);

/// <summary>Everybody a building holds, by the NPC table's own placement column.</summary>
/// <param name="BuildingId">The building's id, which is the row the table places people in.</param>
/// <param name="PlaceId">The place the building stands in, zero when the import placed no door for it.</param>
/// <param name="PersonIds">The people the table places in the building, in table order.</param>
/// <param name="PlacementId">The placement the people are written into, empty when nothing was placed for the building.</param>
public sealed record PlaceHousehold(int BuildingId, int PlaceId, IReadOnlyList<string> PersonIds, string PlacementId);

/// <summary>One person, topic, or placement the import could not carry, with the reason.</summary>
/// <param name="Code">A short stable code for the kind of refusal.</param>
/// <param name="Subject">What the refusal is about, so a reader can find it.</param>
/// <param name="Reason">Why nothing was placed, in terms a person can act on.</param>
public sealed record PlacePeopleRefusal(string Code, string Subject, string Reason);

/// <summary>What one import's people derivation produced.</summary>
/// <param name="People">Every person the content carries, in table order.</param>
/// <param name="Placements">Everybody standing where a map puts them.</param>
/// <param name="Households">Everybody a building holds, whether or not the building was placed.</param>
/// <param name="Refusals">Everything nothing was placed for, with its reason.</param>
/// <param name="Notes">What the read noticed about the tables, for a report.</param>
public sealed record PlacePeopleSummary(
    IReadOnlyList<PlacePerson> People,
    IReadOnlyList<PlacePersonPlacement> Placements,
    IReadOnlyList<PlaceHousehold> Households,
    IReadOnlyList<PlacePeopleRefusal> Refusals,
    IReadOnlyList<string> Notes)
{
    /// <summary>
    /// Every row of the topic table that has something to say, whoever owns it: what a person's slot can be made to
    /// raise by a map event, which may be a topic the table gives nobody.
    /// </summary>
    public IReadOnlyList<PlacePersonTopic> Topics { get; init; } = [];

    /// <summary>An import that derived no people, such as one that read no tables.</summary>
    public static PlacePeopleSummary Empty { get; } = new([], [], [], [], []);

    /// <summary>How many people the content carries.</summary>
    public int PersonCount => People.Count;

    /// <summary>How many people stand at a position a map states for them.</summary>
    public int PlacementCount => Placements.Count;

    /// <summary>How many distinct people stand at a position of their own.</summary>
    public int PlacedPersonCount => Placements.Select(placement => placement.PersonId).Distinct().Count();

    /// <summary>How many people a building holds.</summary>
    public int ResidentCount => Households.Sum(household => household.PersonIds.Count);

    /// <summary>How many buildings hold somebody.</summary>
    public int HouseholdCount => Households.Count(household => household.PersonIds.Count > 0);

    /// <summary>How many of those buildings the import placed a door for, which is what makes them reachable.</summary>
    public int ReachableHouseholdCount => Households.Count(household => household.PersonIds.Count > 0 && household.PlacementId.Length > 0);

    /// <summary>How many people are placed in a building the import placed nothing for.</summary>
    public int UnreachableResidentCount =>
        Households.Where(household => household.PlacementId.Length == 0).Sum(household => household.PersonIds.Count);

    /// <summary>How many topics the people carry.</summary>
    public int TopicCount => People.Sum(person => person.Topics.Count);

    /// <summary>How many topics name a requirement, which is an errand the party must have finished.</summary>
    public int GatedTopicCount => People.Sum(person => person.Topics.Count(topic => topic.Requires != 0));

    /// <summary>How many topics the table gives in more than one version.</summary>
    public int BranchedTopicCount => People.Sum(person => person.Topics.Count(topic => topic.TextCount > 1));

    /// <summary>How many people the table gives a greeting.</summary>
    public int GreetedCount => People.Count(person => person.Greeting.Length > 0);

    /// <summary>How many people the table scripts dialogue events for.</summary>
    public int ScriptedCount => People.Count(person => person.DialogueEvents > 0);

    /// <summary>How many things nothing was placed for.</summary>
    public int RefusalCount => Refusals.Count;
}

/// <summary>
/// Turns the game's people tables and the maps' own actor records into the people a place holds.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two sources, one person per row.</b> The NPC table says who exists, what they say when met, and what
/// they can be asked about; the building table's own placement column says which building each of them is
/// in; and a map's actor records say who is standing in the open where no building is. The people are
/// emitted once, as entries a placement names, because one person can stand in more than one place and a
/// copy per placement would be that many people.
/// </para>
/// <para>
/// <b>A topic without a text is not a topic.</b> The topic table carries rows whose text column is empty,
/// which are the original's own script stubs — something the event program raises rather than something
/// anybody says. They are refused rather than emitted with no answer, because a topic a person cannot
/// answer is a choice that does nothing.
/// </para>
/// <para>
/// <b>What the table states is what travels.</b> A line's other versions, a requirement column, a dialogue
/// event number, and a person's own notes are carried so a report can state them; none of them is
/// interpreted here, because what a requirement means and what an event would do are the runtime ruleset's
/// and the event interpreter's business rather than a pack's.
/// </para>
/// </remarks>
public static class PlacePeopleEmitter
{
    /// <summary>The definition kind a person entry is declared under.</summary>
    public const string PersonDefinitionKind = "person";

    /// <summary>The placement kind somebody standing in a place at their own position stands under.</summary>
    public const string PersonPlacementKind = "person";

    /// <summary>The field a house placement names the people who live in it under.</summary>
    public const string PlacementPeopleField = "people";

    /// <summary>The prefix a person entry's identity carries, so an identity says what it is.</summary>
    public const string PersonIdPrefix = "npc-";

    /// <summary>The prefix a topic's identity carries.</summary>
    public const string TopicIdPrefix = "topic-";

    /// <summary>Derives every person, every topic, and every placement that holds one.</summary>
    /// <param name="people">The tables the game's people are read from.</param>
    /// <param name="maps">The decoded maps, keyed by the place id the map table gives them.</param>
    /// <param name="services">What the building table's own emission produced, which is where a building's placement is.</param>
    /// <param name="globalEvents">
    /// The numbers of the global program's events (<see cref="GlobalEventEmitter"/>): a topic of such a number raises
    /// the event when chosen, so it is something the person says even when the topic table names no text for it.
    /// </param>
    /// <exception cref="ArgumentNullException">A required input is null.</exception>
    public static PlacePeopleSummary Emit(
        PersonTable people,
        IReadOnlyDictionary<int, DecodedMap> maps,
        PlaceServiceSummary services,
        IReadOnlySet<int>? globalEvents = null)
    {
        ArgumentNullException.ThrowIfNull(people);
        ArgumentNullException.ThrowIfNull(maps);
        ArgumentNullException.ThrowIfNull(services);
        IReadOnlySet<int> events = globalEvents ?? new HashSet<int>();
        Dictionary<int, NpcTopicRecord> rows = [];
        foreach (NpcTopicRecord row in people.Topics) rows.TryAdd(row.Id, row);

        List<PlacePeopleRefusal> refusals = [];
        List<string> notes = [.. people.Notes];
        List<PlacePerson> entries = [];
        Dictionary<int, PlacePerson> byNpc = [];

        foreach (NpcRecord npc in people.Npcs)
        {
            string id = $"{PersonIdPrefix}{npc.Id.ToString(CultureInfo.InvariantCulture)}";
            NpcGreeting? greeting = npc.GreetingIndex == 0 ? null : people.Greeting(npc.GreetingIndex);
            if (npc.GreetingIndex != 0 && greeting is null)
            {
                notes.Add($"npcdata row {npc.Id} ('{npc.Name}') uses greeting {npc.GreetingIndex}, which npcgreet does not carry, so they are greeted with this game's own words.");
            }

            // What a person can be asked about is what their slots raise: the row of each slot's number is offered under
            // its label and its event runs when it is chosen (OpenEnroth src/GUI/UI/NPCTopics.cpp:546-559, :593-619,
            // :662-666). A row the topic table's owner column gives the person and no slot names is not offered until a
            // map event changes a slot to it, which the conversation reads from the whole table written below.
            List<PlacePersonTopic> topics = [];
            foreach (int slot in npc.DialogueSlots)
            {
                if (slot == 0 || topics.Any(topic => topic.Id == TopicId(slot))) continue;
                if (!rows.TryGetValue(slot, out NpcTopicRecord? row))
                {
                    refusals.Add(new PlacePeopleRefusal(
                        "slot-without-a-topic",
                        string.Create(CultureInfo.InvariantCulture, $"npcdata row {npc.Id} ('{npc.Name}') slot raising {slot}"),
                        "The topic table has no row of the slot's number, so there is no label to offer it under."));
                    continue;
                }

                if (Topic(people, row, events) is not { } said)
                {
                    refusals.Add(new PlacePeopleRefusal(
                        "topic-without-text",
                        $"npctopic row {row.Id} ('{row.Label}') for {npc.Name}",
                        "The topic's text column names no text this import read and the global program holds no event of its number, so there is nothing this person says to it."));
                    continue;
                }

                topics.Add(said);
            }

            PlacePerson person = new(
                id,
                npc.Id,
                npc.Name,
                npc.Portrait == 0 ? string.Empty : npc.Portrait.ToString(CultureInfo.InvariantCulture),
                greeting?.First ?? string.Empty,
                greeting?.Again ?? string.Empty,
                npc.House,
                npc.DialogueSlots,
                npc.CanJoin,
                topics,
                npc.Id);
            entries.Add(person);
            byNpc[npc.Id] = person;
        }

        List<PlacePersonPlacement> placements = [];
        foreach ((int placeId, DecodedMap map) in maps.OrderBy(entry => entry.Key))
        {
            if (map.Delta is not { } delta) continue;
            foreach (MapActor actor in delta.Actors)
            {
                if (!actor.IsPerson) continue;
                if (!byNpc.TryGetValue(actor.NpcId, out PlacePerson? person))
                {
                    refusals.Add(new PlacePeopleRefusal(
                        "actor-without-a-person",
                        $"place {placeId} ('{map.FileName}') actor {actor.Index}",
                        $"The actor's record names NPC row {actor.NpcId}, which the NPC table does not carry, so there is nobody to place where it stands."));
                    continue;
                }

                placements.Add(new PlacePersonPlacement(
                    placeId,
                    $"{PersonPlacementKind}-{actor.Index.ToString(CultureInfo.InvariantCulture)}",
                    person.Id,
                    actor.Position.X,
                    actor.Position.Y,
                    actor.Position.Z,
                    actor.YawAngle,
                    actor.Index,
                    actor.Name,
                    actor.MonsterId,
                    actor.Group,
                    actor.Attributes,
                    actor.HostilityGroup));
            }
        }

        // A building's people are the NPC rows whose own placement column names it. The building's door is
        // what makes them reachable, and it comes from the counters the building table's emission placed,
        // so a building nothing was placed for keeps its people and is reported as unreachable rather than
        // being dropped: the fix is a map face or a table row, not a shorter list.
        Dictionary<int, (int Place, string Placement)> houses = [];
        foreach (PlaceServicePlacement counter in services.Placements)
        {
            houses[counter.BuildingId] = (counter.PlaceId, $"{counter.PlacementKind}-{counter.BuildingId.ToString(CultureInfo.InvariantCulture)}");
        }

        List<PlaceHousehold> households = [];
        foreach (IGrouping<int, NpcRecord> group in people.Npcs
            .Where(npc => npc.House != 0)
            .GroupBy(npc => npc.House)
            .OrderBy(group => group.Key))
        {
            IReadOnlyList<string> ids = [.. group.Select(npc => $"{PersonIdPrefix}{npc.Id.ToString(CultureInfo.InvariantCulture)}")];
            (int placeId, string placementId) = houses.TryGetValue(group.Key, out (int Place, string Placement) house)
                ? house
                : (0, string.Empty);
            if (placementId.Length == 0)
            {
                refusals.Add(new PlacePeopleRefusal(
                    "house-without-a-door",
                    $"building {group.Key} ({ids.Count} people)",
                    "No face of the building's map raises the event that opens it, so the import placed no counter or household for it and nobody there can be reached."));
            }

            households.Add(new PlaceHousehold(group.Key, placeId, ids, placementId));
        }

        // The whole topic table, every row with something to say, is what a slot can be changed to raise: a map
        // event names the row by number, and the row may belong to nobody the owner column names.
        List<PlacePersonTopic> table = [];
        foreach (NpcTopicRecord topic in people.Topics)
        {
            if (Topic(people, topic, events) is { } said) table.Add(said);
        }

        return new PlacePeopleSummary(entries, placements, households, refusals, notes) { Topics = table };
    }

    /// <summary>The identity a topic of a row's number carries.</summary>
    private static string TopicId(int row) => $"{TopicIdPrefix}{row.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>
    /// A topic row as somebody says it: with its first text, and with the global event of its number when the program
    /// holds one; null when it has neither, which is a row nothing could answer.
    /// </summary>
    private static PlacePersonTopic? Topic(PersonTable people, NpcTopicRecord topic, IReadOnlySet<int> events)
    {
        string? text = FirstText(people, topic);
        bool raises = events.Contains(topic.Id);
        if (text is null && !raises) return null;
        return new PlacePersonTopic(TopicId(topic.Id), topic.Label, topic.Requires, text ?? string.Empty, topic.TextIds.Count)
        {
            Event = raises ? topic.Id : null,
        };
    }

    /// <summary>The first line a topic's texts hold, or null when none of them resolves.</summary>
    /// <remarks>
    /// The first is taken because the table lists the versions in the order the original tries them, and a
    /// reader is told how many there were rather than being handed one as if it were all of them.
    /// </remarks>
    private static string? FirstText(PersonTable people, NpcTopicRecord topic)
    {
        foreach (int id in topic.TextIds)
        {
            if (people.Text(id) is { } text && text.Text.Length > 0) return text.Text;
        }

        return null;
    }
}
