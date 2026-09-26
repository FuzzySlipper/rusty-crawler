using System.Globalization;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Promotion;
using PartyRpg.Kit.Skills;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>Which school one alternative of a second promotion takes, and which it leaves to the other.</summary>
/// <remarks>
/// <para>
/// <b>Ours, over the donor's mastery table.</b> The shipped class table carries thirty-six rank rows and no
/// flag saying which alternative is which; what it <em>does</em> carry, in the donor's transcription of the
/// executable, is the per-class mastery of every skill
/// (<c>OpenEnroth/src/Engine/mm7_data.cpp:763</c>, <c>skillMaxMasteryPerClass</c>). Four families' pairs
/// differ in exactly the two magic schools and in nothing else — Paladin's Hero and Villain, Archer's Master
/// Archer and Sniper, Cleric's Priest of the Light and Priest of the Dark, and Sorcerer's Arch Mage and Lich
/// — which is precisely the four the manual names when it describes the split
/// ([`docs/research/mm7-manual-outline.md`](../../../docs/research/mm7-manual-outline.md) §1 and §8,
/// printed pp.14–16 and p.36). The other five families' pairs differ elsewhere or nowhere, so their paths
/// take no school at all and this records that rather than claiming a magic split they do not have.
/// </para>
/// <para>
/// The words <c>light</c> and <c>dark</c> are the manual's own description of the choice ("the second
/// promotion offers two alternative ranks per class … the pairs differ in Light versus Dark Magic access"),
/// and the two the donor's grand-master gate names when it asks whether a caster is one of the two light
/// classes or one of the two dark ones (<c>src/GUI/UI/NPCTopics.cpp:512-519</c>, <c>CLASS_ARCHAMGE</c>,
/// <c>CLASS_PRIEST_OF_SUN</c>, <c>CLASS_LICH</c>, <c>CLASS_PRIEST_OF_MOON</c>).
/// </para>
/// </remarks>
/// <param name="Choice">Which alternative it is: <c>light</c> or <c>dark</c>.</param>
/// <param name="Opens">The school this alternative takes, or null when it takes none.</param>
/// <param name="Closes">
/// The school it leaves to the other alternative, or null when the pair splits on no school. It is what a
/// refusal names when a caster asks for the magic their own choice put out of reach.
/// </param>
internal readonly record struct PromotionPath(string Choice, SkillId? Opens, SkillId? Closes);

