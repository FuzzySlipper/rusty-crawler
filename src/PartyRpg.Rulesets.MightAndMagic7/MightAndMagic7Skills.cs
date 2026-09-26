using System.Globalization;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Skills;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// This game's skills: which of the shipped rows it uses and what block each is in, how far a class and rank
/// let a skill grow, what a raise costs, what a mastery teacher requires, and what each rung reads as.
/// </summary>
/// <remarks>
/// <para>
/// <b>The catalog is content's; the reading is ours.</b> The shipped table carries 37 rows and this game
/// uses 34 of them, in the manual's own four blocks — 8 weapon, 5 armour, 9 magic, 12 miscellaneous
/// ([`docs/research/mm7-manual-outline.md`](../../../docs/research/mm7-manual-outline.md) §2, printed
/// pp.38–41). The three rows the blocks do not carry are kept in the catalog and reported as unused rather
/// than dropped, because they are the shipped table's own history and an operator checking an import wants
/// to see them: <c>Blaster</c> (two shipped items require it and the donor's class table lets several
/// classes grand-master it, but the manual's weapon block does not carry it — the donor's
/// <c>Skill</c> enum has it for MM7, <c>src/Engine/Objects/CharacterEnums.h:14</c>),
/// <c>Diplomacy</c> (the donor marks it "Not used in MM7", <c>CharacterEnums.h:27</c>, and its shipped
/// grand-master text is the placeholder "Grandmaster text here"), and <c>Thievery</c> (its shipped
/// description reads "Not Used", its four effect texts are placeholders, and no class row of the donor's
/// table grants it at all — <c>src/Engine/mm7_data.cpp</c>, <c>skillMaxMasteryPerClass</c>).
/// </para>
/// <para>
/// <b>The ceilings are the donor's transcription of the executable, and this is not a file we imported.</b>
/// No class-to-skill table exists in the shipped data — the inventory records why
/// ([`docs/research/mm7-data-inventory.md`](../../../docs/research/mm7-data-inventory.md), <i>Skills</i>,
/// and [`docs/gameplay-design.md`](../../../docs/gameplay-design.md) §3.12) — so the per-class mastery
/// ceilings below are re-expressed from the donor's transcription of <c>MM7.exe::004ED820</c>
/// (<c>OpenEnroth/src/Engine/mm7_data.cpp</c>, <c>skillMaxMasteryPerClass</c>, 36 class rows × 39 skills),
/// and the design records that our ceilings are authored rather than extracted. The matrix is transcribed
/// row for row in the donor's own skill order; every value is the donor's, and the test beside this file
/// checks a sample of them against the donor's own numbers.
/// </para>
/// <para>
/// <b>The level ceiling is ours.</b> The donor caps a <em>skill's effective value</em> at a flat 60 for
/// every class (<c>src/Engine/mm7_data.cpp:339</c>, <c>skills_max_level</c>) and lets a character buy
/// levels up to it whatever their class; the design says how far a skill may grow is capped by class and
/// rank (§3.2), which the shipped data does not state, so this game authors the ladder: each rung of a
/// skill's ladder carries a level band, each promotion raises it, and the donor's own 60 remains the
/// absolute top. The rung thresholds themselves are the donor's own — a mastery teacher wants the skill at
/// level 4 for expert, 7 for master, and 10 for grand master
/// (<c>src/GUI/UI/NPCTopics.cpp:481-527</c>) — and the bands sit just above them, so a class that may only
/// reach expert can raise the skill past the master threshold and still never learn the master rung.
/// </para>
/// <para>
/// <b>A rung is learned from a person, not bought with points.</b> <see cref="Lesson"/> is what a counter
/// answers when it offers a rung: whether the member's class may ever hold it, whether the promotions above
/// them would open it, whether they already hold the rung below, whether the skill is high enough, and the
/// donor's own extra conditions. What a lesson costs and how it is applied stay the service mechanism's.
/// </para>
/// </remarks>
internal sealed class MightAndMagic7Skills : ISkillRule
{
    /// <summary>The definition kind the shipped skill table is declared under.</summary>
    internal const string SkillDefinitionKind = "skill";

    /// <summary>The definition kind the shipped class ladder is declared under.</summary>
    internal const string ClassDefinitionKind = "class";

    /// <summary>The rung a skill is learned at, which the shipped table calls "normal".</summary>
    internal const int BasicRung = 1;

    /// <summary>The expert rung, the first a mastery teacher can grant.</summary>
    internal const int ExpertRung = 2;

    /// <summary>The master rung.</summary>
    internal const int MasterRung = 3;

    /// <summary>The grand master rung, the top of the ladder.</summary>
    internal const int GrandMasterRung = 4;

    /// <summary>
    /// The skill level a mastery teacher wants before teaching a rung, indexed by the rung.
    /// </summary>
    /// <remarks>
    /// OpenEnroth <c>src/GUI/UI/NPCTopics.cpp:481-527</c> (<c>masteryTeacherOptionString</c>): expert at
    /// level 4, master at 7, grand master at 10. The donor also requires the rung below — an expert before a
    /// master, a master before a grand master — which <see cref="Lesson"/> judges.
    /// </remarks>
    internal static readonly int[] TeacherLevels = [0, 0, 4, 7, 10];

