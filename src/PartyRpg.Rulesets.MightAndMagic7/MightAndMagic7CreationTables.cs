using PartyRpg.Kit.Party;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// This game's authored creation tables: the races, portraits and default party, and the per-class terms
/// creation offers.
/// </summary>
/// <remarks>
/// <para>
/// <b>These values are ours where the shipped data does not carry them.</b> The extracted tables have no
/// race table at all and no class-to-skill availability table: the per-race creation ranges and the
/// per-class skill lists live in the executable, as
/// [`docs/research/mm7-data-inventory.md`](../../../docs/research/mm7-data-inventory.md) records under
/// <i>Characters</i> and <i>Skills</i>, and the design says so in
/// [`docs/gameplay-design.md`](../../../docs/gameplay-design.md) §3.12. Nothing here is imported; the
/// numbers are informed by the two donor transcriptions of that executable data and checked against the
/// manual's own examples:
/// </para>
/// <list type="bullet">
/// <item>
/// Race attribute ranges: the manual's race table anchors —
/// [`docs/research/mm7-manual-outline.md`](../../../docs/research/mm7-manual-outline.md) §1 (printed p.17)
/// gives Human 9/11/25, Elf Intellect 12/14/30, Goblin Might 12/14/30, Dwarf Endurance 12/14/30 and Dwarf
/// Accuracy 5/7/15 — and the donor's transcription of the executable's per-race stat table,
/// <c>OpenEnroth/src/Engine/Objects/Character.cpp</c> (<c>StatTable</c>, whose <c>uBaseValue</c>,
/// <c>uMaxValue</c>, <c>uDroppedStep</c> and <c>uBaseStep</c> are read here as start, ceiling, step cost and
/// step size). Every manual anchor agrees with it, and the manual's "may be lowered 2 below its start"
/// (p.12) is where each floor comes from.
/// </item>
/// <item>
/// Class to skill availability: the donor's transcription of the executable's table,
/// <c>OpenEnroth/src/Engine/mm7_data.cpp</c> (<c>pSkillAvailabilityPerClass</c>, annotated there as read
/// from <c>MM7.exe::004ED820</c>), whose <c>PRIMARY</c> entries are the two skills a class fixes and whose
/// <c>AVAILABLE</c> entries are the skills a player may choose. The donor's two hidden rows (<c>SKILL_CLUB</c>
/// and <c>SKILL_MISC</c>) are left out because the shipped skill table does not carry them —
/// [`docs/research/mm7-data-inventory.md`](../../../docs/research/mm7-data-inventory.md) records the shipped
/// table at 37 rows against the donor's 39.
/// </item>
/// <item>
/// Starting hit and spell points per class: the manual's examples (p.17) and the donor's base arrays
/// (<c>OpenEnroth/src/Engine/Objects/Character.cpp</c>, <c>pBaseHealthByClass</c> and
/// <c>pBaseManaByClass</c>, indexed by base class) agree on every base class.
/// </item>
/// <item>
/// Races' and portraits' identities, the portrait set, the default party, and the starting purse and larder
/// are ours outright: the shipped data carries no portrait table (only art), and the manual gives no
/// starting amount (manual outline §1, §9).
/// </item>
/// </list>
/// <para>
/// The seven attributes keep the manual's names and order — Might, Intellect, Personality, Endurance,
/// Accuracy, Speed, Luck (manual pp.12–13) — because a player reads them.
/// </para>
/// </remarks>
internal static class MightAndMagic7CreationTables
{
    /// <summary>Might: melee damage (manual p.13).</summary>
    private static readonly AttributeId Might = new("Might");

    /// <summary>Intellect: spell points for the elemental casters (manual p.13).</summary>
    private static readonly AttributeId Intellect = new("Intellect");

    /// <summary>Personality: spell points for the casters of the magic of the self (manual p.13).</summary>
    private static readonly AttributeId Personality = new("Personality");

    /// <summary>Endurance: hit points (manual p.13).</summary>
    private static readonly AttributeId Endurance = new("Endurance");

    /// <summary>Accuracy: the chance to hit (manual p.13).</summary>
    private static readonly AttributeId Accuracy = new("Accuracy");

    /// <summary>Speed: how often a character acts and recovers (manual p.13).</summary>
    private static readonly AttributeId Speed = new("Speed");

    /// <summary>Luck: magic resistance and trap damage (manual p.13).</summary>
    private static readonly AttributeId Luck = new("Luck");

