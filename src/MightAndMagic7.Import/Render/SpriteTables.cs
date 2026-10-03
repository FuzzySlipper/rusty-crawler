using System.Buffers.Binary;
using System.Text;
using MightAndMagic7.Import.Lod;

namespace MightAndMagic7.Import.Render;

/// <summary>One frame of the game's sprite frame table.</summary>
/// <param name="GroupName">The group's name, set on a group's first frame and used to look it up by name.</param>
/// <param name="TextureName">The sprite entries' name without their octant digit.</param>
/// <param name="Scale">How large the frame is drawn: its texel is this many place units.</param>
/// <param name="Flags">The donor's <c>SpriteFrameFlag</c> word.</param>
/// <param name="PaletteId">The <c>pal%03d</c> the frame is coloured with, which recolours one creature's art for another.</param>
/// <param name="Seconds">How long the frame shows, in real seconds.</param>
/// <param name="AnimationSeconds">How long the group's whole animation runs, on its first frame.</param>
public sealed record SpriteFrame(string GroupName, string TextureName, double Scale, int Flags, int PaletteId, double Seconds, double AnimationSeconds)
{
    /// <summary><c>SPRITE_FRAME_HAS_MORE</c>: the group continues with the next frame.</summary>
    public const int HasMore = 0x1;

    /// <summary><c>SPRITE_FRAME_LIT</c>: the frame lights itself.</summary>
    public const int Lit = 0x2;

    /// <summary><c>SPRITE_FRAME_IMAGE1</c>: one image for all eight octants.</summary>
    public const int Image1 = 0x10;

    /// <summary><c>SPRITE_FRAME_CENTER</c>: the frame is centred on its point rather than standing on it.</summary>
    public const int Center = 0x20;

    /// <summary><c>SPRITE_FRAME_FIDGET</c>: a creature's fidget sequence, whose side views are its standing frames.</summary>
    public const int Fidget = 0x40;

    /// <summary><c>SPRITE_FRAME_MIRROR_0</c>; octant <c>n</c>'s flag is this shifted left by <c>n</c>.</summary>
    public const int Mirror0 = 0x100;

    /// <summary><c>SPRITE_FRAME_IMAGES3</c>: only views 0, 2 and 4 exist.</summary>
    public const int Images3 = 0x10000;
}

/// <summary>
/// The game's sprite frame table (<c>dsft.bin</c>): every frame and the sorted index of named groups.
/// </summary>
/// <remarks>
/// Layout (OpenEnroth <c>src/Engine/Snapshots/CompositeSnapshots.h:160-165</c>, <c>SpriteFrameTable_MM7</c>): a u32 frame
/// count, a u32 group-index count, that many 60-byte <c>SpriteFrame_MM7</c> records (<c>EntitySnapshots.h:135-156</c>) and
/// that many u16 frame indices sorted by group name. A frame's length is in eighths of a 128-tick second
/// (<c>EntitySnapshots.cpp:275-276</c>, <c>src/Core/Time/Duration.h:28</c>). [data: 9,221 frames, 2,057 named groups.]
/// </remarks>
public sealed class SpriteFrameTable
{
    /// <summary>Where the table lives.</summary>
    public static LodSource Source { get; } = new("sprite-frames", "Events.lod", "dsft.bin");

    private const int FrameSize = 60;
    private readonly Dictionary<string, int> _groups;

    private SpriteFrameTable(IReadOnlyList<SpriteFrame> frames, Dictionary<string, int> groups)
    {
        Frames = frames;
        _groups = groups;
    }

    /// <summary>Every frame, in table order.</summary>
    public IReadOnlyList<SpriteFrame> Frames { get; }

