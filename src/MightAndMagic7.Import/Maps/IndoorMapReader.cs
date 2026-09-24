using MightAndMagic7.Import.Lod;

namespace MightAndMagic7.Import.Maps;

/// <summary>Reads an indoor payload in the order its layout stores the fields.</summary>
/// <remarks>
/// Most of an interior is not in its records: every face's vertex ids and texture coordinates, and
/// every sector's face, portal, decoration and light lists, are concatenated into shared pools whose
/// only structure is the counts in the records that come before them. Those pools cannot be skipped,
/// and the walk checks that each one is consumed exactly, because an over- or under-read silently
/// reassigns every later list.
/// </remarks>
internal static class IndoorMapReader
{
    /// <summary>The only interior layout version this decoder reads.</summary>
    private const int Version = 1;

    private const int NameWidth = 100;
    private const int ReservedSize = 16;
    private const int PoolValueSize = sizeof(short);
    private const int VertexSize = 6;
    private const int FaceSize = 96;
    private const int FaceTextureNameWidth = 10;
    private const int FaceExtraSize = 36;
    private const int SectorSize = 116;
    private const int LightSize = 16;
    private const int BspNodeSize = 8;
    private const int MapOutlineSize = 12;

    /// <summary>The sector id a face stores when it belongs to no sector.</summary>
    private const int NoSector = 0xFFFF;

    // Field offsets inside BLVFace_MM7.
    private const int FacePlaneFixedOffset = 0x10;
    private const int FaceAttributesOffset = 0x2C;
    private const int FaceExtraIdOffset = 0x48;
    private const int FaceSectorIdOffset = 0x4C;
    private const int FaceBackSectorIdOffset = 0x4E;
    private const int FacePolygonTypeOffset = 0x5C;
    private const int FaceVertexCountOffset = 0x5D;

    // Field offsets inside BLVFaceExtra_MM7.
    private const int ExtraFaceIdOffset = 0x0C;
    private const int ExtraAdditionalBitmapIdOffset = 0x0E;
    private const int ExtraTextureDeltaUOffset = 0x14;
    private const int ExtraTextureDeltaVOffset = 0x16;
    private const int ExtraCogNumberOffset = 0x18;
    private const int ExtraEventIdOffset = 0x1A;

    // Count offsets inside BLVSector_MM7, in the order the sector data pool stores the lists.
    private const int SectorFlagsOffset = 0x00;
    private const int SectorFloorCountOffset = 0x04;
    private const int SectorWallCountOffset = 0x0C;
    private const int SectorCeilingCountOffset = 0x14;
    private const int SectorFluidCountOffset = 0x1C;
    private const int SectorPortalCountOffset = 0x24;
    private const int SectorFaceCountOffset = 0x2C;
    private const int SectorNonBspFaceCountOffset = 0x2E;
    private const int SectorCylinderFaceCountOffset = 0x34;
    private const int SectorCogCountOffset = 0x3C;
    private const int SectorDecorationCountOffset = 0x44;
    private const int SectorMarkerCountOffset = 0x4C;
    private const int SectorLightCountOffset = 0x54;
    private const int SectorWaterLevelOffset = 0x5C;
    private const int SectorMistLevelOffset = 0x5E;
    private const int SectorLightDistanceMultiplierOffset = 0x60;
    private const int SectorMinAmbientLightLevelOffset = 0x62;
    private const int SectorFirstBspNodeOffset = 0x64;
    private const int SectorExitTagOffset = 0x66;
    private const int SectorBoundsOffset = 0x68;

    // Field offsets inside BLVLight_MM7.
    private const int LightRadiusOffset = 0x06;
    private const int LightRedOffset = 0x08;
    private const int LightGreenOffset = 0x09;
    private const int LightBlueOffset = 0x0A;
    private const int LightTypeOffset = 0x0B;
    private const int LightAttributesOffset = 0x0C;
    private const int LightBrightnessOffset = 0x0E;

