namespace PartyRpg.Kit.Content;

/// <summary>One document declared by a pack's manifest.</summary>
/// <param name="Path">The document's path inside the pack.</param>
/// <param name="DocumentId">The document's stable id, unique inside the catalog.</param>
/// <param name="DefinitionKind">What kind of thing the document holds, such as a place or a monster.</param>
/// <param name="References">Ids the document's entries refer to, written as <c>kind:id</c>.</param>
public sealed record PackDocumentEntry(
    string Path,
    string DocumentId,
    string DefinitionKind,
    IReadOnlyList<string>? References = null);

/// <summary>
/// A pack's manifest: what the pack is, where it came from, and which documents it declares.
/// </summary>
/// <param name="SchemaVersion">The schema the pack was written against.</param>
/// <param name="PackId">The pack's stable id, which must match its directory.</param>
/// <param name="Kind">Which of the four kinds of content the pack carries.</param>
/// <param name="Provenance">Where the pack came from.</param>
/// <param name="Documents">The documents the pack declares.</param>
public sealed record PackManifest(
    int SchemaVersion,
    string PackId,
    ContentPackKind Kind,
    ContentProvenance Provenance,
    IReadOnlyList<PackDocumentEntry> Documents)
{
    /// <summary>The schema version this build writes and accepts.</summary>
    public const int CurrentSchemaVersion = 1;
}
