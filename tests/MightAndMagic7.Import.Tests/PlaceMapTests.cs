using MightAndMagic7.Import.Lod;
using MightAndMagic7.Import.Maps;
using MightAndMagic7.Import.Packs;
using Xunit;

namespace MightAndMagic7.Import.Tests;

/// <summary>
/// What a place's own automap raster is built from, and the one arithmetic that puts it in the product's axes.
/// </summary>
/// <remarks>
/// <para>
/// The raster is the importer's answer to what a map can honestly be drawn from: a region's own terrain grid,
/// one band of its own height map each, and an interior's own minimap outlines rasterised at this importer's
/// cell size. The tests here state both halves over maps these tests build themselves, so nothing needs the
/// operator's game data: every terrain cell is walked back through the map's own <c>CellToWorld</c> to prove
/// the flip, and an interior's outlines are checked to have marked the squares they pass through.
/// </para>
/// <para>
/// The packed document itself is checked in the writer's own suite, where a whole pack is written and read
/// back by the product's loader.
/// </para>
/// </remarks>
public sealed class PlaceMapTests
{
    [Fact]
    public void A_region_is_mapped_on_its_own_terrain_grid_in_the_products_axes()
    {
        OutdoorMap map = MapDecoder.DecodeOutdoor(LodFixture.Stored("out01.odm", MapDecoderTests.OutdoorPayload()));
        PlaceMapRaster raster = PlaceMaps.Of(placeId: 1, map);

        // The grid is the region's own terrain square: 128 cells of 512 units each, starting half a map below
        // the origin on both axes, which is where the height map spans.
        Assert.Equal(MapKind.Outdoor, raster.Kind);
        Assert.Equal(OutdoorMap.TerrainCellSize, raster.CellSize);
        Assert.Equal(-32768, raster.OriginX);
        Assert.Equal(-32768, raster.OriginY);
        Assert.Equal(128, raster.Columns);
        Assert.Equal(128, raster.Rows);
        Assert.Equal(128 * 128, raster.Kinds.Length);

        // Every square says which band of the map's own height it stands at, and the banding is the reading the
        // format spec records: a height byte of 5 is the first of four bands, and one of 200 is the last.
        Assert.Equal(Band(map.HeightMap[0]), raster.Kinds[Cell(raster, 0, OutdoorMap.TerrainCells - 1)]);
        Assert.Equal(PlaceMaps.HeightBands, Band(255));
        Assert.Equal(1, Band(0));

        // The one arithmetic worth stating is the flip, and this is the proof of it: a payload's cell (gridX,
        // gridY) starts at the point CellToWorld names, and that point falls in the raster's own square. A
        // raster written the other way round would put every place's map upside down against its own poses.
        for (int gridY = 0; gridY < OutdoorMap.TerrainCells; gridY += 7)
        {
            for (int gridX = 0; gridX < OutdoorMap.TerrainCells; gridX += 11)
            {
                // A cell's own start is its corner, so the centre of the square is what has to land in the
                // raster's square: half a cell right and half a cell down from where the payload says it starts.
                MapPoint world = map.CellToWorld(gridX, gridY);
                int half = OutdoorMap.TerrainCellSize / 2;
                int column = (int)Math.Floor((world.X + half - raster.OriginX) / (double)raster.CellSize);
                int row = (int)Math.Floor((world.Y - half - raster.OriginY) / (double)raster.CellSize);
                Assert.Equal(gridX, column);
                Assert.Equal((OutdoorMap.TerrainCells - 1) - gridY, row);
                Assert.Equal(
                    Band(map.HeightMap[(gridY * OutdoorMap.TerrainCells) + gridX]),
                    raster.Kinds[(row * raster.Columns) + column]);
            }
        }
    }

    [Fact]
    public void An_interior_is_mapped_from_its_own_outlines_at_this_importers_own_cell_size()
    {
        IndoorMap map = MapDecoder.DecodeIndoor(LodFixture.Stored("d01.blv", OutlinePayload()));
        PlaceMapRaster raster = PlaceMaps.Of(placeId: 2, map);

        // The level's own minimap outlines are read rather than skipped, and they are what its automap is drawn
        // from: the shipped data carries no indoor terrain grid and no indoor map picture.
        Assert.Equal(2, map.Outlines.Count);
        Assert.Equal(MapKind.Indoor, raster.Kind);
        Assert.Equal(PlaceMaps.InteriorCellSize, raster.CellSize);

        // The grid covers the level's own vertices: it starts at the cell boundary below the lowest of them and
        // is as many squares as the level needs, at this importer's own cell size. A level whose vertices span
        // exactly four cells of ground gets the four cells and the boundary square beyond them, which is the
        // cell its own edge stands on.
        Assert.Equal(0, raster.OriginX);
        Assert.Equal(0, raster.OriginY);
        Assert.Equal(5, raster.Columns);
        Assert.Equal(5, raster.Rows);
        Assert.Equal(25, raster.Kinds.Length);

        // A line between two of the level's vertices marks every square it passes through, and the rest of the
        // level is open: what a party sees of an interior is its own outline drawing, and nothing here invents
        // walls the level does not state.
        int marked = raster.Kinds.Count(kind => kind == PlaceMaps.InteriorOutline);
        Assert.True(marked >= 8, $"the rasterised outline marked {marked} squares");
        Assert.Equal(PlaceMaps.InteriorOutline, raster.Kinds[Cell(raster, 0, 0)]);
        Assert.Equal(PlaceMaps.InteriorOpen, raster.Kinds[Cell(raster, 3, 3)]);

        // And a level whose payload carries no outlines has a raster with no outlines in it rather than a
        // guessed shape: the shipped level with no automap data is drawn as empty ground.
        IndoorMap bare = MapDecoder.DecodeIndoor(LodFixture.Stored("d02.blv", MapDecoderTests.IndoorPayload()));
        PlaceMapRaster none = PlaceMaps.Of(placeId: 3, bare);
        Assert.Empty(bare.Outlines);
        Assert.All(none.Kinds, kind => Assert.Equal(PlaceMaps.InteriorOpen, kind));
    }

