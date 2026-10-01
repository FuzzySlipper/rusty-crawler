using System.Globalization;
using MightAndMagic7.Import.Events;
using MightAndMagic7.Import.Maps;
using MightAndMagic7.Import.Tables;
using MightAndMagic7.Import.World;

namespace MightAndMagic7.Import.Packs;

/// <summary>One event of the global program, with its normalized steps.</summary>
/// <param name="EventId">The event's number, which is also the topic table row whose label a person's slot shows.</param>
/// <param name="Steps">Its steps, in program order.</param>
public sealed record GlobalEvent(int EventId, IReadOnlyList<PlaceEventStep> Steps)
{
    /// <summary>The event's content identity.</summary>
    public string Id => EventId.ToString(CultureInfo.InvariantCulture);

    /// <summary>Whether the event states whether a topic raising it is offered, which a topic's offer reads.</summary>
    public bool ChecksOffer => Steps.Any(step => step.Op is "can-show-dialog-item-compare" or "set-can-show-dialog-item");
}

/// <summary>What the global program's emission produced.</summary>
/// <param name="Events">Every event the program holds, in event order.</param>
/// <param name="TopicRaised">
/// The events a person's topic raises: a slot the people table states, or one a step of any program changes a slot
/// to; in event order.
/// </param>
public sealed record GlobalEventSummary(IReadOnlyList<GlobalEvent> Events, IReadOnlyList<int> TopicRaised)
{
    /// <summary>An import that read no global program.</summary>
    public static GlobalEventSummary Empty { get; } = new([], []);

    /// <summary>The numbers of every event carried.</summary>
    public IReadOnlySet<int> Numbers => Events.Select(globalEvent => globalEvent.EventId).ToHashSet();

    /// <summary>
    /// How many steps of the events a topic raises there are of each kind, a variable step counted under its
    /// instruction and its variable family together, which is what the ruleset's interpretation is measured against.
    /// </summary>
    public IReadOnlyDictionary<string, int> StepKinds
    {
        get
        {
            HashSet<int> raised = [.. TopicRaised];
            return Events.Where(globalEvent => raised.Contains(globalEvent.EventId))
                .SelectMany(globalEvent => globalEvent.Steps)
                .GroupBy(step => step.Variable is { } variable ? $"{step.Op} {variable}" : step.Op, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        }
    }
}

/// <summary>
/// Normalizes the global program — the events a person's topic raises — into steps a ruleset reads, the same steps a
/// place's own events are written as.
/// </summary>
/// <remarks>
/// <para>
/// <b>Where the global program is run from.</b> A person's six dialogue slots each name an event number; the slot is
/// offered under the topic table's row of that number (OpenEnroth <c>src/GUI/UI/NPCTopics.cpp:546-559</c>) when the
/// event's offer check allows it (<c>:593-619</c>, <c>src/Engine/Evt/Processor.cpp:177-189</c>, <c>src/Engine/Evt/EvtInterpreter.cpp:632-655</c>), and choosing it runs
/// the event from the global program rather than the map's (<c>src/GUI/UI/NPCTopics.cpp:662-666</c>,
/// <c>src/Engine/Evt/Processor.cpp:158-161</c>). The slots of a few numbers are the donor's own screens rather than
/// events — a teacher's offer (200 to 310), a guild's (400 to 410), the oracle (139) and the arena (399)
/// (<c>NPCTopics.cpp:649-661</c>) — and the shipped program holds no event of any of those numbers, so carrying what
/// it holds carries none of them.
/// </para>
/// <para>
/// <b>What a step's text is.</b> The global program has no string table of its own: a message it shows while a topic
/// runs is the topic text table's row of that number (<c>src/Engine/Evt/EvtInterpreter.cpp:403-423</c> reads
/// <c>pNPCTopics[text_id - 1].pText</c>, and <c>src/Engine/Tables/NPCTable.cpp:26-33</c> stores text row <c>n</c> at
/// <c>n - 1</c>), so the line is resolved from <c>npctext.txt</c> here.
/// </para>
/// <para>
/// <b>What a move is.</b> The global program belongs to no map, so a move of it is a link the world issues
/// (<see cref="PlaceGraph"/>), written as <c>scripted</c> travel. Nothing is interpreted here, as for a place's events.
/// </para>
/// </remarks>
public static class GlobalEventEmitter
{
    /// <summary>The global program's entry name in the rules archive.</summary>
    public const string ProgramName = "global.evt";

