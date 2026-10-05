using System.Text;
using MightAndMagic7.Import.Events;
using MightAndMagic7.Import.Lod;
using MightAndMagic7.Import.Maps;
using MightAndMagic7.Import.Packs;
using MightAndMagic7.Import.Tables;
using MightAndMagic7.Import.World;
using Xunit;

namespace MightAndMagic7.Import.Tests;

/// <summary>
/// The fixtures a place's faces raise its own events through, and the events themselves written as steps a
/// ruleset reads: which raised events are a fixture's, where a fixture stands, and what each step says.
/// </summary>
public sealed class FixtureEmissionTests
{
    /// <summary>The donor's attribute for a face the party clicks to raise its event.</summary>
    private const uint Clickable = PlaceEntranceEmitter.ClickableAttribute;

    /// <summary>The donor's attribute for a face the party raises its event by stepping on.</summary>
    private const uint PressurePlate = PlaceEntranceEmitter.PressurePlateAttribute;

    [Fact]
    public void Departure_and_chest_side_effect_programs_retain_their_trigger_and_chest_identity()
    {
        byte[] program = [.. Record(2, 0, 53), .. Record(2, 1, EvtOpcodes.Exit),
            .. Record(181, 0, EvtOpcodes.OpenChest, 4),
            .. Record(181, 1, EvtOpcodes.Add, [.. U16(0x10), .. I32(242)]),
            .. Record(181, 2, EvtOpcodes.Exit),
            .. Record(195, 0, EvtOpcodes.OpenChest, 4),
            .. Record(195, 1, EvtOpcodes.Add, [.. U16(0x10), .. I32(999)])];
        var summary = PlaceFixtureEmitter.Emit(new Dictionary<int, DecodedMap> { [7] = Interior((181, Clickable)) },
            [EvtProgram.Read("d01.evt", program)], new Dictionary<string, MapStrings>());
        Assert.Equal("on-map-leave", summary.Events.Single(e => e.EventId == 2).Steps[0].Op);
        Assert.False(summary.Events.Single(e => e.EventId == 2).Triggered);
        var chest = summary.Events.Single(e => e.EventId == 181);
        Assert.Equal(4, chest.Steps[0].Index);
        Assert.Equal("add", chest.Steps[1].Op);
        Assert.DoesNotContain(summary.Events, e => e.EventId == 195); // No face raises this unrelated chest program.
        Assert.Empty(summary.Fixtures); // The existing container owns the clickable surface.
    }

    [Fact]
    public void Map_entry_events_are_retained_without_a_clicked_face_or_timer()
    {
        byte[] program = [.. Record(1, 0, EvtOpcodes.OnMapReload), .. Record(1, 1, EvtOpcodes.Exit)];
        var summary = PlaceFixtureEmitter.Emit(new Dictionary<int, DecodedMap> { [7] = Interior() },
            [EvtProgram.Read("d01.evt", program)], new Dictionary<string, MapStrings>());
        var entry = Assert.Single(summary.Events);
        Assert.False(entry.Triggered);
        Assert.False(entry.Raised);
        Assert.Equal("on-map-reload", entry.Steps[0].Op);
        Assert.Empty(summary.Fixtures);
    }

    [Fact]
    public void A_shared_event_does_not_make_a_pressure_plate_part_of_a_clicked_fixture_surface()
    {
        PlaceFixtureSummary summary = PlaceFixtureEmitter.Emit(
            new Dictionary<int, DecodedMap> { [7] = Interior((300, Clickable), (300, PressurePlate)) },
            [EvtProgram.Read("d01.evt", Program())],
            new Dictionary<string, MapStrings>(StringComparer.OrdinalIgnoreCase) { ["d01"] = Strings() });
        Assert.Equal([0], Assert.Single(summary.Fixtures).FaceIndices);
        Assert.Contains(summary.Triggers, trigger => trigger.EventId == 300);
    }

