using System.Globalization;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Magic;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Skills;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>What a spell's effect is, as the category this game reads it under.</summary>
/// <remarks>
/// <para>
/// <b>The categories are the design's own eight</b>
/// ([`docs/gameplay-design.md`](../../../docs/gameplay-design.md) §3.3: "Individual spell effects are
/// approximated by category first (damage, healing, resistance, condition, light, travel, detection,
/// utility), then deepened"), and a spell carries one as its effect identity. That is what keeps the effect
/// seam one answer rather than ninety-nine: the casting mechanism hands a category to the effect owner and
/// never learns a spell's name, so deepening an effect is work behind the seam and no change to the
/// mechanism.
/// </para>
/// <para>
/// <b>Nothing in the kit reads one of these words.</b> They are this ruleset's own vocabulary, passed
/// through the kit untouched exactly as an attack ability's name is.
/// </para>
/// </remarks>
internal static class SpellEffects
{
    /// <summary>Harm: the spell's own dice, its target's resistance, and the condition a hit leaves.</summary>
    internal const string Damage = "damage";

    /// <summary>Hit points given back, including the spells that raise the fallen.</summary>
    internal const string Healing = "healing";

    /// <summary>A ward that takes its share of one kind of harm, or of harm altogether.</summary>
    internal const string Resistance = "resistance";

    /// <summary>A condition inflicted on a target, or lifted from one.</summary>
    internal const string Condition = "condition";

    /// <summary>The spells that change what the party can see by.</summary>
    internal const string Light = "light";

    /// <summary>The spells that move the party, or let it cross what it otherwise could not.</summary>
    internal const string Travel = "travel";

    /// <summary>The spells that tell the party something about where it stands.</summary>
    internal const string Detection = "detection";

    /// <summary>Everything else a spell does to the party, its gear, or a creature's allegiance.</summary>
    internal const string Utility = "utility";
}

/// <summary>
/// This game's magic: which spells content declares, what each costs and does, how far a member's spell
/// points reach, and what a book teaches.
/// </summary>
/// <remarks>
/// <para>
/// <b>Content declares the spells; the numbers are the donor's transcription of the executable.</b> The
/// shipped <c>SPELLS.TXT</c> carries 99 rows, nine schools of eleven, with each spell's name, its level
/// inside its school, a damage or resistance type, four mastery effect texts, and a flag string
/// ([`docs/research/mm7-data-inventory.md`](../../../docs/research/mm7-data-inventory.md), <i>Magic</i>). It
/// carries no cost, recovery, damage, required mastery, or targeting: those are in <c>MM7.exe</c>, and the
/// donor transcribes them (<c>OpenEnroth/src/Engine/Spells/Spells.cpp:193-292</c>, <c>pSpellDatas</c>). The
/// design records that guild spell-level gating is authored by us rather than extracted
/// ([`docs/gameplay-design.md`](../../../docs/gameplay-design.md) §3.12), and this table is the same kind of
/// authored reading: the numbers are the donor's, the targeting and the effect category are ours.
/// </para>
/// <para>
/// <b>The reading of the shipped rows, with counts.</b> 99 rows, nine schools of eleven, every one of them
/// declared by content and carried in the catalog. The shipped <c>Lvl</c> column cannot be a ladder: the
/// Light section numbers its rows 1, 2, 3, <b>6</b>, <b>5</b>, <b>6</b>, 7, 8, 9, 10, 11 — six twice and no
/// four — which is the one row meaning this file refuses to guess at. The donor's required-mastery column
/// resolves it: Light's fourth row (Paralyze) requires novice and its fifth (Summon Elemental) expert, so
/// the column that agrees with the row's ordinal band is the mastery rather than the <c>Lvl</c> cell. A
/// spell's tier is therefore the donor's required mastery, which follows the ordinal in all nine schools —
/// four spells at novice, three at expert, three at master, one at grand master — and that ordinal is what a
/// guild's tier sells.
/// </para>
/// <para>
/// <b>Harm is the donor's criterion, not a guess.</b> A spell whose row states base or per-skill damage is
/// one that harms, and every one of those is read under <see cref="SpellEffects.Damage"/>; the rest carry a
/// category this game authors from the shipped description. Each row below is commented with the spell's own
/// name, so the mapping can be checked against the shipped table.
/// </para>
/// <para>
/// <b>An area spell is aimed at one opponent here.</b> The donor distinguishes a point, a group, and a cone
/// (Fireball, Meteor Shower, Inferno, Prismatic Light, Dragon Breath, Shrapmetal, Armageddon); this build's
/// aim vocabulary has one foe at a time, and what an area does is the effect's own business behind the seam.
/// <b>Item-aimed spells name no actor</b> — Recharge Item, Enchant Item, Telekinesis, and Sacrifice act on
/// gear, a distant object, or a follower — so they carry <see cref="SpellTargeting.None"/> and are cast with
/// no target named until an item-aim owner exists.
/// </para>
/// </remarks>
internal sealed class MightAndMagic7Spells : ISpellRule
{
    /// <summary>The definition kind the shipped spell table is declared under.</summary>
    internal const string SpellDefinitionKind = "spell";

    /// <summary>The definition kind the shipped item table is declared under.</summary>
    internal const string ItemDefinitionKind = "item";

    /// <summary>The item field that names the spell a book teaches, which the importer writes.</summary>
    internal const string TeachesField = "spell";

    /// <summary>The item field the shipped table's own spell reference is carried in.</summary>
    internal const string DamageDiceField = "damageDice";

    /// <summary>The item equipment word that makes a row a spell book.</summary>
    internal const string BookEquipStat = "Book";

    /// <summary>The prefix the shipped item table puts in front of a book's spell id.</summary>
    internal const string BookSpellPrefix = "S";

