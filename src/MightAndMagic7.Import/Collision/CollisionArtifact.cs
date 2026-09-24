using System.Text;
using System.Text.Json;
using MightAndMagic7.Import.Maps;

namespace MightAndMagic7.Import.Collision;

/// <summary>Why a place's geometry was refused, named so the place and the reason travel together.</summary>
/// <remarks>
/// A refusal is not a fallback: a place with no artifact has no collision at all, and the product says
/// so when the party enters it. The code is what a report groups by and the detail is what names the
/// value that failed, so a refusal can be acted on without re-running the import under a debugger.
/// </remarks>
/// <param name="Code">The reason's stable code, such as <c>no-standable-surface</c>.</param>
/// <param name="Detail">What was found, in terms of the map and the values that failed.</param>
public sealed record CollisionRefusal(string Code, string Detail);

/// <summary>
/// The engine's canonical collision artifact for one place: validation, and the document itself.
/// </summary>
/// <remarks>
/// <para>
/// The engine parses this document itself, as canonical schema JSON with unknown fields refused
/// (<c>rust/crates/csharp-engine-services/src/spatial.rs</c>, <c>parse_spatial_content_artifact</c>), so
/// nothing between content and that parse may rewrite a field of it. What the importer owns is the
/// decision of what goes in: the solid surfaces of a decoded map, laid on the engine's axes, plus the
/// bounds the engine requires every position to lie inside.
/// </para>
/// <para>
/// The checks here are the ones that keep a party from falling through a floor. A place whose geometry
/// fails any of them is refused as a whole rather than emitted partially: a partly emitted place is a
/// place with a hole in it, which is exactly the failure this refuses to ship.
/// </para>
/// </remarks>
public static class CollisionArtifact
{
    /// <summary>The one artifact schema version the engine accepts.</summary>
    private const int SchemaVersion = 1;

    /// <summary>
    /// The greatest magnitude the engine accepts on any axis, per <c>MAX_SPATIAL_CONTENT_COORDINATE</c>.
    /// </summary>
    private const double MaximumCoordinate = 10_000_000;

    /// <summary>The greatest number of positions one artifact may carry, per the engine's quota.</summary>
    private const int MaximumVertices = 1_000_000;

    /// <summary>The greatest number of triangles one artifact may carry, per the engine's quota.</summary>
    private const int MaximumTriangles = 1_000_000;

    /// <summary>
    /// How steep a surface may be and still count as ground the party stands on, in degrees.
    /// </summary>
    /// <remarks>
    /// The engine's own character controller holds a body on a surface whose normal is within this angle
    /// of up (the controller's default <c>maximum_slope_radians</c> is 50 degrees,
    /// <c>rust/crates/engine-spatial/src/character_controller.rs:80</c>), so a place validated against
    /// any steeper surface would be validated against ground the party slides off.
    /// </remarks>
    private const double WalkableSlopeDegrees = 50;

    /// <summary>
    /// How far outside a surface's own edge an arrival point may still count as standing on it.
    /// </summary>
    /// <remarks>
    /// The donor allows the same slack when it looks for the floor under a position, and says why:
    /// "there are actual holes in level geometry sometimes, up to several units wide" (OpenEnroth
    /// <c>src/Application/GameConfig.h:143</c>, <c>floor_checks_eps</c> default 3).
    /// </remarks>
    private const double ArrivalSlack = 3;

    /// <summary>The cosine of the steepest surface the party stands on.</summary>
    private static readonly double WalkableNormalY = Math.Cos(WalkableSlopeDegrees * Math.PI / 180);

    /// <summary>
    /// Checks a place's mesh, returning why it cannot be admitted as collision, or null when it can.
    /// </summary>
    /// <remarks>
    /// The order is deliberate: a mesh with nothing in it is refused before anything is measured against
    /// it, and the arrival check comes last because it is the one that speaks about the party rather than
    /// about the mesh.
    /// </remarks>
    /// <param name="mesh">The place's geometry, in the engine's axes.</param>
    /// <param name="arrivals">Where the party can enter the place; every one of them needs ground.</param>
    public static CollisionRefusal? Validate(CollisionMesh mesh, IReadOnlyList<MapEntryPoint> arrivals)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(arrivals);

        if (mesh.TriangleCount == 0)
        {
            return new CollisionRefusal(
                "no-solid-geometry",
                mesh.DroppedFaces > 0
                    ? $"the map's {mesh.DroppedFaces} solid faces all had fewer than three corners, so the place has no surface to stand on"
                    : "the map's solid faces produced no triangle, so the place has no surface to stand on");
        }

        if (mesh.VertexCount > MaximumVertices || mesh.TriangleCount > MaximumTriangles)
        {
            return new CollisionRefusal(
                "outside-engine-quota",
                $"the place carries {mesh.VertexCount} positions and {mesh.TriangleCount} triangles, and the engine admits at most {MaximumVertices} and {MaximumTriangles}");
        }

