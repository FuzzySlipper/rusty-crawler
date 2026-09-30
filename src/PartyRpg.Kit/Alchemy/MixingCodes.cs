namespace PartyRpg.Kit.Alchemy;

/// <summary>The codes a mixture reports what came of it with, and refuses one with.</summary>
/// <remarks>
/// A code is what a caller and a test branch on and the message beside it is what a person reads, so every
/// refusal this mechanism can give is named here once rather than spelled at the point of use.
/// </remarks>
public static class MixingCodes
{
    /// <summary>The refusal code <c>mixture-burst</c>.</summary>
    public const string MixtureBurst = "mixture-burst";

    /// <summary>The refusal code <c>mixture-ingredient-missing</c>.</summary>
    public const string MixtureIngredientMissing = "mixture-ingredient-missing";

    /// <summary>The refusal code <c>mixture-mastery-too-low</c>.</summary>
    public const string MixtureMasteryTooLow = "mixture-mastery-too-low";

    /// <summary>The refusal code <c>mixture-mixed</c>.</summary>
    public const string MixtureMixed = "mixture-mixed";

    /// <summary>The refusal code <c>mixture-no-member</c>.</summary>
    public const string MixtureNoMember = "mixture-no-member";

    /// <summary>The refusal code <c>mixture-nothing</c>.</summary>
    public const string MixtureNothing = "mixture-nothing";

    /// <summary>The refusal code <c>mixture-same-instance</c>.</summary>
    public const string MixtureSameInstance = "mixture-same-instance";

    /// <summary>The refusal code <c>mixture-strength-unstated</c>.</summary>
    public const string MixtureStrengthUnstated = "mixture-strength-unstated";

    /// <summary>The refusal code <c>mixture-unknown</c>.</summary>
    public const string MixtureUnknown = "mixture-unknown";
}
