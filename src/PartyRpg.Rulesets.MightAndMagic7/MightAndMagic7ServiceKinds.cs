using PartyRpg.Kit.Services;
using PartyRpg.Kit.Time;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// What each kind of building in the shipped table does, as this game's answer.
/// </summary>
/// <remarks>
/// <para>
/// <b>The table names a kind; this says what it is.</b> The building table carries twenty-one kinds of
/// service and nothing about what any of them offers — which operations a counter has, what it stocks,
/// what it teaches, what it charges — because those are the game's rules rather than the operator's data.
/// They live here, one answer per kind, and every one of them is cited to the donor file it was read from,
/// so a kind that behaves differently from the original is a difference a reader can see rather than a
/// guess.
/// </para>
/// <para>
/// <b>Nothing here is a class.</b> A kind is a word, and this is a lookup from the word to the answers the
/// one service mechanism needs. A kind the table does not carry, or a kind a ruleset adds, gets the
/// fallback: a counter that sells what its stock says, teaches what its lessons say, and offers nothing
/// else — which is what an authored pack with no policy of its own gets.
/// </para>
/// </remarks>
internal static class MightAndMagic7ServiceKinds
{
    /// <summary>The kind a coach stands under.</summary>
    internal const string Stables = "Stables";

    /// <summary>The kind a boat stands under.</summary>
    internal const string Boats = "Boats";

    /// <summary>The kind a tavern stands under.</summary>
    internal const string Tavern = "Tavern";

    /// <summary>The kind a temple stands under.</summary>
    internal const string Temple = "Temple";

    /// <summary>The kind a training hall stands under.</summary>
    internal const string Training = "Training";

    /// <summary>The kind a bank stands under.</summary>
    internal const string Bank = "Bank";

    /// <summary>The kind a town hall stands under.</summary>
    internal const string TownHall = "Town Hall";

    /// <summary>The kind a weapon shop stands under.</summary>
    internal const string WeaponShop = "Weapon Shop";

    /// <summary>The kind an armor shop stands under.</summary>
    internal const string ArmorShop = "Armor Shop";

    /// <summary>The kind a magic shop stands under.</summary>
    internal const string MagicShop = "Magic Shop";

    /// <summary>The kind an alchemist stands under.</summary>
    internal const string Alchemist = "Alchemist";

    /// <summary>
    /// The base price of a lesson, before the counter's multiplier: the donor's own five hundred
    /// (OpenEnroth <c>src/Engine/PriceCalculator.cpp:150-160</c>, <c>skillLearningCostForPlayer</c>).
    /// </summary>
    internal const int LessonBasePrice = 500;

    /// <summary>
    /// How many lines a shop's shelves hold, which is the donor's own twelve-slot shop floor shown six to
    /// a row (OpenEnroth <c>src/GUI/UI/Houses/Shops.cpp:626-660</c>, <c>itemAmountInShop</c>).
    /// </summary>
    internal const int ShopStockLines = 8;

    /// <summary>What a coach seat costs before the stable's own multiplier, as the donor prices it.</summary>
    internal const int CoachFare = 25;

    /// <summary>What a berth costs before the dock's own multiplier; a boat is twice a coach.</summary>
    internal const int BoatFare = 50;

    /// <summary>The material words the item table uses for the rarities no ordinary shop stocks.</summary>
    /// <remarks>
    /// The table itself spells these out rather than numbering them: an artifact, a relic, and a special
    /// item each carry their word in the material column. A shop that sold them would be selling what the
    /// original reserves for treasure, so they are excluded from every ordinary shelf by the table's own
    /// word rather than by a value threshold this ruleset invented.
    /// </remarks>
    internal static readonly IReadOnlyList<string> RareMaterials = ["Artifact", "Relic", "Special"];

    /// <summary>Which operations a kind's counter offers.</summary>
    /// <remarks>
    /// The donor is the source of each set: a shop sells, identifies, and repairs, and an alchemist does not
    /// repair (OpenEnroth <c>src/GUI/UI/Houses/Shops.cpp:264-275</c>); a temple heals and teaches
    /// (<c>Temple.cpp:145-152</c>); a tavern rents rooms, sells food, and teaches (<c>Tavern.cpp:182-190</c>);
    /// a training hall trains and teaches (<c>Training.cpp:143-148</c>); a guild sells spell books and
    /// teaches its school (<c>MagicGuild.cpp:120-140,250-262</c>); a town hall posts a bounty and takes a
    /// fine (<c>TownHall.cpp:30-45</c>), and its bounty is a surface rather than an operation, because
    /// claiming one needs the kill this build has no combat for.
    /// </remarks>
    /// <param name="kind">The kind's name, as the building table states it.</param>
    internal static IReadOnlyList<ServiceOperationKind> Operations(string kind) => kind switch
    {
        WeaponShop or ArmorShop or MagicShop =>
            [ServiceOperationKind.Buy, ServiceOperationKind.Sell, ServiceOperationKind.Identify, ServiceOperationKind.Repair, ServiceOperationKind.Teach],
        Alchemist =>
            [ServiceOperationKind.Buy, ServiceOperationKind.Sell, ServiceOperationKind.Identify, ServiceOperationKind.Teach],
        _ when IsGuild(kind) =>
            [ServiceOperationKind.Buy, ServiceOperationKind.Teach],
        Tavern =>
            [ServiceOperationKind.Provision, ServiceOperationKind.Stay, ServiceOperationKind.Teach],
        Temple =>
            [ServiceOperationKind.Cure, ServiceOperationKind.Teach],
        Training =>
            [ServiceOperationKind.Train, ServiceOperationKind.Teach],
        Bank =>
            [ServiceOperationKind.Deposit, ServiceOperationKind.Withdraw],
        Stables or Boats =>
            [ServiceOperationKind.Fare],
        TownHall => [],
        _ => [],
    };

