namespace PartyRpg.Kit.Party;

/// <summary>
/// Everything a member is, apart from the durable identity the party mints and the items it holds.
/// </summary>
/// <remarks>
/// One shape serves both ends of a party's life: creation fills it with what the creation flow chose, and a
/// save writes it back exactly as it stood. Items are deliberately absent — an instance is recorded once,
/// in the save's own item list, together with the custody that says whether it lies in the shared pack or
/// on a member's figure — so no member record can disagree with where its equipment went. The class
/// parameter is spelled <c>@class</c> because a save writes and reads this shape field by field, and the
/// generated metadata binds a field to the constructor parameter that carries the property's own name.
/// </remarks>
public sealed record PartyMemberSeed
{
    /// <summary>Creates a member's state.</summary>
    /// <param name="name">The name a player reads, which must not be blank.</param>
    /// <param name="race">The race definition the character was created under.</param>
    /// <param name="class">The class definition the character belongs to.</param>
    /// <param name="attributes">The character's attribute scores, in the game's own order.</param>
    /// <param name="skills">The skills the character has learned.</param>
    /// <param name="spells">The spells the character knows.</param>
    /// <param name="experience">Total experience earned.</param>
    /// <param name="level">The character's current level.</param>
    /// <param name="skillPoints">Skill points not yet spent.</param>
    /// <param name="classRank">The character's rank in its class ladder.</param>
    /// <param name="conditions">The conditions acting on the character.</param>
    /// <param name="hitPoints">What the character has to lose.</param>
    /// <param name="spellPoints">What the character has to cast with.</param>
    /// <param name="portrait">
    /// The portrait the character was created with, or null when nothing chose one. It is state rather than
    /// presentation because in this game family the face decides the race, so a character that lost the
    /// portrait it was created with has lost part of who it is.
    /// </param>
    /// <param name="quickSpell">
    /// The spell the character casts with one key, or null when they have chosen none. It is part of what a
    /// character is, like the spellbook it is drawn from, so a save carries it and a restore puts it back.
    /// </param>
    /// <exception cref="ArgumentException">The name is blank.</exception>
    public PartyMemberSeed(
        string name,
        RaceId race,
        ClassId @class,
        IReadOnlyList<AttributeScore> attributes,
        IReadOnlyList<SkillEntry> skills,
        IReadOnlyList<SpellId> spells,
        long experience,
        int level,
        int skillPoints,
        int classRank,
        IReadOnlyList<ActiveCondition> conditions,
        ResourcePool hitPoints,
        ResourcePool spellPoints,
        PortraitId? portrait = null,
        SpellId? quickSpell = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(attributes);
        ArgumentNullException.ThrowIfNull(skills);
        ArgumentNullException.ThrowIfNull(spells);
        ArgumentNullException.ThrowIfNull(conditions);
        Name = name;
        Race = race;
        Class = @class;
        Attributes = attributes;
        Skills = skills;
        Spells = spells;
        Experience = experience;
        Level = level;
        SkillPoints = skillPoints;
        ClassRank = classRank;
        Conditions = conditions;
        HitPoints = hitPoints;
        SpellPoints = spellPoints;
        Portrait = portrait;
        QuickSpell = quickSpell;
    }

    /// <summary>The name a player reads.</summary>
    public string Name { get; }

    /// <summary>The race definition the character was created under.</summary>
    public RaceId Race { get; }

    /// <summary>The class definition the character belongs to.</summary>
    public ClassId Class { get; }

    /// <summary>The character's attribute scores, in the game's own order.</summary>
    public IReadOnlyList<AttributeScore> Attributes { get; }

    /// <summary>The skills the character has learned.</summary>
    public IReadOnlyList<SkillEntry> Skills { get; }

    /// <summary>The spells the character knows.</summary>
    public IReadOnlyList<SpellId> Spells { get; }

    /// <summary>Total experience earned.</summary>
    public long Experience { get; }

    /// <summary>The character's current level.</summary>
    public int Level { get; }

    /// <summary>Skill points not yet spent.</summary>
    public int SkillPoints { get; }

    /// <summary>The character's rank in its class ladder.</summary>
    public int ClassRank { get; }

    /// <summary>The conditions acting on the character.</summary>
    public IReadOnlyList<ActiveCondition> Conditions { get; }

    /// <summary>What the character has to lose.</summary>
    public ResourcePool HitPoints { get; }

    /// <summary>What the character has to cast with.</summary>
    public ResourcePool SpellPoints { get; }

    /// <summary>
    /// The portrait the character was created with, or null when nothing chose one.
    /// </summary>
    /// <remarks>
    /// Recorded beside the race rather than derived from it: the race is what the portrait decided, but the
    /// portrait is which face the player picked, and two characters of one race are only told apart by it.
    /// </remarks>
    public PortraitId? Portrait { get; }

    /// <summary>The spell the character casts with one key, or null when they have chosen none.</summary>
    /// <remarks>
    /// Recorded beside the spells rather than derived from them, because which of several known spells a
    /// player keeps in the slot is their choice and not something a spellbook can work out.
    /// </remarks>
    public SpellId? QuickSpell { get; }
}
