namespace PartyRpg.Kit.Services;

/// <summary>The codes a service refuses a command with.</summary>
/// <remarks>
/// A code is what a caller and a test branch on and the message beside it is what a person reads, so every
/// refusal this mechanism can give is named here once rather than spelled at the point of use.
/// </remarks>
public static class ServiceCodes
{
    /// <summary>The refusal code <c>service-already-identified</c>.</summary>
    public const string ServiceAlreadyIdentified = "service-already-identified";

    /// <summary>The refusal code <c>service-barred</c>.</summary>
    public const string ServiceBarred = "service-barred";

    /// <summary>The refusal code <c>service-closed</c>.</summary>
    public const string ServiceClosed = "service-closed";

    /// <summary>The refusal code <c>service-count-invalid</c>.</summary>
    public const string ServiceCountInvalid = "service-count-invalid";

    /// <summary>The refusal code <c>service-debt-exceeded</c>.</summary>
    public const string ServiceDebtExceeded = "service-debt-exceeded";

    /// <summary>The refusal code <c>service-fare-unrouted</c>.</summary>
    public const string ServiceFareUnrouted = "service-fare-unrouted";

    /// <summary>The refusal code <c>service-holding-short</c>.</summary>
    public const string ServiceHoldingShort = "service-holding-short";

    /// <summary>The refusal code <c>service-item-worn</c>.</summary>
    public const string ServiceItemWorn = "service-item-worn";

    /// <summary>The refusal code <c>service-no-accounts</c>.</summary>
    public const string ServiceNoAccounts = "service-no-accounts";

    /// <summary>The refusal code <c>service-no-clock</c>.</summary>
    public const string ServiceNoClock = "service-no-clock";

    /// <summary>The refusal code <c>service-no-progression</c>.</summary>
    public const string ServiceNoProgression = "service-no-progression";

    /// <summary>The refusal code <c>service-no-rest</c>.</summary>
    public const string ServiceNoRest = "service-no-rest";

    /// <summary>The refusal code <c>service-no-such-item</c>.</summary>
    public const string ServiceNoSuchItem = "service-no-such-item";

    /// <summary>The refusal code <c>service-no-such-lesson</c>.</summary>
    public const string ServiceNoSuchLesson = "service-no-such-lesson";

    /// <summary>The refusal code <c>service-no-such-lot</c>.</summary>
    public const string ServiceNoSuchLot = "service-no-such-lot";

    /// <summary>The refusal code <c>service-no-such-member</c>.</summary>
    public const string ServiceNoSuchMember = "service-no-such-member";

    /// <summary>The refusal code <c>service-no-such-offer</c>.</summary>
    public const string ServiceNoSuchOffer = "service-no-such-offer";

    /// <summary>The refusal code <c>service-not-damaged</c>.</summary>
    public const string ServiceNotDamaged = "service-not-damaged";

    /// <summary>The refusal code <c>service-not-enough-stock</c>.</summary>
    public const string ServiceNotEnoughStock = "service-not-enough-stock";

    /// <summary>The refusal code <c>service-not-open</c>.</summary>
    public const string ServiceNotOpen = "service-not-open";

    /// <summary>The refusal code <c>service-offer-ambiguous</c>.</summary>
    public const string ServiceOfferAmbiguous = "service-offer-ambiguous";

    /// <summary>The refusal code <c>service-operation-unavailable</c>.</summary>
    public const string ServiceOperationUnavailable = "service-operation-unavailable";

    /// <summary>The refusal code <c>service-out-of-stock</c>.</summary>
    public const string ServiceOutOfStock = "service-out-of-stock";}
