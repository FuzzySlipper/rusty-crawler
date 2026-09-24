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

    /// <summary>Spell definitions by school.</summary>
    public static LodSource Spells { get; } = new("spells", RulesArchive, "spells.txt");

    /// <summary>Item definitions.</summary>
    public static LodSource Items { get; } = new("items", RulesArchive, "items.txt");

    /// <summary>Class and rank descriptions.</summary>
    public static LodSource Classes { get; } = new("classes", RulesArchive, "class.txt");

    /// <summary>Skill descriptions and per-tier effects.</summary>
    public static LodSource Skills { get; } = new("skills", RulesArchive, "skilldes.txt");

    /// <summary>Quest bit text.</summary>
    public static LodSource Quests { get; } = new("quests", RulesArchive, "quests.txt");

    /// <summary>Every declared table source.</summary>
    public static IReadOnlyList<LodSource> All { get; } =
    [
        MapStats,
        Services,
        Monsters,
        Spells,
        Items,
        Classes,
        Skills,
        Quests,
    ];
}
