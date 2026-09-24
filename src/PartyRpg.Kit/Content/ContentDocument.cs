using System.Text.Json;

namespace PartyRpg.Kit.Content;

/// <summary>
/// One document of definitions, as data rather than as types the kit would have to understand.
/// </summary>
/// <remarks>
/// A document holds entries keyed by id. The kit validates identity, uniqueness, and references; what
/// the fields mean is the ruleset's business, so an entry keeps its payload as JSON and is read
/// through <see cref="Payload"/> by the ruleset that owns its meaning.
/// </remarks>
/// <param name="DocumentId">The document's stable id.</param>
/// <param name="DefinitionKind">What kind of thing the document holds.</param>
/// <param name="Entries">The entries, in the order the document declares them.</param>
public sealed class ContentDocument
{
    private ContentDocument(string documentId, string definitionKind, IReadOnlyList<ContentEntry> entries, JsonDocument source)
    {
        DocumentId = documentId;
        DefinitionKind = definitionKind;
        Entries = entries;
        Source = source;
    }

    /// <summary>The document's stable id.</summary>
    public string DocumentId { get; }

    /// <summary>What kind of thing the document holds.</summary>
    public string DefinitionKind { get; }

    /// <summary>The entries the document declares.</summary>
    public IReadOnlyList<ContentEntry> Entries { get; }

    /// <summary>The parsed document, kept alive because entries hold views into it.</summary>
    public JsonDocument Source { get; }

    /// <summary>Reads a document, failing with a named issue when it is not one.</summary>
    public static ContentDocument Read(string packId, string path, string json, List<ContentValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);
        JsonDocument parsed;
        try
        {
            parsed = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        }
        catch (JsonException error)
        {
            issues.Add(new ContentValidationIssue("document-not-json", $"'{path}' is not valid JSON: {error.Message}", packId));
            return new ContentDocument(path, string.Empty, [], JsonDocument.Parse("{}"));
        }

        JsonElement root = parsed.RootElement;
        string documentId = ReadString(root, "documentId");
        string definitionKind = ReadString(root, "definitionKind");
        if (documentId.Length == 0)
        {
            issues.Add(new ContentValidationIssue("document-id-missing", $"'{path}' declares no documentId.", packId, path));
        }

        if (definitionKind.Length == 0)
        {
            issues.Add(new ContentValidationIssue("definition-kind-missing", $"'{path}' declares no definitionKind.", packId, documentId));
        }

        List<ContentEntry> entries = [];
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("entries", out JsonElement entryList) &&
            entryList.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement entry in entryList.EnumerateArray())
            {
                string id = entry.ValueKind == JsonValueKind.Object ? ReadString(entry, "id") : string.Empty;
                if (id.Length == 0)
                {
                    issues.Add(new ContentValidationIssue("entry-id-missing", $"an entry in '{path}' declares no id.", packId, documentId));
                    continue;
                }

                entries.Add(new ContentEntry(id, entry));
            }
        }
        else
        {
            issues.Add(new ContentValidationIssue("entries-missing", $"'{path}' declares no 'entries' array.", packId, documentId));
        }

        return new ContentDocument(documentId, definitionKind, entries, parsed);
    }

    private static string ReadString(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(property, out JsonElement value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
}
