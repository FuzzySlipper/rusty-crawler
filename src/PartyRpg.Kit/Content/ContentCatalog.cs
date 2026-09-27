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
/// The packs a caller may read — every pack under a content root, or the selection a bundle resolved to.
/// </summary>
/// <remarks>
/// <para>
/// The catalog is loaded, not compiled: changing valid content or tuning must not require a rebuild.
/// It is validated when it is loaded, and validation is all-or-nothing at the point of use — a caller
/// that needs content calls <see cref="RequireValid"/> and gets every issue at once, while a caller
/// that is reporting on content can read <see cref="Issues"/> and carry on.
/// </para>
/// <para>
/// <b>A catalog can be narrowed to a selection, and every reader sees the same narrowing.</b>
/// <see cref="Selected"/> answers with a catalog holding exactly the packs a bundle named, so what a
/// session may read is decided once, where the bundle is resolved, rather than by each mechanism that
/// reads content asking whether the pack it is looking at was selected. The issues travel with the
/// narrowing because the root was read whole and a defect anywhere in it is still a defect in the
/// content the product would run.
/// </para>
/// </remarks>
public sealed class ContentCatalog
{
    private ContentCatalog(IReadOnlyList<LoadedPack> packs, IReadOnlyList<ContentValidationIssue> issues)
    {
        Packs = packs;
        Issues = issues;
    }

    /// <summary>Every pack this catalog reads, in load order.</summary>
    public IReadOnlyList<LoadedPack> Packs { get; }

    /// <summary>Everything wrong with the content, empty when it is valid.</summary>
    public IReadOnlyList<ContentValidationIssue> Issues { get; }

    /// <summary>Whether the content is valid.</summary>
    public bool IsValid => Issues.Count == 0;

    /// <summary>Finds a pack by id.</summary>
    /// <remarks>
    /// A pack id names exactly one pack — the loader refuses a second directory that claims one — so the
    /// first match is the only match there is.
    /// </remarks>
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

    /// <summary>
    /// This catalog as a selection sees it: exactly the packs given, and nothing else.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A bundle names the packs a product runs, so this is what "selected" means everywhere downstream:
    /// a pack that was not named contributes no definitions, no placements, and no scenario start, because
    /// it is not in the catalog a session reads. Nothing else has to know the rule, and no mechanism can
    /// quietly read around it.
    /// </para>
    /// <para>
    /// The order is the caller's — a bundle lists its packs in load order — and the issues are the root's,
    /// because narrowing is a view of content that was already read and judged: a root that is present and
    /// wrong stays wrong for a caller reading one pack of it.
    /// </para>
    /// </remarks>
    /// <param name="packs">The packs the selection names, each of which must be a pack of this catalog.</param>
    /// <exception cref="ArgumentException">A pack is not part of this catalog, so it cannot be selected from it.</exception>
    public ContentCatalog Selected(IReadOnlyList<LoadedPack> packs)
    {
        ArgumentNullException.ThrowIfNull(packs);
        HashSet<string> known = [.. Packs.Select(pack => pack.PackId)];
        foreach (LoadedPack pack in packs)
        {
            if (!known.Contains(pack.PackId))
            {
                throw new ArgumentException(
                    $"Pack '{pack.PackId}' is not part of this catalog, so it cannot be selected from it.",
                    nameof(packs));
            }
        }

        return new ContentCatalog([.. packs], Issues);
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
