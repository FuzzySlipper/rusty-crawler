using MightAndMagic7.Import.Lod;

namespace MightAndMagic7.Import.Tables;

/// <summary>
/// The declared sources of the tables this importer consumes, and the archive they all live in.
/// </summary>
/// <remarks>
/// The seventh game's rule tables live in <c>Events.lod</c>. Another archive in the same installation
/// carries a second, older copy of several of them under the same names, from the previous game in
/// the family — its class table lists that game's classes and its map table has a different column
/// set. Declaring the archive per table is what keeps that copy out of an import.
/// </remarks>
public static class Mm7TableSources
{
    /// <summary>The archive that owns this game's rule tables.</summary>
    public const string RulesArchive = "Events.lod";

    /// <summary>The container that owns map files and music.</summary>
    public const string AssetsArchive = "Games.lod";

    /// <summary>The container that owns the second, older table set. Named so a report can warn about it.</summary>
    public const string LegacyTablesArchive = "Icons.lod";

    /// <summary>Per-map metadata, names, file names, respawn and encounter settings.</summary>
    public static LodSource MapStats { get; } = new("map-stats", RulesArchive, "MapStats.txt");

    /// <summary>Buildings, services, schedules, and their exits.</summary>
    public static LodSource Services { get; } = new("services", RulesArchive, "2dEvents.txt");

    /// <summary>Monster statistics.</summary>
    public static LodSource Monsters { get; } = new("monsters", RulesArchive, "monsters.txt");

    /// <summary>What every kind of monster thinks of every other kind, and of the party.</summary>
    public static LodSource Hostility { get; } = new("hostility", RulesArchive, "hostile.txt");

    /// <summary>Spell definitions by school.</summary>
    public static LodSource Spells { get; } = new("spells", RulesArchive, "spells.txt");

    /// <summary>Item definitions.</summary>
    public static LodSource Items { get; } = new("items", RulesArchive, "items.txt");

    /// <summary>How often each item appears at each treasure level, which is what random loot draws from.</summary>
    public static LodSource RandomItems { get; } = new("random-items", RulesArchive, "rnditems.txt");

    /// <summary>Class and rank descriptions.</summary>
    public static LodSource Classes { get; } = new("classes", RulesArchive, "class.txt");

    /// <summary>Skill descriptions and per-tier effects.</summary>
    public static LodSource Skills { get; } = new("skills", RulesArchive, "skilldes.txt");

    /// <summary>Quest bit text.</summary>
    public static LodSource Quests { get; } = new("quests", RulesArchive, "quests.txt");

    /// <summary>The people the world holds: names, portraits, buildings, and dialogue events.</summary>
    public static LodSource Npcs { get; } = new("npcs", RulesArchive, "npcdata.txt");

    /// <summary>What each person says when met, and when met again.</summary>
    public static LodSource Greetings { get; } = new("npc-greetings", RulesArchive, "npcgreet.txt");

    /// <summary>What each person can be asked about, and who owns each topic.</summary>
    public static LodSource Topics { get; } = new("npc-topics", RulesArchive, "npctopic.txt");

    /// <summary>What a person answers with, keyed by the number a topic names.</summary>
    public static LodSource TopicTexts { get; } = new("npc-texts", RulesArchive, "npctext.txt");

    /// <summary>Every declared table source.</summary>
    public static IReadOnlyList<LodSource> All { get; } =
    [
        MapStats,
        Services,
        Monsters,
        Hostility,
        Spells,
        Items,
        RandomItems,
        Classes,
        Skills,
        Quests,
        Npcs,
        Greetings,
        Topics,
        TopicTexts,
    ];
}
