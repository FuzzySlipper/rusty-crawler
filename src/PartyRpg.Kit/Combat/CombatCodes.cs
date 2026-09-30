namespace PartyRpg.Kit.Combat;

/// <summary>The codes a fight refuses an order with.</summary>
/// <remarks>
/// A code is what a caller and a test branch on and the message beside it is what a person reads, so every
/// refusal this mechanism can give is named here once rather than spelled at the point of use.
/// </remarks>
public static class CombatCodes
{
    /// <summary>The refusal code <c>friendly-target</c>.</summary>
    public const string FriendlyTarget = "friendly-target";

    /// <summary>The refusal code <c>incapacitated</c>.</summary>
    public const string Incapacitated = "incapacitated";

    /// <summary>The refusal code <c>recovering</c>.</summary>
    public const string Recovering = "recovering";

    /// <summary>The refusal code <c>unknown-combatant</c>.</summary>
    public const string UnknownCombatant = "unknown-combatant";

    /// <summary>The refusal code <c>unknown-target</c>.</summary>
    public const string UnknownTarget = "unknown-target";

    /// <summary>The refusal code <c>weapon-no-target</c>.</summary>
    public const string WeaponNoTarget = "weapon-no-target";
}
