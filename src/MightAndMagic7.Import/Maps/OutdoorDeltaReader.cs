using MightAndMagic7.Import.Lod;

namespace MightAndMagic7.Import.Maps;

/// <summary>Reads an outdoor delta: the runtime state that travels beside an outdoor map.</summary>
/// <remarks>
/// An outdoor delta has no doors. What it carries beyond the shared header is how much of the map the
/// party has revealed, plus the same per-face attributes, per-decoration flags, actors, sprite objects,
/// chests, map variables, last visit time and weather that an indoor delta does — all consumed, and
/// only the counts kept, because those records belong to the object and event formats rather than to
/// this one.
/// </remarks>
internal static class OutdoorDeltaReader
{
    private const int RevelationCells = 88 * 11;
    private const int ActorSize = 0x344;
    private const int SpriteObjectSize = 0x70;
    private const int ChestSize = 5324;
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

        int actorCount = reader.ArrayCount(ActorSize, "actorCount");
        reader.Skip(reader.BytesOf(actorCount, ActorSize, "actors"), "actors");
        int spriteObjectCount = reader.ArrayCount(SpriteObjectSize, "spriteObjectCount");
        reader.Skip(reader.BytesOf(spriteObjectCount, SpriteObjectSize, "spriteObjects"), "spriteObjects");
        int chestCount = reader.ArrayCount(ChestSize, "chestCount");
        reader.Skip(reader.BytesOf(chestCount, ChestSize, "chests"), "chests");

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
            spriteObjectCount,
            chestCount,
            lastVisitTime,
            new MapWeather(skyTexture, weatherFlags, fogDistance1, fogDistance2),
            []);
    }
}
