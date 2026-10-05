using MightAndMagic7.Import.Events;
using MightAndMagic7.Import.Maps;
using MightAndMagic7.Import.Tables;
using MightAndMagic7.Import.World;

namespace MightAndMagic7.Import.Packs;

/// <summary>One step of a map event, normalized: what the step is, and the operands a reader of it needs.</summary>
/// <remarks>
/// Only the fields the step's own instruction carries are set; the rest are null. A step whose opcode this
/// importer decodes no operands for is written with its word alone, so a reader knows what it is and that
/// nothing else about it was read.
/// </remarks>
/// <param name="Step">The step's number inside its event, which is what a jump names.</param>
/// <param name="Op">The instruction's word (<see cref="EvtOpcodes.Word"/>).</param>
public sealed record PlaceEventStep(int Step, string Op)
{
    /// <summary>The opcode, written only when the language has no word for it.</summary>
    public int? Opcode { get; init; }

    /// <summary>The variable family a comparison or assignment names (<see cref="EvtVariables.Name"/>).</summary>
    public string? Variable { get; init; }

    /// <summary>The member of the family, when the family has several; a season jump's season.</summary>
    public string? Which { get; init; }

    /// <summary>The slot of a numbered family, such as a map variable's.</summary>
    public int? Index { get; init; }

    /// <summary>The raw variable code, written only when the language has no word for it.</summary>
    public int? Code { get; init; }

    /// <summary>The value a comparison compares with, or an assignment writes.</summary>
    public int? Value { get; init; }

    /// <summary>The step a comparison or a jump goes to.</summary>
    public int? Target { get; init; }

    /// <summary>The steps a random jump chooses among.</summary>
    public IReadOnlyList<int>? Targets { get; init; }

    /// <summary>The line of the map's own text a status line or message prints.</summary>
    public string? Text { get; init; }

    /// <summary>The index of that line in the map's string table.</summary>
    public int? TextId { get; init; }

    /// <summary>Which characters a choice or a harm names: <c>member</c>, <c>active</c>, <c>party</c>, <c>random</c>.</summary>
    public string? Who { get; init; }

    /// <summary>The character's position when <see cref="Who"/> names one character.</summary>
    public int? Member { get; init; }

    /// <summary>The kind of harm a damage step does.</summary>
    public string? Kind { get; init; }

    /// <summary>How much harm a damage step does.</summary>
    public int? Amount { get; init; }

    /// <summary>A timer's period: <c>yearly</c>, <c>monthly</c>, <c>weekly</c>, <c>daily</c>, or <c>interval</c>.</summary>
    public string? Period { get; init; }

    /// <summary>A daily timer's hour.</summary>
    public int? Hour { get; init; }

    /// <summary>A daily timer's minute.</summary>
    public int? Minute { get; init; }

    /// <summary>An interval timer's period in half minutes.</summary>
    public int? HalfMinutes { get; init; }

    /// <summary>The id of the door a door step moves, which is the id the place's door placement carries.</summary>
    public int? Door { get; init; }

    /// <summary>What a door step does: <c>open</c>, <c>close</c>, or <c>toggle</c> a door at rest.</summary>
    public string? Action { get; init; }

    /// <summary>The treasure level an item gift draws at.</summary>
    public int? Level { get; init; }

    /// <summary>The item tag a gift's draw is narrowed to by what a thing is, empty for any.</summary>
    public string? ItemKind { get; init; }

    /// <summary>The item tag a gift's draw is narrowed to by the skill it is used with, empty for any.</summary>
    public string? ItemSkill { get; init; }

    /// <summary>The item a gift gives outright, replacing the draw; absent when the draw stands.</summary>
    public int? Item { get; init; }

    /// <summary>The spell a cast step casts.</summary>
    public int? Spell { get; init; }

    /// <summary>The mastery a cast step casts at, or a skill jump waits for: <c>novice</c> to <c>grandmaster</c>.</summary>
    public string? Mastery { get; init; }

    /// <summary>The skill rank a cast step casts at, or a skill jump waits for.</summary>
    public int? Rank { get; init; }

    /// <summary>The person a conversation or topic step names.</summary>
    public int? Person { get; init; }

    /// <summary>The event a topic step makes the person's topic raise.</summary>
    public int? Raises { get; init; }

    /// <summary>The face group or creature group a flag step names; the face or decoration cog a texture or sprite step names.</summary>
    public int? Group { get; init; }

    /// <summary>The bitmap a texture step gives its faces, or the decoration-list name a sprite step gives its decorations.</summary>
    public string? Name { get; init; }

    /// <summary>The interior light a light step turns on or off, by its index in the level's lights.</summary>
    public int? Light { get; init; }

    /// <summary>The attribute bit a flag step sets or clears.</summary>
    public long? Flag { get; init; }

    /// <summary>Whether a flag step sets its bit rather than clearing it; whether a sprite step shows its decorations; whether a light step turns its light on.</summary>
    public bool? On { get; init; }

    /// <summary>
    /// The travel link a move to another place takes, by its entry id in the place graph, which names the
    /// transition the product takes it through.
    /// </summary>
    public string? Link { get; init; }

    /// <summary>The place a move to another place arrives at.</summary>
    public int? ToPlace { get; init; }

    /// <summary>
    /// What kind of travel a move to another place is: <c>walking</c> between two regions, <c>entrance</c> otherwise,
    /// and <c>scripted</c> for a move of the global program, which the world issues from no place.
    /// </summary>
    public string? Travel { get; init; }

    /// <summary>Whether a move stays in the place that issued it, which is a reposition rather than a link.</summary>
    public bool? WithinPlace { get; init; }

