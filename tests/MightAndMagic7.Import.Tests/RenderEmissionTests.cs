using System.Buffers.Binary;
using MightAndMagic7.Import.Maps;
using MightAndMagic7.Import.Render;
using Xunit;

namespace MightAndMagic7.Import.Tests;

/// <summary>
/// A place's render geometry is read from the same decoded faces and terrain its collision is, on the same axes, with
/// texture coordinates the donor's way and a door's faces in a part of their own carrying the collision's travel.
/// </summary>
public sealed class RenderEmissionTests
{
    private static readonly TerrainTileTable Tiles = TerrainTileTable.Read(SyntheticInstallation.TileTable());

    [Fact]
    public void An_indoor_door_face_is_its_own_part_with_each_corner_carrying_its_doors_travel()
    {
        IndoorMap map = MapDecoder.DecodeIndoor(
            LodFixture.Stored("d01.blv", MapDecoderTests.IndoorPayload()), LodFixture.Stored("d01.dlv", MapDecoderTests.IndoorDeltaPayload()));

        PlaceRender render = PlaceRender.Emit(7, map, Tiles, name => name == "Cfb1" ? (64, 32) : null);

        // The fixture's only face is moved by door 0 at two of its four corners, so there is no static geometry.
        RenderPart part = Assert.Single(render.Parts);
        Assert.Equal(0, part.Door);
        Assert.Equal(4, part.VertexCount);
        Assert.Equal(6, part.IndexCount);
        RenderMaterial material = Assert.Single(render.Materials);
        Assert.Equal(("Cfb1", RenderSurface.Face, true), (material.Texture, material.Surface, material.Resolved));
        Assert.Empty(render.MissingTextures);

        float[] floats = Floats(render.ToBytes(), render.Vertices);

        // The second corner is (1, 2, 3) in the map, so (1, 3, -2) on the Engine's axes, as the collision lays it.
        Assert.Equal([1f, 3f, -2f], Position(floats, 1));

        // Its texel (10, 0) offset by the face's own delta, over the 64x32 bitmap.
        MapFace face = Assert.Single(map.Faces);
        Assert.Equal([(10f + face.TextureDeltaU) / 64, (0f + face.TextureDeltaV) / 32], Uv(floats, render.Vertices, 1));

        // The door's travel is the collision layout's, and the corner no door moves carries none.
        Assert.Equal(8f * 64 / 65536, Travel(floats, render.Vertices, 1)[1]);
        Assert.Equal([0f, 0f, 0f], Travel(floats, render.Vertices, 2));
    }

    [Fact]
    public void A_region_draws_each_terrain_square_with_its_tiles_bitmap_and_names_every_bitmap_it_lacks()
    {
        OutdoorMap map = MapDecoder.DecodeOutdoor(LodFixture.Stored("out01.odm", MapDecoderTests.OutdoorPayload(waterRow: true)));

        PlaceRender render = PlaceRender.Emit(1, map, Tiles, name => name == "wtrtyl" ? (128, 128) : null);

        // One quad per square and the model's one triangle, all static.
        RenderPart part = Assert.Single(render.Parts);
        Assert.Null(part.Door);
        Assert.Equal((127 * 127 * 4) + 3, render.Vertices);
        Assert.Equal((127 * 127 * 2) + 1, render.Triangles);

        // The water row draws the water tile and is water; every other bitmap the place names is unresolved here, and
        // the place says so rather than drawing a stand-in.
        Assert.Contains(render.Materials, material => material is { Texture: "wtrtyl", Surface: RenderSurface.Water, Resolved: true });
        Assert.Contains("Hhp1d", render.MissingTextures);
        Assert.DoesNotContain("wtrtyl", render.MissingTextures);
        Assert.True(render.UntexturedFaces > 0);

        // A region never visited shows the donor's first-visit sky when its weather names none it holds.
        Assert.Equal(PlaceRender.FirstVisitSky, render.Sky);
    }

    [Fact]
    public void A_door_keeps_a_switched_cogs_face_and_the_cogs_retexture_bitmaps_are_materials()
    {
        IndoorMap moving = MapDecoder.DecodeIndoor(
            LodFixture.Stored("d01.blv", MapDecoderTests.IndoorPayload()), LodFixture.Stored("d01.dlv", MapDecoderTests.IndoorDeltaPayload()));
        PlaceSwitches switches = new(new HashSet<int> { 9 }, ["swap"]);

        // The fixture's one face is cog 9 and moved by door 0: the door keeps it, and the retexture bitmap is still listed.
        PlaceRender render = PlaceRender.Emit(7, moving, Tiles, name => name is "Cfb1" or "swap" ? (64, 32) : null, switches);
        RenderPart part = Assert.Single(render.Parts);
        Assert.Equal((0, (int?)null, false), (part.Door, part.Switch, part.StartsHidden));
        Assert.Contains(render.Materials, material => material.Texture == "swap" && material.Resolved);

    }

    [Fact]
    public void The_binary_mesh_states_its_counts_after_its_magic()
    {
        IndoorMap map = MapDecoder.DecodeIndoor(
            LodFixture.Stored("d01.blv", MapDecoderTests.IndoorPayload()), LodFixture.Stored("d01.dlv", MapDecoderTests.IndoorDeltaPayload()));
        PlaceRender render = PlaceRender.Emit(7, map, Tiles, _ => (64, 64));

        byte[] bytes = render.ToBytes();

        Assert.True(bytes.AsSpan(0, 8).SequenceEqual(PlaceRender.Magic));
        Assert.Equal((uint)render.Vertices, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(8)));
        Assert.Equal((uint)(render.Triangles * 3), BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(12)));
        Assert.Equal((uint)render.Parts.Count, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(16)));
        Assert.Equal(24 + (render.Vertices * 44) + (render.Triangles * 12) + (render.Parts.Count * 36)
            + (render.Parts.Sum(part => part.Groups.Count) * 12), bytes.Length);
    }

    [Fact]
    public void A_shore_tile_shows_the_water_under_its_keyed_texels()
    {
        byte[] keyed = [252, 0, 252, 255, 10, 20, 30, 255];
        Assert.True(ShoreComposite.HasKey(keyed));
        Assert.False(ShoreComposite.HasKey([10, 20, 30, 255]));
    }

    private static float[] Floats(byte[] bytes, int vertices)
    {
        float[] values = new float[vertices * 11];
        for (int index = 0; index < values.Length; index++) values[index] = BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(24 + (index * 4)));
        return values;
    }

    private static float[] Position(float[] floats, int vertex) => floats[(vertex * 3)..((vertex * 3) + 3)];

    private static float[] Uv(float[] floats, int vertices, int vertex) => floats[((vertices * 6) + (vertex * 2))..((vertices * 6) + (vertex * 2) + 2)];

    private static float[] Travel(float[] floats, int vertices, int vertex) => floats[((vertices * 8) + (vertex * 3))..((vertices * 8) + (vertex * 3) + 3)];
}