    [Fact]
    public void A_clicked_event_no_other_emitter_answers_for_is_a_fixture_standing_where_its_faces_are()
    {
        // Two faces raise the well's event four hundred units apart, which is two wells rather than one between
        // them; one face raises a chest's event, one a door's, one a floor trigger's, and one an event the
        // program does not hold.
        DecodedMap map = Interior(
            (300, Clickable),
            (300, Clickable),
            (176, Clickable),
            (303, Clickable),
            (302, PressurePlate),
            (304, Clickable));
        PlaceFixtureSummary summary = PlaceFixtureEmitter.Emit(
            new Dictionary<int, DecodedMap> { [7] = map },
            [EvtProgram.Read("d01.evt", Program())],
            new Dictionary<string, MapStrings>(StringComparer.OrdinalIgnoreCase) { ["d01"] = Strings() });

        Assert.Equal(2, summary.Fixtures.Count);
        Assert.All(summary.Fixtures, fixture => Assert.Equal(300, fixture.EventId));
        Assert.Equal(["fixture-300", "fixture-300-1"], summary.Fixtures.Select(fixture => fixture.PlacementId));
        Assert.Equal(5, summary.Fixtures[0].X);
        Assert.Equal(405, summary.Fixtures[1].X);
        Assert.Equal([0], summary.Fixtures[0].FaceIndices);
        Assert.Equal([1], summary.Fixtures[1].FaceIndices);
        Assert.All(summary.Fixtures, fixture => Assert.Equal("Drink from the Well", fixture.Label));

        // The chest's event is the container emitter's and the door's is the door's; the floor trigger is not
        // something the party uses, and the event the program lacks is counted rather than invented.
        Assert.Equal(1, summary.OwnedElsewhere["open-chest"]);
        Assert.Equal(1, summary.OwnedElsewhere["change-door-state"]);
        Assert.Equal(1, summary.RaisedWithoutInstructions);
        Assert.Equal(1, summary.PlaceCount);
        Assert.Equal(1, summary.FixtureEventCount);

        // The timer that refills the well travels with it although nothing raises it, and the plate's event — which
        // does nothing but end — is a floor trigger all the same: every plate is.
        Assert.Equal(1, summary.TriggeredEventCount);
        Assert.Equal(["7.300", "7.302", "7.310"], summary.Events.Select(placeEvent => placeEvent.Id));
        Assert.Equal(302, Assert.Single(summary.Triggers).EventId);
    }

    [Fact]
    public void Every_plate_is_a_floor_trigger_with_a_reach_unless_a_counter_or_a_container_answers_for_its_event()
    {
        // A trap (500) that spares an invisible party, harms it and summons ten creatures of the first slot graded A;
        // a plate that only shuts a door (501), which a door answers for only when it is clicked; a plate that opens a
        // chest (502), which is the container's; and a plate whose event the program lacks (503).
        DecodedMap map = Interior(
            (500, PressurePlate),
            (500, PressurePlate),
            (501, PressurePlate),
            (502, PressurePlate),
            (503, PressurePlate));
        IReadOnlyList<EvtProgram> programs = [EvtProgram.Read("d01.evt", TrapProgram())];
        PlaceGraph graph = PlaceGraph.Build(programs, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["d01"] = 7 });
        Dictionary<int, DecodedMap> maps = new() { [7] = map };

        PlaceFixtureSummary fixtures = PlaceFixtureEmitter.Emit(
            maps,
            programs,
            new Dictionary<string, MapStrings>(StringComparer.OrdinalIgnoreCase) { ["d01"] = Strings() },
            graph);

        Assert.Equal([500, 501], fixtures.Triggers.Select(trigger => trigger.EventId));
        Assert.Equal(2, fixtures.Triggers[0].FaceCount);
        Assert.Equal(1, fixtures.SteppedOwnedElsewhere["open-chest"]);
        Assert.Equal(1, fixtures.SteppedWithoutInstructions);
        Assert.Equal(2, fixtures.SteppedEventCount);
        Assert.Empty(fixtures.Fixtures);

