using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Maps;

/// <summary>
/// One of a place's own map cells: what that square of the place is, in the words the map itself states
/// it with.
/// </summary>
/// <remarks>
/// <para>
/// The number is content's, not this layer's: a region's cell carries the terrain tile its own map puts
/// there, and an interior's carries whether the level's own outlines pass through it. What the number means
/// is the game's reading and is asked of it when a map is drawn, so this layer never learns a terrain or a
/// wall.
/// </para>
/// <para>
/// A map is deliberately a raster and not a set of lines: a cell is what the party can have seen, what the
/// save records, and what a screen draws, and one representation for all three is what keeps "what the party
/// has seen" and "what the automap shows" from drifting apart.
/// </para>
/// </remarks>
public static class MapCellKinds
{
    /// <summary>The kind a cell carries when the place's own map states nothing about it.</summary>
    /// <remarks>
    /// Nothing is stated for most cells of most maps, and that is not a missing reading: a level's outlines
    /// pass through some squares and not others, so "no outline here" is what the map says about the rest.
    /// </remarks>
    public const byte Unstated = 0;
}

/// <summary>
/// One place's own map, as content carries it: the grid it is drawn on and what each of its cells states.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the place's map and not the party's knowledge of it.</b> Every cell of the place is here from
/// the moment the world is composed, seen or not; what the party has seen is the owner's own set of cells,
/// and the two are kept apart so a place the party has never entered has a map it has seen none of rather
/// than no map at all.
/// </para>
/// <para>
/// <b>The map is the place's own data, never a drawing of its collision.</b> A region's cells are the
/// terrain its own payload carries at the map's own tile pitch, and an interior's are the rasterised
/// outlines its level carries for its minimap, so what a player sees is derived from the same data the
/// original's automap is drawn from rather than from a second opinion about the geometry.
/// </para>
/// </remarks>
/// <param name="Place">The place this is the map of.</param>
/// <param name="Grid">The grid the place is mapped on.</param>
/// <param name="Kinds">One kind per cell, in row-major order, which is what each square of the place states.</param>
/// <exception cref="ArgumentException">The kinds do not cover the grid exactly.</exception>
public sealed record PlaceMap(PlaceId Place, MapGrid Grid, IReadOnlyList<byte> Kinds)
{
    /// <summary>One kind per cell, in row-major order.</summary>
    public IReadOnlyList<byte> Kinds { get; } = Check(Grid, Kinds, Place);

    /// <summary>What one cell of the place states, or <see cref="MapCellKinds.Unstated"/> off the map.</summary>
    /// <param name="cell">The cell to read.</param>
    /// <returns>The kind that square of the place carries.</returns>
    public byte KindAt(MapCell cell) =>
        Grid.Contains(cell) ? Kinds[cell.Index(Grid.Columns)] : MapCellKinds.Unstated;

    /// <summary>Whether the place's map covers the cell a point of the place falls in.</summary>
    /// <param name="x">A position along the place's first axis.</param>
    /// <param name="y">A position along the place's second axis.</param>
    /// <returns>Whether the grid has a cell there.</returns>
    public bool Covers(double x, double y) => Grid.Contains(Grid.CellAt(x, y));

    /// <summary>Reads a place's map, refusing one that does not cover its own grid exactly.</summary>
    /// <remarks>
    /// A map whose cells do not match its grid is a content defect that would make every cell after the gap
    /// read as its neighbour's, so it is refused where the map is built rather than drawn wrong later. The
    /// bound is enforced here too, because it is the grid and its cells together that a party's memory is
    /// bounded by.
    /// </remarks>
    private static IReadOnlyList<byte> Check(MapGrid grid, IReadOnlyList<byte>? kinds, PlaceId place)
    {
        if (kinds is null)
        {
            throw new ArgumentException(
                $"The map of place '{place}' carries no cells, so nothing could be drawn or seen of it.",
                nameof(kinds));
        }

        if (grid.Cells > MapGrid.MaxCells)
        {
            throw new ArgumentException(
                $"The map of place '{place}' is {grid.Cells} cells, and this build maps at most {MapGrid.MaxCells} cells of one place: a party's memory of a place would be unbounded.",
                nameof(kinds));
        }

        if (kinds.Count != grid.Cells)
        {
            throw new ArgumentException(
                $"The map of place '{place}' carries {kinds.Count} cells for a {grid} grid, so the squares of the place would not line up with what it states.",
                nameof(kinds));
        }

        return kinds;
    }
}

/// <summary>
/// Where a place's own map comes from, when the loaded content carries one.
/// </summary>
/// <remarks>
/// The seam is the same shape as the collision geometry's: content names the documents, the game reads them
/// into this layer's own value, and a place content states no map for answers with none — which is a place
/// the automap cannot draw, and saying so is better than drawing an empty rectangle that looks like an
/// unmapped room.
/// </remarks>
public interface IPlaceMapSource
{
    /// <summary>The place's own map, or null when content provides none for it.</summary>
    /// <param name="place">The place the party is mapping.</param>
    /// <returns>The place's map.</returns>
    PlaceMap? For(PlaceId place);
}
