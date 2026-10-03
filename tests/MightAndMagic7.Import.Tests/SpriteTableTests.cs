using System.Buffers.Binary;
using System.Text;
using MightAndMagic7.Import.Lod;
using MightAndMagic7.Import.Media;
using MightAndMagic7.Import.Render;
using Xunit;

namespace MightAndMagic7.Import.Tests;

/// <summary>
/// The sprite frame table and the look lists are read by their stated record sizes, and a frame's views are named by the
/// donor's own rules.
/// </summary>
public sealed class SpriteTableTests
{
    [Fact]
    public void The_frame_table_reads_frames_and_finds_a_group_by_name_through_its_index()
    {
        byte[] bytes = new byte[8 + (3 * 60) + (2 * 2)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 3);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 2);
        Frame(bytes, 0, "null", "null", 0, 0);
        Frame(bytes, 1, "Walk", "wlka", SpriteFrame.HasMore, 8, scale: 2, palette: 223, animation: 16);
        Frame(bytes, 2, string.Empty, "wlkb", 0, 8);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8 + 180), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8 + 182), 1);

        SpriteFrameTable table = SpriteFrameTable.Read(bytes);

        Assert.Equal(1, table.Find("walk"));
        Assert.Null(table.Find("run"));
        IReadOnlyList<SpriteFrame> group = table.Group(1);
        Assert.Equal(2, group.Count);
        Assert.Equal(("wlka", 2d, 223, 0.5, 1d), (group[0].TextureName, group[0].Scale, group[0].PaletteId, group[0].Seconds, group[0].AnimationSeconds));
        Assert.Throws<LodFormatException>(() => SpriteFrameTable.Read(bytes[..^1]));
    }

    [Fact]
    public void The_look_lists_read_each_records_fields_at_its_stated_offsets_and_drop_the_last_monster_row()
    {
        byte[] monsters = new byte[4 + (2 * 152)];
        BinaryPrimitives.WriteInt32LittleEndian(monsters, 2);
        BinaryPrimitives.WriteUInt16LittleEndian(monsters.AsSpan(4), 160);
        BinaryPrimitives.WriteUInt16LittleEndian(monsters.AsSpan(6), 40);
        Encoding.ASCII.GetBytes("PeasStand").CopyTo(monsters, 4 + 52);
        Encoding.ASCII.GetBytes("PeasDead").CopyTo(monsters, 4 + 52 + 60);
        byte[] decorations = new byte[4 + 84];
        BinaryPrimitives.WriteInt32LittleEndian(decorations, 1);
        Encoding.ASCII.GetBytes("tree01").CopyTo(decorations, 4);
        BinaryPrimitives.WriteUInt16LittleEndian(decorations.AsSpan(4 + 66), 512);
        BinaryPrimitives.WriteInt16LittleEndian(decorations.AsSpan(4 + 70), 120);
        BinaryPrimitives.WriteUInt16LittleEndian(decorations.AsSpan(4 + 72), 77);
        BinaryPrimitives.WriteUInt16LittleEndian(decorations.AsSpan(4 + 74), DecorationLook.Marker);
        decorations[4 + 80] = 12;
        decorations[4 + 81] = 34;
        decorations[4 + 82] = 56;
        byte[] objects = new byte[4 + 56];
        BinaryPrimitives.WriteInt32LittleEndian(objects, 1);
        BinaryPrimitives.WriteUInt16LittleEndian(objects.AsSpan(4 + 40), 91);

        LookLists lists = LookLists.Read(monsters, decorations, objects);

        MonsterLook peasant = Assert.Single(lists.Monsters);
        Assert.Equal((1, 160, 40, "PeasStand", "PeasDead"), (peasant.Monster, peasant.Height, peasant.Radius, peasant.Groups[0], peasant.Groups[6]));
        DecorationLook tree = Assert.Single(lists.Decorations);
        Assert.Equal(("tree01", 77, 512, DecorationLook.Marker), (tree.Name, tree.Frame, tree.Height, tree.Flags));
        Assert.Equal((120, new Rgb24(12, 34, 56)), (tree.LightRadius, tree.LightColour));
        Assert.Equal(91, Assert.Single(lists.Objects).Frame);
        Assert.Throws<LodFormatException>(() => LookLists.Read(monsters[..^1], decorations, objects));
    }

    [Fact]
    public void A_map_decoration_is_the_first_row_from_one_its_name_matches_without_regard_to_case()
    {
        // Rows 0, 1 and 2: the donor never answers row zero by name, and the first later match wins.
        byte[] decorations = new byte[4 + (3 * 84)];
        BinaryPrimitives.WriteInt32LittleEndian(decorations, 3);
        Encoding.ASCII.GetBytes("torch01").CopyTo(decorations, 4);
        Encoding.ASCII.GetBytes("Torch01").CopyTo(decorations, 4 + 84);
        Encoding.ASCII.GetBytes("torch01").CopyTo(decorations, 4 + 168);
        byte[] none = new byte[4];
        LookLists lists = LookLists.Read(none, decorations, none);

        Assert.Equal(1, lists.DecorationRow("TORCH01"));
        Assert.Equal(0, lists.DecorationRow("fire01"));
        Assert.Equal(0, lists.DecorationRow(string.Empty));
    }

    [Theory]
    [InlineData(0, 2, "wlka2", false)]
    [InlineData(SpriteFrame.Image1, 5, "wlka", false)]
    [InlineData(SpriteFrame.Images3, 5, "wlka4", false)]
    [InlineData(SpriteFrame.Images3, 7, "wlka0", false)]
    [InlineData(SpriteFrame.Mirror0 << 3, 3, "wlka5", true)]
    public void A_frames_views_follow_the_donors_naming_and_mirroring(int flags, int octant, string entry, bool mirrored)
    {
        SpriteFrame frame = new("walk", "wlka", 1, flags, 1, 0.5, 1);

        Assert.Equal((entry, mirrored), (SpriteAtlasBuilder.View(frame, octant)!.Value.Entry, SpriteAtlasBuilder.View(frame, octant)!.Value.Mirrored));
    }

    private static void Frame(byte[] bytes, int index, string group, string texture, int flags, short length, int scale = 1, short palette = 0, short animation = 0)
    {
        Span<byte> record = bytes.AsSpan(8 + (index * 60), 60);
        Encoding.ASCII.GetBytes(group).CopyTo(record);
        Encoding.ASCII.GetBytes(texture).CopyTo(record[12..]);
        BinaryPrimitives.WriteInt32LittleEndian(record[40..], scale * 65536);
        BinaryPrimitives.WriteInt32LittleEndian(record[44..], flags);
        BinaryPrimitives.WriteInt16LittleEndian(record[50..], palette);
        BinaryPrimitives.WriteInt16LittleEndian(record[54..], length);
        BinaryPrimitives.WriteInt16LittleEndian(record[56..], animation);
    }
}
