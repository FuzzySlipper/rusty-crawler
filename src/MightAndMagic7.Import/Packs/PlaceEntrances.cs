using System.Globalization;
using MightAndMagic7.Import.Events;
using MightAndMagic7.Import.Maps;
using MightAndMagic7.Import.World;

namespace MightAndMagic7.Import.Packs;

/// <summary>
/// One reach a party walks into to tread on a pressure plate: where in the place the plate is, how far its reach
/// extends, which map face it came from, and the floor trigger it raises.
/// </summary>
/// <remarks>
/// The position and the radius are both derived from one plate rather than authored: the position is the face's
/// own centroid and the radius is how far the face's own corners reach from it. That keeps the whole reach a fact
/// of the map data — it is the smallest ball holding the face the donor hangs the event on — and it says where it
/// came from, so a report can follow the reach back to a model and a face index rather than to a number this
/// importer chose.
/// </remarks>
/// <param name="FromPlace">The place the plate lies in.</param>
/// <param name="EventId">The event the plate raises.</param>
/// <param name="Links">The travel links the event's moves take, in link order; a run takes at most one of them.</param>
/// <param name="X">The plate's centroid along the place's first axis.</param>
/// <param name="Y">The plate's centroid along the place's second axis.</param>
/// <param name="Z">The plate's centroid in height.</param>
/// <param name="Radius">How far the plate's corners reach from its centroid.</param>
/// <param name="SourceFaceIndex">The face's index in the decoded map's own face list.</param>
/// <param name="SourceModelIndex">The owning model's index, or -1 for an interior face, which has none.</param>
/// <param name="SourceModelName">The owning model's name, empty for an interior face.</param>
/// <param name="Attributes">The face's attribute word, kept raw so a report can state what the donor does with it.</param>
public sealed record PlaceEntrancePlacement(
    int FromPlace,
    int EventId,
    IReadOnlyList<int> Links,
    double X,
    double Y,
    double Z,
    double Radius,
    int SourceFaceIndex,
    int SourceModelIndex,
    string SourceModelName,
    uint Attributes)
{
    /// <summary>The floor-trigger placement the reach raises, which the event's run belongs to.</summary>
    public string Raises => PlaceFloorTrigger.PlacementIdOf(EventId);
}

/// <summary>What the shipped data makes of one travel link: how a party takes it, and under what condition.</summary>
/// <param name="LinkIndex">The travel link's own entry id in the pack.</param>
/// <param name="FromPlace">The place the link leaves, absent for a link the world issues.</param>
/// <param name="ToPlace">The place the link arrives at.</param>
/// <param name="EventId">The event the link's move belongs to.</param>
/// <param name="Step">The move's step inside that event.</param>
/// <param name="Disposition">
/// How the link is taken: <c>used</c> (a clicked face group or a decoration raises the event, and using it runs the
/// event), <c>walked</c> (a pressure plate raises it, and walking onto the plate runs it), <c>used-or-walked</c>
/// (both), <c>counter</c> (the event opens a building, whose counter owns the face), <c>world-issued</c> (the
/// global program moves the party from no place), or <c>unreachable</c>.
/// </param>
/// <param name="Trigger">What raises the event in the source place, in words; empty for a link the world issues.</param>
/// <param name="Condition">What must hold for a run of the event to reach this move, empty when every run does.</param>
/// <param name="Detail">The evidence the disposition rests on, in terms a person can follow to the data.</param>
public sealed record PlaceLinkAccount(
    int LinkIndex,
    int? FromPlace,
    int ToPlace,
    int EventId,
    int Step,
    string Disposition,
    string Trigger,
    string Condition,
    string Detail)
{
    /// <summary>Whether a run reaches the move only when a condition holds.</summary>
    public bool IsConditional => Condition.Length > 0;
}

