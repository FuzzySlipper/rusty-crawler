using System.Globalization;
using System.Text;
using MightAndMagic7.Import.Lod;
using MightAndMagic7.Import.Maps;
using MightAndMagic7.Import.Tables;
using Xunit;

namespace MightAndMagic7.Import.Tests;

/// <summary>
/// The map decoders, exercised over payloads this file constructs. No original game data is copied into
/// the repository and none is needed to run these tests.
/// </summary>
/// <remarks>
/// The fixtures are built field by field rather than by copying a real map, because the point of these
/// tests is the walk: which field is consumed when, and what happens when a declared count, a shared
/// pool, or the payload's own length disagrees with the records. The one place a shipped file's shape
/// matters — that an indoor face may carry more corners than the fixed slot array an outdoor face has —
/// is a fixture parameter rather than a recorded expectation.
/// </remarks>
public sealed class MapDecoderTests
{
    [Fact]
    public void An_outdoor_payload_decodes_its_model_faces_and_start_points()
    {
        OutdoorMap map = MapDecoder.DecodeOutdoor(Payload("out01.odm", OutdoorPayload()));

        Assert.Equal(MapKind.Outdoor, map.Kind);
        Assert.Equal("blank", map.Name);
        Assert.Equal("default.odm", map.PayloadFileName);
        Assert.Equal("MM6 Outdoor v7.00", map.Description);
        Assert.Equal("grastyl", map.GroundTileset);
        Assert.Equal(4, map.TileTypes.Count);
        Assert.Equal(1, map.TileTypes[0].Tileset);
        Assert.Equal(90, map.TileTypes[0].TileOffset);
        Assert.Null(map.Delta);
        Assert.Empty(map.Doors);
        Assert.Empty(map.Lights);

        // One height byte at the map's centre cell, which is where the world origin sits.
        Assert.Equal(5, map.HeightMap[(63 * 128) + 64]);
        Assert.Equal(160, map.TerrainHeightAt(64, 63));
        Assert.Equal(new MapPoint(0, 512, 160), map.CellToWorld(64, 63));
        Assert.Equal(-32768, map.TerrainBounds.MinX);
        Assert.Equal(32256, map.TerrainBounds.MaxX);
        Assert.Equal(0, map.TerrainBounds.MinZ);
        Assert.Equal(160, map.TerrainBounds.MaxZ);

        OutdoorModel model = Assert.Single(map.Models);
        Assert.Equal("Tavern_E", model.Name);
        Assert.Equal("Tavern_E2", model.Name2);
        Assert.Equal(new MapPoint(100, 200, 300), model.Position);
        Assert.Equal(new MapBounds(100, 200, 300, 700, 800, 900), model.Bounds);
        Assert.Equal(42, model.BoundingRadius);
        Assert.Equal(3, model.Vertices.Count);
        Assert.Equal(new MapPoint(700, 800, 900), model.Vertices[2]);

        MapFace face = Assert.Single(map.Faces);
        Assert.Equal("Hhp1d", face.TextureName);
        Assert.Equal(new[] { 0, 1, 2 }, face.VertexIds);
        Assert.Equal(
            new[] { new MapPoint(100, 200, 300), new MapPoint(400, 500, 600), new MapPoint(700, 800, 900) },
            face.Vertices);
        Assert.Equal(
            new[] { new MapTextureCoordinate(0, 0), new MapTextureCoordinate(10, 0), new MapTextureCoordinate(10, 10) },
            face.TextureCoordinates);
        Assert.Equal(1f, face.Plane.NormalX);
        Assert.Equal(-100f, face.Plane.Distance);
        Assert.Equal(0x10u, face.Attributes);
        Assert.Equal(4, face.PolygonType);
        Assert.Equal(2, face.TextureDeltaU);
        Assert.Equal(-2, face.TextureDeltaV);
        Assert.Equal(-1, face.SectorId);
        Assert.Equal(-1, face.BackSectorId);
        Assert.Equal(-1, face.FaceExtraId);

        // The third decoration is the lower-case spelling a shipped map uses, and it is matched too.
        Assert.Equal(new[] { "Party Start", "North Start" }, map.EntryPoints.Select(point => point.Name));
        MapEntryPoint start = map.EntryPoints[0];
        Assert.Equal(new MapPoint(12552, 800, 160), start.Position);
        Assert.Equal(512, start.YawAngle);
        Assert.Equal(0, start.DecorationIndex);
        Assert.Equal(1536, map.EntryPoints[1].YawAngle);
        Assert.Equal(2, map.EntryPoints[1].DecorationIndex);
        Assert.Equal(3, map.Decorations.Count);
        Assert.Equal("tree04", map.Decorations[1].Name);
        Assert.Equal(3, map.Decorations[0].DescriptionId);

        MapSpawnPoint spawn = Assert.Single(map.SpawnPoints);
        Assert.Equal(new MapPoint(1, 2, 3), spawn.Position);
        Assert.Equal(4, spawn.Radius);
        Assert.Equal(3, spawn.Type);
        Assert.Equal(5, spawn.TreasureLevelOrMonsterIndex);
        Assert.Equal(6, spawn.Attributes);
        Assert.Equal(7u, spawn.Group);

        Assert.Equal(0, map.Counts.NormalCount);
        Assert.Equal(1, map.Counts.ModelCount);
        Assert.Equal(3, map.Counts.DecorationCount);
        Assert.Equal(0, map.Counts.DecorationPidCount);
        Assert.Equal(1, map.Counts.SpawnPointCount);
        Assert.Equal(1, map.Counts.FaceCount);
        Assert.Equal(3, map.Counts.VertexCount);
    }

