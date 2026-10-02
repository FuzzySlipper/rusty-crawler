using System.Text.Json;
using MightAndMagic7.Import.Collision;
using MightAndMagic7.Import.Lod;
using MightAndMagic7.Import.Maps;
using Xunit;

namespace MightAndMagic7.Import.Tests;

/// <summary>
/// What a place's collision is made of, and when a place has none.
/// </summary>
/// <remarks>
/// The artifact's shape is the engine's, so the tests here state it as the engine's parser sees it: an
/// exact field set in camel case with nothing extra, because the parser refuses unknown fields. The
/// surfaces are exercised over faces these tests build themselves, so nothing here needs the operator's
/// game data.
/// </remarks>
public sealed class CollisionTests
{
    /// <summary>The donor's face attribute words, from OpenEnroth <c>src/Engine/Graphics/FaceEnums.h</c>.</summary>
    private const uint IsPortal = 0x00000001;
    private const uint IsFluid = 0x00000010;
    private const uint IsInvisible = 0x00002000;
    private const uint Ethereal = 0x20000000;

    /// <summary>An attribute word no shipped face carries on its own, used as the "plain face" case.</summary>
    private const uint Unremarkable = 0x00000008;

    [Theory]
    [InlineData(Unremarkable, 0, true)]
    [InlineData(IsPortal, 0, false)]
    [InlineData(Unremarkable, 1, false)]
    [InlineData(Ethereal, 0, false)]
    [InlineData(IsPortal, 1, false)]
    [InlineData(IsFluid, 0, true)]
    [InlineData(IsInvisible, 0, true)]
    [InlineData(IsFluid | Ethereal, 0, false)]
    public void A_face_is_solid_unless_the_level_says_it_can_be_passed_through(
        uint attributes,
        int backSectorId,
        bool solid)
    {
        MapFace face = Quad(attributes, backSectorId);
        Assert.Equal(solid, PlaceCollisionEmitter.IsSolid(face));

        // The rule is only worth stating if it is the rule the mesh is built by: a face that is not solid
        // must contribute nothing, and one that is must contribute its fan.
        CollisionMesh mesh = new();
        CollisionSourceCounts counts = PlaceCollisionEmitter.AddSolidFaces(mesh, [face]);
        Assert.Equal(solid ? 1 : 0, counts.Faces);
        Assert.Equal(solid ? 2 : 0, mesh.TriangleCount);
    }

    [Fact]
    public void A_face_with_fewer_than_three_corners_contributes_no_surface()
    {
        MapFace face = Face(Unremarkable, 0, [(0, 0, 0), (100, 0, 0)]);
        CollisionMesh mesh = new();
        PlaceCollisionEmitter.AddSolidFaces(mesh, [face]);

        Assert.Equal(0, mesh.TriangleCount);
        Assert.Equal(1, mesh.DroppedFaces);
    }

    [Fact]
    public void A_polygon_corner_that_lies_on_its_neighbours_edge_drops_only_the_empty_triangle()
    {
        // A level's faces are not minimal polygons: a neighbouring face's corner sits in the middle of an
        // edge, and the fan triangle it makes carries no area at all.
        MapFace face = Face(Unremarkable, 0, [(0, 0, 0), (50, 0, 0), (100, 0, 0), (100, 100, 0)]);
        CollisionMesh mesh = new();
        CollisionSourceCounts counts = PlaceCollisionEmitter.AddSolidFaces(mesh, [face]);

        Assert.Equal(1, counts.Faces);
        Assert.Equal(1, mesh.TriangleCount);
        Assert.Equal(1, mesh.DroppedTriangles);
    }

    [Fact]
    public void Corners_are_laid_on_the_engines_own_axes_where_height_is_the_second_of_them()
    {
        CollisionMesh mesh = new();
        mesh.AddPolygon(Points((10, 20, 30), (110, 20, 30), (110, 120, 30), (10, 120, 30)));

        // A place stores height third; the engine's second axis is height and its third is the place's
        // second ground axis, negated. Getting this wrong is a mesh standing on its side.
        Assert.Equal([10d, 30d, -20d, 110d, 30d, -20d, 110d, 30d, -120d, 10d, 30d, -120d], mesh.Positions);
    }

