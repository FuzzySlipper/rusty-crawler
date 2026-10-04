namespace PartyRpg.Kit.Magic;

/// <summary>The codes a spell is refused with, and the two a cast spell's effect reports.</summary>
/// <remarks>
/// A code is what a caller and a test branch on and the message beside it is what a person reads, so every
/// refusal this mechanism can give is named here once rather than spelled at the point of use.
/// </remarks>
public static class SpellCodes
{
    /// <summary>The refusal code <c>spell-already-known</c>.</summary>
    public const string SpellAlreadyKnown = "spell-already-known";

    /// <summary>The refusal code <c>spell-caster-cannot-act</c>.</summary>
    public const string SpellCasterCannotAct = "spell-caster-cannot-act";

    /// <summary>The refusal code <c>spell-effect-applied</c>.</summary>
    public const string SpellEffectApplied = "spell-effect-applied";

    /// <summary>The refusal code <c>spell-effect-unexpressed</c>.</summary>
    public const string SpellEffectUnexpressed = "spell-effect-unexpressed";

    /// <summary>The refusal code <c>spell-item-carries-another</c>.</summary>
    public const string SpellItemCarriesAnother = "spell-item-carries-another";

    /// <summary>The refusal code <c>spell-item-carries-none</c>.</summary>
    public const string SpellItemCarriesNone = "spell-item-carries-none";

    /// <summary>The refusal code <c>spell-item-no-charges</c>.</summary>
    public const string SpellItemNoCharges = "spell-item-no-charges";

    /// <summary>The refusal code <c>spell-item-not-held</c>.</summary>
    public const string SpellItemNotHeld = "spell-item-not-held";

    /// <summary>The refusal code <c>spell-item-not-wielded</c>.</summary>
    public const string SpellItemNotWielded = "spell-item-not-wielded";

    /// <summary>The refusal code <c>spell-mastery-too-low</c>.</summary>
    public const string SpellMasteryTooLow = "spell-mastery-too-low";

    /// <summary>The refusal code <c>spell-no-effect-path</c>.</summary>
    public const string SpellNoEffectPath = "spell-no-effect-path";

    /// <summary>The refusal code <c>spell-no-such-member</c>.</summary>
    public const string SpellNoSuchMember = "spell-no-such-member";

    /// <summary>The refusal code <c>spell-not-applied</c>.</summary>
    public const string SpellNotApplied = "spell-not-applied";

    /// <summary>The refusal code <c>spell-not-known</c>.</summary>
    public const string SpellNotKnown = "spell-not-known";

    /// <summary>The refusal code <c>spell-points-short</c>.</summary>
    public const string SpellPointsShort = "spell-points-short";

    /// <summary>The refusal code <c>spell-school-closed</c>.</summary>
    public const string SpellSchoolClosed = "spell-school-closed";

    /// <summary>The refusal code <c>spell-school-missing</c>.</summary>
    public const string SpellSchoolMissing = "spell-school-missing";

    /// <summary>The refusal code <c>spell-target-invalid</c>.</summary>
    public const string SpellTargetInvalid = "spell-target-invalid";

    /// <summary>The refusal code <c>spell-target-missing</c>.</summary>
    public const string SpellTargetMissing = "spell-target-missing";

    /// <summary>The refusal code <c>spell-target-unavailable</c>.</summary>
    public const string SpellTargetUnavailable = "spell-target-unavailable";

    /// <summary>The refusal code <c>spell-target-out-of-reach</c>.</summary>
    public const string SpellTargetOutOfReach = "spell-target-out-of-reach";

    /// <summary>The refusal code <c>spell-target-friendly</c>.</summary>
    public const string SpellTargetFriendly = "spell-target-friendly";

    /// <summary>The refusal code <c>spell-unknown</c>.</summary>
    public const string SpellUnknown = "spell-unknown";
}