    /// <summary>Reads the table from its decompressed bytes.</summary>
    /// <exception cref="LodFormatException">The counts do not match the bytes.</exception>
    public static SpriteFrameTable Read(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length < 8) throw new LodFormatException(LodFault.Truncated, $"{Source.EntryName} holds {bytes.Length} bytes, too few for its counts.");
        int frames = BinaryPrimitives.ReadInt32LittleEndian(bytes);
        int indices = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4));
        if (frames < 0 || indices < 0 || 8L + (frames * (long)FrameSize) + (indices * 2L) != bytes.Length)
            throw new LodFormatException(LodFault.Count, $"{Source.EntryName} declares {frames} frames and {indices} group indices, which do not fill its {bytes.Length} bytes.");
        List<SpriteFrame> read = [];
        for (int index = 0; index < frames; index++)
        {
            ReadOnlySpan<byte> record = bytes.AsSpan(8 + (index * FrameSize), FrameSize);
            read.Add(new SpriteFrame(
                Text(record[..12]),
                Text(record[12..24]),
                BinaryPrimitives.ReadInt32LittleEndian(record[40..]) / 65536d,
                BinaryPrimitives.ReadInt32LittleEndian(record[44..]),
                BinaryPrimitives.ReadInt16LittleEndian(record[50..]),
                BinaryPrimitives.ReadInt16LittleEndian(record[54..]) / 16d,
                BinaryPrimitives.ReadInt16LittleEndian(record[56..]) / 16d));
        }

        Dictionary<string, int> groups = new(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < indices; index++)
        {
            int frame = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(8 + (frames * FrameSize) + (index * 2)));
            if (frame < read.Count && read[frame].GroupName.Length > 0) groups.TryAdd(read[frame].GroupName, frame);
        }

        return new SpriteFrameTable(read, groups);
    }

    /// <summary>Reads the table from the installation.</summary>
    public static SpriteFrameTable Read(LodInstall install) => Read(install.Read(Source).Bytes);

    /// <summary>A named group's first frame, or null when the table names none (the donor's <c>FastFindSprite</c>).</summary>
    public int? Find(string name) => name.Length > 0 && _groups.TryGetValue(name, out int frame) ? frame : null;

    /// <summary>A group's frames, from its first to the last that says the group continues.</summary>
    public IReadOnlyList<SpriteFrame> Group(int first)
    {
        List<SpriteFrame> frames = [];
        for (int index = first; index < Frames.Count; index++)
        {
            frames.Add(Frames[index]);
            if ((Frames[index].Flags & SpriteFrame.HasMore) == 0) break;
        }

        return frames;
    }

    internal static string Text(ReadOnlySpan<byte> field)
    {
        int end = field.IndexOf((byte)0);
        return Encoding.ASCII.GetString(field[..(end < 0 ? field.Length : end)]);
    }
}

/// <summary>A creature kind's look: its size and the sprite group of each of its eight animations.</summary>
/// <param name="Monster">The monster row it belongs to.</param>
/// <param name="Height">Its height in place units.</param>
/// <param name="Radius">Its radius in place units.</param>
/// <param name="Groups">Its animations' group names, in the donor's <c>ActorAnimation</c> order: standing, walking,
/// melee, ranged, hit, dying, dead, fidget (OpenEnroth <c>src/Engine/Objects/ActorEnums.h:80-88</c>).</param>
public sealed record MonsterLook(int Monster, int Height, int Radius, IReadOnlyList<string> Groups);

/// <summary>A decoration kind's look: its sprite group's first frame and how it stands.</summary>
/// <param name="Index">Its row in the decoration list, which a map decoration names.</param>
/// <param name="Name">Its internal name.</param>
/// <param name="Frame">Its sprite group's first frame.</param>
/// <param name="Height">Its height in place units.</param>
/// <param name="Radius">Its radius in place units.</param>
/// <param name="Flags">The donor's <c>DecorationDescFlag</c> word.</param>
public sealed record DecorationLook(int Index, string Name, int Frame, int Height, int Radius, int Flags)
{
    /// <summary><c>DECORATION_DESC_DONT_DRAW</c> (OpenEnroth <c>src/Engine/Data/DecorationEnums.h:249</c>).</summary>
    public const int DontDraw = 0x2;

    /// <summary><c>DECORATION_DESC_MARKER</c>: an editor marker such as a start point, never drawn.</summary>
    public const int Marker = 0x20;
}

/// <summary>A loose object kind's look: the sprite group's first frame an object on the ground is drawn with.</summary>
/// <param name="Index">Its row in the object list, which a map's sprite object names.</param>
/// <param name="Frame">Its sprite group's first frame.</param>
/// <param name="Flags">The donor's <c>ObjectDescFlag</c> word; <c>OBJECT_DESC_NO_SPRITE</c> (0x1) draws nothing.</param>
public sealed record ObjectLook(int Index, int Frame, int Flags);