    /// <summary>The item field that states what sort of equipment a row is.</summary>
    private const string EquipStatField = "equipStat";

    /// <summary>The item field that states a row's name.</summary>
    private const string NameField = "name";

    /// <summary>The spell field that states the school the spell belongs to.</summary>
    private const string SchoolField = "school";

    /// <summary>The spell field that states the kind of harm or resistance the spell uses.</summary>
    private const string ResistField = "resist";

    /// <summary>How many spells the shipped table carries: nine schools of eleven.</summary>
    internal const int ExpectedSpells = 99;

    /// <summary>How many rungs of a school's ladder a spell's numbers are stated at.</summary>
    internal const int Rungs = 4;

    /// <summary>
    /// The base spell points each base class starts from, keyed by the class's own name.
    /// </summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/Objects/Character.cpp:133</c>, <c>pBaseManaByClass</c> read at
    /// <c>classType / 4</c> — the donor's class rows are four per family — which is
    /// <c>{0, 0, 0, 5, 5, 0, 10, 10, 15}</c> for the nine base classes in this game's own ladder order. The
    /// same numbers are what creation states as each class's starting spell points, which is this formula's
    /// level-one value.
    /// </remarks>
    private static readonly Dictionary<string, int> BaseMana = new(StringComparer.Ordinal)
    {
        ["Knight"] = 0,
        ["Thief"] = 0,
        ["Monk"] = 0,
        ["Paladin"] = 5,
        ["Archer"] = 5,
        ["Ranger"] = 0,
        ["Cleric"] = 10,
        ["Druid"] = 10,
        ["Sorcerer"] = 15,
    };

    /// <summary>Which scores each base class draws its spell points from.</summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/Objects/Character.cpp:1797-1843</c>, <c>Character::GetMaxMana</c>: the rogue,
    /// archer, and sorcerer families cast from intellect, the monk, paladin, and cleric families from
    /// personality, the ranger and druid families from both, and the knight and thief families cast nothing
    /// at all and hold no pool.
    /// </remarks>
    private static readonly Dictionary<string, AttributeId[]> CastingScores = new(StringComparer.Ordinal)
    {
        ["Knight"] = [],
        ["Thief"] = [],
        ["Monk"] = [new AttributeId("Personality")],
        ["Paladin"] = [new AttributeId("Personality")],
        ["Archer"] = [new AttributeId("Intellect")],
        ["Ranger"] = [new AttributeId("Intellect"), new AttributeId("Personality")],
        ["Cleric"] = [new AttributeId("Personality")],
        ["Druid"] = [new AttributeId("Intellect"), new AttributeId("Personality")],
        ["Sorcerer"] = [new AttributeId("Intellect")],
    };

    /// <summary>The donor's own score-to-bonus steps, from the highest score down to zero.</summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/Objects/Character.cpp:231-235</c>, <c>param_to_bonus_table</c> and
    /// <c>parameter_to_bonus_value</c>: a score of at least 500 is worth thirty, 400 twenty-five, and so down
    /// to zero, which is worth minus six. The first floor a score reaches is its bonus.
    /// </remarks>
    private static readonly (int Floor, int Bonus)[] ParameterBonusSteps =
    [
        (500, 30), (400, 25), (350, 20), (300, 19), (275, 18), (250, 17), (225, 16), (200, 15),
        (175, 14), (150, 13), (125, 12), (100, 11), (75, 10), (50, 9), (40, 8), (35, 7), (30, 6),
        (25, 5), (21, 4), (19, 3), (17, 2), (15, 1), (13, 0), (11, -1), (9, -2), (7, -3), (5, -4),
        (3, -5), (0, -6),
    ];

