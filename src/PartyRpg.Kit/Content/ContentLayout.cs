namespace PartyRpg.Kit.Content;

/// <summary>
/// Where things live inside a content root, so the host, the loader, and the importer agree on layout
/// without repeating strings.
/// </summary>
/// <param name="ContentPacks">Authored content packs.</param>
/// <param name="Imports">Packs produced by the importer.</param>
/// <param name="Bundles">Game bundles that select a ruleset and a set of packs.</param>
public readonly record struct ContentLayout(string ContentPacks, string Imports, string Bundles)
{
    /// <summary>
    /// The layout under a content root, inside the product's own directory.
    /// </summary>
    /// <param name="productDirectory">The directory this product keeps its content in, relative to the content root.</param>
    public static ContentLayout Under(string productDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productDirectory);
        string prefix = productDirectory.Trim('/');
        return new ContentLayout($"{prefix}/content-packs", $"{prefix}/imports", $"{prefix}/bundles");
    }

    /// <summary>The locations a catalog reads packs from, in load order.</summary>
    public IReadOnlyList<string> PackRoots() => [ContentPacks, Imports];
}
