using PartyRpg.Kit.Content;
using Xunit;

namespace PartyRpg.Host.Tests;

/// <summary>
/// The content this repository ships must be valid content: the bundles and packs a checkout carries
/// are what a product starts from, and a broken one is a product that refuses to run.
/// </summary>
public sealed class ShippedContentTests
{
    [Fact]
    public void The_bundles_this_repository_ships_validate_and_the_default_resolves()
    {
        string root = RepositoryContentRoot();
        ContentLayout layout = ContentLayout.Under(ContentDirectory());

        ContentBootstrapResult result = ContentBootstrap.Load(new FileContentSource(root), layout, BuiltInBundles.Default);

        Assert.True(result.IsValid, result.IsValid ? string.Empty : string.Join("; ", result.Issues.Select(issue => issue.ToString())));
        GameBundle bundle = Assert.Single(result.Bundles.Bundles);
        Assert.Equal(BuiltInBundles.Default, bundle.BundleId);
        Assert.Equal("mightandmagic7", bundle.Ruleset);
        Assert.NotNull(result.Selection);
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