    /// <summary>Which equipment words of the item table a kind's shelves hold.</summary>
    /// <remarks>
    /// The item table's own equipment column divides the goods, so a weapon shop's range is the weapon
    /// words, an armor shop's the armor words, a magic shop's the jewellery, wands, and scrolls, and an
    /// alchemist's the potions, reagents, and gems. A guild's range is not here because its shelves are its
    /// school's spell books instead of an equipment word.
    /// </remarks>
    /// <param name="kind">The kind's name.</param>
    internal static IReadOnlyList<string> StockEquipStats(string kind) => kind switch
    {
        WeaponShop => ["Weapon", "Weapon2", "Weapon1or2", "Missile", "Bow"],
        ArmorShop => ["Armor", "Helm", "Shield", "Cloak", "Boots", "Gauntlets", "Belt"],
        MagicShop => ["Ring", "Amulet", "WeaponW", "Sscroll"],
        Alchemist => ["Bottle", "Reagent", "Gem"],
        _ => [],
    };

    /// <summary>What a kind's counter teaches besides the skills its goods imply.</summary>
    /// <remarks>
    /// The donor's own lists: a magic shop teaches identifying and repairing
    /// (<c>Shops.cpp:733-735</c>), an alchemist alchemy and monster identification (<c>Shops.cpp:737-739</c>),
    /// a temple unarmed combat, dodging, and the merchant's trade (<c>Temple.cpp:147-152</c>), a tavern
    /// stealing, trap disarming, and perception (<c>Tavern.cpp:188-190</c>), and a training hall armsmaster
    /// and bodybuilding (<c>Training.cpp:146-148</c>). A weapon or armor shop teaches the skills of the goods
    /// it stocks, which is read from the catalogue rather than listed here.
    /// </remarks>
    /// <param name="kind">The kind's name.</param>
    internal static IReadOnlyList<string> FixedLessons(string kind) => kind switch
    {
        MagicShop => ["Identify Item", "Repair"],
        Alchemist => ["Alchemy", "Identify Monster"],
        Temple => ["Unarmed", "Dodging", "Merchant"],
        Tavern => ["Stealing", "Disarm Traps", "Perception"],
        Training => ["Armsmaster", "Bodybuilding"],
        _ => [],
    };

    /// <summary>Whether a kind is one of the ten magic guilds.</summary>
    /// <param name="kind">The kind's name.</param>
    internal static bool IsGuild(string kind) => GuildSchool(kind) is not null;

    /// <summary>The magic school a guild's kind names, or null when the kind is not a guild.</summary>
    /// <remarks>
    /// The donor's own mapping from the guild house type to the school it sells (OpenEnroth
    /// <c>src/GUI/UI/Houses/MagicGuild.cpp:31-41</c>, <c>guildSpellsSchool</c>), read from the kind word the
    /// table states rather than from a house id.
    /// </remarks>
    /// <param name="kind">The kind's name.</param>
    internal static string? GuildSchool(string kind) => kind switch
    {
        "Fire Guild" => "Fire",
        "Air Guild" => "Air",
        "Water Guild" => "Water",
        "Earth Guild" => "Earth",
        "Spirit Guild" => "Spirit",
        "Mind Guild" => "Mind",
        "Body Guild" => "Body",
        "Light Guild" => "Light",
        "Dark Guild" => "Dark",
        _ => null,
    };

    /// <summary>The second skill a guild teaches, or empty when it teaches only its school.</summary>
    /// <remarks>
    /// The donor's own pairs (OpenEnroth <c>src/GUI/UI/Houses/MagicGuild.cpp:54-66</c>,
    /// <c>learnableAdditionalSkillDialogue</c>): the four element guilds teach learning, the three mental
    /// and spiritual guilds teach meditation, and the light and dark guilds teach only their own school.
    /// </remarks>
    /// <param name="kind">The kind's name.</param>
    internal static string GuildExtraSkill(string kind) => GuildSchool(kind) switch
    {
        "Fire" or "Air" or "Water" or "Earth" => "Learning",
        "Spirit" or "Mind" or "Body" => "Meditation",
        _ => string.Empty,
    };