    /// <summary>The house a person-moving step moves the person to, zero for none; the house a house step opens.</summary>
    public int? House { get; init; }

    /// <summary>The greeting table row a greeting step makes the person greet the party with.</summary>
    public int? Greeting { get; init; }

    /// <summary>The actual actor group and news row named by a group-news instruction.</summary>
    public uint? NewsGroup { get; init; }

    /// <summary>The news table row; zero silences the group.</summary>
    public uint? News { get; init; }

    /// <summary>Where a move within the place sets the party down along the place's first axis.</summary>
    public int? X { get; init; }

    /// <summary>Where along the second axis.</summary>
    public int? Y { get; init; }

    /// <summary>Where in height.</summary>
    public int? Z { get; init; }

    /// <summary>The facing it sets the party down with, in the donor's units; -1 keeps the party's own.</summary>
    public int? Yaw { get; init; }

    /// <summary>The encounter number a summoning names: its slot plus three times its grade, as a spawn record's.</summary>
    public int? Encounter { get; init; }

    /// <summary>The unique name a summoning gives its creatures, zero for their row's own.</summary>
    public int? UniqueName { get; init; }

    /// <summary>
    /// What a summoning's encounter number reads as in the place's own map table row — the slot's kind, difficulty,
    /// count range and graded variants — or null when the row resolves it to nothing or no tables were read.
    /// </summary>
    public PlaceEncounterSlot? Summons { get; init; }
}

/// <summary>One map event a place's content carries, with its normalized steps.</summary>
/// <param name="PlaceId">The place whose program holds it.</param>
/// <param name="FileName">The map file the place was decoded from.</param>
/// <param name="EventId">The event's id in the program.</param>
/// <param name="Label">The hint the event shows while the party's aim rests on what raises it, or empty.</param>
/// <param name="Steps">Its steps, in program order, the hint excluded.</param>
/// <param name="Raised">Whether a face or a decoration of the place raises it, which is what makes it a fixture's.</param>
/// <param name="Triggered">Whether it holds a timer, whose steps the map runs rather than a use.</param>
public sealed record PlaceEvent(int PlaceId, string FileName, int EventId, string Label, IReadOnlyList<PlaceEventStep> Steps, bool Raised, bool Triggered)
{
    /// <summary>
    /// Whether a pressure plate of the place raises it, which is what makes it a floor trigger's: the party sets it
    /// off by walking onto the plate. Every event a plate raises is carried this way unless a counter or a container
    /// answers for it.
    /// </summary>
    public bool Stepped { get; init; }

    /// <summary>
    /// Whether it is a house's own event that does more than open the house — a door that also moves the party, a shop
    /// a quest bit shuts, the arbiter's door — which the house's own use runs (its placement's <c>sourceEvent</c>).
    /// </summary>
    public bool Housed { get; init; }

    /// <summary>The event's content identity, which a fixture's placement and the ruleset name it by.</summary>
    public string Id => $"{PlaceId}.{EventId}";
}

/// <summary>One fixture a place holds: something whose use raises one of the place's own events.</summary>
/// <param name="PlaceId">The place it stands in.</param>
/// <param name="EventId">The event its use raises.</param>
/// <param name="Cluster">Which group of the event's faces it is, counting from zero, when the event's faces stand apart.</param>
/// <param name="X">The mean of its faces' box centres along the place's first axis.</param>
/// <param name="Y">The mean along the second axis.</param>
/// <param name="Z">The lowest corner of those faces, which is where a party stands to use it.</param>
/// <param name="FaceCount">How many faces raise the event here.</param>
/// <param name="ModelIndex">The region model the faces belong to, or null in an interior.</param>
/// <param name="ModelName">The region model's own name, or empty in an interior.</param>
/// <param name="Label">The event's hint, which is what the fixture is called.</param>
public sealed record PlaceFixturePlacement(
    int PlaceId,
    int EventId,
    int Cluster,
    double X,
    double Y,
    double Z,
    int FaceCount,
    int? ModelIndex,
    string ModelName,
    string Label)
{
    /// <summary>The placement's identity in its place.</summary>
    public string PlacementId => Cluster == 0 ? $"fixture-{EventId}" : $"fixture-{EventId}-{Cluster}";

    /// <summary>The flattened source face identities belonging to this fixture, excluding other clusters.</summary>
    public IReadOnlyList<int> FaceIndices { get; init; } = [];
}

/// <summary>
/// One floor trigger a place holds: the event its pressure plates raise when the party walks onto one, standing at
/// the mean of the plates' own box centres.
/// </summary>
/// <remarks>
/// The plates themselves are the reaches a party walks into (<see cref="PlaceEntranceEmitter"/>), each raising this
/// one placement; the placement is what the ruleset describes and runs, so a plate — a move, a trap, an alarm — is
/// used through the same workflow as a clicked face.
/// </remarks>
/// <param name="PlaceId">The place it stands in.</param>
/// <param name="EventId">The event its plates raise.</param>
/// <param name="X">The mean of the plates' box centres along the place's first axis.</param>
/// <param name="Y">The mean along the second axis.</param>
/// <param name="Z">The mean in height.</param>
/// <param name="FaceCount">How many plates raise the event.</param>
/// <param name="Label">The event's hint, which is what the trigger is called.</param>
public sealed record PlaceFloorTrigger(int PlaceId, int EventId, double X, double Y, double Z, int FaceCount, string Label)
{
    /// <summary>The placement kind a floor trigger is written as.</summary>
    public const string PlacementKind = "floor-trigger";

    /// <summary>The placement's identity in its place, which the plates' reaches name.</summary>
    public string PlacementId => PlacementIdOf(EventId);

