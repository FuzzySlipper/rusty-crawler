using MightAndMagic7.Import.Lod;

namespace MightAndMagic7.Import.Maps;

/// <summary>Reads an indoor delta, which is where an interior's doors and its containers are.</summary>
/// <remarks>
/// A delta is parsed with its level payload as context: the number of faces it carries attributes for,
/// the number of decorations it carries flags for, and the size of its door array all come from the
/// level, not from the delta. Most of it is presentation or runtime state that this decoder consumes
/// and does not surface — how much of the map has been revealed, which faces and decorations have
/// changed, and the actors standing on it. Its chests and sprite objects are surfaced because a place's
/// containers and the items lying in it are what a party reaches for, and neither is stored anywhere
/// else.
/// </remarks>
internal static class IndoorDeltaReader
{
    private const int HeaderSize = 40;
    private const int VisibleOutlinesSize = 875;
    private const int DoorSize = 80;
    private const int EventVariableSize = 200;
    private const int WeatherSkyNameWidth = 12;
    private const int WeatherUnusedSize = 24;
    private const int PoolValueSize = sizeof(short);

    // Field offsets inside BLVDoor_MM7; the eight pointers between the speeds and the counts are stale
    // addresses, and the pool order below is what replaces them.
    private const int DoorAttributesOffset = 0x00;
    private const int DoorIdOffset = 0x04;
    private const int DoorTimeSinceTriggeredOffset = 0x08;
    private const int DoorDirectionOffset = 0x0C;
    private const int DoorMoveLengthOffset = 0x18;
    private const int DoorOpenSpeedOffset = 0x1C;
    private const int DoorCloseSpeedOffset = 0x20;
    private const int DoorVertexCountOffset = 0x44;
    private const int DoorFaceCountOffset = 0x46;
    private const int DoorSectorCountOffset = 0x48;
    private const int DoorOffsetCountOffset = 0x4A;
    private const int DoorStateOffset = 0x4C;

    internal static MapDelta Read(LodPayload payload, IndoorCounts counts)
    {
        MapPayloadReader reader = new(payload);
        MapDeltaHeader header = new(
            reader.Int32("header.respawnCount"),
            reader.Int32("header.lastRespawnDay"),
            reader.Int32("header.reputation"),
            reader.Int32("header.alertStatus"),
            reader.UInt32("header.totalFacesCount"),
            reader.UInt32("header.decorationCount"),
            reader.UInt32("header.bmodelCount"),
            reader.Int32("header.field1C"),
            reader.Int32("header.field20"),
            reader.Int32("header.field24"));

        // The reveal bitfield, the per-face attributes and the per-decoration flags are sized by the
        // level payload rather than by the header's counts, which are zero in every shipped delta.
        reader.Skip(VisibleOutlinesSize, "visibleOutlines");
        reader.Skip(reader.BytesOf(counts.FaceCount, sizeof(uint), "faceAttributes"), "faceAttributes");
        reader.Skip(reader.BytesOf(counts.DecorationCount, sizeof(ushort), "decorationFlags"), "decorationFlags");

        int actorCount = reader.ArrayCount(MapDeltaRecord.ActorSize, "actorCount");
        ReadOnlySpan<byte> actorRecords = reader.Span(
            reader.BytesOf(actorCount, MapDeltaRecord.ActorSize, "actors"),
            "actors");

        int spriteObjectCount = reader.ArrayCount(MapDeltaRecord.SpriteObjectSize, "spriteObjectCount");
        ReadOnlySpan<byte> spriteObjectRecords = reader.Span(
            reader.BytesOf(spriteObjectCount, MapDeltaRecord.SpriteObjectSize, "spriteObjects"),
            "spriteObjects");

        int chestCount = reader.ArrayCount(MapDeltaRecord.ChestSize, "chestCount");
        ReadOnlySpan<byte> chestRecords = reader.Span(
            reader.BytesOf(chestCount, MapDeltaRecord.ChestSize, "chests"),
            "chests");

        // The door array has no count prefix of its own: the level payload declares how many slots the
        // level has, and the delta always stores exactly that many records.
        ReadOnlySpan<byte> doorRecords = reader.Span(reader.BytesOf(counts.DoorSlotCount, DoorSize, "doorCount"), "doors");
        int doorPoolCount = reader.Elements(counts.DoorsDataSizeBytes, PoolValueSize, "doorsDataSizeBytes");
        int doorPoolOffset = reader.Position;
        MapInt16Pool doorPool = new(reader.Int16Values(doorPoolCount, "doorsData"), reader.Source, "doorsData", doorPoolOffset);

        reader.Skip(EventVariableSize, "eventVariables");
        long lastVisitTime = reader.Int64("lastVisitTime");
        string skyTexture = reader.Text(WeatherSkyNameWidth, "weather.skyTexture");
        int weatherFlags = reader.Int32("weather.flags");
        int fogDistance1 = reader.Int32("weather.fogDistance1");
        int fogDistance2 = reader.Int32("weather.fogDistance2");
        reader.Skip(WeatherUnusedSize, "weather.unused");

        reader.ExpectEnd();

        MapDoor[] doors = new MapDoor[counts.DoorSlotCount];
        for (int index = 0; index < doors.Length; index++)
        {
            doors[index] = ReadDoor(reader, doorRecords.Slice(index * DoorSize, DoorSize), index, doorPool, counts);
        }

        doorPool.ExpectConsumed("the doors");

        return new MapDelta(
            header,
            counts.FaceCount,
            counts.DecorationCount,
            actorCount,
            MapDeltaRecord.Actors(actorRecords, actorCount),
            MapDeltaRecord.SpriteObjects(spriteObjectRecords, spriteObjectCount),
            MapDeltaRecord.Chests(chestRecords, chestCount),
            lastVisitTime,
            new MapWeather(skyTexture, weatherFlags, fogDistance1, fogDistance2),
            doors);
    }