    [Fact]
    public void An_arrival_point_covers_itself_after_the_engines_own_axis_rule()
    {
        // The same square as above, seen as a place: its corners in place coordinates, and an arrival on
        // it expressed in place coordinates, have to meet once both are on the engine's axes.
        CollisionMesh mesh = new();
        mesh.AddPolygon(Points((10, 20, 30), (110, 20, 30), (110, 120, 30), (10, 120, 30)));
        MapEntryPoint on = new("Party Start", new MapPoint(60, 70, 300), 0, 0);
        MapEntryPoint off = new("North Start", new MapPoint(600, 700, 300), 0, 1);

        Assert.Null(CollisionArtifact.Validate(mesh, [on]));
        CollisionRefusal? refusal = CollisionArtifact.Validate(mesh, [off]);
        Assert.Equal("arrival-without-ground", refusal?.Code);
    }

    [Fact]
    public void A_place_with_no_solid_face_at_all_is_refused()
    {
        CollisionRefusal? refusal = CollisionArtifact.Validate(new CollisionMesh(), []);
        Assert.Equal("no-solid-geometry", refusal?.Code);
    }

    [Fact]
    public void A_place_whose_only_surface_is_a_wall_is_refused()
    {
        CollisionMesh mesh = new();
        mesh.AddPolygon(Points((0, 0, 0), (0, 0, 100), (0, 100, 100), (0, 100, 0)));

        CollisionRefusal? refusal = CollisionArtifact.Validate(mesh, []);
        Assert.Equal("no-standable-surface", refusal?.Code);
    }

    [Fact]
    public void A_position_the_engine_cannot_hold_is_refused()
    {
        CollisionMesh mesh = new();
        mesh.AddPolygon(Points((0, 0, 0), (10_000_001, 0, 0), (10_000_001, 100, 10_000_001)));

        CollisionRefusal? refusal = CollisionArtifact.Validate(mesh, []);
        Assert.Equal("outside-engine-limits", refusal?.Code);
    }

    [Fact]
    public void The_artifact_is_the_document_the_engines_parser_expects()
    {
        CollisionMesh mesh = new();
        mesh.AddPolygon(Points((0, 0, 0), (512, 0, 0), (512, 512, 0), (0, 512, 0)));
        MapEntryPoint arrival = new("Party Start", new MapPoint(256, 256, 0), 0, 0);
        Assert.Null(CollisionArtifact.Validate(mesh, [arrival]));

        using JsonDocument document = JsonDocument.Parse(CollisionArtifact.Write(7, mesh));
        JsonElement root = document.RootElement;

        // The engine's parser refuses unknown fields, so the test states the whole shape rather than the
        // fields it happens to care about.
        Assert.Equal(
            ["schemaVersion", "staticMeshArtifactId", "bounds", "collision", "navigation"],
            Names(root));
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("mm7/place/7/collision", root.GetProperty("staticMeshArtifactId").GetString());

        JsonElement bounds = root.GetProperty("bounds");
        Assert.Equal(["min", "max"], Names(bounds));
        Assert.Equal([0d, 0d, -512d], Numbers(bounds.GetProperty("min")));
        Assert.Equal([512d, 0d, 0d], Numbers(bounds.GetProperty("max")));

        JsonElement collision = root.GetProperty("collision");
        Assert.Equal(["positions", "triangles"], Names(collision));
        JsonElement positions = collision.GetProperty("positions");
        JsonElement triangles = collision.GetProperty("triangles");
        Assert.Equal(4, positions.GetArrayLength());
        Assert.Equal(2, triangles.GetArrayLength());
        foreach (JsonElement position in positions.EnumerateArray())
        {
            // Every position lies inside the bounds the artifact declares: the engine refuses one that
            // does not, and a surface outside its own bounds is a mesh the collider cannot hold.
            Assert.Equal(3, position.GetArrayLength());
            for (int axis = 0; axis < 3; axis++)
            {
                double value = position[axis].GetDouble();
                Assert.InRange(value, Numbers(bounds.GetProperty("min"))[axis], Numbers(bounds.GetProperty("max"))[axis]);
            }
        }

        foreach (JsonElement triangle in triangles.EnumerateArray())
        {
            Assert.Equal(3, triangle.GetArrayLength());
            Assert.Equal(3, triangle.EnumerateArray().Select(index => index.GetInt32()).Distinct().Count());
            foreach (JsonElement index in triangle.EnumerateArray())
            {
                Assert.InRange(index.GetInt32(), 0, positions.GetArrayLength() - 1);
            }
        }

        JsonElement navigation = root.GetProperty("navigation");
        Assert.Equal(["id", "config", "cells"], Names(navigation));
        Assert.Equal("mm7/place/7/navigation", navigation.GetProperty("id").GetString());

        // No cell is emitted: the engine derives collision navigation itself, so a pack states none rather
        // than deriving a second grid this importer cannot check.
        Assert.Empty(navigation.GetProperty("cells").EnumerateArray());

        JsonElement config = navigation.GetProperty("config");
        Assert.Equal(
            ["schemaVersion", "cellSize", "levelQuantum", "maximumSlopeDegrees", "requiredHeadroom", "supportProbeDrop"],
            Names(config));
        Assert.Equal(1, config.GetProperty("schemaVersion").GetInt32());
        Assert.True(config.GetProperty("cellSize").GetDouble() > 0);
        Assert.True(config.GetProperty("levelQuantum").GetDouble() > 0);
        Assert.InRange(config.GetProperty("maximumSlopeDegrees").GetDouble(), 0, 90);
        Assert.True(config.GetProperty("requiredHeadroom").GetDouble() > 0);
        Assert.True(config.GetProperty("supportProbeDrop").GetDouble() >= 0);

        // The identities the engine checks for whitespace and control bytes, which it refuses.
        foreach (string identity in new[] { root.GetProperty("staticMeshArtifactId").GetString()!, navigation.GetProperty("id").GetString()! })
        {
            Assert.DoesNotContain(identity, character => char.IsWhiteSpace(character) || char.IsControl(character));
        }
    }