/// <summary>
/// This game's ranks: every class's first promotion, and the two alternatives its second promotion splits
/// into, with who gives each and what each asks for.
/// </summary>
/// <remarks>
/// <para>
/// <b>The ladder's shape is the shipped class table's; the ranks' terms are ours.</b> The nine families and
/// their thirty-six rank rows are the operator's own <c>CLASS.TXT</c>, four rows per family in the order
/// base, first promotion, light second promotion, dark second promotion
/// ([`docs/research/mm7-data-inventory.md`](../../../docs/research/mm7-data-inventory.md), <i>Classes</i>;
/// the class table is emitted as content and this reads its names from there). What the shipped data does
/// not carry is a promotion table: no row anywhere states that a rank is asked for, by whom, or on what
/// terms — the design records that the promotion quests are ours
/// ([`docs/gameplay-design.md`](../../../docs/gameplay-design.md) §3.8), and the tables below are that
/// statement, written out and cited row by row rather than presented as extracted facts.
/// </para>
/// <para>
/// <b>Who gives each rank is shipped, and the shipped data says it twice.</b> The NPC table's own notes
/// column names every promoter by family and alignment — <c>npc.txt</c> row 46 is "Good Cleric promoter",
/// row 47 "Evil Cleric promoter", row 40 "Good Archer promoter", row 41 "Evil Archer promoter", and so on
/// through rows 15–51 — and the topic table names them again by the rank they give: <c>npctopic</c> row 92
/// "Wizard" and row 94 "Archmage" belong to Thomas Grey, rows 96–97 "Lich" to Halfgild Wynac, rows 88–89
/// "Priest" and 90–91 "Priest of Dark" to Daedalus Falk, rows 86–87 "Priest of Light" to Rebecca Devine,
/// and the rest the same way. The identities below are the NPC table's own row numbers, which are what
/// content carries its people under (<c>person:npc-48</c> is Thomas Grey), and the names beside them are
/// the table's.
/// </para>
/// <para>
/// <b>What each rank asks for is shipped too, and it is a quest.</b> The quest table states every
/// promotion's errand and the promoter it is turned in to: bit 18 "Go to Lord Markham's estate in Tatalia,
/// steal the vase there, and return it to William Lasker in the Erathian Sewers" (Thief → Rogue), bit 30
/// "Retrieve the Perfect Bow from the Titans' Stronghold in Avlee and return it to Lawrence Mark in
/// Harmondale" (Warrior Mage → Master Archer), bit 45 "Collect the six golem pieces and construct a
/// complete golem, then return to Thomas Grey in the School of Sorcery" (Sorcerer → Wizard), bit 48
/// "Retrieve the lich jars from the Proving Grounds in Celeste and bring them back to Halfgild Wynac in the
/// Pit" (Wizard → Lich), and one row per promotion in between. Errands carry no quest state in this build,
/// so a rank whose errand is a deed states it as a <see cref="PromotionRequirementKind.Quest"/> requirement
/// naming the shipped bit, and that requirement is refused by name until the owner of quests judges it. Where
/// the errand's own words name something the party brings back, and the shipped item table carries it, the
/// rank asks for that item instead — holding it is state this build really has — and the quest bit it is the
/// turn-in of is named in the row's own comment. Two errands are counts the original keeps as its own awards
/// rather than as quests — five arena wins and ten thousand gold of bounties
/// (<c>OpenEnroth/src/Engine/Data/AwardEnums.h:88-91</c>, <c>AWARD_ARENA_*_WINS</c>; <c>:86</c>,
/// <c>AWARD_BOUNTIES_COLLECTED</c>, and <c>src/Engine/Evt/EvtEnums.h:67</c>,
/// <c>EVENT_IsTotalBountyHuntingAwardInRange</c>) — so those two rank requirements are stated as records
/// with a magnitude, which is what a party carries.
/// </para>
/// <para>
/// <b>Every rank also leaves a record.</b> The original marks an earned promotion with an award bit
/// (<c>src/Engine/Data/AwardEnums.h:10-79</c>, <c>AWARD_PROMOTION_ROGUE</c> through
/// <c>AWARD_PROMOTION_LICH</c>) and reads those bits back when it asks whether somebody is of a class
/// (<c>src/Engine/Objects/Character.cpp:6591-6617</c>, <c>Character::isClass</c>). This game keeps the same
/// fact as a party-carried record under its own name — <c>promotion:&lt;rank&gt;</c> — written where the
/// rank is granted, so what the party has become is readable state rather than something a later rank would
/// have to infer.
/// </para>
/// <para>
/// <b>Who is a giver is judged, not assumed.</b> Each rank states its giver as one of its requirements, so a
/// rank offered by somebody the ladder does not name is refused by name instead of being quietly granted:
/// the conversation offers a person only the ranks they give, and the owner judges the requirement again
/// when the rank is actually taken.
/// </para>
/// </remarks>
internal sealed class MightAndMagic7Promotions : IPromotionRule
{
    /// <summary>The word this game's ladder uses for the light alternative of a second promotion.</summary>
    internal const string LightChoice = "light";

    /// <summary>The word this game's ladder uses for the dark alternative of a second promotion.</summary>
    internal const string DarkChoice = "dark";

    /// <summary>The prefix a promotion's own record carries on the party.</summary>
    internal const string AwardPrefix = "promotion:";

    /// <summary>The school the light alternatives of the four magic-splitting families take.</summary>
    internal static readonly SkillId LightSchool = new("Light");

