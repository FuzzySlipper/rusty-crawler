using MightAndMagic7.Import.Maps;

namespace MightAndMagic7.Import.Collision;

/// <summary>What one place's collision emission produced.</summary>
/// <param name="PlaceId">The place's own id, which names the artifact's entry in the pack.</param>
/// <param name="FileName">The container entry the place's map was decoded from.</param>
/// <param name="Kind">Which map family the place came from.</param>
/// <param name="Counts">What each geometry source contributed.</param>
/// <param name="Vertices">How many positions the artifact carries.</param>
/// <param name="Triangles">How many triangles the artifact carries.</param>
/// <param name="DroppedFaces">How many solid faces carried fewer than three corners.</param>
/// <param name="DroppedTriangles">How many fan triangles enclosed no area.</param>
/// <param name="Artifact">The engine's document, or null when the place was refused.</param>
/// <param name="Refusal">Why the place was refused, or null when it was emitted.</param>
/// <remarks>
/// <see cref="Surfaces"/> are the parts of the place's ground that are not ordinary ground — its water squares and its
/// fluid faces — each as a mesh of its own over the same corners the collision has, in the engine's axes. They are
/// beside the artifact rather than in it, because the engine parses the artifact and refuses a field it does not know.
/// </remarks>
public sealed record PlaceCollision(
    int PlaceId,
    string FileName,
    MapKind Kind,
    IReadOnlyDictionary<CollisionSource, CollisionSourceCounts> Counts,
    int Vertices,
    int Triangles,
    int DroppedFaces,
    int DroppedTriangles,
    string? Artifact,
    CollisionRefusal? Refusal)
{
    /// <summary>Whether a place's geometry was emitted, which is the same as its artifact existing.</summary>
    public bool Emitted { get; init; } = Artifact is not null;

    /// <summary>The named ground surfaces the place's collision carries, each a mesh in the engine's axes.</summary>
    public IReadOnlyList<PlaceSurface> Surfaces { get; init; } = [];

    /// <summary>The region and sampling scale for Engine-derived navigation, absent when collision was refused.</summary>
    public CollisionNavigationRegion? NavigationRegion { get; init; }

    /// <summary>How many of the place's terrain squares are water.</summary>
    public int WaterSquares { get; init; }

    /// <summary>How many of the place's solid faces are fluid.</summary>
    public int FluidFaces { get; init; }

    /// <summary>The same outcome with the documents dropped, for a report that only states facts.</summary>
    public PlaceCollision Facts => this with { Artifact = null, Surfaces = [], Layout = null };

    /// <summary>Complete authored collision partition, with per-corner door travel and event face groups.</summary>
    public PlaceCollisionLayout? Layout { get; init; }

    /// <summary>How many doors own collision corners in the complete partition.</summary>
    public int CollisionDoors { get; init; }

    /// <summary>How many faces are controlled by a door or a face group.</summary>
    public int MutableFaces { get; init; }

    /// <summary>What one source contributed.</summary>
    /// <param name="source">The source to report.</param>
    public CollisionSourceCounts CountOf(CollisionSource source) =>
        Counts.TryGetValue(source, out CollisionSourceCounts counts) ? counts : CollisionSourceCounts.None;
}

/// <summary>Collision vertex bounds in Engine axes and the sampling scale requested for their navigation.</summary>
/// <param name="Minimum">The least coordinate on each axis, from the collision vertices.</param>
/// <param name="Maximum">The greatest coordinate on each axis, from the same vertices.</param>
/// <param name="CellSize">The navigation sampling width: fine indoors, a terrain square outdoors.</param>
public sealed record CollisionNavigationRegion(double[] Minimum, double[] Maximum, double CellSize);

/// <summary>One named kind of ground in a place, and the triangles that are it.</summary>
/// <param name="Surface">
/// The ground's name as the pack writes it: <see cref="PlaceCollisionEmitter.WaterSurface"/> for the terrain's water
/// squares, <see cref="PlaceCollisionEmitter.FluidSurface"/> for faces the level marks fluid.
/// </param>
/// <param name="Mesh">The triangles of that ground, in the engine's axes, the same triangles the collision carries.</param>
public sealed record PlaceSurface(string Surface, CollisionMesh Mesh);

/// <summary>What one import's collision emission did, over every place it read.</summary>
/// <param name="Places">Every place, in place-id order, emitted or refused.</param>
public sealed record CollisionSummary(IReadOnlyList<PlaceCollision> Places)
{
    /// <summary>Summarizes outcomes, dropping the documents and ordering the places by their id.</summary>
    /// <param name="places">Every place's outcome.</param>
    public static CollisionSummary Of(IEnumerable<PlaceCollision> places) =>
        new([.. places.OrderBy(place => place.PlaceId).Select(place => place.Facts)]);

