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
        RandomItemsTable randomItems,
        PotionTable potions,
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
        RandomItems = randomItems;
        Potions = potions;
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

    /// <summary>What each item weighs at each treasure level, which is what random loot draws from.</summary>
    public RandomItemsTable RandomItems { get; }

    /// <summary>Reagents, potions, and what combining a pair of them makes.</summary>
    public PotionTable Potions { get; }

    /// <summary>Quest text.</summary>
    public QuestTable Quests { get; }

    /// <summary>The people the world holds, what they say when met, and what they can be asked about.</summary>
    public PersonTable People { get; }

    /// <summary>Reads every typed table from an installation.</summary>
    public static Mm7Tables Read(LodInstall install)
    {
        ArgumentNullException.ThrowIfNull(install);

        // The item table is read first because the potion table checks a reagent's stated power against the
        // damage column of the same row, which is where the donor reads that power from.
        ItemTable items = ItemTable.Read(install);
        return new Mm7Tables(
            ClassTable.Read(install),
            SkillTable.Read(install),
            MapStatsTable.Read(install),
            ServiceTable.Read(install),
            MonsterTable.Read(install),
            HostilityTable.Read(install),
            SpellTable.Read(install),
            items,
            RandomItemsTable.Read(install),
            PotionTable.Read(install, items),
            QuestTable.Read(install),
            PersonTable.Read(install));
    }
}