    internal static IndoorMap Read(LodPayload payload, LodPayload? deltaPayload)
    {
        MapPayloadReader reader = new(payload);
        // The payload's first word identifies the interior layout rather than merely decorating it:
        // the other games in the family store different record widths in the same fields, so a
        // payload that does not declare this game's version is refused instead of misread.
        int version = reader.ExpectInt32(Version, "version");
        string name = reader.Text(NameWidth, "name");
        int faceDataSizeBytes = reader.Int32("faceDataSizeBytes");
        int sectorDataSizeBytes = reader.Int32("sectorDataSizeBytes");
        int sectorLightDataSizeBytes = reader.Int32("sectorLightDataSizeBytes");
        int doorsDataSizeBytes = reader.Int32("doorsDataSizeBytes");
        reader.Skip(ReservedSize, "reserved");

        int vertexCount = reader.ArrayCount(VertexSize, "vertexCount");
        ReadOnlySpan<byte> vertexData = reader.Span(vertexCount * VertexSize, "vertices");
        int faceCount = reader.ArrayCount(FaceSize, "faceCount");
        ReadOnlySpan<byte> faceRecords = reader.Span(faceCount * FaceSize, "faces");

        int facePoolCount = reader.Elements(faceDataSizeBytes, PoolValueSize, "faceDataSizeBytes");
        int facePoolOffset = reader.Position;
        MapInt16Pool facePool = new(reader.Int16Values(facePoolCount, "faceData"), reader.Source, "faceData", facePoolOffset);

        string[] faceTextures = new string[faceCount];
        for (int index = 0; index < faceCount; index++)
        {
            faceTextures[index] = reader.Text(FaceTextureNameWidth, $"faceTextures[{index}]");
        }

        int faceExtraCount = reader.ArrayCount(FaceExtraSize, "faceExtraCount");
        ReadOnlySpan<byte> faceExtraRecords = reader.Span(faceExtraCount * FaceExtraSize, "faceExtras");
        string[] faceExtraTextures = new string[faceExtraCount];
        for (int index = 0; index < faceExtraCount; index++)
        {
            faceExtraTextures[index] = reader.Text(FaceTextureNameWidth, $"faceExtraTextures[{index}]");
        }

        int sectorCount = reader.ArrayCount(SectorSize, "sectorCount");
        ReadOnlySpan<byte> sectorRecords = reader.Span(sectorCount * SectorSize, "sectors");

        int sectorPoolCount = reader.Elements(sectorDataSizeBytes, PoolValueSize, "sectorDataSizeBytes");
        int sectorPoolOffset = reader.Position;
        MapInt16Pool sectorPool = new(reader.UInt16Values(sectorPoolCount, "sectorData"), reader.Source, "sectorData", sectorPoolOffset);
        int lightPoolCount = reader.Elements(sectorLightDataSizeBytes, PoolValueSize, "sectorLightDataSizeBytes");
        int lightPoolOffset = reader.Position;
        MapInt16Pool lightPool = new(reader.UInt16Values(lightPoolCount, "sectorLightData"), reader.Source, "sectorLightData", lightPoolOffset);

        int doorSlotCount = reader.Int32("doorCount");
        if (doorSlotCount < 0)
        {
            throw reader.Failure($"'doorCount' at 0x{reader.LastOffset:X} is negative ({doorSlotCount}).");
        }

        int decorationCount = reader.ArrayCount(MapRecord.DecorationSize, "decorationCount");
        ReadOnlySpan<byte> decorationData = reader.Span(decorationCount * MapRecord.DecorationSize, "decorations");
        string[] decorationNames = new string[decorationCount];
        for (int index = 0; index < decorationCount; index++)
        {
            decorationNames[index] = reader.Text(MapRecord.DecorationNameWidth, $"decorationNames[{index}]");
        }

        int lightCount = reader.ArrayCount(LightSize, "lightCount");
        ReadOnlySpan<byte> lightData = reader.Span(lightCount * LightSize, "lights");
        int bspNodeCount = reader.ArrayCount(BspNodeSize, "bspNodeCount");
        ReadOnlySpan<byte> bspNodeData = reader.Span(bspNodeCount * BspNodeSize, "bspNodes");
        int spawnCount = reader.ArrayCount(MapRecord.SpawnPointSize, "spawnPointCount");
        ReadOnlySpan<byte> spawnData = reader.Span(spawnCount * MapRecord.SpawnPointSize, "spawnPoints");

        // The minimap outlines are consumed for their length: they describe a map picture, not geometry.
        int outlineCount = reader.ArrayCount(MapOutlineSize, "mapOutlineCount");
        reader.Skip(outlineCount * MapOutlineSize, "mapOutlines");

        reader.ExpectEnd();

        MapPoint[] vertices = new MapPoint[vertexCount];
        for (int index = 0; index < vertexCount; index++)
        {
            vertices[index] = MapRecord.ShortPoint(vertexData, index * VertexSize);
        }

        MapFaceExtra[] faceExtras = new MapFaceExtra[faceExtraCount];
        for (int index = 0; index < faceExtraCount; index++)
        {
            ReadOnlySpan<byte> record = faceExtraRecords.Slice(index * FaceExtraSize, FaceExtraSize);
            faceExtras[index] = new MapFaceExtra(
                index,
                MapRecord.Int16(record, ExtraFaceIdOffset),
                MapRecord.UInt16(record, ExtraAdditionalBitmapIdOffset),
                MapRecord.Int16(record, ExtraTextureDeltaUOffset),
                MapRecord.Int16(record, ExtraTextureDeltaVOffset),
                MapRecord.UInt16(record, ExtraCogNumberOffset),
                MapRecord.UInt16(record, ExtraEventIdOffset),
                faceExtraTextures[index]);
        }

        MapFace[] faces = new MapFace[faceCount];
        for (int index = 0; index < faceCount; index++)
        {
            faces[index] = ReadFace(reader, faceRecords.Slice(index * FaceSize, FaceSize), index, facePool, faceTextures[index], faceExtras, vertices, sectorCount);
        }

        MapSector[] sectors = new MapSector[sectorCount];
        for (int index = 0; index < sectorCount; index++)
        {
            sectors[index] = ReadSector(reader, sectorRecords.Slice(index * SectorSize, SectorSize), index, sectorPool, lightPool, faceCount, lightCount);
        }

        facePool.ExpectConsumed("the level's faces");
        sectorPool.ExpectConsumed("the level's sectors");
        lightPool.ExpectConsumed("the level's sectors");

        MapLight[] lights = new MapLight[lightCount];
        for (int index = 0; index < lightCount; index++)
        {
            ReadOnlySpan<byte> record = lightData.Slice(index * LightSize, LightSize);
            lights[index] = new MapLight(
                index,
                MapRecord.ShortPoint(record, 0),
                MapRecord.Int16(record, LightRadiusOffset),
                MapRecord.Byte(record, LightRedOffset),
                MapRecord.Byte(record, LightGreenOffset),
                MapRecord.Byte(record, LightBlueOffset),
                MapRecord.Byte(record, LightTypeOffset),
                MapRecord.Int16(record, LightAttributesOffset),
                MapRecord.Int16(record, LightBrightnessOffset));
        }

        MapBspNode[] bspNodes = new MapBspNode[bspNodeCount];
        for (int index = 0; index < bspNodeCount; index++)
        {
            ReadOnlySpan<byte> record = bspNodeData.Slice(index * BspNodeSize, BspNodeSize);
            bspNodes[index] = new MapBspNode(
                index,
                MapRecord.Int16(record, 0),
                MapRecord.Int16(record, 2),
                MapRecord.Int16(record, 4),
                MapRecord.Int16(record, 6));
        }

        IndoorCounts counts = new(
            version,
            faceDataSizeBytes,
            sectorDataSizeBytes,
            sectorLightDataSizeBytes,
            doorsDataSizeBytes,
            vertexCount,
            faceCount,
            faceExtraCount,
            sectorCount,
            doorSlotCount,
            decorationCount,
            lightCount,
            bspNodeCount,
            spawnCount,
            outlineCount);

        MapDecoration[] decorations = MapRecord.Decorations(decorationData, decorationNames);
        MapDelta? delta = deltaPayload is null ? null : IndoorDeltaReader.Read(deltaPayload.Value, counts);
        return new IndoorMap(
            payload.Entry.Name,
            version,
            name,
            counts,
            vertices,
            faces,
            faceExtras,
            sectors,
            lights,
            bspNodes,
            decorations,
            MapRecord.EntryPoints(decorations),
            MapRecord.SpawnPoints(spawnData, spawnCount),
            delta);
    }