    /// <summary>
    /// The attribute a member must have before a mastery teacher will take them to the master rung.
    /// </summary>
    /// <remarks>
    /// The donor's own three exceptions (<c>src/GUI/UI/NPCTopics.cpp:492-505</c>): a master of the
    /// merchant's trade wants a base personality of fifty, a master of bodybuilding a base endurance of
    /// fifty, and a master of learning a base intellect of fifty; every other skill's master rung asks for
    /// the level alone. Those are the donor's numbers on a scale this build's creation ranges do not reach
    /// (they run 15–30 by race, [`docs/research/mm7-manual-outline.md`](../../../docs/research/mm7-manual-outline.md)
    /// §1), so a member grown only by levels meets them when content states such a score or when attribute
    /// growth lands; the requirement is judged rather than skipped, exactly as the donor states it.
    /// </remarks>
    private static readonly Dictionary<string, (AttributeId Attribute, int Score)> MasterGates =
        new(StringComparer.Ordinal)
        {
            ["Merchant"] = (new AttributeId("Personality"), 50),
            ["Bodybuilding"] = (new AttributeId("Endurance"), 50),
            ["Learning"] = (new AttributeId("Intellect"), 50),
        };

    /// <summary>
    /// A grand-master rung whose teacher wants another skill at level ten first.
    /// </summary>
    /// <remarks>
    /// The donor's own pair (<c>src/GUI/UI/NPCTopics.cpp:512-519</c>): dodging's grand master wants unarmed
    /// combat at ten, and unarmed combat's wants dodging at ten. Every other rung's requirement is the
    /// level and the rung below.
    /// </remarks>
    private static readonly Dictionary<string, string> GrandMasterGates = new(StringComparer.Ordinal)
    {
        ["Dodging"] = "Unarmed",
        ["Unarmed"] = "Dodging",
    };

    /// <summary>The skill whose grand-master gate is the unarmed skill, and the one that wants dodging.</summary>
    private const int CompanionLevel = 10;

    /// <summary>
    /// How many levels each rung of a skill's ladder allows before a promotion, indexed by the rung.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Ours.</b> The shipped data states no per-class level cap, and the design says the class and rank
    /// cap how far a skill grows (§3.2), so the bands are authored here and recorded as ours: a skill whose
    /// class ceiling is the basic rung may reach six, one that reaches expert nine, one that reaches master
    /// twelve, and one that may reach grand master runs to the donor's own flat 60. The first three sit just
    /// above the donor's own thresholds for the rung above them (4, 7, 10,
    /// <c>src/GUI/UI/NPCTopics.cpp:481-527</c>), so a class is never stopped below the level its own rung
    /// needed.
    /// </para>
    /// <para>
    /// <b>Ours as well:</b> how much a promotion raises the band. Six levels per rank above the first, which
    /// makes a rank a real widening rather than a formality and keeps every ceiling the donor's own absolute
    /// 60 at the top (<c>src/Engine/mm7_data.cpp:339</c>, <c>skills_max_level</c>).
    /// </para>
    /// </remarks>
    private static readonly int[] LevelBands = [0, 6, 9, 12, 60];

    /// <summary>How many levels a promotion above the first adds to a skill's ceiling (ours).</summary>
    private const int RankLevelStep = 6;

    /// <summary>
    /// The donor's own flat cap on a skill, which no ceiling this game authors goes above.
    /// </summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/mm7_data.cpp:339</c> (<c>skills_max_level</c>): every visible skill caps at
    /// sixty, and the two hidden rows at one. It is the top of this game's ladder as well.
    /// </remarks>
    internal const int DonorLevelCap = 60;

    /// <summary>
    /// The donor's own skill order, in the shipped table's names, which the ceilings below are written in.
    /// </summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/Objects/CharacterEnums.h:9-48</c> (<c>enum class Skill</c>) and the row order
    /// of <c>skillMaxMasteryPerClass</c> (<c>src/Engine/mm7_data.cpp:763</c>). The donor's enum names are
    /// written here as the shipped table's own words — <c>SKILL_ITEM_ID</c> is "Identify Item",
    /// <c>SKILL_TRAP_DISARM</c> is "Disarm Traps", <c>SKILL_MONSTER_ID</c> is "Identify Monster",
    /// <c>SKILL_DODGE</c> is "Dodging", <c>SKILL_BODYBUILDING</c> is "Bodybuilding" — so a column is found by
    /// the identity content declares. The last two, <c>Club</c> and <c>Misc</c>, are the donor's hidden rows
    /// the shipped table does not carry
    /// ([`docs/research/mm7-data-inventory.md`](../../../docs/research/mm7-data-inventory.md), <i>Skills</i>);
    /// they are kept so the transcription is the donor's whole array, and nothing reads them.
    /// </remarks>
    private static readonly string[] Columns =
    [
        "Staff", "Sword", "Dagger", "Axe", "Spear", "Bow", "Mace", "Blaster", "Shield", "Leather",
        "Chain", "Plate", "Fire", "Air", "Water", "Earth", "Spirit", "Mind", "Body", "Light", "Dark",
        "Identify Item", "Merchant", "Repair", "Bodybuilding", "Meditation", "Perception", "Diplomacy",
        "Thievery", "Disarm Traps", "Dodging", "Unarmed", "Identify Monster", "Armsmaster", "Stealing",
        "Alchemy", "Learning", "Club", "Misc",
    ];

