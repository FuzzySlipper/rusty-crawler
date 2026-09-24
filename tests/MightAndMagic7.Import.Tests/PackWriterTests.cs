using PartyRpg.Kit.Content;
using PartyRpg.Kit.World;
using MightAndMagic7.Import.Lod;
using MightAndMagic7.Import.Tool;
using Xunit;

namespace MightAndMagic7.Import.Tests;

/// <summary>
/// The contract between the importer and the product: packs the importer writes are packs the product
/// loads. The importer never references the kit and the kit never references the importer, so this is
/// the only place the two shapes meet, and it is the test that keeps them from drifting apart.
/// </summary>
public sealed class PackWriterTests
{
    private static readonly ContentLayout Layout = new("content-packs", "imports", "bundles");

    [Fact]
    public void A_written_pack_is_a_pack_the_product_loads()
    {
        string installRoot = SyntheticInstallation.Create();
        string root = Path.Combine(Path.GetTempPath(), $"mm7-content-{Guid.NewGuid():N}");
        try
        {
            string imports = Path.Combine(root, "imports");
            PackWriteResult written = PackWriter.Write(LodInstall.Open(installRoot), imports, PackWriter.MapDetail.None);

            Assert.Equal(["mm7-tables", "mm7-world"], written.PackIds);
            Assert.Contains("1.1", written.Provenance.BuildString);

            // A bundle that names the written packs is what the host would ship once the operator has
            // generated them; the loader must accept the packs, their provenance, and every reference.
            WriteBundle(root, written.PackIds);
            ContentBootstrapResult bootstrap = ContentBootstrap.Load(new FileContentSource(root), Layout, "imported");

            Assert.True(bootstrap.IsValid, string.Join("; ", bootstrap.Issues.Select(issue => issue.ToString())));
            Assert.Equal(2, bootstrap.Catalog.Packs.Count);
            ResolvedBundle selection = Assert.IsType<ResolvedBundle>(bootstrap.Selection);
            Assert.Equal(2, selection.Packs.Count);
            Assert.Equal(76, bootstrap.Catalog.Entries("place").Count());
            Assert.Equal(36, bootstrap.Catalog.Entries("class").Count());
            Assert.Equal(37, bootstrap.Catalog.Entries("skill").Count());
            Assert.Equal(99, bootstrap.Catalog.Entries("spell").Count());
            Assert.Equal(276, bootstrap.Catalog.Entries("monster").Count());
            Assert.Equal(800, bootstrap.Catalog.Entries("item").Count());
            Assert.Equal(512, bootstrap.Catalog.Entries("quest").Count());
            // Two of the fixture's three moves link maps; the third names the placeholder destination
            // and stays on its own map, so it is not a link.
            Assert.Equal(2, bootstrap.Catalog.Entries("travel-link").Count());
        }
        finally
        {
            Directory.Delete(installRoot, recursive: true);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Two_runs_over_the_same_installation_write_identical_bytes()
    {
        string installRoot = SyntheticInstallation.Create();
        string first = Path.Combine(Path.GetTempPath(), $"mm7-run-a-{Guid.NewGuid():N}");
        string second = Path.Combine(Path.GetTempPath(), $"mm7-run-b-{Guid.NewGuid():N}");
        try
        {
            LodInstall install = LodInstall.Open(installRoot);
            PackWriter.Write(install, first, PackWriter.MapDetail.None);
            PackWriter.Write(install, second, PackWriter.MapDetail.None);

            Assert.True(PackWriter.AreIdentical(first, second), "two runs over the same installation produced different bytes");
        }
        finally
        {
            Directory.Delete(installRoot, recursive: true);
            if (Directory.Exists(first)) Directory.Delete(first, recursive: true);
            if (Directory.Exists(second)) Directory.Delete(second, recursive: true);
        }
    }

    [Fact]
    public void An_imported_pack_records_the_game_and_the_build_it_came_from()
    {
        string installRoot = SyntheticInstallation.Create();
        string imports = Path.Combine(Path.GetTempPath(), $"mm7-prov-{Guid.NewGuid():N}");
        try
        {
            PackWriter.Write(LodInstall.Open(installRoot), imports, PackWriter.MapDetail.None);
            string manifest = File.ReadAllText(Path.Combine(imports, "mm7-tables", "pack.json"));

            Assert.Contains("\"game\": \"mightandmagic7\"", manifest);
            Assert.Contains("\"build\":", manifest);
            Assert.Contains("containers", manifest);
            Assert.Contains("\"producer\": \"mm7import\"", manifest);
        }
        finally
        {
            Directory.Delete(installRoot, recursive: true);
            if (Directory.Exists(imports)) Directory.Delete(imports, recursive: true);
        }
    }

    [Fact]
    public void Places_carry_their_kind_and_arrival_points_and_transitions_name_where_they_arrive()
    {
        string installRoot = SyntheticInstallation.Create(withMaps: true);
        string root = Path.Combine(Path.GetTempPath(), $"mm7-world-{Guid.NewGuid():N}");
        try
        {
            string imports = Path.Combine(root, "imports");
            PackWriter.Write(LodInstall.Open(installRoot), imports);

            WriteBundle(root, ["mm7-tables", "mm7-world"]);
            ContentBootstrapResult bootstrap = ContentBootstrap.Load(new FileContentSource(root), Layout, "imported");
            Assert.True(bootstrap.IsValid, string.Join("; ", bootstrap.Issues.Select(issue => issue.ToString())));

            PlaceGraph graph = PlaceGraphLoader.Load(bootstrap.Catalog);

            // Thirteen rows named the outdoor payload and sixty-three the indoor one, so the graph has
            // both kinds and the arrival points the maps actually declare.
            Assert.Equal(76, graph.Places.Count);
            Assert.Equal(13, graph.Places.Count(place => place.Kind == PlaceKind.Region));
            Assert.Equal(63, graph.Places.Count(place => place.Kind == PlaceKind.Interior));
            PlaceDefinition region = graph.Places.First(place => place.Kind == PlaceKind.Region);
            Assert.Contains(region.EntryPoints, point => point.Id == "Party Start");
            Assert.Contains(region.EntryPoints, point => point.Id == "North Start");

            // The fixture's one inter-map move carries no position, so it names the destination's start
            // point, and resolving it gives the pose that point declares.
            PlaceTransition transition = Assert.Single(graph.Transitions.Where(edge => !edge.IsWorldIssued && edge.Arrival.IsEntryPoint));
            PlacePose arrival = graph.ResolveArrival(transition);
            PlaceEntryPoint start = graph.Require(transition.To).FindEntryPoint("Party Start")!;
            Assert.Equal(start.Pose, arrival);
        }
        finally
        {
            Directory.Delete(installRoot, recursive: true);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static void WriteBundle(string root, IReadOnlyList<string> packIds)
    {
        string directory = Path.Combine(root, "bundles", "imported");
        Directory.CreateDirectory(directory);
        File.WriteAllText(
            Path.Combine(directory, "bundle.json"),
            $$"""
            {
              "schemaVersion": 1,
              "bundleId": "imported",
              "ruleset": "mightandmagic7",
              "contentPacks": [{{string.Join(", ", packIds.Select(id => $"\"{id}\""))}}],
              "description": "the packs an import wrote"
            }
            """);
    }
}
