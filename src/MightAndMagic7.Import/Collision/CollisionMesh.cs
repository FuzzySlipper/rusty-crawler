using MightAndMagic7.Import.Maps;

namespace MightAndMagic7.Import.Collision;

/// <summary>Where one place's collision geometry came from, for the counts an import reports.</summary>
/// <remarks>
/// The three sources are the three shapes the map payloads store geometry in, and they are counted apart
/// because they fail apart: an interior whose faces stop decoding and a region whose terrain stops
/// tiling are different defects, and a single total would hide which one happened.
/// </remarks>
public enum CollisionSource
{
    /// <summary>An interior's own faces, read from the level's face array.</summary>
    InteriorFace,

    /// <summary>An outdoor map's terrain, tiled from its height field.</summary>
    Terrain,

    /// <summary>An outdoor map's placed models, each with its own vertices and faces.</summary>
    ModelFace,
}

/// <summary>What one source contributed to a place's geometry, in faces and in emitted triangles.</summary>
/// <param name="Faces">How many solid faces the source held.</param>
/// <param name="Triangles">How many triangles those faces produced after the fan dropped degenerate ones.</param>
public readonly record struct CollisionSourceCounts(int Faces, int Triangles)
{
    /// <summary>Nothing contributed, which is what every source starts at.</summary>
    public static CollisionSourceCounts None => default;

    /// <summary>This source with one more face and its triangles added.</summary>
    /// <param name="triangles">How many triangles the face contributed.</param>
    public CollisionSourceCounts Plus(int triangles) => new(Faces + 1, Triangles + triangles);

    /// <summary>Two sources' contributions together, which is how an import totals them.</summary>
    /// <param name="left">One contribution.</param>
    /// <param name="right">The other.</param>
    public static CollisionSourceCounts operator +(CollisionSourceCounts left, CollisionSourceCounts right) =>
        new(left.Faces + right.Faces, left.Triangles + right.Triangles);
}

/// <summary>One place's collision geometry under construction, in the engine's own world axes.</summary>
/// <remarks>
/// <para>
/// The engine's world puts height in Y and moves a body along X and Z, while a map stores height in its
/// third coordinate. Every point is therefore laid onto the engine's axes as it enters, and nothing
/// downstream has to know which payload it came from. This is the same rule the ruleset states for the
/// party's pose when it composes movement over a place's coordinates; the importer cannot read it from
/// there — it is offline tooling with no reference to the runtime — so the rule is stated once here and
/// the live walk is what proves the two agree.
/// </para>
/// <para>
/// Corners are shared by their coordinates, so a face that repeats its first corner, or two faces that
/// meet along an edge, contribute one position rather than copies of it. The mesh keeps no normals: the
/// engine derives them from the winding, and the donor's own triangulation is what fixes the winding.
/// </para>
/// </remarks>
public sealed class CollisionMesh
{
    private readonly Dictionary<(int X, int Y, int Z), int> _corners = [];
    private readonly List<double> _positions = [];
    private readonly List<int> _triangles = [];

    /// <summary>How many positions the mesh holds.</summary>
    public int VertexCount => _positions.Count / 3;

    /// <summary>How many triangles the mesh holds.</summary>
    public int TriangleCount => _triangles.Count / 3;

    /// <summary>How many faces held fewer than three corners, so they could carry no surface at all.</summary>
    public int DroppedFaces { get; private set; }

    /// <summary>
    /// How many fan triangles were degenerate and were dropped.
    /// </summary>
    /// <remarks>
    /// A level's faces are not minimal polygons: neighbouring faces meet at corners that lie in the
    /// middle of an edge, which leaves a fan triangle with no area. Such a triangle carries no surface,
    /// and feeding it to a collider would be feeding it a triangle whose normal is undefined.
    /// </remarks>
    public int DroppedTriangles { get; private set; }

    /// <summary>The mesh's positions, three components each, in the engine's axes.</summary>
    public IReadOnlyList<double> Positions => _positions;

    /// <summary>The mesh's triangles, three position indices each.</summary>
    public IReadOnlyList<int> Triangles => _triangles;

    /// <summary>
    /// Adds one polygon as a fan from its first corner, and returns how many triangles it contributed.
    /// </summary>
    /// <remarks>
    /// The fan is the donor's own triangulation — "123, 134, 145, 156.." (OpenEnroth
    /// <c>src/Engine/Graphics/Renderer/OpenGLRenderer.cpp:3007-3020</c>) — so the surface this mesh
    /// collides against is the surface the game draws, and the winding that fan produces is the winding
    /// the level's own plane normals agree with.
    /// </remarks>
    /// <param name="corners">The polygon's corners, in the payload's winding order.</param>
    public int AddPolygon(IReadOnlyList<MapPoint> corners)
    {
        if (corners.Count < 3)
        {
            DroppedFaces++;
            return 0;
        }

        int added = 0;
        for (int index = 2; index < corners.Count; index++)
        {
            if (AddTriangle(corners[0], corners[index - 1], corners[index])) added++;
        }

        return added;
    }

    /// <summary>Adds one triangle, dropping it when its three corners enclose no area.</summary>
    /// <param name="a">The triangle's first corner.</param>
    /// <param name="b">The triangle's second corner.</param>
    /// <param name="c">The triangle's third corner.</param>
    /// <returns>Whether the triangle was kept.</returns>
    public bool AddTriangle(MapPoint a, MapPoint b, MapPoint c)
    {
        if (IsDegenerate(a, b, c))
        {
            DroppedTriangles++;
            return false;
        }

        _triangles.Add(Index(a));
        _triangles.Add(Index(b));
        _triangles.Add(Index(c));
        return true;
    }

    /// <summary>The position at one index, in the engine's axes.</summary>
    /// <param name="index">The position's index.</param>
    public (double X, double Y, double Z) Position(int index) =>
        (_positions[index * 3], _positions[(index * 3) + 1], _positions[(index * 3) + 2]);

    /// <summary>Whether three corners enclose no area, computed exactly because the corners are whole numbers.</summary>
    private static bool IsDegenerate(MapPoint a, MapPoint b, MapPoint c)
    {
        long ux = b.X - a.X, uy = b.Y - a.Y, uz = b.Z - a.Z;
        long vx = c.X - a.X, vy = c.Y - a.Y, vz = c.Z - a.Z;
        long nx = (uy * vz) - (uz * vy);
        long ny = (uz * vx) - (ux * vz);
        long nz = (ux * vy) - (uy * vx);
        return (nx * nx) + (ny * ny) + (nz * nz) == 0;
    }

    /// <summary>The index of a corner, adding it when the mesh has not seen it yet.</summary>
    private int Index(MapPoint point)
    {
        if (_corners.TryGetValue((point.X, point.Y, point.Z), out int index)) return index;

        // The engine's second axis is height and its third is the place's second ground axis, negated:
        // the one orientation of the place's own frame that keeps the engine's right-handed frame.
        index = VertexCount;
        _corners[(point.X, point.Y, point.Z)] = index;
        _positions.Add(point.X);
        _positions.Add(point.Z);
        _positions.Add(-point.Y);
        return index;
    }
}
