namespace MightAndMagic7.Import.Tables;

/// <summary>
/// The words the shipped item and monster tables use for what an item is and what it is used with, read
/// into the two tags the rest of the product compares.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is a format quirk and it lives here.</b> An item row states its shape in a free-text equipment
/// column and its skill in another, and a monster row's treasure cell states what it asks a treasure level
/// for in a third vocabulary of its own; the donor reads all three through its own maps
/// (<c>src/Engine/Tables/ItemTable.cpp:91-131</c> for the item's two, and
/// <c>src/Engine/Objects/Monsters.cpp:299-325</c> with <c>src/Engine/Objects/ItemEnumFunctions.h:279-315</c>
/// for the treasure cell's). A ruleset that had to know those spellings would be a ruleset that knows the
/// file format, so the spellings are read here, once, and what travels on into the packs is two tags.
/// </para>
/// <para>
/// <b>Both halves must agree, which is why one type owns both.</b> A treasure cell that asks for a cloak and
/// an item row that is a cloak are one tag, or the request would find nothing; stating the two maps beside
/// each other is what keeps them from drifting apart.
/// </para>
/// <para>
/// <b>What the donor cannot read, this cannot either.</b> An equipment word outside its map is a misc item
/// and a skill word outside its map is the misc skill, because that is what the donor's own
/// <c>valueOr(map, word, default)</c> does; a treasure cell word outside the donor's map asks for anything
/// at all, which is the donor's answer too. Nothing here guesses at a word the shipped game never states.
/// </para>
/// </remarks>
public static class ItemVocabulary
{
    /// <summary>The tag an item with no readable kind carries, and the skill the donor falls back to.</summary>
    public const string Misc = "misc";

    /// <summary>The tag a teaching book carries, which the donor reads a spell from.</summary>
    public const string Book = "book";

    /// <summary>The tag a spell scroll carries, which the donor reads a spell from.</summary>
    public const string SpellScroll = "spell-scroll";

    /// <summary>The tag a wand carries, which the donor reads a spell from.</summary>
    public const string Wand = "wand";

    /// <summary>The kinds the donor's equipment map names, in the data's own spellings.</summary>
    private static readonly Dictionary<string, string> KindsByEquipStat = new(StringComparer.OrdinalIgnoreCase)
    {
        ["weapon"] = "single-handed",
        ["weapon1or2"] = "single-handed",
        ["weapon2"] = "two-handed",
        ["missile"] = "bow",
        ["bow"] = "bow",
        ["armor"] = "armour",
        ["shield"] = "shield",
        ["helm"] = "helmet",
        ["belt"] = "belt",
        ["cloak"] = "cloak",
        ["gauntlets"] = "gauntlets",
        ["boots"] = "boots",
        ["ring"] = "ring",
        ["amulet"] = "amulet",
        ["weaponw"] = "wand",
        ["herb"] = "reagent",
        ["reagent"] = "reagent",
        ["bottle"] = "potion",
        ["sscroll"] = "spell-scroll",
        ["book"] = "book",
        ["mscroll"] = "message-scroll",
        ["gold"] = "gold",
        ["gem"] = "gem",
    };

    /// <summary>The skills the donor's item-skill map names, in the data's own spellings.</summary>
    private static readonly Dictionary<string, string> SkillsByGroup = new(StringComparer.OrdinalIgnoreCase)
    {
        ["staff"] = "staff",
        ["sword"] = "sword",
        ["dagger"] = "dagger",
        ["axe"] = "axe",
        ["spear"] = "spear",
        ["bow"] = "bow",
        ["mace"] = "mace",
        ["blaster"] = "blaster",
        ["shield"] = "shield",
        ["leather"] = "leather",
        ["chain"] = "chain",
        ["plate"] = "plate",
        ["club"] = "club",
    };

    /// <summary>
    /// What one kind of thing a treasure request can name asks a level for.
    /// </summary>
    /// <remarks>
    /// The donor's own table, read as it stands: a request for a weapon asks for a single-handed one, a
    /// request for armour by its kind, and a request for a sword asks by the skill a sword is used with
    /// rather than by its shape. A word outside the donor's map asks for anything, which is what its own
    /// reader answers with.
    /// </remarks>
    private static readonly Dictionary<string, (string Kind, string Skill)> FiltersByTreasureWord = new(StringComparer.OrdinalIgnoreCase)
    {
        ["weapon"] = ("single-handed", string.Empty),
        ["armor"] = ("armour", string.Empty),
        ["misc"] = (string.Empty, Misc),
        ["sword"] = (string.Empty, "sword"),
        ["dagger"] = (string.Empty, "dagger"),
        ["axe"] = (string.Empty, "axe"),
        ["spear"] = (string.Empty, "spear"),
        ["bow"] = (string.Empty, "bow"),
        ["mace"] = (string.Empty, "mace"),
        ["club"] = (string.Empty, "club"),
        ["staff"] = (string.Empty, "staff"),
        ["leather"] = (string.Empty, "leather"),
        ["chain"] = (string.Empty, "chain"),
        ["plate"] = (string.Empty, "plate"),
        ["shield"] = ("shield", string.Empty),
        ["helm"] = ("helmet", string.Empty),
        ["belt"] = ("belt", string.Empty),
        ["cape"] = ("cloak", string.Empty),
        ["gauntlets"] = ("gauntlets", string.Empty),
        ["boots"] = ("boots", string.Empty),
        ["ring"] = ("ring", string.Empty),
        ["amulet"] = ("amulet", string.Empty),
        ["wand"] = ("wand", string.Empty),
        ["scroll"] = ("spell-scroll", string.Empty),
        ["gem"] = ("gem", string.Empty),
    };

    /// <summary>What kind of thing an item is, as the product's own tag.</summary>
    /// <param name="equipStat">The row's own equipment word.</param>
    /// <returns>The tag, or <see cref="Misc"/> when the donor has no kind for the word.</returns>
    public static string KindOf(string equipStat) =>
        KindsByEquipStat.TryGetValue(equipStat.Trim(), out string? kind) ? kind : Misc;

    /// <summary>What skill an item is used with, as the product's own tag.</summary>
    /// <param name="skillGroup">The row's own skill word.</param>
    /// <returns>The tag, or <see cref="Misc"/> when the donor has no skill for the word.</returns>
    public static string SkillOf(string skillGroup) =>
        SkillsByGroup.TryGetValue(skillGroup.Trim(), out string? skill) ? skill : Misc;

    /// <summary>What a treasure request that names a kind of thing asks a level for.</summary>
    /// <param name="treasureWord">The word the request stated, which may be empty.</param>
    /// <returns>The two tags, both empty when the request asks for anything at all.</returns>
    public static (string Kind, string Skill) FilterOf(string treasureWord) =>
        FiltersByTreasureWord.TryGetValue(treasureWord.Trim(), out (string Kind, string Skill) filter)
            ? filter
            : (string.Empty, string.Empty);
}
