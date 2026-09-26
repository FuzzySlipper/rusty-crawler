using PartyRpg.Kit.Party;
using PartyRpg.Kit.Services;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// This game's conditions, and what a temple charges to end them.
/// </summary>
/// <remarks>
/// <para>
/// <b>The conditions are the game's, the state is the party's.</b> The party model holds a condition
/// identity and a severity and knows nothing about either; this names the nineteen the game has and groups
/// them into the families a temple prices. The names are the donor's own condition list
/// (OpenEnroth <c>src/Engine/Objects/CharacterEnums.h:5-28</c>, <c>Condition</c>), which is where the
/// shipped content's conditions live: the tables carry none, so this is authored content with a donor
/// citation rather than a reading of the operator's data.
/// </para>
/// <para>
/// <b>What a temple claims.</b> The donor's temple heals every condition a character suffers and fills both
/// pools at once, priced by the worst condition (<c>src/GUI/UI/Houses/Temple.cpp:33-81</c>,
/// <c>PriceCalculator::templeHealingCostForPlayer</c>): ordinary afflictions cost their days times the
/// shop's multiplier, death and petrification five times that, eradication ten. This splits that one act by
/// family so a counter offers a cure per family — which is what makes "remove every condition it claims,
/// including death and eradication" a list a player reads rather than a single button whose price nobody
/// can see — and keeps the donor's multipliers for the price.
/// </para>
/// <para>
/// <b>Severity is what the donor measures in days.</b> The donor multiplies by how many days a condition
/// has been suffered, which this party model does not record; a condition's severity stands in for that
/// span, so a more severe case costs more and a fresh one costs the base. That is a divergence, and it is
/// stated here rather than hidden: a party model that recorded when a condition began would restore the
/// donor's own multiplier.
/// </para>
/// </remarks>
internal static class MightAndMagic7Conditions
{
    /// <summary>A member's condition is named by content, and these are the names this game gives them.</summary>
    internal static readonly ConditionId Cursed = new("Cursed");

    /// <summary>The weakened condition a hungry party and a worn-off spell leave.</summary>
    internal static readonly ConditionId Weak = new("Weak");

    /// <summary>Magical sleep.</summary>
    internal static readonly ConditionId Sleep = new("Sleep");

    /// <summary>Magical fear.</summary>
    internal static readonly ConditionId Fear = new("Fear");

    /// <summary>Drunkenness.</summary>
    internal static readonly ConditionId Drunk = new("Drunk");

    /// <summary>Insanity.</summary>
    internal static readonly ConditionId Insane = new("Insane");

    /// <summary>Mild poison.</summary>
    internal static readonly ConditionId PoisonWeak = new("Poison Weak");

    /// <summary>Mild disease.</summary>
    internal static readonly ConditionId DiseaseWeak = new("Disease Weak");

    /// <summary>Medium poison.</summary>
    internal static readonly ConditionId PoisonMedium = new("Poison Medium");

    /// <summary>Medium disease.</summary>
    internal static readonly ConditionId DiseaseMedium = new("Disease Medium");

    /// <summary>Severe poison.</summary>
    internal static readonly ConditionId PoisonSevere = new("Poison Severe");

    /// <summary>Severe disease.</summary>
    internal static readonly ConditionId DiseaseSevere = new("Disease Severe");

    /// <summary>Paralysis.</summary>
    internal static readonly ConditionId Paralyzed = new("Paralyzed");

    /// <summary>Unconsciousness, which is one step short of death.</summary>
    internal static readonly ConditionId Unconscious = new("Unconscious");

    /// <summary>Death, which a temple can undo and a party cannot.</summary>
    internal static readonly ConditionId Dead = new("Dead");

    /// <summary>Petrification, which a temple undoes with death.</summary>
    internal static readonly ConditionId Petrified = new("Petrified");

    /// <summary>Eradication, the one condition a temple charges dearest for.</summary>
    internal static readonly ConditionId Eradicated = new("Eradicated");

    /// <summary>The zombie state a body raised at a temple of the dark powers comes back in.</summary>
    internal static readonly ConditionId Zombie = new("Zombie");

    /// <summary>Good health, which the donor lists as a condition and which no counter removes.</summary>
    internal static readonly ConditionId Good = new("Good");

    /// <summary>Every condition a temple can end, in the order a panel reads them.</summary>
    /// <summary>
    /// Whether this game's own conditions leave a character able to act at all.
    /// </summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/Objects/Character.cpp:350-357</c> (<c>Character::CanAct</c>): sleep,
    /// paralysis, unconsciousness, death, petrification, and eradication stop a character acting, and the
    /// weaker conditions do not. This is the one answer the fight's own gate and the mixing workflow both
    /// read, so a character a blow laid out can neither swing nor mix a potion.
    /// </remarks>
    /// <param name="member">The character whose conditions are read.</param>
    /// <returns>Whether they may act.</returns>
    internal static bool CanAct(PartyMember member)
    {
        ArgumentNullException.ThrowIfNull(member);
        return !member.Conditions.Has(Sleep) &&
               !member.Conditions.Has(Paralyzed) &&
               !member.Conditions.Has(Unconscious) &&
               !member.Conditions.Has(Dead) &&
               !member.Conditions.Has(Petrified) &&
               !member.Conditions.Has(Eradicated);
    }

