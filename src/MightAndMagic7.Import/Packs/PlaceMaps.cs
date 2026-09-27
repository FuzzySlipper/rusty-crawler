using MightAndMagic7.Import.Maps;

namespace MightAndMagic7.Import.Packs;

/// <summary>
/// One place's automap raster: the grid the product draws it on and what each of its squares states.
/// </summary>
/// <remarks>
/// This is the importer's answer to "what can a map honestly be drawn from", and it is deliberately a raster
/// rather than a drawing: the product's map owner remembers which squares a party has walked as one bit each,
/// so the squares have to exist before anything walks them. What each square's number means is the ruleset's
/// reading — a region's is a band of its own height map and an interior's says whether the level's own outlines
/// pass through it — and the layout is recorded in <c>docs/research/mm7-map-formats.md</c>.
/// </remarks>
/// <param name="PlaceId">The place this is the map of.</param>
/// <param name="FileName">The map file the place was read from, which is the provenance a report prints.</param>
/// <param name="Kind">Whether the place is a region or an interior.</param>
/// <param name="CellSize">How wide one square is, in the place's own units.</param>
/// <param name="OriginX">Where the grid's first column starts along the place's first axis.</param>
/// <param name="OriginY">Where the grid's first row starts along the place's second axis.</param>
/// <param name="Columns">How many squares the grid has along the first axis.</param>
/// <param name="Rows">How many squares the grid has along the second axis.</param>
/// <param name="Kinds">One number per square, in row-major order.</param>
public readonly record struct PlaceMapRaster(
    int PlaceId,
    string FileName,
    MapKind Kind,
    int CellSize,
    int OriginX,
    int OriginY,
    int Columns,
    int Rows,
    byte[] Kinds);

/// <summary>
/// Builds each place's automap raster from the map's own data.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every number here comes from the shipped map, and the one value that does not is the interior cell
/// size.</b> A region's squares are the region's own terrain grid at its own 512-unit pitch, and each square's
/// number is the band the square's own height falls in. An interior's squares are this importer's
/// <see cref="InteriorCellSize"/> — the shipped data states no cell size for an indoor map, because the
/// original draws lines rather than squares — and each square says whether a minimap outline passes through
/// it, which is the same data the original draws its lines from.
/// </para>
/// <para>
/// <b>A region's grid is flipped into the product's axes, and that is the one arithmetic worth stating.</b> A
/// payload's cell (0,0) is the level's north-west corner and its second grid axis runs south, while a place's
/// own second axis runs the other way (<c>OutdoorMap.CellToWorld</c>). The raster is written in the product's
/// axes — one square per cell of a grid that starts at the terrain's own corner — so a pose and a square mean
/// the same thing on both sides of the pack, and a test walks every terrain cell back through
/// <c>CellToWorld</c> to prove it.
/// </para>
/// <para>
/// <b>What is drawn is coarser than the original's automap and that is marked as ours.</b> The original draws
/// an outdoor region as a prerendered landscape picture and an interior as the level's own outline lines shaded
/// by their height; what is emitted here is a square per terrain cell and a square per 128 units of an
/// interior, so the shape survives and the picture does not.
/// </para>
/// </remarks>
public static class PlaceMaps
{
    /// <summary>How wide one square of an interior's automap is, in the place's own units.</summary>
    /// <remarks>
    /// <b>Ours, and stated here because the importer is what rasterises it.</b> The shipped indoor payload
    /// states outlines and no cell size, so the size is a product decision: 128 units is a quarter of the
    /// region tile pitch, fine enough that a room is several squares across and coarse enough that the largest
    /// level is a few tens of thousands of squares. The ruleset reads it back out of the document rather than
    /// repeating the number, so this is the one home of the value.
    /// </remarks>
    public const int InteriorCellSize = 128;

    /// <summary>How many height bands a region's own height map is drawn in.</summary>
    /// <remarks>
    /// <b>Ours.</b> A height byte is 32 engine units of elevation, and four bands make a relief that reads at a
    /// glance; the band is <c>(height * Bands) / 256</c>, which is the whole of the reading and is recorded in
    /// the format spec.
    /// </remarks>
    public const int HeightBands = 4;

    /// <summary>The number a region's square carries when the level's outlines do not pass through it.</summary>
    public const byte InteriorOpen = 0;

    /// <summary>The number a region's square carries when the level's outlines do pass through it.</summary>
    public const byte InteriorOutline = 1;

    /// <summary>Builds the raster for one decoded map.</summary>
    /// <param name="placeId">The place the map belongs to.</param>
    /// <param name="map">The decoded map.</param>
    /// <returns>The raster.</returns>
    /// <exception cref="ArgumentNullException">No map was supplied.</exception>
    /// <exception cref="InvalidOperationException">The map carries nothing a grid could be built over.</exception>
    public static PlaceMapRaster Of(int placeId, DecodedMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        return map switch
        {
            OutdoorMap outdoor => Outdoor(placeId, outdoor),
            IndoorMap indoor => Indoor(placeId, indoor),
            _ => throw new InvalidOperationException(
                $"Place {placeId} was read from '{map.FileName}', which is neither an outdoor nor an indoor map, so no grid could be built over it."),
        };
    }

