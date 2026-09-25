using System.Text;
using MightAndMagic7.Import.Events;
using MightAndMagic7.Import.Lod;
using MightAndMagic7.Import.Maps;
using MightAndMagic7.Import.Packs;
using Xunit;

namespace MightAndMagic7.Import.Tests;

/// <summary>
/// Containers: what a delta's chest records and sprite objects say, and how a place turns them into
/// something a party can walk up to and open.
/// </summary>
/// <remarks>
/// The payloads here are built to the layout the format spec records, and they are built so that a
/// container's derived position can be checked by hand: every face is a small triangle whose bounding box
/// is known, so the position a container is placed at is either exactly that box's centre or a defect.
/// </remarks>
public sealed class ContainerDecoderTests
{
    private static readonly PlaceTrapNumbers Traps = new(4, 2);

    [Fact]
    public void A_delta_decodes_the_chests_and_the_objects_a_map_holds()
    {
        MapDelta delta = Delta();

        Assert.Equal(2, delta.ChestCount);
        Assert.Equal(1, delta.SpriteObjectCount);

        // The first chest is trapped and holds a named item and a random reference; the second is a record
        // the map never uses, which is why it is a slot with nothing in it rather than a container.
        MapChest trapped = delta.Chests[0];
        Assert.Equal(0, trapped.TypeId);
        Assert.Equal(1, trapped.Flags);
        Assert.True(trapped.IsTrapped);
        Assert.False(trapped.IsItemsPlaced);
        Assert.False(trapped.IsOpened);
        Assert.Equal([new MapChestItem(0, 220), new MapChestItem(3, -3)], trapped.Items);

        MapChestItem random = trapped.Items[1];
        Assert.True(random.IsRandom);
        Assert.Equal(3, random.TreasureLevel);
        Assert.False(trapped.Items[0].IsRandom);
        Assert.Equal(0, trapped.Items[0].TreasureLevel);

        MapChest empty = delta.Chests[1];
        Assert.Equal(0, empty.Flags);
        Assert.False(empty.IsTrapped);
        Assert.Empty(empty.Items);

        // The object standing in the map is the item in it: its own position, its own sector, and the item
        // identifier the donor resolves when it reconstructs one whose item is not a missile.
        MapSpriteObject held = Assert.Single(delta.SpriteObjects);
        Assert.Equal(0, held.Index);
        Assert.Equal(76, held.SpriteId);
        Assert.Equal(77, held.ObjectDescId);
        Assert.Equal(new MapPoint(512, 14368, 0), held.Position);
        Assert.Equal(0, held.YawAngle);
        Assert.Equal(0, held.Attributes);
        Assert.Equal(12, held.SectorId);
        Assert.Equal(220, held.ContainingItemId);
    }

    [Fact]
    public void A_container_stands_where_the_face_that_opens_it_stands()
    {
        DecodedMap map = Map(176);
        PlaceContainerSummary summary = PlaceContainerEmitter.Emit(
            new Dictionary<int, DecodedMap> { [7] = map },
            [Program(("a", 176, 0))],
            new Dictionary<int, PlaceTrapNumbers> { [7] = Traps });

        // One face opens container 0, and the container is placed at that face's own bounding-box centre:
        // a one-face container has nowhere else to be, and its spread is zero.
        PlaceChestPlacement container = Assert.Single(summary.Chests);
        Assert.Equal(7, container.PlaceId);
        Assert.Equal(0, container.ChestIndex);
        Assert.Equal(5, container.X);
        Assert.Equal(5, container.Y);
        Assert.Equal(5, container.Z);
        Assert.Equal(1, container.SourceFaceCount);
        Assert.Equal(0, container.FaceSpread);
        Assert.Equal(Traps.Difficulty, container.TrapDifficulty);
        Assert.Equal(Traps.DamageDice, container.TrapDamageDice);
        Assert.True(container.Chest.IsTrapped);

        // The record nothing opens is a slot, and the object the delta holds is emitted where it stands
        // rather than placed from any face's geometry.
        Assert.Equal(1, summary.UnplacedRecords);
        Assert.Empty(summary.Refusals);
        PlaceSpriteObjectPlacement pile = Assert.Single(summary.SpriteObjects);
        Assert.Equal(7, pile.PlaceId);
        Assert.Equal(new MapPoint(512, 14368, 0), pile.Object.Position);
        Assert.Equal(1, summary.ContainerCount);
        Assert.Equal(1, summary.TrappedCount);
        Assert.Equal(1, summary.StockedCount);
        Assert.Equal(2, summary.ItemReferenceCount);
        Assert.Equal(1, summary.RandomItemReferenceCount);
    }

