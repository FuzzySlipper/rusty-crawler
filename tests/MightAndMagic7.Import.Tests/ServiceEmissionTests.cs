using System.Text.Json;
using MightAndMagic7.Import.Lod;
using MightAndMagic7.Import.Packs;
using MightAndMagic7.Import.Tool;
using Xunit;

namespace MightAndMagic7.Import.Tests;

/// <summary>
/// The building table's emission: the counters a party can walk up to, the households it can speak to, the
/// rows nothing was placed for, and the passages a stable sells.
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
    [Fact]
    public void The_building_table_becomes_counters_households_and_passages()
    {
        string installRoot = SyntheticInstallation.Create(withMaps: true, withServices: true);
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
            Assert.Equal(9, shop.OpenHour);
            Assert.Equal(21, shop.ClosedHour);
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

            // The house row is a household rather than a counter, which is what the table called it.
            PlaceServicePlacement house = Assert.Single(services.Placements, item => item.BuildingId == 100);
            Assert.Equal(PlaceServiceEmitter.ResidencePlacementKind, house.PlacementKind);
            Assert.Equal("House R100", house.Fixture);

            // Every other row of the fixture names a map that hangs no event on it, and each is refused by
            // name rather than placed somewhere plausible: nothing is silently dropped.
            Assert.All(services.Refusals, refusal => Assert.Equal("no-signing-face", refusal.Code));
            Assert.Equal(525, services.ServiceCount + services.ResidenceCount + services.Refusals.Count);

            // The stable sells a passage to the other place in the network, and the link that takes it is
            // in the world's graph with the destination's own arrival point.
            PlaceServiceDefinition stable = services.Services.Single(service => service.BuildingId == 99);
            PlaceServiceDefinition other = services.Services.Single(service => service.BuildingId == 198);

            // Two places keep a coach, so each sells a passage to the other: one fare each way, arriving at
            // the destination's own arrival point, and the link that takes it is named for the counter that
            // sold it.
            Assert.Equal(2, services.FareCount);
            Assert.All(services.Fares, fare => Assert.Equal(SyntheticInstallation.ServiceMap(stable.BuildingId) == fare.FromPlace ? stable.BuildingId : other.BuildingId, fare.ServiceId));
            PlaceFare fare = Assert.Single(services.Fares, item => item.ServiceId == stable.BuildingId);
            Assert.Equal(SyntheticInstallation.ServiceMap(stable.BuildingId), fare.FromPlace);
            Assert.Equal(SyntheticInstallation.ServiceMap(other.BuildingId), fare.ToPlace);
            Assert.Equal(PlaceServiceEmitter.CoachDays, fare.Days);
            Assert.StartsWith("fare-", fare.LinkId, StringComparison.Ordinal);

            string places = File.ReadAllText(Path.Combine(root, "mm7-tables", "places.json"));
            Assert.Contains($"\"service-{shop.BuildingId}\"", places, StringComparison.Ordinal);
            Assert.Contains("\"residence-100\"", places, StringComparison.Ordinal);
            Assert.Contains("\"positionSource\": \"", places, StringComparison.Ordinal);

            string graph = File.ReadAllText(Path.Combine(root, "mm7-world", "place-graph.json"));
            Assert.Contains(fare.LinkId, graph, StringComparison.Ordinal);
            Assert.Contains("\"fare\": true", graph, StringComparison.Ordinal);

            string definitions = File.ReadAllText(Path.Combine(root, "mm7-tables", "services.json"));
            Assert.Contains("\"definitionKind\": \"service\"", definitions, StringComparison.Ordinal);
            Assert.Contains($"\"kind\": \"{shop.Kind}\"", definitions, StringComparison.Ordinal);
            Assert.Contains("\"source\": \"2DEvents.txt\"", definitions, StringComparison.Ordinal);

            // The passages a stable sells are written into its own definition, so the counter's offer and
            // the world's transition come from one emission.
            using JsonDocument document = JsonDocument.Parse(definitions);
            JsonElement stableEntry = document.RootElement.GetProperty("entries")
                .EnumerateArray()
                .Single(entry => entry.GetProperty("id").GetString() == stable.BuildingId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(1, stableEntry.GetProperty("fares").GetArrayLength());
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
            // two runs over one installation must agree on both: the definitions, the placements, and the
            // fares are compared as bytes rather than as shapes.
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