    /// <summary>
    /// The donor's per-spell ladder, indexed by the spell's global id, in the order <c>SPELLS.TXT</c> states
    /// the rows.
    /// </summary>
    /// <remarks>
    /// Each entry is one row of <c>pSpellDatas</c> (<c>OpenEnroth/src/Engine/Spells/Spells.cpp:193-292</c>)
    /// joined to the shipped row of the same id: what one casting costs at each of the four mastery rungs,
    /// how long the caster recovers at each, the damage the spell rolls, and the mastery it requires.
    /// </remarks>
    private static readonly Numbers[] Table =
    [
        Entry(1, [1, 1, 1, 1], [60, 60, 60, 40], 0, 0, 1, SpellTargeting.Party, SpellEffects.Light),   // Torch Light
        Entry(2, [2, 2, 2, 2], [110, 110, 100, 90], 0, 3, 1, SpellTargeting.Foe, SpellEffects.Damage),   // Fire Bolt
        Entry(3, [3, 3, 3, 3], [120, 120, 120, 120], 0, 0, 1, SpellTargeting.Ally, SpellEffects.Resistance),   // Fire Resistance
        Entry(4, [4, 4, 4, 4], [120, 120, 120, 120], 0, 0, 1, SpellTargeting.Ally, SpellEffects.Utility),   // Fire Aura
        Entry(5, [5, 5, 5, 5], [120, 120, 120, 120], 0, 0, 2, SpellTargeting.Party, SpellEffects.Utility),   // Haste
        Entry(6, [8, 8, 8, 8], [100, 100, 90, 80], 0, 6, 2, SpellTargeting.Foe, SpellEffects.Damage),   // Fireball
        Entry(7, [10, 10, 10, 10], [150, 150, 150, 150], 0, 6, 2, SpellTargeting.Foe, SpellEffects.Damage),   // Fire Spike
        Entry(8, [15, 15, 15, 15], [120, 120, 120, 120], 0, 6, 3, SpellTargeting.Foe, SpellEffects.Damage),   // Immolation
        Entry(9, [20, 20, 20, 20], [100, 100, 100, 90], 0, 8, 3, SpellTargeting.Foe, SpellEffects.Damage),   // Meteor Shower
        Entry(10, [25, 25, 25, 25], [100, 100, 100, 90], 12, 1, 3, SpellTargeting.Foe, SpellEffects.Damage),   // Inferno
        Entry(11, [30, 30, 30, 30], [90, 90, 90, 90], 15, 15, 4, SpellTargeting.Foe, SpellEffects.Damage),   // Incinerate
        Entry(12, [1, 1, 1, 0], [60, 60, 60, 60], 0, 0, 1, SpellTargeting.Party, SpellEffects.Detection),   // Wizard Eye
        Entry(13, [2, 2, 2, 2], [120, 120, 120, 100], 0, 0, 1, SpellTargeting.Party, SpellEffects.Travel),   // Feather Fall
        Entry(14, [3, 3, 3, 3], [120, 120, 120, 120], 0, 0, 1, SpellTargeting.Ally, SpellEffects.Resistance),   // Air Resistance
        Entry(15, [4, 4, 4, 4], [110, 100, 90, 80], 2, 1, 1, SpellTargeting.Foe, SpellEffects.Damage),   // Sparks
        Entry(16, [5, 5, 5, 5], [90, 90, 70, 50], 0, 0, 2, SpellTargeting.Caster, SpellEffects.Travel),   // Jump
        Entry(17, [8, 8, 8, 8], [120, 120, 120, 120], 0, 0, 2, SpellTargeting.Caster, SpellEffects.Resistance),   // Shield
        Entry(18, [10, 10, 10, 10], [100, 100, 90, 70], 0, 8, 2, SpellTargeting.Foe, SpellEffects.Damage),   // Lightning Bolt
        Entry(19, [15, 15, 15, 15], [200, 200, 200, 200], 0, 0, 3, SpellTargeting.Party, SpellEffects.Utility),   // Invisibility
        Entry(20, [20, 20, 20, 20], [100, 100, 100, 90], 10, 10, 3, SpellTargeting.Foe, SpellEffects.Damage),   // Implosion
        Entry(21, [25, 25, 25, 25], [250, 250, 250, 250], 0, 0, 3, SpellTargeting.Party, SpellEffects.Travel),   // Fly
        Entry(22, [30, 30, 30, 30], [90, 90, 90, 90], 20, 1, 4, SpellTargeting.Foe, SpellEffects.Damage),   // Starburst
        Entry(23, [1, 1, 1, 1], [60, 60, 60, 20], 0, 0, 1, SpellTargeting.Ally, SpellEffects.Condition),   // Awaken
        Entry(24, [2, 2, 2, 2], [110, 100, 90, 70], 2, 2, 1, SpellTargeting.Foe, SpellEffects.Damage),   // Poison Spray
        Entry(25, [3, 3, 3, 3], [120, 120, 120, 120], 0, 0, 1, SpellTargeting.Ally, SpellEffects.Resistance),   // Water Resistance
        Entry(26, [4, 4, 4, 4], [110, 100, 90, 80], 0, 4, 1, SpellTargeting.Foe, SpellEffects.Damage),   // Ice Bolt
        Entry(27, [5, 5, 5, 5], [150, 150, 150, 150], 0, 0, 2, SpellTargeting.Party, SpellEffects.Travel),   // Water Walk
        Entry(28, [8, 8, 8, 8], [200, 200, 200, 200], 0, 0, 2, SpellTargeting.None, SpellEffects.Utility),   // Recharge Item
        Entry(29, [10, 10, 10, 10], [100, 100, 90, 80], 9, 9, 2, SpellTargeting.Foe, SpellEffects.Damage),   // Acid Burst
        Entry(30, [15, 15, 15, 15], [140, 140, 140, 140], 0, 0, 3, SpellTargeting.None, SpellEffects.Utility),   // Enchant Item
        Entry(31, [20, 20, 20, 20], [200, 200, 200, 200], 0, 0, 3, SpellTargeting.None, SpellEffects.Travel),   // Town Portal
        Entry(32, [25, 25, 25, 25], [80, 80, 80, 80], 12, 3, 3, SpellTargeting.Foe, SpellEffects.Damage),   // Ice Blast
        Entry(33, [30, 30, 30, 30], [250, 250, 250, 250], 0, 0, 4, SpellTargeting.None, SpellEffects.Travel),   // Lloyd's Beacon
        Entry(34, [1, 1, 1, 1], [80, 80, 80, 80], 0, 0, 1, SpellTargeting.Foe, SpellEffects.Condition),   // Stun
        Entry(35, [2, 2, 2, 2], [100, 100, 100, 100], 0, 0, 1, SpellTargeting.Foe, SpellEffects.Condition),   // Slow
        Entry(36, [3, 3, 3, 3], [120, 120, 120, 120], 0, 0, 1, SpellTargeting.Ally, SpellEffects.Resistance),   // Earth Resistance
        Entry(37, [4, 4, 4, 4], [110, 100, 90, 80], 5, 3, 1, SpellTargeting.Foe, SpellEffects.Damage),   // Deadly Swarm
        Entry(38, [5, 5, 5, 5], [120, 120, 120, 120], 0, 0, 2, SpellTargeting.Party, SpellEffects.Resistance),   // Stone Skin
        Entry(39, [8, 8, 8, 8], [100, 100, 90, 80], 0, 9, 2, SpellTargeting.Foe, SpellEffects.Damage),   // Blades
        Entry(40, [10, 10, 10, 10], [140, 140, 140, 140], 0, 0, 2, SpellTargeting.Ally, SpellEffects.Condition),   // Stone to Flesh
        Entry(41, [15, 15, 15, 15], [90, 90, 90, 80], 0, 8, 3, SpellTargeting.Foe, SpellEffects.Damage),   // Rock Blast
        Entry(42, [20, 20, 20, 20], [150, 150, 150, 150], 0, 0, 3, SpellTargeting.None, SpellEffects.Utility),   // Telekinesis
        Entry(43, [25, 25, 25, 25], [100, 100, 100, 90], 20, 1, 3, SpellTargeting.Foe, SpellEffects.Damage),   // Death Blossom
        Entry(44, [30, 30, 30, 30], [90, 90, 90, 90], 25, 2, 4, SpellTargeting.Foe, SpellEffects.Damage),   // Mass Distortion
        Entry(45, [1, 1, 1, 1], [100, 100, 100, 100], 0, 0, 1, SpellTargeting.Caster, SpellEffects.Detection),   // Detect Life
        Entry(46, [2, 2, 2, 2], [100, 100, 100, 100], 0, 0, 1, SpellTargeting.Ally, SpellEffects.Utility),   // Bless
        Entry(47, [3, 3, 3, 3], [90, 90, 90, 90], 0, 0, 1, SpellTargeting.Ally, SpellEffects.Utility),   // Fate
        Entry(48, [4, 4, 4, 4], [120, 120, 120, 120], 0, 0, 1, SpellTargeting.Foe, SpellEffects.Condition),   // Turn Undead
        Entry(49, [5, 5, 5, 5], [120, 120, 120, 120], 0, 0, 2, SpellTargeting.Ally, SpellEffects.Condition),   // Remove Curse
        Entry(50, [8, 8, 8, 8], [120, 120, 120, 120], 0, 0, 2, SpellTargeting.Caster, SpellEffects.Utility),   // Preservation
        Entry(51, [10, 10, 10, 10], [120, 120, 120, 120], 0, 0, 2, SpellTargeting.Ally, SpellEffects.Utility),   // Heroism
        Entry(52, [15, 15, 15, 15], [100, 100, 100, 100], 10, 8, 3, SpellTargeting.Foe, SpellEffects.Damage),   // Spirit Lash
        Entry(53, [20, 20, 20, 20], [240, 240, 240, 240], 0, 0, 3, SpellTargeting.Ally, SpellEffects.Healing),   // Raise Dead
        Entry(54, [25, 25, 25, 25], [150, 150, 150, 150], 0, 0, 3, SpellTargeting.Ally, SpellEffects.Healing),   // Shared Life
        Entry(55, [30, 30, 30, 30], [1000, 1000, 1000, 1000], 0, 0, 4, SpellTargeting.Ally, SpellEffects.Healing),   // Resurrection
        Entry(56, [1, 1, 1, 1], [120, 120, 120, 120], 0, 0, 1, SpellTargeting.Ally, SpellEffects.Condition),   // Remove Fear
        Entry(57, [2, 2, 2, 2], [110, 110, 110, 110], 3, 3, 1, SpellTargeting.Foe, SpellEffects.Damage),   // Mind Blast
        Entry(58, [3, 3, 3, 3], [120, 120, 120, 120], 0, 0, 1, SpellTargeting.Ally, SpellEffects.Resistance),   // Mind Resistance
        Entry(59, [4, 4, 4, 4], [110, 100, 90, 80], 0, 0, 1, SpellTargeting.Caster, SpellEffects.Detection),   // Telepathy
        Entry(60, [5, 5, 5, 5], [100, 100, 100, 100], 0, 0, 2, SpellTargeting.Foe, SpellEffects.Condition),   // Charm
        Entry(61, [8, 8, 8, 8], [120, 120, 120, 120], 0, 0, 2, SpellTargeting.Ally, SpellEffects.Condition),   // Cure Paralysis
        Entry(62, [10, 10, 10, 10], [120, 120, 120, 120], 0, 0, 2, SpellTargeting.Foe, SpellEffects.Condition),   // Berserk
        Entry(63, [15, 15, 15, 15], [80, 80, 80, 80], 0, 0, 3, SpellTargeting.Foe, SpellEffects.Condition),   // Mass Fear
        Entry(64, [20, 20, 20, 20], [120, 120, 120, 120], 0, 0, 3, SpellTargeting.Ally, SpellEffects.Condition),   // Cure Insanity
        Entry(65, [25, 25, 25, 25], [110, 110, 110, 100], 12, 12, 3, SpellTargeting.Foe, SpellEffects.Damage),   // Psychic Shock
        Entry(66, [30, 30, 30, 30], [120, 120, 120, 120], 0, 0, 4, SpellTargeting.Foe, SpellEffects.Condition),   // Enslave
        Entry(67, [1, 1, 1, 1], [120, 120, 120, 120], 0, 0, 1, SpellTargeting.Ally, SpellEffects.Condition),   // Cure Weakness
        Entry(68, [2, 2, 2, 2], [100, 100, 100, 100], 0, 0, 1, SpellTargeting.Ally, SpellEffects.Healing),   // Heal
        Entry(69, [3, 3, 3, 3], [120, 120, 120, 120], 0, 0, 1, SpellTargeting.Ally, SpellEffects.Resistance),   // Body Resistance
        Entry(70, [4, 4, 4, 4], [110, 100, 90, 80], 8, 2, 1, SpellTargeting.Foe, SpellEffects.Damage),   // Harm
        Entry(71, [5, 5, 5, 5], [110, 110, 110, 110], 0, 0, 2, SpellTargeting.Ally, SpellEffects.Healing),   // Regeneration
        Entry(72, [8, 8, 8, 8], [120, 120, 120, 120], 0, 0, 2, SpellTargeting.Ally, SpellEffects.Condition),   // Cure Poison
        Entry(73, [10, 10, 10, 10], [120, 120, 120, 120], 0, 0, 2, SpellTargeting.Caster, SpellEffects.Utility),   // Hammerhands
        Entry(74, [15, 15, 15, 15], [120, 120, 120, 120], 0, 0, 3, SpellTargeting.Ally, SpellEffects.Condition),   // Cure Disease
        Entry(75, [20, 20, 20, 20], [120, 120, 120, 120], 0, 0, 3, SpellTargeting.Party, SpellEffects.Resistance),   // Protection from Magic
        Entry(76, [25, 25, 25, 25], [110, 110, 110, 100], 30, 5, 3, SpellTargeting.Foe, SpellEffects.Damage),   // Flying Fist
        Entry(77, [30, 30, 30, 30], [100, 100, 100, 100], 0, 0, 4, SpellTargeting.Ally, SpellEffects.Healing),   // Power Cure
        Entry(78, [5, 5, 5, 5], [110, 100, 90, 80], 0, 4, 1, SpellTargeting.Foe, SpellEffects.Damage),   // Light Bolt
        Entry(79, [10, 10, 10, 10], [120, 110, 100, 90], 16, 16, 1, SpellTargeting.Foe, SpellEffects.Damage),   // Destroy Undead
        Entry(80, [15, 15, 15, 15], [120, 110, 100, 90], 0, 0, 1, SpellTargeting.Foe, SpellEffects.Utility),   // Dispel Magic
        Entry(81, [20, 20, 20, 20], [160, 140, 120, 100], 0, 0, 1, SpellTargeting.Foe, SpellEffects.Condition),   // Paralyze
        Entry(82, [25, 25, 25, 25], [140, 140, 140, 140], 0, 0, 2, SpellTargeting.Caster, SpellEffects.Utility),   // Summon Elemental
        Entry(83, [30, 30, 30, 30], [500, 500, 500, 500], 0, 0, 2, SpellTargeting.Party, SpellEffects.Utility),   // Day of the Gods
        Entry(84, [35, 35, 35, 35], [135, 135, 120, 100], 25, 1, 2, SpellTargeting.Foe, SpellEffects.Damage),   // Prismatic Light
        Entry(85, [40, 40, 40, 40], [500, 500, 500, 500], 0, 0, 3, SpellTargeting.Party, SpellEffects.Resistance),   // Day of Protection
        Entry(86, [45, 45, 45, 45], [250, 250, 250, 250], 0, 0, 3, SpellTargeting.Party, SpellEffects.Utility),   // Hour of Power
        Entry(87, [50, 50, 50, 50], [150, 150, 150, 135], 20, 20, 3, SpellTargeting.Foe, SpellEffects.Damage),   // Sunray
        Entry(88, [55, 55, 55, 55], [300, 300, 300, 300], 0, 0, 4, SpellTargeting.Party, SpellEffects.Healing),   // Divine Intervention
        Entry(89, [10, 10, 10, 10], [140, 140, 140, 140], 0, 0, 1, SpellTargeting.Foe, SpellEffects.Utility),   // Reanimate
        Entry(90, [15, 15, 15, 15], [120, 110, 100, 90], 25, 10, 1, SpellTargeting.Foe, SpellEffects.Damage),   // Toxic Cloud
        Entry(91, [20, 20, 20, 20], [120, 100, 90, 120], 0, 0, 1, SpellTargeting.Ally, SpellEffects.Utility),   // Vampiric Weapon
        Entry(92, [25, 25, 25, 25], [120, 120, 120, 120], 0, 0, 1, SpellTargeting.Foe, SpellEffects.Condition),   // Shrinking Ray
        Entry(93, [30, 30, 30, 30], [90, 90, 80, 70], 6, 6, 2, SpellTargeting.Foe, SpellEffects.Damage),   // Shrapmetal
        Entry(94, [35, 35, 35, 35], [120, 120, 100, 80], 0, 0, 2, SpellTargeting.Foe, SpellEffects.Condition),   // Control Undead
        Entry(95, [40, 40, 40, 40], [110, 110, 110, 110], 0, 0, 2, SpellTargeting.Caster, SpellEffects.Utility),   // Pain Reflection
        Entry(96, [45, 45, 45, 45], [200, 200, 200, 150], 0, 0, 3, SpellTargeting.None, SpellEffects.Utility),   // Sacrifice
        Entry(97, [50, 50, 50, 50], [120, 120, 120, 100], 0, 25, 3, SpellTargeting.Foe, SpellEffects.Damage),   // Dragon Breath
        Entry(98, [55, 55, 55, 55], [250, 250, 250, 250], 50, 1, 3, SpellTargeting.Foe, SpellEffects.Damage),   // Armageddon
        Entry(99, [60, 60, 60, 60], [300, 300, 300, 300], 25, 8, 4, SpellTargeting.Foe, SpellEffects.Damage),   // Souldrinker
    ];

