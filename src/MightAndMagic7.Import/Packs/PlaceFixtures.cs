using MightAndMagic7.Import.Events;
using MightAndMagic7.Import.Maps;

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
    /// <summary>The event's content identity, which a fixture's placement and the ruleset name it by.</summary>
    public string Id => $"{PlaceId}.{EventId}";
}

/// <summary>One fixture a place holds: something whose use raises one of the place's own events.</summary>
/// <param name="PlaceId">The place it stands in.</param>
/// <param name="EventId">The event its use raises.</param>
/// <param name="Cluster">Which group of the event's faces it is, counting from zero, when the event's faces stand apart.</param>
/// <param name="X">The mean of its faces' box centres along the place's first axis.</param>
/// <param name="Y">The mean along the second axis.</param>
/// <param name="Z">The mean in height.</param>
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
}

/// <summary>What an import's fixture derivation produced, over every map it decoded.</summary>
/// <param name="Fixtures">Every fixture placement, in place, event and cluster order.</param>
/// <param name="Events">Every event the places' content carries, in place and event order.</param>
/// <param name="OwnedElsewhere">
/// How many raised events another emitter answers for, by its word: <c>move-to-map</c> (a transition's reach),
/// <c>speak-in-house</c> (a counter), <c>open-chest</c> (a container).
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

    /// <summary>How many events a fixture raises.</summary>
    public int FixtureEventCount => Events.Count(placeEvent => placeEvent.Raised);

    /// <summary>How many carried events hold a timer.</summary>
    public int TriggeredEventCount => Events.Count(placeEvent => placeEvent.Triggered);

    /// <summary>How many places hold a fixture.</summary>
    public int PlaceCount => Fixtures.Select(fixture => fixture.PlaceId).Distinct().Count();

    /// <summary>
    /// How many steps of the fixtures' own events there are of each kind, a variable step counted under its
    /// instruction and its variable family together (<c>add autonote</c>).
    /// </summary>
    /// <remarks>
    /// This is what the ruleset's interpretation is measured against: every kind here is either interpreted
    /// or refused by name, and the counts say how much of the shipped programs each answer covers.
    /// </remarks>
    public IReadOnlyDictionary<string, int> StepKinds =>
        Events.Where(placeEvent => placeEvent.Raised)
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
/// one that moves the party (a transition's reach), one that opens a building (a counter), and one that opens a
/// container — and those are left to it. Every other raised event is a fixture's: a well, a fountain, an
/// obelisk, a sign, a lever, a crate.
/// </para>
/// <para>
/// <b>Where a fixture stands.</b> Like a container, a fixture has no position of its own, so it stands at the
/// mean of the bounding-box centres of the faces that raise its event (the donor's own reading of a face,
/// <c>src/Engine/Objects/Chest.cpp:396-402</c>). One event raised by faces in two places — the same signpost
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
    /// <returns>What was derived.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static PlaceFixtureSummary Emit(
        IReadOnlyDictionary<int, DecodedMap> maps,
        IReadOnlyList<EvtProgram> programs,
        IReadOnlyDictionary<string, MapStrings> strings)
    {
        ArgumentNullException.ThrowIfNull(maps);
        ArgumentNullException.ThrowIfNull(programs);
        ArgumentNullException.ThrowIfNull(strings);

        Dictionary<string, EvtProgram> byStem = new(StringComparer.OrdinalIgnoreCase);
        foreach (EvtProgram program in programs) byStem.TryAdd(Path.GetFileNameWithoutExtension(program.Name), program);

        List<PlaceFixturePlacement> fixtures = [];
        List<PlaceEvent> events = [];
        SortedDictionary<string, int> owned = new(StringComparer.Ordinal);
        int withoutInstructions = 0;
        int withoutProgram = 0;
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
                    continue;
                }

                fixtureEvents.Add(eventId);
            }

            foreach ((int eventId, List<EvtInstruction> instructions) in byEvent.OrderBy(entry => entry.Key))
            {
                bool isFixture = fixtureEvents.Contains(eventId);
                bool triggered = instructions.Any(instruction => instruction.Opcode is EvtOpcodes.OnTimer or EvtOpcodes.OnLongTimer);
                if (!isFixture && !(triggered && Owner(instructions) is null)) continue;
                events.Add(Normalize(placeId, map.FileName, eventId, instructions, text, isFixture, triggered));
            }

            foreach (PlaceFixturePlacement fixture in Place(placeId, map, fixtureEvents, events)) fixtures.Add(fixture);
        }

        return new PlaceFixtureSummary(fixtures, events, owned, withoutInstructions, withoutProgram);
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
    /// A move is checked first because a transition's event often prints a line or checks a bit before it
    /// moves the party, and the move is what the event is for; a building's and a container's come after.
    /// </remarks>
    private static string? Owner(List<EvtInstruction> instructions)
    {
        if (instructions.Any(instruction => instruction.Opcode == EvtOpcodes.MoveToMap)) return EvtOpcodes.Word(EvtOpcodes.MoveToMap);
        if (instructions.Any(instruction => instruction.Opcode == EvtOpcodes.SpeakInHouse)) return EvtOpcodes.Word(EvtOpcodes.SpeakInHouse);
        if (instructions.Any(instruction => instruction.Opcode == EvtOpcodes.OpenChest)) return EvtOpcodes.Word(EvtOpcodes.OpenChest);

        // An event that does nothing but move doors is a door's: the face a party clicks to open a door, or a
        // switch beside it, raises an event whose only work is the door's own change of state, and the door
        // record that change names is already a placement the party opens directly. Writing the face as a
        // fixture too would put two targets on one doorway that do one thing.
        if (instructions.Any(instruction => instruction.Opcode == EvtOpcodes.ChangeDoorState) &&
            instructions.All(instruction => instruction.Opcode is EvtOpcodes.ChangeDoorState or EvtOpcodes.Exit or EvtOpcodes.MouseOver or EvtOpcodes.PlaySound))
        {
            return EvtOpcodes.Word(EvtOpcodes.ChangeDoorState);
        }

        return null;
    }

    /// <summary>One event, normalized.</summary>
    private static PlaceEvent Normalize(
        int placeId,
        string fileName,
        int eventId,
        List<EvtInstruction> instructions,
        MapStrings? strings,
        bool raised,
        bool triggered)
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

            steps.Add(Step(instruction, strings));
        }

        return new PlaceEvent(placeId, fileName, eventId, label, steps, raised, triggered);
    }

    /// <summary>One instruction as the step a pack carries.</summary>
    private static PlaceEventStep Step(EvtInstruction instruction, MapStrings? strings)
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
            return step with { TextId = textId, Text = strings?.Line(textId) ?? string.Empty };
        }

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
        foreach ((int _, MapFace face, int modelIndex, string modelName) in MapFaceList.Flatten(map))
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
        }

        foreach ((int eventId, List<Group> list) in groups.OrderBy(entry => entry.Key))
        {
            for (int cluster = 0; cluster < list.Count; cluster++)
            {
                Group group = list[cluster];
                double x = group.Points.Sum(point => point.X) / group.Points.Count;
                double y = group.Points.Sum(point => point.Y) / group.Points.Count;
                double z = group.Points.Sum(point => point.Z) / group.Points.Count;
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
                    labels.GetValueOrDefault(eventId, string.Empty));
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
    }
}