/// <summary>What one import's travel derivation produced: the plates a party treads on, and every link's account.</summary>
/// <param name="Entrances">Every plate reach, in place, event and face order.</param>
/// <param name="Accounts">Every travel link the graph carries, in link order, with its disposition.</param>
public sealed record PlaceEntranceSummary(
    IReadOnlyList<PlaceEntrancePlacement> Entrances,
    IReadOnlyList<PlaceLinkAccount> Accounts)
{
    /// <summary>An import that derived nothing, such as one that decoded no maps.</summary>
    public static PlaceEntranceSummary Empty { get; } = new([], []);

    /// <summary>How many places carry a plate a party treads on to travel.</summary>
    public int PlaceCount => Entrances.Select(entrance => entrance.FromPlace).Distinct().Count();

    /// <summary>How many plate reaches were derived.</summary>
    public int ReachCount => Entrances.Count;

    /// <summary>How many links have each disposition, by its word.</summary>
    public IReadOnlyDictionary<string, int> Dispositions =>
        Accounts.GroupBy(account => account.Disposition, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

    /// <summary>How many links a run reaches only when a condition holds.</summary>
    public int ConditionalCount => Accounts.Count(account => account.IsConditional);

    /// <summary>How many links a party can take in play by using or treading on what raises them.</summary>
    public int TakenCount => Accounts.Count(account => account.Disposition is PlaceEntranceEmitter.Used or PlaceEntranceEmitter.Walked or PlaceEntranceEmitter.UsedOrWalked);
}

/// <summary>
/// Derives the plates a party treads on to travel, and accounts for every travel link the place graph carries.
/// </summary>
/// <remarks>
/// <para>
/// <b>How the donor raises a travel event.</b> A map hands an event to a face or a decoration: the donor runs it
/// when the party clicks a face carrying the clickable attribute or a decoration
/// (OpenEnroth <c>src/Engine/Graphics/Indoor.cpp:1397-1416</c>, <c>src/Engine/Graphics/Viewport.cpp:201-213</c>),
/// and when the party steps on a face carrying the pressure-plate attribute
/// (<c>src/Engine/Graphics/Outdoor.cpp:966-980</c>, <c>Indoor.cpp:1491</c>). A travel link is one move of such an
/// event, so it is taken the way its event is raised: a clicked face group or a decoration is a fixture the party
/// uses (<see cref="PlaceFixtureEmitter"/>), and a plate is a reach the party walks into, which raises the event's
/// floor trigger. Either way the ruleset runs the event and the run decides which move — if any — is taken; this
/// emitter derives no transition of its own.
/// </para>
/// <para>
/// <b>What each link is.</b> Every link ends with one disposition: taken by use, by treading, or both, with the
/// condition a run must meet to reach its move (<see cref="PlaceEventPaths"/>); a counter's, when the event opens a
/// building and the counter owns the face; world-issued, when the global program moves the party from no place; or
/// unreachable, with the evidence — no face, plate or decoration of the source place raises the event at all.
/// </para>
/// </remarks>
public static class PlaceEntranceEmitter
{
    /// <summary>The donor's attribute for an event the party triggers by stepping on the face.</summary>
    public const uint PressurePlateAttribute = 0x04000000;

    /// <summary>The donor's attribute for an event the party triggers by clicking the face.</summary>
    public const uint ClickableAttribute = 0x02000000;

    /// <summary>The disposition of a link a party takes by using what raises its event.</summary>
    public const string Used = "used";

    /// <summary>The disposition of a link a party takes by treading on a plate that raises its event.</summary>
    public const string Walked = "walked";

    /// <summary>The disposition of a link whose event both a use and a plate raise.</summary>
    public const string UsedOrWalked = "used-or-walked";

    /// <summary>The disposition of a link whose event opens a building, whose counter owns the face.</summary>
    public const string Counter = "counter";

    /// <summary>The disposition of a link the world issues from no place.</summary>
    public const string WorldIssued = "world-issued";

    /// <summary>The disposition of a link nothing in its source place raises.</summary>
    public const string Unreachable = "unreachable";

    /// <summary>Derives every plate reach, and accounts for every link.</summary>
    /// <param name="graph">The links the places were read from.</param>
    /// <param name="maps">The decoded maps, keyed by the place id the map table gives them.</param>
    /// <param name="programs">Every event program the installation carries, which the links' events are read from.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static PlaceEntranceSummary Emit(PlaceGraph graph, IReadOnlyDictionary<int, DecodedMap> maps, IReadOnlyList<EvtProgram> programs)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(maps);
        ArgumentNullException.ThrowIfNull(programs);

        Dictionary<string, Dictionary<int, List<EvtInstruction>>> events = new(StringComparer.OrdinalIgnoreCase);
        foreach (EvtProgram program in programs)
        {
            if (events.ContainsKey(program.Name)) continue;
            Dictionary<int, List<EvtInstruction>> byEvent = [];
            foreach (EvtInstruction instruction in program.Instructions)
            {
                if (!byEvent.TryGetValue(instruction.EventId, out List<EvtInstruction>? list)) byEvent[instruction.EventId] = list = [];
                list.Add(instruction);
            }

            events[program.Name] = byEvent;
        }

        Writers writers = new(programs);
        List<PlaceLinkAccount> accounts = [];
        List<PlaceEntrancePlacement> entrances = [];
        for (int index = 0; index < graph.Links.Count; index++)
        {
            PlaceLink link = graph.Links[index];
            List<EvtInstruction> instructions = events.TryGetValue(link.SourceEvtName, out Dictionary<int, List<EvtInstruction>>? byEvent)
                ? byEvent.GetValueOrDefault(link.EventId) ?? []
                : [];
            PlaceEventPath? path = PlaceEventPaths.Of(instructions).GetValueOrDefault(link.Step);
            string condition = path?.Condition ?? string.Empty;
            string conditionDetail = condition.Length == 0 ? string.Empty : writers.Describe(path!);

            if (link.SourceMapId is not int from)
            {
                accounts.Add(Account(index, link, WorldIssued, string.Empty, condition, $"{link.SourceEvtName} moves the party from no map: the global program's events are run by a person's topic and the game's own scripts, not by a place, so there is no place a party could take it from."));
                continue;
            }

            if (!maps.TryGetValue(from, out DecodedMap? map))
            {
                accounts.Add(Account(index, link, Unreachable, string.Empty, condition, $"Place {from} was not decoded by this import, so nothing that raises event {link.EventId} was read."));
                continue;
            }

            if (path is null)
            {
                accounts.Add(Account(index, link, Unreachable, string.Empty, string.Empty, $"No run of event {link.EventId} reaches step {link.Step}: every way through the event's branches ends before it."));
                continue;
            }

            (int clicked, int plates, int decorations) = Raisers(map, link.EventId);
            if (PlaceFixtureEmitter.Owner(instructions) is { } owner)
            {
                accounts.Add(Account(index, link, Counter, Trigger(clicked, plates, decorations), condition, $"Event {link.EventId} also holds '{owner}', so the face is that emitter's target and using it opens what it opens; the event's move{(condition.Length > 0 ? $" ({condition})" : string.Empty)} is not run by it.{conditionDetail}"));
                continue;
            }

            if (clicked + plates + decorations == 0)
            {
                accounts.Add(Account(index, link, Unreachable, string.Empty, condition, NoRaiser(map, link)));
                continue;
            }

            string disposition = clicked + decorations > 0 && plates > 0 ? UsedOrWalked : plates > 0 ? Walked : Used;
            accounts.Add(Account(index, link, disposition, Trigger(clicked, plates, decorations), condition, conditionDetail.TrimStart()));
        }

        // Every plate of an event that moves the party is a reach raising the event's floor trigger — the same events
        // the fixture emitter carries as floor triggers, a move within the place among them, so stepping on any of
        // them runs the event and the ruleset answers for what it does.
        Dictionary<string, int> mapByStem = maps.ToDictionary(entry => Path.GetFileNameWithoutExtension(entry.Value.FileName), entry => entry.Key, StringComparer.OrdinalIgnoreCase);
        foreach ((string programName, Dictionary<int, List<EvtInstruction>> byEvent) in events.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (!mapByStem.TryGetValue(Path.GetFileNameWithoutExtension(programName), out int from)) continue;
            DecodedMap map = maps[from];
            foreach (int eventId in PlaceFixtureEmitter.Stepped(map))
            {
                if (!byEvent.TryGetValue(eventId, out List<EvtInstruction>? instructions) || !PlaceFixtureEmitter.IsTravel(instructions) || PlaceFixtureEmitter.Owner(instructions) is not null) continue;
                List<int> links = [.. Enumerable.Range(0, graph.Links.Count).Where(other =>
                    graph.Links[other].SourceMapId == from && graph.Links[other].EventId == eventId && string.Equals(graph.Links[other].SourceEvtName, programName, StringComparison.OrdinalIgnoreCase))];
                foreach ((int faceIndex, MapFace face, int modelIndex, string modelName) in MapFaceList.Flatten(map))
                {
                    if (face.EventId != eventId || (face.Attributes & PressurePlateAttribute) == 0) continue;
                    entrances.Add(Place(from, eventId, links, faceIndex, face, modelIndex, modelName));
                }
            }
        }

        return new PlaceEntranceSummary(
            [.. entrances.OrderBy(entrance => entrance.FromPlace).ThenBy(entrance => entrance.EventId).ThenBy(entrance => entrance.SourceFaceIndex)],
            accounts);
    }

    /// <summary>How many clicked faces, plates and decorations of a map raise an event.</summary>
    private static (int Clicked, int Plates, int Decorations) Raisers(DecodedMap map, int eventId)
    {
        int clicked = 0;
        int plates = 0;
        foreach ((int _, MapFace face, int _, string _) in MapFaceList.Flatten(map))
        {
            if (face.EventId != eventId) continue;
            if ((face.Attributes & ClickableAttribute) != 0) clicked++;
            if ((face.Attributes & PressurePlateAttribute) != 0) plates++;
        }

        return (clicked, plates, map.Decorations.Count(decoration => decoration.EventId == eventId));
    }

    /// <summary>What raises an event, in words.</summary>
    private static string Trigger(int clicked, int plates, int decorations)
    {
        List<string> parts = [];
        if (clicked > 0) parts.Add(string.Create(CultureInfo.InvariantCulture, $"{clicked} clicked face(s)"));
        if (decorations > 0) parts.Add(string.Create(CultureInfo.InvariantCulture, $"{decorations} decoration(s)"));
        if (plates > 0) parts.Add(string.Create(CultureInfo.InvariantCulture, $"{plates} pressure plate(s)"));
        return string.Join(", ", parts);
    }

    /// <summary>The evidence that nothing in a place raises an event, read from the decoded map itself.</summary>
    /// <remarks>
    /// The donor raises a map's own event only through what the map holds — a face's event, read from the face
    /// extras an interior's faces index (<c>src/Engine/Graphics/Indoor.cpp:1397-1416</c>,
    /// <c>src/Engine/Objects/SpriteObject.cpp:301-476</c>, <c>src/Engine/Graphics/Collisions.cpp:735</c>), a
    /// decoration's (<c>src/Engine/Graphics/Viewport.cpp:201-213</c>), and the map's own triggers
    /// (<c>src/Engine/Evt/Processor.cpp:89-140</c>, <c>:203-234</c>) — and a person's topic runs the global
    /// program instead (<c>src/GUI/UI/NPCTopics.cpp:662-666</c>). So a map whose faces, face extras and decorations
    /// carry no event of that number holds nothing that could raise it, which is what is checked and stated.
    /// </remarks>
    private static string NoRaiser(DecodedMap map, PlaceLink link)
    {
        int inert = MapFaceList.Flatten(map).Count(entry => entry.Face.EventId == link.EventId);
        string faces = inert == 0
            ? string.Create(CultureInfo.InvariantCulture, $"no face of its {MapFaceList.Flatten(map).Count()} carries the event")
            : string.Create(CultureInfo.InvariantCulture, $"the {inert} face(s) carrying it have neither the clickable nor the pressure-plate attribute");
        string extras = map is IndoorMap indoor && inert == 0
            ? string.Create(CultureInfo.InvariantCulture, $", none of its {indoor.FaceExtras.Count} face extras carries event {link.EventId}")
            : string.Empty;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"Place {link.SourceMapId} ('{map.FileName}') raises event {link.EventId} from nothing a party does: {faces}{extras}, no decoration raises it, and the event holds no trigger, so neither a click, a step, a timer nor the map's loading runs it in the donor either. Everything the donor's own code raises a map's event through is read here; the event is content the shipped map never wires up to the party.");
    }

    private static PlaceLinkAccount Account(int index, PlaceLink link, string disposition, string trigger, string condition, string detail) =>
        new(index, link.SourceMapId, link.DestinationMapId!.Value, link.EventId, link.Step, disposition, trigger, condition, detail);

    /// <summary>Derives one reach from one plate.</summary>
    private static PlaceEntrancePlacement Place(int from, int eventId, IReadOnlyList<int> links, int faceIndex, MapFace face, int modelIndex, string modelName)
    {
        // Integer division is avoided: the centroid is the mean of the face's corners, and the radius is
        // the farthest corner from it, so both are the face's own geometry rather than a rounding of it.
        double x = 0;
        double y = 0;
        double z = 0;
        foreach (MapPoint vertex in face.Vertices)
        {
            x += vertex.X;
            y += vertex.Y;
            z += vertex.Z;
        }

        x /= face.Vertices.Count;
        y /= face.Vertices.Count;
        z /= face.Vertices.Count;

        double radius = 0;
        foreach (MapPoint vertex in face.Vertices)
        {
            double dx = vertex.X - x;
            double dy = vertex.Y - y;
            double dz = vertex.Z - z;
            double distance = Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
            if (distance > radius) radius = distance;
        }

        return new PlaceEntrancePlacement(from, eventId, links, x, y, z, radius, faceIndex, modelIndex, modelName, face.Attributes);
    }

    /// <summary>Which events of which programs write each quest bit, which is what says whether a condition can be met.</summary>
    private sealed class Writers
    {
        private readonly Dictionary<int, List<string>> _bits = [];

        internal Writers(IReadOnlyList<EvtProgram> programs)
        {
            foreach (EvtProgram program in programs)
            {
                foreach (EvtInstruction instruction in program.Instructions)
                {
                    if (instruction.Opcode is not (EvtOpcodes.Set or EvtOpcodes.Add)) continue;
                    if (!instruction.TryReadVariable(out VariableInstruction variable)) continue;
                    if (EvtVariables.Name(variable.Variable).Word != "quest-bit") continue;
                    if (!_bits.TryGetValue(variable.Value, out List<string>? list)) _bits[variable.Value] = list = [];
                    string writer = string.Create(CultureInfo.InvariantCulture, $"{program.Name} event {instruction.EventId}");
                    if (!list.Contains(writer, StringComparer.Ordinal)) list.Add(writer);
                }
            }
        }

        /// <summary>Who sets each quest bit a path compares, as a sentence; empty when the path compares none.</summary>
        internal string Describe(PlaceEventPath path)
        {
            List<string> said = [];
            foreach (string condition in path.Conditions)
            {
                const string prefix = "quest bit ";
                if (!condition.StartsWith(prefix, StringComparison.Ordinal)) continue;
                string number = condition[prefix.Length..].Split(' ')[0];
                if (!int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out int bit)) continue;
                List<string> writers = _bits.GetValueOrDefault(bit) ?? [];
                said.Add(writers.Count == 0
                    ? $"No event of the shipped programs sets quest bit {number}."
                    : $"Quest bit {number} is set by {string.Join(", ", writers)}.");
            }

            if (path.Ways > 1) said.Add(string.Create(CultureInfo.InvariantCulture, $"{path.Ways} ways through the event reach the move; the condition is the shortest one's."));
            return said.Count == 0 ? string.Empty : " " + string.Join(" ", said.Distinct(StringComparer.Ordinal));
        }
    }
}