    private static MapDoor ReadDoor(
        MapPayloadReader reader,
        ReadOnlySpan<byte> record,
        int index,
        MapInt16Pool doorPool,
        IndoorCounts counts)
    {
        int vertexCount = MapRecord.UInt16(record, DoorVertexCountOffset);
        int faceCount = MapRecord.UInt16(record, DoorFaceCountOffset);
        int sectorCount = MapRecord.UInt16(record, DoorSectorCountOffset);
        int offsetCount = MapRecord.UInt16(record, DoorOffsetCountOffset);

        int[] vertexIds = doorPool.Take(vertexCount, $"door {index} vertexIds");
        int[] faceIds = doorPool.Take(faceCount, $"door {index} faceIds");
        int[] sectorIds = doorPool.Take(sectorCount, $"door {index} sectorIds");
        int[] deltaUs = doorPool.Take(faceCount, $"door {index} deltaUs");
        int[] deltaVs = doorPool.Take(faceCount, $"door {index} deltaVs");
        int[] xOffsets = doorPool.Take(offsetCount, $"door {index} xOffsets");
        int[] yOffsets = doorPool.Take(offsetCount, $"door {index} yOffsets");
        int[] zOffsets = doorPool.Take(offsetCount, $"door {index} zOffsets");

        foreach (int vertexId in vertexIds)
        {
            if (vertexId < 0 || vertexId >= counts.VertexCount)
            {
                throw reader.Failure($"door {index} names vertex {vertexId} but the level has {counts.VertexCount} vertices.");
            }
        }

        foreach (int faceId in faceIds)
        {
            if (faceId < 0 || faceId >= counts.FaceCount)
            {
                throw reader.Failure($"door {index} names face {faceId} but the level has {counts.FaceCount} faces.");
            }
        }

        return new MapDoor(
            index,
            vertexCount != 0,
            MapRecord.UInt16(record, DoorStateOffset),
            MapRecord.UInt32(record, DoorAttributesOffset),
            MapRecord.UInt32(record, DoorIdOffset),
            MapRecord.UInt32(record, DoorTimeSinceTriggeredOffset),
            MapRecord.Point(record, DoorDirectionOffset),
            MapRecord.UInt32(record, DoorMoveLengthOffset),
            MapRecord.UInt32(record, DoorOpenSpeedOffset),
            MapRecord.UInt32(record, DoorCloseSpeedOffset),
            vertexIds,
            faceIds,
            sectorIds,
            deltaUs,
            deltaVs,
            xOffsets,
            yOffsets,
            zOffsets);
    }
}
