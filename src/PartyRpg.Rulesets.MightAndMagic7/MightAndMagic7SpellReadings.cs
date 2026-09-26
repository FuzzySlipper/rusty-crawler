using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Magic;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>How a healing spell gives health back.</summary>
/// <remarks>
/// The donor's healing spells are not one shape: a cure gives an amount, a shared life pools the party's
/// health and hands each member the mean, a divine intervention fills everything, and the two raising spells
/// put a body back on its feet at one hit point and lift what laid it out
/// (<c>OpenEnroth/src/Engine/Spells/CastSpellInfo.cpp:2244-2262, 1817-1828, 1840-1880, 2599</c>). The shape is
/// read from the spell's own row, which is what keeps one healing path from pretending four spells are one.
/// </remarks>
internal enum HealingMode
{
    /// <summary>The row states no healing, which is every spell of another category.</summary>
    None,

    /// <summary>An amount of hit points, on one member or on the whole band.</summary>
    Restore,

    /// <summary>The party's health is pooled and shared, which is Shared Life's own arithmetic.</summary>
    Share,

    /// <summary>Both pools are filled, which is what a divine intervention does.</summary>
    Fill,

    /// <summary>A laid-out member is put back on their feet and what laid them out is lifted.</summary>
    Raise,
}

/// <summary>What a travel spell does with the party, which is a portal or a way of moving.</summary>
internal enum TravelShape
{
    /// <summary>The row states no travel, which is every spell of another category.</summary>
    None,

    /// <summary>A portal to a place the party names, taken through the world's own transition path.</summary>
    Portal,

    /// <summary>A party-carried beacon, set where the party stands and recalled to later.</summary>
    Beacon,

    /// <summary>Flight, water-walking, a jump, or a feather fall: a way of moving this build's mover has not.</summary>
    Movement,
}

/// <summary>What a detection spell reports over.</summary>
internal enum DetectionScope
{
    /// <summary>The row states no detection, which is every spell of another category.</summary>
    None,

    /// <summary>The places the party knows and what stands in the one it is in.</summary>
    Places,

    /// <summary>Everything alive in the party's own place.</summary>
    Life,

    /// <summary>Who is in the party's own place, by name.</summary>
    Minds,
}

/// <summary>A ward's strength and length, as the donor's own formulas over the caster's school.</summary>
/// <remarks>
/// The power and the duration are functions of the caster's level in the spell's school and its mastery rung
/// because that is exactly what the donor's casts are: the six protections set
/// <c>spell_power = skillLevel * mastery</c> and <c>spell_length = hours(skillLevel)</c>, a stone skin sets
/// <c>skillLevel + 5</c> over the shield family's duration, and a day of protection sets four or five per
/// level over four or five hours per level (<c>OpenEnroth/src/Engine/Spells/Spells.cpp:728-762</c> and
/// <c>CastSpellInfo.cpp:2517-2603</c>, <c>:900-945</c>). Naming each shape once is what keeps the table's rows
/// readable as the donor's own columns.
/// </remarks>
internal static class WardFormulas
{
    /// <summary>The donor's protection spells: the skill's level times the mastery rung.</summary>
    internal static readonly Func<int, int, int> MasteryTimesLevel = (level, mastery) => level * mastery;

    /// <summary>A flat power plus a stated amount per level.</summary>
    internal static Func<int, int, int> LevelPlus(int perLevel, int flat) => (level, _) => (level * perLevel) + flat;

    /// <summary>The donor's day of protection at master: four per level.</summary>
    internal static readonly Func<int, int, int> FourPerLevel = LevelPlus(perLevel: 4, flat: 0);

    /// <summary>The donor's day of protection at grand master: five per level.</summary>
    internal static readonly Func<int, int, int> FivePerLevel = LevelPlus(perLevel: 5, flat: 0);

    /// <summary>The donor's protection spells: one hour per level of the school.</summary>
    internal static readonly Func<int, int, GameDuration> HoursPerLevel = (level, _) => GameDuration.FromHours(level);

