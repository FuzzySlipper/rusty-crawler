namespace PartyRpg.Host;

/// <summary>
/// The catalog of bundles this product ships and will start from. It is a compiled list, so a bundle
/// cannot arrive from outside the product, and the default is named here rather than inferred.
/// </summary>
public static class BuiltInBundles
{
    /// <summary>The bundle this product starts from unless another is selected explicitly.</summary>
    public const string Default = "partyrpg-default";

    /// <summary>Every bundle this product will select.</summary>
    public static IReadOnlyList<string> All { get; } = [Default];
}
