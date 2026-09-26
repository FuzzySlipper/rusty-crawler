using PartyRpg.Kit.Magic;
using PartyRpg.Kit.Party;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// What each of this game's potions does when it is drunk, as a row of this game's own effect vocabulary.
/// </summary>
/// <remarks>
/// <para>
/// <b>The shipped table states the effect in words; the executable states the numbers.</b> <c>POTION.TXT</c>
/// names each potion and states what it is for — "Heal 10+skill HP", "Cast Haste", "Remove Weak cond" — and
/// the shipped item table's own notes say more ("Grants Haste (as the spell) for 30 minutes per point of
/// potion strength"). What none of it carries is what drinking one actually does to a character: that is a
/// switch in the executable (<c>OpenEnroth/src/Engine/Objects/Character.cpp:3080-3300</c>), and the donor's
/// transcription of it is what each row below states, with the donor's own line beside the number.
/// </para>
/// <para>
/// <b>These rows are the same kind of row the spell table carries, and they go through the same effect
/// path.</b> A potion's row states one of the eight effect categories and a <see cref="SpellReading"/> from
/// the same vocabulary a spell's row uses, so drinking a potion is a casting whose source is the item and
/// whose effect is applied by the same owner that applies a spell's — the fight's own path for harm, the
/// member's own pool for health and spell points, the member's own conditions for a cure or a poisoning, and
/// the running-effect ledger with a per-character deadline for everything that lasts. There is no second
/// table of what a potion does and no second place that applies it.
/// </para>
/// <para>
/// <b>What a potion's strength is read as.</b> Every duration and magnitude below is a function of the
/// potion's own strength rather than of any character's skill, which is the whole point of a potion: the
/// donor passes <c>potionStrength</c> where a spell passes the caster's school level. The effect path asks
/// <see cref="MightAndMagic7Spells.LevelOf"/> for the level of a casting, and that answers an item's own
/// strength when the casting came from one.
/// </para>
/// <para>
/// <b>The potions this build cannot express are stated, not faked.</b> A potion whose effect belongs to an
/// owner this build does not have — a score raised for a while, a weapon given a property, a score raised for
/// good, ageing undone — carries the same kind of named gap a spell of that shape carries, with the receiver
/// that would close it, and is refused by name before the bottle is spent.
/// </para>
/// </remarks>
internal static class MightAndMagic7Potions
{
    /// <summary>The first potion effect this game states: the catalyst, which is drunk as well as mixed.</summary>
    internal const int FirstPotion = 221;

    /// <summary>The last potion effect this game states.</summary>
    internal const int LastPotion = 271;

    /// <summary>The word this game gives a potion's "school", which no character trains.</summary>
    /// <remarks>
    /// A potion's row is read as a spell so that it can travel through the one casting workflow, and a spell
    /// stands in a school. Nothing gates a potion on a school — an item is how a character uses magic that is
    /// not theirs, and the mixing gate is the Alchemy rung its recipe states — so the school here is this
    /// game's own word for "carried by an item" and is deliberately not any of the nine a character can learn.
    /// </remarks>
    internal const string School = "Potion";

    /// <summary>The prefix an authored potion effect's identity carries, so it cannot collide with a spell's.</summary>
    private const string EffectPrefix = "potion:";