    /// <summary>
    /// The shield family's duration, which the donor states three ways by mastery.
    /// </summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/Spells/CastSpellInfo.cpp:907-921</c>: an hour plus five minutes a level below
    /// master, an hour plus fifteen minutes a level at master, and one hour per level plus one at grand
    /// master — the last of which the donor writes as <c>hours(spell_level + 1)</c>.
    /// </remarks>
    internal static readonly Func<int, int, GameDuration> HourAndMinutesByMastery = (level, mastery) => mastery switch
    {
        <= 2 => GameDuration.FromHours(1) + GameDuration.FromMinutes(5 * level),
        3 => GameDuration.FromHours(1) + GameDuration.FromMinutes(15 * level),
        _ => GameDuration.FromHours(level + 1),
    };

    /// <summary>
    /// Haste's own duration, which the donor states three ways by mastery.
    /// </summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/Spells/Spells.cpp:676-688</c>: an hour plus a minute a level below master,
    /// three at master, and four at grand master.
    /// </remarks>
    internal static readonly Func<int, int, GameDuration> HourAndFewMinutesByMastery = (level, mastery) => mastery switch
    {
        <= 2 => GameDuration.FromHours(1) + GameDuration.FromMinutes(level),
        3 => GameDuration.FromHours(1) + GameDuration.FromMinutes(3 * level),
        _ => GameDuration.FromHours(1) + GameDuration.FromMinutes(4 * level),
    };

    /// <summary>A power that does not vary with the caster: what a fixed reduction or a flag is worth.</summary>
    internal static Func<int, int, int> Flat(int value) => (_, _) => value;

    /// <summary>The donor's invisibility at master: ten minutes per level of the school.</summary>
    internal static readonly Func<int, int, GameDuration> TenMinutesPerLevel = (level, _) => GameDuration.FromMinutes(10 * level);

    /// <summary>The donor's own five minutes, which is all the fate it grants lasts.</summary>
    internal static readonly Func<int, int, GameDuration> FiveMinutes = (_, _) => GameDuration.FromMinutes(5);

    /// <summary>
    /// Fate's own power, which the donor states as one, two, four, or six per level by mastery.
    /// </summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/Spells/CastSpellInfo.cpp:1634-1650</c>: the luck a fate grants is the school's
    /// level times one at novice, two at expert, four at master, and six at grand master — which is not the
    /// mastery times the level that the protections use, and is why it is stated as its own shape.
    /// </remarks>
    internal static readonly Func<int, int, int> FatePower = (level, mastery) => level * mastery switch
    {
        <= 1 => 1,
        2 => 2,
        3 => 4,
        _ => 6,
    };

    /// <summary>The donor's day of protection at master: four hours per level.</summary>
    internal static readonly Func<int, int, GameDuration> FourHoursPerLevel = (level, _) => GameDuration.FromHours(4 * level);

    /// <summary>The donor's day of protection at grand master: five hours per level.</summary>
    internal static readonly Func<int, int, GameDuration> FiveHoursPerLevel = (level, _) => GameDuration.FromHours(5 * level);
}

/// <summary>A ward one spell leaves on the party: what it takes a share of, how strongly, and for how long.</summary>
/// <param name="Kinds">Which kinds of harm the ward takes a share of.</param>
/// <param name="Armour">Whether the ward is armour class rather than a resistance, which is the donor's stone skin.</param>
/// <param name="Power">What the ward is worth at the caster's school level and mastery.</param>
/// <param name="Lasts">How long the ward lasts at the caster's school level and mastery.</param>
internal readonly record struct WardReading(
    DamageKindId[] Kinds,
    bool Armour,
    Func<int, int, int> Power,
    Func<int, int, GameDuration> Lasts);

/// <summary>A party-carried effect a utility spell leaves, with its power and its length.</summary>
/// <param name="Effect">The state the spell leaves, which the reading that honours it also names.</param>
/// <param name="Power">What the effect is worth at the caster's school level and mastery.</param>
/// <param name="Lasts">How long it lasts at the caster's school level and mastery.</param>
internal readonly record struct BuffReading(EffectId Effect, Func<int, int, int> Power, Func<int, int, GameDuration> Lasts);

