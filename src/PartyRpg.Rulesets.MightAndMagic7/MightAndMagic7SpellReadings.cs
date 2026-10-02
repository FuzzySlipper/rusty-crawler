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

    /// <summary>A leap the party's mover takes from where it stands, which is the donor's jump.</summary>
    Leap,

    /// <summary>A flight the caster holds the party in, which the party's mover asks the engine's flying mode for.</summary>
    Flight,

    /// <summary>A walk over water the caster holds the party on, which this game's drowning reads.</summary>
    WaterWalk,
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

/// <summary>Which creatures a spell aimed at a foe takes hold of.</summary>
internal enum CreatureReach
{
    /// <summary>The one creature the casting named.</summary>
    Named,

    /// <summary>
    /// Every creature standing within the donor's mass-spell depth of the party, which is what the donor's own
    /// "every actor in the viewport" is in this build (<c>OpenEnroth/src/Application/GameConfig.h:200</c>,
    /// <c>mass_spell_depth</c>, 4096).
    /// </summary>
    InView,
}

/// <summary>Which kinds of creature a spell aimed at a foe can take hold of at all.</summary>
internal enum CreatureKindGate
{
    /// <summary>Any creature.</summary>
    Any,

    /// <summary>Only the undead.</summary>
    Undead,

    /// <summary>Only the living, which is every creature that is not undead.</summary>
    Living,
}

/// <summary>What a spell leaves on a creature: an effect of the creature's own, how strong, for how long, and on whom.</summary>
/// <param name="Effect">The effect the creature carries, which the fight's readings name.</param>
/// <param name="ResistedBy">The kind of harm a creature immune to which is untouched by the spell, or null for none.</param>
/// <param name="Power">What the effect is worth at the caster's school level and mastery.</param>
/// <param name="Lasts">How long it lasts at the caster's school level and mastery, or null when it leaves nothing that lasts.</param>
/// <param name="Reach">Which creatures it takes hold of.</param>
/// <param name="Kind">Which kinds of creature it can take hold of.</param>
/// <param name="Provokes">Whether casting it is an act against the creature that puts it into the fight.</param>
/// <param name="DelayTicks">The donor's ticks the creature's recovery is pushed back by, zero for none.</param>
/// <param name="Ends">The effects it replaces on the creature, which is the donor's charm, berserk and enslavement each ending the others.</param>
internal readonly record struct CreatureReading(
    EffectId Effect,
    DamageKindId? ResistedBy,
    Func<int, int, int> Power,
    Func<int, int, GameDuration>? Lasts,
    CreatureReach Reach = CreatureReach.Named,
    CreatureKindGate Kind = CreatureKindGate.Any,
    bool Provokes = true,
    int DelayTicks = 0,
    EffectId[]? Ends = null);

/// <summary>A creature a spell calls up to stand with the party: which row, how many at once, and for how long.</summary>
/// <param name="Rows">
/// The monster table's own internal name of the row called up at each rung of mastery, novice first; a rung the
/// spell cannot be cast at names the row of the lowest rung it can.
/// </param>
/// <param name="Most">How many a caster may have standing at once, at each rung of mastery, novice first.</param>
/// <param name="Lasts">How long one stays, at the caster's school level and mastery.</param>
internal readonly record struct SummonReading(string[] Rows, int[] Most, Func<int, int, GameDuration> Lasts);