    /// <summary>The school the dark alternatives of the four magic-splitting families take.</summary>
    internal static readonly SkillId DarkSchool = new("Dark");

    private readonly Dictionary<string, PromotionPath> _paths;
    private readonly IReadOnlyList<string> _notes;

    private MightAndMagic7Promotions(PromotionLadder ladder, Dictionary<string, PromotionPath> paths, IReadOnlyList<string> notes)
    {
        Ladder = ladder;
        _paths = paths;
        _notes = notes;
    }

    /// <inheritdoc />
    public PromotionLadder Ladder { get; }

    /// <summary>Which school each alternative of a second promotion takes, by the class it belongs to.</summary>
    internal IReadOnlyDictionary<string, PromotionPath> Paths => _paths;

    /// <summary>What reading this game's ladder noticed, for a report.</summary>
    internal IReadOnlyList<string> Notes => _notes;

    /// <summary>How many ranks the ladder states: every class's first promotion and both of its seconds.</summary>
    internal int RankCount => Ladder.Ranks.Count;

    /// <summary>How many people the ladder names as givers of a rank.</summary>
    internal int GiverCount => Ladder.Ranks.Select(rank => rank.Giver).Distinct(StringComparer.Ordinal).Count();

    /// <summary>How many ranks ask for an errand no owner in this build judges.</summary>
    internal int QuestRequirementCount =>
        Ladder.Ranks.Sum(rank => rank.Requirements.Count(requirement => requirement.Kind == PromotionRequirementKind.Quest));

    /// <summary>How many ranks ask for something the party carries, and could be given today.</summary>
    internal int ItemRequirementCount =>
        Ladder.Ranks.Sum(rank => rank.Requirements.Count(requirement => requirement.Kind == PromotionRequirementKind.Item));