/// <summary>Everything a spell does besides harming, as its own row states it.</summary>
/// <remarks>
/// <para>
/// <b>A row is read once and applied by category.</b> The eight categories are the table's own effect column;
/// what each spell does <em>inside</em> its category is this reading — the shape of its healing, the condition
/// it lifts or leaves, the ward it raises, the effect it carries, the place a portal reaches, what a detection
/// reports, and, for a spell this build cannot apply, what is missing and who owns it. The effect paths read
/// their own field of this and never a spell's identity, which is what keeps ninety-nine rows from becoming
/// ninety-nine code paths.
/// </para>
/// <para>
/// <b>A missing reading names its receiver.</b> A utility spell whose state nothing reads, a travel spell the
/// mover cannot express, and an item-aimed spell with nothing to aim at carry the owner that would close the
/// gap. That is what makes the coverage report a set of routed facts rather than a list of apologies.
/// </para>
/// </remarks>
/// <param name="Healing">How the spell gives health back, if it does.</param>
/// <param name="HealBase">The flat part of the healing amount.</param>
/// <param name="HealPerLevel">How much each level of the school adds, before the mastery multiplier.</param>
/// <param name="HealByMastery">Whether the caster's mastery multiplies the per-level part, as the donor's first aid does.</param>
/// <param name="Clears">The conditions the spell lifts from the member it is cast on.</param>
/// <param name="Lifts">The conditions that laid a member out, which a raising spell lifts as it stands them up.</param>
/// <param name="Inflicts">The condition the spell leaves on its target, when that target is not a party member.</param>
/// <param name="Ward">The ward the spell raises, when it raises one.</param>
/// <param name="Buff">The party-carried effect the spell leaves, when it leaves one.</param>
/// <param name="Travel">What the spell does with the party's place.</param>
/// <param name="Detection">What the spell reports over.</param>
/// <param name="Dispels">Whether the spell ends the effects other spells have left running.</param>
/// <param name="Weakness">How weak a raising spell leaves the member it stands up, or null when it leaves none.</param>
/// <param name="Unaimable">Whether the spell acts on something this build has no way to aim at, so a casting is refused before it is paid for.</param>
/// <param name="Divergence">How this build's reading of the spell is coarser than the game's, empty when it is not.</param>
/// <param name="Missing">What this build cannot apply for the spell, empty when it applies all of it.</param>
/// <param name="Receiver">Who owns what is missing, empty when nothing is.</param>
internal readonly record struct SpellReading(
    HealingMode Healing,
    int HealBase,
    int HealPerLevel,
    bool HealByMastery,
    ConditionId[]? Clears,
    ConditionId[]? Lifts,
    ConditionId? Inflicts,
    WardReading? Ward,
    BuffReading? Buff,
    TravelShape Travel,
    DetectionScope Detection,
    bool Dispels,
    int? Weakness,
    bool Unaimable,
    string Divergence,
    string Missing,
    string Receiver)
{
    /// <summary>The reading of a spell whose category this field does not describe.</summary>
    internal static readonly SpellReading None = new(
        HealingMode.None,
        HealBase: 0,
        HealPerLevel: 0,
        HealByMastery: false,
        Clears: null,
        Lifts: null,
        Inflicts: null,
        Ward: null,
        Buff: null,
        Travel: TravelShape.None,
        Detection: DetectionScope.None,
        Dispels: false,
        Weakness: null,
        Unaimable: false,
        Divergence: string.Empty,
        Missing: string.Empty,
        Receiver: string.Empty);
}

/// <summary>The readings a spell's own row is written with, one factory per shape the table states.</summary>
/// <remarks>
/// Each factory is a reading the donor's own cast states, so the table reads as a column of the original's
/// behaviour rather than as a column of switches: an amount and a mastery multiplier for a cure, a list of
/// conditions for a curing spell, a power and a duration for a ward, and a named gap for a spell this build
/// cannot apply.
/// </remarks>
internal static class Readings
{
    /// <summary>A cure of a stated amount, scaled by the caster's mastery of the school.</summary>
    internal static SpellReading Restore(int perLevel, int flat, bool byMastery) =>
        SpellReading.None with { Healing = HealingMode.Restore, HealPerLevel = perLevel, HealBase = flat, HealByMastery = byMastery };