    /// <summary>
    /// The per-class mastery ceilings, transcribed from the donor's own table of the executable's array.
    /// </summary>
    /// <remarks>
    /// <para>
    /// OpenEnroth <c>src/Engine/mm7_data.cpp:763-1050</c>, <c>skillMaxMasteryPerClass</c>, itself annotated
    /// there as read from <c>MM7.exe::004ED820</c>: 36 class rows, each a ceiling per skill in the donor's
    /// skill order. The letters are the donor's own <c>Mastery</c> enum
    /// (<c>src/Engine/Objects/CharacterEnums.h:31-39</c>): <c>-</c> none, <c>N</c> novice, <c>E</c> expert,
    /// <c>M</c> master, <c>G</c> grand master. The rows appear in the donor's class order
    /// (<c>CharacterEnums.h:52-95</c>, <c>enum class Class</c>), four per family: the base class, its first
    /// promotion, and its two second promotions, which are the light and dark alternatives.
    /// </para>
    /// <para>
    /// Nothing here is generated at run time and nothing is read from a file: an operator's install carries
    /// no such table, and this is the transcription the design records as ours-informed-by-the-donor.
    /// </para>
    /// </remarks>
    private static readonly Dictionary<string, string> MasteryRows = new(StringComparer.Ordinal)
    {
        ["Knight"] = "EMEMMEMGMMMM----------EMM-EN-NEE-M--NNN",
        ["Cavalier"] = "EMEMMEMGMMMM----------EMM-EN-NEE-M--NNN",
        ["Champion"] = "EGEMGEMGGMMG----------EGG-EN-NEE-G--NNN",
        ["Black Knight"] = "EGEMGEMGGMMG----------EGG-EN-NEE-G--NNN",
        ["Thief"] = "-MM--EEGNME----------MMNE-M--MME-MMEENN",
        ["Rogue"] = "-MM--EEGNME-NNNN-----MMNE-M--MME-MMEENN",
        ["Spy"] = "-MG--EEGNGE-NNNN-----MMNE-M--GME-MGEENN",
        ["Assassin"] = "-MG--EEGNGE-NNNN-----MMNE-M--GME-MGEENN",
        ["Monk"] = "MEE-EN-G-M--------------M-EN-NMMEMN-MNN",
        ["Initiate"] = "MEE-EN-G-M------NNN-----M-EN-EMMEMN-MNN",
        ["Master"] = "GEE-EN-G-M------EEE-----G-EN-EGGEMN-GNN",
        ["Ninja"] = "GEE-EN-G-M------NNN-----G-EN-MGGEME-GNN",
        ["Paladin"] = "NMEEEEMGMEEM----NNN---EMMENM--NN-E--NNN",
        ["Crusader"] = "NMEEEEMGMEEM----EEE---EMMENM--NN-E--NNN",
        ["Hero"] = "NMEEEEGGGEEM----MMMN--EGMENG--NN-E--NNN",
        ["Villain"] = "NMEEEEGGGEEM----MMM-N-EGMENG--NN-E--NNN",
        ["Archer"] = "NEEEMM-G-MM-NNNN------EEEEM--NEN-E--MNN",
        ["Warrior Mage"] = "NEEEMM-G-MM-EEEE------EEEEM--NEN-E--MNN",
        ["Master Archer"] = "NEEEMG-G-MG-MMMM---N--EEEEG--EEN-E--MNN",
        ["Sniper"] = "NEEEMG-G-MG-MMMM----N-EEEEG--EEN-E--MNN",
        ["Ranger"] = "NEEMEM-GEMM----------NNNENME-EENMEENENN",
        ["Hunter"] = "NEEMEM-GEMM-NNNNNNN--NNNENME-EENMEENENN",
        ["Ranger Lord"] = "NEEGEM-GEMM-EEEEEEE--NNNENME-EENGEENENN",
        ["Bounty Hunter"] = "NEEGEM-GEMM-EEEEEEE--NNNENME-EENGEENENN",
        ["Cleric"] = "N----EMGMEE-----EEE---MMNMEM----E--EMNN",
        ["Priest"] = "N----EMGMEE-----MMM---MMNMEM----E--EMNN",
        ["Priest of the Light"] = "N----EMGMEE-----GGGG--GMNMEM----E--EMNN",
        ["Priest of the Dark"] = "N----EMGMEE-----GGG-G-GMNMEM----E--EMNN",
        ["Druid"] = "N-M--NEGEE--EEEEEEE--EE--ME-----EN-MMNN",
        ["Great Druid"] = "N-M--NEGEE--MMMMMMM--EE--ME-----EN-MMNN",
        ["Arch Druid"] = "N-M--NEGEE--MMMMMMM--EE--GE-----EN-GMNN",
        ["Warlock"] = "N-M--NEGEE--MMMMMMM--EE--GE-----EN-GMNN",
        ["Sorcerer"] = "M-E--N-G-E--EEEE-----MNE-MEN----M--MMNN",
        ["Wizard"] = "M-E--N-G-E--MMMM-----MNE-MEN----M--MMNN",
        ["Arch Mage"] = "M-E--N-G-E--GGGG---G-GNE-MEN----G--MMNN",
        ["Lich"] = "M-E--N-G-E--GGGG----GGNE-MEN----G--MMNN",
    };