    [Fact]
    public void The_same_place_emits_the_same_bytes()
    {
        LodPayload payload = LodFixture.Stored("out01.odm", MapDecoderTests.OutdoorPayload());
        OutdoorMap first = MapDecoder.DecodeOutdoor(payload);
        OutdoorMap second = MapDecoder.DecodeOutdoor(payload);

        PlaceCollision one = PlaceCollisionEmitter.Emit(3, "Out01.odm", first);
        PlaceCollision two = PlaceCollisionEmitter.Emit(3, "Out01.odm", second);

        Assert.True(one.Emitted, one.Refusal?.Detail);
        Assert.Equal(one.NavigationRegion!.Minimum, two.NavigationRegion!.Minimum);
        Assert.Equal(one.NavigationRegion.Maximum, two.NavigationRegion.Maximum);
        Assert.Equal(512d, one.NavigationRegion.CellSize);
        Assert.Equal(one.Artifact, two.Artifact);
        Assert.Equal(one.Vertices, two.Vertices);
        Assert.Equal(one.Triangles, two.Triangles);
    }

    [Fact]
    public void A_region_gets_its_terrain_and_its_models_and_an_interior_gets_its_faces()
    {
        OutdoorMap region = MapDecoder.DecodeOutdoor(LodFixture.Stored("out01.odm", MapDecoderTests.OutdoorPayload()));
        PlaceCollision emitted = PlaceCollisionEmitter.Emit(1, "Out01.odm", region);

        Assert.True(emitted.Emitted, emitted.Refusal?.Detail);
        // The terrain is 127 by 127 squares of two triangles each, whatever the payload holds.
        Assert.Equal(127 * 127, emitted.CountOf(CollisionSource.Terrain).Faces);
        Assert.Equal(127 * 127 * 2, emitted.CountOf(CollisionSource.Terrain).Triangles);

        // The fixture's one model face is marked fluid, and a fluid face is a surface the party stands on
        // (it means "the party is standing on water"), so it is read as solid — and then dropped as
        // carrying no area, because the fixture's own corners lie on one line.
        Assert.Equal(1, emitted.CountOf(CollisionSource.ModelFace).Faces);
        Assert.Equal(0, emitted.CountOf(CollisionSource.ModelFace).Triangles);
        Assert.Equal(1, emitted.DroppedTriangles);

        // The fixture's interior holds one face, and that face's corners lie on one line, so the level has
        // no surface at all and the place is refused rather than emitted with a hole in it.
        IndoorMap interior = MapDecoder.DecodeIndoor(LodFixture.Stored("d01.blv", MapDecoderTests.IndoorPayload()));
        PlaceCollision refused = PlaceCollisionEmitter.Emit(14, "D01.blv", interior);
        Assert.Null(refused.NavigationRegion);
        Assert.False(refused.Emitted);
        Assert.Equal("no-solid-geometry", refused.Refusal?.Code);
        Assert.Equal(1, refused.CountOf(CollisionSource.InteriorFace).Faces);
    }