/// <summary>
/// The game's look lists: <c>dmonlist.bin</c>, <c>ddeclist.bin</c> and <c>dobjlist.bin</c>, each a u32 count and that many
/// records (OpenEnroth <c>src/Engine/Snapshots/EntitySnapshots.h</c>: <c>MonsterDesc_MM7</c> 152 bytes at :731-742,
/// <c>DecorationData_MM7</c> 84 bytes at :1005-1026, <c>ObjectDesc_MM7</c> 56 bytes at :1186-1201). [data: 277 monster rows,
/// the last unused (<c>TableSerialization.cpp:54-56</c>); 228 decorations; 254 objects.]
/// </summary>
public sealed record LookLists(IReadOnlyList<MonsterLook> Monsters, IReadOnlyList<DecorationLook> Decorations, IReadOnlyList<ObjectLook> Objects)
{
    /// <summary>Reads the three lists from the installation.</summary>
    public static LookLists Read(LodInstall install)
    {
        ArgumentNullException.ThrowIfNull(install);
        byte[] Bytes(string name) => install.Read(new LodSource(name, "Events.lod", name)).Bytes;
        return Read(Bytes("dmonlist.bin"), Bytes("ddeclist.bin"), Bytes("dobjlist.bin"));
    }

    /// <summary>Reads the three lists from their decompressed bytes.</summary>
    /// <exception cref="LodFormatException">A list's count does not match its bytes.</exception>
    public static LookLists Read(byte[] monsters, byte[] decorations, byte[] objects)
    {
        List<MonsterLook> creatures = [];
        Records(monsters, 152, "dmonlist.bin", (index, record) =>
        {
            // The monster table numbers its rows from one; the list's last row is unused.
            string[] groups = new string[8];
            for (int slot = 0; slot < groups.Length; slot++) groups[slot] = SpriteFrameTable.Text(record.Slice(52 + (slot * 10), 10));
            creatures.Add(new MonsterLook(index + 1, BinaryPrimitives.ReadUInt16LittleEndian(record), BinaryPrimitives.ReadUInt16LittleEndian(record[2..]), groups));
        });
        if (creatures.Count > 0) creatures.RemoveAt(creatures.Count - 1);
        List<DecorationLook> decor = [];
        Records(decorations, 84, "ddeclist.bin", (index, record) => decor.Add(new DecorationLook(
            index, SpriteFrameTable.Text(record[..32]), BinaryPrimitives.ReadUInt16LittleEndian(record[72..]),
            BinaryPrimitives.ReadUInt16LittleEndian(record[66..]), BinaryPrimitives.ReadInt16LittleEndian(record[68..]),
            BinaryPrimitives.ReadUInt16LittleEndian(record[74..]))));
        List<ObjectLook> loose = [];
        Records(objects, 56, "dobjlist.bin", (index, record) => loose.Add(new ObjectLook(
            index, BinaryPrimitives.ReadUInt16LittleEndian(record[40..]), BinaryPrimitives.ReadUInt16LittleEndian(record[38..]))));
        return new LookLists(creatures, decor, loose);
    }

    /// <summary>
    /// The decoration-list row a map decoration's name is: the first row from one whose name matches it without regard to
    /// case, or row zero for none. A level stores a row beside each name, but the donor discards it and resolves every
    /// decoration by its name when the map loads (OpenEnroth <c>src/Engine/Snapshots/CompositeSnapshots.cpp:282-286</c>
    /// and <c>:570-574</c>, <c>src/Engine/Tables/DecorationTable.cpp:13-22</c>); an interior's stored rows are all zero.
    /// </summary>
    /// <param name="name">The decoration's name as the map stores it.</param>
    public int DecorationRow(string name)
    {
        if (string.IsNullOrEmpty(name)) return 0;
        foreach (DecorationLook decoration in Decorations)
        {
            if (decoration.Index >= 1 && string.Equals(decoration.Name, name, StringComparison.OrdinalIgnoreCase)) return decoration.Index;
        }

        return 0;
    }

    private delegate void Reader(int index, ReadOnlySpan<byte> record);

    private static void Records(byte[] bytes, int size, string name, Reader read)
    {
        int count = bytes.Length >= 4 ? BinaryPrimitives.ReadInt32LittleEndian(bytes) : -1;
        if (count < 0 || 4L + (count * (long)size) != bytes.Length)
            throw new LodFormatException(LodFault.Count, $"{name} declares {count} records of {size} bytes, and holds {bytes.Length - 4} bytes after the count.");
        for (int index = 0; index < count; index++) read(index, bytes.AsSpan(4 + (index * size), size));
    }
}
