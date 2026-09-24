namespace PartyRpg.Kit.Content;

/// <summary>
/// What the host selected to start the product with: the bundle it chose, and how much content that
/// bundle resolved to.
/// </summary>
/// <remarks>
/// A session reports what it is running, not what it hoped to run, so this is the resolved selection
/// rather than the bundle id alone. An unselected content root is a value with no bundle rather than a
/// failure: a product whose content has not been generated yet still starts, and says so.
/// </remarks>
/// <param name="BundleId">The selected bundle, or null when none was selected.</param>
/// <param name="PackCount">How many content packs the selection resolved to.</param>
public readonly record struct BundleSelection(string? BundleId, int PackCount)
{
    /// <summary>No bundle was selected.</summary>
    public static BundleSelection None => default;
}
