using MightAndMagic7.Import.Lod;

namespace MightAndMagic7.Import.Maps;

/// <summary>Reads an outdoor payload in the order its layout stores the fields.</summary>
/// <remarks>
/// The order is the whole format: the model headers sit in one block before any model's vertices, the
/// normal blocks sit between the height field and the models, and the decoration map sits between the
/// decoration references and the spawn points. Everything is consumed in file order and the walk is
/// required to end exactly at the payload's end, because a map whose walk ends early has already been
/// misread somewhere.
/// </remarks>
internal static class OutdoorMapReader
{
    private const int TerrainCells = OutdoorMap.TerrainCells;
    private const int NameWidth = 32;
    private const int TileTypeCount = 4;
    private const int NormalSize = 12;
    private const int ModelHeaderSize = 188;
    private const int ModelVertexSize = 12;
    private const int ModelFaceSize = 308;
    private const int ModelNodeSize = 8;
    private const int FaceTextureNameWidth = 10;
    private const int FaceVertexSlots = 20;
    private const int DecorationMapSize = TerrainCells * TerrainCells * sizeof(uint);

    // Field offsets inside BSPModelData_MM7, which the payload stores as one 188-byte block per model.
    private const int ModelName2Offset = 0x20;
    private const int ModelWasSeenOffset = 0x40;
    private const int ModelVertexCountOffset = 0x44;
    private const int ModelFaceCountOffset = 0x4C;
    private const int ModelConvexFaceCountOffset = 0x50;
    private const int ModelNodeCountOffset = 0x5C;
    private const int ModelDecorationCountOffset = 0x64;
    private const int ModelPositionOffset = 0x70;
    private const int ModelBoundsOffset = 0x7C;
    private const int ModelOtherBoundsOffset = 0x94;
    private const int ModelBoundingCenterOffset = 0xAC;
    private const int ModelBoundingRadiusOffset = 0xB8;

    // Field offsets inside ODMFace_MM7.
    private const int FacePlaneOffset = 0x00;
    private const int FaceAttributesOffset = 0x1C;
    private const int FaceVertexIdsOffset = 0x20;
    private const int FaceTextureUsOffset = 0x48;
    private const int FaceTextureVsOffset = 0x70;
    private const int FaceTextureDeltaUOffset = 0x112;
    private const int FaceTextureDeltaVOffset = 0x114;
    private const int FaceVertexCountOffset = 0x12E;
    private const int FacePolygonTypeOffset = 0x12F;

