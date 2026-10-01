namespace PartyRpg.Kit.Persistence;

/// <summary>The codes a save problem is named with.</summary>
/// <remarks>
/// A code is what a caller and a test branch on and the text beside it is what a person reads, so every kind
/// of problem a save can have — refused at capture, unreadable in the store, or contradictory on load — is
/// named here once rather than spelled at the point of use. One code names one kind of contradiction; which
/// place, quest, member, or item it was is the problem's subject.
/// </remarks>
public static class SaveCodes
{
    /// <summary>The problem code <c>save-part-missing</c>.</summary>
    public const string SavePartMissing = "save-part-missing";

    /// <summary>The problem code <c>save-fight-unsaved</c>.</summary>
    public const string SaveFightUnsaved = "save-fight-unsaved";

    /// <summary>The problem code <c>save-deadline-unowned</c>.</summary>
    public const string SaveDeadlineUnowned = "save-deadline-unowned";

    /// <summary>The problem code <c>save-deadline-uncarried</c>.</summary>
    public const string SaveDeadlineUncarried = "save-deadline-uncarried";

    /// <summary>The problem code <c>save-unreadable</c>.</summary>
    public const string SaveUnreadable = "save-unreadable";

    /// <summary>The problem code <c>save-store-unopened</c>.</summary>
    public const string SaveStoreUnopened = "save-store-unopened";

    /// <summary>The problem code <c>save-store-absent</c>: there is no persistence to read a save from at all.</summary>
    public const string SaveStoreAbsent = "save-store-absent";

    /// <summary>The problem code <c>save-slot-empty</c>: the slot a resume reads holds no session.</summary>
    public const string SaveSlotEmpty = "save-slot-empty";

    /// <summary>The problem code <c>save-day-before-start</c>.</summary>
    public const string SaveDayBeforeStart = "save-day-before-start";

    /// <summary>The problem code <c>save-day-disagrees</c>.</summary>
    public const string SaveDayDisagrees = "save-day-disagrees";

    /// <summary>The problem code <c>save-place-unknown</c>.</summary>
    public const string SavePlaceUnknown = "save-place-unknown";

    /// <summary>The problem code <c>save-place-twice</c>.</summary>
    public const string SavePlaceTwice = "save-place-twice";

    /// <summary>The problem code <c>save-place-restorations-negative</c>.</summary>
    public const string SavePlaceRestorationsNegative = "save-place-restorations-negative";

    /// <summary>The problem code <c>save-place-restored-future</c>.</summary>
    public const string SavePlaceRestoredFuture = "save-place-restored-future";

    /// <summary>The problem code <c>save-pose-place-unknown</c>.</summary>
    public const string SavePosePlaceUnknown = "save-pose-place-unknown";

    /// <summary>The problem code <c>save-pose-not-numbers</c>.</summary>
    public const string SavePoseNotNumbers = "save-pose-not-numbers";

    /// <summary>The problem code <c>save-pose-outside</c>.</summary>
    public const string SavePoseOutside = "save-pose-outside";

    /// <summary>The problem code <c>save-quest-twice</c>.</summary>
    public const string SaveQuestTwice = "save-quest-twice";

    /// <summary>The problem code <c>save-quest-stage-unknown</c>.</summary>
    public const string SaveQuestStageUnknown = "save-quest-stage-unknown";

    /// <summary>The problem code <c>save-quest-unknown</c>.</summary>
    public const string SaveQuestUnknown = "save-quest-unknown";

    /// <summary>The problem code <c>save-quest-place-unknown</c>.</summary>
    public const string SaveQuestPlaceUnknown = "save-quest-place-unknown";

    /// <summary>The problem code <c>save-quest-no-giver</c>.</summary>
    public const string SaveQuestNoGiver = "save-quest-no-giver";

    /// <summary>The problem code <c>save-journal-oversize</c>.</summary>
    public const string SaveJournalOversize = "save-journal-oversize";

    /// <summary>The problem code <c>save-journal-kind-unknown</c>.</summary>
    public const string SaveJournalKindUnknown = "save-journal-kind-unknown";

    /// <summary>The problem code <c>save-journal-empty</c>.</summary>
    public const string SaveJournalEmpty = "save-journal-empty";

    /// <summary>The problem code <c>save-journal-future</c>.</summary>
    public const string SaveJournalFuture = "save-journal-future";

    /// <summary>The problem code <c>save-journal-twice</c>.</summary>
    public const string SaveJournalTwice = "save-journal-twice";

    /// <summary>The problem code <c>save-knowledge-oversize</c>.</summary>
    public const string SaveKnowledgeOversize = "save-knowledge-oversize";

    /// <summary>The problem code <c>save-knowledge-kind-unknown</c>.</summary>
    public const string SaveKnowledgeKindUnknown = "save-knowledge-kind-unknown";

