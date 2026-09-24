namespace PartyRpg.Kit.Content;

/// <summary>What a product's content root yielded: the packs it holds, its bundles, and its selection.</summary>
/// <param name="Catalog">The packs that loaded.</param>
/// <param name="Bundles">The bundles that loaded.</param>
/// <param name="Selection">The bundle and packs the product should start with.</param>
/// <param name="Issues">Everything wrong with the content, empty when it is valid.</param>
public sealed record ContentBootstrapResult(
    ContentCatalog Catalog,
    BundleCatalog Bundles,
    ResolvedBundle? Selection,
    IReadOnlyList<ContentValidationIssue> Issues)
{
    /// <summary>Whether the content is usable as it stands.</summary>
    public bool IsValid => Issues.Count == 0;
}

/// <summary>
/// Loads a product's content root and resolves the bundle it should start from.
/// </summary>
/// <remarks>
/// Two failures are kept apart on purpose. Content that is present but wrong is a hard failure: the
/// player would meet it as a missing monster, and a product that starts anyway hides the defect until
/// then. Content that is simply absent is not a failure: a checkout whose packs have not been
/// generated yet still starts, with no bundle selected and the reason recorded, rather than refusing
/// to run.
/// </remarks>
public static class ContentBootstrap
{
    /// <summary>Loads a content root and resolves the requested bundle.</summary>
    /// <param name="source">Where the content root is read from.</param>
    /// <param name="layout">Where things live inside it.</param>
    /// <param name="requestedBundleId">The bundle the product asks for, or null for none.</param>
    public static ContentBootstrapResult Load(IContentSource source, ContentLayout layout, string? requestedBundleId)
    {
        ArgumentNullException.ThrowIfNull(source);
        ContentCatalog catalog = ContentCatalogLoader.Load(source, layout);
        BundleCatalog bundles = BundleCatalog.Load(source, layout);
        List<ContentValidationIssue> issues = [.. catalog.Issues, .. bundles.Issues];

        if (requestedBundleId is null or { Length: 0 })
        {
            return new ContentBootstrapResult(catalog, bundles, null, issues);
        }

        GameBundle? bundle = bundles.Find(requestedBundleId);
        if (bundle is null)
        {
            // Nothing to select from is a content root that has not been populated yet; a content root
            // that has bundles but not the requested one is a packaging mistake worth stopping for.
            if (bundles.Bundles.Count > 0)
            {
                issues.Add(new ContentValidationIssue(
                    "requested-bundle-missing",
                    $"the product asks for bundle '{requestedBundleId}', which is not present; the content root has {string.Join(", ", bundles.Bundles.Select(entry => entry.BundleId))}.",
                    requestedBundleId));
            }

            return new ContentBootstrapResult(catalog, bundles, null, issues);
        }

        ResolvedBundle? selection = null;
        try
        {
            selection = BundleCatalog.Resolve(bundle, catalog);
        }
        catch (ContentValidationException error)
        {
            issues.AddRange(error.Issues);
        }

        return new ContentBootstrapResult(catalog, bundles, selection, issues);
    }
}