    /// <summary>Whether a guild kind is one of the two paired schools, light or dark.</summary>
    /// <param name="kind">The kind's name.</param>
    internal static bool IsPairedGuild(string kind) => GuildSchool(kind) is "Light" or "Dark";

    /// <summary>
    /// The deepest rung of a skill's ladder a counter can teach a member, given the guild rung it stands at.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Ours, over the donor's own guild ladder.</b> The donor's mastery teachers are people in the world
    /// rather than counters: an NPC dialogue topic teaches a skill at expert, master, or grand master, keyed
    /// by the skill and the teacher's level (<c>src/GUI/UI/NPCTopics.cpp:434-540</c>,
    /// <c>masteryTeacherOptionString</c>), and none of those topics is in the shipped topic table this build
    /// imports — 1,000 people carry 2,000-odd topics and not one names a skill's rung (checked over the
    /// operator's own people pack). What the donor does state per guild is how deep in its own school each
    /// guild's knowledge runs: the nine school guilds are four houses apiece, and the house's rung is the
    /// spell mastery it may sell, read from the table's own order rather than from the words
    /// (<c>src/GUI/UI/Houses/MagicGuild.cpp:64-99</c>, <c>guildSpellsMastery</c>).
    /// </para>
    /// <para>
    /// So the keeper of a guild is the master teacher for its school's skill: the rung the guild stands at in
    /// its own ladder is the deepest rung of that skill the guild can take a member to — an initiate guild
    /// teaches the first rung, an adept guild expert, a master guild the master rung, and a paramount guild
    /// grand master. A guild of the Light or the Dark has two houses rather than four, and the donor puts the
    /// first at expert and the second at grand master (<c>MagicGuild.cpp:79-90</c>), which is the same
    /// reading of the same table. Every other counter teaches one rung and no more, which is why a shop that
    /// sells a skill's first lesson does not also sell its mastery.
    /// </para>
    /// </remarks>
    /// <param name="tier">The guild's rung among its school's guilds, counting from one.</param>
    /// <param name="paired">Whether the school keeps two guilds rather than four, which light and dark do.</param>
    internal static int MasteryDepth(int tier, bool paired)
    {
        if (paired) return tier <= 1 ? 2 : 4;
        return Math.Clamp(tier, 1, 4);
    }

    /// <summary>What a fare costs before the counter's multiplier, by the kind that sells it.</summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/PriceCalculator.cpp:162-174</c>, <c>transportCostForPlayer</c>: a boat is
    /// twice a coach, and both scale by the counter's own multiplier.
    /// </remarks>
    /// <param name="kind">The kind's name.</param>
    internal static int FareBase(string kind) => string.Equals(kind, Boats, StringComparison.Ordinal) ? BoatFare : CoachFare;

    /// <summary>
    /// How many hours a rented room gives the party, from the hour the clock stands at.
    /// </summary>
    /// <remarks>
    /// OpenEnroth <c>src/Application/Game.cpp:1054-1066</c>: a room rests the party until dawn plus an hour,
    /// and twelve hours more in the three underground taverns. The dawn is this game's own daylight window
    /// rather than a second statement of when morning is, so a room and the outdoor lighting cannot
    /// disagree about it.
    /// </remarks>
    /// <param name="at">The hour the room is taken at.</param>
    /// <param name="underground">Whether the tavern stands in one of the three underground towns.</param>
    internal static int RoomHours(GameDate at, bool underground)
    {
        int dawn = MightAndMagic7Time.Daylight.Dawn.Hour;
        int hours = ((dawn - at.Hour) + 24) % 24;
        if (hours == 0) hours = 24;
        hours += 1;
        if (underground) hours += 12;
        return hours;
    }

    /// <summary>Whether a counter's place is one of the three underground towns whose taverns rest longer.</summary>
    /// <remarks>
    /// The donor names the houses (OpenEnroth <c>src/Application/Game.cpp:1061-1063</c>): the taverns of
    /// Deyja, the Pit, and Mount Nighon. This reads the places the shipped maps put them on — 5, 8, and 10 —
    /// rather than the house ids, so the rule survives the table being renumbered.
    /// </remarks>
    /// <param name="mapId">The place the counter stands in.</param>
    internal static bool IsUndergroundTown(int mapId) => mapId is 5 or 8 or 10;

    /// <summary>The hour a room is given up at, as the donor's own rest states it.</summary>
    /// <param name="at">The hour the room is taken at.</param>
    /// <param name="underground">Whether the tavern stands in one of the three underground towns.</param>
    internal static int CheckoutHour(GameDate at, bool underground)
    {
        int hours = RoomHours(at, underground);
        return (at.Hour + hours) % 24;
    }
}
