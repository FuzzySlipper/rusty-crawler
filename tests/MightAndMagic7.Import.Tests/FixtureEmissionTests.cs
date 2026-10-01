using System.Text;
using MightAndMagic7.Import.Events;
using MightAndMagic7.Import.Lod;
using MightAndMagic7.Import.Maps;
using MightAndMagic7.Import.Packs;
using MightAndMagic7.Import.Tables;
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
        Assert.All(summary.Fixtures, fixture => Assert.Equal("Drink from the Well", fixture.Label));

        // The chest's event is the container emitter's and the door's is the door's; the floor trigger is not
        // something the party uses, and the event the program lacks is counted rather than invented.
        Assert.Equal(1, summary.OwnedElsewhere["open-chest"]);
        Assert.Equal(1, summary.OwnedElsewhere["change-door-state"]);
        Assert.Equal(1, summary.RaisedWithoutInstructions);
        Assert.Equal(1, summary.PlaceCount);
        Assert.Equal(1, summary.FixtureEventCount);

        // The timer that refills the well travels with it although nothing raises it.
        Assert.Equal(1, summary.TriggeredEventCount);
        Assert.Equal(["7.300", "7.310"], summary.Events.Select(placeEvent => placeEvent.Id));
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
                new PlaceEventStep(9, "cast-spell"),
                new PlaceEventStep(10, "exit"),
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
        .. Record(300, 9, 21, new byte[28]),
        .. Record(300, 10, EvtOpcodes.Exit, 0),
        .. Record(310, 0, EvtOpcodes.OnLongTimer, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0),
        .. Record(310, 1, EvtOpcodes.Set, [.. U16(0x7B), .. I32(30)]),
        .. Record(176, 0, EvtOpcodes.OpenChest, 0),
        .. Record(303, 0, EvtOpcodes.ChangeDoorState, 1, 2),
        .. Record(303, 1, EvtOpcodes.Exit, 0),
        .. Record(302, 0, EvtOpcodes.Exit, 0),
    ];

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
