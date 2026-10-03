using PartyRpg.Kit.Content;
using Xunit;

namespace PartyRpg.Host.Tests;

/// <summary>
/// The content this repository ships must be valid content: the bundles and packs a checkout carries
/// are what a product starts from, and a broken one is a product that refuses to run.
/// </summary>
public sealed class ShippedContentTests
{
    /// <remarks>
    /// The default bundle this repository ships names the operator's imported packs, which no checkout carries, so it
    /// resolves only where an operator has written them; elsewhere it must be the missing-content state naming exactly
    /// those packs. What this case proves besides is everything the checkout's own tree carries: every bundle file it ships loads and
    /// validates, the bundles are exactly the ones the host will select, each resolves to exactly the packs its own
    /// file names, every pack directory under the shipped content root loads (none today, so that part binds the
    /// first pack anyone commits rather than passing on one now), and the product itself starts from that tree as the
    /// engine stages it, selecting the default bundle.
    /// </remarks>
    [Fact]
    public void Every_shipped_bundle_and_pack_validates_each_bundle_resolves_to_the_packs_it_names_and_the_product_starts_from_the_default()
    {
        string root = RepositoryContentRoot();
        ContentLayout layout = ContentLayout.Under(ContentDirectory());
        FileContentSource source = new(root);

        ContentCatalog packs = ContentCatalogLoader.Load(source, layout);
        Assert.True(packs.IsValid, string.Join("; ", packs.Issues.Select(issue => issue.ToString())));
        // Every root the loader reads packs from, which includes the importer's output: an operator's own imported
        // packs lie there, ignored by the repository, and the loader reads them exactly as it reads authored ones.
        string[] packDirectories = [.. layout.PackRoots()
            .Select(packRoot => Path.Combine(root, packRoot))
            .Where(Directory.Exists)
            .SelectMany(Directory.GetDirectories)
            .Where(directory => File.Exists(Path.Combine(directory, "pack.json")))
            .Select(directory => Path.GetFileName(directory)!)
            .Order(StringComparer.Ordinal)];
        Assert.Equal(packDirectories, packs.Packs.Select(pack => pack.PackId).Order(StringComparer.Ordinal));

        string[] bundleFiles = [.. Directory.GetDirectories(Path.Combine(root, layout.Bundles))
            .Where(directory => File.Exists(Path.Combine(directory, GameBundle.FileName)))
            .Select(directory => Path.GetFileName(directory)!)
            .Order(StringComparer.Ordinal)];
        Assert.Equal(BuiltInBundles.All.Order(StringComparer.Ordinal), bundleFiles);

        int? defaultPacks = null;
        foreach (string bundleId in BuiltInBundles.All)
        {
            ContentBootstrapResult result = ContentBootstrap.Load(source, layout, bundleId, BuiltInRulesets.Default.Id);

            Assert.True(result.IsValid, string.Join("; ", result.Issues.Select(issue => issue.ToString())));
            if (result.Missing is { } missing)
            {
                // Only packs the importer writes may be absent: an authored pack the bundle names ships with it.
                Assert.All(missing.Packs, pack => Assert.False(Directory.Exists(Path.Combine(root, layout.ContentPacks, pack)), pack));
                Assert.NotEqual(string.Empty, missing.Bundle.Setup);
                if (bundleId == BuiltInBundles.Default) defaultPacks = 0;
                continue;
            }

            Assert.NotNull(result.Selection);
            Assert.Equal(bundleId, result.Selection.Bundle.BundleId);
            Assert.Equal(result.Selection.Bundle.ContentPacks, result.Selection.Packs.Select(pack => pack.PackId));
            if (bundleId == BuiltInBundles.Default) defaultPacks = result.Selection.Packs.Count;
        }

        // The product over the shipped tree, staged the way the engine stages the content root.
        (string Path, string Text)[] staged = [.. Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .Select(file => (Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/'), File.ReadAllText(file)))];
        // The engine's content service is the one the creation screen's faces are opened through when the imported
        // media is present.
        (Rusty.Engine.IContentService faces, _) = RecordingEngineService<Rusty.Engine.IContentService>.Create();
        (Rusty.Engine.ProductCreateContext context, _) = ProductTestContext.Create(persistence: null, spatial: null, faces, staged);
        using CrawlerProduct product = new(context, ProductTestContext.NoVariables);

        Assert.NotNull(defaultPacks);
        if (defaultPacks == 0)
        {
            Assert.Equal(BuiltInBundles.Default, product.Selection.Unavailable);
        }
        else
        {
            Assert.Equal(BuiltInBundles.Default, product.Selection.BundleId);
            Assert.Equal(defaultPacks.Value, product.Selection.PackCount);
        }
    }

    /// <summary>
    /// The new game names every pack the importer writes, so an operator who ran <c>write</c> plays all of it: when
    /// an import's own pack list is present, the shipped default must name each pack in it.
    /// </summary>
    [Fact]
    public void The_new_game_names_every_pack_the_importer_last_wrote()
    {
        string root = RepositoryContentRoot();
        ContentLayout layout = ContentLayout.Under(ContentDirectory());
        string fragment = Path.Combine(root, ContentDirectory(), "imports", "imported-bundle.json");
        if (!File.Exists(fragment)) return;

        using System.Text.Json.JsonDocument written = System.Text.Json.JsonDocument.Parse(File.ReadAllText(fragment));
        string[] imported = [.. written.RootElement.GetProperty("contentPacks").EnumerateArray().Select(pack => pack.GetString()!)];
        GameBundle shipped = BundleCatalog.Load(new FileContentSource(root), layout).Find(BuiltInBundles.NewGame)!;
        Assert.All(imported, pack => Assert.Contains(pack, shipped.ContentPacks));
    }

    [Fact]
    public void The_content_layout_matches_where_the_shipped_content_actually_is()
    {
        string root = RepositoryContentRoot();
        ContentLayout layout = ContentLayout.Under(ContentDirectory());

        Assert.True(Directory.Exists(Path.Combine(root, layout.Bundles)), $"'{layout.Bundles}' does not exist under '{root}'.");
        Assert.True(Directory.Exists(Path.Combine(root, layout.ContentPacks)), $"'{layout.ContentPacks}' does not exist under '{root}'.");
    }

    /// <summary>
    /// The product's content directory, read from the host rather than restated, so a layout the test
    /// happens to like cannot pass while the product looks somewhere else.
    /// </summary>
    private static string ContentDirectory() => ProductIdentity.ContentDirectory;

    /// <summary>The repository's content root, found by walking up from the test binary.</summary>
    private static string RepositoryContentRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "content", "partyrpg", "bundles");
            if (Directory.Exists(candidate)) return Path.Combine(directory.FullName, "content");
            directory = directory.Parent;
        }

        throw new InvalidOperationException($"No 'content/partyrpg/bundles' directory above '{AppContext.BaseDirectory}'.");
    }
}
