namespace PartyRpg.Host;

/// <summary>
/// The catalog of bundles this product ships and will start from. It is a compiled list, so a bundle
/// cannot arrive from outside the product, and the default is named here rather than inferred.
/// </summary>
public static class BuiltInBundles
{
    /// <summary>
    /// The ordinary new game: the operator's imported tables and world with the authored opening arrival. Its
    /// imported packs are generated from the operator's own installation, so a checkout without them starts with
    /// the bundle's setup guidance instead of a world.
    /// </summary>
    public const string NewGame = "mm7-new-game";

    /// <summary>The empty-content shell: no packs, no world. Kept explicit for diagnosing the host itself.</summary>
    public const string Shell = "partyrpg-default";

    /// <summary>The bundle this product starts from unless another is selected explicitly.</summary>
    public const string Default = NewGame;

    /// <summary>Every bundle this product will select.</summary>
    public static IReadOnlyList<string> All { get; } = [NewGame, Shell];

    /// <summary>The built-in bundle a selection value names, the default when it names none.</summary>
    /// <param name="value">The operator's value, or null when unset.</param>
    /// <exception cref="InvalidOperationException">The value names no built-in bundle.</exception>
    internal static string Parse(string? value)
    {
        string trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0) return Default;
        return All.Contains(trimmed, StringComparer.Ordinal)
            ? trimmed
            : throw new InvalidOperationException(
                $"{ProductIdentity.BundleVariable} is '{value}', which names no bundle this product ships; it ships {string.Join(" and ", All)}. Leave it unset for {Default}.");
    }
}
