namespace PartyRpg.Kit.Content;

/// <summary>
/// A game bundle: the one place where a ruleset, a set of content packs, and an optional tuning profile
/// are assembled into something a product can start. It carries identities only — the ruleset it names
/// is compiled into the product, and the packs it names are data.
/// </summary>
/// <param name="SchemaVersion">The schema the bundle was written against.</param>
/// <param name="BundleId">The bundle's stable id, which must match its directory.</param>
/// <param name="Ruleset">The ruleset identity the bundle is played with.</param>
/// <param name="ContentPacks">The packs the bundle includes, in load order.</param>
/// <param name="TuningPack">The tuning pack the bundle includes, when it has one.</param>
/// <param name="Description">A human-readable statement of what the bundle is.</param>
public sealed record GameBundle(
    int SchemaVersion,
    string BundleId,
    string Ruleset,
    IReadOnlyList<string> ContentPacks,
    string? TuningPack,
    string Description)
{
    /// <summary>The schema version this build writes and accepts.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>The file name a bundle directory carries.</summary>
    public const string FileName = "bundle.json";
}