    private readonly Dictionary<SpellId, Facts> _facts;
    private readonly Dictionary<string, SpellDefinition> _byName;
    private readonly Dictionary<ItemDefinitionId, SpellId> _books;
    private readonly MightAndMagic7Skills? _skills;

    private MightAndMagic7Spells(
        SpellCatalog catalog,
        Dictionary<SpellId, Facts> facts,
        Dictionary<string, SpellDefinition> byName,
        Dictionary<ItemDefinitionId, SpellId> books,
        MightAndMagic7Skills? skills)
    {
        Catalog = catalog;
        _facts = facts;
        _byName = byName;
        _books = books;
        _skills = skills;
    }

    /// <inheritdoc />
    public SpellCatalog Catalog { get; }

    /// <summary>Reads this game's magic over the content the product loaded, or null when it loaded none.</summary>
    /// <param name="catalog">The validated content, or null when no bundle supplied any.</param>
    /// <param name="skills">
    /// This game's skill policy, when the caller composed one, so a refusal can name a rung of a school's
    /// ladder in the game's own words rather than as a number.
    /// </param>
    /// <returns>This game's magic, or null when there is no content to read it over.</returns>
    /// <exception cref="ContentValidationException">Content declares a spell this game has no numbers for; every problem is named.</exception>
    internal static MightAndMagic7Spells? Read(ContentCatalog? catalog, MightAndMagic7Skills? skills = null)
    {
        if (catalog is null) return null;

        List<ContentValidationIssue> issues = [];
        HashSet<string> declaredSkills = [.. catalog.Entries(MightAndMagic7Skills.SkillDefinitionKind).Select(entry => entry.Entry.Id)];
        List<SpellDefinition> definitions = [];
        Dictionary<SpellId, Facts> facts = [];
        Dictionary<string, SpellDefinition> byName = new(StringComparer.OrdinalIgnoreCase);

        foreach ((LoadedPack pack, ContentDocument document, ContentEntry entry) in catalog.Entries(SpellDefinitionKind))
        {
            void Defect(string code, string message) =>
                issues.Add(new ContentValidationIssue(code, message, pack.PackId, document.DocumentId));

            string name = entry.GetString(NameField);
            string school = entry.GetString(SchoolField);
            if (entry.Id.Length == 0 || name.Length == 0 || school.Length == 0)
            {
                Defect("spell-incomplete", $"a spell entry states id '{entry.Id}', name '{name}', and school '{school}', and a spell needs all three.");
                continue;
            }

            if (!int.TryParse(entry.Id, NumberStyles.None, CultureInfo.InvariantCulture, out int id) ||
                id < 1 || id > Table.Length)
            {
                Defect("spell-numbers-missing", $"spell '{entry.Id}' ({name}) is not one of the {ExpectedSpells} rows this game's table states numbers for.");
                continue;
            }

            Numbers numbers = Table[id - 1];
            if (!string.Equals(numbers.Id.ToString(CultureInfo.InvariantCulture), entry.Id, StringComparison.Ordinal))
            {
                Defect("spell-table-mismatch", $"spell '{entry.Id}' ({name}) stands where this game's table states spell {numbers.Id}.");
                continue;
            }

            // The school is the skill a caster's mastery is read from. Content that declares the spells but
            // not the school's skill is content whose spells nobody can learn, and that is said where it
            // matters rather than failing the load: a learning attempt refuses by name for the missing
            // school, and a partial pack — a staged counter, a fixture — still composes a session.
            _ = declaredSkills;
            SpellDefinition definition = new(
                new SpellId(entry.Id),
                name,
                school,
                new SkillId(school),
                new SkillTier(numbers.Tier),
                numbers.Mana[0],
                numbers.Targeting,
                numbers.Effect);
            definitions.Add(definition);
            facts[definition.Id] = new Facts(definition, numbers, MightAndMagic7Damage.Known(entry.GetString(ResistField).Trim()));
            byName[name] = definition;
        }

        // A spell book names the spell it teaches in the shipped item table's own reference column, which the
        // importer writes out as the spell's id; a book whose spell this content does not declare is a lesson
        // nothing could teach, so it is a defect rather than a purchase that silently does nothing.
        Dictionary<ItemDefinitionId, SpellId> books = [];
        foreach ((LoadedPack pack, ContentDocument document, ContentEntry entry) in catalog.Entries(ItemDefinitionKind))
        {
            if (!string.Equals(entry.GetString(EquipStatField), BookEquipStat, StringComparison.OrdinalIgnoreCase)) continue;
            string teaches = Taught(entry);
            if (teaches.Length == 0) continue;
            if (!facts.ContainsKey(new SpellId(teaches)))
            {
                issues.Add(new ContentValidationIssue(
                    "spell-book-unknown",
                    $"item '{entry.Id}' ({entry.GetString(NameField)}) is a book of spell '{teaches}', which this content does not declare.",
                    pack.PackId,
                    document.DocumentId));
                continue;
            }

            books[new ItemDefinitionId(entry.Id)] = new SpellId(teaches);
        }

        if (issues.Count > 0)
        {
            throw new ContentValidationException(
                $"This game's magic cannot be read: {issues[0].Message}",
                issues);
        }

        return new MightAndMagic7Spells(new SpellCatalog(definitions), facts, byName, books, skills);
    }