    /// <summary>The four races creation offers, each with the attribute table a portrait choice brings.</summary>
    /// <remarks>
    /// Read as the donor's <c>{uBaseValue, uMaxValue, uDroppedStep, uBaseStep}</c> becomes
    /// <c>(start, start − 2, ceiling, step size, step cost)</c>: above the start an adjustment moves the step
    /// size and costs the step cost, below it the two swap, which is what makes a refund exactly undo a
    /// purchase.
    /// </remarks>
    internal static IReadOnlyList<CreationRace> Races { get; } =
    [
        new CreationRace(new RaceId("Human"), "Human",
        [
            Range(Might, 11, 25, 1, 1),
            Range(Intellect, 11, 25, 1, 1),
            Range(Personality, 11, 25, 1, 1),
            Range(Endurance, 9, 25, 1, 1),
            Range(Accuracy, 11, 25, 1, 1),
            Range(Speed, 11, 25, 1, 1),
            Range(Luck, 9, 25, 1, 1),
        ]),
        new CreationRace(new RaceId("Elf"), "Elf",
        [
            Range(Might, 7, 15, 1, 2),
            Range(Intellect, 14, 30, 2, 1),
            Range(Personality, 11, 25, 1, 1),
            Range(Endurance, 7, 15, 1, 2),
            Range(Accuracy, 14, 30, 2, 1),
            Range(Speed, 11, 25, 1, 1),
            Range(Luck, 9, 20, 1, 1),
        ]),
        new CreationRace(new RaceId("Goblin"), "Goblin",
        [
            Range(Might, 14, 30, 2, 1),
            Range(Intellect, 7, 15, 1, 2),
            Range(Personality, 7, 15, 1, 2),
            Range(Endurance, 11, 25, 1, 1),
            Range(Accuracy, 11, 25, 1, 1),
            Range(Speed, 14, 30, 2, 1),
            Range(Luck, 9, 20, 1, 1),
        ]),
        new CreationRace(new RaceId("Dwarf"), "Dwarf",
        [
            Range(Might, 14, 30, 2, 1),
            Range(Intellect, 11, 25, 1, 1),
            Range(Personality, 11, 25, 1, 1),
            Range(Endurance, 14, 30, 2, 1),
            Range(Accuracy, 7, 15, 1, 2),
            Range(Speed, 7, 15, 1, 2),
            Range(Luck, 9, 20, 1, 1),
        ]),
    ];

    /// <summary>The portraits creation offers, two for each race.</summary>
    /// <remarks>
    /// Ours: the installed game carries portrait art frames but no portrait table, and which face a player
    /// may pick is presentation the ruleset owns. A portrait is drawn as one race, which is why each of these
    /// names the race it belongs to.
    /// </remarks>
    internal static IReadOnlyList<CreationPortrait> Portraits { get; } =
    [
        Portrait("human-woman", "Human", "Human woman"),
        Portrait("human-man", "Human", "Human man"),
        Portrait("elf-woman", "Elf", "Elf woman"),
        Portrait("elf-man", "Elf", "Elf man"),
        Portrait("goblin-woman", "Goblin", "Goblin woman"),
        Portrait("goblin-man", "Goblin", "Goblin man"),
        Portrait("dwarf-woman", "Dwarf", "Dwarf woman"),
        Portrait("dwarf-man", "Dwarf", "Dwarf man"),
    ];

    /// <summary>The nine base classes, with the skills each fixes and offers and what it starts with.</summary>
    /// <remarks>
    /// Fixed skills come from the donor's <c>PRIMARY</c> entries and the choices from its <c>AVAILABLE</c>
    /// entries, in the donor's own order; the hidden club and miscellaneous skills are dropped because the
    /// shipped skill table does not carry them. Starting hit and spell points are the manual's examples and
    /// the donor's base arrays, which agree: 40/0 Knight, 35/0 Thief, 35/0 Monk, 30/5 Paladin, 30/5 Archer,
    /// 30/0 Ranger, 25/10 Cleric, 20/10 Druid, 20/15 Sorcerer.
    /// </remarks>
    internal static IReadOnlyList<CreationClass> Classes { get; } =
    [
        Class("Knight", 40, 0, ["Sword", "Leather"],
            ["Axe", "Spear", "Bow", "Mace", "Shield", "Chain", "Bodybuilding", "Perception", "Armsmaster"]),
        Class("Thief", 35, 0, ["Dagger", "Stealing"],
            ["Sword", "Bow", "Leather", "Identify Item", "Merchant", "Perception", "Disarm Traps", "Dodging", "Alchemy"]),
        Class("Monk", 35, 0, ["Dodging", "Unarmed"],
            ["Staff", "Sword", "Dagger", "Spear", "Leather", "Bodybuilding", "Perception", "Identify Monster", "Armsmaster"]),
        Class("Paladin", 30, 5, ["Mace", "Spirit"],
            ["Sword", "Dagger", "Axe", "Shield", "Leather", "Merchant", "Repair", "Bodybuilding", "Armsmaster"]),
        Class("Archer", 30, 5, ["Bow", "Air"],
            ["Sword", "Axe", "Spear", "Leather", "Fire", "Water", "Perception", "Armsmaster", "Learning"]),
        Class("Ranger", 30, 0, ["Axe", "Perception"],
            ["Sword", "Dagger", "Bow", "Leather", "Bodybuilding", "Disarm Traps", "Dodging", "Identify Monster", "Armsmaster"]),
        Class("Cleric", 25, 10, ["Mace", "Body"],
            ["Shield", "Leather", "Spirit", "Mind", "Merchant", "Repair", "Meditation", "Alchemy", "Learning"]),
        Class("Druid", 20, 10, ["Dagger", "Earth"],
            ["Mace", "Leather", "Water", "Spirit", "Body", "Meditation", "Perception", "Alchemy", "Learning"]),
        Class("Sorcerer", 20, 15, ["Staff", "Fire"],
            ["Dagger", "Leather", "Air", "Water", "Earth", "Identify Item", "Merchant", "Identify Monster", "Alchemy"]),
    ];

