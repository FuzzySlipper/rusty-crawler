using MightAndMagic7.Import.Lod;
using MightAndMagic7.Import.Tool;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using PartyRpg.Rulesets.MightAndMagic7;
using Rusty.Engine;
using Xunit;

namespace MightAndMagic7.Import.Tests;

/// <summary>
/// What the importer writes is what this game's ruleset reads: the packs written from the synthetic
/// installation load, select, and compose a session with no content defect.
/// </summary>
/// <remarks>
/// The writer names its fields and the ruleset reads them by the same names in another project, so this is
/// the one place a renamed field fails: the ruleset refuses a monster row whose typed combat fields it cannot
/// read, a spell it cannot price, a service it cannot serve, by name, and a session that composes is one whose
/// every table was read.
/// </remarks>
public sealed class PipelineTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_packs_the_importer_writes_compose_this_games_session_with_no_content_defect(bool allDayService)
    {
        string installRoot = SyntheticInstallation.Create(withMaps: true, withContainers: true, withServices: true, allDayService: allDayService);
        string root = Path.Combine(Path.GetTempPath(), $"mm7-pipeline-{Guid.NewGuid():N}");
        try
        {
            PackWriteResult written = PackWriter.Write(LodInstall.Open(installRoot), Path.Combine(root, "partyrpg", "imports"));
            string bundle = Path.Combine(root, "partyrpg", "bundles", "imported", "bundle.json");
            Directory.CreateDirectory(Path.GetDirectoryName(bundle)!);
            File.WriteAllText(bundle, $$"""
                {
                  "schemaVersion": 1,
                  "bundleId": "imported",
                  "ruleset": "{{MightAndMagic7Ruleset.Instance.Id.Value}}",
                  "contentPacks": [ {{string.Join(", ", written.PackIds.Select(id => $"\"{id}\""))}} ],
                  "description": "every pack the importer wrote"
                }
                """);

            ContentBootstrapResult bootstrap = ContentBootstrap.Load(
                new FileContentSource(root),
                ContentLayout.Under("partyrpg"),
                "imported",
                MightAndMagic7Ruleset.Instance.Id);
            Assert.True(bootstrap.IsValid, string.Join("; ", bootstrap.Issues.Select(issue => issue.ToString())));
            Assert.NotNull(bootstrap.Selection);

            using NullChannel channel = new();
            using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(new RulesetSessionContext(
                channel,
                new BundleSelection(bootstrap.Selection!.Bundle.BundleId, bootstrap.Selection.Packs.Count),
                bootstrap.Catalog));
            session.Start();
            session.Update(new ProductUpdate(
                new ProductUpdateFacts(ProductUpdateMode.Realtime, ProductLifecycleState.Running, 1, 1, 0, 0, 60, 1, 0, 1.0 / 60.0),
                ReadOnlySpan<ProductInputEvent>.Empty));
            Assert.True(channel.Published > 0);
        }
        finally
        {
            Directory.Delete(installRoot, recursive: true);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>A projection channel that counts what it is handed.</summary>
    private sealed class NullChannel : IUiProjectionChannel
    {
        internal int Published { get; private set; }

        public void Publish(UiValue value) => Published++;

        public void Dispose()
        {
        }
    }
}