    /// <summary>A cure of the whole band, which is what the donor's power cure is.</summary>
    internal static SpellReading RestoreParty(int perLevel, int flat) =>
        SpellReading.None with { Healing = HealingMode.Restore, HealPerLevel = perLevel, HealBase = flat, HealByMastery = false };

    /// <summary>The party's health pooled and shared, which is Shared Life's own arithmetic.</summary>
    internal static SpellReading Share(int perLevel) =>
        SpellReading.None with { Healing = HealingMode.Share, HealPerLevel = perLevel };

    /// <summary>Every pool filled and every condition lifted, which is what a divine intervention does.</summary>
    internal static SpellReading Fill() => SpellReading.None with { Healing = HealingMode.Fill };

    /// <summary>A member stood back up at one hit point, left as weak as the spell leaves them.</summary>
    /// <param name="weakness">How weak the raising leaves them, which the donor states per spell.</param>
    /// <param name="lifts">The conditions that laid them out, which the raising lifts.</param>
    internal static SpellReading Raise(int weakness, params ConditionId[] lifts) =>
        SpellReading.None with { Healing = HealingMode.Raise, Lifts = lifts, Weakness = weakness };

    /// <summary>A spell that lifts the conditions it names from the member it is cast on.</summary>
    internal static SpellReading Cure(params ConditionId[] clears) => SpellReading.None with { Clears = clears };

    /// <summary>A spell that leaves a condition on a target that is not a party member.</summary>
    internal static SpellReading Inflict(ConditionId condition) =>
        SpellReading.None with { Inflicts = condition, Missing = "a condition on a world actor", Receiver = "the fight's own condition model, which is the party's" };

    /// <summary>A ward against the kinds of harm it names, at the donor's own power and duration.</summary>
    internal static SpellReading Ward(DamageKindId[] kinds, Func<int, int, int> power, Func<int, int, GameDuration> lasts) =>
        SpellReading.None with { Ward = new WardReading(kinds, Armour: false, power, lasts) };

    /// <summary>Armour class rather than a resistance, which is what the donor's stone skin raises.</summary>
    internal static SpellReading Armour(Func<int, int, int> power, Func<int, int, GameDuration> lasts) =>
        SpellReading.None with { Ward = new WardReading([], Armour: true, power, lasts) };

    /// <summary>A party-carried effect the spell leaves, with its power and its length.</summary>
    internal static SpellReading Buff(EffectId effect, Func<int, int, int> power, Func<int, int, GameDuration> lasts) =>
        SpellReading.None with { Buff = new BuffReading(effect, power, lasts) };

    /// <summary>A portal to a place the party names, taken through the world's own transition path.</summary>
    internal static SpellReading Portal() => SpellReading.None with { Travel = TravelShape.Portal };

    /// <summary>A beacon set where the party stands and recalled to later.</summary>
    internal static SpellReading Beacon() => SpellReading.None with { Travel = TravelShape.Beacon };

    /// <summary>A way of moving this build's mover has not, named with the owner that has it.</summary>
    internal static SpellReading Movement(string missing) =>
        SpellReading.None with { Travel = TravelShape.Movement, Missing = missing, Receiver = "the party's mover, which walks and falls and does nothing else" };

    /// <summary>A report over what the world holds.</summary>
    internal static SpellReading Detect(DetectionScope scope) => SpellReading.None with { Detection = scope };

    /// <summary>A dispelling of the effects other spells have left running.</summary>
    internal static SpellReading Dispel() => SpellReading.None with { Dispels = true };

    /// <summary>What this build cannot apply for a spell, and who owns it.</summary>
    internal static SpellReading NotYet(string missing, string receiver) =>
        SpellReading.None with { Missing = missing, Receiver = receiver };

    /// <summary>
    /// A spell whose effect needs a target this build cannot name, refused by name before it is paid for.
    /// </summary>
    /// <param name="missing">What the spell would act on.</param>
    /// <param name="receiver">Which owner would make that kind of target nameable.</param>
    internal static SpellReading Unaimable(string missing, string receiver) =>
        SpellReading.None with { Missing = missing, Receiver = receiver, Unaimable = true };