        for (int index = 0; index < mesh.VertexCount; index++)
        {
            (double x, double y, double z) = mesh.Position(index);
            if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(z))
            {
                return new CollisionRefusal(
                    "non-finite-coordinate",
                    $"position {index} is ({x}, {y}, {z}), and a position the engine cannot measure is not a surface");
            }

            if (Math.Abs(x) > MaximumCoordinate || Math.Abs(y) > MaximumCoordinate || Math.Abs(z) > MaximumCoordinate)
            {
                return new CollisionRefusal(
                    "outside-engine-limits",
                    $"position {index} is ({x}, {y}, {z}), and the engine admits coordinates no greater than {MaximumCoordinate} on any axis");
            }
        }

        int standable = 0;
        for (int index = 0; index < mesh.TriangleCount; index++)
        {
            (int a, int b, int c) = Triangle(mesh, index);
            if (a == b || a == c || b == c)
            {
                return new CollisionRefusal(
                    "degenerate-triangle",
                    $"triangle {index} repeats a position index, which the engine's own artifact parser refuses");
            }

            if (NormalY(mesh, a, b, c) is not { } up)
            {
                return new CollisionRefusal(
                    "degenerate-triangle",
                    $"triangle {index} encloses no area, so it has no surface for the engine to collide against");
            }

            if (up >= WalkableNormalY) standable++;
        }

        if (standable == 0)
        {
            return new CollisionRefusal(
                "no-standable-surface",
                $"none of the place's {mesh.TriangleCount} triangles is within {WalkableSlopeDegrees} degrees of level, so there is nowhere in it to stand");
        }

        foreach (MapEntryPoint arrival in arrivals)
        {
            if (!HasGround(mesh, arrival))
            {
                return new CollisionRefusal(
                    "arrival-without-ground",
                    $"arrival '{arrival.Name}' at ({arrival.Position.X}, {arrival.Position.Y}, {arrival.Position.Z}) stands over no surface within {WalkableSlopeDegrees} degrees of level, so the party would fall through where it arrives");
            }
        }

        return null;
    }

    /// <summary>Writes the engine's artifact document for a place's mesh.</summary>
    /// <remarks>
    /// The document is written compactly rather than indented: these bytes are handed to the engine's
    /// content owner and parsed by it, and their only reader is that parser. The outer pack document
    /// stays indented for a person, and this document is embedded in it unchanged.
    /// </remarks>
    /// <param name="placeId">The place's own id, which names the artifact inside the document.</param>
    /// <param name="mesh">The place's geometry, already validated.</param>
    public static string Write(int placeId, CollisionMesh mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        (double[] min, double[] max) = Bounds(mesh);

        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", SchemaVersion);
            writer.WriteString("staticMeshArtifactId", $"mm7/place/{placeId}/collision");
            writer.WriteStartObject("bounds");
            WriteVector(writer, "min", min);
            WriteVector(writer, "max", max);
            writer.WriteEndObject();

            writer.WriteStartObject("collision");
            writer.WriteStartArray("positions");
            for (int index = 0; index < mesh.VertexCount; index++)
            {
                (double x, double y, double z) = mesh.Position(index);
                writer.WriteStartArray();
                writer.WriteNumberValue(x);
                writer.WriteNumberValue(y);
                writer.WriteNumberValue(z);
                writer.WriteEndArray();
            }

            writer.WriteEndArray();
            writer.WriteStartArray("triangles");
            for (int index = 0; index < mesh.TriangleCount; index++)
            {
                (int a, int b, int c) = Triangle(mesh, index);
                writer.WriteStartArray();
                writer.WriteNumberValue(a);
                writer.WriteNumberValue(b);
                writer.WriteNumberValue(c);
                writer.WriteEndArray();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();

            // No walkable cell is emitted: a navigation grid is derived from a coherent collision scene,
            // and the engine does exactly that through its own spatial service
            // (ISpatialService.ReplaceCollisionNavigation, NavigationSource::CollisionDerived). Cells
            // invented here from the same triangles would be a second derivation that could disagree
            // with the one the engine would do, so the artifact declares the grid its cells would live on
            // and leaves the cells to the engine. The configuration below is therefore inert until a
            // consumer derives cells; its values are stated rather than taken from geometry.
            writer.WriteStartObject("navigation");
            writer.WriteString("id", $"mm7/place/{placeId}/navigation");
            writer.WriteStartObject("config");
            writer.WriteNumber("schemaVersion", SchemaVersion);
            writer.WriteNumber("cellSize", NavigationCellSize);
            writer.WriteNumber("levelQuantum", NavigationLevelQuantum);
            writer.WriteNumber("maximumSlopeDegrees", WalkableSlopeDegrees);
            writer.WriteNumber("requiredHeadroom", NavigationHeadroom);
            writer.WriteNumber("supportProbeDrop", NavigationSupportProbeDrop);
            writer.WriteEndObject();
            writer.WriteStartArray("cells");
            writer.WriteEndArray();
            writer.WriteEndObject();

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>How wide one navigation cell is, in place units.</summary>
    /// <remarks>Two party radii, rounded to a power of two so a grid of them stays aligned to the map's own cells.</remarks>
    private const double NavigationCellSize = 64;

    /// <summary>How finely a navigation cell samples height, in place units: a quarter of a cell.</summary>
    private const double NavigationLevelQuantum = 16;

    /// <summary>How much room a navigation step needs above its support, in place units.</summary>
    /// <remarks>One party height (OpenEnroth <c>src/Engine/Party.h:280</c>, "Party height, 192 by default").</remarks>
    private const double NavigationHeadroom = 192;

    /// <summary>How far below a candidate support a probe reaches before giving up, in place units.</summary>
    private const double NavigationSupportProbeDrop = 16;

    /// <summary>Whether any standable triangle covers an arrival point's own column.</summary>
    private static bool HasGround(CollisionMesh mesh, MapEntryPoint arrival)
    {
        double x = arrival.Position.X;
        double z = -arrival.Position.Y;
        for (int index = 0; index < mesh.TriangleCount; index++)
        {
            (int a, int b, int c) = Triangle(mesh, index);
            if (NormalY(mesh, a, b, c) is not { } up || up < WalkableNormalY) continue;
            if (Covers(mesh.Position(a), mesh.Position(b), mesh.Position(c), x, z)) return true;
        }

        return false;
    }

    /// <summary>
    /// Whether a point lies inside a triangle's own plan projection, with the donor's slack at the edges.
    /// </summary>
    /// <remarks>
    /// A standable triangle is close to level, so its plan projection is the surface the party lands on
    /// and the two-dimensional test is the same question the donor asks of a floor polygon.
    /// </remarks>
    private static bool Covers(
        (double X, double Y, double Z) a,
        (double X, double Y, double Z) b,
        (double X, double Y, double Z) c,
        double x,
        double z)
    {
        double orientation = Cross(b.X - a.X, b.Z - a.Z, c.X - a.X, c.Z - a.Z);
        if (orientation == 0) return false;

        double sign = Math.Sign(orientation);
        return Within(a, b) && Within(b, c) && Within(c, a);

        // The signed distance to an edge, in units: a point outside by less than the slack still counts.
        bool Within((double X, double Y, double Z) from, (double X, double Y, double Z) to)
        {
            double edgeX = to.X - from.X;
            double edgeZ = to.Z - from.Z;
            double length = Math.Sqrt((edgeX * edgeX) + (edgeZ * edgeZ));
            double distance = Cross(edgeX, edgeZ, x - from.X, z - from.Z) / length;
            return sign * distance >= -ArrivalSlack;
        }
    }

    /// <summary>The two-dimensional cross product of two vectors.</summary>
    private static double Cross(double ax, double az, double bx, double bz) => (ax * bz) - (az * bx);

    /// <summary>A triangle's unit normal's upward component, or null when it encloses no area.</summary>
    private static double? NormalY(CollisionMesh mesh, int a, int b, int c)
    {
        (double ax, double ay, double az) = mesh.Position(a);
        (double bx, double by, double bz) = mesh.Position(b);
        (double cx, double cy, double cz) = mesh.Position(c);
        double ux = bx - ax, uy = by - ay, uz = bz - az;
        double vx = cx - ax, vy = cy - ay, vz = cz - az;
        double nx = (uy * vz) - (uz * vy);
        double ny = (uz * vx) - (ux * vz);
        double nz = (ux * vy) - (uy * vx);
        double length = Math.Sqrt((nx * nx) + (ny * ny) + (nz * nz));
        return length == 0 ? null : ny / length;
    }

    /// <summary>A whole triangle's three position indices.</summary>
    private static (int A, int B, int C) Triangle(CollisionMesh mesh, int index) =>
        (mesh.Triangles[index * 3], mesh.Triangles[(index * 3) + 1], mesh.Triangles[(index * 3) + 2]);

    /// <summary>The least and greatest value on each axis, which is the bounds the engine is given.</summary>
    private static (double[] Min, double[] Max) Bounds(CollisionMesh mesh)
    {
        double[] min = [double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity];
        double[] max = [double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity];
        for (int index = 0; index < mesh.VertexCount; index++)
        {
            (double x, double y, double z) = mesh.Position(index);
            min[0] = Math.Min(min[0], x);
            min[1] = Math.Min(min[1], y);
            min[2] = Math.Min(min[2], z);
            max[0] = Math.Max(max[0], x);
            max[1] = Math.Max(max[1], y);
            max[2] = Math.Max(max[2], z);
        }

        return (min, max);
    }

    /// <summary>Writes one three-component vector as an array.</summary>
    private static void WriteVector(Utf8JsonWriter writer, string name, double[] values)
    {
        writer.WriteStartArray(name);
        foreach (double value in values) writer.WriteNumberValue(value);
        writer.WriteEndArray();
    }
}
