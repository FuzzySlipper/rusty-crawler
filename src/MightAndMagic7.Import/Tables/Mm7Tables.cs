using MightAndMagic7.Import.Lod;

namespace MightAndMagic7.Import.Tables;

/// <summary>Every typed table this importer reads, read once.</summary>
public sealed class Mm7Tables
{
    private Mm7Tables(
        ClassTable classes,
        SkillTable skills,
        MapStatsTable maps,
        ServiceTable services,
        MonsterTable monsters,
        SpellTable spells,
        ItemTable items,
        QuestTable quests)
    {
        Classes = classes;
        Skills = skills;
        Maps = maps;
        Services = services;
        Monsters = monsters;
        Spells = spells;
        Items = items;
        Quests = quests;
    }

    /// <summary>Classes and ranks.</summary>
    public ClassTable Classes { get; }

    /// <summary>Skills and their mastery tiers.</summary>
    public SkillTable Skills { get; }

    /// <summary>Per-map metadata.</summary>
    public MapStatsTable Maps { get; }

    /// <summary>Buildings, services, and schedules.</summary>
    public ServiceTable Services { get; }

    /// <summary>Monsters.</summary>
    public MonsterTable Monsters { get; }

    /// <summary>Spells.</summary>
    public SpellTable Spells { get; }

    /// <summary>Items.</summary>
    public ItemTable Items { get; }

    /// <summary>Quest text.</summary>
    public QuestTable Quests { get; }

    /// <summary>Reads every typed table from an installation.</summary>
    public static Mm7Tables Read(LodInstall install)
    {
        ArgumentNullException.ThrowIfNull(install);
        return new Mm7Tables(
            ClassTable.Read(install),
            SkillTable.Read(install),
            MapStatsTable.Read(install),
            ServiceTable.Read(install),
            MonsterTable.Read(install),
            SpellTable.Read(install),
            ItemTable.Read(install),
            QuestTable.Read(install));
    }
}
