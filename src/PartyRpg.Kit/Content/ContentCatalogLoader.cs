using System.Text.Json;

namespace PartyRpg.Kit.Content;

/// <summary>
/// Reads and validates every pack under a content root.
/// </summary>
/// <remarks>
/// Validation is deliberately whole-catalog rather than per-pack: two packs can each be individually
/// well formed and still disagree, by declaring the same entry id or by referring to an entry that
/// neither of them has. Both are failures a player would meet as a missing monster or a wrong item, so
/// they are caught here where the message can name both packs.
/// </remarks>
public static class ContentCatalogLoader
{
    /// <summary>The manifest file every pack directory carries.</summary>
    public const string ManifestFileName = "pack.json";

    /// <summary>Loads and validates every pack of a content root.</summary>
    public static ContentCatalog Load(IContentSource source, ContentLayout layout)
    {
        ArgumentNullException.ThrowIfNull(source);

        // A layout that names the same root twice would load every pack twice, and the second copy
        // would show up as duplicate identities all over the catalog rather than as the configuration
        // mistake it is.
        IReadOnlyList<string> roots = layout.PackRoots();
        if (roots.Distinct(StringComparer.Ordinal).Count() != roots.Count)
        {
            throw new ContentValidationException(
                $"The content layout names the same pack root twice ({string.Join(", ", roots)}).",
                [new ContentValidationIssue("layout-duplicate-root", "The content layout names the same pack root twice.", roots[0])]);
        }

        List<ContentValidationIssue> issues = [];
        List<LoadedPack> packs = [];
        Dictionary<string, string> entryOwners = new(StringComparer.Ordinal);
        Dictionary<string, string> documentOwners = new(StringComparer.Ordinal);
        List<(string PackId, PackDocumentEntry Document)> pendingReferences = [];

        foreach (string root in layout.PackRoots())
        {
            bool imported = string.Equals(root, layout.Imports, StringComparison.Ordinal);
            foreach (string directory in source.ListDirectories(root))
            {
                string packPath = $"{root}/{directory}";
                string manifestPath = $"{packPath}/{ManifestFileName}";
                if (!source.FileExists(manifestPath))
                {
                    issues.Add(new ContentValidationIssue(
                        "manifest-missing",
                        $"'{packPath}' has no {ManifestFileName}, so it is not a pack.",
                        directory));
                    continue;
                }

                PackManifest? manifest = ReadManifest(source, manifestPath, directory, imported, issues);
                if (manifest is null) continue;

                List<ContentDocument> documents = [];
                foreach (PackDocumentEntry declared in manifest.Documents)
                {
                    string documentPath = $"{packPath}/{declared.Path}";
                    if (!source.FileExists(documentPath))
                    {
                        issues.Add(new ContentValidationIssue(
                            "document-missing",
                            $"the manifest declares '{declared.Path}', which is not in the pack.",
                            manifest.PackId,
                            declared.DocumentId));
                        continue;
                    }

                    ContentDocument document = ContentDocument.Read(manifest.PackId, declared.Path, source.ReadText(documentPath), issues);
                    documents.Add(document);

                    if (document.DocumentId.Length > 0 &&
                        !documentOwners.TryAdd(document.DocumentId, manifest.PackId))
                    {
                        issues.Add(new ContentValidationIssue(
                            "document-id-reused",
                            $"document id '{document.DocumentId}' is already declared by pack '{documentOwners[document.DocumentId]}'.",
                            manifest.PackId,
                            document.DocumentId));
                    }

                    foreach (ContentEntry entry in document.Entries)
                    {
                        string key = $"{document.DefinitionKind}:{entry.Id}";
                        if (!entryOwners.TryAdd(key, manifest.PackId))
                        {
                            issues.Add(new ContentValidationIssue(
                                "entry-id-reused",
                                $"entry '{entry.Id}' of kind '{document.DefinitionKind}' is already declared by pack '{entryOwners[key]}'.",
                                manifest.PackId,
                                document.DocumentId));
                        }
                    }

                    foreach (string reference in declared.References ?? [])
                    {
                        pendingReferences.Add((manifest.PackId, new PackDocumentEntry(
                            declared.Path,
                            declared.DocumentId,
                            declared.DefinitionKind,
                            [reference])));
                    }
                }

                packs.Add(new LoadedPack(manifest, documents));
            }
        }

        foreach ((string packId, PackDocumentEntry document) in pendingReferences)
        {
            foreach (string reference in document.References ?? [])
            {
                if (entryOwners.ContainsKey(reference)) continue;
                issues.Add(new ContentValidationIssue(
                    "reference-unresolved",
                    $"'{document.DocumentId}' refers to '{reference}', which no pack declares.",
                    packId,
                    document.DocumentId));
            }
        }

        return ContentCatalog.From(packs, issues);
    }