    /// <summary>How many places the import read.</summary>
    public int PlaceCount => Places.Count;

    /// <summary>How many places produced an artifact.</summary>
    public int EmittedCount => Places.Count(place => place.Emitted);

    /// <summary>How many places could not be closed enough and produced no artifact.</summary>
    public int RefusedCount => PlaceCount - EmittedCount;

    /// <summary>The places that produced no artifact, each with the reason it did not.</summary>
    public IReadOnlyList<PlaceCollision> Refused => [.. Places.Where(place => !place.Emitted)];

    /// <summary>How many emitted places belong to one map family.</summary>
    /// <param name="kind">The family to count.</param>
    public int EmittedOf(MapKind kind) => Places.Count(place => place.Emitted && place.Kind == kind);

    /// <summary>What one geometry source contributed across every emitted place.</summary>
    /// <param name="source">The source to total.</param>
    public CollisionSourceCounts CountOf(CollisionSource source) =>
        Places.Aggregate(CollisionSourceCounts.None, (sum, place) => sum + place.CountOf(source));

    /// <summary>How many positions every emitted artifact carries together.</summary>
    public int Vertices => Places.Where(place => place.Emitted).Sum(place => place.Vertices);

    /// <summary>How many triangles every emitted artifact carries together.</summary>
    public int Triangles => Places.Where(place => place.Emitted).Sum(place => place.Triangles);
}

/// <summary>
/// Reads the collision geometry a place's decoded map already states.
/// </summary>
/// <remarks>
/// <para>
/// Nothing is decoded again here: an interior's faces arrive with their corners resolved, and a region's
/// terrain arrives as its height field. What this adds is the two things the map payloads leave to a
/// consumer — which faces are solid, and how a height field becomes surfaces.
/// </para>
/// <para>
/// A face is solid unless the level says otherwise, and the level says so in two ways the donor also
/// honours. A portal is the plane between two sectors: it is walked through, and admitting it would seal
/// every doorway in the level (<c>FACE_IsPortal</c>, OpenEnroth
/// <c>src/Engine/Graphics/FaceEnums.h:5</c>, skipped for collision at
/// <c>src/Engine/Graphics/Collisions.cpp:231</c>). An ethereal face is "untouchable, you can pass
/// through it" (<c>FACE_ETHEREAL</c>, <c>FaceEnums.h:39</c>, skipped at <c>Collisions.cpp:101</c>). A
/// fluid face is <em>not</em> excluded: the flag means the party is standing on water
/// (<c>src/Engine/Graphics/Indoor.cpp:1499</c>), and the floor under water is still a floor — dropping it
/// would drop a room's own floor.
/// </para>
/// <para>
/// <b>Which ground is water.</b> The place's water is named beside its collision as two surfaces. The terrain's water
/// squares are the ones whose tile the game's tile table flags as water (<see cref="TerrainTileTable"/>), which is the
/// donor's own test for water under a party outdoors (OpenEnroth <c>src/Engine/Graphics/OutdoorTerrain.cpp:114-120</c>,
/// read at <c>src/Engine/Graphics/Outdoor.cpp:1321-1323</c>). A face the level marks fluid (<c>FACE_IsFluid</c>, 0x10,
/// <c>src/Engine/Graphics/FaceEnums.h:12</c>) is a second surface, because the donor treats it differently: a party on
/// one is "on water" (<c>src/Engine/Graphics/Indoor.cpp:1499</c>, <c>Outdoor.cpp:854</c>) for its footsteps, and is
/// never drowned there. What either surface does is the ruleset's; the importer only says where they are.
/// </para>
/// </remarks>
public static class PlaceCollisionEmitter
{
    /// <summary>The surface the terrain's water squares are written under.</summary>
    public const string WaterSurface = "water";

    /// <summary>The surface faces the level marks fluid are written under.</summary>
    public const string FluidSurface = "fluid";

    /// <summary>The donor's fluid attribute, <c>FACE_IsFluid</c>.</summary>
    private const uint FluidAttribute = 0x00000010;

    /// <summary>The donor's portal attribute, <c>FACE_IsPortal</c>.</summary>
    private const uint PortalAttribute = 0x00000001;

    /// <summary>The donor's ethereal attribute, <c>FACE_ETHEREAL</c>.</summary>
    private const uint EtherealAttribute = 0x20000000;

