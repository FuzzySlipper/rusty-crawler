using System.Text.Json;
using MightAndMagic7.Import.Lod;
using MightAndMagic7.Import.Packs;
using MightAndMagic7.Import.Tool;
using Xunit;

namespace MightAndMagic7.Import.Tests;

/// <summary>
/// The building table's emission: the counters a party can walk up to, the households it can speak to, the
/// rows nothing was placed for, and the stables whose passages the ruleset decides.
/// </summary>
/// <remarks>
/// <para>
/// What is proved here is that the table's own columns become content and that a counter's position is the
/// map's geometry rather than a number this importer chose: the placement carries the face, the model, and
/// the texture it was read from, and a row whose map hangs no event on it is refused by name instead of
/// being placed somewhere plausible.
/// </para>
/// <para>
/// The fixture is synthetic, so nothing here is the operator's data: it is a table with a weapon shop, a
/// stable, a temple, and a house, the programs that open them, and maps whose faces raise those events.
/// </para>
/// </remarks>
public sealed class ServiceEmissionTests
{
    [Theory]
    [InlineData(false, 9, 21)]
    [InlineData(true, 0, 24)]
    public void The_building_table_becomes_counters_and_households_and_no_passages(bool allDayService, int opens, int closes)
    {
        string installRoot = SyntheticInstallation.Create(withMaps: true, withServices: true, allDayService: allDayService);
        string root = Path.Combine(Path.GetTempPath(), $"mm7-services-{Guid.NewGuid():N}");
        try
        {
            PackWriteResult written = PackWriter.Write(LodInstall.Open(installRoot), root);
            PlaceServiceSummary services = written.Services;

            // The fixture's counter rows are entered: a weapon shop, a stable, and a temple, each with the
            // row's own kind, name, proprietor, hours, and multipliers.
            Assert.Equal(4, services.ServiceCount);
            Assert.Equal(1, services.CountKind("Weapon Shop"));
            Assert.Equal(2, services.CountKind("Stables"));
            Assert.Equal(1, services.CountKind("Temple"));
            PlaceServiceDefinition shop = Assert.Single(services.Services, service => service.Kind == "Weapon Shop");
            Assert.Equal(98, shop.BuildingId);
            Assert.Equal("Building 98", shop.Name);
            Assert.Equal("Proprietor 98", shop.Proprietor);
            Assert.Equal(SyntheticInstallation.ServiceMap(98), shop.MapId);
            Assert.Equal(opens, shop.OpenHour);
            Assert.Equal(closes, shop.ClosedHour);
            Assert.Equal(1.5, shop.PriceMultiplier);
            Assert.Equal(7, shop.StockIntervalDays);

            // Every counter stands in a place, at a point read from a face of the map that raises the
            // house's own event, and the placement says which face it was.
            Assert.Equal(5, services.PlacementCount);
            Assert.Equal(4, services.CounterCount);
            Assert.Equal(1, services.ResidenceCount);
            PlaceServicePlacement placement = Assert.Single(services.Placements, item => item.BuildingId == 98);
            Assert.Equal(PlaceServiceEmitter.ServicePlacementKind, placement.PlacementKind);
            Assert.Equal(SyntheticInstallation.ServiceMap(98), placement.PlaceId);
            Assert.Equal(98, placement.EventId);
            Assert.True(placement.FaceCount >= 1);
            Assert.Contains(placement.PositionSource, new[] { "sign-face-centroid", "door-face-centroid", "lowest-face-centroid" });
            Assert.Contains(
                placement.HeightSource,
                new[] { PlaceServiceEmitter.TerrainHeightSource, PlaceServiceEmitter.FaceHeightSource });
            Assert.Equal("pc05-01", placement.KeeperPortrait);
            Assert.All(services.Placements, item => Assert.NotEmpty(item.KeeperPortrait));

            // The house row is a household rather than a counter, which is what the table called it.
            PlaceServicePlacement house = Assert.Single(services.Placements, item => item.BuildingId == 100);
            Assert.Equal(PlaceServiceEmitter.ResidencePlacementKind, house.PlacementKind);
            Assert.Equal("House R100", house.Fixture);
            using JsonDocument emittedPlaces = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "mm7-tables", "places.json")));
            JsonElement emittedHouse = emittedPlaces.RootElement.GetProperty("entries").EnumerateArray()
                .SelectMany(place => place.TryGetProperty("placements", out var placed) ? placed.EnumerateArray().ToArray() : [])
                .Single(placed => placed.GetProperty("id").GetString() == "residence-100");
            Assert.Equal(house.OpenHour, emittedHouse.GetProperty("openHour").GetInt32());
            Assert.Equal(house.ClosedHour, emittedHouse.GetProperty("closedHour").GetInt32());
            Assert.Equal(house.KeeperPortrait, emittedHouse.GetProperty("portrait").GetString());
            JsonElement emittedCounter = emittedPlaces.RootElement.GetProperty("entries").EnumerateArray()
                .SelectMany(place => place.TryGetProperty("placements", out var placed) ? placed.EnumerateArray().ToArray() : [])
                .Single(placed => placed.GetProperty("id").GetString() == "service-98");
            Assert.False(emittedCounter.TryGetProperty("openHour", out _));
            Assert.False(emittedCounter.TryGetProperty("closedHour", out _));
            Assert.Equal(placement.KeeperPortrait, emittedCounter.GetProperty("portrait").GetString());
            Assert.Null(placement.OpenHour);
            Assert.Null(placement.ClosedHour);


            // Every other row of the fixture names a map that hangs no event on it, and each is refused by
            // name rather than placed somewhere plausible: nothing is silently dropped.
            Assert.All(services.Refusals, refusal => Assert.Equal("no-signing-face", refusal.Code));
            Assert.Equal(525, services.ServiceCount + services.ResidenceCount + services.Refusals.Count);

            // Both stables are emitted as what the table says they are — a counter of the stable kind standing in
            // its own place — and nothing more: which destinations a stable sells and how long they take are the
            // ruleset's fare network and fare rule, so the import derives no passage and writes no route.
            PlaceServiceDefinition stable = services.Services.Single(service => service.BuildingId == 99);
            PlaceServiceDefinition other = services.Services.Single(service => service.BuildingId == 198);
            Assert.Equal(2, services.FareCounterCount);
            Assert.Equal(SyntheticInstallation.ServiceMap(stable.BuildingId), Assert.Single(services.Placements, item => item.BuildingId == stable.BuildingId).PlaceId);
            Assert.Equal(SyntheticInstallation.ServiceMap(other.BuildingId), Assert.Single(services.Placements, item => item.BuildingId == other.BuildingId).PlaceId);

            string places = File.ReadAllText(Path.Combine(root, "mm7-tables", "places.json"));
            Assert.Contains($"\"service-{shop.BuildingId}\"", places, StringComparison.Ordinal);
            Assert.Contains("\"residence-100\"", places, StringComparison.Ordinal);
            Assert.Contains("\"positionSource\": \"", places, StringComparison.Ordinal);

            string graph = File.ReadAllText(Path.Combine(root, "mm7-world", "place-graph.json"));
            Assert.DoesNotContain("\"fare\"", graph, StringComparison.Ordinal);
            Assert.DoesNotContain("\"route\"", graph, StringComparison.Ordinal);

            string definitions = File.ReadAllText(Path.Combine(root, "mm7-tables", "services.json"));
            Assert.Contains("\"definitionKind\": \"service\"", definitions, StringComparison.Ordinal);
            Assert.Contains($"\"kind\": \"{shop.Kind}\"", definitions, StringComparison.Ordinal);
            Assert.Contains("\"source\": \"2DEvents.txt\"", definitions, StringComparison.Ordinal);

            // The stable's definition is the table's row and where it stands, with no destinations of its own.
            using JsonDocument document = JsonDocument.Parse(definitions);
            JsonElement shopEntry = document.RootElement.GetProperty("entries").EnumerateArray()
                .Single(entry => entry.GetProperty("id").GetString() == "98");
            Assert.Equal(opens, shopEntry.GetProperty("openHour").GetInt32());
            Assert.Equal(closes, shopEntry.GetProperty("closedHour").GetInt32());
            JsonElement stableEntry = document.RootElement.GetProperty("entries")
                .EnumerateArray()
                .Single(entry => entry.GetProperty("id").GetString() == stable.BuildingId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal("Stables", stableEntry.GetProperty("kind").GetString());
            Assert.Equal(SyntheticInstallation.ServiceMap(stable.BuildingId).ToString(System.Globalization.CultureInfo.InvariantCulture), stableEntry.GetProperty("place").GetString());
            Assert.False(stableEntry.TryGetProperty("fares", out _));
        }
        finally
        {
            Directory.Delete(installRoot, recursive: true);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void The_same_installation_emits_the_same_services()
    {
        string installRoot = SyntheticInstallation.Create(withMaps: true, withServices: true);
        string first = Path.Combine(Path.GetTempPath(), $"mm7-services-a-{Guid.NewGuid():N}");
        string second = Path.Combine(Path.GetTempPath(), $"mm7-services-b-{Guid.NewGuid():N}");
        try
        {
            LodInstall install = LodInstall.Open(installRoot);
            PackWriteResult left = PackWriter.Write(install, first);
            PackWriter.Write(install, second);

            // A counter's position is a face of a map and its shelves are the item table's own goods, so
            // two runs over one installation must agree on both: the definitions and the placements are
            // compared as bytes rather than as shapes.
            Assert.True(PackWriter.AreIdentical(first, second));
            Assert.Equal(left.Services.ServiceCount, PackWriter.Write(install, second).Services.ServiceCount);
        }
        finally
        {
            Directory.Delete(installRoot, recursive: true);
            if (Directory.Exists(first)) Directory.Delete(first, recursive: true);
            if (Directory.Exists(second)) Directory.Delete(second, recursive: true);
        }
    }
}