    [Fact]
    public void A_regions_water_squares_are_the_ones_its_tile_table_flags_as_water()
    {
        // The fixture's first row of squares is the water tileset's base tile but for its last, a shore tile, and the
        // tile table flags the base and not the shore (OpenEnroth src/Engine/Data/TileEnums.h:9, :15).
        TerrainTileTable tiles = TerrainTileTable.Read(SyntheticInstallation.TileTable());
        OutdoorMap region = MapDecoder.DecodeOutdoor(LodFixture.Stored("out01.odm", MapDecoderTests.OutdoorPayload(waterRow: true)));
        bool[] water = tiles.WaterSquares(region);

        Assert.Equal(126, water.Count(wet => wet));
        Assert.All(Enumerable.Range(0, 126), column => Assert.True(water[column]));
        Assert.False(water[126]);
        Assert.False(water[127]);

        // The water squares travel beside the collision as their own surface, two triangles a square, and the
        // collision itself is exactly what it was without them.
        PlaceCollision wet = PlaceCollisionEmitter.Emit(1, "Out01.odm", region, tiles);
        PlaceCollision dry = PlaceCollisionEmitter.Emit(1, "Out01.odm", region);
        Assert.Equal(126, wet.WaterSquares);
        PlaceSurface surface = Assert.Single(wet.Surfaces);
        Assert.Equal(PlaceCollisionEmitter.WaterSurface, surface.Surface);
        Assert.Equal(126 * 2, surface.Mesh.TriangleCount);
        Assert.Equal(dry.Artifact, wet.Artifact);
        Assert.Empty(dry.Surfaces);
        Assert.Empty(wet.Facts.Surfaces);
    }

    [Fact]
    public void A_tile_table_that_is_not_its_count_of_records_is_refused()
    {
        byte[] table = SyntheticInstallation.TileTable();
        Assert.Throws<LodFormatException>(() => TerrainTileTable.Read(table[..^1]));
        Assert.Throws<LodFormatException>(() => TerrainTileTable.Read([1, 0]));
        Assert.Equal(4, TerrainTileTable.Read(table).Count);
    }

    [Fact]
    public void A_solid_face_the_level_marks_fluid_is_ground_and_is_named_fluid_beside_it()
    {
        // FACE_IsFluid, 0x10 (OpenEnroth src/Engine/Graphics/FaceEnums.h:12), on a floor and on a portal.
        CollisionMesh mesh = new();
        CollisionMesh fluid = new();
        CollisionSourceCounts added = PlaceCollisionEmitter.AddSolidFaces(mesh, [Quad(0x10, 0), Quad(0, 0), Quad(0x11, 0)], fluid);

        Assert.Equal(2, added.Faces);
        Assert.Equal(2, fluid.TriangleCount);
        Assert.True(PlaceCollisionEmitter.IsFluid(Quad(0x10, 0)));
        Assert.False(PlaceCollisionEmitter.IsFluid(Quad(0x11, 0)));
    }

    /// <summary>Corners a test states, as the points a polygon is made of.</summary>
    private static List<MapPoint> Points(params (int X, int Y, int Z)[] corners) =>
        [.. corners.Select(corner => new MapPoint(corner.X, corner.Y, corner.Z))];

    /// <summary>A four-cornered floor face with the attributes and back sector a test asks for.</summary>
    private static MapFace Quad(uint attributes, int backSectorId) =>
        Face(attributes, backSectorId, [(0, 0, 0), (512, 0, 0), (512, 512, 0), (0, 512, 0)]);

    /// <summary>A face with corners a test states, wound counter-clockwise as the level's floors are.</summary>
    private static MapFace Face(uint attributes, int backSectorId, IReadOnlyList<(int X, int Y, int Z)> corners)
    {
        List<MapPoint> vertices = [.. corners.Select(corner => new MapPoint(corner.X, corner.Y, corner.Z))];
        return new MapFace(
            Index: 0,
            TextureName: "test",
            Vertices: vertices,
            VertexIds: [.. Enumerable.Range(0, vertices.Count)],
            TextureCoordinates: [.. vertices.Select(_ => new MapTextureCoordinate(0, 0))],
            Plane: new MapPlane(0, 0, 1, 0),
            Attributes: attributes,
            PolygonType: 3,
            TextureDeltaU: 0,
            TextureDeltaV: 0,
            SectorId: 1,
            BackSectorId: backSectorId,
            FaceExtraId: 0);
    }

    /// <summary>One payload as the decoder reads it, without a container around it.</summary>

    /// <summary>An object's property names, in the order the document writes them.</summary>
    private static string[] Names(JsonElement element) => [.. element.EnumerateObject().Select(property => property.Name)];

    /// <summary>An array's numbers.</summary>
    private static double[] Numbers(JsonElement element) => [.. element.EnumerateArray().Select(value => value.GetDouble())];
}
