using System.Text.Json;

namespace PartyRpg.Kit.Content;

/// <summary>A bundle together with the content it resolved to.</summary>
/// <param name="Bundle">The bundle as declared.</param>
/// <param name="Packs">The packs it includes, in the order it lists them.</param>
/// <param name="TuningPack">The tuning pack it includes, when it has one.</param>
public sealed record ResolvedBundle(GameBundle Bundle, IReadOnlyList<LoadedPack> Packs, LoadedPack? TuningPack);

/// <summary>
/// Every bundle under a content root, each one checked against the catalog it names.
/// </summary>
/// <remarks>
/// A bundle that names a pack which is not there is a broken product, not a degraded one: the player
/// would meet it as missing monsters halfway through an evening. Resolution therefore fails with every
/// missing piece named at once, so one run fixes the whole bundle.
/// </remarks>
public sealed class BundleCatalog
{
    private BundleCatalog(IReadOnlyList<GameBundle> bundles, IReadOnlyList<ContentValidationIssue> issues)
    {
        Bundles = bundles;
        Issues = issues;
    }

    /// <summary>Every bundle that loaded, ordered by id.</summary>
    public IReadOnlyList<GameBundle> Bundles { get; }

    /// <summary>Everything wrong with the bundles, empty when they are valid.</summary>
    public IReadOnlyList<ContentValidationIssue> Issues { get; }

    /// <summary>Whether every bundle is valid.</summary>
    public bool IsValid => Issues.Count == 0;

    /// <summary>Finds a bundle by id.</summary>
    public GameBundle? Find(string bundleId) =>
        Bundles.FirstOrDefault(bundle => string.Equals(bundle.BundleId, bundleId, StringComparison.Ordinal));

    /// <summary>Loads every bundle of a content root.</summary>
    public static BundleCatalog Load(IContentSource source, ContentLayout layout)
    {
        ArgumentNullException.ThrowIfNull(source);
        List<GameBundle> bundles = [];
        List<ContentValidationIssue> issues = [];
        foreach (string directory in source.ListDirectories(layout.Bundles))
        {
            string path = $"{layout.Bundles}/{directory}/{GameBundle.FileName}";
            if (!source.FileExists(path))
            {
                issues.Add(new ContentValidationIssue("bundle-missing", $"'{layout.Bundles}/{directory}' has no {GameBundle.FileName}.", directory));
                continue;
            }

            GameBundle? bundle = Read(source, path, directory, issues);
            if (bundle is not null) bundles.Add(bundle);
        }

        return new BundleCatalog([.. bundles.OrderBy(bundle => bundle.BundleId, StringComparer.Ordinal)], issues);
    }

    /// <summary>
    /// Resolves a bundle against a catalog, failing with every missing pack named.
    /// </summary>
    public static ResolvedBundle Resolve(GameBundle bundle, ContentCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(catalog);
        List<ContentValidationIssue> issues = [];
        List<LoadedPack> packs = [];
        foreach (string packId in bundle.ContentPacks)
        {
            LoadedPack? pack = catalog.Find(packId);
            if (pack is null)
            {
                issues.Add(new ContentValidationIssue(
                    "bundle-pack-missing",
                    $"bundle '{bundle.BundleId}' includes pack '{packId}', which is not present.",
                    bundle.BundleId));
                continue;
            }

            packs.Add(pack);
        }

        LoadedPack? tuning = null;
        if (bundle.TuningPack is { Length: > 0 } tuningId)
        {
            tuning = catalog.Find(tuningId);
            if (tuning is null)
            {
                issues.Add(new ContentValidationIssue(
                    "bundle-tuning-missing",
                    $"bundle '{bundle.BundleId}' includes tuning pack '{tuningId}', which is not present.",
                    bundle.BundleId));
            }
            else if (tuning.Manifest.Kind != ContentPackKind.Tuning)
            {
                issues.Add(new ContentValidationIssue(
                    "bundle-tuning-wrong-kind",
                    $"bundle '{bundle.BundleId}' names '{tuningId}' as tuning, but that pack is {tuning.Manifest.Kind}.",
                    bundle.BundleId));
            }
        }

        if (issues.Count > 0)
        {
            throw new ContentValidationException(
                $"bundle '{bundle.BundleId}' cannot be loaded: {issues[0].Message}",
                issues);
        }

        return new ResolvedBundle(bundle, packs, tuning);
    }

    private static GameBundle? Read(IContentSource source, string path, string directory, List<ContentValidationIssue> issues)
    {
        JsonDocument parsed;
        try
        {
            parsed = JsonDocument.Parse(source.ReadText(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        }
        catch (JsonException error)
        {
            issues.Add(new ContentValidationIssue("bundle-not-json", $"'{path}' is not valid JSON: {error.Message}", directory));
            return null;
        }

        JsonElement root = parsed.RootElement;
        int schemaVersion = root.TryGetProperty("schemaVersion", out JsonElement version) && version.TryGetInt32(out int parsedVersion)
            ? parsedVersion
            : 0;
        if (schemaVersion != GameBundle.CurrentSchemaVersion)
        {
            issues.Add(new ContentValidationIssue(
                "schema-version-unsupported",
                $"'{path}' declares schema version {schemaVersion}; this build reads {GameBundle.CurrentSchemaVersion}.",
                directory));
            return null;
        }

        string bundleId = Read(root, "bundleId");
        string ruleset = Read(root, "ruleset");
        if (bundleId.Length == 0 || ruleset.Length == 0)
        {
            issues.Add(new ContentValidationIssue("bundle-incomplete", $"'{path}' needs both a bundleId and a ruleset.", directory));
            return null;
        }

        if (!string.Equals(bundleId, directory, StringComparison.Ordinal))
        {
            issues.Add(new ContentValidationIssue(
                "bundle-id-mismatch",
                $"the bundle says '{bundleId}' but its directory is '{directory}'.",
                bundleId));
        }

        List<string> packs = [];
        if (root.TryGetProperty("contentPacks", out JsonElement declared) && declared.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in declared.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String) packs.Add(item.GetString() ?? string.Empty);
            }
        }

        string tuning = Read(root, "tuningPack");
        return new GameBundle(
            schemaVersion,
            bundleId,
            ruleset,
            packs,
            tuning.Length == 0 ? null : tuning,
            Read(root, "description"));
    }

    private static string Read(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(property, out JsonElement value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
}
