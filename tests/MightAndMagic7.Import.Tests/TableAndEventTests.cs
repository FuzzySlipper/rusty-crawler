using MightAndMagic7.Import.Events;
using MightAndMagic7.Import.Lod;
using MightAndMagic7.Import.Tables;
using MightAndMagic7.Import.World;
using Xunit;

namespace MightAndMagic7.Import.Tests;

/// <summary>The table reader, the event reader, and the graph they produce.</summary>
public sealed class TableAndEventTests
{
    [Fact]
    public void The_table_reader_honours_quotes_tabs_and_newlines_inside_fields()
    {
        const string text = "a\tb\tc\n1\t\"has\ttab\"\t\"line\nbreak\"\n2\t\"quote \"\"inside\"\"\"\tend\n";
        List<string[]> records = TabularTable.SplitRecords(text);

        Assert.Equal(3, records.Count);
        Assert.Equal(["a", "b", "c"], records[0]);
        Assert.Equal(["1", "has\ttab", "line\nbreak"], records[1]);
        Assert.Equal(["2", "quote \"inside\"", "end"], records[2]);
    }

    [Fact]
    public void A_table_shorter_than_its_declared_header_fails_by_name()
    {
        string root = LodFixture.Installation("MMVI", LodFixture.TextTable("MAPSTATS.TXT", "only one row\n"));
        try
        {
            LodFormatException error = Assert.Throws<LodFormatException>(() =>
                TabularTable.Read(LodInstall.Open(root), Mm7TableSources.MapStats, 3));
            Assert.Contains("header rows", error.Message);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Rows_without_an_id_are_partitioned_out_as_annotations()
    {
        string text = "header\n1\tfirst\n\tlegend text\n2\tsecond\n";
        string root = LodFixture.Installation("MMVI", LodFixture.TextTable("CLASS.TXT", text));
        try
        {
            TabularTable table = TabularTable.Read(LodInstall.Open(root), Mm7TableSources.Classes, 1);
            (IReadOnlyList<TabularRow> data, IReadOnlyList<TabularRow> annotations) = table.Partition();
            Assert.Equal(2, data.Count);
            Assert.Single(annotations);
            Assert.Equal("legend text", annotations[0].Field(1));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void A_table_carries_the_provenance_that_proves_which_bytes_it_came_from()
    {
        const string text = "Class\tDescriptions\tNotes\n";
        string root = LodFixture.Installation("MMVI", LodFixture.TextTable("CLASS.TXT", text));
        try
        {
            TabularTable table = TabularTable.Read(LodInstall.Open(root), Mm7TableSources.Classes, 1);
            Assert.Equal("Events.lod", table.Source.ArchiveName);
            Assert.Equal("MMVI", table.ArchiveVersionString);
            Assert.Equal(System.Text.Encoding.Latin1.GetByteCount(text), table.ByteLength);
            Assert.Equal(64, table.Sha256.Length);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void The_text_inventory_separates_text_tables_from_binary_entries_that_carry_the_text_flag()
    {
        // The rules archive marks every entry as a text entry, including its binary tables, so the
        // inventory needs the bytes to agree.
        string root = LodFixture.Installation(
            "MMVI",
            LodFixture.TextTable("CLASS.TXT", "Class\tDescriptions\tNotes\n"),
            ("dmonlist.bin", LodFixture.DeflatedText([0x01, 0x00, 0x02, 0x00])));
        try
        {
            TableInventory inventory = TableInventory.Read(LodInstall.Open(root));
            Assert.Equal(["CLASS.TXT"], inventory.TextTables.Select(table => table.Name));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void An_event_record_that_overruns_the_program_fails_by_name()
    {
        byte[] truncated = [40, 1, 0, 0, 6];
        LodFormatException error = Assert.Throws<LodFormatException>(() => EvtProgram.Read("D01.EVT", truncated));
        Assert.Contains("declares 41 bytes", error.Message);

        byte[] tooShort = [2, 1, 0, 0, 6];
        Assert.Contains("below the minimum", Assert.Throws<LodFormatException>(() => EvtProgram.Read("D01.EVT", tooShort)).Message);
    }

    [Fact]
    public void A_map_move_decodes_its_position_house_and_destination()
    {
        byte[] program = MoveRecord(153, destination: "Out03.odm", x: 2727, y: 400, z: 164, exitPicture: 8);
        EvtProgram parsed = EvtProgram.Read("D01.EVT", program);

        EvtInstruction instruction = Assert.Single(parsed.Instructions);
        Assert.Equal(153, instruction.EventId);
        Assert.Equal(EvtOpcodes.MoveToMap, instruction.Opcode);
        Assert.True(instruction.TryReadMoveToMap(out MoveToMapInstruction move));
        Assert.Equal(2727u, move.X);
        Assert.Equal(400u, move.Y);
        Assert.Equal(164u, move.Z);
        Assert.Equal(8, move.ExitPicture);
        Assert.Equal("Out03.odm", move.DestinationMapFile);
        Assert.False(move.IsWithinMap);
    }

    [Fact]
    public void The_placeholder_destination_means_the_move_stays_on_the_map()
    {
        byte[] program = MoveRecord(153, destination: "0");
        Assert.True(EvtProgram.Read("D01.EVT", program).Instructions[0].TryReadMoveToMap(out MoveToMapInstruction move));
        Assert.True(move.IsWithinMap);

        EvtProgram emptyProgram = EvtProgram.Read("D01.EVT", MoveRecord(153, destination: string.Empty));
        Assert.True(emptyProgram.Instructions[0].TryReadMoveToMap(out MoveToMapInstruction empty));
        Assert.True(empty.IsWithinMap);
    }

    [Fact]
    public void The_graph_links_maps_by_the_destination_each_move_names()
    {
        IReadOnlyDictionary<string, int> maps = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["out01"] = 1,
            ["out02"] = 2,
            ["d01"] = 3,
        };

        PlaceGraph graph = PlaceGraph.Build(
            [
                EvtProgram.Read("OUT01.EVT", [.. MoveRecord(10, "Out02.odm"), .. MoveRecord(11, "0")]),
                EvtProgram.Read("OUT02.EVT", MoveRecord(12, "Out01.odm")),
                EvtProgram.Read("GLOBAL.EVT", MoveRecord(13, "d01.blv")),
            ],
            maps);

        Assert.Equal(3, graph.Links.Count);
        Assert.Equal(2, graph.LinksFromMapPrograms);
        Assert.Equal(1, graph.LinksFromGlobalProgram);
        Assert.Single(graph.WithinMapMoves);
        // Only the link that leaves the map counts as outbound; the within-map move does not.
        Assert.Equal(1, graph.OutboundPerMap[1]);
        Assert.Equal(1, graph.InboundPerMap[2]);
        Assert.Equal(1, graph.InboundPerMap[1]);
        Assert.Equal(1, graph.InboundPerMap[3]);
        Assert.Equal(["GLOBAL.EVT"], graph.ProgramsWithoutAMap);
    }

    [Fact]
    public void A_destination_that_is_not_a_known_map_fails_the_build()
    {
        IReadOnlyDictionary<string, int> maps = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["out01"] = 1 };
        LodFormatException error = Assert.Throws<LodFormatException>(() => PlaceGraph.Build(
            [EvtProgram.Read("OUT01.EVT", MoveRecord(10, "Out99.odm"))],
            maps));

        Assert.Contains("Out99.odm", error.Message);
    }

    /// <summary>A program carrying one move record with the given destination.</summary>
    private static byte[] MoveRecord(
        ushort eventId,
        string destination,
        uint x = 0,
        uint y = 0,
        uint z = 0,
        byte exitPicture = 0)
    {
        byte[] name = System.Text.Encoding.Latin1.GetBytes(destination);
        // Six 32-bit operands plus the house and exit bytes plus the name and its terminator, and the
        // size byte counts everything after itself.
        int payloadLength = 31 + name.Length;
        byte[] record = new byte[payloadLength + 1];
        record[0] = (byte)payloadLength;
        record[1] = (byte)(eventId & 0xFF);
        record[2] = (byte)(eventId >> 8);
        record[3] = 0;
        record[4] = EvtOpcodes.MoveToMap;
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(5), x);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(9), y);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(13), z);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(17), 0);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(21), 0);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(25), 0);
        record[29] = 0;
        record[30] = exitPicture;
        name.CopyTo(record, 31);
        return record;
    }
}
