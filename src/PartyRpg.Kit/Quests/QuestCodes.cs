namespace PartyRpg.Kit.Quests;

/// <summary>The codes an errand is refused with.</summary>
/// <remarks>
/// A code is what a caller and a test branch on and the message beside it is what a person reads, so every
/// refusal this mechanism can give is named here once rather than spelled at the point of use.
/// </remarks>
public static class QuestCodes
{
    /// <summary>A counted reward would exceed the durable record's range, so nothing was settled.</summary>
    public const string QuestRecordCapacity = "quest-record-capacity";

    /// <summary>The refusal code <c>quest-item-needed</c>.</summary>
    public const string QuestItemNeeded = "quest-item-needed";

    /// <summary>The refusal code <c>quest-already-finished</c>.</summary>
    public const string QuestAlreadyFinished = "quest-already-finished";

    /// <summary>The refusal code <c>quest-already-known</c>.</summary>
    public const string QuestAlreadyKnown = "quest-already-known";

    /// <summary>The refusal code <c>quest-already-taken</c>.</summary>
    public const string QuestAlreadyTaken = "quest-already-taken";

    /// <summary>The refusal code <c>quest-no-award-owner</c>.</summary>
    public const string QuestNoAwardOwner = "quest-no-award-owner";

    /// <summary>The refusal code <c>quest-no-ledger</c>.</summary>
    public const string QuestNoLedger = "quest-no-ledger";

    /// <summary>The refusal code <c>quest-not-accepted</c>.</summary>
    public const string QuestNotAccepted = "quest-not-accepted";

    /// <summary>The refusal code <c>quest-not-offered</c>.</summary>
    public const string QuestNotOffered = "quest-not-offered";

    /// <summary>The refusal code <c>quest-not-taken</c>.</summary>
    public const string QuestNotTaken = "quest-not-taken";

    /// <summary>The refusal code <c>quest-not-the-giver</c>.</summary>
    public const string QuestNotTheGiver = "quest-not-the-giver";

    /// <summary>The refusal code <c>quest-objectives-unmet</c>.</summary>
    public const string QuestObjectivesUnmet = "quest-objectives-unmet";

    /// <summary>The refusal code <c>quest-offer-condition-unmet</c>.</summary>
    public const string QuestOfferConditionUnmet = "quest-offer-condition-unmet";

    /// <summary>The refusal code <c>quest-unknown</c>.</summary>
    public const string QuestUnknown = "quest-unknown";
}