    /// <summary>The identity of the effect one potion is drunk as.</summary>
    /// <param name="potion">The potion's own row id, which is its item id.</param>
    /// <returns>The identity, as the catalog and a casting name it.</returns>
    internal static SpellId EffectId(int potion) =>
        new(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{EffectPrefix}{potion}"));

    /// <summary>One potion's authored effect: what category it is, what it is aimed at, and what it does.</summary>
    /// <param name="Id">The potion's own row id.</param>
    /// <param name="Effect">Which of the eight categories the row is read under.</param>
    /// <param name="Targeting">What the effect is aimed at, which is the character drinking it.</param>
    /// <param name="Reading">What it does inside its category, as the donor's own drinking switch states it.</param>
    internal readonly record struct PotionEffect(int Id, string Effect, SpellTargeting Targeting, SpellReading Reading);

    /// <summary>
    /// The potions that cure something, keep something off, or give something back, in the shipped table's own
    /// id order.
    /// </summary>
    /// <remarks>
    /// Each row is the donor's own case from the drinking switch: the condition list a cure resets, the amount
    /// a healing potion adds, the power and length of a buff. The three that state no numbers at all are stated
    /// as what this build cannot do with them and which owner could.
    /// </remarks>
    private static readonly PotionEffect[] Table =
    [
        // Catalyst: drunk on its own it poisons the drinker weakly. The donor's own case
        // (OpenEnroth src/Engine/Objects/Character.cpp:3087-3089) leaves the weak poison at severity one, and
        // the shipped item table's own description of the row — "Boost Potion" — says nothing about drinking
        // it at all, which is exactly why the executable is the source for this row.
        new(221, SpellEffects.Condition, SpellTargeting.Caster, Readings.Afflicts(MightAndMagic7Conditions.PoisonWeak, 1)),   // Catalyst

        // Cure Wounds: Heal(potionStrength + 10). Character.cpp:3091-3093.
        new(222, SpellEffects.Healing, SpellTargeting.Caster, Readings.Restore(perLevel: 1, flat: 10, byMastery: false)),   // Cure Wounds

        // Magic Potion: mana += potionStrength + 10, never past the maximum. Character.cpp:3095-3100.
        new(223, SpellEffects.Utility, SpellTargeting.Caster, Readings.RestoresMana(perLevel: 1, flat: 10)),   // Magic Potion

        // Cure Weakness: reset(CONDITION_WEAK). Character.cpp:3102-3104.
        new(224, SpellEffects.Condition, SpellTargeting.Caster, Readings.Cure(MightAndMagic7Conditions.Weak)),   // Cure Weakness

        // Cure Disease: reset of the three disease severities. Character.cpp:3106-3111.
        new(
            225,
            SpellEffects.Condition,
            SpellTargeting.Caster,
            Readings.Cure(MightAndMagic7Conditions.DiseaseSevere, MightAndMagic7Conditions.DiseaseMedium, MightAndMagic7Conditions.DiseaseWeak)),   // Cure Disease

        // Cure Poison: reset of the three poison severities. Character.cpp:3113-3118.
        new(
            226,
            SpellEffects.Condition,
            SpellTargeting.Caster,
            Readings.Cure(MightAndMagic7Conditions.PoisonSevere, MightAndMagic7Conditions.PoisonMedium, MightAndMagic7Conditions.PoisonWeak)),   // Cure Poison

        // Awaken: reset(CONDITION_SLEEP). Character.cpp:3120-3122.
        new(227, SpellEffects.Condition, SpellTargeting.Caster, Readings.Cure(MightAndMagic7Conditions.Sleep)),   // Awaken

        // Haste: the haste buff at mastery master, power five, for thirty minutes a point of strength.
        // Character.cpp:3124-3128.
        new(228, SpellEffects.Utility, SpellTargeting.Caster, Readings.Buff(SpellEffectIds.Haste, WardFormulas.Flat(5), WardFormulas.ThirtyMinutesPerPoint).OnOne()),   // Haste

        // Heroism: the same shape. Character.cpp:3130-3132.
        new(229, SpellEffects.Utility, SpellTargeting.Caster, Readings.Buff(SpellEffectIds.Heroism, WardFormulas.Flat(5), WardFormulas.ThirtyMinutesPerPoint).OnOne()),   // Heroism

        // Bless: the same shape. Character.cpp:3134-3136.
        new(230, SpellEffects.Utility, SpellTargeting.Caster, Readings.Buff(SpellEffectIds.Bless, WardFormulas.Flat(5), WardFormulas.ThirtyMinutesPerPoint).OnOne()),   // Bless

        // Preservation: the donor raises its preservation buff at three times the strength
        // (Character.cpp:3138-3142). This build's own spell of that name states the same gap — there is no
        // owner of an item's protection from harm — so the potion states it with the same receiver rather than
        // inventing an identity nothing reads.
        new(231, SpellEffects.Utility, SpellTargeting.Caster, Readings.NotYet("the party's gear protected from harm", "item state, which carries what a spell would protect")),   // Preservation

        // Shield: armour class at three times the strength, which is the same reading the donor's own cast
        // states (Character.cpp:3144-3149). Unlike the spell of the same name, which this build refuses as a
        // missile shield it cannot resolve, the potion's own buff is armour class and is applied.
        new(232, SpellEffects.Resistance, SpellTargeting.Caster, Readings.Armour(WardFormulas.ThreePerPoint, WardFormulas.ThirtyMinutesPerPoint).OnOne()),   // Shield

        // Recharge Item: an item's charges given back, which needs an owner that can aim at an item.
        // Character.cpp:3214-3220.
        new(233, SpellEffects.Utility, SpellTargeting.None, Readings.Unaimable("an item whose charges are given back", "an item-aim owner: the pack holds the party's items and nothing aims a potion at one")),   // Recharge Item

        // Stoneskin: the stone skin buff at power five. Character.cpp:3151-3155. The donor's buff is armour,
        // which is what this build's stone skin ward already carries.
        new(234, SpellEffects.Resistance, SpellTargeting.Caster, Readings.Armour(WardFormulas.Flat(5), WardFormulas.ThirtyMinutesPerPoint).OnOne()),   // Stoneskin

        // Water Breathing: the donor raises its water-walk buff (Character.cpp:3157-3160) and the shipped
        // description says it prevents drowning. This build's mover neither swims nor drowns.
        new(235, SpellEffects.Utility, SpellTargeting.Caster, Readings.NotYet("water breathed under rather than walked over", "the party's mover, which walks and falls and does nothing else")),   // Water Breathing

        // Harden Item: an item made harder to break, which needs an owner that can aim at an item and a
        // break rule to aim it at. Character.cpp:3222-3245.
        new(236, SpellEffects.Utility, SpellTargeting.None, Readings.Unaimable("an item made harder to break", "an item-aim owner: the pack holds the party's items and nothing aims a potion at one")),   // Harden Item

        // Remove Fear. Character.cpp:3162-3164.
        new(237, SpellEffects.Condition, SpellTargeting.Caster, Readings.Cure(MightAndMagic7Conditions.Fear)),   // Remove Fear

        // Remove Curse. Character.cpp:3166-3168.
        new(238, SpellEffects.Condition, SpellTargeting.Caster, Readings.Cure(MightAndMagic7Conditions.Cursed)),   // Remove Curse

        // Cure Insanity. Character.cpp:3170-3172.
        new(239, SpellEffects.Condition, SpellTargeting.Caster, Readings.Cure(MightAndMagic7Conditions.Insane)),   // Cure Insanity

        // The six temporary-score boosts: three times the strength for thirty minutes a point. The donor's own
        // cases (Character.cpp:3174-3212) raise a score on the character rather than on the party, and this
        // build's fights read scores from a character's own attributes, which no effect can raise yet — the
        // same gap the shipped Hour of Power and Day of the Gods carry, with the same receiver.
        new(240, SpellEffects.Utility, SpellTargeting.Caster, Readings.NotYet("Might raised for a while", "the attribute readings a fight is priced by")),   // Might Boost
        new(241, SpellEffects.Utility, SpellTargeting.Caster, Readings.NotYet("Intellect raised for a while", "the attribute readings a fight is priced by")),   // Intellect Boost
        new(242, SpellEffects.Utility, SpellTargeting.Caster, Readings.NotYet("Personality raised for a while", "the attribute readings a fight is priced by")),   // Personality Boost
        new(243, SpellEffects.Utility, SpellTargeting.Caster, Readings.NotYet("Endurance raised for a while", "the attribute readings a fight is priced by")),   // Endurance Boost
        new(244, SpellEffects.Utility, SpellTargeting.Caster, Readings.NotYet("Speed raised for a while", "the attribute readings a fight is priced by")),   // Speed Boost
        new(245, SpellEffects.Utility, SpellTargeting.Caster, Readings.NotYet("Accuracy raised for a while", "the attribute readings a fight is priced by")),   // Accuracy Boost

        // The weapon potions: a property added to a weapon for a while. Character.cpp:3247-3290, where each is
        // a temporary enchantment on the item in hand — the owner that would apply them is the item-aim owner
        // the two item potions above already name.
        new(246, SpellEffects.Utility, SpellTargeting.None, Readings.Unaimable("a weapon given the property of flame", "an item-aim owner: the pack holds the party's items and nothing aims a potion at one")),   // Flaming Potion
        new(247, SpellEffects.Utility, SpellTargeting.None, Readings.Unaimable("a weapon given the property of frost", "an item-aim owner: the pack holds the party's items and nothing aims a potion at one")),   // Freezing Potion
        new(248, SpellEffects.Utility, SpellTargeting.None, Readings.Unaimable("a weapon given the property of poison", "an item-aim owner: the pack holds the party's items and nothing aims a potion at one")),   // Noxious Potion
        new(249, SpellEffects.Utility, SpellTargeting.None, Readings.Unaimable("a weapon given the property of sparks", "an item-aim owner: the pack holds the party's items and nothing aims a potion at one")),   // Shocking Potion
        new(250, SpellEffects.Utility, SpellTargeting.None, Readings.Unaimable("a weapon given the property of swiftness", "an item-aim owner: the pack holds the party's items and nothing aims a potion at one")),   // Swift Potion

        // Cure Paralysis. Character.cpp:3292-3294.
        new(251, SpellEffects.Condition, SpellTargeting.Caster, Readings.Cure(MightAndMagic7Conditions.Paralyzed)),   // Cure Paralysis

        // Divine Restoration: every condition except the three that are a body's state rather than an
        // affliction. Character.cpp:3296-3308, where the dead, the petrified, and the eradicated are put back
        // after the reset — which is the same list this row states.
        new(
            252,
            SpellEffects.Condition,
            SpellTargeting.Caster,
            Readings.Cure(
                MightAndMagic7Conditions.Weak,
                MightAndMagic7Conditions.Sleep,
                MightAndMagic7Conditions.Fear,
                MightAndMagic7Conditions.Drunk,
                MightAndMagic7Conditions.Insane,
                MightAndMagic7Conditions.PoisonWeak,
                MightAndMagic7Conditions.PoisonMedium,
                MightAndMagic7Conditions.PoisonSevere,
                MightAndMagic7Conditions.DiseaseWeak,
                MightAndMagic7Conditions.DiseaseMedium,
                MightAndMagic7Conditions.DiseaseSevere,
                MightAndMagic7Conditions.Paralyzed,
                MightAndMagic7Conditions.Unconscious,
                MightAndMagic7Conditions.Cursed,
                MightAndMagic7Conditions.Zombie,
                MightAndMagic7Conditions.Good)),   // Divine Restoration

        // Divine Cure: Heal(5 * potionStrength). Character.cpp:3310-3312.
        new(253, SpellEffects.Healing, SpellTargeting.Caster, Readings.Restore(perLevel: 5, flat: 0, byMastery: false)),   // Divine Cure

        // Divine Power: mana += 5 * potionStrength. Character.cpp:3314-3319.
        new(254, SpellEffects.Utility, SpellTargeting.Caster, Readings.RestoresMana(perLevel: 5, flat: 0)),   // Divine Power

        // Luck Boost: three times the strength, read as the luck a saving throw uses, which is the effect the
        // shipped Fate spell already carries. Character.cpp:3321-3326.
        new(255, SpellEffects.Utility, SpellTargeting.Caster, Readings.Buff(SpellEffectIds.Fate, WardFormulas.ThreePerPoint, WardFormulas.ThirtyMinutesPerPoint).OnOne()),   // Luck Boost

        // The six resistance potions: three times the strength for thirty minutes a point, each against its own
        // kind of harm. Character.cpp:3328-3372.
        new(256, SpellEffects.Resistance, SpellTargeting.Caster, Readings.Ward([MightAndMagic7Damage.Fire], WardFormulas.ThreePerPoint, WardFormulas.ThirtyMinutesPerPoint).OnOne()),   // Fire Resistance
        new(257, SpellEffects.Resistance, SpellTargeting.Caster, Readings.Ward([MightAndMagic7Damage.Air], WardFormulas.ThreePerPoint, WardFormulas.ThirtyMinutesPerPoint).OnOne()),   // Air Resistance
        new(258, SpellEffects.Resistance, SpellTargeting.Caster, Readings.Ward([MightAndMagic7Damage.Water], WardFormulas.ThreePerPoint, WardFormulas.ThirtyMinutesPerPoint).OnOne()),   // Water Resistance
        new(259, SpellEffects.Resistance, SpellTargeting.Caster, Readings.Ward([MightAndMagic7Damage.Earth], WardFormulas.ThreePerPoint, WardFormulas.ThirtyMinutesPerPoint).OnOne()),   // Earth Resistance
        new(260, SpellEffects.Resistance, SpellTargeting.Caster, Readings.Ward([MightAndMagic7Damage.Mind], WardFormulas.ThreePerPoint, WardFormulas.ThirtyMinutesPerPoint).OnOne()),   // Mind Resistance
        new(261, SpellEffects.Resistance, SpellTargeting.Caster, Readings.Ward([MightAndMagic7Damage.Body], WardFormulas.ThreePerPoint, WardFormulas.ThirtyMinutesPerPoint).OnOne()),   // Body Resistance

        // Stone to Flesh. Character.cpp:3374-3376.
        new(262, SpellEffects.Condition, SpellTargeting.Caster, Readings.Cure(MightAndMagic7Conditions.Petrified)),   // Stone to Flesh

        // Slaying Potion: 'of dragon slaying' on a weapon, for good rather than for a while.
        // Character.cpp:3378-3382.
        new(263, SpellEffects.Utility, SpellTargeting.None, Readings.Unaimable("a weapon made deadly to dragons", "an item-aim owner: the pack holds the party's items and nothing aims a potion at one")),   // Slaying Potion

        // The seven pure potions: fifty to a score, for good, once each. Character.cpp:3384-3400, where the
        // donor records that the character has already had that one. A permanent score change is
        // progression's business rather than an effect's.
        new(264, SpellEffects.Utility, SpellTargeting.Caster, Readings.NotYet("Luck raised for good", "progression, which owns a character's attributes and their growth")),   // Pure Luck
        new(265, SpellEffects.Utility, SpellTargeting.Caster, Readings.NotYet("Speed raised for good", "progression, which owns a character's attributes and their growth")),   // Pure Speed
        new(266, SpellEffects.Utility, SpellTargeting.Caster, Readings.NotYet("Intellect raised for good", "progression, which owns a character's attributes and their growth")),   // Pure Intellect
        new(267, SpellEffects.Utility, SpellTargeting.Caster, Readings.NotYet("Endurance raised for good", "progression, which owns a character's attributes and their growth")),   // Pure Endurance
        new(268, SpellEffects.Utility, SpellTargeting.Caster, Readings.NotYet("Personality raised for good", "progression, which owns a character's attributes and their growth")),   // Pure Personality
        new(269, SpellEffects.Utility, SpellTargeting.Caster, Readings.NotYet("Accuracy raised for good", "progression, which owns a character's attributes and their growth")),   // Pure Accuracy
        new(270, SpellEffects.Utility, SpellTargeting.Caster, Readings.NotYet("Might raised for good", "progression, which owns a character's attributes and their growth")),   // Pure Might

        // Rejuvenation: the age modifier set to zero. Character.cpp:3402-3404.
        new(271, SpellEffects.Utility, SpellTargeting.Caster, Readings.NotYet("unnatural ageing undone", "progression, which owns a character's age")),   // Rejuvenation
    ];

    /// <summary>Every potion effect this game states, in the shipped table's own id order.</summary>
    internal static IReadOnlyList<PotionEffect> Effects => Table;

    /// <summary>
    /// Every potion as the coverage report reads it: what it is drunk as, and how far this build expresses it.
    /// </summary>
    /// <remarks>
    /// The rows are read from the same table the effect path applies by, exactly as the spells' rows are, so
    /// the report cannot claim a potion does something the path does not do. A potion whose reading names what
    /// this build cannot express is <c>not yet</c> and names the owner that would close it; a reading whose
    /// numbers are coarser than the game's is <c>approximated</c> and says how.
    /// </remarks>
    internal static IReadOnlyList<PotionRowReading> Rows =>
        [.. Table.Select(effect => new PotionRowReading(
            effect.Id,
            effect.Effect,
            SpellTargetings.WireName(effect.Targeting),
            CoverageOf(effect)))];

    /// <summary>How far this build expresses one potion's effect, and what that leaves out.</summary>
    /// <param name="effect">The potion's own row.</param>
    /// <returns>How far it is expressed.</returns>
    internal static SpellEffectCoverage CoverageOf(PotionEffect effect)
    {
        SpellReading reading = effect.Reading;
        if (reading.Missing.Length > 0) return SpellEffectCoverage.NotYet(reading.Missing, reading.Receiver);
        if (reading.Divergence.Length > 0) return SpellEffectCoverage.Approximated(reading.Divergence);
        return SpellEffectCoverage.Implemented(Expressed(effect));
    }

    /// <summary>What a potion this build expresses does, and through which owner.</summary>
    private static string Expressed(PotionEffect effect)
    {
        SpellReading reading = effect.Reading;
        if (reading.Leaves is { } leaves)
        {
            return $"a condition left on the character drinking it through their own condition state ({leaves})";
        }

        if (reading.ManaPerLevel > 0 || reading.ManaBase > 0)
        {
            return "spell points given back through the member's own pool, at the potion's own strength";
        }

        return effect.Effect switch
        {
            SpellEffects.Healing => reading.Healing == HealingMode.Fill
                ? "every pool filled and every condition lifted through the member's own state"
                : "hit points restored through the member's own pool, at the potion's own strength",
            SpellEffects.Condition => "the conditions the potion names lifted from the drinker's own condition state",
            SpellEffects.Resistance => reading.Ward is { Armour: true }
                ? "armour class carried by the character and read by the fight's own armour class, with a deadline on the one clock"
                : "a resistance carried by the character and read by the fight's own resistance sum, with a deadline on the one clock",
            SpellEffects.Utility => reading.Buff is { } buff
                ? $"a carried effect of its own identity ({buff.Effect}) on that character, read where it applies and ended by a deadline on the one clock"
                : "a carried effect, read where it applies",
            _ => "the potion's own category, applied where that category is applied",
        };
    }

    /// <summary>One potion as the coverage report lists it.</summary>
    /// <param name="Id">The potion's own row id.</param>
    /// <param name="Effect">Which of the eight categories the row is read under.</param>
    /// <param name="Targeting">What the effect is aimed at, as the wire spells it.</param>
    /// <param name="Coverage">How far this build expresses it, and what that leaves out.</param>
    internal readonly record struct PotionRowReading(int Id, string Effect, string Targeting, SpellEffectCoverage Coverage);

    /// <summary>How many potion effects this game states.</summary>
    internal static int Count => Table.Length;

    /// <summary>The effect one potion's row states, or null when this game states none for that row.</summary>
    /// <param name="potion">The potion's own row id.</param>
    /// <returns>The effect, or null when the game states none.</returns>
    internal static PotionEffect? Row(int potion) =>
        potion >= FirstPotion && potion <= LastPotion ? Table[potion - FirstPotion] : null;
}