    /// <summary>The placement identity of the floor trigger raising an event.</summary>
    /// <param name="eventId">The event.</param>
    public static string PlacementIdOf(int eventId) => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"trigger-{eventId}");
}

/// <summary>What an import's fixture derivation produced, over every map it decoded.</summary>
/// <param name="Fixtures">Every fixture placement, in place, event and cluster order.</param>
/// <param name="Events">Every event the places' content carries, in place and event order.</param>
/// <param name="OwnedElsewhere">
/// How many raised events another emitter answers for, by its word: <c>speak-in-house</c> (a counter),
/// <c>open-chest</c> (a container), <c>change-door-state</c> (a door).
/// </param>
/// <param name="RaisedWithoutInstructions">How many events a face or decoration raises that the place's program does not hold.</param>
/// <param name="PlacesWithoutProgram">How many decoded places have no event program named after their map.</param>
public sealed record PlaceFixtureSummary(
    IReadOnlyList<PlaceFixturePlacement> Fixtures,
    IReadOnlyList<PlaceEvent> Events,
    IReadOnlyDictionary<string, int> OwnedElsewhere,
    int RaisedWithoutInstructions,
    int PlacesWithoutProgram)
{
    /// <summary>An import that derived nothing, such as one that decoded no maps.</summary>
    public static PlaceFixtureSummary Empty { get; } = new([], [], new Dictionary<string, int>(), 0, 0);

    /// <summary>
    /// Every floor trigger: the events pressure plates raise that no counter or container answers for, one per event
    /// and place.
    /// </summary>
    public IReadOnlyList<PlaceFloorTrigger> Triggers { get; init; } = [];

    /// <summary>How many events a house's own use runs because they do more than open the house.</summary>
    public int HousedEventCount => Events.Count(placeEvent => placeEvent.Housed);
    /// <summary>How many events a plate raises that a counter or a container answers for, by the owner's word.</summary>
    public IReadOnlyDictionary<string, int> SteppedOwnedElsewhere { get; init; } = new Dictionary<string, int>();

    /// <summary>How many events a plate raises that the place's program does not hold.</summary>
    public int SteppedWithoutInstructions { get; init; }

    /// <summary>How many events a floor trigger raises.</summary>
    public int SteppedEventCount => Events.Count(placeEvent => placeEvent.Stepped);

    /// <summary>How many raised events move the party, which a fixture or a floor trigger now answers for.</summary>
    public int TravelEventCount => Events.Count(placeEvent => (placeEvent.Raised || placeEvent.Stepped) && placeEvent.Steps.Any(step => step.Op == "move-to-map"));

    /// <summary>How many events a fixture raises.</summary>
    public int FixtureEventCount => Events.Count(placeEvent => placeEvent.Raised);

    /// <summary>How many carried events hold a timer.</summary>
    public int TriggeredEventCount => Events.Count(placeEvent => placeEvent.Triggered);

    /// <summary>How many places hold a fixture.</summary>
    public int PlaceCount => Fixtures.Select(fixture => fixture.PlaceId).Distinct().Count();

    /// <summary>
    /// How many steps of the fixtures' and the floor triggers' own events there are of each kind, a variable step
    /// counted under its instruction and its variable family together (<c>add autonote</c>).
    /// </summary>
    /// <remarks>
    /// This is what the ruleset's interpretation is measured against: every kind here is either interpreted
    /// or refused by name, and the counts say how much of the shipped programs each answer covers.
    /// </remarks>
    public IReadOnlyDictionary<string, int> StepKinds =>
        Events.Where(placeEvent => placeEvent.Raised || placeEvent.Stepped)
            .SelectMany(placeEvent => placeEvent.Steps)
            .GroupBy(step => step.Variable is { } variable ? $"{step.Op} {variable}" : step.Op, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
}

/// <summary>
/// Derives a place's fixtures — the things whose use raises one of its own events — and the events
/// themselves, normalized into steps a ruleset reads.
/// </summary>
/// <remarks>
/// <para>
/// <b>What a fixture is.</b> A map's faces and decorations raise events by number (a face's event id, a
/// decoration's), and the donor runs the event with that number from the map's own program when the party uses
/// the face or the decoration (OpenEnroth <c>src/Engine/Graphics/Viewport.cpp:201-213</c> for a decoration; a
/// face is used when it carries the clickable attribute, <c>src/Engine/Graphics/FaceEnums.h:34</c>). A face
/// raising its event only when stepped on is a floor trigger and not a fixture. Three kinds of event already have an owner in this import —
/// one that opens a building (a counter), one that opens a container, and one that only moves the doors a party
/// clicks open (a door) — and those are left to it (<see cref="Owner"/>). Every other raised event is a fixture's: a well, a fountain, an
/// obelisk, a sign, a lever, a crate.
/// </para>
/// <para>
/// <b>Where a fixture stands.</b> Like a container, a fixture has no position of its own, so it stands over the
/// mean of the bounding-box centres of the faces that raise its event (the donor's own reading of a face,
/// <c>src/Engine/Objects/Chest.cpp:396-402</c>), at the height of their lowest corner: a party uses a cave mouth or
/// a shrine standing at its foot, as it reaches a counter standing on the street (ours). One event raised by faces in two places — the same signpost
/// text on two posts across a region — is two fixtures, not one between them: in a region the faces are
/// grouped by the model they belong to, and in an interior by distance, a face joining the first group whose
/// first face stands within <see cref="PlaceContainerEmitter.FaceSpreadLimit"/> of it.
/// </para>
/// <para>
/// <b>What an event is written as.</b> Its steps, in program order, each with its instruction's word and the
/// operands that instruction carries, the variables named by family (<see cref="EvtVariables"/>) and the
/// text a step prints resolved from the map's own string table (<see cref="MapStrings"/>). The hint an event
/// shows is not a step — the donor never executes one (<c>src/Engine/Evt/EvtInstruction.cpp:919-924</c>) — so
/// it is written as the event's label. Nothing is interpreted here: what a step does in play is the
/// ruleset's, and a step this importer reads no operands for is written by its word alone.
/// </para>
/// <para>
/// <b>Timers travel with the fixtures.</b> An event holding a timer trigger is carried whether or not
/// anything raises it, because what a well has left to give is often set by a timer of another event — the
/// central wells of the first region are refilled by an event nothing raises.
/// </para>
/// </remarks>
public static class PlaceFixtureEmitter
{
    /// <summary>Derives every place's fixtures and events.</summary>
    /// <param name="maps">The decoded maps, keyed by the place id the map table gives them.</param>
    /// <param name="programs">Every event program the installation carries.</param>
    /// <param name="strings">Every map's string table, by the map file stem.</param>
    /// <param name="graph">
    /// The travel links the programs' moves were read into, which a move step names its link by; without one a move
    /// step carries no link and the ruleset refuses it by name.
    /// </param>
    /// <param name="tables">
    /// The rule tables, whose map table rows say what a summoning's encounter slot is; without them a summoning carries
    /// no slot and the ruleset refuses it by name.
    /// </param>
    /// <returns>What was derived.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static PlaceFixtureSummary Emit(
        IReadOnlyDictionary<int, DecodedMap> maps,
        IReadOnlyList<EvtProgram> programs,
        IReadOnlyDictionary<string, MapStrings> strings,
        PlaceGraph? graph = null,
        Mm7Tables? tables = null)
    {
        ArgumentNullException.ThrowIfNull(maps);
        ArgumentNullException.ThrowIfNull(programs);
        ArgumentNullException.ThrowIfNull(strings);
        PlaceMoves moves = new(graph, maps);
        List<PlaceFloorTrigger> triggers = [];

        Dictionary<string, EvtProgram> byStem = new(StringComparer.OrdinalIgnoreCase);
        foreach (EvtProgram program in programs) byStem.TryAdd(Path.GetFileNameWithoutExtension(program.Name), program);

        List<PlaceFixturePlacement> fixtures = [];
        List<PlaceEvent> events = [];
        SortedDictionary<string, int> owned = new(StringComparer.Ordinal);
        int withoutInstructions = 0;
        int withoutProgram = 0;
        SortedDictionary<string, int> steppedOwned = new(StringComparer.Ordinal);
        int steppedWithoutInstructions = 0;
        foreach ((int placeId, DecodedMap map) in maps.OrderBy(entry => entry.Key))
        {
            string stem = Path.GetFileNameWithoutExtension(map.FileName);
            if (!byStem.TryGetValue(stem, out EvtProgram? program))
            {
                withoutProgram++;
                continue;
            }

            MapStrings? text = strings.GetValueOrDefault(stem);
            Dictionary<int, List<EvtInstruction>> byEvent = [];
            foreach (EvtInstruction instruction in program.Instructions)
            {
                if (!byEvent.TryGetValue(instruction.EventId, out List<EvtInstruction>? list)) byEvent[instruction.EventId] = list = [];
                list.Add(instruction);
            }

            SortedSet<int> raised = Raised(map);
            HashSet<int> fixtureEvents = [];
            HashSet<int> housedEvents = [];
            HashSet<int> steppedEvents = [];
            foreach (int eventId in Stepped(map))
            {
                // A plate raises an event when the party walks onto it, and every such event is a floor trigger's —
                // a move, a trap's spell, an alarm turning a group hostile, a door shutting behind the party — unless
                // a counter or a container answers for it; the run decides what it does.
                if (!byEvent.TryGetValue(eventId, out List<EvtInstruction>? stepped))
                {
                    steppedWithoutInstructions++;
                    continue;
                }

                if (Owner(stepped, trodden: true) is { } owner)
                {
                    steppedOwned[owner] = steppedOwned.GetValueOrDefault(owner) + 1;
                    continue;
                }

                steppedEvents.Add(eventId);
            }

            foreach (int eventId in raised)
            {
                if (!byEvent.TryGetValue(eventId, out List<EvtInstruction>? instructions))
                {
                    withoutInstructions++;
                    continue;
                }

                if (Owner(instructions) is { } owner)
                {
                    owned[owner] = owned.GetValueOrDefault(owner) + 1;

                    // A house's event that does more than open the house is the house's own use to run: the donor runs a
                    // clicked face's event whatever it holds (OpenEnroth src/Engine/Graphics/Viewport.cpp:300-320), and
                    // opening the house is one of its steps (src/Engine/Evt/EvtInterpreter.cpp:189-198).
                    if (owner == EvtOpcodes.Word(EvtOpcodes.SpeakInHouse) && DoesMoreThanOpen(instructions)) housedEvents.Add(eventId);
                    continue;
                }

                fixtureEvents.Add(eventId);
            }

            foreach ((int eventId, List<EvtInstruction> instructions) in byEvent.OrderBy(entry => entry.Key))
            {
                bool isFixture = fixtureEvents.Contains(eventId);
                bool isStepped = steppedEvents.Contains(eventId);
                bool isHoused = housedEvents.Contains(eventId);
                bool triggered = instructions.Any(instruction => instruction.Opcode is EvtOpcodes.OnTimer or EvtOpcodes.OnLongTimer);
                bool lifecycle = instructions.Any(instruction => instruction.Opcode is EvtOpcodes.OnMapReload or 53);
                bool containerProgram = raised.Contains(eventId) && instructions.Any(i => i.Opcode == EvtOpcodes.OpenChest)
                    && instructions.Any(i => i.Opcode is not (EvtOpcodes.OpenChest or EvtOpcodes.MouseOver or EvtOpcodes.Exit));
                if (!isFixture && !isStepped && !isHoused && !containerProgram && !lifecycle && !(triggered && Owner(instructions) is null)) continue;
                events.Add(Normalize(placeId, map, program.Name, eventId, instructions, text, isFixture, triggered, moves, tables) with { Stepped = isStepped, Housed = isHoused });
            }

            foreach (PlaceFixturePlacement fixture in Place(placeId, map, fixtureEvents, events)) fixtures.Add(fixture);
            triggers.AddRange(Triggers(placeId, map, steppedEvents, events));
        }

        return new PlaceFixtureSummary(fixtures, events, owned, withoutInstructions, withoutProgram)
        {
            Triggers = triggers,
            SteppedOwnedElsewhere = steppedOwned,
            SteppedWithoutInstructions = steppedWithoutInstructions,
        };
    }

    /// <summary>Every event a party raises by walking onto a pressure plate of a map.</summary>
    /// <remarks>
    /// The donor raises a face's event when the party steps on a face carrying the pressure-plate attribute
    /// (OpenEnroth <c>src/Engine/Graphics/Outdoor.cpp:966-980</c>, <c>src/Engine/Graphics/Indoor.cpp:1491</c>).
    /// </remarks>
    internal static SortedSet<int> Stepped(DecodedMap map)
    {
        SortedSet<int> stepped = [];
        foreach ((int _, MapFace face, int _, string _) in MapFaceList.Flatten(map))
        {
            if (face.EventId != 0 && (face.Attributes & PlaceEntranceEmitter.PressurePlateAttribute) != 0) stepped.Add(face.EventId);
        }

        return stepped;
    }

    /// <summary>Whether a house's event does anything beyond opening the house, leaving it, and its hint and sound.</summary>
    internal static bool DoesMoreThanOpen(IReadOnlyList<EvtInstruction> instructions) =>
        instructions.Any(instruction => instruction.Opcode is not (EvtOpcodes.SpeakInHouse or EvtOpcodes.Exit or EvtOpcodes.MouseOver or EvtOpcodes.PlaySound));

    /// <summary>
    /// Whether a plate's event is a floor trigger's: every event a plate raises is, unless a counter or a container
    /// answers for it (<see cref="Owner"/>).
    /// </summary>
    internal static bool IsTrodden(IReadOnlyList<EvtInstruction> instructions) => Owner(instructions, trodden: true) is null;

    /// <summary>Where each of a place's floor-trigger events stands: the mean of its plates' box centres.</summary>
    private static IEnumerable<PlaceFloorTrigger> Triggers(int placeId, DecodedMap map, HashSet<int> steppedEvents, List<PlaceEvent> events)
    {
        if (steppedEvents.Count == 0) yield break;
        Dictionary<int, string> labels = events
            .Where(placeEvent => placeEvent.PlaceId == placeId)
            .ToDictionary(placeEvent => placeEvent.EventId, placeEvent => placeEvent.Label);
        Dictionary<int, List<(double X, double Y, double Z)>> plates = [];
        foreach ((int _, MapFace face, int _, string _) in MapFaceList.Flatten(map))
        {
            if (!steppedEvents.Contains(face.EventId) || (face.Attributes & PlaceEntranceEmitter.PressurePlateAttribute) == 0) continue;
            if (!plates.TryGetValue(face.EventId, out List<(double X, double Y, double Z)>? list)) plates[face.EventId] = list = [];
            list.Add(MapFaceList.BoxCentre(face));
        }

        foreach ((int eventId, List<(double X, double Y, double Z)> points) in plates.OrderBy(entry => entry.Key))
        {
            yield return new PlaceFloorTrigger(
                placeId,
                eventId,
                points.Sum(point => point.X) / points.Count,
                points.Sum(point => point.Y) / points.Count,
                points.Sum(point => point.Z) / points.Count,
                points.Count,
                labels.GetValueOrDefault(eventId, string.Empty));
        }
    }

    /// <summary>Every event a party raises by using a face or a decoration of a map.</summary>
    /// <remarks>
    /// A face raises its event when it is clicked only if it carries the donor's clickable attribute; a face
    /// that carries only the pressure-plate attribute raises it when the party steps on it, which is a trap or
    /// an alarm in the floor rather than something a party walks up to and uses (OpenEnroth
    /// <c>src/Engine/Graphics/FaceEnums.h:34-35</c>). A decoration raising an event is always used by a click
    /// (<c>src/Engine/Graphics/Viewport.cpp:201-213</c>).
    /// </remarks>
    private static SortedSet<int> Raised(DecodedMap map)
    {
        SortedSet<int> raised = [];
        foreach ((int _, MapFace face, int _, string _) in MapFaceList.Flatten(map))
        {
            if (Clickable(face)) raised.Add(face.EventId);
        }

        foreach (MapDecoration decoration in map.Decorations)
        {
            if (decoration.EventId != 0) raised.Add(decoration.EventId);
        }

        return raised;
    }

    /// <summary>The emitter that answers for an event, by the word of the instruction it answers for, or null.</summary>
    /// <remarks>
    /// A move is not an owner: an event that moves the party is a fixture's when a face or a decoration raises it
    /// and a floor trigger's when a plate does, and its run decides whether — and which — move is taken. A
    /// building's and a container's events are their emitters', because opening the house or the chest is what the
    /// party uses the face for; an event that does both is the house's (<see cref="PlaceEntranceEmitter"/> states
    /// the move it carries as a counter's link).
    /// </remarks>
    /// <param name="instructions">The event's instructions.</param>
    /// <param name="trodden">
    /// Whether a plate raises the event rather than a click: a door answers only for the face a party clicks, so an
    /// event a plate raises that moves doors — a wall closing behind the party — is the plate's floor trigger.
    /// </param>
    internal static string? Owner(IReadOnlyList<EvtInstruction> instructions, bool trodden = false)
    {
        if (instructions.Any(instruction => instruction.Opcode == EvtOpcodes.SpeakInHouse)) return EvtOpcodes.Word(EvtOpcodes.SpeakInHouse);
        if (instructions.Any(instruction => instruction.Opcode == EvtOpcodes.OpenChest)) return EvtOpcodes.Word(EvtOpcodes.OpenChest);

        // An event that does nothing but move doors is a door's: the face a party clicks to open a door, or a
        // switch beside it, raises an event whose only work is the door's own change of state, and the door
        // record that change names is already a placement the party opens directly. Writing the face as a
        // fixture too would put two targets on one doorway that do one thing.
        // A plate is no such target — the party sets it off by walking, and nothing else sets the door moving then.
        if (!trodden &&
            instructions.Any(instruction => instruction.Opcode == EvtOpcodes.ChangeDoorState) &&
            instructions.All(instruction => instruction.Opcode is EvtOpcodes.ChangeDoorState or EvtOpcodes.Exit or EvtOpcodes.MouseOver or EvtOpcodes.PlaySound))
        {
            return EvtOpcodes.Word(EvtOpcodes.ChangeDoorState);
        }

        return null;
    }

    /// <summary>One event, normalized.</summary>
    private static PlaceEvent Normalize(
        int placeId,
        DecodedMap map,
        string programName,
        int eventId,
        List<EvtInstruction> instructions,
        MapStrings? strings,
        bool raised,
        bool triggered,
        PlaceMoves moves,
        Mm7Tables? tables)
    {
        string label = string.Empty;
        List<PlaceEventStep> steps = [];
        foreach (EvtInstruction instruction in instructions)
        {
            if (instruction.Opcode == EvtOpcodes.MouseOver)
            {
                if (label.Length == 0 && instruction.TryReadText(out int hint)) label = strings?.Line(hint) ?? string.Empty;
                continue;
            }

            PlaceEventStep step = Classed(Step(instruction, id => strings?.Line(id) ?? string.Empty), tables?.Classes);
            if (step.Encounter is { } encounter && tables is not null)
            {
                // A summoning names one of the place's encounter slots the way a spawn record does, so its slot is read
                // from the same map table row, by the same reading.
                step = step with { Summons = PlaceEncounters.Slot(tables, placeId, encounter, out _) };
            }

            steps.Add(instruction.TryReadMoveToMap(out MoveToMapInstruction move) ? moves.Describe(step, map, programName, instruction, move) : step);
        }

        return new PlaceEvent(placeId, map.FileName, eventId, label, steps, raised, triggered);
    }

    /// <summary>
    /// A step comparing or changing a character's class, with the class named by the class table's row it numbers.
    /// </summary>
    /// <remarks>
    /// The donor's class variable is the class's own number (OpenEnroth <c>src/Engine/Objects/Character.cpp:3618-3619</c>
    /// compares it, <c>:4028-4029</c> sets it), and that number is the row of the class table (<c>CLASS.TXT</c>, read in
    /// table order), so the step carries the row's name in <c>which</c> and a reader never needs the table's order. A
    /// number past the table names nothing and is written without one.
    /// </remarks>
    /// <param name="step">The step as read.</param>
    /// <param name="classes">The class table, or null when the import read none.</param>
    internal static PlaceEventStep Classed(PlaceEventStep step, ClassTable? classes) =>
        step is { Variable: "class", Value: { } row } && classes is not null && row >= 0 && row < classes.Ranks.Count
            ? step with { Which = classes.Ranks[row].Name }
            : step;

    /// <summary>One instruction as the step a pack carries.</summary>
    /// <param name="instruction">The instruction.</param>
    /// <param name="text">
    /// The line a text step's number names: a map's own string table for a place's event, the topic text table for
    /// the global program's (<see cref="GlobalEventEmitter"/>).
    /// </param>
    internal static PlaceEventStep Step(EvtInstruction instruction, Func<int, string> text)
    {
        string word = EvtOpcodes.Word(instruction.Opcode);
        PlaceEventStep step = new(instruction.Step, word)
        {
            Opcode = string.Equals(word, EvtOpcodes.UnknownWord, StringComparison.Ordinal) ? instruction.Opcode : null,
        };

        if (instruction.TryReadVariable(out VariableInstruction variable))
        {
            EvtVariableName name = EvtVariables.Name(variable.Variable);
            return step with
            {
                Variable = name.Word,
                Which = name.Which.Length > 0 ? name.Which : null,
                Index = name.Index,
                Code = string.Equals(name.Word, EvtVariables.UnknownWord, StringComparison.Ordinal) ? variable.Variable : null,
                Value = variable.Value,
                Target = variable.Target,
            };
        }

        if (instruction.TryReadText(out int textId))
        {
            return step with { TextId = textId, Text = text(textId) };
        }

        if (instruction.TryReadOpenChest(out OpenChestInstruction opening)) return step with { Index = opening.ContainerId };

        if (instruction.TryReadJump(out int target)) return step with { Target = target };

        if (instruction.TryReadCheckSeason(out int season, out int seasonTarget))
        {
            return step with { Which = EvtVariables.Season(season), Target = seasonTarget };
        }

        if (instruction.TryReadForPartyMember(out byte who))
        {
            (string choice, int? member) = EvtVariables.Who(who);
            return step with { Who = choice, Member = member };
        }

        if (instruction.TryReadRandomGoTo(out IReadOnlyList<int> targets)) return step with { Targets = targets };

        if (instruction.TryReadReceiveDamage(out DamageInstruction damage))
        {
            (string choice, int? member) = EvtVariables.Who(damage.Who);
            return step with { Who = choice, Member = member, Kind = EvtVariables.DamageKind(damage.Kind), Amount = damage.Amount };
        }

        if (instruction.TryReadDoor(out int door, out int action))
        {
            return step with { Door = door, Action = EvtVariables.DoorAction(action) };
        }

        if (instruction.TryReadGiveItem(out GiveItemInstruction gift))
        {
            (string kind, string skill) = ItemVocabulary.FilterOfRandomItem(gift.Kind);
            return step with { Level = gift.Level, ItemKind = kind, ItemSkill = skill, Item = gift.Item != 0 ? gift.Item : null };
        }

        if (instruction.TryReadCastSpell(out CastSpellInstruction cast))
        {
            return step with { Spell = cast.Spell, Mastery = EvtVariables.Mastery(cast.Mastery), Rank = cast.Rank };
        }

        if (instruction.TryReadSummonMonsters(out SummonMonstersInstruction summon))
        {
            // Where the creatures stand is a point of the map, written where a move's point is; the count is how many,
            // zero for the slot's own range.
            return step with
            {
                Encounter = summon.Encounter,
                Amount = summon.Count,
                X = summon.X,
                Y = summon.Y,
                Z = summon.Z,
                Group = summon.Group,
                UniqueName = summon.UniqueName,
            };
        }

        if (instruction.TryReadSpeakNpc(out int person)) return step with { Person = person };

        if (instruction.TryReadSpeakInHouse(out int building)) return step with { House = building };

        if (instruction.TryReadMoveNpc(out int moved, out int house)) return step with { Person = moved, House = house };

        if (instruction.TryReadNpcGroupNews(out uint newsGroup, out uint news)) return step with { NewsGroup = newsGroup, News = news };
        if (instruction.TryReadNpcGreeting(out int greeted, out int greeting)) return step with { Person = greeted, Greeting = greeting };

        if (instruction.TryReadNpcSetItem(out int holder, out int held, out bool given)) return step with { Person = holder, Item = held, On = given };

        if (instruction.TryReadCanShow(out bool shows)) return step with { On = shows };

        if (instruction.TryReadNpcTopic(out NpcTopicInstruction topic))
        {
            return step with { Person = topic.Person, Index = topic.Slot, Raises = topic.Event };
        }

        if (instruction.TryReadCheckSkill(out CheckSkillInstruction skillJump))
        {
            return step with
            {
                Which = EvtVariables.Skill(skillJump.Skill),
                Mastery = EvtVariables.Mastery(skillJump.Mastery),
                Rank = skillJump.Rank,
                Target = skillJump.Target,
            };
        }

        if (instruction.TryReadIsActorKilled(out ActorKilledInstruction killed))
        {
            return step with
            {
                Which = EvtVariables.KillPolicy(killed.Policy),
                Value = killed.Parameter,
                Amount = killed.Count,
                Target = killed.Target,
            };
        }

        if (instruction.TryReadFlagToggle(out FlagToggleInstruction toggle))
        {
            return step with { Group = toggle.Group, Flag = toggle.Flag, On = toggle.On };
        }

        if (instruction.TryReadSetTexture(out int textured, out string texture))
        {
            return step with { Group = textured, Name = texture };
        }

        if (instruction.TryReadSetSprite(out int decorated, out bool decorationShows, out string decoration))
        {
            return step with { Group = decorated, On = decorationShows, Name = decoration };
        }

        if (instruction.TryReadToggleIndoorLight(out int light, out bool lit))
        {
            return step with { Light = light, On = lit };
        }

        if (instruction.TryReadTimer(out TimerInstruction timer))
        {
            // The donor's own precedence: an interval replaces everything, then yearly, monthly and weekly
            // in that order, and a timer that states none of them is daily at its hour
            // (OpenEnroth src/Engine/Evt/Processor.cpp:101-118).
            if (timer.HalfMinutes != 0) return step with { Period = "interval", HalfMinutes = timer.HalfMinutes };
            if (timer.Yearly) return step with { Period = "yearly" };
            if (timer.Monthly) return step with { Period = "monthly" };
            if (timer.Weekly) return step with { Period = "weekly" };
            return step with { Period = "daily", Hour = timer.Hour, Minute = timer.Minute };
        }

        return step;
    }

    /// <summary>Where each of a place's fixture events stands: one fixture per group of the faces that raise it.</summary>
    private static IEnumerable<PlaceFixturePlacement> Place(int placeId, DecodedMap map, HashSet<int> fixtureEvents, List<PlaceEvent> events)
    {
        if (fixtureEvents.Count == 0) yield break;
        Dictionary<int, string> labels = events
            .Where(placeEvent => placeEvent.PlaceId == placeId)
            .ToDictionary(placeEvent => placeEvent.EventId, placeEvent => placeEvent.Label);

        // Faces in the map's own flattened order, so the grouping — and the cluster numbers — are the same on
        // every run.
        Dictionary<int, List<Group>> groups = [];
        foreach ((int faceIndex, MapFace face, int modelIndex, string modelName) in MapFaceList.Flatten(map))
        {
            if (!Clickable(face) || !fixtureEvents.Contains(face.EventId)) continue;
            if (!groups.TryGetValue(face.EventId, out List<Group>? list)) groups[face.EventId] = list = [];
            (double X, double Y, double Z) centre = MapFaceList.BoxCentre(face);
            Group? joined = list.Find(group => modelIndex >= 0
                ? group.ModelIndex == modelIndex
                : Distance(group.First, centre) <= PlaceContainerEmitter.FaceSpreadLimit);
            if (joined is null)
            {
                joined = new Group(modelIndex >= 0 ? modelIndex : null, modelName, centre);
                list.Add(joined);
            }

            joined.Points.Add(centre);
            joined.FaceIndices.Add(faceIndex);
            joined.Lowest = Math.Min(joined.Lowest, face.Vertices.Count == 0 ? centre.Z : face.Vertices.Min(vertex => (double)vertex.Z));
        }

        foreach ((int eventId, List<Group> list) in groups.OrderBy(entry => entry.Key))
        {
            for (int cluster = 0; cluster < list.Count; cluster++)
            {
                Group group = list[cluster];
                double x = group.Points.Sum(point => point.X) / group.Points.Count;
                double y = group.Points.Sum(point => point.Y) / group.Points.Count;
                // The height is the faces' lowest corner rather than their middle, for the reason a counter stands
                // on the ground: a party reaches a cave mouth, a door or a shrine standing at its foot, and the
                // middle of a door face is half a door up — further above the party's eye than the use's aim
                // admits within its reach.
                double z = group.Lowest;
                yield return new PlaceFixturePlacement(
                    placeId,
                    eventId,
                    cluster,
                    x,
                    y,
                    z,
                    group.Points.Count,
                    group.ModelIndex,
                    group.ModelName,
                    labels.GetValueOrDefault(eventId, string.Empty)) { FaceIndices = group.FaceIndices.ToArray() };
            }
        }
    }

    /// <summary>Whether a face raises an event when the party uses it.</summary>
    private static bool Clickable(MapFace face) =>
        face.EventId != 0 && (face.Attributes & PlaceEntranceEmitter.ClickableAttribute) != 0;

    private static double Distance((double X, double Y, double Z) point, (double X, double Y, double Z) from)
    {
        double dx = point.X - from.X;
        double dy = point.Y - from.Y;
        double dz = point.Z - from.Z;
        return Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }

    /// <summary>One group of the faces raising one event.</summary>
    private sealed class Group(int? modelIndex, string modelName, (double X, double Y, double Z) first)
    {
        public int? ModelIndex { get; } = modelIndex;

        public string ModelName { get; } = modelName;

        public (double X, double Y, double Z) First { get; } = first;

        public List<(double X, double Y, double Z)> Points { get; } = [];

        public List<int> FaceIndices { get; } = [];

        /// <summary>The lowest corner of the group's faces.</summary>
        public double Lowest { get; set; } = double.MaxValue;
    }
}