    /// <summary>
    /// What this game's four blocks carry, with the shipped table's own three leftovers left out.
    /// </summary>
    /// <remarks>
    /// The manual's own lists ([`docs/research/mm7-manual-outline.md`](../../../docs/research/mm7-manual-outline.md)
    /// §2, printed pp.38–41): weapon Sword, Axe, Staff, Spear, Dagger, Bow, Mace, Unarmed; armour Leather,
    /// Chain, Plate, Shield, Dodging; magic the nine schools; miscellaneous Alchemy, Armsmaster, Body
    /// Building, Disarm Trap, Identify Item, Identify Monster, Learning, Meditation, Merchant, Perception,
    /// Repair Item, Stealing. A row that is not here belongs to no block, which is what
    /// <see cref="SkillBlock.Unused"/> reports.
    /// </remarks>
    private static readonly Dictionary<string, SkillBlock> Blocks = new(StringComparer.Ordinal)
    {
        ["Staff"] = SkillBlock.Weapon,
        ["Sword"] = SkillBlock.Weapon,
        ["Dagger"] = SkillBlock.Weapon,
        ["Axe"] = SkillBlock.Weapon,
        ["Spear"] = SkillBlock.Weapon,
        ["Bow"] = SkillBlock.Weapon,
        ["Mace"] = SkillBlock.Weapon,
        ["Unarmed"] = SkillBlock.Weapon,
        ["Leather"] = SkillBlock.Armour,
        ["Chain"] = SkillBlock.Armour,
        ["Plate"] = SkillBlock.Armour,
        ["Shield"] = SkillBlock.Armour,
        ["Dodging"] = SkillBlock.Armour,
        ["Fire"] = SkillBlock.Magic,
        ["Air"] = SkillBlock.Magic,
        ["Water"] = SkillBlock.Magic,
        ["Earth"] = SkillBlock.Magic,
        ["Spirit"] = SkillBlock.Magic,
        ["Mind"] = SkillBlock.Magic,
        ["Body"] = SkillBlock.Magic,
        ["Light"] = SkillBlock.Magic,
        ["Dark"] = SkillBlock.Magic,
        ["Alchemy"] = SkillBlock.Miscellaneous,
        ["Armsmaster"] = SkillBlock.Miscellaneous,
        ["Bodybuilding"] = SkillBlock.Miscellaneous,
        ["Disarm Traps"] = SkillBlock.Miscellaneous,
        ["Identify Item"] = SkillBlock.Miscellaneous,
        ["Identify Monster"] = SkillBlock.Miscellaneous,
        ["Learning"] = SkillBlock.Miscellaneous,
        ["Meditation"] = SkillBlock.Miscellaneous,
        ["Merchant"] = SkillBlock.Miscellaneous,
        ["Perception"] = SkillBlock.Miscellaneous,
        ["Repair"] = SkillBlock.Miscellaneous,
        ["Stealing"] = SkillBlock.Miscellaneous,
    };

    /// <summary>
    /// What a mastery teacher charges for each rung, in the four bands the donor's own tables fall into.
    /// </summary>
    /// <remarks>
    /// <para>
    /// OpenEnroth <c>src/GUI/UI/NPCTopics.cpp:71-197</c> — <c>expertSkillMasteryCost</c>,
    /// <c>masterSkillMasteryCost</c>, and <c>grandmasterSkillMasteryCost</c> — a fee per skill and rung,
    /// charged flat: the donor's teacher names the price and the party pays it, with no merchant's discount
    /// and no house multiplier on top. The three tables state four fees for the skills this game uses, so the
    /// transcription is four bands rather than thirty-seven rows; every skill's numbers are the donor's,
    /// including the ones that look odd beside their neighbours — Light and Dark are priced with the weapons
    /// at 2000/5000/8000 while the seven other schools are 1000/4000/8000, which is what the donor's arrays
    /// say.
    /// </para>
    /// <para>
    /// The rows this game does not use — Blaster, Diplomacy, Thievery — are priced at zero by the donor,
    /// which is its own way of saying no teacher takes them; they are left out here rather than given a fee,
    /// and no counter offers them in any case.
    /// </para>
    /// </remarks>
    private static readonly int[] WeaponFees = [0, 0, 2000, 5000, 8000];

    /// <summary>The donor's fee band for armour: shield, leather, chain, and plate (1000/3000/7000).</summary>
    private static readonly int[] ArmourFees = [0, 0, 1000, 3000, 7000];

    /// <summary>The donor's fee band for the seven element and self schools (1000/4000/8000).</summary>
    private static readonly int[] SchoolFees = [0, 0, 1000, 4000, 8000];

    /// <summary>The donor's fee band for the trades: identification, repair, and the rest (500/2500/6000).</summary>
    private static readonly int[] TradeFees = [0, 0, 500, 2500, 6000];