    /// <summary>The squares one outdoor map's terrain has per side.</summary>
    /// <remarks>
    /// The height field stores 128 by 128 corner heights, so the map is 127 by 127 squares
    /// (OpenEnroth <c>src/Engine/Graphics/OutdoorTerrain.cpp:11-14</c>).
    /// </remarks>
    private const int TerrainSquares = OutdoorMap.TerrainCells - 1;

    /// <summary>Emits one place's collision, or refuses it and names why.</summary>
    /// <param name="placeId">The place's own id.</param>
    /// <param name="fileName">The container entry the place's map was decoded from.</param>
    /// <param name="map">The decoded map whose geometry the place's collision comes from.</param>
    /// <param name="tiles">
    /// The game's terrain tile table, which says which of a region's squares are water. Without one no square is
    /// marked water; a place's fluid faces are marked either way.
    /// </param>
    public static PlaceCollision Emit(int placeId, string fileName, DecodedMap map, TerrainTileTable? tiles = null)
    {
        ArgumentNullException.ThrowIfNull(map);

        CollisionMesh mesh = new();
        CollisionMesh water = new();
        CollisionMesh fluid = new();
        Dictionary<CollisionSource, CollisionSourceCounts> counts = [];
        int waterSquares = 0;
        if (map is IndoorMap indoor) AddIndoorFaces(mesh, counts, indoor, fluid);
        if (map is OutdoorMap outdoor)
        {
            waterSquares = AddTerrain(mesh, counts, outdoor, tiles?.WaterSquares(outdoor), water);
            AddModelFaces(mesh, counts, outdoor, fluid);
        }

        List<PlaceSurface> surfaces = [];
        if (water.TriangleCount > 0) surfaces.Add(new PlaceSurface(WaterSurface, water));
        if (fluid.TriangleCount > 0) surfaces.Add(new PlaceSurface(FluidSurface, fluid));

        CollisionRefusal? refusal = CollisionArtifact.Validate(mesh, map.EntryPoints);
        PlaceCollisionLayout? layout = refusal is null ? PlaceCollisionLayout.From(map) : null;
        return new PlaceCollision(
            placeId,
            fileName,
            map.Kind,
            counts,
            mesh.VertexCount,
            mesh.TriangleCount,
            mesh.DroppedFaces,
            mesh.DroppedTriangles,
            refusal is null ? CollisionArtifact.Write(placeId, mesh) : null,
            refusal)
        {
            Surfaces = refusal is null ? surfaces : [],
            NavigationRegion = refusal is null ? NavigationRegion(mesh, map.Kind, layout) : null,
            WaterSquares = waterSquares,
            FluidFaces = FluidFacesOf(map),
            Layout = layout,
            CollisionDoors = layout?.Faces.SelectMany(face => face.Corners).Where(corner => corner.Door is not null).Select(corner => corner.Door).Distinct().Count() ?? 0,
            MutableFaces = layout?.Faces.Count ?? 0,
        };
    }

    /// <summary>States a navigation request over the same geometry, without deriving any walkable cells.</summary>
    private static CollisionNavigationRegion NavigationRegion(CollisionMesh mesh, MapKind kind, PlaceCollisionLayout? layout)
    {
        (double[] minimum, double[] maximum) = CollisionArtifact.Bounds(mesh);
        foreach (CollisionCorner corner in layout?.Faces.SelectMany(face => face.Corners) ?? [])
        {
            for (int axis = 0; axis < 3; axis++)
            {
                minimum[axis] = Math.Min(minimum[axis], Math.Min(corner.Rest[axis], corner.Rest[axis] + corner.Travel[axis]));
                maximum[axis] = Math.Max(maximum[axis], Math.Max(corner.Rest[axis], corner.Rest[axis] + corner.Travel[axis]));
            }
        }
        return new CollisionNavigationRegion(minimum, maximum,
            kind == MapKind.Outdoor ? OutdoorMap.TerrainCellSize : 128);
    }

    /// <summary>How many of a map's solid faces the level marks fluid.</summary>
    private static int FluidFacesOf(DecodedMap map) => map switch
    {
        IndoorMap indoor => indoor.Faces.Count(IsFluid),
        OutdoorMap outdoor => outdoor.Models.SelectMany(model => model.Faces).Count(IsFluid),
        _ => 0,
    };

    /// <summary>Whether a face is solid and marked fluid, which is the ground a party walks on as water.</summary>
    /// <param name="face">The face to judge.</param>
    public static bool IsFluid(MapFace face) => IsSolid(face) && (face.Attributes & FluidAttribute) != 0;