    [Fact]
    public void Two_faces_that_open_one_container_place_it_between_them_only_when_they_are_close()
    {
        // Both faces open container 0, so the container's position is the mean of the two box centres: the
        // donor's own reading of a container a map draws with more than one face.
        DecodedMap near = Map(176, 176);
        PlaceContainerSummary close = PlaceContainerEmitter.Emit(
            new Dictionary<int, DecodedMap> { [7] = near },
            [Program(("a", 176, 0))],
            new Dictionary<int, PlaceTrapNumbers> { [7] = Traps });

        PlaceChestPlacement placed = Assert.Single(close.Chests);
        Assert.Equal(2, placed.SourceFaceCount);
        Assert.Equal(55, placed.X);
        Assert.Equal(5, placed.Y);
        Assert.Equal(5, placed.Z);
        Assert.Equal(50, placed.FaceSpread);
        Assert.Empty(close.Refusals);

        // The same two events on faces a thousand units apart are not one container: the donor refuses to
        // place what it calls a wormhole chest, and so does this, naming the spread it measured.
        DecodedMap far = MapWithSpacing(1000, 176, 176);
        PlaceContainerSummary spread = PlaceContainerEmitter.Emit(
            new Dictionary<int, DecodedMap> { [7] = far },
            [Program(("a", 176, 0))],
            new Dictionary<int, PlaceTrapNumbers> { [7] = Traps });

        Assert.Empty(spread.Chests);
        PlaceContainerRefusal refusal = Assert.Single(spread.Refusals);
        Assert.Equal("faces-spread", refusal.Code);
        Assert.Equal(0, refusal.ChestIndex);
        Assert.Contains("500", refusal.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void A_face_that_opens_a_container_the_delta_does_not_carry_is_refused_by_name()
    {
        PlaceContainerSummary summary = PlaceContainerEmitter.Emit(
            new Dictionary<int, DecodedMap> { [7] = Map(176) },
            [Program(("a", 176, 5))],
            new Dictionary<int, PlaceTrapNumbers> { [7] = Traps });

        Assert.Empty(summary.Chests);
        PlaceContainerRefusal refusal = Assert.Single(summary.Refusals);
        Assert.Equal("record-missing", refusal.Code);
        Assert.Equal(5, refusal.ChestIndex);
        Assert.Contains("carries 2 containers", refusal.Reason, StringComparison.Ordinal);

        // Both of the delta's records are slots here, because the only event that opens anything names a
        // container that is not in the delta.
        Assert.Equal(2, summary.UnplacedRecords);
    }

    [Fact]
    public void A_place_with_no_event_program_is_refused_rather_than_placed_from_nothing()
    {
        PlaceContainerSummary summary = PlaceContainerEmitter.Emit(
            new Dictionary<int, DecodedMap> { [7] = Map(176) },
            [],
            new Dictionary<int, PlaceTrapNumbers> { [7] = Traps });

        Assert.Empty(summary.Chests);
        PlaceContainerRefusal refusal = Assert.Single(summary.Refusals);
        Assert.Equal("program-not-found", refusal.Code);
        Assert.Null(refusal.ChestIndex);
        Assert.Equal(2, summary.UnplacedRecords);

        // The object the delta holds is still emitted: where it stands is the record's own, and no event
        // program has anything to do with it.
        Assert.Single(summary.SpriteObjects);
    }

    [Fact]
    public void A_place_whose_containers_have_no_trap_numbers_is_a_defect_and_not_a_harmless_chest()
    {
        // Writing a container whose trap numbers are unknown would state that its chests are harmless, so
        // the emission fails instead of choosing a number.
        Assert.Throws<LodFormatException>(() => PlaceContainerEmitter.Emit(
            new Dictionary<int, DecodedMap> { [7] = Map(176) },
            [Program(("a", 176, 0))],
            new Dictionary<int, PlaceTrapNumbers>()));
    }

    [Fact]
    public void An_outdoor_map_decodes_its_containers_and_objects_the_same_way()
    {
        // A region's delta carries the same arrays, and its chests are placed from its own model faces: the
        // fixture's face raises no event, so its four records are slots and only the loose object stands.
        OutdoorMap map = MapDecoder.DecodeOutdoor(
            Payload("out01.odm", MapDecoderTests.OutdoorPayload()),
            Payload("out01.ddm", MapDecoderTests.OutdoorDeltaPayload()));
        MapDelta delta = Assert.IsType<MapDelta>(map.Delta);
        Assert.Equal(4, delta.ChestCount);
        Assert.Single(delta.SpriteObjects);

        PlaceContainerSummary summary = PlaceContainerEmitter.Emit(
            new Dictionary<int, DecodedMap> { [1] = map },
            [Program(("a", 176, 0))],
            new Dictionary<int, PlaceTrapNumbers> { [1] = Traps });

        Assert.Empty(summary.Chests);
        Assert.Equal(4, summary.UnplacedRecords);
        PlaceSpriteObjectPlacement pile = Assert.Single(summary.SpriteObjects);
        Assert.Equal(1, pile.PlaceId);
        Assert.Equal(0, pile.Object.ContainingItemId);
        Assert.Equal(0, summary.StockedSpriteObjectCount);
    }

    [Fact]
    public void A_chest_program_is_read_as_the_container_opens_it_and_not_as_a_move()
    {
        EvtProgram program = EvtProgram.Read("d01.evt", ChestProgram("a", (176, 3)));

        OpenChestInstruction? open = null;
        foreach (EvtInstruction instruction in program.Instructions)
        {
            if (instruction.TryReadOpenChest(out OpenChestInstruction read)) open = read;
        }

        Assert.Equal(new OpenChestInstruction(3), open);
        Assert.DoesNotContain(program.Instructions, instruction => instruction.TryReadMoveToMap(out _));
    }

    /// <summary>The indoor payload the fixture holds, with one face per event it is given.</summary>
    /// <param name="faceEventIds">The event each face raises, in face order.</param>
    /// <param name="spacing">How far apart the faces stand along the level's first axis.</param>
    internal static byte[] ContainerIndoorPayload(IReadOnlyList<int> faceEventIds, int spacing = 100)
    {
        DeltaWriter writer = new();
        writer.U32(1);
        writer.Text("No Name Level", 100);
        int sizes = writer.Length;
        writer.U32(0).U32(0).U32(0).U32(0);
        writer.Zero(16);

        // Three vertices per face, laid out so that face index's bounding box centre is (5 + index*spacing,
        // 5, 5): a position this suite can state by hand.
        writer.U32((uint)(faceEventIds.Count * 3));
        for (int index = 0; index < faceEventIds.Count; index++)
        {
            int x = index * spacing;
            writer.I16((short)x).I16(0).I16(0);
            writer.I16((short)(x + 10)).I16(0).I16(0);
            writer.I16((short)x).I16(10).I16(10);
        }

        writer.U32((uint)faceEventIds.Count);
        int face = writer.Length;
        writer.Zero(96 * faceEventIds.Count);
        for (int index = 0; index < faceEventIds.Count; index++)
        {
            int record = face + (index * 96);
            writer.SetU16(record + 0x48, (ushort)index);    // this face's own extra
            writer.SetU16(record + 0x4A, 0xFFFF);
            writer.SetU16(record + 0x4C, 1);                // sector 1
            writer.SetI16(record + 0x4E, 0);
            writer.SetU8(record + 0x5C, 3);                 // floor
            writer.SetU8(record + 0x5D, 3);                 // three corners
        }

        // The shared face data pool: six arrays of three values and a closing slot, per face in face order.
        for (int index = 0; index < faceEventIds.Count; index++)
        {
            int first = index * 3;
            writer.I16((short)first).I16((short)(first + 1)).I16((short)(first + 2)).I16(0);
            for (int array = 0; array < 3; array++) writer.I16(0).I16(0).I16(0).I16(0);
            writer.I16(0).I16(10).I16(10).I16(0);
            writer.I16(0).I16(0).I16(10).I16(0);
        }

        for (int index = 0; index < faceEventIds.Count; index++) writer.Text("Cfb1", 10);

        writer.U32((uint)faceEventIds.Count);
        int extra = writer.Length;
        writer.Zero(36 * faceEventIds.Count);
        for (int index = 0; index < faceEventIds.Count; index++)
        {
            writer.SetU16(extra + (index * 36) + 0x1A, (ushort)faceEventIds[index]);
        }

        for (int index = 0; index < faceEventIds.Count; index++) writer.Text(string.Empty, 10);

        // Two sectors, both empty: the face names sector 1, and sector 0 is the level's own pseudo-sector.
        writer.U32(2);
        writer.Zero(116 * 2);

        writer.U32(0);                          // door capacity
        writer.U32(0);                          // decorations
        writer.U32(0);                          // lights
        writer.U32(0);                          // BSP nodes
        writer.U32(0);                          // spawn points
        writer.U32(0);                          // map outlines

        writer.SetU32(sizes, (uint)(faceEventIds.Count * 6 * 4 * sizeof(short)));
        writer.SetU32(sizes + 4, 0);
        writer.SetU32(sizes + 8, 0);
        writer.SetU32(sizes + 12, 0);
        return writer.ToArray();
    }

    /// <summary>The delta of <see cref="ContainerIndoorPayload"/>: two chests and one loose object.</summary>
    internal static byte[] ContainerIndoorDeltaPayload(int faceCount = 1)
    {
        DeltaWriter writer = new();
        writer.Zero(40);                        // header
        writer.Zero(875);                       // visible outlines
        writer.Zero(faceCount * sizeof(uint));  // face attributes, one per level face
        writer.U32(0);                          // actors

        writer.U32(1);                          // one sprite object
        int held = writer.Length;
        writer.Zero(0x70);
        writer.SetU16(held + 0x00, 76);
        writer.SetU16(held + 0x02, 77);
        writer.SetI32(held + 0x04, 512).SetI32(held + 0x08, 14368).SetI32(held + 0x0C, 0);
        writer.SetU16(held + 0x16, 0);          // facing
        writer.SetU16(held + 0x1A, 0);          // attributes
        writer.SetI16(held + 0x1C, 12);         // sector
        writer.SetI32(held + 0x24, 220);        // the item it holds

        writer.U32(2);                          // two chests
        int chest = writer.Length;
        writer.Zero(2 * 5324);
        writer.SetU16(chest + 0x00, 0);         // type
        writer.SetU16(chest + 0x02, 1);         // flags: trapped
        writer.SetI32(chest + 0x04 + (0 * 36), 220);
        writer.SetI32(chest + 0x04 + (3 * 36), -3);
        // The second record is the all-zero one a map's spare slot looks like.

        writer.Zero(200);                       // event variables
        writer.I64(0);                          // last visit time
        writer.Text("sky", 12).U32(1).I32(100).I32(200).Zero(24);
        return writer.ToArray();
    }

    /// <summary>An event program that moves nowhere and opens the containers a test names.</summary>
    /// <param name="destination">The move's destination, which no container test needs.</param>
    /// <param name="chests">One container opening per entry: the event that raises it and the container it opens.</param>
    internal static byte[] ChestProgram(string destination, params (int EventId, int ChestId)[] chests)
    {
        List<byte> bytes = [];
        foreach ((int eventId, int chestId) in chests)
        {
            bytes.Add(5);                       // record size minus one: event id, step, opcode, and one operand
            bytes.AddRange(BitConverter.GetBytes((ushort)eventId));
            bytes.Add(0);                       // step
            bytes.Add(EvtOpcodes.OpenChest);
            bytes.Add((byte)chestId);
        }

        _ = destination;
        return [.. bytes];
    }

    /// <summary>A decoded interior holding one face per event, spaced the way a test needs.</summary>
    private static DecodedMap MapWithSpacing(int spacing, params int[] events) =>
        MapDecoder.DecodeIndoor(
            Payload("d01.blv", ContainerIndoorPayload(events, spacing)),
            Payload("d01.dlv", ContainerIndoorDeltaPayload(events.Length)));

    private static DecodedMap Map(params int[] events) => MapWithSpacing(100, events);

    private static MapDelta Delta() => Assert.IsType<MapDelta>(Map(176).Delta);

    private static EvtProgram Program(params (string Destination, int EventId, int ChestId)[] chests)
    {
        string destination = chests.Length == 0 ? "0" : chests[0].Destination;
        return EvtProgram.Read("d01.evt", ChestProgram(destination, [.. chests.Select(chest => (chest.EventId, chest.ChestId))]));
    }

    private static LodPayload Payload(string entryName, byte[] bytes) =>
        new(new LodEntry(entryName, 0, bytes.Length), bytes, LodPayloadKind.Verbatim);

    /// <summary>A byte writer for the payloads this suite builds, with fixed offsets set after the fact.</summary>
    private sealed class DeltaWriter
    {
        private readonly List<byte> _bytes = [];

        internal int Length => _bytes.Count;

        internal DeltaWriter U16(ushort value) => Raw(BitConverter.GetBytes(value));

        internal DeltaWriter I16(short value) => Raw(BitConverter.GetBytes(value));

        internal DeltaWriter U32(uint value) => Raw(BitConverter.GetBytes(value));

        internal DeltaWriter I32(int value) => Raw(BitConverter.GetBytes(value));

        internal DeltaWriter I64(long value) => Raw(BitConverter.GetBytes(value));

        internal DeltaWriter Zero(int count)
        {
            _bytes.AddRange(new byte[count]);
            return this;
        }

        internal DeltaWriter Text(string text, int width)
        {
            byte[] encoded = Encoding.Latin1.GetBytes(text);
            _bytes.AddRange(encoded);
            return Zero(width - encoded.Length);
        }

        internal DeltaWriter Raw(byte[] value)
        {
            _bytes.AddRange(value);
            return this;
        }

        internal DeltaWriter SetU16(int offset, ushort value) => Set(offset, BitConverter.GetBytes(value));

        internal DeltaWriter SetI16(int offset, short value) => Set(offset, BitConverter.GetBytes(value));

        internal DeltaWriter SetU32(int offset, uint value) => Set(offset, BitConverter.GetBytes(value));

        internal DeltaWriter SetI32(int offset, int value) => Set(offset, BitConverter.GetBytes(value));

        internal DeltaWriter SetU8(int offset, byte value) => Set(offset, [value]);

        internal byte[] ToArray() => [.. _bytes];

        private DeltaWriter Set(int offset, byte[] value)
        {
            for (int index = 0; index < value.Length; index++) _bytes[offset + index] = value[index];
            return this;
        }
    }
}