    /// <summary>What one member can hold of what casting spends, from the class, the level, and the scores.</summary>
    /// <remarks>
    /// The donor's own formula (OpenEnroth <c>src/Engine/Objects/Character.cpp:1845-1856</c>,
    /// <c>Character::GetMaxMana</c>): the class's base, plus how much one level of it adds times the level
    /// plus the bonus of the score the class casts from. The per-level figure is the same one the progression
    /// table grants a rising level, read from that owner rather than restated here, so a member's pool and
    /// this answer cannot drift apart: a character created at level one and raised to level ten holds exactly
    /// what this says.
    /// </remarks>
    /// <param name="member">The member whose capacity is being read.</param>
    /// <returns>How many spell points the member holds when full, never negative.</returns>
    /// <exception cref="ArgumentNullException">No member was supplied.</exception>
    public int SpellPointCapacity(PartyMember member)
    {
        ArgumentNullException.ThrowIfNull(member);
        string characterClass = MightAndMagic7Skills.BaseClassOf(member.Profile.Class.Value);
        if (!BaseMana.TryGetValue(characterClass, out int baseMana)) return 0;

        // A score the character does not carry adds nothing rather than the donor's penalty for a nought: a
        // record that omits a score has not said the character has none, and the donor's own creation always
        // states all seven, so the case is a partial record rather than a character.
        int scores = 0;
        foreach (AttributeId attribute in CastingScores.GetValueOrDefault(characterClass, []))
        {
            scores += member.Attributes.TryGet(attribute, out int score) ? ParameterBonus(score) : 0;
        }

        int perLevel = MightAndMagic7Progression.SpellPointsPerLevel(characterClass, member.Progression.ClassRank);
        long capacity = baseMana + ((long)perLevel * (member.Progression.Level + scores));
        return capacity < 0 ? 0 : (int)Math.Min(capacity, int.MaxValue);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The donor's mana column for the rung the member's mastery of the spell's school stands at
    /// (<c>src/Engine/Spells/Spells.cpp:162-168</c>, <c>mana_per_skill</c>, one price per rung). A member who
    /// has not learned the school at all pays the novice price the table states, because a price is a fact
    /// about the table rather than a permission: whether they may cast at all is a different answer.
    /// </remarks>
    public int CostFor(PartyMember member, SpellDefinition spell)
    {
        ArgumentNullException.ThrowIfNull(member);
        if (!_facts.TryGetValue(spell.Id, out Facts facts)) return 0;
        return facts.Numbers.Mana[Rung(member, spell) - 1];
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// The donor's own gate, read at the three places it is stated: a spell already in the spellbook is not
    /// learned twice; the character must know the school's skill at all (<c>val.level() == 0</c> refuses);
    /// and the member's mastery of that school must reach the rung the spell's own row requires
    /// (<c>requiredMastery &gt; val.mastery()</c> refuses) — OpenEnroth
    /// <c>src/Engine/Objects/Character.cpp:3361-3380</c>, the book's own learning path, whose refusal strings
    /// are "you already know the spell" and "you don't have the skill to learn".
    /// </para>
    /// <para>
    /// <b>What is not judged here.</b> What a book costs, whether the party is a member of the guild selling
    /// it, and whether the counter is open belong to the service mechanism and its own rules; the shipped
    /// item table's own note for a book states the one requirement it carries — "Your character must know the
    /// Fire magic skill to learn this spell" — which is the school skill this judges.
    /// </para>
    /// </remarks>
    public SpellRefusal? MayLearn(PartyMember member, SpellDefinition spell)
    {
        ArgumentNullException.ThrowIfNull(member);
        if (!Catalog.Declares(spell.Id)) return SpellRefusal.Unknown(spell.Id.Value);
        if (member.Spells.Knows(spell.Id)) return SpellRefusal.AlreadyKnown(member.Profile.Name, spell.Name);
        if (member.Skills.LevelOf(spell.SchoolSkill) <= 0)
        {
            return SpellRefusal.SchoolMissing(member.Profile.Name, spell.Name, spell.SchoolSkill.Value);
        }

        SkillTier held = member.Skills.TierOf(spell.SchoolSkill);
        if (held.Value >= spell.Tier.Value) return null;
        return SpellRefusal.MasteryTooLow(member.Profile.Name, spell.Name, RungName(spell.Tier), RungName(held));
    }

    /// <summary>How long one casting of a spell makes its caster recover, in the donor's own ticks.</summary>
    /// <param name="member">The member casting it.</param>
    /// <param name="spell">The spell being cast.</param>
    /// <returns>The recovery the donor's row states for the member's rung, in ticks.</returns>
    internal int RecoveryTicks(PartyMember member, SpellDefinition spell)
    {
        ArgumentNullException.ThrowIfNull(member);
        if (!_facts.TryGetValue(spell.Id, out Facts facts)) return 0;
        return facts.Numbers.Recovery[Rung(member, spell) - 1];
    }

    /// <summary>The spell content declares under an identity, or null when nothing does.</summary>
    /// <param name="id">The spell's own identity.</param>
    internal SpellDefinition? Spell(string id) =>
        id.Length > 0 && Catalog.Declares(new SpellId(id)) ? Catalog.Read(new SpellId(id)) : null;

    /// <summary>The spell a name denotes, which is how a monster row's spell cell is joined to the catalog.</summary>
    /// <param name="name">The spell's name, as the shipped table writes it.</param>
    internal SpellDefinition? SpellByName(string name) =>
        name.Length > 0 && _byName.TryGetValue(name, out SpellDefinition spell) ? spell : null;

    /// <summary>What kind of harm a spell does, or null when its content states none.</summary>
    /// <param name="spell">The spell being read.</param>
    internal DamageKindId? Harm(SpellDefinition spell) =>
        _facts.TryGetValue(spell.Id, out Facts facts) ? facts.Harm : null;

    /// <summary>What one casting of a spell rolls, at the caster's own skill level in its school.</summary>
    /// <remarks>
    /// The donor's own damage expression (<c>CalcSpellDamage</c>,
    /// <c>OpenEnroth/src/Engine/Spells/Spells.cpp:813-838</c>): the row's base damage plus that many dice of
    /// the row's per-skill die as the caster's skill in the school. A spell whose row states no per-skill die
    /// rolls its base alone, which is what the donor's ordinary case reduces to.
    /// </remarks>
    /// <param name="spell">The spell being cast.</param>
    /// <param name="skillLevel">How many levels of the spell's school the caster holds.</param>
    /// <returns>The roll.</returns>
    internal DamageRoll Damage(SpellDefinition spell, int skillLevel)
    {
        if (!_facts.TryGetValue(spell.Id, out Facts facts)) return DamageRoll.Flat(0);
        Numbers numbers = facts.Numbers;
        int dice = Math.Max(0, skillLevel);
        return numbers.Skill > 0 && dice > 0
            ? new DamageRoll(dice, numbers.Skill, numbers.Base)
            : DamageRoll.Flat(numbers.Base);
    }

    /// <summary>How many levels of a spell's school the caster holds, which is what its damage scales by.</summary>
    /// <param name="member">The member casting it.</param>
    /// <param name="spell">The spell being cast.</param>
    internal static int SkillLevelOf(PartyMember member, SpellDefinition spell)
    {
        ArgumentNullException.ThrowIfNull(member);
        return member.Skills.LevelOf(spell.SchoolSkill);
    }

    /// <summary>
    /// Whether a guild standing at one rung of its school's ladder may sell a spell of that school.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Ours, over the donor's own guild ladder.</b> The shipped data carries no guild-to-spell table
    /// ([`docs/research/mm7-data-inventory.md`](../../../docs/research/mm7-data-inventory.md), <i>Magic</i>,
    /// and [`docs/gameplay-design.md`](../../../docs/gameplay-design.md) §3.12), so the gate is authored here.
    /// What the donor does state is how deep a guild's knowledge runs: a guild's house is its rung in its own
    /// school (<c>src/GUI/UI/Houses/MagicGuild.cpp:64-99</c>, <c>guildSpellsMastery</c>), and the depth is
    /// turned into a number of spells — four at novice, seven at expert, ten at master, eleven at grand
    /// master (<c>src/Engine/Objects/CharacterEnumFunctions.h:33-46</c>, <c>spellCountForMastery</c>) — which
    /// the donor reads as a range of book ids (<c>src/Engine/Objects/ItemEnumFunctions.h:107-114</c>,
    /// <c>spellbooksForSchool</c>). Those counts are exactly the band counts of the mastery tiers: four
    /// spells require novice, three more expert, three more master, and the last grand master, so a guild of
    /// one rung sells every spell whose tier is at most that rung.
    /// </para>
    /// <para>
    /// <b>This is the gate the ordinal reading gives.</b> A ship that numbered spells by the shipped
    /// <c>Lvl</c> column would sell Light's Paralyze (whose cell reads six, but whose row is the school's
    /// fourth and whose mastery is novice) only at a master guild, and would sell Summon Elemental before it;
    /// the tier is the donor's own answer and the counts agree with it in all nine schools.
    /// </para>
    /// </remarks>
    /// <param name="guildRung">The rung the guild stands at in its own school, counting from one.</param>
    /// <param name="spell">The spell its shelves would hold.</param>
    internal static bool Sells(int guildRung, SpellDefinition spell) => spell.Tier.Value <= Math.Clamp(guildRung, 1, Rungs);

    /// <summary>The spell a book teaches, or null when the item is not a book of one.</summary>
    /// <param name="book">The item definition.</param>
    internal SpellId? TaughtBy(ItemDefinitionId book) =>
        _books.TryGetValue(book, out SpellId spell) ? spell : null;

    /// <summary>The spell a shipped book's own reference column names, empty when it names none.</summary>
    /// <remarks>
    /// A book's row carries the spell it teaches in the item table's own reference column, written as the
    /// letter <c>S</c> and the spell's global id (item 400 is "Torch Light" with <c>S1</c>, item 498 is
    /// "Souldrinker" with <c>S99</c>). The importer writes that join out as a field of its own, and this
    /// reads the field first and falls back to the shipped spelling, so a pack written before the field
    /// existed still teaches its spells.
    /// </remarks>
    /// <param name="entry">The item's content entry.</param>
    private static string Taught(ContentEntry entry)
    {
        string written = ContentEntry.ReadId(entry.Payload, TeachesField);
        if (written.Length > 0) return written;

        string reference = entry.GetString(DamageDiceField).Trim();
        return reference.StartsWith(BookSpellPrefix, StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(reference[BookSpellPrefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out int id) && id > 0
            ? id.ToString(CultureInfo.InvariantCulture)
            : string.Empty;
    }

    /// <summary>The rung of a spell's school the member's mastery stands at, never below the first.</summary>
    private static int Rung(PartyMember member, SpellDefinition spell) =>
        Math.Clamp(member.Skills.TierOf(spell.SchoolSkill).Value, 1, Rungs);

    /// <summary>What one rung of a skill's ladder is called, in this game's own words.</summary>
    private string RungName(SkillTier tier) =>
        _skills is { } skills ? skills.TierName(tier) : tier.Value.ToString(CultureInfo.InvariantCulture);

    /// <summary>The donor's bonus for one attribute score, from the step table.</summary>
    private static int ParameterBonus(int score)
    {
        foreach ((int floor, int bonus) in ParameterBonusSteps)
        {
            if (score >= floor) return bonus;
        }

        return ParameterBonusSteps[^1].Bonus;
    }

    /// <summary>One spell's authored row: what it costs and recovers at each rung, and what it does.</summary>
    /// <param name="Id">The spell's global id, which is the row's own place plus one.</param>
    /// <param name="Mana">What one casting costs at each of the four rungs.</param>
    /// <param name="Recovery">How long the caster recovers at each rung, in the donor's ticks.</param>
    /// <param name="Base">The flat damage the spell adds to its dice.</param>
    /// <param name="Skill">How many faces each die has, one die per level of the caster's school skill.</param>
    /// <param name="Tier">The rung of the school's ladder the spell requires.</param>
    /// <param name="Targeting">What the spell is aimed at.</param>
    /// <param name="Effect">Which of the eight categories the spell's effect is.</param>
    private readonly record struct Numbers(
        int Id,
        int[] Mana,
        int[] Recovery,
        int Base,
        int Skill,
        int Tier,
        SpellTargeting Targeting,
        string Effect);

    /// <summary>One spell's reading: its definition, its authored numbers, and the harm its content states.</summary>
    /// <param name="Definition">The definition the catalog carries.</param>
    /// <param name="Numbers">The authored numbers.</param>
    /// <param name="Harm">What kind of harm the spell does, or null when its content states none.</param>
    private readonly record struct Facts(SpellDefinition Definition, Numbers Numbers, DamageKindId? Harm);

    /// <summary>States one authored row, so the table above reads as the donor's own columns.</summary>
    private static Numbers Entry(
        int id,
        int[] mana,
        int[] recovery,
        int baseDamage,
        int skillDamage,
        int tier,
        SpellTargeting targeting,
        string effect) =>
        new(id, mana, recovery, baseDamage, skillDamage, tier, targeting, effect);
}