    [Fact]
    public void Every_structure_a_report_totals_is_reachable_through_the_base_map()
    {
        DecodedMap map = MapDecoder.DecodeIndoor(Payload("d01.blv", IndoorPayload()), Payload("d01.dlv", IndoorDeltaPayload()));

        Assert.Equal(MapKind.Indoor, map.Kind);
        Assert.Equal(4, map.Vertices.Count);
        Assert.Single(map.Faces);
        Assert.Single(map.Decorations);
        Assert.Single(map.EntryPoints);
        Assert.Single(map.SpawnPoints);
        Assert.Equal(2, map.Doors.Count);
        Assert.Equal(2, map.Lights.Count);
        Assert.NotNull(map.Delta);
    }

    [Fact]
    public void An_outdoor_delta_is_decoded_with_its_map()
    {
        OutdoorMap map = MapDecoder.DecodeOutdoor(Payload("out01.odm", OutdoorPayload()), Payload("out01.ddm", OutdoorDeltaPayload()));

        MapDelta delta = Assert.IsType<MapDelta>(map.Delta);
        Assert.Equal(1, delta.FaceAttributeCount);
        Assert.Equal(3, delta.DecorationFlagCount);
        Assert.Equal(2, delta.ActorCount);
        Assert.Equal(1, delta.SpriteObjectCount);
        Assert.Equal(4, delta.ChestCount);
        Assert.Equal(99L, delta.LastVisitTime);
        Assert.Equal("sky", delta.Weather.SkyTexture);
        Assert.Equal(1, delta.Weather.Flags);
        Assert.Equal(100, delta.Weather.FogDistance1);
        Assert.Equal(200, delta.Weather.FogDistance2);
        Assert.Equal(0, delta.Header.LastRespawnDay);
        Assert.Empty(delta.Doors);
        Assert.Empty(map.Doors);
    }