    /// <summary>The height band one of the region's own height bytes is drawn as.</summary>
    private static byte Band(byte height) => (byte)(((height * PlaceMaps.HeightBands) / 256) + 1);

    /// <summary>One square of a raster, by column and row.</summary>
    private static int Cell(PlaceMapRaster raster, int column, int row) => (row * raster.Columns) + column;


    /// <summary>
    /// An indoor payload whose four vertices are a square of the level's own units apart, with two outlines
    /// between them.
    /// </summary>
    /// <remarks>
    /// The shipped layout is walked in one order and its readers are strict, so this builder writes the same
    /// fields in the same order as the decoder's own fixture does and stops at the outline array. The vertices
    /// are spread over 512 units so the raster has squares to mark, which the decoder's own fixture — four
    /// vertices one unit apart — could not show.
    /// </remarks>
    private static byte[] OutlinePayload()
    {
        MapDecoderTests.MapWriter writer = new();
        writer.U32(1);
        writer.Text("A hall", 100);
        int sizes = writer.Length;
        writer.U32(0).U32(0).U32(0).U32(0);
        writer.Zero(16);

        // Four vertices a square of 512 units apart, in the payload's own axes.
        writer.U32(4);
        writer.I16(0).I16(0).I16(0);
        writer.I16(512).I16(0).I16(0);
        writer.I16(512).I16(512).I16(0);
        writer.I16(0).I16(512).I16(0);

        writer.U32(1);                          // face count
        int face = writer.Length;
        writer.Zero(96);
        writer.SetU32(face + 0x2C, 0x08);
        writer.SetU16(face + 0x48, 0);
        writer.SetU16(face + 0x4A, 0xFFFF);
        writer.SetU16(face + 0x4C, 1);
        writer.SetI16(face + 0x4E, 0);
        writer.SetU8(face + 0x5C, 3);
        writer.SetU8(face + 0x5D, 4);

        // The shared face data pool: six arrays of five values for a four-cornered face.
        for (int slot = 0; slot < 4; slot++) writer.I16((short)slot);
        writer.I16(0);
        for (int array = 0; array < 3; array++)
        {
            for (int slot = 0; slot <= 4; slot++) writer.I16(0);
        }

        for (int slot = 0; slot < 4; slot++) writer.I16((short)(slot * 10));
        writer.I16(0);
        for (int slot = 0; slot < 4; slot++) writer.I16((short)(slot < 2 ? 0 : 10));
        writer.I16(0);

        writer.Text("Cfb1", 10);

        writer.U32(1);                          // face extra count
        int extra = writer.Length;
        writer.Zero(36);
        writer.SetI16(extra + 0x0C, 0);
        writer.SetU16(extra + 0x0E, 0xFFFF);
        writer.Text(string.Empty, 10);

        writer.U32(2);                          // sector count
        writer.Zero(116);
        int sector = writer.Length;
        writer.Zero(116);
        writer.SetI32(sector + 0x00, 0x18);
        writer.SetU16(sector + 0x04, 1);
        writer.SetU16(sector + 0x0C, 1);
        writer.SetU16(sector + 0x14, 1);
        writer.SetU16(sector + 0x2C, 1);
        writer.SetU16(sector + 0x2E, 1);
        writer.SetU16(sector + 0x44, 1);
        writer.SetU16(sector + 0x54, 2);
        writer.SetShortBounds(sector + 0x68, 0, 0, 0, 512, 512, 0);

        // The sector data pool holds the sector's lists in sector order, then the light pool its lights.
        writer.I16(0).I16(0).I16(0).I16(0).I16(0);
        writer.I16(0).I16(1);

        writer.U32(2);                          // door capacity
        writer.U32(1);                          // decoration count
        int decoration = writer.Length;
        writer.Zero(32);
        writer.SetU16(decoration + 0x00, 3);
        writer.Text("Party Start", 32);

        writer.U32(2);                          // light count
        int light = writer.Length;
        writer.Zero(16);
        writer.SetI16(light + 0x00, 10).SetI16(light + 0x02, 20).SetI16(light + 0x04, 30);
        writer.SetI16(light + 0x06, 40);
        writer.SetU8(light + 0x08, 1).SetU8(light + 0x09, 2).SetU8(light + 0x0A, 3).SetU8(light + 0x0B, 4);
        writer.SetI16(light + 0x0C, 5).SetI16(light + 0x0E, 6);
        writer.Zero(16);

        writer.U32(0);                          // BSP node count
        writer.U32(0);                          // spawn point count

        // The level's own automap: two lines between its own vertices, which is what the raster is built from.
        writer.U32(2);
        writer.U16(0).U16(1).U16(0).U16(0).I16(0).U16(0);
        writer.U16(0).U16(3).U16(0).U16(0).I16(0).U16(0);

        writer.SetU32(sizes, (uint)(6 * 5 * sizeof(short)));
        writer.SetU32(sizes + 4, 5 * sizeof(short));
        writer.SetU32(sizes + 8, 2 * sizeof(short));
        writer.SetU32(sizes + 12, 8 * sizeof(short));
        return writer.ToArray();
    }
}