    private static MapFace ReadFace(
        MapPayloadReader reader,
        ReadOnlySpan<byte> record,
        int index,
        MapInt16Pool facePool,
        string textureName,
        MapFaceExtra[] faceExtras,
        MapPoint[] vertices,
        int sectorCount)
    {
        int faceVertexCount = MapRecord.Byte(record, FaceVertexCountOffset);
        int[] vertexIds = facePool.Take(faceVertexCount, $"face {index} vertexIds");
        // Each face owns six arrays in the pool and every one of them carries a closing slot that is
        // not part of the face; the ids repeat their first vertex there and the rest store zero. The
        // three intersection-offset arrays are consumed but not kept: no consumer places a face's
        // vertices with them, and dropping them would misalign the pool.
        facePool.Skip(1, $"face {index} vertexIds closing slot");
        facePool.Skip(faceVertexCount + 1, $"face {index} xInterceptDisplacements");
        facePool.Skip(faceVertexCount + 1, $"face {index} yInterceptDisplacements");
        facePool.Skip(faceVertexCount + 1, $"face {index} zInterceptDisplacements");
        int[] textureUs = facePool.Take(faceVertexCount, $"face {index} textureUs");
        facePool.Skip(1, $"face {index} textureUs closing slot");
        int[] textureVs = facePool.Take(faceVertexCount, $"face {index} textureVs");
        facePool.Skip(1, $"face {index} textureVs closing slot");

        List<MapPoint> positions = new(faceVertexCount);
        List<MapTextureCoordinate> coordinates = new(faceVertexCount);
        for (int slot = 0; slot < faceVertexCount; slot++)
        {
            int id = vertexIds[slot];
            if (id < 0 || id >= vertices.Length)
            {
                throw reader.Failure($"face {index} names vertex {id} but the level has {vertices.Length} vertices.");
            }

            positions.Add(vertices[id]);
            coordinates.Add(new MapTextureCoordinate(textureUs[slot], textureVs[slot]));
        }

        // The face's texture offsets live in its extra rather than in the face record, and every
        // shipped face names one, so an extra id outside the array is a misread rather than "none".
        int extraId = MapRecord.UInt16(record, FaceExtraIdOffset);
        if (extraId >= faceExtras.Length)
        {
            throw reader.Failure($"face {index} names face extra {extraId} but the level has {faceExtras.Length} extras.");
        }

        // A face that belongs to no sector stores zero or the all-ones sentinel there. No shipped face
        // uses either, and every shipped id is in range, so an out-of-range id is a misread.
        int sectorId = MapRecord.UInt16(record, FaceSectorIdOffset);
        bool hasSector = sectorId != 0 && sectorId != NoSector;
        if (hasSector && sectorId >= sectorCount)
        {
            throw reader.Failure($"face {index} names sector {sectorId} but the level has {sectorCount} sectors.");
        }

        // Back sector zero means the face is not a portal; a shipped portal's far side is in range.
        int backSectorId = MapRecord.Int16(record, FaceBackSectorIdOffset);
        if (backSectorId > 0 && backSectorId >= sectorCount)
        {
            throw reader.Failure($"face {index} names back sector {backSectorId} but the level has {sectorCount} sectors.");
        }

        MapFaceExtra extra = faceExtras[extraId];
        return new MapFace(
            index,
            textureName,
            positions,
            vertexIds,
            coordinates,
            MapRecord.FixedPlane(record, FacePlaneFixedOffset),
            MapRecord.UInt32(record, FaceAttributesOffset),
            MapRecord.Byte(record, FacePolygonTypeOffset),
            extra.TextureDeltaU,
            extra.TextureDeltaV,
            hasSector ? sectorId : -1,
            backSectorId > 0 ? backSectorId : -1,
            extraId);
    }