/// <summary>
/// What a move step of an event is in the product's terms: the travel link it takes and the kind of travel that is,
/// or a reposition within its own place.
/// </summary>
/// <remarks>
/// The link is the place graph's own entry for the instruction — the same program, event and step — so a fixture's
/// move and the transition the product takes are one reading of one instruction. The kind is the one the reaches
/// were always given: between two regions a party walks, and anything else is an entrance.
/// </remarks>
internal sealed class PlaceMoves
{
    private readonly Dictionary<(string Program, int Event, int Step), int> _links = [];
    private readonly PlaceGraph? _graph;
    private readonly IReadOnlyDictionary<int, DecodedMap> _maps;

    internal PlaceMoves(PlaceGraph? graph, IReadOnlyDictionary<int, DecodedMap> maps)
    {
        _graph = graph;
        _maps = maps;
        if (graph is null) return;
        for (int index = 0; index < graph.Links.Count; index++)
        {
            PlaceLink link = graph.Links[index];
            _links.TryAdd((link.SourceEvtName.ToUpperInvariant(), link.EventId, link.Step), index);
        }
    }

    /// <summary>The step with what its move is.</summary>
    /// <param name="step">The step as normalized.</param>
    /// <param name="map">The map whose program holds the move, or null for the global program, which belongs to no map.</param>
    /// <param name="programName">The program's entry name.</param>
    /// <param name="instruction">The move's instruction.</param>
    /// <param name="move">The move, decoded.</param>
    internal PlaceEventStep Describe(PlaceEventStep step, DecodedMap? map, string programName, EvtInstruction instruction, MoveToMapInstruction move)
    {
        if (_graph is not null && _links.TryGetValue((programName.ToUpperInvariant(), instruction.EventId, instruction.Step), out int index))
        {
            int destination = _graph.Links[index].DestinationMapId!.Value;
            bool walking = map?.Kind == MapKind.Outdoor && _maps.TryGetValue(destination, out DecodedMap? arrival) && arrival.Kind == MapKind.Outdoor;
            return step with
            {
                Link = index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ToPlace = destination,
                Travel = map is null ? "scripted" : walking ? "walking" : "entrance",
            };
        }

        // The graph holds every move between two places, so a move it has no link for — with a graph to ask — stays
        // in its own place, as one naming no map or its own map does.
        return _graph is not null || PlaceGraph.IsWithinMap(move.DestinationMapFile)
            ? step with { WithinPlace = true, X = move.X, Y = move.Y, Z = move.Z, Yaw = move.Yaw }
            : step;
    }
}