    /// <summary>Which fee band each skill the donor prices belongs to.</summary>
    /// <remarks>
    /// The four bands above, keyed by the skill's identity as content declares it. The donor's own skill
    /// names are written as the shipped table's words, exactly as the mastery columns are.
    /// </remarks>
    private static readonly Dictionary<string, int[]> Fees = new(StringComparer.Ordinal)
    {
        ["Staff"] = WeaponFees,
        ["Sword"] = WeaponFees,
        ["Dagger"] = WeaponFees,
        ["Axe"] = WeaponFees,
        ["Spear"] = WeaponFees,
        ["Bow"] = WeaponFees,
        ["Mace"] = WeaponFees,
        ["Light"] = WeaponFees,
        ["Dark"] = WeaponFees,
        ["Merchant"] = WeaponFees,
        ["Dodging"] = WeaponFees,
        ["Unarmed"] = WeaponFees,
        ["Armsmaster"] = WeaponFees,
        ["Learning"] = WeaponFees,
        ["Shield"] = ArmourFees,
        ["Leather"] = ArmourFees,
        ["Chain"] = ArmourFees,
        ["Plate"] = ArmourFees,
        ["Fire"] = SchoolFees,
        ["Air"] = SchoolFees,
        ["Water"] = SchoolFees,
        ["Earth"] = SchoolFees,
        ["Spirit"] = SchoolFees,
        ["Mind"] = SchoolFees,
        ["Body"] = SchoolFees,
        ["Identify Item"] = TradeFees,
        ["Repair"] = TradeFees,
        ["Bodybuilding"] = TradeFees,
        ["Meditation"] = TradeFees,
        ["Perception"] = TradeFees,
        ["Disarm Traps"] = TradeFees,
        ["Identify Monster"] = TradeFees,
        ["Stealing"] = TradeFees,
        ["Alchemy"] = TradeFees,
    };

    /// <summary>
    /// This game's nine class families, each in the donor's own four rows.
    /// </summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/Objects/CharacterEnums.h:52-95</c> (<c>enum class Class</c>): thirty-six
    /// rows, four per family — the base class, its first promotion, and its two second promotions, which are
    /// the light and dark alternatives and both stand at the third rank
    /// (<c>src/Engine/Objects/CharacterEnumFunctions.h:149-152</c>, <c>getClassTier</c>). The names are the
    /// shipped table's own, which are also the names content declares its classes under; the ladder lives
    /// here rather than in content because a pack that declares no classes — a staged counter, a test, an
    /// operator's partial import — must still get the ceilings this game's own table states.
    /// </remarks>
    private static readonly string[][] Families =
    [
        ["Knight", "Cavalier", "Champion", "Black Knight"],
        ["Thief", "Rogue", "Spy", "Assassin"],
        ["Monk", "Initiate", "Master", "Ninja"],
        ["Paladin", "Crusader", "Hero", "Villain"],
        ["Archer", "Warrior Mage", "Master Archer", "Sniper"],
        ["Ranger", "Hunter", "Ranger Lord", "Bounty Hunter"],
        ["Cleric", "Priest", "Priest of the Light", "Priest of the Dark"],
        ["Druid", "Great Druid", "Arch Druid", "Warlock"],
        ["Sorcerer", "Wizard", "Arch Mage", "Lich"],
    ];

    private readonly Dictionary<(string Class, int Rank), string> _tables;
    private readonly MightAndMagic7Promotions? _promotions;

    private MightAndMagic7Skills(
        SkillCatalog catalog,
        Dictionary<(string Class, int Rank), string> tables,
        MightAndMagic7Promotions? promotions)
    {
        Catalog = catalog;
        _tables = tables;
        _promotions = promotions;
    }

    /// <inheritdoc />
    public SkillCatalog Catalog { get; }

    /// <summary>Reads this game's skills over the content the product loaded, or null when it loaded none.</summary>
    /// <param name="catalog">The validated content, or null when no bundle supplied any.</param>
    /// <param name="promotions">
    /// This game's ranks, when they were read: they are what lets a closed school say <em>why</em> it is
    /// closed — the alternative a character took, or the rank above that would open it — which is one fact
    /// about one character and belongs in the sentence that refuses them rather than in a panel's guess.
    /// </param>
    /// <returns>This game's skill policy, or null when there is no content to read it over.</returns>
    internal static MightAndMagic7Skills? Read(ContentCatalog? catalog, MightAndMagic7Promotions? promotions = null)
    {
        if (catalog is null) return null;

        List<SkillDefinition> definitions = [];
        foreach ((_, _, ContentEntry entry) in catalog.Entries(SkillDefinitionKind))
        {
            SkillId skill = new(entry.Id);
            definitions.Add(new SkillDefinition(
                skill,
                Blocks.TryGetValue(skill.Value, out SkillBlock block) ? block : SkillBlock.Unused));
        }

        // The class ladder is this game's own table rather than content's. A member stands on the base
        // class's name until a promotion renames it, so the base class's name answers for every rank of its
        // family; where the two second promotions differ — the light and dark alternatives differ in exactly
        // the Light and Dark schools and in nothing else — the shared row is what both allow, so a character
        // who has not chosen a path is promised only what either path would give. Naming the path is the
        // promotion stone's work, and until it lands the two schools' grand-master rungs are open to nobody.
        Dictionary<(string Class, int Rank), string> tables = [];
        foreach (string[] family in Families)
        {
            string[] rows = new string[3];
            for (int position = 0; position < family.Length; position++)
            {
                int rank = Math.Min(position + 1, 3);
                if (!MasteryRows.TryGetValue(family[position], out string? row)) continue;
                tables[(family[position], rank)] = row;
                rows[rank - 1] = rank == 3 && rows[2] is { Length: > 0 } ? Shared(rows[2], row) : row;
            }

            for (int rank = 1; rank <= 3; rank++)
            {
                if (rows[rank - 1] is { Length: > 0 } row) tables[(family[0], rank)] = row;
            }
        }

        return new MightAndMagic7Skills(new SkillCatalog(definitions), tables, promotions);
    }

