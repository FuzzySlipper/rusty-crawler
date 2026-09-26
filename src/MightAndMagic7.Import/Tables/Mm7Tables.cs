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
        HostilityTable hostility,
        SpellTable spells,
        ItemTable items,
        QuestTable quests,
        PersonTable people)
    {
        Classes = classes;
        Skills = skills;
        Maps = maps;
        Services = services;
        Monsters = monsters;
        Hostility = hostility;
        Spells = spells;
        Items = items;
        Quests = quests;
        People = people;
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

    /// <summary>What every kind of monster thinks of every other kind, and of the party.</summary>
    public HostilityTable Hostility { get; }

    /// <summary>Spells.</summary>
    public SpellTable Spells { get; }

    /// <summary>Items.</summary>
    public ItemTable Items { get; }

    /// <summary>Quest text.</summary>
    public QuestTable Quests { get; }

    /// <summary>The people the world holds, what they say when met, and what they can be asked about.</summary>
    public PersonTable People { get; }

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
            HostilityTable.Read(install),
            SpellTable.Read(install),
            ItemTable.Read(install),
            QuestTable.Read(install),
            PersonTable.Read(install));
    }
}