    [Fact]
    public void An_indoor_payload_decodes_its_faces_from_the_shared_pool_and_its_doors_from_the_delta()
    {
        IndoorMap map = MapDecoder.DecodeIndoor(Payload("d01.blv", IndoorPayload()), Payload("d01.dlv", IndoorDeltaPayload()));

        Assert.Equal(MapKind.Indoor, map.Kind);
        Assert.Equal("No Name Level", map.Name);
        Assert.Equal(1, map.Version);

        // Six arrays of five values for a four-cornered face, plus the sector and light pools.
        Assert.Equal(
            new IndoorCounts(1, 60, 10, 4, 16, 4, 1, 1, 2, 2, 1, 2, 1, 1, 0),
            map.Counts);

        MapFace face = Assert.Single(map.Faces);
        Assert.Equal("Cfb1", face.TextureName);
        Assert.Equal(new[] { 0, 1, 2, 3 }, face.VertexIds);
        Assert.Equal(
            new[] { new MapPoint(0, 0, 0), new MapPoint(1, 2, 3), new MapPoint(2, 4, 6), new MapPoint(3, 6, 9) },
            face.Vertices);
        Assert.Equal(
            new[]
            {
                new MapTextureCoordinate(0, 0),
                new MapTextureCoordinate(10, 0),
                new MapTextureCoordinate(20, 10),
                new MapTextureCoordinate(30, 10),
            },
            face.TextureCoordinates);
        Assert.Equal(0f, face.Plane.NormalX);
        Assert.Equal(0f, face.Plane.NormalY);
        Assert.Equal(-1f, face.Plane.NormalZ);
        Assert.Equal(-1f, face.Plane.Distance);
        Assert.Equal(0x08u, face.Attributes);
        Assert.Equal(3, face.PolygonType);
        Assert.Equal(4, face.TextureDeltaU);
        Assert.Equal(-4, face.TextureDeltaV);
        Assert.Equal(1, face.SectorId);
        Assert.Equal(-1, face.BackSectorId);
        Assert.Equal(0, face.FaceExtraId);

        MapFaceExtra extra = Assert.Single(map.FaceExtras);
        Assert.Equal(7, extra.FaceId);
        Assert.Equal(0xFFFF, extra.AdditionalBitmapId);
        Assert.Equal(9, extra.CogNumber);
        Assert.Equal(11, extra.EventId);

        Assert.Equal(2, map.Sectors.Count);
        MapSector empty = map.Sectors[0];
        Assert.Equal(0, empty.Flags);
        Assert.Equal(0, empty.WaterLevel);
        Assert.Empty(empty.FaceIds);
        Assert.Empty(empty.LightIds);

        MapSector sector = map.Sectors[1];
        Assert.Equal(0x18, sector.Flags);
        Assert.Equal(-30000, sector.WaterLevel);
        Assert.Equal(5, sector.MinAmbientLightLevel);
        Assert.Equal(new[] { 0 }, sector.FloorIds);
        Assert.Equal(new[] { 0 }, sector.WallIds);
        Assert.Equal(new[] { 0 }, sector.CeilingIds);
        Assert.Empty(sector.PortalIds);
        Assert.Equal(new[] { 0 }, sector.FaceIds);
        Assert.Equal(new[] { 0 }, sector.NonBspFaceIds);
        Assert.Equal(new[] { 0 }, sector.DecorationIds);
        Assert.Equal(new[] { 0, 1 }, sector.LightIds);
        Assert.Empty(sector.FluidIds);
        Assert.Empty(sector.CogIds);
        Assert.Empty(sector.MarkerIds);
        Assert.Equal(new MapBounds(-10, -20, -30, 10, 20, 30), sector.Bounds);

        Assert.Equal(2, map.Lights.Count);
        MapLight light = map.Lights[0];
        Assert.Equal(new MapPoint(10, 20, 30), light.Position);
        Assert.Equal(40, light.Radius);
        Assert.Equal(1, light.Red);
        Assert.Equal(2, light.Green);
        Assert.Equal(3, light.Blue);
        Assert.Equal(4, light.Type);
        Assert.Equal(5, light.Attributes);
        Assert.Equal(6, light.Brightness);

        MapBspNode node = Assert.Single(map.BspNodes);
        Assert.Equal((short)0, node.Front);
        Assert.Equal((short)-1, node.Back);
        Assert.Equal((short)0, node.FaceIdOffset);
        Assert.Equal((short)1, node.FaceCount);

        MapSpawnPoint spawn = Assert.Single(map.SpawnPoints);
        Assert.Equal(new MapPoint(1, 2, 3), spawn.Position);
        Assert.Equal(7u, spawn.Group);

        Assert.Equal(new MapPoint(1536, -8448, 128), Assert.Single(map.EntryPoints).Position);
        Assert.Equal(new MapBounds(0, 0, 0, 3, 6, 9), map.Bounds);

        MapDelta delta = Assert.IsType<MapDelta>(map.Delta);
        Assert.Equal(1, delta.FaceAttributeCount);
        Assert.Equal(1, delta.DecorationFlagCount);
        Assert.Equal(0, delta.ActorCount);
        Assert.Equal("sky", delta.Weather.SkyTexture);

        Assert.Equal(2, map.Doors.Count);
        MapDoor door = map.Doors[0];
        Assert.True(door.InUse);
        Assert.Equal(2, door.State);
        Assert.Equal(1u, door.Attributes);
        Assert.Equal(77u, door.DoorId);
        Assert.Equal(5u, door.TimeSinceTriggered);
        Assert.Equal(new MapPoint(0, 0, 64), door.Direction);
        Assert.Equal(8u, door.MoveLength);
        Assert.Equal(2u, door.OpenSpeed);
        Assert.Equal(3u, door.CloseSpeed);
        Assert.Equal(new[] { 0, 1 }, door.VertexIds);
        Assert.Equal(new[] { 0 }, door.FaceIds);
        Assert.Empty(door.SectorIds);
        Assert.Equal(new[] { 7 }, door.DeltaUs);
        Assert.Equal(new[] { -7 }, door.DeltaVs);
        Assert.Equal(new[] { 0 }, door.XOffsets);
        Assert.Equal(new[] { 0 }, door.YOffsets);
        Assert.Equal(new[] { -64 }, door.ZOffsets);
        Assert.False(map.Doors[1].InUse);
        Assert.Empty(map.Doors[1].FaceIds);
    }

    [Fact]
    public void An_indoor_face_may_carry_more_corners_than_an_outdoor_face_has_slots_for()
    {
        // The outdoor face stores its corners in a fixed 20-slot array; the indoor pool has no such
        // limit, and shipped interiors contain faces of up to 42 corners.
        const int corners = 24;
        IndoorMap map = MapDecoder.DecodeIndoor(Payload("d24.blv", IndoorPayload(corners)));

        MapFace face = Assert.Single(map.Faces);
        Assert.Equal(corners, face.Vertices.Count);
        Assert.Equal(corners, face.TextureCoordinates.Count);
        Assert.Equal(corners, face.VertexIds.Count);
        Assert.Equal(new MapPoint(23, 46, 69), face.Vertices[23]);
        Assert.Empty(map.Doors);
        Assert.Null(map.Delta);
    }

    [Fact]
    public void A_payload_whose_walk_does_not_end_exactly_is_a_failure_naming_the_bytes_left()
    {
        byte[] withTrailer = [.. OutdoorPayload(), 0x00];
        LodFormatException error = Assert.Throws<LodFormatException>(() => MapDecoder.DecodeOutdoor(Payload("out01.odm", withTrailer)));

        Assert.Contains("out01.odm", error.Message);
        Assert.Contains("1 of", error.Message);
        Assert.Contains("unconsumed", error.Message);
    }

