namespace PartyRpg.Kit.Party;

/// <summary>The codes party creation refuses a choice with.</summary>
/// <remarks>
/// A code is what a caller and a test branch on and the message beside it is what a person reads, so every
/// refusal this mechanism can give is named here once rather than spelled at the point of use.
/// </remarks>
public static class CreationCodes
{
    /// <summary>The refusal code <c>attribute-ceiling</c>.</summary>
    public const string AttributeCeiling = "attribute-ceiling";

    /// <summary>The refusal code <c>attribute-floor</c>.</summary>
    public const string AttributeFloor = "attribute-floor";

    /// <summary>The refusal code <c>attribute-pool-short</c>.</summary>
    public const string AttributePoolShort = "attribute-pool-short";

    /// <summary>The refusal code <c>attribute-pool-unspent</c>.</summary>
    public const string AttributePoolUnspent = "attribute-pool-unspent";

    /// <summary>The refusal code <c>attribute-unknown</c>.</summary>
    public const string AttributeUnknown = "attribute-unknown";

    /// <summary>The refusal code <c>attribute-unreachable</c>.</summary>
    public const string AttributeUnreachable = "attribute-unreachable";

    /// <summary>The refusal code <c>class-unchosen</c>.</summary>
    public const string ClassUnchosen = "class-unchosen";

    /// <summary>The refusal code <c>class-unknown</c>.</summary>
    public const string ClassUnknown = "class-unknown";

    /// <summary>The refusal code <c>creation-choice-missing</c>.</summary>
    public const string CreationChoiceMissing = "creation-choice-missing";

    /// <summary>The refusal code <c>creation-incomplete</c>.</summary>
    public const string CreationIncomplete = "creation-incomplete";

    /// <summary>The refusal code <c>creation-refused</c>.</summary>
    public const string CreationRefused = "creation-refused";

    /// <summary>The refusal code <c>creation-step</c>.</summary>
    public const string CreationStep = "creation-step";

    /// <summary>The refusal code <c>member-unknown</c>.</summary>
    public const string MemberUnknown = "member-unknown";

    /// <summary>The refusal code <c>name-blank</c>.</summary>
    public const string NameBlank = "name-blank";

    /// <summary>The refusal code <c>name-invalid</c>.</summary>
    public const string NameInvalid = "name-invalid";

    /// <summary>The refusal code <c>name-too-long</c>.</summary>
    public const string NameTooLong = "name-too-long";

    /// <summary>The refusal code <c>no-default</c>.</summary>
    public const string NoDefault = "no-default";

    /// <summary>The refusal code <c>portrait-race-unknown</c>.</summary>
    public const string PortraitRaceUnknown = "portrait-race-unknown";

    /// <summary>The refusal code <c>portrait-unchosen</c>.</summary>
    public const string PortraitUnchosen = "portrait-unchosen";

    /// <summary>The refusal code <c>portrait-unknown</c>.</summary>
    public const string PortraitUnknown = "portrait-unknown";

    /// <summary>The refusal code <c>skill-already-chosen</c>.</summary>
    public const string SkillAlreadyChosen = "skill-already-chosen";

    /// <summary>The refusal code <c>skill-fixed</c>.</summary>
    public const string SkillFixed = "skill-fixed";

    /// <summary>The refusal code <c>skill-not-chosen</c>.</summary>
    public const string SkillNotChosen = "skill-not-chosen";

    /// <summary>The refusal code <c>skill-not-legal</c>.</summary>
    public const string SkillNotLegal = "skill-not-legal";

    /// <summary>The refusal code <c>skills-complete</c>.</summary>
    public const string SkillsComplete = "skills-complete";

    /// <summary>The refusal code <c>skills-unchosen</c>.</summary>
    public const string SkillsUnchosen = "skills-unchosen";
}