    /// <summary>Adds an interior's solid faces.</summary>
    private static void AddIndoorFaces(
        CollisionMesh mesh,
        Dictionary<CollisionSource, CollisionSourceCounts> counts,
        IndoorMap map,
        CollisionMesh fluid)
    {
        CollisionSourceCounts added = AddSolidFaces(mesh, map.Faces, fluid);
        if (added.Faces > 0) counts[CollisionSource.InteriorFace] = added;
    }

    /// <summary>Adds every solid face of a face list to a mesh, and returns what they contributed.</summary>
    /// <remarks>
    /// Both families' face lists go through here, so which faces are solid is decided once: an interior's
    /// face array and an outdoor model's face array are read with the same rule, and a test can state
    /// that rule over faces it builds itself instead of over a map payload.
    /// </remarks>
    /// <param name="mesh">The mesh to add to.</param>
    /// <param name="faces">The faces to read.</param>
    /// <param name="fluid">Where the solid faces marked fluid are added a second time, or null to mark none.</param>
    public static CollisionSourceCounts AddSolidFaces(CollisionMesh mesh, IEnumerable<MapFace> faces, CollisionMesh? fluid = null)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(faces);

        CollisionSourceCounts added = CollisionSourceCounts.None;
        foreach (MapFace face in faces)
        {
            if (!IsSolid(face)) continue;
            added = added.Plus(mesh.AddPolygon(face.Vertices));
            if (fluid is not null && IsFluid(face)) fluid.AddPolygon(face.Vertices);
        }

        return added;
    }

    /// <summary>Adds an outdoor map's terrain as surfaces over its height field.</summary>
    /// <remarks>
    /// The height field holds a height per <em>corner</em>, and a square is split along the diagonal from
    /// its north-west corner to its south-east one, which is how the donor reads a height out of the
    /// field: its interpolation branches on the two triangles this split leaves
    /// (OpenEnroth <c>src/Engine/Graphics/OutdoorTerrain.cpp:53-93</c>, and the corners it samples at
    /// <c>OutdoorTerrain.cpp:236-247</c>). The winding is the donor's, so the surface's own normal points
    /// up and the engine's collision sees the ground the party walks on.
    /// </remarks>
    /// <returns>How many squares were water.</returns>
    internal static int AddTerrain(
        CollisionMesh mesh,
        Dictionary<CollisionSource, CollisionSourceCounts> counts,
        OutdoorMap map,
        bool[]? waterSquares,
        CollisionMesh water)
    {
        CollisionSourceCounts added = CollisionSourceCounts.None;
        int wet = 0;
        for (int row = 0; row < TerrainSquares; row++)
        {
            for (int column = 0; column < TerrainSquares; column++)
            {
                MapPoint northWest = map.CellToWorld(column, row);
                MapPoint northEast = map.CellToWorld(column + 1, row);
                MapPoint southWest = map.CellToWorld(column, row + 1);
                MapPoint southEast = map.CellToWorld(column + 1, row + 1);
                int triangles = 0;
                if (mesh.AddTriangle(northWest, southWest, southEast)) triangles++;
                if (mesh.AddTriangle(northWest, southEast, northEast)) triangles++;

                // A water square is the same two triangles, named as water beside the collision.
                if (waterSquares?[(row * TerrainSquares) + column] == true)
                {
                    water.AddTriangle(northWest, southWest, southEast);
                    water.AddTriangle(northWest, southEast, northEast);
                    wet++;
                }

                // A terrain "face" is one of the height field's squares, which is the unit the map
                // stores; the two triangles it becomes are counted beside it.
                added = new CollisionSourceCounts(added.Faces + 1, added.Triangles + triangles);
            }
        }

        counts[CollisionSource.Terrain] = added;
        return wet;
    }

    /// <summary>Adds the solid faces of an outdoor map's placed models.</summary>
    private static void AddModelFaces(
        CollisionMesh mesh,
        Dictionary<CollisionSource, CollisionSourceCounts> counts,
        OutdoorMap map,
        CollisionMesh fluid)
    {
        CollisionSourceCounts added = AddSolidFaces(mesh, map.Models.SelectMany(model => model.Faces), fluid);
        if (added.Faces > 0) counts[CollisionSource.ModelFace] = added;
    }

    /// <summary>Whether a face is a surface the party collides against.</summary>
    /// <param name="face">The face to judge.</param>
    public static bool IsSolid(MapFace face)
    {
        ArgumentNullException.ThrowIfNull(face);
        return face.BackSectorId <= 0 && (face.Attributes & (PortalAttribute | EtherealAttribute)) == 0;
    }
}
