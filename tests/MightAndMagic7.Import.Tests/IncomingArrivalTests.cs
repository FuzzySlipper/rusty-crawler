using System.Text.Json;
using MightAndMagic7.Import.Tool;
using MightAndMagic7.Import.World;
using Xunit;

namespace MightAndMagic7.Import.Tests;

public sealed class IncomingArrivalTests
{
    [Fact]
    public void Missing_decorations_use_only_explicit_incoming_poses_with_source_provenance()
    {
        PlaceLink arrival = new("OUT01.EVT", 1, 2, "d01.blv", 12, -34, 56, 789, 10, 0, 0, 501, 3);
        PlaceLink[] links =
        [
            arrival with { DestinationMapId = 3 },
            arrival with { X = 0, Y = 0, Z = 0 },
            arrival,
            arrival with { SourceEvtName = "OUT02.EVT", SourceMapId = 3, X = -98, EventId = 502 },
        ];
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream))
        {
            writer.WriteStartArray();
            PackWriter.WriteIncomingArrivals(writer, 2, links);
            writer.WriteEndArray();
        }

        using JsonDocument document = JsonDocument.Parse(stream.ToArray());
        JsonElement[] points = document.RootElement.EnumerateArray().ToArray();
        Assert.Equal(2, points.Length);
        Assert.Equal("arrival-2", points[0].GetProperty("id").GetString());
        Assert.Equal("arrival-3", points[1].GetProperty("id").GetString());
        Assert.Equal(-98, points[1].GetProperty("x").GetInt32());
        Assert.Equal(12, points[0].GetProperty("x").GetInt32());
        Assert.Equal(-34, points[0].GetProperty("y").GetInt32());
        Assert.Equal(56, points[0].GetProperty("z").GetInt32());
        Assert.Equal(789, points[0].GetProperty("yaw").GetInt32());
        Assert.Equal(10, points[0].GetProperty("pitch").GetInt32());
        Assert.Equal("incoming-map-move", points[0].GetProperty("source").GetString());
        Assert.Equal("2", points[0].GetProperty("travelLink").GetString());
        Assert.Equal("OUT01.EVT", points[0].GetProperty("program").GetString());
        Assert.Equal(501, points[0].GetProperty("eventId").GetInt32());
        Assert.Equal(3, points[0].GetProperty("step").GetInt32());
    }
}