    /// <summary>The problem code <c>save-knowledge-empty</c>.</summary>
    public const string SaveKnowledgeEmpty = "save-knowledge-empty";

    /// <summary>The problem code <c>save-knowledge-future</c>.</summary>
    public const string SaveKnowledgeFuture = "save-knowledge-future";

    /// <summary>The problem code <c>save-knowledge-twice</c>.</summary>
    public const string SaveKnowledgeTwice = "save-knowledge-twice";

    /// <summary>The problem code <c>save-maps-oversize</c>.</summary>
    public const string SaveMapsOversize = "save-maps-oversize";

    /// <summary>The problem code <c>save-map-twice</c>.</summary>
    public const string SaveMapTwice = "save-map-twice";

    /// <summary>The problem code <c>save-map-place-unknown</c>.</summary>
    public const string SaveMapPlaceUnknown = "save-map-place-unknown";

    /// <summary>The problem code <c>save-map-grid-defective</c>.</summary>
    public const string SaveMapGridDefective = "save-map-grid-defective";

    /// <summary>The problem code <c>save-map-cells-misaligned</c>.</summary>
    public const string SaveMapCellsMisaligned = "save-map-cells-misaligned";

    /// <summary>The problem code <c>save-map-cells-not-hex</c>.</summary>
    public const string SaveMapCellsNotHex = "save-map-cells-not-hex";

    /// <summary>The problem code <c>save-kept-place-unknown</c>: a place keeps values and the world has no such place.</summary>
    public const string SaveKeptPlaceUnknown = "save-kept-place-unknown";

    /// <summary>The problem code <c>save-kept-place-twice</c>: one place's kept values are recorded twice.</summary>
    public const string SaveKeptPlaceTwice = "save-kept-place-twice";

    /// <summary>The problem code <c>save-kept-value-unnamed</c>: a kept value has no name.</summary>
    public const string SaveKeptValueUnnamed = "save-kept-value-unnamed";

    /// <summary>The problem code <c>save-kept-value-twice</c>: one name is recorded twice in one place.</summary>
    public const string SaveKeptValueTwice = "save-kept-value-twice";

    /// <summary>The problem code <c>save-kept-value-unknown</c>: the ruleset keeps no such value, or not that figure.</summary>
    public const string SaveKeptValueUnknown = "save-kept-value-unknown";

    /// <summary>The problem code <c>save-record-unknown</c>: the ruleset writes a record of that name, and not that one.</summary>
    public const string SaveRecordUnknown = "save-record-unknown";

    /// <summary>The problem code <c>save-cursor-zero</c>.</summary>
    public const string SaveCursorZero = "save-cursor-zero";

    /// <summary>The problem code <c>save-member-unidentified</c>.</summary>
    public const string SaveMemberUnidentified = "save-member-unidentified";

    /// <summary>The problem code <c>save-member-twice</c>.</summary>
    public const string SaveMemberTwice = "save-member-twice";

    /// <summary>The problem code <c>save-member-beyond-cursor</c>.</summary>
    public const string SaveMemberBeyondCursor = "save-member-beyond-cursor";

    /// <summary>The problem code <c>save-item-unidentified</c>.</summary>
    public const string SaveItemUnidentified = "save-item-unidentified";

    /// <summary>The problem code <c>save-item-twice</c>.</summary>
    public const string SaveItemTwice = "save-item-twice";

    /// <summary>The problem code <c>save-item-beyond-cursor</c>.</summary>
    public const string SaveItemBeyondCursor = "save-item-beyond-cursor";

    /// <summary>The problem code <c>save-item-held-by-nobody</c>.</summary>
    public const string SaveItemHeldByNobody = "save-item-held-by-nobody";

    /// <summary>The problem code <c>save-item-worn-by-stranger</c>.</summary>
    public const string SaveItemWornByStranger = "save-item-worn-by-stranger";

    /// <summary>The problem code <c>save-slot-twice</c>.</summary>
    public const string SaveSlotTwice = "save-slot-twice";

    /// <summary>The problem code <c>save-purse-negative</c>.</summary>
    public const string SavePurseNegative = "save-purse-negative";

    /// <summary>The problem code <c>save-larder-negative</c>.</summary>
    public const string SaveLarderNegative = "save-larder-negative";

    /// <summary>The problem code <c>save-entry-unnamed</c>.</summary>
    public const string SaveEntryUnnamed = "save-entry-unnamed";

    /// <summary>The problem code <c>save-entry-twice</c>.</summary>
    public const string SaveEntryTwice = "save-entry-twice";

    /// <summary>The problem code <c>save-entry-below-minimum</c>.</summary>
    public const string SaveEntryBelowMinimum = "save-entry-below-minimum";
}
