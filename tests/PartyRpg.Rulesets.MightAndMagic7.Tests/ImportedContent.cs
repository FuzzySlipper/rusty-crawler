using PartyRpg.Kit.Content;
using Xunit;

namespace PartyRpg.Rulesets.MightAndMagic7.Tests;

/// <summary>
/// The packs the importer wrote from the operator's own game data, when a run was pointed at them.
/// </summary>
/// <remarks>
/// The operator's data is not part of the repository, so a case that checks this game's policy against the
/// shipped tables has nothing to check on a machine without it. Such a case reports itself skipped with the
/// reason rather than returning early and counting as a pass. It reads a content root of its own, written by
/// <c>mm7import write</c> for the run (<c>scripts/verify.sh</c> does this when the install is present), and
/// never the working tree's <c>content/</c>, whose imports hold whatever packs a live check last staged.
/// </remarks>
internal static class ImportedContent
{
    /// <summary>The variable naming a content root whose <c>partyrpg/imports</c> the importer wrote.</summary>
    internal const string Variable = "CRAWLER_IMPORTED_CONTENT";

    private static readonly ContentLayout Layout = new("partyrpg/content-packs", "partyrpg/imports", "partyrpg/bundles");

    /// <summary>The content root the run was pointed at, or null when it was pointed at none.</summary>
    internal static string? Root
    {
        get
        {
            string? root = Environment.GetEnvironmentVariable(Variable);
            return string.IsNullOrWhiteSpace(root) ? null : Path.GetFullPath(root);
        }
    }

    /// <summary>The path of one document of the imported rule tables.</summary>
    /// <param name="document">The document's file name inside the tables pack.</param>
    internal static string Table(string document) =>
        Path.Combine(Root ?? throw new InvalidOperationException($"{Variable} is not set."), "partyrpg", "imports", "mm7-tables", document);

    /// <summary>Loads and validates every pack under the imported root.</summary>
    internal static ContentCatalog Load() =>
        ContentCatalogLoader.Load(
            new FileContentSource(Root ?? throw new InvalidOperationException($"{Variable} is not set.")),
            Layout);

    /// <summary>
    /// The packs the import itself wrote, selected the way a bundle selects them: the ones the import's own
    /// bundle fragment names, and nothing else under the root.
    /// </summary>
    /// <remarks>
    /// A root an operator has used for live checks also holds the scenario packs they staged there, and a
    /// session composed over the whole root would read every one of their starts — a selection no bundle
    /// makes, refused as ambiguous. The fragment is the import's own statement of what it wrote, so a case
    /// that composes a session over it composes what a bundle naming the import would play.
    /// </remarks>
    /// <param name="catalog">The whole imported root, as <see cref="Load"/> read it.</param>
    internal static ContentCatalog Written(ContentCatalog catalog)
    {
        string fragment = Path.Combine(Root ?? throw new InvalidOperationException($"{Variable} is not set."), "partyrpg", "imports", "imported-bundle.json");
        using System.Text.Json.JsonDocument bundle = System.Text.Json.JsonDocument.Parse(File.ReadAllText(fragment));
        List<LoadedPack> written = [];
        foreach (System.Text.Json.JsonElement packId in bundle.RootElement.GetProperty("contentPacks").EnumerateArray())
        {
            string id = packId.GetString() ?? string.Empty;
            written.Add(catalog.Find(id) ?? throw new InvalidOperationException($"{fragment} names pack '{id}', which the imported root does not hold."));
        }

        return catalog.Selected(written);
    }

    /// <summary>Why a case that reads the given table cannot run here, or null when it can.</summary>
    /// <param name="document">The document's file name inside the tables pack.</param>
    internal static string? Unavailable(string document)
    {
        if (Root is null)
        {
            return $"{Variable} names no imported content root; this case checks the operator's own game data.";
        }

        return File.Exists(Table(document))
            ? null
            : $"{Table(document)} is absent from the imported content root {Root}.";
    }
}

/// <summary>A case that checks this game's policy against the operator's imported tables, skipped without them.</summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class ImportedFactAttribute : FactAttribute
{
    /// <summary>Declares the table the case reads.</summary>
    /// <param name="document">The document's file name inside the tables pack.</param>
    public ImportedFactAttribute(string document)
    {
        Skip = ImportedContent.Unavailable(document);
    }
}