    private static PackManifest? ReadManifest(
        IContentSource source,
        string manifestPath,
        string directory,
        bool imported,
        List<ContentValidationIssue> issues)
    {
        JsonDocument parsed;
        try
        {
            parsed = JsonDocument.Parse(source.ReadText(manifestPath), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        }
        catch (JsonException error)
        {
            issues.Add(new ContentValidationIssue("manifest-not-json", $"'{manifestPath}' is not valid JSON: {error.Message}", directory));
            return null;
        }

        JsonElement root = parsed.RootElement;
        int schemaVersion = root.TryGetProperty("schemaVersion", out JsonElement version) && version.TryGetInt32(out int parsedVersion)
            ? parsedVersion
            : 0;
        if (schemaVersion != PackManifest.CurrentSchemaVersion)
        {
            issues.Add(new ContentValidationIssue(
                "schema-version-unsupported",
                $"the manifest declares schema version {schemaVersion}; this build reads {PackManifest.CurrentSchemaVersion}.",
                directory));
            return null;
        }

        string packId = ReadString(root, "packId");
        if (packId.Length == 0)
        {
            issues.Add(new ContentValidationIssue("pack-id-missing", "the manifest declares no packId.", directory));
            return null;
        }

        if (!string.Equals(packId, directory, StringComparison.Ordinal))
        {
            issues.Add(new ContentValidationIssue(
                "pack-id-mismatch",
                $"the manifest says '{packId}' but the pack directory is '{directory}'.",
                packId));
        }

        string kindText = ReadString(root, "kind");
        if (!TryParseKind(kindText, out ContentPackKind kind))
        {
            issues.Add(new ContentValidationIssue(
                "pack-kind-unknown",
                $"the manifest declares kind '{kindText}', which is not one of definitions, tuning, scenario, or world.",
                packId));
            return null;
        }

        ContentProvenance provenance = ReadProvenance(root);
        if (provenance.Description.Length == 0)
        {
            issues.Add(new ContentValidationIssue(
                "provenance-missing",
                "the manifest declares no provenance description, so its origin cannot be recorded.",
                packId));
        }

        // An imported pack claims to come from a specific edition, so it has to say which one; a pack
        // that cannot say where it came from cannot be checked against the game it was taken from.
        if (imported && (provenance.Game is null || provenance.Build is null))
        {
            issues.Add(new ContentValidationIssue(
                "provenance-incomplete",
                "an imported pack must record the game and the build it was taken from.",
                packId));
        }

        List<PackDocumentEntry> documents = [];
        if (root.TryGetProperty("documents", out JsonElement declared) && declared.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in declared.EnumerateArray())
            {
                string path = ReadString(item, "path");
                string documentId = ReadString(item, "documentId");
                string definitionKind = ReadString(item, "definitionKind");
                if (path.Length == 0 || definitionKind.Length == 0)
                {
                    issues.Add(new ContentValidationIssue(
                        "document-declaration-incomplete",
                        "a declared document needs both a path and a definitionKind.",
                        packId,
                        documentId.Length > 0 ? documentId : null));
                    continue;
                }

                List<string> references = [];
                if (item.TryGetProperty("references", out JsonElement referenceList) && referenceList.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement reference in referenceList.EnumerateArray())
                    {
                        if (reference.ValueKind == JsonValueKind.String) references.Add(reference.GetString() ?? string.Empty);
                    }
                }

                documents.Add(new PackDocumentEntry(path, documentId, definitionKind, references));
            }
        }

        return new PackManifest(schemaVersion, packId, kind, provenance, documents);
    }

    private static ContentProvenance ReadProvenance(JsonElement root)
    {
        if (!root.TryGetProperty("provenance", out JsonElement provenance) || provenance.ValueKind != JsonValueKind.Object)
        {
            return new ContentProvenance(string.Empty);
        }

        return new ContentProvenance(
            ReadString(provenance, "description"),
            ReadOptionalString(provenance, "game"),
            ReadOptionalString(provenance, "build"),
            ReadOptionalString(provenance, "producer"));
    }

    private static bool TryParseKind(string text, out ContentPackKind kind)
    {
        switch (text)
        {
            case "definitions":
                kind = ContentPackKind.Definitions;
                return true;
            case "tuning":
                kind = ContentPackKind.Tuning;
                return true;
            case "scenario":
                kind = ContentPackKind.Scenario;
                return true;
            case "world":
                kind = ContentPackKind.World;
                return true;
            default:
                kind = ContentPackKind.Definitions;
                return false;
        }
    }

    private static string ReadString(JsonElement element, string property) => ReadOptionalString(element, property) ?? string.Empty;

    private static string? ReadOptionalString(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(property, out JsonElement value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