    private static MapSector ReadSector(
        MapPayloadReader reader,
        ReadOnlySpan<byte> record,
        int index,
        MapInt16Pool sectorPool,
        MapInt16Pool lightPool,
        int faceCount,
        int lightCount)
    {
        // The cylinder face list sits between the faces and the cogs in the sector record, but the
        // donor does not advance the pool by it and no shipped sector sets it. Advancing would
        // misalign every later sector, so a non-zero value is refused rather than guessed at.
        int cylinderFaces = MapRecord.UInt16(record, SectorCylinderFaceCountOffset);
        if (cylinderFaces != 0)
        {
            throw reader.Failure($"sector {index} declares {cylinderFaces} cylinder faces, which the sector data pool does not place.");
        }

        int[] floorIds = sectorPool.Take(MapRecord.UInt16(record, SectorFloorCountOffset), $"sector {index} floors");
        int[] wallIds = sectorPool.Take(MapRecord.UInt16(record, SectorWallCountOffset), $"sector {index} walls");
        int[] ceilingIds = sectorPool.Take(MapRecord.UInt16(record, SectorCeilingCountOffset), $"sector {index} ceilings");
        int[] fluidIds = sectorPool.Take(MapRecord.UInt16(record, SectorFluidCountOffset), $"sector {index} fluids");
        int[] portalIds = sectorPool.Take(MapRecord.UInt16(record, SectorPortalCountOffset), $"sector {index} portals");
        int[] faceIds = sectorPool.Take(MapRecord.UInt16(record, SectorFaceCountOffset), $"sector {index} faces");
        int[] cogIds = sectorPool.Take(MapRecord.UInt16(record, SectorCogCountOffset), $"sector {index} cogs");
        int[] decorationIds = sectorPool.Take(MapRecord.UInt16(record, SectorDecorationCountOffset), $"sector {index} decorations");
        int[] markerIds = sectorPool.Take(MapRecord.UInt16(record, SectorMarkerCountOffset), $"sector {index} markers");
        int[] lightIds = lightPool.Take(MapRecord.UInt16(record, SectorLightCountOffset), $"sector {index} lights");

        foreach (int faceId in faceIds)
        {
            if (faceId >= faceCount) throw reader.Failure($"sector {index} names face {faceId} but the level has {faceCount} faces.");
        }

        foreach (int lightId in lightIds)
        {
            if (lightId >= lightCount) throw reader.Failure($"sector {index} names light {lightId} but the level has {lightCount} lights.");
        }

        int nonBspFaceCount = MapRecord.UInt16(record, SectorNonBspFaceCountOffset);
        if (nonBspFaceCount > faceIds.Length)
        {
            throw reader.Failure($"sector {index} declares {nonBspFaceCount} non-BSP faces but owns {faceIds.Length} faces.");
        }

        return new MapSector(
            index,
            MapRecord.Int32(record, SectorFlagsOffset),
            MapRecord.Int16(record, SectorWaterLevelOffset),
            MapRecord.Int16(record, SectorMistLevelOffset),
            MapRecord.Int16(record, SectorLightDistanceMultiplierOffset),
            MapRecord.Int16(record, SectorMinAmbientLightLevelOffset),
            MapRecord.Int16(record, SectorFirstBspNodeOffset),
            MapRecord.Int16(record, SectorExitTagOffset),
            MapRecord.ShortBounds(record, SectorBoundsOffset),
            floorIds,
            wallIds,
            ceilingIds,
            fluidIds,
            portalIds,
            faceIds,
            faceIds[..nonBspFaceCount],
            cogIds,
            decorationIds,
            markerIds,
            lightIds);
    }
}