    /// <summary>The default party creation starts from, offered to a player rather than imposed on one.</summary>
    /// <remarks>
    /// Ours: the original opens its creation screen with a default party already built
    /// ([`docs/research/mm7-manual-outline.md`](../../../docs/research/mm7-manual-outline.md) §1, pp.10–11)
    /// but the shipped data carries no default party to import. Each member's attributes spend the pool of
    /// fifty exactly, and each member's chosen skills are on its class's choice list — the flow checks both
    /// again when it applies them.
    /// </remarks>
    internal static PartyCreationDefaults DefaultParty { get; } = new(
    [
        Default("human-man", "Knight", "Roderick",
            [Score(Might, 21), Score(Intellect, 15), Score(Personality, 15), Score(Endurance, 19),
                Score(Accuracy, 19), Score(Speed, 17), Score(Luck, 17)],
            ["Shield", "Bodybuilding"]),
        Default("elf-woman", "Sorcerer", "Aelina",
            [Score(Might, 9), Score(Intellect, 30), Score(Personality, 15), Score(Endurance, 9),
                Score(Accuracy, 30), Score(Speed, 25), Score(Luck, 17)],
            ["Leather", "Air"]),
        Default("dwarf-man", "Cleric", "Borin",
            [Score(Might, 20), Score(Intellect, 14), Score(Personality, 25), Score(Endurance, 30),
                Score(Accuracy, 11), Score(Speed, 9), Score(Luck, 19)],
            ["Spirit", "Meditation"]),
        Default("goblin-woman", "Thief", "Nyx",
            [Score(Might, 24), Score(Intellect, 7), Score(Personality, 7), Score(Endurance, 25),
                Score(Accuracy, 25), Score(Speed, 30), Score(Luck, 18)],
            ["Perception", "Disarm Traps"]),
    ]);

    /// <summary>States one attribute's creation range, taking its floor from the game's "two below the start".</summary>
    private static AttributeCreationRange Range(AttributeId attribute, int start, int ceiling, int stepSize, int stepCost) =>
        new(attribute, attribute.Value, start, start - 2, ceiling, stepSize, stepCost);

    /// <summary>States one portrait choice.</summary>
    private static CreationPortrait Portrait(string id, string race, string name) =>
        new(new PortraitId(id), new RaceId(race), name);

    /// <summary>States one base class's creation terms.</summary>
    private static CreationClass Class(
        string id,
        int hitPoints,
        int spellPoints,
        string[] fixedSkills,
        string[] choosableSkills) =>
        new(
            new ClassId(id),
            id,
            [.. fixedSkills.Select(skill => new SkillId(skill))],
            [.. choosableSkills.Select(skill => new SkillId(skill))],
            hitPoints,
            spellPoints,
            MightAndMagic7Creation.StartingRank);

    /// <summary>States one default member, whose class rank is the base rung.</summary>
    private static CreationMemberDefaults Default(
        string portrait,
        string characterClass,
        string name,
        AttributeScore[] attributes,
        string[] chosenSkills) =>
        new(
            new PortraitId(portrait),
            new ClassId(characterClass),
            name,
            attributes,
            [.. chosenSkills.Select(skill => new SkillId(skill))]);

    /// <summary>States one attribute's default score.</summary>
    private static AttributeScore Score(AttributeId attribute, int value) => new(attribute, value);
}
