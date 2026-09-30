namespace PartyRpg.Kit.Progression;

/// <summary>The codes growth, training, a raise and a promotion are refused with.</summary>
/// <remarks>
/// A code is what a caller and a test branch on and the message beside it is what a person reads, so every
/// refusal this mechanism can give is named here once rather than spelled at the point of use.
/// </remarks>
public static class ProgressionCodes
{
    /// <summary>The refusal code <c>insufficient-skill-points</c>.</summary>
    public const string InsufficientSkillPoints = "insufficient-skill-points";

    /// <summary>The refusal code <c>progression-award-empty</c>.</summary>
    public const string ProgressionAwardEmpty = "progression-award-empty";

    /// <summary>The refusal code <c>progression-award-unshared</c>.</summary>
    public const string ProgressionAwardUnshared = "progression-award-unshared";

    /// <summary>The refusal code <c>progression-experience-short</c>.</summary>
    public const string ProgressionExperienceShort = "progression-experience-short";

    /// <summary>The refusal code <c>progression-training-capped</c>.</summary>
    public const string ProgressionTrainingCapped = "progression-training-capped";

    /// <summary>The refusal code <c>promotion-class-absent</c>.</summary>
    public const string PromotionClassAbsent = "promotion-class-absent";

    /// <summary>The refusal code <c>promotion-policy-missing</c>.</summary>
    public const string PromotionPolicyMissing = "promotion-policy-missing";

    /// <summary>The refusal code <c>promotion-requirements-unmet</c>.</summary>
    public const string PromotionRequirementsUnmet = "promotion-requirements-unmet";

    /// <summary>The refusal code <c>promotion-unknown</c>.</summary>
    public const string PromotionUnknown = "promotion-unknown";

    /// <summary>The refusal code <c>skill-ceiling-reached</c>.</summary>
    public const string SkillCeilingReached = "skill-ceiling-reached";

    /// <summary>The refusal code <c>skill-not-learned</c>.</summary>
    public const string SkillNotLearned = "skill-not-learned";

    /// <summary>The refusal code <c>skill-not-permitted</c>.</summary>
    public const string SkillNotPermitted = "skill-not-permitted";

    /// <summary>The refusal code <c>skill-policy-missing</c>.</summary>
    public const string SkillPolicyMissing = "skill-policy-missing";
}