/// <summary>A body a spell stands back up to fight for the party: how strong a body it can raise, and what it is left with.</summary>
/// <param name="MostLevel">The highest monster level it raises, at the caster's school level and mastery.</param>
/// <param name="HitPointsPerLevel">The most hit points the raised creature is left with, per level it could raise.</param>
internal readonly record struct ReanimateReading(Func<int, int, int> MostLevel, int HitPointsPerLevel);

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

    /// <summary>
    /// Thirty minutes for every point of the mixture's strength, which is the donor's one duration for its
    /// potions (<c>OpenEnroth/src/Engine/Objects/Character.cpp:3084-3085</c>, <c>30 * potionStrength</c>
    /// minutes) and what the shipped item table's own notes state in words ("for 30 minutes per point of
    /// potion strength").
    /// </summary>
    internal static readonly Func<int, int, GameDuration> ThirtyMinutesPerPoint = (level, _) => GameDuration.FromMinutes(30 * level);

    /// <summary>Three times the mixture's strength, which is what the donor's own potions raise a score or a resistance by.</summary>
    internal static readonly Func<int, int, int> ThreePerPoint = (level, _) => 3 * level;

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

    /// <summary>
    /// A day of the gods' own power: three, four, or five a level by mastery, plus ten.
    /// </summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/Spells/CastSpellInfo.cpp:2450-2475</c>: expert adds <c>3 * level + 10</c>, master
    /// <c>4 * level + 10</c>, and grand master <c>5 * level + 10</c> to every one of the seven attributes, which
    /// the donor reads in <c>GetMagicalBonus</c> (<c>src/Engine/Objects/Character.cpp:2360-2387</c>).
    /// </remarks>
    internal static readonly Func<int, int, int> DayOfTheGodsPower = (level, mastery) => (Math.Clamp(mastery + 1, 3, 5) * level) + 10;

    /// <summary>A day of the gods' own length: three, four, or five hours a level by mastery.</summary>
    /// <remarks>OpenEnroth <c>src/Engine/Spells/CastSpellInfo.cpp:2458-2470</c>.</remarks>
    internal static readonly Func<int, int, GameDuration> DayOfTheGodsLasts = (level, mastery) => GameDuration.FromHours(Math.Clamp(mastery + 1, 3, 5) * level);

    /// <summary>
    /// What an hour of power's own buffs are worth: the school's level plus five, which is what the single
    /// spells of the same names give.
    /// </summary>
    /// <remarks>OpenEnroth <c>src/Engine/Spells/CastSpellInfo.cpp:2530-2536</c> (<c>target_skill_level = spell_level + 5</c>).</remarks>
    internal static readonly Func<int, int, int> HourOfPowerPower = LevelPlus(perLevel: 1, flat: 5);

    /// <summary>
    /// How long an hour of power's buffs other than haste last: an hour plus fifteen minutes for every point of
    /// four times the level at master, five times at grand master.
    /// </summary>
    /// <remarks>OpenEnroth <c>src/Engine/Spells/CastSpellInfo.cpp:2538-2556</c>.</remarks>
    internal static readonly Func<int, int, GameDuration> HourOfPowerLasts = (level, mastery) =>
        GameDuration.FromHours(1) + GameDuration.FromMinutes(15 * level * (mastery >= 4 ? 5 : 4));

    /// <summary>How long an hour of power's haste lasts: an hour plus three minutes a point at master, four at grand master.</summary>
    /// <remarks>OpenEnroth <c>src/Engine/Spells/CastSpellInfo.cpp:2538-2556</c>.</remarks>
    internal static readonly Func<int, int, GameDuration> HourOfPowerHasteLasts = (level, mastery) =>
        mastery >= 4
            ? GameDuration.FromHours(1) + GameDuration.FromMinutes(4 * 5 * level)
            : GameDuration.FromHours(1) + GameDuration.FromMinutes(3 * 4 * level);

    /// <summary>
    /// What a regeneration gives back every five minutes: five times its power, which is one at novice and expert,
    /// three at master, and ten at grand master.
    /// </summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/Spells/CastSpellInfo.cpp:744-766</c> states the power by mastery, and
    /// <c>src/Engine/Engine.cpp:1398-1401</c> turns it into <c>5 * power</c> hit points for every five minutes of
    /// game time that pass (<c>:1236</c>, <c>ticksBetween(oldTime, newTime, 5 minutes)</c>).
    /// </remarks>
    internal static readonly Func<int, int, int> RegenerationPower = (_, mastery) => 5 * mastery switch
    {
        <= 2 => 1,
        3 => 3,
        _ => 10,
    };

    /// <summary>
    /// Pain reflection's own length: an hour plus five minutes a level at expert and master, fifteen at grand master.
    /// </summary>
    /// <remarks>OpenEnroth <c>src/Engine/Spells/CastSpellInfo.cpp:2813-2828</c>.</remarks>
    internal static readonly Func<int, int, GameDuration> PainReflectionLasts = (level, mastery) =>
        GameDuration.FromHours(1) + GameDuration.FromMinutes((mastery >= 4 ? 15 : 5) * level);

    /// <summary>
    /// Minutes per level of the school, at one rate for each rung of mastery: the shape every creature spell's
    /// length is stated in by the donor.
    /// </summary>
    /// <param name="novice">Minutes per level at novice.</param>
    /// <param name="expert">Minutes per level at expert.</param>
    /// <param name="master">Minutes per level at master.</param>
    /// <param name="grandmaster">Minutes per level at grand master.</param>
    /// <param name="flat">Minutes added whatever the level.</param>
    internal static Func<int, int, GameDuration> MinutesPerLevel(int novice, int expert, int master, int grandmaster, int flat = 0) =>
        (level, mastery) => GameDuration.FromMinutes(flat + (level * mastery switch
        {
            <= 1 => novice,
            2 => expert,
            3 => master,
            _ => grandmaster,
        }));

    /// <summary>
    /// Minutes per level by mastery, and at grand master a length that outlasts any visit to a place: the donor's
    /// own "until the player leaves the map", which it writes as a year (<c>CastSpellInfo.cpp:640-643</c>,
    /// <c>:2742-2745</c>) and which here is a year of this game's own calendar.
    /// </summary>
    internal static Func<int, int, GameDuration> MinutesPerLevelThenAVisit(int expert, int master) =>
        (level, mastery) => mastery >= 4
            ? GameDuration.FromHours(24 * 28 * 12)
            : GameDuration.FromMinutes(level * (mastery >= 3 ? master : expert));

    /// <summary>A berserk's own length: five or ten minutes a level by mastery, and an hour at grand master (<c>CastSpellInfo.cpp:2016-2030</c>).</summary>
    internal static readonly Func<int, int, GameDuration> BerserkLasts = (level, mastery) => mastery >= 4
        ? GameDuration.FromHours(1)
        : GameDuration.FromMinutes(level * (mastery >= 3 ? 10 : 5));

    /// <summary>A power that is one value for each rung of mastery.</summary>
    internal static Func<int, int, int> ByMastery(int novice, int expert, int master, int grandmaster) =>
        (_, mastery) => mastery switch
        {
            <= 1 => novice,
            2 => expert,
            3 => master,
            _ => grandmaster,
        };

    /// <summary>
    /// A feather fall's own length: five minutes a level at novice, ten at expert, and an hour a level at master and
    /// grand master (OpenEnroth <c>src/Engine/Spells/CastSpellInfo.cpp:1041-1060</c>).
    /// </summary>
    internal static readonly Func<int, int, GameDuration> FeatherFallLasts = (level, mastery) => mastery switch
    {
        <= 1 => GameDuration.FromMinutes(5 * level),
        2 => GameDuration.FromMinutes(10 * level),
        _ => GameDuration.FromHours(level),
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
/// <param name="OnEach">
/// Whether the effect lands on every member under their own entry rather than on the party, which is the donor's
/// own shape where one casting gives each character a buff of their own (an hour of power's blessing).
/// </param>
/// <param name="SparesTheWeak">
/// Whether a party with a weak member is given nothing of this effect, which is the donor's own haste: a weak
/// character cannot be hastened, and the party's haste is withheld from all of them
/// (<c>OpenEnroth/src/Engine/Spells/CastSpellInfo.cpp:826-838</c>, and <c>:2560-2590</c> for an hour of power).
/// </param>
internal readonly record struct BuffReading(
    EffectId Effect,
    Func<int, int, int> Power,
    Func<int, int, GameDuration> Lasts,
    bool OnEach = false,
    bool SparesTheWeak = false);

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
/// <param name="DetectionLasts">
/// How long a detection lasts once cast, as a function of the caster's school level and mastery rung, or null
/// for a spell that is not a detection.
/// </param>
/// <param name="OnMember">Whether the effect lands on the character the casting named rather than on the whole party.</param>
/// <param name="Dispels">Whether the spell ends the effects other spells have left running.</param>
/// <param name="Weakness">How weak a raising spell leaves the member it stands up, or null when it leaves none.</param>
/// <param name="Unaimable">Whether the spell acts on something this build has no way to aim at, so a casting is refused before it is paid for.</param>
/// <param name="Divergence">How this build's reading of the spell is coarser than the game's, empty when it is not.</param>
/// <param name="Missing">What this build cannot apply for the spell, empty when it applies all of it.</param>
/// <param name="Receiver">Who owns what is missing, empty when nothing is.</param>
/// <param name="NotApplied">Whether this build applies nothing of the spell, so a casting is refused before it is paid for.</param>
/// <param name="Buffs">
/// Further effects one casting leaves beside <paramref name="Buff"/>, each with its own power, length, and
/// carrier, which is what a spell the donor states as several buffs at once is (an hour of power).
/// </param>
/// <param name="Expresses">
/// What the coverage report says the spell does where the category's own sentence would not say it, empty when
/// the category's sentence is the whole truth.
/// </param>
/// <param name="AgesCaster">How many years older than their natural age the casting leaves its caster, zero for none.</param>
/// <param name="Rejuvenates">Whether the casting gives back every year its carrier was aged beyond their natural age.</param>
/// <param name="ForGood">The score the casting raises for good, once for each character, or null for none.</param>
/// <param name="ForGoodBy">How much that score is raised by.</param>
/// <param name="OnCreature">What the spell leaves on the creatures it takes hold of, or null when it touches none.</param>
/// <param name="Summons">The creature the spell calls up to stand with the party, or null when it calls none.</param>
/// <param name="Reanimates">The body the spell stands back up to fight for the party, or null when it raises none.</param>
/// <param name="PerDay">How many times a day one caster may cast it, zero when the game sets no daily limit.</param>
internal readonly record struct SpellReading(
    HealingMode Healing,
    int HealBase,
    int HealPerLevel,
    bool HealByMastery,
    ConditionId[]? Clears,
    ConditionId[]? Lifts,
    ConditionId? Inflicts,
    ConditionId? Leaves,
    int LeavesSeverity,
    int ManaBase,
    int ManaPerLevel,
    WardReading? Ward,
    BuffReading? Buff,
    TravelShape Travel,
    DetectionScope Detection,
    Func<int, int, GameDuration>? DetectionLasts,
    bool OnMember,
    bool Dispels,
    int? Weakness,
    bool Unaimable,
    string Divergence,
    string Missing,
    string Receiver,
    bool NotApplied = false,
    BuffReading[]? Buffs = null,
    string Expresses = "",
    int AgesCaster = 0,
    bool Rejuvenates = false,
    AttributeId? ForGood = null,
    int ForGoodBy = 0,
    CreatureReading? OnCreature = null,
    SummonReading? Summons = null,
    ReanimateReading? Reanimates = null,
    int PerDay = 0,
    ItemMagicShape? ItemMagic = null)
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
        Leaves: null,
        LeavesSeverity: 0,
        ManaBase: 0,
        ManaPerLevel: 0,
        Ward: null,
        Buff: null,
        Travel: TravelShape.None,
        Detection: DetectionScope.None,
        DetectionLasts: null,
        OnMember: false,
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

    /// <summary>
    /// A condition left on the character the casting lands on, at a stated severity.
    /// </summary>
    /// <remarks>
    /// The donor leaves a condition on the drinking character in one place — a catalyst drunk on its own
    /// poisons them weakly (<c>OpenEnroth/src/Engine/Objects/Character.cpp:3087-3089</c>,
    /// <c>SetCondition(CONDITION_POISON_WEAK, 1)</c>) — and a spell that leaves a condition on a party member
    /// is the same act, so it is stated rather than folded into <see cref="Inflict"/>, which names a
    /// condition on a world actor this build cannot carry.
    /// </remarks>
    /// <param name="condition">The condition left.</param>
    /// <param name="severity">How severe it is left, zero when the game states none.</param>
    internal static SpellReading Afflicts(ConditionId condition, int severity = 0) =>
        SpellReading.None with { Leaves = condition, LeavesSeverity = severity };

    /// <summary>Spell points given back through the member's own pool.</summary>
    /// <remarks>
    /// The donor's potion of magic adds its strength plus ten to the drinker's mana and stops at their maximum
    /// (<c>OpenEnroth/src/Engine/Objects/Character.cpp:3091-3096</c>), and its divine power adds five times the
    /// strength (<c>:3222-3227</c>). No shipped spell restores spell points, so this reading exists for the
    /// mixtures that do; the pool's own clamp is what stops it at the maximum.
    /// </remarks>
    /// <param name="perLevel">How many points each point of strength is worth.</param>
    /// <param name="flat">A flat amount the mixture states.</param>
    internal static SpellReading RestoresMana(int perLevel, int flat) =>
        SpellReading.None with { ManaPerLevel = perLevel, ManaBase = flat };

    /// <summary>A ward against the kinds of harm it names, at the donor's own power and duration.</summary>
    internal static SpellReading Ward(DamageKindId[] kinds, Func<int, int, int> power, Func<int, int, GameDuration> lasts) =>
        SpellReading.None with { Ward = new WardReading(kinds, Armour: false, power, lasts) };

    /// <summary>Armour class rather than a resistance, which is what the donor's stone skin raises.</summary>
    internal static SpellReading Armour(Func<int, int, int> power, Func<int, int, GameDuration> lasts) =>
        SpellReading.None with { Ward = new WardReading([], Armour: true, power, lasts) };

    /// <summary>A party-carried effect the spell leaves, with its power and its length.</summary>
    internal static SpellReading Buff(EffectId effect, Func<int, int, int> power, Func<int, int, GameDuration> lasts) =>
        SpellReading.None with { Buff = new BuffReading(effect, power, lasts) };

    /// <summary>Several effects one casting leaves at once, each with its own carrier, power, and length.</summary>
    /// <param name="buffs">The effects, in the order the donor's own case applies them.</param>
    internal static SpellReading Bundle(params BuffReading[] buffs) => SpellReading.None with { Buffs = buffs };

    /// <summary>A carried effect the donor withholds while a character it would land on is weak, which is its haste.</summary>
    internal static SpellReading WithheldFromTheWeak(this SpellReading reading) =>
        reading.Buff is { } buff ? reading with { Buff = buff with { SparesTheWeak = true } } : reading;

    /// <summary>A spell that leaves an effect on the creatures it takes hold of.</summary>
    /// <param name="creature">What it leaves, on whom, and for how long.</param>
    internal static SpellReading OnCreatures(CreatureReading creature) => SpellReading.None with { OnCreature = creature };

    /// <summary>A creature called up to stand with the party, which is the donor's summoned elemental.</summary>
    internal static SpellReading Summon(SummonReading summon) => SpellReading.None with { Summons = summon };

    /// <summary>A body stood back up to fight for the party, which is the donor's reanimation.</summary>
    internal static SpellReading Reanimate(ReanimateReading reanimate) => SpellReading.None with { Reanimates = reanimate };

    /// <summary>A spell one caster may cast only so many times a day, which is the donor's divine intervention.</summary>
    /// <param name="reading">The spell's reading.</param>
    /// <param name="times">How many castings a day one caster has.</param>
    internal static SpellReading PerDay(this SpellReading reading, int times) => reading with { PerDay = times };

    /// <summary>A casting that leaves its caster older than their natural age, which is the donor's divine intervention.</summary>
    /// <param name="reading">The reading the spell otherwise has.</param>
    /// <param name="years">How many years it adds.</param>
    internal static SpellReading AgesTheCaster(this SpellReading reading, int years) => reading with { AgesCaster = years };

    /// <summary>Every year a character was aged beyond their natural age given back, which is the donor's rejuvenation.</summary>
    internal static SpellReading Rejuvenation() => SpellReading.None with { Rejuvenates = true };

    /// <summary>A score raised for good, once for each character, which is the donor's pure potions.</summary>
    /// <param name="attribute">The score raised.</param>
    /// <param name="by">How much it is raised by.</param>
    internal static SpellReading RaisesForGood(AttributeId attribute, int by) =>
        SpellReading.None with { ForGood = attribute, ForGoodBy = by };

    /// <summary>What the coverage report says a spell does, where its category's own sentence would not say it.</summary>
    internal static SpellReading Says(this SpellReading reading, string expresses) => reading with { Expresses = expresses };

    /// <summary>A portal to a place the party names, taken through the world's own transition path.</summary>
    internal static SpellReading Portal() => SpellReading.None with { Travel = TravelShape.Portal };

    /// <summary>A beacon set where the party stands and recalled to later.</summary>
    internal static SpellReading Beacon() => SpellReading.None with { Travel = TravelShape.Beacon };

    /// <summary>A leap the party's mover takes from where it stands.</summary>
    internal static SpellReading Leap() => SpellReading.None with { Travel = TravelShape.Leap };

    /// <summary>A flight the caster holds the party in, outdoors, for as long as it lasts and the caster can keep it up.</summary>
    internal static SpellReading Flight() => SpellReading.None with { Travel = TravelShape.Flight };

    /// <summary>A walk over water the caster holds the party on, for as long as it lasts.</summary>
    internal static SpellReading WaterWalk() => SpellReading.None with { Travel = TravelShape.WaterWalk };


    /// <summary>A report over what the world holds, which the party carries for a while as the original does.</summary>
    /// <remarks>
    /// <para>
    /// <b>The duration is ours.</b> The shipped spell table states what a detection looks over and nothing about
    /// how long it runs, and the donor's own buff table is not read here, so this game gives its detections the
    /// same length its protections have — the donor's one hour per level of the school
    /// (<c>WardFormulas.HoursPerLevel</c>, whose citation stands beside the formula) — rather than leaving a
    /// spell the manual draws an icon for running for no time at all.
    /// </para>
    /// <para>
    /// What the duration is <em>for</em> is the automap: the reveal a detection puts on the map lasts exactly
    /// as long as the effect does, so a spell that has lapsed marks nothing and a party that walks away from
    /// what it saw keeps none of it.
    /// </para>
    /// </remarks>
    internal static SpellReading Detect(DetectionScope scope, Func<int, int, GameDuration> lasts) =>
        SpellReading.None with { Detection = scope, DetectionLasts = lasts };

    /// <summary>A dispelling of the effects other spells have left running.</summary>
    /// <summary>An operation over one actual item, handed to the shared utility effect path.</summary>
    internal static SpellReading OnItem(ItemMagicShape shape) => SpellReading.None with
    {
        ItemMagic = shape,
        Expresses = "a property, hardening or recharge on the named party item through the canonical instance; item aims use the ordinary casting workflow",
        Divergence = "enchantment properties and weapon magnitudes are approximate; common item eligibility, quest refusal, mastery strength and clock duration are explicit",
    };

    internal static SpellReading Dispel() => SpellReading.None with { Dispels = true };

    /// <summary>What this build cannot apply for a spell, and who owns it.</summary>
    internal static SpellReading NotYet(string missing, string receiver) =>
        SpellReading.None with { Missing = missing, Receiver = receiver, NotApplied = true };

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
    /// An effect that lands on the character the casting named rather than on the whole party.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The donor's own buffs are not one shape. Its six protections and its heroism are party buffs
    /// (<c>OpenEnroth/src/Engine/Spells/CastSpellInfo.cpp:767-801</c>, <c>pPartyBuffs[PARTY_BUFF_RESIST_*]</c>,
    /// and <c>:930-945</c> for heroism), its blessing is one character's below expert mastery and the party's
    /// at expert and above (<c>:846-880</c>), its fate is one character's (<c>:1631-1656</c>), and its
    /// hammerhands is one character's below grand master and the party's at grand master
    /// (<c>:2364-2384</c>). What decides here is this game's own table: a spell the table aims at one
    /// character lands on that character, with their own deadline, and a spell it aims at the party is carried
    /// by the party. That is finer-grained than the donor where the donor buffs a whole party, and it is
    /// stated per spell in the coverage report rather than hidden in an effect path.
    /// </para>
    /// <para>
    /// Why it is worth being finer: the effect is read where it applies — a resistance by that character's own
    /// resistance sum, a blessing by their own chance to land — so a ward one member was given cannot be a
    /// ward the whole band was given for the price of one casting.
    /// </para>
    /// </remarks>
    internal static SpellReading OnOne(this SpellReading reading) => reading with { OnMember = true };
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

    /// <summary>A score raised for a while, whose magnitude is added to that score wherever a fight reads it.</summary>
    /// <remarks>
    /// The donor keeps one character buff per score (<c>CHARACTER_BUFF_STRENGTH</c> and its siblings) and adds its
    /// power in <c>GetMagicalBonus</c> (<c>OpenEnroth/src/Engine/Objects/Character.cpp:2360-2387</c>); the identity is
    /// derived from the score so every boost is the same arithmetic.
    /// </remarks>
    /// <param name="attribute">The score raised.</param>
    internal static EffectId Attribute(AttributeId attribute) => new(string.Concat("spell.attribute.", attribute.Value));

    /// <summary>Day of the Gods, whose magnitude the party carries and every member adds to all seven scores.</summary>
    internal static readonly EffectId DayOfTheGods = new("spell.day-of-the-gods");

    /// <summary>Shield, which halves what a missile does to whoever carries it; a fact rather than a magnitude.</summary>
    internal static readonly EffectId Shield = new("spell.shield");

    /// <summary>Pain Reflection, which turns the harm a character takes back onto whoever dealt it.</summary>
    internal static readonly EffectId PainReflection = new("spell.pain-reflection");

    /// <summary>
    /// Fly, carried by the character who cast it: while anybody carries it the party may fly, and its magnitude is the
    /// spell points that character pays for every five minutes the party spends in the air.
    /// </summary>
    internal static readonly EffectId Fly = new("spell.fly");

    /// <summary>
    /// Water walking, carried by the character who cast it: while anybody carries it the party walks over water rather
    /// than drowning in it, and its magnitude is the spell points that character pays for every twenty minutes the
    /// party stands on water.
    /// </summary>
    internal static readonly EffectId WaterWalk = new("spell.water-walk");

    /// <summary>Water breathing, carried by one character: water the party stands in does not drown them.</summary>
    internal static readonly EffectId WaterBreathing = new("spell.water-breathing");

    /// <summary>Feather fall, which the party carries and which spares every member a fall's harm.</summary>
    internal static readonly EffectId FeatherFall = new("spell.feather-fall");

    /// <summary>Regeneration, whose magnitude is the health a character is given back every five minutes.</summary>
    internal static readonly EffectId Regeneration = new("spell.regeneration");

    /// <summary>A creature held where it stands, unable to act: <c>ACTOR_BUFF_PARALYZED</c>.</summary>
    internal static readonly EffectId CreatureParalyzed = new("creature.paralyzed");

    /// <summary>A creature slowed, whose magnitude divides its pace and which doubles its recovery: <c>ACTOR_BUFF_SLOWED</c>.</summary>
    internal static readonly EffectId CreatureSlowed = new("creature.slowed");

    /// <summary>A creature afraid, which runs from what it fights: <c>ACTOR_BUFF_AFRAID</c>.</summary>
    internal static readonly EffectId CreatureAfraid = new("creature.afraid");

    /// <summary>A creature shrunk, whose magnitude divides the harm it does: <c>ACTOR_BUFF_SHRINK</c>.</summary>
    internal static readonly EffectId CreatureShrunk = new("creature.shrunk");

    /// <summary>A creature charmed, which stands with the party and fights nobody for it: <c>ACTOR_BUFF_CHARM</c>.</summary>
    internal static readonly EffectId CreatureCharmed = new("creature.charmed");

    /// <summary>A creature driven berserk, which is the enemy of everything: <c>ACTOR_BUFF_BERSERK</c>.</summary>
    internal static readonly EffectId CreatureBerserk = new("creature.berserk");

    /// <summary>A creature bound to serve, which fights for the party as one of its own: <c>ACTOR_BUFF_ENSLAVED</c>.</summary>
    internal static readonly EffectId CreatureEnslaved = new("creature.enslaved");

    /// <summary>The three allegiances a spell can leave on a creature, each of which ends the others.</summary>
    internal static readonly EffectId[] Allegiances = [CreatureCharmed, CreatureBerserk, CreatureEnslaved];

    /// <summary>A creature staggered, which leaves nothing that lasts: the stun pushes its recovery back.</summary>
    internal static readonly EffectId CreatureStunned = new("creature.stunned");

    /// <summary>Invisibility, which is carried as a fact rather than as a magnitude.</summary>
    internal static readonly EffectId Invisibility = new("spell.invisibility");

    /// <summary>The identity a detection's own effect is carried under, one per scope it looks over.</summary>
    /// <param name="scope">What the detection reports over.</param>
    /// <returns>The effect identity.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The scope is none, which is not a detection.</exception>
    internal static EffectId Detection(DetectionScope scope) => scope switch
    {
        DetectionScope.Places => Places,
        DetectionScope.Life => Life,
        DetectionScope.Minds => Minds,
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "A spell that reports over nothing is not a detection."),
    };

    /// <summary>What a running effect of this game's looks over, or none when it is not a detection.</summary>
    /// <param name="effect">The effect identity to read.</param>
    /// <returns>The scope that effect is a detection over.</returns>
    internal static DetectionScope ScopeOf(EffectId effect)
    {
        if (effect == Places) return DetectionScope.Places;
        if (effect == Life) return DetectionScope.Life;
        return effect == Minds ? DetectionScope.Minds : DetectionScope.None;
    }

    /// <summary>A detection over the places the party knows, which is what Wizard Eye leaves running.</summary>
    internal static readonly EffectId Places = new("spell.detect.places");

    /// <summary>A detection over everything alive where the party stands, which is what Detect Life leaves.</summary>
    internal static readonly EffectId Life = new("spell.detect.life");

    /// <summary>A detection over who answers where the party stands, which is what Telepathy leaves.</summary>
    internal static readonly EffectId Minds = new("spell.detect.minds");

    /// <summary>The light a spell carries, which is what a party in the dark sees by.</summary>
    internal static readonly EffectId Light = new("spell.light");

    /// <summary>The record the party keeps for the beacon it set, at the place it was set in.</summary>
    /// <param name="place">The place the beacon stands in.</param>
    internal static string Beacon(PlaceId place) => new BeaconIdentity(place).Record;

    /// <summary>
    /// The record the party keeps that one character has had the potion that raises one score for good, which the
    /// donor keeps per character and per score (<c>OpenEnroth/src/Engine/Objects/Character.cpp:3282-3295</c>,
    /// <c>_pureStatPotionUsed</c>).
    /// </summary>
    /// <param name="member">The character.</param>
    /// <param name="attribute">The score.</param>
    internal static string ForGood(PartyMemberId member, AttributeId attribute) =>
        string.Concat("potion.for-good.", member.ToString(), ".", attribute.Value);

    /// <summary>The place a beacon record stands for, or null when the record is not a beacon's.</summary>
    /// <param name="record">The record's name.</param>
    internal static PlaceId? BeaconPlace(string record) => BeaconIdentity.Read(record)?.Place;
}
