namespace PartyRpg.Kit.Interaction;

/// <summary>The codes a use of something in the world is refused with.</summary>
/// <remarks>
/// A code is what a caller and a test branch on and the message beside it is what a person reads, so every
/// refusal this mechanism can give is named here once rather than spelled at the point of use.
/// </remarks>
public static class InteractionCodes
{
    /// <summary>The refusal code <c>interaction-locked</c>.</summary>
    public const string InteractionLocked = "interaction-locked";

    /// <summary>The refusal code <c>interaction-no-party</c>.</summary>
    public const string InteractionNoParty = "interaction-no-party";

    /// <summary>The refusal code <c>interaction-no-target</c>.</summary>
    public const string InteractionNoTarget = "interaction-no-target";

    /// <summary>The refusal code <c>interaction-occluded</c>.</summary>
    public const string InteractionOccluded = "interaction-occluded";

    /// <summary>The refusal code <c>interaction-out-of-reach</c>.</summary>
    public const string InteractionOutOfReach = "interaction-out-of-reach";

    /// <summary>The refusal code <c>interaction-outside-view</c>.</summary>
    public const string InteractionOutsideView = "interaction-outside-view";

    /// <summary>The refusal code <c>interaction-refused</c>.</summary>
    public const string InteractionRefused = "interaction-refused";

    /// <summary>The refusal code <c>interaction-requirement-unmet</c>.</summary>
    public const string InteractionRequirementUnmet = "interaction-requirement-unmet";

    /// <summary>The refusal code <c>interaction-target-changed</c>.</summary>
    public const string InteractionTargetChanged = "interaction-target-changed";

    /// <summary>The refusal code <c>interaction-target-gone</c>.</summary>
    public const string InteractionTargetGone = "interaction-target-gone";

    /// <summary>The refusal code <c>interaction-unavailable</c>.</summary>
    public const string InteractionUnavailable = "interaction-unavailable";

    /// <summary>The refusal code <c>interaction-visibility-unknown</c>.</summary>
    public const string InteractionVisibilityUnknown = "interaction-visibility-unknown";
}
