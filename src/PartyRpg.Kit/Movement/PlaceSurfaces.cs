using System.Numerics;

namespace PartyRpg.Kit.Movement;

/// <summary>One named kind of ground in a place: the triangles that are it, in the engine's world axes.</summary>
/// <param name="Surface">The ground's name, as content states it.</param>
/// <param name="Positions">The mesh's corners, in the engine's world axes.</param>
/// <param name="Triangles">Three corner indices per triangle.</param>
public sealed record SurfaceMesh(string Surface, IReadOnlyList<Vector3> Positions, IReadOnlyList<int> Triangles);

/// <summary>
/// The named ground of one place — its water, its road, whatever content names — and the answer to which of it a
/// point the engine reported lies on.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a second look at triangles the engine already holds.</b> The engine collides with a place as one mesh and
/// reports the ground it found by its source and its point, not by which of the mesh's triangles it was; a place's
/// content says which triangles are water. So the point the engine reported is looked up here among the triangles
/// content named, which are the same triangles the engine collided with. Nothing here sweeps, casts, or decides where
/// the party stands: the engine has already said that, and this only names it.
/// </para>
/// <para>
/// <b>Plan and height.</b> A point lies on a named triangle when the triangle's plan covers it and the triangle's
/// plane passes close to it, so a bridge over water is the bridge and not the water under it. Both closenesses are
/// stated against the mesh's own scale — the cell its triangles are indexed by — so the same rule serves a world in
/// any unit.
/// </para>
/// </remarks>
public sealed class PlaceSurfaces
{
    /// <summary>How far outside a triangle's edge a point may lie and still be on it, as a fraction of a cell.</summary>
    private const float PlanSlack = 1f / 1024f;

    /// <summary>How far above or below a named triangle a point may lie and still be on it, as a fraction of a cell.</summary>
    private const float HeightSlack = 1f / 16f;

    private readonly Face[] _faces;
    private readonly Dictionary<(int Column, int Row), List<int>> _cells = [];
    private readonly float _cell;
    private readonly float _originX;
    private readonly float _originZ;

    /// <summary>A place with no named ground: everything in it is ordinary.</summary>
    public static PlaceSurfaces None { get; } = new([]);