    /// <summary>How far one member's class and rank let one skill grow.</summary>
    /// <param name="member">The member whose class and rank the ceiling is read for.</param>
    /// <param name="skill">The skill being asked about.</param>
    /// <returns>The ceiling, or none when the class, the rank, or the row states that the skill is not theirs.</returns>
    public SkillCeiling Ceiling(PartyMember member, SkillId skill)
    {
        ArgumentNullException.ThrowIfNull(member);

        // A skill this game does not use is one nobody may hold: a leftover row of the shipped table is a row
        // nothing offers and no class row grants, so the ceiling is none however generous the class's other
        // skills are.
        if (!Catalog.Declares(skill) || !Catalog.Read(skill).IsUsed) return SkillCeiling.None;
        SkillTier tier = RungOf(member, skill);
        if (tier.IsNone)
        {
            // Closed, and this game can often say by whose doing: the alternative the character took, or the
            // rank above that both alternatives are waiting behind. Where it cannot — a class that may hold
            // no such skill however it is promoted — the ceiling carries no reason and a caller states the
            // class, which is the whole truth there.
            return SkillCeiling.None with { Reason = _promotions?.ClosedReason(member, skill) };
        }

        return new SkillCeiling(LevelCeiling(member.Progression.ClassRank, tier), tier);
    }

