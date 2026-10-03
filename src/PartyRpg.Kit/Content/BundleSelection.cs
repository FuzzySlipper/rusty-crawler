namespace PartyRpg.Kit.Content;

/// <summary>
/// What the host selected to start the product with: the bundle it chose, and how much content that
/// bundle resolved to.
/// </summary>
/// <remarks>
/// A session reports what it is running, not what it hoped to run, so this is the resolved selection
/// rather than the bundle id alone. An unselected content root is a value with no bundle rather than a
/// failure: a product whose content has not been generated yet still starts, and says so. A bundle whose packs
/// are absent is the same kind of value, carrying which packs are missing and the bundle's own setup guidance, so
/// the product can say what an operator does about it instead of presenting an empty session as a game.
/// </remarks>
/// <param name="BundleId">The selected bundle, or null when none was selected.</param>
/// <param name="PackCount">How many content packs the selection resolved to.</param>
public readonly record struct BundleSelection(string? BundleId, int PackCount)
{
    /// <summary>No bundle was selected.</summary>
    public static BundleSelection None => default;

    /// <summary>The bundle the host asked for whose packs are absent, or null when nothing is missing.</summary>
    public string? Unavailable { get; init; }

    /// <summary>The packs the requested bundle names that the content root does not hold.</summary>
    public IReadOnlyList<string> MissingPacks { get => _missing ?? []; init => _missing = value; }

    /// <summary>The requested bundle's guidance for producing its missing packs, empty when it states none.</summary>
    public string Setup { get => _setup ?? string.Empty; init => _setup = value; }

    private readonly IReadOnlyList<string>? _missing;
    private readonly string? _setup;

    /// <summary>A requested bundle that cannot be played because the packs it names are absent.</summary>
    /// <param name="missing">What the content root lacks.</param>
    public static BundleSelection Missing(MissingContent missing)
    {
        ArgumentNullException.ThrowIfNull(missing);
        return new BundleSelection(null, 0)
        {
            Unavailable = missing.Bundle.BundleId,
            MissingPacks = missing.Packs,
            Setup = missing.Bundle.Setup,
        };
    }
}

/// <summary>A requested bundle that resolved to nothing because packs it names are not in the content root.</summary>
/// <param name="Bundle">The bundle as declared.</param>
/// <param name="Packs">The pack ids it names that are absent, in the order it names them.</param>
public sealed record MissingContent(GameBundle Bundle, IReadOnlyList<string> Packs);