    internal static OutdoorMap Read(LodPayload payload, LodPayload? deltaPayload)
    {
        MapPayloadReader reader = new(payload);
        string name = reader.Text(NameWidth, "name");
        string payloadFileName = reader.Text(NameWidth, "fileName");
        string description = reader.Text(NameWidth, "description");
        string skyTexture = reader.Text(NameWidth, "skyTexture");
        string groundTileset = reader.Text(NameWidth, "groundTilesetUnused");

        OutdoorTileType[] tileTypes = new OutdoorTileType[TileTypeCount];
        for (int index = 0; index < tileTypes.Length; index++)
        {
            int tileset = reader.UInt16($"tileTypes[{index}].tileset");
            int tileOffset = reader.UInt16($"tileTypes[{index}].tileOffset");
            tileTypes[index] = new OutdoorTileType(tileset, tileOffset);
        }

        int terrainCells = TerrainCells * TerrainCells;
        byte[] heightMap = reader.Bytes(terrainCells, "heightMap");
        byte[] tileMap = reader.Bytes(terrainCells, "tileMap");
        // Zero throughout the shipped maps and read by no donor, so it is consumed for its length only.
        reader.Skip(terrainCells, "attributeMap");

        int normalCount = reader.ArrayCount(NormalSize, "normalCount");
        // The per-cell normal blocks are consumed but not kept: their sizes are established and their
        // meaning is not, and the donor recomputes normals from the heights instead of reading them.
        reader.Skip(terrainCells * 2 * sizeof(float), "normalDistances");
        reader.Skip(terrainCells * 2 * sizeof(ushort), "normalIndices");
        reader.Skip(reader.BytesOf(normalCount, NormalSize, "normals"), "normals");

        int modelCount = reader.ArrayCount(ModelHeaderSize, "modelCount");
        ReadOnlySpan<byte> modelHeaders = reader.Span(modelCount * ModelHeaderSize, "modelHeaders");

        List<OutdoorModel> models = new(modelCount);
        List<MapPoint> vertices = [];
        List<MapFace> faces = [];
        for (int index = 0; index < modelCount; index++)
        {
            ReadOnlySpan<byte> header = modelHeaders.Slice(index * ModelHeaderSize, ModelHeaderSize);
            int vertexCount = MapRecord.Int32(header, ModelVertexCountOffset);
            int faceCount = MapRecord.Int32(header, ModelFaceCountOffset);
            int nodeCount = MapRecord.Int32(header, ModelNodeCountOffset);

            int vertexBytes = reader.BytesOf(vertexCount, ModelVertexSize, $"model {index} vertices");
            int faceBytes = reader.BytesOf(faceCount, ModelFaceSize, $"model {index} faces");
            int nodeBytes = reader.BytesOf(nodeCount, ModelNodeSize, $"model {index} nodes");
            ReadOnlySpan<byte> vertexData = reader.Span(vertexBytes, $"model {index} vertices");
            ReadOnlySpan<byte> faceData = reader.Span(faceBytes, $"model {index} faces");
            // The ordering words are zero or garbage in every shipped model and no consumer uses them,
            // and the per-model BSP nodes are empty in every one; both are consumed for their length.
            reader.Skip(faceCount * sizeof(ushort), $"model {index} facesOrdering");
            reader.Skip(nodeBytes, $"model {index} nodes");

            string[] textures = new string[faceCount];
            for (int face = 0; face < faceCount; face++)
            {
                textures[face] = reader.Text(FaceTextureNameWidth, $"model {index} faceTextures[{face}]");
            }

            MapPoint[] modelVertices = new MapPoint[vertexCount];
            for (int vertex = 0; vertex < vertexCount; vertex++)
            {
                modelVertices[vertex] = MapRecord.Point(vertexData, vertex * ModelVertexSize);
            }

            MapFace[] modelFaces = new MapFace[faceCount];
            for (int face = 0; face < faceCount; face++)
            {
                modelFaces[face] = ReadFace(
                    reader,
                    faceData.Slice(face * ModelFaceSize, ModelFaceSize),
                    index,
                    face,
                    modelVertices,
                    textures[face]);
            }

            models.Add(new OutdoorModel(
                index,
                MapRecord.Text(header, 0, NameWidth),
                MapRecord.Text(header, ModelName2Offset, NameWidth),
                MapRecord.Int32(header, ModelWasSeenOffset),
                MapRecord.Int32(header, ModelConvexFaceCountOffset),
                nodeCount,
                MapRecord.Int32(header, ModelDecorationCountOffset),
                MapRecord.Point(header, ModelPositionOffset),
                MapRecord.Bounds(header, ModelBoundsOffset),
                MapRecord.Bounds(header, ModelOtherBoundsOffset),
                MapRecord.Point(header, ModelBoundingCenterOffset),
                MapRecord.Int32(header, ModelBoundingRadiusOffset),
                modelVertices,
                modelFaces));
            vertices.AddRange(modelVertices);
            faces.AddRange(modelFaces);
        }

        int decorationCount = reader.ArrayCount(MapRecord.DecorationSize, "decorationCount");
        ReadOnlySpan<byte> decorationData = reader.Span(decorationCount * MapRecord.DecorationSize, "decorations");
        string[] decorationNames = new string[decorationCount];
        for (int index = 0; index < decorationCount; index++)
        {
            decorationNames[index] = reader.Text(MapRecord.DecorationNameWidth, $"decorationNames[{index}]");
        }

        // The object reference list and the decoration map that indexes it place decorations on the
        // terrain, which the decoration records already state, so both are consumed for their length.
        int pidCount = reader.ArrayCount(sizeof(ushort), "decorationPidCount");
        reader.Skip(pidCount * sizeof(ushort), "decorationPidList");
        reader.Skip(DecorationMapSize, "decorationMap");

        int spawnCount = reader.ArrayCount(MapRecord.SpawnPointSize, "spawnPointCount");
        ReadOnlySpan<byte> spawnData = reader.Span(spawnCount * MapRecord.SpawnPointSize, "spawnPoints");

        reader.ExpectEnd();

        MapDecoration[] decorations = MapRecord.Decorations(decorationData, decorationNames);
        MapDelta? delta = deltaPayload is null
            ? null
            : OutdoorDeltaReader.Read(deltaPayload.Value, faces.Count, decorationCount);
        OutdoorCounts counts = new(normalCount, modelCount, decorationCount, pidCount, spawnCount, faces.Count, vertices.Count);
        return new OutdoorMap(
            payload.Entry.Name,
            name,
            payloadFileName,
            description,
            skyTexture,
            groundTileset,
            tileTypes,
            heightMap,
            tileMap,
            counts,
            models,
            vertices,
            faces,
            decorations,
            MapRecord.EntryPoints(decorations),
            MapRecord.SpawnPoints(spawnData, spawnCount),
            delta);
    }

    private static MapFace ReadFace(
        MapPayloadReader reader,
        ReadOnlySpan<byte> record,
        int modelIndex,
        int faceIndex,
        MapPoint[] vertices,
        string textureName)
    {
        int vertexCount = MapRecord.Byte(record, FaceVertexCountOffset);
        // The face stores its corners in a fixed 20-slot array, and the payload's own count should not
        // outrun it; a larger count would mean this face's fields are not where the layout puts them.
        if (vertexCount > FaceVertexSlots)
        {
            throw reader.Failure(
                $"model {modelIndex} face {faceIndex} declares {vertexCount} vertices but the layout stores {FaceVertexSlots}.");
        }

        List<MapPoint> positions = new(vertexCount);
        List<int> vertexIds = new(vertexCount);
        List<MapTextureCoordinate> coordinates = new(vertexCount);
        for (int slot = 0; slot < vertexCount; slot++)
        {
            int id = MapRecord.Int16(record, FaceVertexIdsOffset + (slot * sizeof(short)));
            if (id < 0 || id >= vertices.Length)
            {
                throw reader.Failure(
                    $"model {modelIndex} face {faceIndex} names vertex {id} but the model has {vertices.Length} vertices.");
            }

            vertexIds.Add(id);
            positions.Add(vertices[id]);
            coordinates.Add(new MapTextureCoordinate(
                MapRecord.Int16(record, FaceTextureUsOffset + (slot * sizeof(short))),
                MapRecord.Int16(record, FaceTextureVsOffset + (slot * sizeof(short)))));
        }

        return new MapFace(
            faceIndex,
            textureName,
            positions,
            vertexIds,
            coordinates,
            MapRecord.FixedPlane(record, FacePlaneOffset),
            MapRecord.UInt32(record, FaceAttributesOffset),
            MapRecord.Byte(record, FacePolygonTypeOffset),
            MapRecord.Int16(record, FaceTextureDeltaUOffset),
            MapRecord.Int16(record, FaceTextureDeltaVOffset),
            -1,
            -1,
            -1);
    }
}
