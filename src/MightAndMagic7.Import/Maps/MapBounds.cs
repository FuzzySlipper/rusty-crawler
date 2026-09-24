namespace MightAndMagic7.Import.Maps;

/// <summary>An axis-aligned box in the level's own coordinate system.</summary>
/// <remarks>
/// The field order is the one outdoor model headers use — each minimum immediately before its maximum
/// — rather than the face bounding box's interleaved order, because the two differ in the file and
/// silently swapping them yields a box that does not contain its own geometry.
/// </remarks>
/// <param name="MinX">Lowest X.</param>
/// <param name="MinY">Lowest Y.</param>
/// <param name="MinZ">Lowest Z.</param>
/// <param name="MaxX">Highest X.</param>
/// <param name="MaxY">Highest Y.</param>
/// <param name="MaxZ">Highest Z.</param>
public readonly record struct MapBounds(int MinX, int MinY, int MinZ, int MaxX, int MaxY, int MaxZ)
{
    /// <summary>The smallest box containing both boxes, or <paramref name="right"/> when the left is absent.</summary>
    /// <param name="left">The first box, or null.</param>
    /// <param name="right">The second box.</param>
    public static MapBounds? Union(MapBounds? left, MapBounds right) =>
        left is null
            ? right
            : new MapBounds(
                Math.Min(left.Value.MinX, right.MinX),
                Math.Min(left.Value.MinY, right.MinY),
                Math.Min(left.Value.MinZ, right.MinZ),
                Math.Max(left.Value.MaxX, right.MaxX),
                Math.Max(left.Value.MaxY, right.MaxY),
                Math.Max(left.Value.MaxZ, right.MaxZ));
}
