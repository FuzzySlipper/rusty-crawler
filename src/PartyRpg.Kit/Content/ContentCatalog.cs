namespace PartyRpg.Kit.Content;

/// <summary>A pack's manifest together with the documents it declares.</summary>
/// <param name="Manifest">What the pack says it is.</param>
/// <param name="Documents">The documents the pack declares, in manifest order.</param>
public sealed record LoadedPack(PackManifest Manifest, IReadOnlyList<ContentDocument> Documents)
{
    /// <summary>The pack's id.</summary>
    public string PackId => Manifest.PackId;
}

/// <summary>
/// Every pack under a content root, validated as a whole.
/// </summary>
/// <remarks>
/// The catalog is loaded, not compiled: changing valid content or tuning must not require a rebuild.
/// It is validated when it is loaded, and validation is all-or-nothing at the point of use — a caller
/// that needs content calls <see cref="RequireValid"/> and gets every issue at once, while a caller
/// that is reporting on content can read <see cref="Issues"/> and carry on.
/// </remarks>
public sealed class ContentCatalog
{
    private ContentCatalog(IReadOnlyList<LoadedPack> packs, IReadOnlyList<ContentValidationIssue> issues)
    {
        Packs = packs;
        Issues = issues;
    }

    /// <summary>Every pack that loaded, in load order.</summary>
    public IReadOnlyList<LoadedPack> Packs { get; }

    /// <summary>Everything wrong with the content, empty when it is valid.</summary>
    public IReadOnlyList<ContentValidationIssue> Issues { get; }

    /// <summary>Whether the content is valid.</summary>
    public bool IsValid => Issues.Count == 0;

    /// <summary>Finds a pack by id.</summary>
    public LoadedPack? Find(string packId) =>
        Packs.FirstOrDefault(pack => string.Equals(pack.PackId, packId, StringComparison.Ordinal));

    /// <summary>Every entry of a definition kind across the catalog, with the pack it came from.</summary>
    public IEnumerable<(LoadedPack Pack, ContentDocument Document, ContentEntry Entry)> Entries(string definitionKind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionKind);
        foreach (LoadedPack pack in Packs)
        {
            foreach (ContentDocument document in pack.Documents)
            {
                if (!string.Equals(document.DefinitionKind, definitionKind, StringComparison.Ordinal)) continue;
                foreach (ContentEntry entry in document.Entries) yield return (pack, document, entry);
            }
        }
    }

    /// <summary>Fails with every issue when the content is not valid.</summary>
    public ContentCatalog RequireValid()
    {
        if (IsValid) return this;
        string summary = $"{Issues.Count} content problem{(Issues.Count == 1 ? string.Empty : "s")}: {Issues[0]}";
        throw new ContentValidationException(summary, Issues);
    }

    /// <summary>Creates a catalog from already-loaded packs and issues.</summary>
    public static ContentCatalog From(IReadOnlyList<LoadedPack> packs, IReadOnlyList<ContentValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(packs);
        ArgumentNullException.ThrowIfNull(issues);
        return new ContentCatalog(packs, issues);
    }
}
