using System.Text;
using MightAndMagic7.Import.Events;
using MightAndMagic7.Import.Lod;
using MightAndMagic7.Import.Packs;
using MightAndMagic7.Import.Tables;
using Xunit;

namespace MightAndMagic7.Import.Tests;

/// <summary>
/// The global program — what a person's topic runs — written in the steps a place's events are written in, with
/// which of its events a topic raises and who raises each.
/// </summary>
public sealed class GlobalEventEmissionTests
{
    [Fact]
    public void The_global_programs_events_are_normalized_with_their_offer_check_and_the_topic_texts_they_show()
    {
        string installRoot = SyntheticInstallation.Create(withMaps: false, withPeople: true);
        try
        {
            PersonTable people = Mm7Tables.Read(LodInstall.Open(installRoot)).People;
            GlobalEventSummary summary = GlobalEventEmitter.Emit([EvtProgram.Read("global.evt", Program())], people);

            // Every event is carried; the ones a person's topic raises are the slot the table states (Tester One's
            // first slot raises 1) and the slot a topic change of any program names (event 50 turns slot 0 of
            // Tester Three to 60). Event 70 nothing raises.
            Assert.Equal([1, 50, 60, 70], summary.Events.Select(globalEvent => globalEvent.EventId));
            Assert.Equal([1, 60], summary.TopicRaised);

            // The offer check is written in the donor's words with its comparison, and a message's line is the topic
            // text table's row of its number, because the global program has no string table of its own.
            GlobalEvent first = summary.Events[0];
            Assert.True(first.ChecksOffer);
            Assert.Equal(
                [
                    new PlaceEventStep(0, "can-show-dialog-item-compare") { Variable = "quest-bit", Value = 7, Target = 3 },
                    new PlaceEventStep(1, "set-can-show-dialog-item") { On = false },
                    new PlaceEventStep(2, "end-can-show-dialog-item"),
                    new PlaceEventStep(3, "set-can-show-dialog-item") { On = true },
                    new PlaceEventStep(4, "end-can-show-dialog-item"),
                    new PlaceEventStep(5, "show-message") { TextId = 2, Text = "I have work for you, if you are willing." },
                    new PlaceEventStep(6, "set") { Variable = "quest-bit", Value = 98 },
                    new PlaceEventStep(7, "set-npc-greeting") { Person = 1, Greeting = 2 },
                ],
                first.Steps);
            Assert.False(summary.Events[1].ChecksOffer);

            // Who raises an event is said in words a person can follow to the tables; nothing raising one is said too.
            List<EvtProgram> programs = [EvtProgram.Read("global.evt", Program())];
            Assert.Equal("Tester One's topic (person 1, slot 0)", GlobalEventEmitter.Raisers(1, programs, people));
            Assert.Equal("Tester Three's topic once global.evt event 50 changes slot 0 to it", GlobalEventEmitter.Raisers(60, programs, people));
            Assert.Equal(string.Empty, GlobalEventEmitter.Raisers(70, programs, people));

            // The step kinds counted are the raised events' only, which is what a topic's interpretation is measured by.
            Assert.Equal(1, summary.StepKinds["set quest-bit"]);
            Assert.False(summary.StepKinds.ContainsKey("set-npc-topic"));
        }
        finally
        {
            Directory.Delete(installRoot, recursive: true);
        }
    }

    [Fact]
    public void A_slot_is_what_a_person_is_asked_about_and_a_topic_whose_event_the_program_holds_speaks_without_table_text()
    {
        string installRoot = SyntheticInstallation.Create(withMaps: true, withServices: true, withPeople: true);
        try
        {
            LodInstall install = LodInstall.Open(installRoot);
            Mm7Tables tables = Mm7Tables.Read(install);
            Dictionary<int, Maps.DecodedMap> maps = [];
            foreach (Maps.MapDecodeOutcome outcome in Maps.MapDecoder.DecodeAll(install).Decoded) maps[outcome.Map.Id] = outcome.Decoded!;
            PlaceServiceSummary services = PlaceServiceEmitter.Emit(tables.Services, tables, EvtProgram.ReadAll(install), maps);

            // Without the global program Tester Three's only slot raises a row with no text, which is refused; with
            // the program holding event 3, the row is a topic the event answers.
            PlacePeopleSummary without = PlacePeopleEmitter.Emit(tables.People, maps, services);
            Assert.DoesNotContain(without.People.Single(person => person.Id == "npc-3").Topics, topic => topic.Id == "topic-3");
            PlacePeopleSummary with = PlacePeopleEmitter.Emit(tables.People, maps, services, new HashSet<int> { 3 });
            PlacePersonTopic raised = Assert.Single(with.People.Single(person => person.Id == "npc-3").Topics);
            Assert.Equal(("topic-3", "The stub", string.Empty, 3), (raised.Id, raised.Label, raised.Text, raised.Event));
            Assert.Contains(with.Topics, topic => topic.Id == "topic-3" && topic.Event == 3);

            // Tester One's slots raise rows 1 and 9: row 1 is offered, and a slot whose row the table lacks is refused
            // with its own reason; Tester Two owns row 2 in the table but no slot of theirs raises it.
            Assert.Equal(["topic-1"], with.People.Single(person => person.Id == "npc-1").Topics.Select(topic => topic.Id));
            Assert.Contains(with.Refusals, refusal => refusal.Code == "slot-without-a-topic" && refusal.Subject.Contains("raising 9", StringComparison.Ordinal));
            Assert.Empty(with.People.Single(person => person.Id == "npc-2").Topics);
        }
        finally
        {
            Directory.Delete(installRoot, recursive: true);
        }
    }

    /// <summary>
    /// A global program: a topic's event with an offer check, a line, a quest bit and a greeting change (1); an event that
    /// changes a slot (50) to an event (60); and an event nothing raises (70).
    /// </summary>
    private static byte[] Program() =>
    [
        .. Record(1, 0, EvtOpcodes.CanShowDialogItemCompare, [.. U16(0x10), .. I32(7), 3]),
        .. Record(1, 1, EvtOpcodes.SetCanShowDialogItem, 0),
        .. Record(1, 2, EvtOpcodes.EndCanShowDialogItem, 0),
        .. Record(1, 3, EvtOpcodes.SetCanShowDialogItem, 1),
        .. Record(1, 4, EvtOpcodes.EndCanShowDialogItem, 0),
        .. Record(1, 5, EvtOpcodes.ShowMessage, I32(2)),
        .. Record(1, 6, EvtOpcodes.Set, [.. U16(0x10), .. I32(98)]),
        .. Record(1, 7, EvtOpcodes.SetNpcGreeting, [.. I32(1), .. I32(2)]),
        .. Record(50, 0, EvtOpcodes.SetNpcTopic, [.. I32(3), 0, .. I32(60)]),
        .. Record(60, 0, EvtOpcodes.Exit, 0),
        .. Record(70, 0, EvtOpcodes.Exit, 0),
    ];

    /// <summary>One event record: the size byte, the event, the step, the opcode, and its operands.</summary>
    private static byte[] Record(int eventId, int step, byte opcode, params byte[] operands) =>
        [(byte)(4 + operands.Length), (byte)(eventId & 0xFF), (byte)(eventId >> 8), (byte)step, opcode, .. operands];

    private static byte[] U16(int value) => BitConverter.GetBytes((ushort)value);

    private static byte[] I32(int value) => BitConverter.GetBytes(value);
}
