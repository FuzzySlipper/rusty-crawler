namespace PartyRpg.Kit.Content;

/// <summary>
/// Where a pack came from. Imported packs must carry this so a pack cannot silently mix editions or
/// claim an origin it does not have; authored packs carry the authoring description instead.
/// </summary>
/// <param name="Description">A human-readable statement of where the content came from.</param>
/// <param name="Game">The game edition the content was taken from, for imported packs.</param>
/// <param name="Build">The release or build the content was taken from, for imported packs.</param>
/// <param name="Producer">What produced the pack: an importer revision, or an authoring tool.</param>
public sealed record ContentProvenance(string Description, string? Game = null, string? Build = null, string? Producer = null);
