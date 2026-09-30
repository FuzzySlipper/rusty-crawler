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
    /// The default bundle this repository ships names no packs — the packs a product plays are the operator's
    /// imports, and none of them is committed — so resolving it alone proves only that one small file parses. What
    /// this case proves instead is everything the checkout's own tree carries: every bundle file it ships loads and
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
            Assert.NotNull(result.Selection);
            Assert.Equal(bundleId, result.Selection.Bundle.BundleId);
            Assert.Equal(result.Selection.Bundle.ContentPacks, result.Selection.Packs.Select(pack => pack.PackId));
            if (bundleId == BuiltInBundles.Default) defaultPacks = result.Selection.Packs.Count;
        }

        // The product over the shipped tree, staged the way the engine stages the content root.
        (string Path, string Text)[] staged = [.. Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .Select(file => (Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/'), File.ReadAllText(file)))];
        (Rusty.Engine.ProductCreateContext context, _) = ProductTestContext.Create(staged);
        using CrawlerProduct product = new(context, ProductTestContext.NoVariables);

        Assert.Equal(BuiltInBundles.Default, product.Selection.BundleId);
        Assert.NotNull(defaultPacks);
        Assert.Equal(defaultPacks.Value, product.Selection.PackCount);
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