    /// <summary>Normalizes every event of the global program and states which of them a person's topic raises.</summary>
    /// <param name="programs">Every event program the installation carries.</param>
    /// <param name="people">The people tables: the slots a topic raises, and the text a message step shows.</param>
    /// <param name="graph">The travel links, which a move step names its link by; without one a move names none.</param>
    /// <param name="maps">The decoded maps, which a move's destination is read against.</param>
    /// <returns>What was derived; <see cref="GlobalEventSummary.Empty"/> when the installation holds no global program.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public static GlobalEventSummary Emit(
        IReadOnlyList<EvtProgram> programs,
        PersonTable people,
        PlaceGraph? graph = null,
        IReadOnlyDictionary<int, DecodedMap>? maps = null)
    {
        ArgumentNullException.ThrowIfNull(programs);
        ArgumentNullException.ThrowIfNull(people);
        EvtProgram? program = programs.FirstOrDefault(candidate => string.Equals(candidate.Name, ProgramName, StringComparison.OrdinalIgnoreCase));
        if (program is null) return GlobalEventSummary.Empty;

        PlaceMoves moves = new(graph, maps ?? new Dictionary<int, DecodedMap>());
        List<GlobalEvent> events = [];
        foreach (IGrouping<ushort, EvtInstruction> group in program.Instructions.GroupBy(instruction => instruction.EventId).OrderBy(group => group.Key))
        {
            List<PlaceEventStep> steps = [];
            foreach (EvtInstruction instruction in group)
            {
                // A hint is never a step the donor runs (src/Engine/Evt/EvtInstruction.cpp:919-924); the global
                // program holds none, and one would be a label nothing shows.
                if (instruction.Opcode == EvtOpcodes.MouseOver) continue;
                PlaceEventStep step = PlaceFixtureEmitter.Step(instruction, id => people.Text(id)?.Text ?? string.Empty);
                steps.Add(instruction.TryReadMoveToMap(out MoveToMapInstruction move) ? moves.Describe(step, null, program.Name, instruction, move) : step);
            }

            events.Add(new GlobalEvent(group.Key, steps));
        }

        return new GlobalEventSummary(events, [.. TopicRaised(programs, people, events.Select(globalEvent => globalEvent.EventId).ToHashSet())]);
    }

    /// <summary>
    /// The events a person's topic raises: every slot the people table states, and every slot a topic change of any
    /// program makes raise an event, that the global program holds.
    /// </summary>
    private static SortedSet<int> TopicRaised(IReadOnlyList<EvtProgram> programs, PersonTable people, HashSet<int> carried)
    {
        SortedSet<int> raised = [];
        foreach (NpcRecord npc in people.Npcs)
        {
            foreach (int slot in npc.DialogueSlots)
            {
                if (carried.Contains(slot)) raised.Add(slot);
            }
        }

        foreach (EvtProgram program in programs)
        {
            foreach (EvtInstruction instruction in program.Instructions)
            {
                if (instruction.TryReadNpcTopic(out NpcTopicInstruction topic) && carried.Contains(topic.Event)) raised.Add(topic.Event);
            }
        }

        return raised;
    }

    /// <summary>
    /// Who raises a global event, in words: the people whose slot states it, and the topic changes that make a slot
    /// raise it; empty when nothing does.
    /// </summary>
    /// <param name="eventId">The global event.</param>
    /// <param name="programs">Every event program the installation carries.</param>
    /// <param name="people">The people tables.</param>
    public static string Raisers(int eventId, IReadOnlyList<EvtProgram> programs, PersonTable people)
    {
        ArgumentNullException.ThrowIfNull(programs);
        ArgumentNullException.ThrowIfNull(people);
        List<string> said = [];
        foreach (NpcRecord npc in people.Npcs)
        {
            int slot = npc.DialogueSlots.ToList().IndexOf(eventId);
            if (slot >= 0) said.Add(string.Create(CultureInfo.InvariantCulture, $"{npc.Name}'s topic (person {npc.Id}, slot {slot})"));
        }

        foreach (EvtProgram program in programs)
        {
            foreach (EvtInstruction instruction in program.Instructions)
            {
                if (!instruction.TryReadNpcTopic(out NpcTopicInstruction topic) || topic.Event != eventId) continue;
                string name = people.Npc(topic.Person)?.Name ?? string.Create(CultureInfo.InvariantCulture, $"person {topic.Person}");
                string change = string.Create(CultureInfo.InvariantCulture, $"{name}'s topic once {program.Name} event {instruction.EventId} changes slot {topic.Slot} to it");
                if (!said.Contains(change, StringComparer.Ordinal)) said.Add(change);
            }
        }

        return string.Join("; ", said);
    }
}