    [Fact]
    public void A_payload_that_ends_before_a_field_is_a_failure_naming_the_field_and_offset()
    {
        byte[] truncated = IndoorPayload()[..40];
        LodFormatException error = Assert.Throws<LodFormatException>(() => MapDecoder.DecodeIndoor(Payload("d01.blv", truncated)));

        Assert.Contains("d01.blv", error.Message);
        Assert.Contains("'name'", error.Message);
        Assert.Contains("0x4", error.Message);
    }

    [Fact]
    public void A_pool_that_does_not_match_its_records_fails_instead_of_drifting()
    {
        // Declaring one extra value makes the face data pool longer than the faces consume, which is
        // what a file whose arrays were reordered without their sizes being updated would look like.
        byte[] payload = IndoorPayload(poolSlackValues: 1);
        LodFormatException error = Assert.Throws<LodFormatException>(() => MapDecoder.DecodeIndoor(Payload("d01.blv", payload)));

        Assert.Contains("faceData pool", error.Message);
        Assert.Contains("holds 31 values", error.Message);
        Assert.Contains("consumed 30", error.Message);
    }

    [Fact]
    public void An_indoor_payload_that_declares_another_layout_version_is_refused()
    {
        // Another game in the family stores different record widths in these same fields, so the
        // version has to be checked rather than walked past.
        LodFormatException error = Assert.Throws<LodFormatException>(() =>
            MapDecoder.DecodeIndoor(Payload("d01.blv", IndoorPayload(version: 2))));

        Assert.Contains("'version'", error.Message);
        Assert.Contains("0x0", error.Message);
    }

    [Fact]
    public void A_delta_that_belongs_to_another_map_is_refused()
    {
        LodFormatException error = Assert.Throws<LodFormatException>(() => MapDecoder.DecodeIndoor(
            Payload("d01.blv", IndoorPayload()),
            Payload("d02.dlv", IndoorDeltaPayload())));

        Assert.Contains("d02.dlv", error.Message);
        Assert.Contains("d01.blv", error.Message);
    }