        // The trap's steps are carried in the donor's words: the invisibility it compares, the harm, and the summoning
        // with its encounter (slot one plus three times grade one), its count, point, group and unique name. No tables
        // were read, so the slot itself is not resolved here.
        PlaceEvent trap = fixtures.Events.Single(placeEvent => placeEvent.EventId == 500);
        Assert.True(trap.Stepped);
        Assert.False(trap.Raised);
        Assert.Equal(
            [
                new PlaceEventStep(0, "compare") { Variable = "invisible", Value = 0, Target = 3 },
                new PlaceEventStep(1, "receive-damage") { Who = "party", Kind = "fire", Amount = 5 },
                new PlaceEventStep(2, "summon-monsters") { Encounter = 4, Amount = 10, X = 100, Y = -200, Z = 64, Group = 15, UniqueName = 0 },
                new PlaceEventStep(3, "exit"),
            ],
            trap.Steps,
            new StepComparer());
        Assert.True(fixtures.Events.Single(placeEvent => placeEvent.EventId == 501).Stepped);

        // Every plate of a carried event is a reach raising its floor trigger, and no other plate is.
        PlaceEntranceSummary reaches = PlaceEntranceEmitter.Emit(graph, maps, programs);
        Assert.Equal([500, 500, 501], reaches.Entrances.Select(entrance => entrance.EventId));
        Assert.All(reaches.Entrances, entrance => Assert.Empty(entrance.Links));
        Assert.Equal(["trigger-500", "trigger-500", "trigger-501"], reaches.Entrances.Select(entrance => entrance.Raises));
    }

    [Fact]
    public void An_event_is_written_as_steps_in_the_donor_s_words_with_its_text_resolved()
    {
        PlaceFixtureSummary summary = PlaceFixtureEmitter.Emit(
            new Dictionary<int, DecodedMap> { [7] = Interior((300, Clickable)) },
            [EvtProgram.Read("d01.evt", Program())],
            new Dictionary<string, MapStrings>(StringComparer.OrdinalIgnoreCase) { ["d01"] = Strings() });

        // The hint is the event's label rather than a step, because the donor never runs one; every other
        // instruction is a step with the operands it carries, the variables named by family and slot, and the
        // line a status step prints read from the map's own string table.
        PlaceEvent well = summary.Events.Single(placeEvent => placeEvent.EventId == 300);
        Assert.Equal("Drink from the Well", well.Label);
        Assert.True(well.Raised);
        Assert.False(well.Triggered);
        Assert.Equal(
            [
                new PlaceEventStep(0, "compare") { Variable = "map-variable", Index = 0, Value = 1, Target = 2 },
                new PlaceEventStep(1, "status-text") { TextId = 11, Text = "Refreshing!" },
                new PlaceEventStep(2, "add") { Variable = "hit-points", Value = 5 },
                new PlaceEventStep(3, "add") { Variable = "attribute", Which = "luck", Value = 2 },
                new PlaceEventStep(4, "check-season") { Which = "winter", Target = 6 },
                new PlaceEventStep(5, "for-party-member") { Who = "party" },
                new PlaceEventStep(6, "random-go-to") { Targets = [7, 8] },
                new PlaceEventStep(7, "receive-damage") { Who = "active", Kind = "fire", Amount = 20 },
                new PlaceEventStep(8, "add") { Variable = "autonote", Value = 2 },
                new PlaceEventStep(9, "cast-spell") { Spell = 6, Mastery = "master", Rank = 10 },
                new PlaceEventStep(10, "change-door-state") { Door = 4, Action = "toggle" },
                new PlaceEventStep(11, "give-item") { Level = 5, ItemKind = "spell-scroll", ItemSkill = string.Empty },
                new PlaceEventStep(12, "speak-npc") { Person = 283 },
                new PlaceEventStep(13, "set-npc-topic") { Person = 281, Index = 1, Raises = 176 },
                new PlaceEventStep(14, "check-skill") { Which = "perception", Mastery = "master", Rank = 40, Target = 16 },
                new PlaceEventStep(15, "set-faces-bit") { Group = 5, Flag = 0x2000, On = true },
                new PlaceEventStep(16, "exit"),
            ],
            well.Steps,
            new StepComparer());

        PlaceEvent timer = summary.Events.Single(placeEvent => placeEvent.EventId == 310);
        Assert.False(timer.Raised);
        Assert.True(timer.Triggered);
        Assert.Equal("daily", timer.Steps[0].Period);
        Assert.Equal(new PlaceEventStep(1, "set") { Variable = "map-variable", Index = 0, Value = 30 }, timer.Steps[1], new StepComparer());
    }

    [Fact]
    public void A_travel_event_is_a_fixture_when_clicked_and_a_floor_trigger_when_trodden_and_every_link_states_how_it_is_taken()
    {
        // A shrine (400) clicked by the party compares a quest bit and moves it out only when the bit is set; a plate
        // (401) moves it out unconditionally; a plate (402) moves it within its own place; a door (403) both opens a
        // building and moves the party, which the building's own use runs.
        DecodedMap map = Interior(
            (400, Clickable),
            (401, PressurePlate),
            (401, PressurePlate),
            (402, PressurePlate),
            (403, Clickable));
        IReadOnlyList<EvtProgram> programs = [EvtProgram.Read("d01.evt", TravelProgram())];
        PlaceGraph graph = PlaceGraph.Build(programs, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["d01"] = 7, ["out01"] = 1 });
        Dictionary<int, DecodedMap> maps = new() { [7] = map };

        PlaceFixtureSummary fixtures = PlaceFixtureEmitter.Emit(
            maps,
            programs,
            new Dictionary<string, MapStrings>(StringComparer.OrdinalIgnoreCase) { ["d01"] = Strings() },
            graph);

        // The clicked shrine is a fixture the party uses, and its move names the link the graph read it as.
        PlaceFixturePlacement shrine = Assert.Single(fixtures.Fixtures);
        Assert.Equal(400, shrine.EventId);
        PlaceEvent shrineEvent = fixtures.Events.Single(placeEvent => placeEvent.EventId == 400);
        Assert.True(shrineEvent.Raised);
        Assert.False(shrineEvent.Stepped);
        PlaceEventStep move = shrineEvent.Steps.Single(step => step.Op == "move-to-map");
        Assert.Equal("0", move.Link);
        Assert.Equal(1, move.ToPlace);
        Assert.Equal("entrance", move.Travel);
        Assert.Null(move.WithinPlace);

        // The plates' events are floor triggers, one placement per event at the plates' mean; the within-place move
        // is carried as one, named so the ruleset can refuse it by name.
        Assert.Equal([401, 402], fixtures.Triggers.Select(trigger => trigger.EventId));
        Assert.Equal(["trigger-401", "trigger-402"], fixtures.Triggers.Select(trigger => trigger.PlacementId));
        Assert.Equal(2, fixtures.Triggers[0].FaceCount);
        Assert.True(fixtures.Events.Single(placeEvent => placeEvent.EventId == 401).Stepped);
        Assert.True(fixtures.Events.Single(placeEvent => placeEvent.EventId == 402).Steps.Single(step => step.Op == "move-to-map").WithinPlace);

        // The door that opens a building is the house's, never a fixture's; because it does more than open the house,
        // its event is carried for the house's own use to run, with the house its step opens.
        Assert.Equal(1, fixtures.OwnedElsewhere["speak-in-house"]);
        PlaceEvent door = fixtures.Events.Single(placeEvent => placeEvent.EventId == 403);
        Assert.True(door.Housed);
        Assert.False(door.Raised);
        Assert.Equal(98, door.Steps.Single(step => step.Op == "speak-in-house").House);
        Assert.Equal(1, fixtures.HousedEventCount);
        Assert.DoesNotContain(fixtures.Fixtures, fixture => fixture.EventId == 403);

        PlaceEntranceSummary travel = PlaceEntranceEmitter.Emit(graph, maps, programs);

        // Every plate of a travel event is a reach raising its floor trigger; a clicked face is never one.
        Assert.Equal(3, travel.ReachCount);
        Assert.All(travel.Entrances, entrance => Assert.StartsWith("trigger-", entrance.Raises, StringComparison.Ordinal));
        Assert.Equal([1], travel.Entrances.First(entrance => entrance.EventId == 401).Links);
        Assert.Empty(travel.Entrances.First(entrance => entrance.EventId == 402).Links);

        // Every link is accounted for: the shrine's is used under its condition, the plate's walked, and the door's used
        // through its house.
        Assert.Equal(3, travel.Accounts.Count);
        PlaceLinkAccount gated = travel.Accounts[0];
        Assert.Equal(PlaceEntranceEmitter.Used, gated.Disposition);
        Assert.Equal("quest bit 246 is set", gated.Condition);
        Assert.Equal(3, gated.Step);
        PlaceLinkAccount plate = travel.Accounts[1];
        Assert.Equal(PlaceEntranceEmitter.Walked, plate.Disposition);
        Assert.Equal(string.Empty, plate.Condition);
        Assert.Equal(PlaceEntranceEmitter.Used, travel.Accounts[2].Disposition);
        Assert.Contains("a house's door", travel.Accounts[2].Trigger, StringComparison.Ordinal);
        Assert.Equal(3, travel.TakenCount);
        Assert.Equal(1, travel.ConditionalCount);
    }

    [Fact]
    public void An_event_s_paths_name_the_condition_each_step_is_reached_under()
    {
        IReadOnlyList<EvtInstruction> instructions = EvtProgram.Read("d01.evt", TravelProgram()).Instructions
            .Where(instruction => instruction.EventId == 400)
            .ToList();

        IReadOnlyDictionary<int, PlaceEventPath> paths = PlaceEventPaths.Of(instructions);

        // The comparison holds to step 3; it fails into the status line and the exit, and nothing runs past the exit.
        Assert.True(paths[0].IsUnconditional);
        Assert.Equal("quest bit 246 is not set", paths[1].Condition);
        Assert.Equal("quest bit 246 is set", paths[3].Condition);
        Assert.Equal(1, paths[3].Ways);
    }

    [Fact]
    public void A_map_s_string_table_is_its_lines_by_number_trimmed_and_unquoted()
    {
        MapStrings strings = MapStrings.Read("d01.str", Encoding.Latin1.GetBytes(" \0Crate\0\"Quoted\"\0  Padded  \0"));

        Assert.Equal(4, strings.Count);
        Assert.Equal(string.Empty, strings.Line(0));
        Assert.Equal("Crate", strings.Line(1));
        Assert.Equal("Quoted", strings.Line(2));
        Assert.Equal("Padded", strings.Line(3));

        // A line past the table's end reads as nothing, as the donor reads it.
        Assert.Equal(string.Empty, strings.Line(40));
    }

    [Fact]
    public void Message_scrolls_keep_their_item_identity_and_empty_rows()
    {
        string root = SyntheticInstallation.Create();
        try
        {
            var table = MessageScrollTable.Read(LodInstall.Open(root));
            Assert.Equal("A synthetic letter.", table.Texts[700]);
            Assert.Equal(string.Empty, table.Texts[701]);
            Assert.Equal("Events.lod", table.Table.Source.ArchiveName);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void The_discovery_table_reads_its_notes_and_counts_the_rows_that_hold_none()
    {
        string root = SyntheticInstallation.Create();
        try
        {
            DiscoveryTable table = Mm7Tables.Read(LodInstall.Open(root)).Discoveries;

            // A note of each category the shipped table uses, its category word lower-cased because the table
            // spells it both ways; the placeholder row and the empty row are not notes.
            Assert.Equal([1, 2, 4, 5], table.Rows.Select(row => row.Number));
            Assert.Equal(["misc", "stat", "obelisk", "potion"], table.Rows.Select(row => row.Category));
            Assert.Equal("Cure Wounds (Red) = Widoweeps Berries + Empty Bottle.", table.Rows[3].Text);
            Assert.Equal(2, table.SkippedRows);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void The_history_table_reads_each_line_under_the_slot_an_event_writes_with_its_codes_as_named_marks()
    {
        string root = SyntheticInstallation.Create();
        try
        {
            HistoryTable table = Mm7Tables.Read(LodInstall.Open(root)).History;

            // Row one is slot zero: an event's history variable names the slot and the donor reads the row past it.
            Assert.Equal([0, 1], table.Rows.Select(row => row.Slot));
            Assert.Equal(["Forward", "The Contest"], table.Rows.Select(row => row.Title));

            // The day and the characters the table writes as its own codes are named marks the ruleset fills.
            Assert.Equal("{date}  {member:1} and {member:4} took the castle.", table.Rows[1].Text);
            Assert.DoesNotContain('%', table.Rows[1].Text);

            // The numbered row with no line is counted, and the padding after the last row is not a row.
            Assert.Equal(1, table.SkippedRows);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>An interior with one face per (event, attributes) pair, four hundred units apart.</summary>
    private static DecodedMap Interior(params (int Event, uint Attributes)[] faces) =>
        MapDecoder.DecodeIndoor(
            LodFixture.Stored("d01.blv", ContainerDecoderTests.ContainerIndoorPayload([.. faces.Select(face => face.Event)], 400, [.. faces.Select(face => face.Attributes)])),
            LodFixture.Stored("d01.dlv", ContainerDecoderTests.ContainerIndoorDeltaPayload(faces.Length)));

    /// <summary>The map's string table: line 4 is the well's hint and line 11 what a dry well says.</summary>
    private static MapStrings Strings()
    {
        string[] lines = new string[12];
        Array.Fill(lines, string.Empty);
        lines[4] = "Drink from the Well";
        lines[11] = "Refreshing!";
        return MapStrings.Read("d01.str", Encoding.Latin1.GetBytes(string.Join('\0', lines) + "\0"));
    }

    /// <summary>
    /// The program: a well (300) whose steps use every instruction the emission decodes, its daily refill (310),
    /// a container's event (176), a door's (303), and a floor trigger's (302).
    /// </summary>
    private static byte[] Program() =>
    [
        .. Record(300, 0, EvtOpcodes.MouseOver, 4),
        .. Record(300, 0, EvtOpcodes.Compare, [.. U16(0x7B), .. I32(1), 2]),
        .. Record(300, 1, EvtOpcodes.StatusText, I32(11)),
        .. Record(300, 2, EvtOpcodes.Add, [.. U16(0x03), .. I32(5)]),
        .. Record(300, 3, EvtOpcodes.Add, [.. U16(0x26), .. I32(2)]),
        .. Record(300, 4, EvtOpcodes.CheckSeason, 3, 6),
        .. Record(300, 5, EvtOpcodes.ForPartyMember, 5),
        .. Record(300, 6, EvtOpcodes.RandomGoTo, 7, 8, 0, 0, 0, 0),
        .. Record(300, 7, EvtOpcodes.ReceiveDamage, [4, 0, .. I32(20)]),
        .. Record(300, 8, EvtOpcodes.Add, [.. U16(0xDF), .. I32(2)]),
        .. Record(300, 9, EvtOpcodes.CastSpell, [6, 2, 10, .. new byte[24]]),
        .. Record(300, 10, EvtOpcodes.ChangeDoorState, 4, 2),
        .. Record(300, 11, EvtOpcodes.GiveItem, [5, 43, .. I32(0)]),
        .. Record(300, 12, EvtOpcodes.SpeakNpc, I32(283)),
        .. Record(300, 13, EvtOpcodes.SetNpcTopic, [.. I32(281), 1, .. I32(176)]),
        .. Record(300, 14, EvtOpcodes.CheckSkill, [26, 3, .. I32(40), 16]),
        .. Record(300, 15, EvtOpcodes.SetFacesBit, [.. I32(5), .. I32(0x2000), 1]),
        .. Record(300, 16, EvtOpcodes.Exit, 0),
        .. Record(310, 0, EvtOpcodes.OnLongTimer, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0),
        .. Record(310, 1, EvtOpcodes.Set, [.. U16(0x7B), .. I32(30)]),
        .. Record(176, 0, EvtOpcodes.OpenChest, 0),
        .. Record(303, 0, EvtOpcodes.ChangeDoorState, 1, 2),
        .. Record(303, 1, EvtOpcodes.Exit, 0),
        .. Record(302, 0, EvtOpcodes.Exit, 0),
    ];

    /// <summary>
    /// A travel program: a shrine gated on quest bit 246 (400), a plate's plain move (401), a plate's move within its
    /// own place (402), and a building's door that also moves the party (403).
    /// </summary>
    private static byte[] TravelProgram() =>
    [
        .. Record(400, 0, EvtOpcodes.MouseOver, 4),
        .. Record(400, 0, EvtOpcodes.Compare, [.. U16(0x10), .. I32(246), 3]),
        .. Record(400, 1, EvtOpcodes.StatusText, I32(11)),
        .. Record(400, 2, EvtOpcodes.Exit, 0),
        .. Record(400, 3, EvtOpcodes.MoveToMap, Move("out01.odm")),
        .. Record(401, 0, EvtOpcodes.MoveToMap, Move("Out01.odm")),
        .. Record(402, 0, EvtOpcodes.MoveToMap, Move("0")),
        .. Record(403, 0, EvtOpcodes.SpeakInHouse, I32(98)),
        .. Record(403, 1, EvtOpcodes.MoveToMap, Move("out01.odm")),
    ];

    /// <summary>
    /// A trap program: a plate that spares an invisible party, harms it and summons (500), a plate that only shuts a
    /// door (501), and a plate that opens a chest (502).
    /// </summary>
    private static byte[] TrapProgram() =>
    [
        .. Record(500, 0, EvtOpcodes.Compare, [.. U16(0x13A), .. I32(0), 3]),
        .. Record(500, 1, EvtOpcodes.ReceiveDamage, [5, 0, .. I32(5)]),
        .. Record(500, 2, EvtOpcodes.SummonMonsters, [1, 1, 10, .. I32(100), .. I32(-200), .. I32(64), .. I32(15), .. I32(0)]),
        .. Record(500, 3, EvtOpcodes.Exit, 0),
        .. Record(501, 0, EvtOpcodes.ChangeDoorState, 2, 1),
        .. Record(501, 1, EvtOpcodes.Exit, 0),
        .. Record(502, 0, EvtOpcodes.OpenChest, 0),
    ];

    /// <summary>A move's operands: a position, a facing, no house and no picture, and the destination file.</summary>
    private static byte[] Move(string destination) =>
        [.. I32(100), .. I32(200), .. I32(0), .. I32(0), .. I32(0), .. I32(0), 0, 0, .. Encoding.Latin1.GetBytes(destination), 0];

    /// <summary>One event record: the size byte, the event, the step, the opcode, and its operands.</summary>
    private static byte[] Record(int eventId, int step, byte opcode, params byte[] operands) =>
        [(byte)(4 + operands.Length), (byte)(eventId & 0xFF), (byte)(eventId >> 8), (byte)step, opcode, .. operands];

    private static byte[] U16(int value) => BitConverter.GetBytes((ushort)value);

    private static byte[] I32(int value) => BitConverter.GetBytes(value);

    /// <summary>Compares steps by what they say, including the list of targets a record compares by reference.</summary>
    private sealed class StepComparer : IEqualityComparer<PlaceEventStep>
    {
        public bool Equals(PlaceEventStep? x, PlaceEventStep? y) =>
            x is not null && y is not null &&
            x with { Targets = null } == y with { Targets = null } &&
            (x.Targets ?? []).SequenceEqual(y.Targets ?? []);

        public int GetHashCode(PlaceEventStep obj) => HashCode.Combine(obj.Step, obj.Op);
    }
}
