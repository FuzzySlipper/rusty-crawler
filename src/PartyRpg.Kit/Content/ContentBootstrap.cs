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

    /// <summary>
    /// The requested bundle when the only thing wrong with it is that packs it names are absent, or null. Such a
    /// bundle selects nothing, and the content root is otherwise valid.
    /// </summary>
    public MissingContent? Missing { get; init; }
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
/// what keeps a broken pack on disk a failure worth naming rather than a defect the selection hides; an
/// issue in a pack the selection does not load carries <see cref="ContentValidationIssue.NotSelected"/>,
/// which says so and names the directory it was read from.
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

        if (requestedBundleId is null or { Length: 0 })
        {
            return Unselected(source, layout, catalog, bundles, [], "the product selected no bundle");
        }

        GameBundle? bundle = bundles.Find(requestedBundleId);
        if (bundle is null)
        {
            // Nothing to select from is a content root that has not been populated yet; a content root
            // that has bundles but not the requested one is a packaging mistake worth stopping for.
            List<ContentValidationIssue> missing = [];
            if (bundles.Bundles.Count > 0)
            {
                missing.Add(new ContentValidationIssue(
                    "requested-bundle-missing",
                    $"the product asks for bundle '{requestedBundleId}', which is not present; the content root has {string.Join(", ", bundles.Bundles.Select(entry => entry.BundleId))}.",
                    requestedBundleId));
            }

            return Unselected(source, layout, catalog, bundles, missing, $"bundle '{requestedBundleId}' is not present, so nothing is selected");
        }

        // A bundle names the ruleset it was assembled for, and the host plays the one it was compiled with: a
        // bundle for another ruleset would hand this one content it cannot read, so the two must agree.
        if (compiledRuleset is { } ruleset && !string.Equals(bundle.Ruleset, ruleset.Value, StringComparison.Ordinal))
        {
            return Unselected(
                source,
                layout,
                catalog,
                bundles,
                [new ContentValidationIssue(
                    "bundle-ruleset-mismatch",
                    $"bundle '{bundle.BundleId}' is assembled for ruleset '{bundle.Ruleset}', and the product plays '{ruleset.Value}'.",
                    bundle.BundleId)],
                $"bundle '{bundle.BundleId}' cannot be played, so nothing is selected");
        }

        ResolvedBundle selection;
        try
        {
            selection = BundleCatalog.Resolve(bundle, catalog);
        }
        catch (ContentValidationException error)
        {
            // A bundle over packs generated from the operator's own data names packs a checkout cannot ship. When
            // their absence is all that is wrong, nothing is selected and the absence is the result's own fact,
            // so the product can show the bundle's setup guidance rather than either refusing to start or presenting
            // an empty session as the game. Any other defect in the bundle stays a refusal.
            if (error.Issues.All(issue => issue.Code == BundleCatalog.PackMissingCode))
            {
                IReadOnlyList<string> absent = [.. bundle.ContentPacks.Where(pack => catalog.Find(pack) is null)];
                return Unselected(source, layout, catalog, bundles, [], $"bundle '{bundle.BundleId}' names packs that are not present, so nothing is selected")
                    with { Missing = new MissingContent(bundle, absent) };
            }

            return Unselected(source, layout, catalog, bundles, error.Issues, $"bundle '{bundle.BundleId}' did not resolve, so nothing is selected");
        }

        // The tuning pack the bundle names is selected beside its content packs, so the session reads its values
        // from the same catalog it reads everything else from.
        IReadOnlyList<LoadedPack> selected = selection.TuningPack is { } tuning ? [.. selection.Packs, tuning] : selection.Packs;
        List<ContentValidationIssue> issues =
        [
            .. Judged(source, layout, catalog, selected, $"bundle '{bundle.BundleId}' does not name it"),
            .. bundles.Issues,
        ];
        return new ContentBootstrapResult(catalog.Selected(selected), bundles, selection, issues);
    }

    /// <summary>A result that selects nothing, carrying every issue the root, the bundles, and the selection raised.</summary>
    private static ContentBootstrapResult Unselected(
        IContentSource source,
        ContentLayout layout,
        ContentCatalog catalog,
        BundleCatalog bundles,
        IReadOnlyList<ContentValidationIssue> selectionIssues,
        string reason) =>
        new(catalog.Selected([]), bundles, null, [.. Judged(source, layout, catalog, [], reason), .. bundles.Issues, .. selectionIssues]);

    /// <summary>
    /// The root's issues, each one in a pack the selection does not load marked as not selected, with the
    /// directory it was read from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why an unselected pack is judged at all.</b> The whole root is what the operator staged, and the
    /// loader reads identity across every pack in it — a pack id, a document id, and an entry id are the
    /// root's, not one pack's — so a pack that is present and wrong is a contradiction in the content the
    /// product holds whether or not this bundle names it. Refusing the start costs the operator a restart;
    /// letting it through keeps a defect that surfaces later, under some other bundle, as a silently wrong
    /// artifact.
    /// </para>
    /// <para>
    /// <b>Why the refusal says so.</b> A start refused by a pack nobody selected must not read as a defect in
    /// the game the operator chose: the issue states that the pack is not selected, why, and the directory it
    /// came from, so the fix — repair the pack, or move it out of the root — is in the message.
    /// </para>
    /// </remarks>
    private static IEnumerable<ContentValidationIssue> Judged(
        IContentSource source,
        ContentLayout layout,
        ContentCatalog catalog,
        IReadOnlyList<LoadedPack> selected,
        string reason)
    {
        HashSet<string> loaded = [.. selected.Select(pack => pack.PackId)];
        Dictionary<string, string> readFrom = new(StringComparer.Ordinal);
        foreach (string root in layout.PackRoots())
        {
            foreach (string directory in source.ListDirectories(root)) readFrom.TryAdd(directory, $"{root}/{directory}");
        }

        foreach (ContentValidationIssue issue in catalog.Issues)
        {
            // Every issue the loader raises names the pack directory it read (a pack's id is its directory), so
            // an issue whose pack is a directory the selection does not load is that pack's, and is marked.
            yield return !loaded.Contains(issue.PackId) && readFrom.TryGetValue(issue.PackId, out string? path)
                ? issue with { NotSelected = $"pack '{issue.PackId}' is not selected: {reason}; it was read from '{path}' because every pack under the content root is judged at start" }
                : issue;
        }
    }
}
