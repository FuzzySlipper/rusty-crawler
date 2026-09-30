namespace PartyRpg.Kit.Time;

/// <summary>The codes a rest, a camp, or a wait is refused with.</summary>
/// <remarks>
/// A code is what a caller and a test branch on and the message beside it is what a person reads, so every
/// refusal this mechanism can give is named here once rather than spelled at the point of use.
/// </remarks>
public static class RestCodes
{
    /// <summary>The refusal code <c>rest-larder-short</c>.</summary>
    public const string RestLarderShort = "rest-larder-short";

    /// <summary>The refusal code <c>rest-no-accounts</c>.</summary>
    public const string RestNoAccounts = "rest-no-accounts";

    /// <summary>The refusal code <c>rest-no-clock</c>.</summary>
    public const string RestNoClock = "rest-no-clock";

    /// <summary>The refusal code <c>rest-nowhere</c>.</summary>
    public const string RestNowhere = "rest-nowhere";
}