    /// <summary>A region's raster: the map's own terrain grid, one height band per square.</summary>
    private static PlaceMapRaster Outdoor(int placeId, OutdoorMap map)
    {
        int columns = OutdoorMap.TerrainCells;
        int rows = OutdoorMap.TerrainCells;
        int cellSize = OutdoorMap.TerrainCellSize;

        // The terrain square is centred on the origin and its cell (64, 64) starts there, so the grid starts
        // half a map below zero on both axes and covers exactly the terrain the height map spans.
        int originX = -(columns / 2) * cellSize;
        int originY = -(rows / 2) * cellSize;
        byte[] kinds = new byte[columns * rows];
        for (int gridY = 0; gridY < OutdoorMap.TerrainCells; gridY++)
        {
            int row = (OutdoorMap.TerrainCells - 1) - gridY;
            for (int gridX = 0; gridX < OutdoorMap.TerrainCells; gridX++)
            {
                int cell = (gridY * OutdoorMap.TerrainCells) + gridX;
                kinds[(row * columns) + gridX] = Band(map.HeightMap[cell]);
            }
        }

        return new PlaceMapRaster(placeId, map.FileName, MapKind.Outdoor, cellSize, originX, originY, columns, rows, kinds);
    }

    /// <summary>An interior's raster: its own minimap outlines, rasterised onto this importer's own grid.</summary>
    private static PlaceMapRaster Indoor(int placeId, IndoorMap map)
    {
        if (map.Bounds is not { } bounds)
        {
            throw new InvalidOperationException(
                $"Place {placeId} was read from '{map.FileName}', which holds no vertices at all, so there is no ground to draw a map over.");
        }

        int cellSize = InteriorCellSize;
        int originX = FloorToCell(bounds.MinX, cellSize);
        int originY = FloorToCell(bounds.MinY, cellSize);
        int columns = ((FloorToCell(bounds.MaxX, cellSize) - originX) / cellSize) + 1;
        int rows = ((FloorToCell(bounds.MaxY, cellSize) - originY) / cellSize) + 1;
        byte[] kinds = new byte[columns * rows];

        // Every outline is a line between two of the level's own vertices, which is what the original draws
        // (OpenEnroth src/GUI/UI/UIGame.cpp:1380-1400). Rasterising it is this importer's own reading of the
        // level's data, and it is done in the product's own axes: a square two cells from the party's is two
        // cells from it, whatever the payload's own vertex signs are.
        foreach (MapOutline outline in map.Outlines)
        {
            if (outline.Vertex1 < 0 || outline.Vertex1 >= map.Vertices.Count ||
                outline.Vertex2 < 0 || outline.Vertex2 >= map.Vertices.Count)
            {
                // An outline naming a vertex the level does not have is a defect the reader's own counts would
                // already have failed on; a raster is not the place to invent one, so the line is left out and
                // the level still gets a map of everything else.
                continue;
            }

            MapPoint from = map.Vertices[outline.Vertex1];
            MapPoint to = map.Vertices[outline.Vertex2];
            int fromColumn = (FloorToCell(from.X, cellSize) - originX) / cellSize;
            int fromRow = (FloorToCell(from.Y, cellSize) - originY) / cellSize;
            int toColumn = (FloorToCell(to.X, cellSize) - originX) / cellSize;
            int toRow = (FloorToCell(to.Y, cellSize) - originY) / cellSize;
            Rasterise(kinds, columns, rows, fromColumn, fromRow, toColumn, toRow);
        }

        return new PlaceMapRaster(placeId, map.FileName, MapKind.Indoor, cellSize, originX, originY, columns, rows, kinds);
    }

    /// <summary>Marks every square a segment passes through, by walking the wider of its two axes.</summary>
    /// <remarks>
    /// The walk is the whole of the rasterisation and it is deliberately simple: one square per step of the
    /// longer axis, so a diagonal line is a staircase rather than a gap. Two squares of the same line are one
    /// outline in the drawing, which is what a raster of a line means.
    /// </remarks>
    private static void Rasterise(byte[] kinds, int columns, int rows, int fromColumn, int fromRow, int toColumn, int toRow)
    {
        int steps = Math.Max(Math.Abs(toColumn - fromColumn), Math.Abs(toRow - fromRow));
        if (steps == 0)
        {
            Mark(kinds, columns, rows, fromColumn, fromRow);
            return;
        }

        for (int step = 0; step <= steps; step++)
        {
            int column = fromColumn + (int)Math.Round((toColumn - fromColumn) * (double)step / steps, MidpointRounding.AwayFromZero);
            int row = fromRow + (int)Math.Round((toRow - fromRow) * (double)step / steps, MidpointRounding.AwayFromZero);
            Mark(kinds, columns, rows, column, row);
        }
    }

    /// <summary>Marks one square as one the level's outlines pass through.</summary>
    private static void Mark(byte[] kinds, int columns, int rows, int column, int row)
    {
        if (column < 0 || column >= columns || row < 0 || row >= rows) return;
        kinds[(row * columns) + column] = InteriorOutline;
    }

    /// <summary>Which band of the region's own height one terrain square falls in.</summary>
    private static byte Band(byte height) => (byte)(((height * HeightBands) / 256) + 1);

    /// <summary>The first cell boundary at or below a coordinate.</summary>
    private static int FloorToCell(int value, int cellSize) =>
        (int)Math.Floor((double)value / cellSize) * cellSize;
}