    internal static readonly IReadOnlyList<ConditionId> Afflictions =
    [
        Cursed, Weak, Sleep, Fear, Drunk, Insane,
        PoisonWeak, DiseaseWeak, PoisonMedium, DiseaseMedium, PoisonSevere, DiseaseSevere,
        Paralyzed, Unconscious,
    ];

    /// <summary>The conditions a night's rest ends, which is what a room clears and a cure need not.</summary>
    /// <remarks>
    /// <para>
    /// The donor's own list (OpenEnroth <c>src/Engine/Party.cpp:717-721</c>, <c>Party::restAndHeal</c>): a
    /// completed rest clears unconsciousness, drunkenness, fear, sleep, and weakness, then fills both pools.
    /// Everything else — poison, disease, insanity, a curse, paralysis, and the three conditions a temple
    /// charges dearest for — is left standing, which is why a room is cheaper than a cure and why the two are
    /// different offers.
    /// </para>
    /// <para>
    /// <b>A character the donor will not rest is skipped rather than cleared.</b> <c>Party.cpp:713-715</c>
    /// skips a dead, petrified, or eradicated character entirely: a night does not fill their pools and does
    /// not wake them. None of the three is on this list, so a night leaves them exactly as they were.
    /// </para>
    /// </remarks>
    internal static readonly IReadOnlyList<ConditionId> RestClears = [Unconscious, Drunk, Fear, Sleep, Weak];

    /// <summary>The multiplier the donor charges for death and petrification.</summary>
    internal const int SeriousMultiplier = 5;

    /// <summary>The multiplier the donor charges for eradication.</summary>
    internal const int EradicatedMultiplier = 10;

    /// <summary>The multiplier the donor charges for every other condition.</summary>
    internal const int OrdinaryMultiplier = 1;

    /// <summary>The most a temple's healing can cost, which is the donor's own ceiling.</summary>
    internal const int MaximumHealingPrice = 10000;

    /// <summary>
    /// What one temple offers: a cure per condition family, in the order the families are priced.
    /// </summary>
    /// <remarks>
    /// Each offer names the conditions it ends, so the mechanism clears exactly what the counter claims and
    /// a panel can show it. The base value is the donor's family multiplier, and the price rule turns that
    /// and the member's own severity into coins.
    /// </remarks>
    /// <param name="conditions">The conditions a member is suffering, which decides what is offered.</param>
    internal static IReadOnlyList<ServiceOffer> Cures(IReadOnlyList<ActiveCondition> conditions)
    {
        List<ServiceOffer> offers = [];
        if (conditions.Any(condition => Afflictions.Contains(condition.Condition)))
        {
            offers.Add(new ServiceOffer(
                ServiceOfferKind.Cure,
                "Healing",
                Subject: "affliction",
                Value: OrdinaryMultiplier,
                Amount: 1,
                Clears: Afflictions));
        }

        List<ConditionId> serious = [];
        if (conditions.Any(condition => condition.Condition == Dead)) serious.Add(Dead);
        if (conditions.Any(condition => condition.Condition == Petrified)) serious.Add(Petrified);
        if (serious.Count > 0)
        {
            offers.Add(new ServiceOffer(
                ServiceOfferKind.Cure,
                "Raise the dead",
                Subject: "death",
                Value: SeriousMultiplier,
                Amount: 1,
                Clears: serious));
        }

        if (conditions.Any(condition => condition.Condition == Eradicated))
        {
            offers.Add(new ServiceOffer(
                ServiceOfferKind.Cure,
                "Restore the eradicated",
                Subject: "eradication",
                Value: EradicatedMultiplier,
                Amount: 1,
                Clears: [Eradicated]));
        }

        return offers;
    }

    /// <summary>
    /// What a temple charges to end the conditions one offer claims, as the donor prices the worst of them.
    /// </summary>
    /// <param name="offer">The cure being priced.</param>
    /// <param name="conditions">The conditions the member is suffering.</param>
    /// <param name="priceMultiplier">The temple's own multiplier from the building table.</param>
    internal static int HealingPrice(ServiceOffer offer, IReadOnlyList<ActiveCondition> conditions, double priceMultiplier)
    {
        int multiplier = offer.Value < 1 ? OrdinaryMultiplier : offer.Value;
        int severity = 1;
        foreach (ActiveCondition condition in conditions)
        {
            if (!offer.ClearsCondition(condition.Condition)) continue;
            severity = Math.Max(severity, Math.Max(1, condition.Severity));
        }

        return Math.Clamp(ServicePricing.Coins(severity, multiplier, priceMultiplier), 1, MaximumHealingPrice);
    }
}