    /// <summary>How a reading is coarser than the game's, said where the spell is stated.</summary>
    internal static SpellReading Coarser(this SpellReading reading, string divergence) =>
        reading with { Divergence = divergence };

    /// <summary>
    /// How coarse a ward a character's own buff becomes when the party carries it.
    /// </summary>
    /// <remarks>
    /// The donor's six protections and its blessing, fate, heroism, and hammerhands are the character's own
    /// buffs, applied to the character the caster names. The party model carries effects party-wide — which is
    /// the design's own shape for a buff — so a ward raised for one member is carried by all of them. That is
    /// coarser than the game and is stated on every spell it touches rather than hidden.
    /// </remarks>
    internal const string PartyWideWard =
        "the donor's own buff belongs to the character it is cast on; this build's wards are carried by the party, so every member gets it (receiver: a per-member effect owner, which the party model does not have)";
}

/// <summary>
/// The party-carried state spells leave, named once so the effect that applies it and the reading that
/// honours it cannot disagree.
/// </summary>
/// <remarks>
/// <para>
/// A ward against fire is written by the casting and read by the fight's own resistance, and a haste is
/// written by the casting and read by the fight's own recovery: two owners of one fact, which is exactly why
/// the identity is derived here rather than spelled twice. The resistance identity is derived from the kind
/// of harm, so a ward of any kind is the same arithmetic, and the prefix keeps a spell's effect from
/// colliding with a game's own content names — the same shape a bought passage uses for the place it reaches
/// (<see cref="PartyRpg.Kit.Services.ServicePassage"/>).
/// </para>
/// <para>
/// <b>A beacon is one place, and its identity names it.</b> The party carries at most one beacon because
/// setting a second replaces the first: the ledger applies one effect per identity, and the ruleset drops the
/// one it is replacing.
/// </para>
/// </remarks>
internal static class SpellEffectIds
{
    /// <summary>A ward against one kind of harm.</summary>
    /// <param name="kind">The kind of harm the ward takes a share of.</param>
    internal static EffectId Resistance(DamageKindId kind) => new(string.Concat("spell.resist.", kind.Value));

    /// <summary>Armour class raised by a spell, which is what a stone skin leaves.</summary>
    internal static readonly EffectId Armour = new("spell.armour");

    /// <summary>Haste, whose magnitude is the recovery the donor's own buff takes off every action.</summary>
    internal static readonly EffectId Haste = new("spell.haste");

    /// <summary>Bless, whose magnitude is added to a member's attack bonus.</summary>
    internal static readonly EffectId Bless = new("spell.bless");

    /// <summary>Heroism, whose magnitude is added to what a member's blow is worth.</summary>
    internal static readonly EffectId Heroism = new("spell.heroism");

    /// <summary>Hammerhands, whose magnitude is added to an unarmed blow.</summary>
    internal static readonly EffectId Hammerhands = new("spell.hammerhands");

    /// <summary>Fate, whose magnitude is added to the luck a resistance check and a saving throw read.</summary>
    internal static readonly EffectId Fate = new("spell.fate");

    /// <summary>Invisibility, which is carried as a fact rather than as a magnitude.</summary>
    internal static readonly EffectId Invisibility = new("spell.invisibility");

    /// <summary>The light a spell carries, which is what a party in the dark sees by.</summary>
    internal static readonly EffectId Light = new("spell.light");

    /// <summary>The beacon the party has set, at the place it was set in.</summary>
    /// <param name="place">The place the beacon stands in.</param>
    internal static EffectId Beacon(PlaceId place) => new(string.Concat("spell.beacon.", place.Value));

    /// <summary>The prefix every beacon identity starts with, which is how a set beacon is found again.</summary>
    internal const string BeaconPrefix = "spell.beacon.";

    /// <summary>The place a beacon identity stands for, or null when the identity is not a beacon's.</summary>
    /// <param name="effect">The effect identity to read.</param>
    internal static PlaceId? BeaconPlace(EffectId effect) =>
        effect.Value.StartsWith(BeaconPrefix, StringComparison.Ordinal) && effect.Value.Length > BeaconPrefix.Length
            ? new PlaceId(effect.Value[BeaconPrefix.Length..])
            : null;
}