    [Fact]
    public void Every_map_the_per_map_table_names_is_decoded_and_totalled()
    {
        string[] files = [.. Enumerable.Repeat("d01.blv", MapStatsTable.ExpectedMaps)];
        string root = MapInstallation(files);
        try
        {
            MapDecodeReport report = MapDecoder.DecodeAll(LodInstall.Open(root));

            Assert.Equal(MapStatsTable.ExpectedMaps, report.MapCount);
            Assert.Equal(MapStatsTable.ExpectedMaps, report.DecodedCount);
            Assert.Empty(report.Failures);
            Assert.Equal(MapStatsTable.ExpectedMaps, report.Total.Maps);
            Assert.Equal(MapStatsTable.ExpectedMaps, report.Indoor.Maps);
            Assert.Equal(0, report.Outdoor.Maps);
            Assert.Equal(MapStatsTable.ExpectedMaps, report.Total.Faces);
            Assert.Equal(MapStatsTable.ExpectedMaps * 4, report.Total.Vertices);
            Assert.Equal(MapStatsTable.ExpectedMaps * 2, report.Total.Doors);
            Assert.Equal(MapStatsTable.ExpectedMaps * 2, report.Total.Lights);
            Assert.Equal(MapStatsTable.ExpectedMaps, report.Total.EntryPoints);
            Assert.Equal(MapStatsTable.ExpectedMaps, report.Total.Decorations);
            Assert.Equal(MapStatsTable.ExpectedMaps, report.Total.SpawnPoints);
            Assert.Equal(1, report.Outcomes[0].Map.Id);
            Assert.Equal("Map 1", report.Outcomes[0].Map.Name);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void A_map_that_fails_is_recorded_with_its_id_file_name_and_field()
    {
        // The first row names a map whose payload ends inside its face array; the rest name outdoor
        // maps that decode, so both an outcome and a failure have to reach the report.
        string[] files = ["d01.blv", .. Enumerable.Repeat("out01.odm", MapStatsTable.ExpectedMaps - 1)];
        string root = MapInstallation(files, truncateIndoor: true);
        try
        {
            MapDecodeReport report = MapDecoder.DecodeAll(LodInstall.Open(root));

            Assert.Equal(MapStatsTable.ExpectedMaps, report.MapCount);
            Assert.Equal(MapStatsTable.ExpectedMaps - 1, report.DecodedCount);
            MapDecodeFailure failure = Assert.Single(report.Failures);
            Assert.Equal(1, failure.MapId);
            Assert.Equal("d01.blv", failure.FileName);
            Assert.Contains("d01.blv", failure.Reason);
            Assert.Contains("'faceCount'", failure.Reason);
            Assert.Contains("0x", failure.Reason);
            Assert.Equal(MapStatsTable.ExpectedMaps - 1, report.Outdoor.Maps);
            Assert.Equal((MapStatsTable.ExpectedMaps - 1) * 2, report.Total.EntryPoints);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void A_map_the_container_does_not_hold_is_a_recorded_failure()
    {
        string[] files = [.. Enumerable.Repeat("out01.odm", MapStatsTable.ExpectedMaps)];
        string root = MapInstallation(files);
        try
        {
            // Replace the asset container with one that has no map in it at all: every row must come
            // back as a failure naming its own file rather than as a shorter, silently complete list.
            File.WriteAllBytes(
                Path.Combine(root, "DATA", "Games.lod"),
                LodFixture.Archive("GameMMVI", ("filler.txt", LodFixture.CompressedStored([1, 2, 3]))));

            MapDecodeReport report = MapDecoder.DecodeAll(LodInstall.Open(root));

            Assert.Equal(MapStatsTable.ExpectedMaps, report.MapCount);
            Assert.Equal(0, report.DecodedCount);
            Assert.Equal(MapStatsTable.ExpectedMaps, report.FailureCount);
            Assert.Equal("out01.odm", report.Failures[0].FileName);
            Assert.Equal(1, report.Failures[0].MapId);
            Assert.Contains("out01.odm", report.Failures[0].Reason);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>A payload that came from an entry of the given name.</summary>
    private static LodPayload Payload(string entryName, byte[] bytes) =>
        new(new LodEntry(entryName, 0, bytes.Length), bytes, LodPayloadKind.Verbatim);

    /// <summary>
    /// An outdoor payload with one model of one three-cornered face, three decorations, and one spawn
    /// point, laid out in the order the format stores them.
    /// </summary>
    internal static byte[] OutdoorPayload()
    {
        const int terrainCells = 128 * 128;
        MapWriter writer = new();
        writer.Text("blank", 32).Text("default.odm", 32).Text("MM6 Outdoor v7.00", 32).Text(string.Empty, 32).Text("grastyl", 32);
        writer.U16(1).U16(90).U16(2).U16(126).U16(3).U16(162).U16(4).U16(198);

        byte[] heights = new byte[terrainCells];
        heights[(63 * 128) + 64] = 5;
        writer.Raw(heights);
        writer.Zero(terrainCells);              // tile map
        writer.Zero(terrainCells);              // attribute map
        writer.U32(0);                          // normal count
        writer.Zero(terrainCells * 2 * 4);      // per-cell normal distances
        writer.Zero(terrainCells * 2 * 2);      // per-cell normal indices

        writer.U32(1);                          // model count
        int header = writer.Length;
        writer.Zero(188);
        writer.SetText(header, "Tavern_E", 32);
        writer.SetText(header + 0x20, "Tavern_E2", 32);
        writer.SetI32(header + 0x44, 3);        // vertices
        writer.SetI32(header + 0x4C, 1);        // faces
        writer.SetI32(header + 0x5C, 0);        // nodes
        writer.SetI32(header + 0x70, 100).SetI32(header + 0x74, 200).SetI32(header + 0x78, 300);
        writer.SetBounds(header + 0x7C, 100, 200, 300, 700, 800, 900);
        writer.SetBounds(header + 0x94, 0, 0, 0, 0, 0, 0);
        writer.SetI32(header + 0xAC, 400).SetI32(header + 0xB0, 500).SetI32(header + 0xB4, 600);
        writer.SetI32(header + 0xB8, 42);

        writer.I32(100).I32(200).I32(300);
        writer.I32(400).I32(500).I32(600);
        writer.I32(700).I32(800).I32(900);

        int face = writer.Length;
        writer.Zero(308);
        writer.SetI32(face + 0x00, 65536);      // plane normal X, 16.16 fixed point
        writer.SetI32(face + 0x0C, -100 * 65536);
        writer.SetU32(face + 0x1C, 0x10);
        writer.SetI16Array(face + 0x20, FaceSlots([0, 1, 2]));
        writer.SetI16Array(face + 0x48, FaceSlots([0, 10, 10]));
        writer.SetI16Array(face + 0x70, FaceSlots([0, 0, 10]));
        writer.SetI16(face + 0x112, 2);
        writer.SetI16(face + 0x114, -2);
        writer.SetU8(face + 0x12E, 3);          // corners
        writer.SetU8(face + 0x12F, 4);          // polygon type

        writer.Zero(2 * 1);                     // face ordering, one word per face
        writer.Text("Hhp1d", 10);

        writer.U32(3);                          // decoration count
        int decoration = writer.Length;
        writer.Zero(3 * 32);
        writer.SetU16(decoration + 0x00, 3);
        writer.SetU16(decoration + 0x02, 1);
        writer.SetI32(decoration + 0x04, 12552).SetI32(decoration + 0x08, 800).SetI32(decoration + 0x0C, 160);
        writer.SetI32(decoration + 0x10, 512);
        writer.SetI32(decoration + 0x04 + 64, 10).SetI32(decoration + 0x08 + 64, 20).SetI32(decoration + 0x0C + 64, 30);
        writer.SetI32(decoration + 0x10 + 64, 1536);
        writer.Text("Party Start", 32).Text("tree04", 32).Text("north start", 32);

        writer.U32(0);                          // decoration object references
        writer.Zero(128 * 128 * 4);             // decoration map
        writer.U32(1);                          // spawn point count
        writer.I32(1).I32(2).I32(3).U16(4).U16(3).U16(5).U16(6).U32(7);
        return writer.ToArray();
    }

    /// <summary>An outdoor delta with the counts the outdoor fixture's geometry implies.</summary>
    internal static byte[] OutdoorDeltaPayload()
    {
        MapWriter writer = new();
        writer.Zero(40);                        // header, zero throughout the shipped deltas
        writer.Zero(88 * 11);                   // fully revealed cells
        writer.Zero(88 * 11);                   // partially revealed cells
        writer.U32(0);                          // face attributes, one per map face
        writer.U16(0).U16(0).U16(0);            // decoration flags, one per decoration
        writer.U32(2);                          // actors
        writer.Zero(2 * 0x344);
        writer.U32(1);                          // sprite objects
        writer.Zero(0x70);
        writer.U32(4);                          // chests
        writer.Zero(4 * 5324);
        writer.Zero(200);                       // event variables
        writer.I64(99);                         // last visit time
        writer.Text("sky", 12).U32(1).I32(100).I32(200).Zero(24);
        return writer.ToArray();
    }

    /// <summary>
    /// An indoor payload with one face, one face extra, one sector, two lights, one BSP node, one
    /// decoration and one spawn point, plus the shared pools that hold what the records do not.
    /// </summary>
    /// <param name="faceCorners">How many corners the face has; the shared pool grows with it.</param>
    /// <param name="poolSlackValues">Extra values to declare in the face data pool, to break its walk.</param>
    /// <param name="version">The layout version the payload declares.</param>
    internal static byte[] IndoorPayload(int faceCorners = 4, int poolSlackValues = 0, int version = 1)
    {
        MapWriter writer = new();
        writer.U32((uint)version);
        writer.Text("No Name Level", 100);
        int sizes = writer.Length;
        writer.U32(0).U32(0).U32(0).U32(0);
        writer.Zero(16);

        writer.U32((uint)faceCorners);
        for (int index = 0; index < faceCorners; index++)
        {
            writer.I16((short)index).I16((short)(index * 2)).I16((short)(index * 3));
        }

        writer.U32(1);                          // face count
        int face = writer.Length;
        writer.Zero(96);
        writer.SetI32(face + 0x00, 0).SetI32(face + 0x04, 0).SetI32(face + 0x08, -65536).SetI32(face + 0x0C, -65536);
        writer.SetI32(face + 0x10, 0).SetI32(face + 0x14, 0).SetI32(face + 0x18, -65536).SetI32(face + 0x1C, -65536);
        writer.SetU32(face + 0x2C, 0x08);
        writer.SetU16(face + 0x48, 0);          // face extra id
        writer.SetU16(face + 0x4A, 0xFFFF);     // bitmap id, dead in the shipped data
        writer.SetU16(face + 0x4C, 1);          // sector id
        writer.SetI16(face + 0x4E, 0);          // back sector id, zero for a non-portal
        writer.SetU8(face + 0x5C, 3);           // polygon type, floor
        writer.SetU8(face + 0x5D, (byte)faceCorners);

        // The shared face data pool: six arrays per face, each with a closing slot, in face order.
        for (int slot = 0; slot < faceCorners; slot++) writer.I16((short)slot);
        writer.I16(0);                                                  // closing slot repeats vertex 0
        for (int array = 0; array < 3; array++)
        {
            for (int slot = 0; slot <= faceCorners; slot++) writer.I16(0); // intersection offsets
        }

        for (int slot = 0; slot < faceCorners; slot++) writer.I16((short)(slot * 10));  // texture U
        writer.I16(0);
        for (int slot = 0; slot < faceCorners; slot++) writer.I16((short)(slot < 2 ? 0 : 10)); // texture V
        writer.I16(0);
        for (int slack = 0; slack < poolSlackValues; slack++) writer.I16(0);

        writer.Text("Cfb1", 10);

        writer.U32(1);                          // face extra count
        int extra = writer.Length;
        writer.Zero(36);
        writer.SetI16(extra + 0x0C, 7);
        writer.SetU16(extra + 0x0E, 0xFFFF);
        writer.SetI16(extra + 0x14, 4);
        writer.SetI16(extra + 0x16, -4);
        writer.SetU16(extra + 0x18, 9);
        writer.SetU16(extra + 0x1A, 11);
        writer.Text(string.Empty, 10);          // face extra textures

        // Two sectors: sector 0 is the level's "no sector" pseudo-sector and owns nothing, which is
        // why the face's own sector id is 1.
        writer.U32(2);                          // sector count
        writer.Zero(116);
        int sector = writer.Length;
        writer.Zero(116);
        writer.SetI32(sector + 0x00, 0x18);
        writer.SetU16(sector + 0x04, 1);        // floors
        writer.SetU16(sector + 0x0C, 1);        // walls
        writer.SetU16(sector + 0x14, 1);        // ceilings
        writer.SetU16(sector + 0x2C, 1);        // faces
        writer.SetU16(sector + 0x2E, 1);        // non-BSP faces
        writer.SetU16(sector + 0x44, 1);        // decorations
        writer.SetU16(sector + 0x54, 2);        // lights
        writer.SetI16(sector + 0x5C, -30000);   // water level
        writer.SetI16(sector + 0x62, 5);        // minimum ambient light
        writer.SetShortBounds(sector + 0x68, -10, -20, -30, 10, 20, 30);

        // The sector data pool holds the sector's lists in sector order, then the light pool its lights.
        writer.I16(0).I16(0).I16(0).I16(0).I16(0);
        writer.I16(0).I16(1);

        writer.U32(2);                          // door capacity; the records themselves are in the delta
        writer.U32(1);                          // decoration count
        int decoration = writer.Length;
        writer.Zero(32);
        writer.SetU16(decoration + 0x00, 3);
        writer.SetI32(decoration + 0x04, 1536).SetI32(decoration + 0x08, -8448).SetI32(decoration + 0x0C, 128);
        writer.Text("Party Start", 32);

        writer.U32(2);                          // light count
        int light = writer.Length;
        writer.Zero(16);
        writer.SetI16(light + 0x00, 10).SetI16(light + 0x02, 20).SetI16(light + 0x04, 30);
        writer.SetI16(light + 0x06, 40);
        writer.SetU8(light + 0x08, 1).SetU8(light + 0x09, 2).SetU8(light + 0x0A, 3).SetU8(light + 0x0B, 4);
        writer.SetI16(light + 0x0C, 5).SetI16(light + 0x0E, 6);
        writer.Zero(16);

        writer.U32(1);                          // BSP node count
        writer.I16(0).I16(-1).I16(0).I16(1);
        writer.U32(1);                          // spawn point count
        writer.I32(1).I32(2).I32(3).U16(4).U16(3).U16(5).U16(6).U32(7);
        writer.U32(0);                          // map outline count

        writer.SetU32(sizes, (uint)(((6 * (faceCorners + 1)) + poolSlackValues) * sizeof(short)));
        writer.SetU32(sizes + 4, 5 * sizeof(short));
        writer.SetU32(sizes + 8, 2 * sizeof(short));
        writer.SetU32(sizes + 12, 8 * sizeof(short));
        return writer.ToArray();
    }

    /// <summary>An indoor delta holding two door slots, the first of them in use.</summary>
    internal static byte[] IndoorDeltaPayload()
    {
        MapWriter writer = new();
        writer.Zero(40);                        // header
        writer.Zero(875);                       // visible outlines
        writer.U32(0);                          // face attributes, one per level face
        writer.U16(0);                          // decoration flags, one per decoration
        writer.U32(0);                          // actors
        writer.U32(0);                          // sprite objects
        writer.U32(0);                          // chests

        int door = writer.Length;
        writer.Zero(80);
        writer.SetU32(door + 0x00, 1);
        writer.SetU32(door + 0x04, 77);
        writer.SetU32(door + 0x08, 5);
        writer.SetI32(door + 0x0C, 0).SetI32(door + 0x10, 0).SetI32(door + 0x14, 64);
        writer.SetU32(door + 0x18, 8).SetU32(door + 0x1C, 2).SetU32(door + 0x20, 3);
        writer.SetU16(door + 0x44, 2);          // vertices
        writer.SetU16(door + 0x46, 1);          // faces
        writer.SetU16(door + 0x4A, 1);          // offsets
        writer.SetU16(door + 0x4C, 2);          // state
        writer.Zero(80);                        // the second slot is unused

        // The door pool: vertices, faces, sectors, texture deltas, then the three offset arrays.
        writer.I16(0).I16(1).I16(0).I16(7).I16(-7).I16(0).I16(0).I16(-64);

        writer.Zero(200);                       // event variables
        writer.I64(0);                          // last visit time
        writer.Text("sky", 12).U32(1).I32(100).I32(200).Zero(24);
        return writer.ToArray();
    }

    /// <summary>
    /// Builds an installation whose per-map table names the given maps and holds their payloads.
    /// </summary>
    /// <remarks>The writer suite reuses this so both suites decode the same constructed maps.</remarks>
    /// <param name="mapFiles">One file name per map row; the table has to end up with 76 rows.</param>
    /// <param name="truncateIndoor">Whether to end the indoor payload inside its face array.</param>
    internal static string MapInstallation(IReadOnlyList<string> mapFiles, bool truncateIndoor = false)
    {
        byte[] indoor = IndoorPayload();
        List<(string Name, byte[] Payload)> maps = [];
        if (mapFiles.Contains("d01.blv", StringComparer.OrdinalIgnoreCase))
        {
            maps.Add(("d01.blv", LodFixture.CompressedStored(truncateIndoor ? indoor[..200] : indoor)));
            maps.Add(("d01.dlv", LodFixture.CompressedStored(IndoorDeltaPayload())));
        }

        if (mapFiles.Contains("out01.odm", StringComparer.OrdinalIgnoreCase))
        {
            maps.Add(("out01.odm", LodFixture.CompressedStored(OutdoorPayload())));
            maps.Add(("out01.ddm", LodFixture.CompressedStored(OutdoorDeltaPayload())));
        }

        string root = Path.Combine(Path.GetTempPath(), $"mm7-map-fixture-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "DATA"));
        File.WriteAllBytes(Path.Combine(root, "DATA", "Games.lod"), LodFixture.Archive("GameMMVI", [.. maps]));
        File.WriteAllBytes(
            Path.Combine(root, "DATA", "Events.lod"),
            LodFixture.Archive("MMVI", LodFixture.TextTable("MapStats.txt", MapStatsText(mapFiles))));
        return root;
    }

    /// <summary>Builds a per-map table with the given file name in every row.</summary>
    internal static string MapStatsText(IReadOnlyList<string> mapFiles)
    {
        List<string> lines =
        [
            "#\tName\tFile\tunused\tunused\tunused\tRefil Days\tAlert Days\tunused\tunused\tunused\tTres 0-6\tEnc %\tunused\tunused\tunused\tunused\tMusic\tunused\tunused\tunused\tMonster1\tunused\tunused\tunused\tMonster2\tunused\tunused\tEnvironment\tDesigner\tNotes\tNotes",
            "legend row",
            "another legend row",
        ];
        for (int index = 0; index < mapFiles.Count; index++)
        {
            string[] fields = new string[32];
            Array.Fill(fields, "0");
            fields[0] = (index + 1).ToString(CultureInfo.InvariantCulture);
            fields[1] = $"Map {index + 1}";
            fields[2] = mapFiles[index];
            fields[17] = "music";
            fields[28] = "designer";
            lines.Add(string.Join('\t', fields));
        }

        return string.Join('\n', lines) + "\n";
    }

    /// <summary>Pads a face's vertex list to the fixed 20 slots an outdoor face stores.</summary>
    private static int[] FaceSlots(int[] values)
    {
        int[] slots = new int[20];
        values.CopyTo(slots, 0);
        slots[values.Length] = values[0];
        return slots;
    }

    /// <summary>A byte buffer that appends little-endian fields and can patch them at documented offsets.</summary>
    private sealed class MapWriter
    {
        private readonly List<byte> _bytes = [];

        internal int Length => _bytes.Count;

        internal MapWriter U8(int value) => Field(1, value);

        internal MapWriter I16(short value) => Field(2, value);

        internal MapWriter U16(int value) => Field(2, value);

        internal MapWriter U32(uint value) => Field(4, value);

        internal MapWriter I32(int value) => Field(4, value);

        internal MapWriter I64(long value) => Field(8, value);

        internal MapWriter Zero(int count) => Raw(new byte[count]);

        internal MapWriter Raw(byte[] bytes)
        {
            _bytes.AddRange(bytes);
            return this;
        }

        /// <summary>Writes a NUL-padded character field.</summary>
        internal MapWriter Text(string value, int width)
        {
            byte[] raw = new byte[width];
            Encoding.ASCII.GetBytes(value).AsSpan(0, Math.Min(value.Length, width - 1)).CopyTo(raw);
            return Raw(raw);
        }

        internal MapWriter SetU8(int offset, int value) => Patch(offset, 1, value);

        internal MapWriter SetI16(int offset, short value) => Patch(offset, 2, value);

        internal MapWriter SetU16(int offset, int value) => Patch(offset, 2, value);

        internal MapWriter SetU32(int offset, uint value) => Patch(offset, 4, value);

        internal MapWriter SetI32(int offset, int value) => Patch(offset, 4, value);

        internal MapWriter SetI16Array(int offset, IReadOnlyList<int> values)
        {
            for (int index = 0; index < values.Count; index++) SetI16(offset + (index * sizeof(short)), (short)values[index]);
            return this;
        }

        internal MapWriter SetText(int offset, string value, int width)
        {
            byte[] raw = new byte[width];
            Encoding.ASCII.GetBytes(value).AsSpan(0, Math.Min(value.Length, width - 1)).CopyTo(raw);
            for (int index = 0; index < width; index++) _bytes[offset + index] = raw[index];
            return this;
        }

        /// <summary>Writes a box stored as three minima followed by three maxima.</summary>
        internal MapWriter SetBounds(int offset, int minX, int minY, int minZ, int maxX, int maxY, int maxZ) =>
            SetI32(offset, minX).SetI32(offset + 4, minY).SetI32(offset + 8, minZ)
                .SetI32(offset + 12, maxX).SetI32(offset + 16, maxY).SetI32(offset + 20, maxZ);

        /// <summary>Writes a box stored as three 16-bit pairs, each minimum before its maximum.</summary>
        internal MapWriter SetShortBounds(int offset, int minX, int minY, int minZ, int maxX, int maxY, int maxZ) =>
            SetI16(offset, (short)minX).SetI16(offset + 2, (short)maxX)
                .SetI16(offset + 4, (short)minY).SetI16(offset + 6, (short)maxY)
                .SetI16(offset + 8, (short)minZ).SetI16(offset + 10, (short)maxZ);

        internal byte[] ToArray() => [.. _bytes];

        private MapWriter Field(int size, long value) => Patch(_bytes.Count, size, value, append: true);

        private MapWriter Patch(int offset, int size, long value, bool append = false)
        {
            for (int index = 0; index < size; index++)
            {
                byte octet = (byte)(value >> (index * 8));
                if (append) _bytes.Add(octet);
                else _bytes[offset + index] = octet;
            }

            return this;
        }
    }
}