    /// <summary>Reads this game's ranks over the content the product loaded.</summary>
    /// <remarks>
    /// The class names the ladder is written in are the shipped class table's own, and content carries them,
    /// so this reports how many of them content declares rather than refusing to state a ladder a partial
    /// import would not describe: a pack that declares no classes still gets the ceilings and the ranks this
    /// game's table states, exactly as it gets the mastery rows.
    /// </remarks>
    /// <param name="catalog">The validated content, or null when no bundle supplied any.</param>
    /// <returns>This game's ranks.</returns>
    internal static MightAndMagic7Promotions Read(ContentCatalog? catalog)
    {
        PromotionRank[] ranks = Ranks();
        Dictionary<string, PromotionPath> paths = PathTable();
        List<string> notes = [];
        HashSet<string> declared = catalog is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : [.. catalog.Entries(MightAndMagic7Skills.ClassDefinitionKind).Select(entry => entry.Entry.Id)];
        HashSet<string> stated = [.. ranks.SelectMany(rank => new[] { rank.From.Value, rank.To.Value })];

        notes.Add(string.Create(
            CultureInfo.InvariantCulture,
            $"{ranks.Length} ranks are stated over {ranks.Select(rank => rank.Giver).Distinct(StringComparer.Ordinal).Count()} people who give them; {ranks.Count(rank => rank.Choice.Length > 0)} of them are the second promotion's alternatives."));
        notes.Add(string.Create(
            CultureInfo.InvariantCulture,
            $"{paths.Count} classes take a magic path: the pairs of four families split on exactly the two schools this game's mastery table gives them."));
        if (declared.Count > 0)
        {
            int missing = stated.Count(name => !declared.Contains(name));
            notes.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"Content declares {declared.Count} class rows; {stated.Count - missing} of the {stated.Count} classes this ladder names are among them{(missing > 0 ? $", and {missing} are not" : string.Empty)}."));
        }

        return new MightAndMagic7Promotions(new PromotionLadder(ranks), paths, notes);
    }

    /// <summary>
    /// Why one member's own chosen path closes one skill, or null when this game has nothing to add.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two states are worth naming, and they are different facts. A member who has <em>taken</em> an
    /// alternative is told which one: the light path of the Master Archer takes Light and leaves Dark to the
    /// other alternative, so a Master Archer may hold no Dark. A member who stands at the rank the two
    /// alternatives split from but has taken neither is holding the intersection — both alternatives close
    /// that school, so the rank above is what opens it — and is told that instead.
    /// </para>
    /// <para>
    /// A class whose pair splits on no school answers nothing here, and the ceiling falls back to the game's
    /// own general refusal: a Champion may hold no Dark, and its own table is the reason rather than a choice
    /// it made.
    /// </para>
    /// </remarks>
    /// <param name="member">The member whose class, rank, and path are read.</param>
    /// <param name="skill">The skill that is closed to them.</param>
    /// <returns>The refusal that names the choice, or null when the closing is not a path's doing.</returns>
    internal PartyRefusal? ClosedReason(PartyMember member, SkillId skill)
    {
        ArgumentNullException.ThrowIfNull(member);
        string closed = skill.Value;
        if (_paths.TryGetValue(member.Profile.Class.Value, out PromotionPath taken))
        {
            if (taken.Closes is not { } left || !string.Equals(left.Value, closed, StringComparison.Ordinal)) return null;
            string opens = taken.Opens is { } opened ? opened.Value : "nothing";
            return new PartyRefusal(
                "skill-closed-by-path",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{member.Profile.Name} took the {taken.Choice} path of the {member.Profile.Class}: it takes {opens} and leaves {closed} to the other alternative, so a {member.Profile.Class} may hold no {closed}."));
        }

        // Neither alternative taken yet. The ceiling here is what both alternatives allow, so a school only
        // one of them takes is not the member's yet — and the sentence names the alternative that would open
        // it, which is the whole of what a player has to do about it. A skill neither alternative takes is
        // the class's own limit rather than a choice nobody has made, and says so through the game's general
        // refusal instead.
        List<PromotionRank> split = [.. Ladder.From(member.Profile.Class).Where(rank => rank.Choice.Length > 0)];
        if (split.Count < 2) return null;
        List<string> openings = [];
        foreach (PromotionRank rank in split)
        {
            if (!_paths.TryGetValue(rank.To.Value, out PromotionPath path)) return null;
            if (path.Opens is { } opened && string.Equals(opened.Value, closed, StringComparison.Ordinal))
            {
                openings.Add($"{rank.To} ({path.Choice})");
            }
        }

        if (openings.Count == 0) return null;
        return new PartyRefusal(
            "skill-closed-by-unchosen-path",
            string.Create(
                CultureInfo.InvariantCulture,
                $"{member.Profile.Name} is a {member.Profile.Class} of rank {member.Progression.ClassRank} and has taken neither alternative of the rank above: {string.Join(" or ", openings)} would take {closed}, and until one of them is taken a {member.Profile.Class} may hold none of it."));
    }

    /// <summary>One rank row, with the giver, the errand, and the record it leaves stated together.</summary>
    /// <remarks>
    /// <para>
    /// <b>A rank asks for what this build can judge, and states its errand either way.</b> Where the shipped
    /// errand's own words name something the party brings back — "steal the vase and return it", "bring the
    /// lich jars" — the rank asks for that item, because holding it is state a party really has, and the
    /// shipped quest bit it is the turn-in of is named in the row's own comment. Where the errand is a deed
    /// with a turn-in — "kill Wromthrax", "move the weight in Watchtower 6" — the rank states it as a quest
    /// requirement naming the shipped quest table's bit, which no owner in this build judges: it is refused
    /// by name and travels to the owner of quests. Two errands are counts the original keeps as its own
    /// awards rather than as quests, and those are stated as records with a magnitude, which a party carries.
    /// </para>
    /// <para>
    /// The giver's own words are this game's, written in the voice the shipped topic texts use; where the
    /// shipped table carries a line for that rank, the row's own comment says so.
    /// </para>
    /// </remarks>
    private static PromotionRank Rank(
        string id,
        string from,
        string to,
        int rank,
        string giver,
        string giverName,
        int quest,
        string errand,
        string words,
        string? choice = null,
        (string Item, int Count, string Label)[]? items = null,
        (string Award, int Amount, string Label)? award = null)
    {
        List<PromotionRequirement> requirements = [PromotionRequirement.FromGiver(giver, giverName)];
        bool proofInHand = award is not null || items is { Length: > 0 };
        if (!proofInHand)
        {
            // A deed with a turn-in: the errand is the requirement, stated by the shipped quest table's own
            // bit, and the owner that will judge it does not exist yet.
            requirements.Add(PromotionRequirement.ForQuest(quest.ToString(CultureInfo.InvariantCulture), errand));
        }

        foreach ((string item, int count, string label) in items ?? [])
        {
            requirements.Add(PromotionRequirement.ForItem(item, count, label));
        }

        if (award is { } record)
        {
            requirements.Add(PromotionRequirement.ForAward(record.Award, record.Amount, record.Label));
        }

        return new PromotionRank(
            id,
            new ClassId(from),
            new ClassId(to),
            rank,
            requirements,
            choice ?? string.Empty,
            // Every rank leaves the record the original keeps as an award bit, under this game's own name,
            // so a later rank, a person, or a quest's turn-in can ask what the party has become.
            $"{AwardPrefix}{id}",
            words);
    }

    /// <summary>Every rank this game states, in the shipped class table's own family order.</summary>
    private static PromotionRank[] Ranks() =>
    [
        // Knight → Cavalier → Champion (light) / Black Knight (dark). npc.txt row 43 "Evil Knight promoter"
        // gives the first promotion and the dark one, row 42 "Good Knight promoter" the light one; npctopic
        // rows 73-74 name Cavalier, 75-76 Black Knight, 71-72 Champion.
        Rank("knight-cavalier", "Knight", "Cavalier", 2, "npc-43", "Frederick Org", 35,
            "raid the Elven Treasury at Castle Navan", "Raid the Elven Treasury at Castle Navan and I will name you Cavalier."),
        Rank("cavalier-champion", "Cavalier", "Champion", 3, "npc-42", "Leda Rowan", 33,
            "win five arena challenges", "Win five challenges in the arena and the rank of Champion is yours.",
            LightChoice, award: ("award:arena-wins", 5, "arena victories")),
        Rank("cavalier-black-knight", "Cavalier", "Black Knight", 3, "npc-43", "Frederick Org", 34,
            "destroy the undead in the Haunted House", "Clear the undead out of the Haunted House in the Barrow Downs and you will be a Black Knight.",
            DarkChoice),

        // Thief → Rogue → Spy (light) / Assassin (dark). npc.txt row 15 "Good Thief promoter", row 16 "Evil
        // Thief promoter"; npctopic 44-45 Rogue, 46-47 Spy and 49-50 Assassin, whose own lines are the
        // shipped ones: "Bring me that lovely vase I saw on the mantle in Lord Markham's manor, and I shall
        // call you Rogue", and "return with a trinket of hers to prove the job is done".
        Rank("thief-rogue", "Thief", "Rogue", 2, "npc-15", "William Lasker", 18,
            "steal the vase from Lord Markham's estate", "Bring me that lovely vase I saw on the mantle in Lord Markham's manor, and I shall call you Rogue.",
            items: [("624", 1, "Vase")]),
        Rank("rogue-spy", "Rogue", "Spy", 3, "npc-15", "William Lasker", 19,
            "move the counterweight in Watchtower 6", "Move the weight from the top of Watchtower 6 to the bottom, and you will have proven yourself a Spy.",
            LightChoice),
        Rank("rogue-assassin", "Rogue", "Assassin", 3, "npc-16", "Seknit Undershadow", 21,
            "silence Lady Eleanor Carmine and bring proof", "Silence Lady Carmine and return with a trinket of hers to prove the job is done.",
            DarkChoice, items: [("620", 1, "Big Tapestry")]),

        // Monk → Initiate → Master (light) / Ninja (dark). npc.txt row 38 "Good Monk promoter", row 39 "Evil
        // Monk promoter"; npctopic 58-60 Initiate, 61-62 Master, 63-64 Ninja.
        Rank("monk-initiate", "Monk", "Initiate", 2, "npc-38", "Bartholomew Hume", 27,
            "find the lost meditation spot in the Dwarven Barrows", "Reach the barrow that was built on a site of great natural power and meditate by the water, and your promotion to Initiate is complete."),
        Rank("initiate-master", "Initiate", "Master", 3, "npc-38", "Bartholomew Hume", 28,
            "defeat the High Priest of Baa", "Extinguish the remnants of the Order of Baa and I shall complete your training.",
            LightChoice),
        Rank("initiate-ninja", "Initiate", "Ninja", 3, "npc-39", "Stephan Sand", 29,
            "crack the code and enter the Tomb of Ashwar Nog'Nogoth", "Crack the code in the School of Sorcery, enter the tomb it names, and return to me.",
            DarkChoice),

        // Paladin → Crusader → Hero (light) / Villain (dark). npc.txt row 17 "Good Paladin promoter", row 18
        // "Evil Paladin promoter"; npctopic 51-52 Crusader, 53-54 Hero, 56-57 Villain, whose lines are the
        // shipped ones: "A dragon must be slain … Wromthrax the Heartless", "Alice Hargreaves … rescue
        // sweet Alice", "Capture this woman from her residence in Castle Gryphonheart".
        Rank("paladin-crusader", "Paladin", "Crusader", 2, "npc-17", "Sir Charles Quixote", 22,
            "kill Wromthrax the Heartless in his cave in Tatalia", "The test is simple. A dragon must be slain: Wromthrax the Heartless, in his cave in Tatalia."),
        Rank("crusader-hero", "Crusader", "Hero", 3, "npc-17", "Sir Charles Quixote", 24,
            "rescue Alice Hargreaves from William's Tower", "A wicked villain has kidnapped a fair maiden by the name of Alice Hargreaves. Rescue sweet Alice and you will truly be Heroes of the Land.",
            LightChoice),
        Rank("crusader-villain", "Crusader", "Villain", 3, "npc-18", "William Setag", 26,
            "capture Alice Hargreaves and imprison her in William's Tower", "Capture the noble Alice Hargreaves from Castle Gryphonheart, bring her to my tower, and I shall promote the Crusaders among you to Villains.",
            DarkChoice),

        // Archer → Warrior Mage → Master Archer (light) / Sniper (dark). npc.txt row 41 "Evil Archer
        // promoter" gives the first promotion and the dark one, row 40 "Good Archer promoter" the light one;
        // npctopic 67-68 Warrior Mage, 65-66 Master Archer ("You found the bow!"), 69-70 Sniper.
        Rank("archer-warrior-mage", "Archer", "Warrior Mage", 2, "npc-41", "Steagal Snick", 31,
            "sabotage the lift in the Red Dwarf Mines", "Sabotage the lift in the Red Dwarf Mines and return to me in Avlee, and you will be Warrior Mages."),
        Rank("warrior-mage-master-archer", "Warrior Mage", "Master Archer", 3, "npc-40", "Lawrence Mark", 30,
            "retrieve the Perfect Bow from the Titans' Stronghold", "You found the bow! Let me take some measurements and adjust it to your style, and I will promote the Warrior Mages among you to Master Archers.",
            LightChoice, items: [("542", 1, "The Perfect Bow")]),
        Rank("warrior-mage-sniper", "Warrior Mage", "Sniper", 3, "npc-41", "Steagal Snick", 32,
            "retrieve the Perfect Bow from the Titans' Stronghold", "Bring me the Perfect Bow from the Titans' Stronghold in Avlee, and you will be Snipers.",
            DarkChoice, items: [("542", 1, "The Perfect Bow")]),

        // Ranger → Hunter → Ranger Lord (light) / Bounty Hunter (dark). npc.txt row 45 "Evil Ranger
        // promoter", row 44 "Good Ranger promoter"; npctopic 79-80 Hunter, 77-78 Ranger Lord, 81-82 Bounty
        // Hunter. The dark errand is the bounty hunt the original keeps as an award count rather than as a
        // quest (AWARD_BOUNTIES_COLLECTED), so it is stated as a record with a magnitude.
        Rank("ranger-hunter", "Ranger", "Hunter", 2, "npc-45", "Ebednezer Sower", 37,
            "solve the secret of the Faerie Mound and speak to the Faerie King", "Solve the secret of the entrance to the Faerie Mound in Avlee, speak to the Faerie King, and return to me."),
        Rank("hunter-ranger-lord", "Hunter", "Ranger Lord", 3, "npc-44", "Lysander Sweet", 36,
            "calm the trees of the Tularean Forest by speaking to the Oldest Tree", "Calm the trees in the Tularean Forest by speaking to the Oldest Tree, and return to me.",
            LightChoice),
        Rank("hunter-bounty-hunter", "Hunter", "Bounty Hunter", 3, "npc-45", "Ebednezer Sower", 38,
            "collect ten thousand gold worth of bounties", "Collect ten thousand gold worth of bounties from the town halls, and I will make Bounty Hunters of you.",
            DarkChoice, award: ("award:bounties", 10000, "gold of bounties")),

        // Cleric → Priest → Priest of the Light (light) / Priest of the Dark (dark). npc.txt row 46 "Good
        // Cleric promoter", row 47 "Evil Cleric promoter"; npctopic 88-89 Priest and 90-91 "Priest of Dark"
        // belong to Daedalus Falk, 86-87 "Priest of Light" to Rebecca Devine. The topic table spells the two
        // alternatives without the article the class table uses — "Priest of Light" against "Priest of the
        // Light" — and the class table's own spelling is what the rank names.
        Rank("cleric-priest", "Cleric", "Priest", 2, "npc-47", "Daedalus Falk", 43,
            "find the lost pirate map in the Tidewater Caverns", "Find the lost pirate map in the Tidewater Caverns and return to me in the Deyja Moors."),
        Rank("priest-priest-of-the-light", "Priest", "Priest of the Light", 3, "npc-46", "Rebecca Devine", 42,
            "purify the Altar of Evil in the Temple of the Moon", "Purify the Altar of Evil in the Temple of the Moon on Evenmorn Isle, and the light will make you a Priest of the Light.",
            LightChoice),
        Rank("priest-priest-of-the-dark", "Priest", "Priest of the Dark", 3, "npc-47", "Daedalus Falk", 44,
            "deface the Altar of Good in the Temple of the Sun", "Deface the Altar of Good in the Temple of the Sun on Evenmorn Isle, and the dark will make you a Priest of the Dark.",
            DarkChoice),

        // Druid → Great Druid → Arch Druid (light) / Warlock (dark). npc.txt row 50 "Good Druid promoter",
        // row 51 "Evil Druid promoter"; npctopic 98-99 Great Druid, 100-101 Arch Druid, 102-103 Warlock.
        Rank("druid-great-druid", "Druid", "Great Druid", 2, "npc-50", "Anthony Green", 49,
            "visit the three stonehenge monoliths", "Visit the three stonehenge monoliths in Tatalia, the Evenmorn Islands, and Avlee, and return to me in the Tularean Forest."),
        Rank("great-druid-arch-druid", "Great Druid", "Arch Druid", 3, "npc-50", "Anthony Green", 54,
            "lay the Dwarf King's bones to rest in the Barrow Downs", "Retrieve the bones of the Dwarf King and place them in their proper resting place in the Barrow Downs.",
            LightChoice),
        Rank("great-druid-warlock", "Great Druid", "Warlock", 3, "npc-51", "Tor Anwyn", 55,
            "retrieve the dragon egg from the Dragon Cave", "Retrieve the dragon egg from the Dragon Cave in the Land of the Giants and return it to me in Mount Nighon.",
            DarkChoice, items: [("647", 1, "Dragon Egg")]),

        // Sorcerer → Wizard → Arch Mage (light) / Lich (dark). npc.txt row 48 "Good Sorcerer promoter", row
        // 49 "Evil Sorcerer promoter"; npctopic 92-93 Wizard and 94-95 Archmage belong to Thomas Grey, 96-97
        // Lich to Halfgild Wynac. The two errands whose words name what the party brings back are the two
        // this game can judge today: the six golem parts the shipped item table carries (639 chest, 641 head,
        // 642-643 legs, 644-645 arms — the row 640 "Abbey Normal Golem Head" belongs to the Abbey's own
        // quest), and the lich jars (601 "Lich Jar", 602 "Case of Soul Jars"). The light errand's Book of
        // Divine Intervention is the Light school's own book in the item table (487).
        Rank("sorcerer-wizard", "Sorcerer", "Wizard", 2, "npc-48", "Thomas Grey", 45,
            "collect the six golem pieces and construct a complete golem",
            "Collect the six golem pieces and construct a complete golem, then return to me, and you will be Wizards.",
            items:
            [
                ("639", 1, "Golem chest"),
                ("641", 1, "Golem head"),
                ("642", 1, "Golem left leg"),
                ("643", 1, "Golem right leg"),
                ("644", 1, "Golem right arm"),
                ("645", 1, "Golem left arm"),
            ]),
        Rank("wizard-arch-mage", "Wizard", "Arch Mage", 3, "npc-48", "Thomas Grey", 47,
            "find the Book of Divine Intervention in the Breeding Zone", "Find the Book of Divine Intervention in the Breeding Zone in the Pit and return it to me, and you will be Arch Mages.",
            LightChoice, items: [("487", 1, "Divine Intervention")]),
        Rank("wizard-lich", "Wizard", "Lich", 3, "npc-49", "Halfgild Wynac", 48,
            "retrieve the lich jars from the Proving Grounds", "Retrieve the lich jars from the Proving Grounds in Celeste and bring them back to me in the Pit, and you will be Liches.",
            DarkChoice, items: [("601", 1, "Lich Jar"), ("602", 1, "Case of Soul Jars")]),
    ];

    /// <summary>Which school each alternative of the four magic-splitting families takes.</summary>
    private static Dictionary<string, PromotionPath> PathTable() => new(StringComparer.Ordinal)
    {
        // The light alternatives take Light and leave Dark; the dark ones the other way round. The mastery
        // table says the same thing from its side: Hero, Master Archer, Priest of the Light, and Arch Mage
        // each reach grand master or basic in Light and nothing in Dark, and their opposites the reverse.
        ["Hero"] = new PromotionPath(LightChoice, LightSchool, DarkSchool),
        ["Villain"] = new PromotionPath(DarkChoice, DarkSchool, LightSchool),
        ["Master Archer"] = new PromotionPath(LightChoice, LightSchool, DarkSchool),
        ["Sniper"] = new PromotionPath(DarkChoice, DarkSchool, LightSchool),
        ["Priest of the Light"] = new PromotionPath(LightChoice, LightSchool, DarkSchool),
        ["Priest of the Dark"] = new PromotionPath(DarkChoice, DarkSchool, LightSchool),
        ["Arch Mage"] = new PromotionPath(LightChoice, LightSchool, DarkSchool),
        ["Lich"] = new PromotionPath(DarkChoice, DarkSchool, LightSchool),
    };
}
