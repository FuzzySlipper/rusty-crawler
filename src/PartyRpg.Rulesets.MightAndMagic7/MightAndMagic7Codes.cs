namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>The codes this game's own rules refuse a use, a rest, a lesson, a fare or a service with.</summary>
/// <remarks>
/// A code is what a caller and a test branch on and the message beside it is what a person reads, so every
/// refusal this mechanism can give is named here once rather than spelled at the point of use.
/// </remarks>
public static class MightAndMagic7Codes
{
    /// <summary>The refusal code <c>camp-hostiles-near</c>.</summary>
    public const string CampHostilesNear = "camp-hostiles-near";

    /// <summary>The refusal code <c>camp-risk-unavailable</c>.</summary>
    public const string CampRiskUnavailable = "camp-risk-unavailable";

    /// <summary>The refusal code <c>camp-under-a-roof</c>.</summary>
    public const string CampUnderARoof = "camp-under-a-roof";

    /// <summary>The refusal code <c>container-contents-unresolved</c>.</summary>
    public const string ContainerContentsUnresolved = "container-contents-unresolved";

    /// <summary>The refusal code <c>container-emptied</c>.</summary>
    public const string ContainerEmptied = "container-emptied";

    /// <summary>The refusal code <c>corpse-gone</c>.</summary>
    public const string CorpseGone = "corpse-gone";

    /// <summary>The refusal code <c>door-already-open</c>.</summary>
    public const string DoorAlreadyOpen = "door-already-open";

    /// <summary>The refusal code <c>equipment-hands-full</c>: a two-handed weapon and something in the off hand.</summary>
    public const string EquipmentHandsFull = "equipment-hands-full";

    /// <summary>The refusal code <c>equipment-not-wearable</c>: the item table states nothing worn for the item.</summary>
    public const string EquipmentNotWearable = "equipment-not-wearable";

    /// <summary>The refusal code <c>equipment-off-hand-untrained</c>: a second weapon without the mastery it needs.</summary>
    public const string EquipmentOffHandUntrained = "equipment-off-hand-untrained";

    /// <summary>The refusal code <c>equipment-skill-missing</c>.</summary>
    public const string EquipmentSkillMissing = "equipment-skill-missing";

    /// <summary>The refusal code <c>equipment-skill-unknown</c>.</summary>
    public const string EquipmentSkillUnknown = "equipment-skill-unknown";

    /// <summary>The refusal code <c>equipment-slot-unknown</c>: a place this game's figure does not have.</summary>
    public const string EquipmentSlotUnknown = "equipment-slot-unknown";

    /// <summary>The refusal code <c>equipment-wrong-slot</c>: the item is not shaped for that place.</summary>
    public const string EquipmentWrongSlot = "equipment-wrong-slot";

    /// <summary>The refusal code <c>interaction-event-not-executed</c>.</summary>
    public const string InteractionEventNotExecuted = "interaction-event-not-executed";

    /// <summary>The refusal code <c>mixture-cannot-act</c>.</summary>
    public const string MixtureCannotAct = "mixture-cannot-act";

    /// <summary>The refusal code <c>rest-in-the-open</c>.</summary>
    public const string RestInTheOpen = "rest-in-the-open";

    /// <summary>The refusal code <c>rest-risk-unavailable</c>.</summary>
    public const string RestRiskUnavailable = "rest-risk-unavailable";

    /// <summary>The refusal code <c>service-deposit-empty</c>.</summary>
    public const string ServiceDepositEmpty = "service-deposit-empty";

    /// <summary>The refusal code <c>service-experience-short</c>.</summary>
    public const string ServiceExperienceShort = "service-experience-short";

    /// <summary>The refusal code <c>service-item-needed-by-quest</c>.</summary>
    public const string ServiceItemNeededByQuest = "service-item-needed-by-quest";

    /// <summary>The refusal code <c>service-lesson-attribute-short</c>.</summary>
    public const string ServiceLessonAttributeShort = "service-lesson-attribute-short";

    /// <summary>The refusal code <c>service-lesson-class-forbidden</c>.</summary>
    public const string ServiceLessonClassForbidden = "service-lesson-class-forbidden";

    /// <summary>The refusal code <c>service-lesson-companion-short</c>.</summary>
    public const string ServiceLessonCompanionShort = "service-lesson-companion-short";

    /// <summary>The refusal code <c>service-lesson-level-short</c>.</summary>
    public const string ServiceLessonLevelShort = "service-lesson-level-short";

    /// <summary>The refusal code <c>service-lesson-needs-promotion</c>.</summary>
    public const string ServiceLessonNeedsPromotion = "service-lesson-needs-promotion";

    /// <summary>The refusal code <c>service-lesson-rung-short</c>.</summary>
    public const string ServiceLessonRungShort = "service-lesson-rung-short";

    /// <summary>The refusal code <c>service-lesson-skill-unknown</c>.</summary>
    public const string ServiceLessonSkillUnknown = "service-lesson-skill-unknown";

    /// <summary>The refusal code <c>service-lesson-unknown-skill</c>.</summary>
    public const string ServiceLessonUnknownSkill = "service-lesson-unknown-skill";

    /// <summary>The refusal code <c>service-lesson-unused-skill</c>.</summary>
    public const string ServiceLessonUnusedSkill = "service-lesson-unused-skill";

    /// <summary>The refusal code <c>service-membership-held</c>.</summary>
    public const string ServiceMembershipHeld = "service-membership-held";

    /// <summary>The refusal code <c>service-membership-required</c>.</summary>
    public const string ServiceMembershipRequired = "service-membership-required";

    /// <summary>The refusal code <c>service-nothing-to-heal</c>.</summary>
    public const string ServiceNothingToHeal = "service-nothing-to-heal";

    /// <summary>The refusal code <c>service-nothing-to-learn</c>.</summary>
    public const string ServiceNothingToLearn = "service-nothing-to-learn";

    /// <summary>The refusal code <c>service-packs-full</c>.</summary>
    public const string ServicePacksFull = "service-packs-full";

    /// <summary>The refusal code <c>service-passage-held</c>.</summary>
    public const string ServicePassageHeld = "service-passage-held";

    /// <summary>The refusal code <c>service-training-capped</c>.</summary>
    public const string ServiceTrainingCapped = "service-training-capped";

    /// <summary>The refusal code <c>skill-closed-by-path</c>.</summary>
    public const string SkillClosedByPath = "skill-closed-by-path";

    /// <summary>The refusal code <c>skill-closed-by-unchosen-path</c>.</summary>
    public const string SkillClosedByUnchosenPath = "skill-closed-by-unchosen-path";

    /// <summary>The refusal code <c>spell-place-no-arrival</c>: a travel spell names a place that states nowhere to arrive.</summary>
    public const string SpellPlaceNoArrival = "spell-place-no-arrival";

    /// <summary>The refusal code <c>spell-place-unvisited</c>: a travel spell names a place the party has never been to.</summary>
    public const string SpellPlaceUnvisited = "spell-place-unvisited";

    /// <summary>The refusal code <c>travel-fare-unpaid</c>.</summary>
    public const string TravelFareUnpaid = "travel-fare-unpaid";

    /// <summary>The refusal code <c>travel-kind-unknown</c>.</summary>
    public const string TravelKindUnknown = "travel-kind-unknown";

    /// <summary>The refusal code <c>travel-no-party</c>.</summary>
    public const string TravelNoParty = "travel-no-party";
}