    /// <summary>What raising one learned skill by a number of levels costs in skill points.</summary>
    /// <remarks>
    /// The donor's own price: the next level's number, so a skill at three rises to four for four points
    /// (OpenEnroth <c>src/GUI/UI/UIGame.cpp:1046-1050</c>, <c>requiredSkillpoints = skillLevel + 1</c>).
    /// Raising several levels at once costs what each of them is worth rather than one price repeated.
    /// </remarks>
    /// <param name="skill">The entry the raise starts from.</param>
    /// <param name="levels">How many levels the raise adds, which is at least one.</param>
    /// <returns>How many skill points the raise costs.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The levels are below one.</exception>
    public int RaiseCost(SkillEntry skill, int levels)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(levels);
        long points = 0;
        for (int step = 1; step <= levels; step++) points += skill.Level + step;
        return checked((int)points);
    }

    /// <summary>What one rung of a skill's ladder is called, as a person reads it.</summary>
    /// <remarks>
    /// The design's own four names ([`docs/gameplay-design.md`](../../../docs/gameplay-design.md) §3.2:
    /// "basic, expert, master, grand master"), which are the manual's B/E/M/GM and the shipped table's four
    /// effect columns. A rung above the ladder reads as its number rather than as a word this game does not
    /// have.
    /// </remarks>
    /// <param name="tier">The rung to name.</param>
    /// <returns>The word a person reads for that rung.</returns>
    public string TierName(SkillTier tier) => tier.Value switch
    {
        0 => "untrained",
        1 => "basic",
        2 => "expert",
        3 => "master",
        4 => "grand master",
        _ => string.Create(CultureInfo.InvariantCulture, $"rung {tier.Value}"),
    };

    /// <summary>Finds the declared skill an outside word names, or null when content declares no such skill.</summary>
    /// <remarks>
    /// The item table lower-cases the skill its goods belong to ("sword", "leather") while the skill table
    /// spells it as a person reads it ("Sword", "Leather"), so a requirement is resolved by name without
    /// regard to case: a capitalisation is not an identity, and an item refused for one would be a defect a
    /// player could not see.
    /// </remarks>
    /// <param name="word">The word some other table used for a skill.</param>
    /// <returns>The declared skill, or null when nothing declares it.</returns>
    internal SkillId? Resolve(string word)
    {
        if (string.IsNullOrWhiteSpace(word)) return null;
        foreach (SkillDefinition definition in Catalog.Definitions)
        {
            if (string.Equals(definition.Id.Value, word.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return definition.Id;
            }
        }

        return null;
    }

    /// <summary>What the donor's own mastery teacher charges for one rung of one skill.</summary>
    /// <remarks>
    /// Flat, as the donor charges it: the fee does not take a merchant's discount and no counter's
    /// multiplier applies to it, which is why the quote path charges this one directly. A skill the donor
    /// prices at nothing, or one this game does not use, is worth nothing here as well, and no counter
    /// offers such a lesson.
    /// </remarks>
    /// <param name="skill">The skill being taught.</param>
    /// <param name="tier">The rung the lesson reaches.</param>
    /// <returns>The fee, or zero when the donor states none for that skill.</returns>
    internal static int MasteryFee(SkillId skill, int tier)
    {
        if (tier < 0 || !Fees.TryGetValue(skill.Value, out int[]? band) || tier >= band.Length) return 0;
        return band[tier];
    }

    /// <summary>What a mastery lesson reads as: the skill and the rung it leaves the member at.</summary>
    /// <remarks>
    /// The name is composed here rather than by the screen, because the rung's words are this game's: a
    /// panel that wrote "expert" itself would be a second copy of the ladder's names.
    /// </remarks>
    /// <param name="skill">The skill being taught.</param>
    /// <param name="tier">The rung the lesson reaches.</param>
    /// <returns>The lesson's own name.</returns>
    internal string LessonName(SkillId skill, int tier) => $"{skill.Value}, {TierName(new SkillTier(tier))}";

    /// <summary>
    /// Whether a counter may teach one member a skill at a rung, or why it may not.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the mastery teacher's own judgement, and it is the donor's: the class must be able to hold
    /// the rung at all — a rung the class's current rank cannot reach names the promotion that would open
    /// it, which is the donor's own refusal (<c>src/GUI/UI/NPCTopics.cpp:439-462</c>, and its strings
    /// <c>LSTR_YOU_HAVE_TO_BE_PROMOTED_TO_S_TO_LEARN</c> and
    /// <c>LSTR_THIS_SKILL_LEVEL_CAN_NOT_BE_LEARNED_BY</c>) — then the member must already hold the skill,
    /// must hold the rung below, and must have brought the skill to the level the teacher wants.
    /// </para>
    /// <para>
    /// <b>What is not judged here.</b> The donor's master-rung teachers for the Light and Dark schools also
    /// want a quest bit (<c>src/GUI/UI/NPCTopics.cpp:485-491</c>, <c>QBIT_114</c> and <c>QBIT_110</c>); no
    /// owner of quest state exists in this build, so the requirement is recorded rather than invented, and
    /// the rung's class ceiling — which only the light or dark second promotion reaches — is what stands in
    /// its place. A quest stone judges the bit through this same answer when quest state lands.
    /// </para>
    /// </remarks>
    /// <param name="member">The member the lesson would go to.</param>
    /// <param name="skill">The skill the lesson teaches.</param>
    /// <param name="tier">The rung the lesson leaves the member at.</param>
    /// <param name="level">The skill level the lesson reaches, which is what a first lesson grants.</param>
    /// <returns>Why the lesson is refused, or null when it may be taught.</returns>
    internal PartyRefusal? Lesson(PartyMember member, SkillId skill, int tier, int level)
    {
        ArgumentNullException.ThrowIfNull(member);
        if (!Catalog.Declares(skill))
        {
            return new PartyRefusal(
                "service-lesson-unknown-skill",
                $"This game's skill table carries no '{skill}', so no counter can teach it.");
        }

        if (!Catalog.Read(skill).IsUsed)
        {
            return new PartyRefusal(
                "service-lesson-unused-skill",
                $"{skill} is a row the shipped table carries and this game does not use, so nobody may learn it.");
        }

        SkillCeiling ceiling = Ceiling(member, skill);
        if (ceiling.IsNone)
        {
            // A closed skill the game can account for carries its own refusal, so a lesson refused for a path
            // the character chose names that choice rather than the class it left them in.
            return ceiling.Reason ?? new PartyRefusal(
                "service-lesson-class-forbidden",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{member.Profile.Name} is a {member.Profile.Class} of rank {member.Progression.ClassRank}, and this game's table lets that class hold no {skill}."));
        }

        if (tier > ceiling.MaximumTier.Value)
        {
            return new PartyRefusal(
                "service-lesson-needs-promotion",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{ThisRung(tier)} {skill} is beyond what {member.Profile.Name} may reach as a {member.Profile.Class}: {Promotions(member, skill, tier)}"));
        }

        // A first-rung lesson teaches the skill itself: whether the member already holds it is the service
        // mechanism's own question, and the level it grants is the one level a learned skill starts at.
        if (tier <= BasicRung) return null;

        int held = member.Skills.LevelOf(skill);
        SkillTier standing = member.Skills.TierOf(skill);
        if (held < 1)
        {
            // The donor's own answer to a member who skipped the first lesson (src/GUI/UI/NPCTopics.cpp:476,
            // pNPCTopics[131]): you must know the skill before you can become an expert in it.
            return new PartyRefusal(
                "service-lesson-skill-unknown",
                $"{member.Profile.Name} has not learned {skill} at all, so there is no rung of it to raise.");
        }

        if (standing.Value < tier - 1)
        {
            return new PartyRefusal(
                "service-lesson-rung-short",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{ThisRung(tier)} {skill} is taught to somebody who already stands at the {TierName(new SkillTier(tier - 1))} rung, and {member.Profile.Name} stands at {TierName(standing)}."));
        }

        int wanted = tier < TeacherLevels.Length ? TeacherLevels[tier] : 0;
        if (held < wanted)
        {
            return new PartyRefusal(
                "service-lesson-level-short",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"A teacher takes a member to {ThisRung(tier)} {skill} at skill level {wanted}, and {member.Profile.Name} stands at level {held}."));
        }

        // The donor's own three exceptions apply to the master rung and to no other: an expert of the
        // merchant's trade is asked for the level and nothing else.
        if (tier >= MasterRung && MasterGates.TryGetValue(skill.Value, out (AttributeId Attribute, int Score) gate))
        {
            int score = member.Attributes.TryGet(gate.Attribute, out int stated) ? stated : 0;
            if (score < gate.Score)
            {
                return new PartyRefusal(
                    "service-lesson-attribute-short",
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"The master rung of {skill} wants {gate.Attribute} {gate.Score} and {member.Profile.Name} has {score}."));
            }
        }

        if (tier >= GrandMasterRung && GrandMasterGates.TryGetValue(skill.Value, out string? companion))
        {
            int together = member.Skills.LevelOf(new SkillId(companion));
            if (together < CompanionLevel)
            {
                return new PartyRefusal(
                    "service-lesson-companion-short",
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"The grand-master rung of {skill} wants {companion} at level {CompanionLevel}, and {member.Profile.Name} stands at {together}."));
            }
        }

        return null;
    }

    /// <summary>Which classes above the member's rank would let them reach a rung of a skill.</summary>
    /// <remarks>
    /// The donor names the promoted class a member has to become (<c>src/GUI/UI/NPCTopics.cpp:445-461</c>),
    /// and so does this: the sentence a refused member reads says which promotion opens the rung, or that no
    /// rank of their class ever does. A class whose two second promotions both reach the rung reads as
    /// "either", which is the donor's own "you have to be promoted to X or Y".
    /// </remarks>
    private string Promotions(PartyMember member, SkillId skill, int tier)
    {
        string baseClass = BaseOf(member.Profile.Class.Value);
        int column = ColumnOf(skill.Value);
        List<string> openings = [];
        for (int rank = member.Progression.ClassRank + 1; rank <= 3; rank++)
        {
            string row = _tables.TryGetValue((baseClass, rank), out string? ladder) ? ladder : string.Empty;
            if (row.Length == 0 || column < 0 || TierOf(row, column).Value < tier) continue;
            openings.Add($"rank {rank}");
        }

        if (openings.Count == 0)
        {
            return $"no promotion of the {baseClass} ladder reaches it";
        }

        return $"a promotion to {string.Join(" or ", openings)} would";
    }

    /// <summary>The base class a promoted class belongs to, from this game's own ladder.</summary>
    /// <remarks>
    /// A member stands on its base class's name until a promotion renames it, and the tables that are keyed
    /// by class — growth, ceilings, and the spell point formula — state their rows for the base class, so a
    /// promoted member reads its family's own row. A class this game's ladder does not carry is its own base,
    /// which is what a partial import or a test's own class gets.
    /// </remarks>
    /// <param name="characterClass">The class's name, as content or the member states it.</param>
    /// <returns>The base class of its family.</returns>
    internal static string BaseClassOf(string characterClass) => BaseOf(characterClass);

    /// <summary>The base class a promoted class belongs to, from this game's own ladder.</summary>
    private static string BaseOf(string characterClass)
    {
        foreach (string[] family in Families)
        {
            foreach (string name in family)
            {
                if (string.Equals(name, characterClass, StringComparison.Ordinal)) return family[0];
            }
        }

        return characterClass;
    }

    /// <summary>The rung of one skill that one member's class and rank allow, or none.</summary>
    private SkillTier RungOf(PartyMember member, SkillId skill)
    {
        if (!_tables.TryGetValue((member.Profile.Class.Value, member.Progression.ClassRank), out string? row))
        {
            return SkillTier.None;
        }

        int column = ColumnOf(skill.Value);
        return column < 0 ? SkillTier.None : TierOf(row, column);
    }

    /// <summary>How many levels a rung allows at a rank, from the bands above and the donor's own cap.</summary>
    private static int LevelCeiling(int rank, SkillTier tier)
    {
        int band = tier.Value >= 0 && tier.Value < LevelBands.Length ? LevelBands[tier.Value] : 0;
        if (band <= 0) return 0;
        long level = band + ((long)RankLevelStep * Math.Max(0, rank - 1));
        return (int)Math.Min(level, DonorLevelCap);
    }

    /// <summary>The column a skill occupies in the donor's own array, or -1 when it has none.</summary>
    private static int ColumnOf(string skill)
    {
        for (int column = 0; column < Columns.Length; column++)
        {
            if (string.Equals(Columns[column], skill, StringComparison.Ordinal)) return column;
        }

        return -1;
    }

    /// <summary>The rung a transcribed row states for one column.</summary>
    private static SkillTier TierOf(string row, int column) =>
        column < 0 || column >= row.Length
            ? SkillTier.None
            : row[column] switch
            {
                'N' => new SkillTier(BasicRung),
                'E' => new SkillTier(ExpertRung),
                'M' => new SkillTier(MasterRung),
                'G' => new SkillTier(GrandMasterRung),
                _ => SkillTier.None,
            };

    /// <summary>The lower of two transcribed rows, rung by rung.</summary>
    /// <remarks>
    /// Two second promotions of one family differ in the Light and Dark schools and in nothing else, so the
    /// shared row is the one a character who has not chosen a path may be promised: what both paths allow.
    /// </remarks>
    private static string Shared(string left, string right)
    {
        if (left.Length != right.Length) return left;
        char[] shared = new char[left.Length];
        for (int column = 0; column < left.Length; column++)
        {
            shared[column] = left[column] <= right[column] ? left[column] : right[column];
        }

        return new string(shared);
    }

    /// <summary>How a rung reads inside a sentence: "the expert rung of", or "the first rung of".</summary>
    private string ThisRung(int tier) => tier <= BasicRung ? "the first rung of" : $"the {TierName(new SkillTier(tier))} rung of";
}