    /// <summary>Indexes a place's named ground.</summary>
    /// <param name="meshes">Each named kind of ground and its triangles.</param>
    /// <exception cref="ArgumentException">A mesh has no name, a corner that is not a number, or a triangle that names no corner.</exception>
    public PlaceSurfaces(IEnumerable<SurfaceMesh> meshes)
    {
        ArgumentNullException.ThrowIfNull(meshes);
        List<Face> faces = [];
        HashSet<string> names = new(StringComparer.Ordinal);
        foreach (SurfaceMesh mesh in meshes)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(mesh.Surface);
            if (mesh.Triangles.Count % 3 != 0)
            {
                throw new ArgumentException($"Surface '{mesh.Surface}' lists {mesh.Triangles.Count} corner indices, which is not whole triangles.", nameof(meshes));
            }

            foreach (Vector3 position in mesh.Positions)
            {
                if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z))
                {
                    throw new ArgumentException($"Surface '{mesh.Surface}' has a corner that is not a number, so no point could be measured against it.", nameof(meshes));
                }
            }

            names.Add(mesh.Surface);
            for (int index = 0; index < mesh.Triangles.Count; index += 3)
            {
                faces.Add(new Face(mesh.Surface, Corner(mesh, index), Corner(mesh, index + 1), Corner(mesh, index + 2)));
            }
        }

        _faces = [.. faces];
        Names = names;
        if (_faces.Length == 0) return;

        // The cell is the triangles' own typical size, so a triangle touches a handful of cells and a cell a handful
        // of triangles whatever the world's unit is.
        _cell = Math.Max(1e-3f, (float)_faces.Average(face => Math.Max(face.MaxX - face.MinX, face.MaxZ - face.MinZ)));
        _originX = _faces.Min(face => face.MinX);
        _originZ = _faces.Min(face => face.MinZ);
        for (int index = 0; index < _faces.Length; index++)
        {
            Face face = _faces[index];
            for (int column = Column(face.MinX); column <= Column(face.MaxX); column++)
            {
                for (int row = Row(face.MinZ); row <= Row(face.MaxZ); row++)
                {
                    if (!_cells.TryGetValue((column, row), out List<int>? cell))
                    {
                        cell = [];
                        _cells[(column, row)] = cell;
                    }

                    cell.Add(index);
                }
            }
        }
    }

    /// <summary>The names of the ground this place has.</summary>
    public IReadOnlyCollection<string> Names { get; }

    /// <summary>How many named triangles the place has.</summary>
    public int TriangleCount => _faces.Length;

    /// <summary>Which named ground a point lies on, if any.</summary>
    /// <param name="point">The point, in the engine's world axes — the ground point the engine reported.</param>
    /// <param name="surfaceId">The ground's name when the point lies on named ground.</param>
    /// <returns>Whether the point lies on named ground. A point on none is on ordinary ground.</returns>
    public bool Classify(Vector3 point, out string surfaceId)
    {
        surfaceId = string.Empty;
        if (_faces.Length == 0 || !_cells.TryGetValue((Column(point.X), Row(point.Z)), out List<int>? candidates)) return false;

        float best = HeightSlack * _cell;
        bool found = false;
        foreach (int index in candidates)
        {
            Face face = _faces[index];
            if (face.HeightAt(point.X, point.Z, PlanSlack * _cell) is not { } height) continue;
            float gap = Math.Abs(height - point.Y);
            if (gap > best) continue;
            best = gap;
            surfaceId = face.Surface;
            found = true;
        }

        return found;
    }

    private int Column(float x) => (int)MathF.Floor((x - _originX) / _cell);

    private int Row(float z) => (int)MathF.Floor((z - _originZ) / _cell);

    private static Vector3 Corner(SurfaceMesh mesh, int index)
    {
        int corner = mesh.Triangles[index];
        if (corner < 0 || corner >= mesh.Positions.Count)
        {
            throw new ArgumentException($"Surface '{mesh.Surface}' has a triangle naming corner {corner}, and it has {mesh.Positions.Count}.", nameof(mesh));
        }

        return mesh.Positions[corner];
    }

    /// <summary>One named triangle, with its plan bounds.</summary>
    private readonly record struct Face(string Surface, Vector3 A, Vector3 B, Vector3 C)
    {
        public float MinX => Math.Min(A.X, Math.Min(B.X, C.X));

        public float MaxX => Math.Max(A.X, Math.Max(B.X, C.X));

        public float MinZ => Math.Min(A.Z, Math.Min(B.Z, C.Z));

        public float MaxZ => Math.Max(A.Z, Math.Max(B.Z, C.Z));

        /// <summary>The triangle's height over a plan point it covers, or null when it does not cover it.</summary>
        public float? HeightAt(float x, float z, float slack)
        {
            // Barycentric weights in plan; a triangle standing on its edge covers no plan at all and names no ground.
            float area = Cross(B.X - A.X, B.Z - A.Z, C.X - A.X, C.Z - A.Z);
            if (MathF.Abs(area) < 1e-9f) return null;
            float wb = Cross(x - A.X, z - A.Z, C.X - A.X, C.Z - A.Z) / area;
            float wc = Cross(B.X - A.X, B.Z - A.Z, x - A.X, z - A.Z) / area;
            float wa = 1 - wb - wc;

            // The slack is a length; the weights are fractions of the triangle, so it is measured against its size.
            float tolerance = slack / MathF.Max(1e-6f, MathF.Max(MaxX - MinX, MaxZ - MinZ));
            if (wa < -tolerance || wb < -tolerance || wc < -tolerance) return null;
            return (wa * A.Y) + (wb * B.Y) + (wc * C.Y);
        }

        private static float Cross(float ax, float az, float bx, float bz) => (ax * bz) - (az * bx);
    }
}
