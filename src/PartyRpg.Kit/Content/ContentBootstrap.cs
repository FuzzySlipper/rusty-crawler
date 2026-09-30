using PartyRpg.Kit.Rulesets;

namespace PartyRpg.Kit.Content;

/// <summary>What a product's content root yielded: the content its selection reads, its bundles, and that selection.</summary>
/// <param name="Catalog">
/// The packs a session may read, which are exactly the packs the selected bundle named. A bundle that names
/// no packs, and a product that selected no bundle, leave this empty: what a bundle did not name is not
/// loaded, so it contributes no definitions, no placements, and no scenario start however much of it is
/// sitting under the content root.
/// </param>
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
/// <para>
/// Two failures are kept apart on purpose. Content that is present but wrong is a hard failure: the
/// player would meet it as a missing monster, and a product that starts anyway hides the defect until
/// then. Content that is simply absent is not a failure: a checkout whose packs have not been
/// generated yet still starts, with no bundle selected and the reason recorded, rather than refusing
/// to run.
/// </para>
/// <para>
/// <b>The bundle is resolved here, and so is what it means.</b> The catalog this returns holds the packs
/// the bundle named and no others, so an unselected pack is not loaded at all: the world's places, the
/// scenario's start, and the scenario's party are read from the selection, and a checkout holding packs
/// nobody selected plays exactly as an empty one does. The whole root is still read and judged, which is
/// what keeps a broken pack on disk a failure worth naming rather than a defect the selection hides.
/// </para>
/// </remarks>
public static class ContentBootstrap
{
    /// <summary>Loads a content root and resolves the requested bundle.</summary>
    /// <param name="source">Where the content root is read from.</param>
    /// <param name="layout">Where things live inside it.</param>
    /// <param name="requestedBundleId">The bundle the product asks for, or null for none.</param>
    /// <param name="compiledRuleset">
    /// The ruleset the product was compiled with, which the requested bundle must be assembled for. Without one
    /// the bundle's ruleset is not compared.
    /// </param>
    public static ContentBootstrapResult Load(
        IContentSource source,
        ContentLayout layout,
        string? requestedBundleId,
        RulesetId? compiledRuleset = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ContentCatalog catalog = ContentCatalogLoader.Load(source, layout);
        BundleCatalog bundles = BundleCatalog.Load(source, layout);
        List<ContentValidationIssue> issues = [.. catalog.Issues, .. bundles.Issues];

        if (requestedBundleId is null or { Length: 0 })
        {
            return new ContentBootstrapResult(catalog.Selected([]), bundles, null, issues);
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

            return new ContentBootstrapResult(catalog.Selected([]), bundles, null, issues);
        }

        // A bundle names the ruleset it was assembled for, and the host plays the one it was compiled with: a
        // bundle for another ruleset would hand this one content it cannot read, so the two must agree.
        if (compiledRuleset is { } ruleset && !string.Equals(bundle.Ruleset, ruleset.Value, StringComparison.Ordinal))
        {
            issues.Add(new ContentValidationIssue(
                "bundle-ruleset-mismatch",
                $"bundle '{bundle.BundleId}' is assembled for ruleset '{bundle.Ruleset}', and the product plays '{ruleset.Value}'.",
                bundle.BundleId));
            return new ContentBootstrapResult(catalog.Selected([]), bundles, null, issues);
        }

        ResolvedBundle selection;
        try
        {
            selection = BundleCatalog.Resolve(bundle, catalog);
        }
        catch (ContentValidationException error)
        {
            issues.AddRange(error.Issues);
            return new ContentBootstrapResult(catalog.Selected([]), bundles, null, issues);
        }

        return new ContentBootstrapResult(catalog.Selected(selection.Packs), bundles, selection, issues);
    }
}
