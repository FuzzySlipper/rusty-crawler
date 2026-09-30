using PartyRpg.Kit.Content;
using Xunit;

namespace PartyRpg.Host.Tests;

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
