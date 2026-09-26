using MightAndMagic7.Import.Lod;

namespace MightAndMagic7.Import.Maps;

/// <summary>Reads an outdoor delta: the runtime state that travels beside an outdoor map.</summary>
/// <remarks>
/// An outdoor delta has no doors. What it carries beyond the shared header is how much of the map the
/// party has revealed, plus the same per-face attributes, per-decoration flags, actors, sprite objects,
/// chests, map variables, last visit time and weather that an indoor delta does. Its actors are consumed
/// without being kept, because an actor's record belongs to the actor format; its sprite objects and
/// chests are kept, because a region's chests are placed by the map's own event faces exactly as an
/// interior's are.
/// </remarks>
internal static class OutdoorDeltaReader
{
    private const int RevelationCells = 88 * 11;
    private const int EventVariableSize = 200;
    private const int WeatherSkyNameWidth = 12;
    private const int WeatherUnusedSize = 24;

    internal static MapDelta Read(LodPayload payload, int faceCount, int decorationCount)
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

        // The revealed-cell blocks replace an indoor delta's outline bitfield; the attribute and flag
        // arrays are sized by the map payload rather than by the header, which is zero in every delta.
        reader.Skip(RevelationCells, "fullyRevealedCells");
        reader.Skip(RevelationCells, "partiallyRevealedCells");
        reader.Skip(reader.BytesOf(faceCount, sizeof(uint), "faceAttributes"), "faceAttributes");
        reader.Skip(reader.BytesOf(decorationCount, sizeof(ushort), "decorationFlags"), "decorationFlags");

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

        reader.Skip(EventVariableSize, "eventVariables");
        long lastVisitTime = reader.Int64("lastVisitTime");
        string skyTexture = reader.Text(WeatherSkyNameWidth, "weather.skyTexture");
        int weatherFlags = reader.Int32("weather.flags");
        int fogDistance1 = reader.Int32("weather.fogDistance1");
        int fogDistance2 = reader.Int32("weather.fogDistance2");
        reader.Skip(WeatherUnusedSize, "weather.unused");

        reader.ExpectEnd();

        return new MapDelta(
            header,
            faceCount,
            decorationCount,
            actorCount,
            MapDeltaRecord.Actors(actorRecords, actorCount),
            MapDeltaRecord.SpriteObjects(spriteObjectRecords, spriteObjectCount),
            MapDeltaRecord.Chests(chestRecords, chestCount),
            lastVisitTime,
            new MapWeather(skyTexture, weatherFlags, fogDistance1, fogDistance2),
            []);
    }
}
