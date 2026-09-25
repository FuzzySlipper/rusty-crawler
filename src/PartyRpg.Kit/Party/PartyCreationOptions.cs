using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Party;

/// <summary>
/// Everything a ruleset states so a party can be created: how many members, which races, classes and
/// portraits are offered, how many attribute points there are to spend, and what the new party owns.
/// </summary>
/// <remarks>
/// <para>
/// Creation's choices are data because they belong to a game, not to this layer: the races, classes,
/// portraits, attribute tables and prices, the size of the party, and the skills a player picks all arrive
/// here, and the flow only ever compares the two — which is what lets one creation mechanism serve a game
/// whose party is four characters and a game whose party is six.
/// </para>
/// <para>
/// The options are validated once, when they are assembled, because a choice list that contradicts itself —
/// a portrait drawn as a race nobody may be, a class that fixes the same skill twice — cannot be repaired by
/// refusing a player later.
/// </para>
/// </remarks>
public sealed record PartyCreationOptions
{
    /// <summary>States the choices creation offers.</summary>
    /// <param name="memberCount">How many characters the party is created with, at least one.</param>
    /// <param name="races">The races a player may choose, at least one.</param>
    /// <param name="classes">The classes a player may choose, at least one.</param>
    /// <param name="portraits">The portraits a player may choose, at least one.</param>
    /// <param name="attributePool">How many attribute points each character has to spend.</param>
    /// <param name="chosenSkillCount">How many skills beyond the class's fixed ones a player chooses.</param>
    /// <param name="nameMaximumLength">The longest name a character may be given.</param>
    /// <param name="startingSkillTier">The rung a skill learned at creation stands at.</param>
    /// <param name="startingLevel">The level a created character begins at.</param>
    /// <param name="startingCoins">What the new party's purse holds.</param>
    /// <param name="startingFoodPortions">What the new party's larder holds.</param>
    /// <param name="startingFoodUnit">The unit the larder measures provisions in.</param>
    /// <param name="startingReputation">What the world initially thinks of the new party.</param>
    /// <param name="startingFame">How widely the new party is initially known.</param>
    /// <exception cref="ArgumentException">
    /// A count is below one, a list is empty or repeats an id, a portrait names a race that is not offered, or
    /// a name length is below one.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">The attribute pool or a starting amount is negative.</exception>
    public PartyCreationOptions(
        int memberCount,
        IReadOnlyList<CreationRace> races,
        IReadOnlyList<CreationClass> classes,
        IReadOnlyList<CreationPortrait> portraits,
        int attributePool,
        int chosenSkillCount,
        int nameMaximumLength,
        SkillTier startingSkillTier,
        int startingLevel,
        int startingCoins = 0,
        int startingFoodPortions = 0,
        ProvisionUnit startingFoodUnit = ProvisionUnit.Portions,
        int startingReputation = 0,
        int startingFame = 0)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(memberCount, 1);
        ArgumentNullException.ThrowIfNull(races);
        ArgumentNullException.ThrowIfNull(classes);
        ArgumentNullException.ThrowIfNull(portraits);
        ArgumentOutOfRangeException.ThrowIfNegative(attributePool);
        ArgumentOutOfRangeException.ThrowIfNegative(chosenSkillCount);
        ArgumentOutOfRangeException.ThrowIfLessThan(nameMaximumLength, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(startingLevel, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(startingCoins);
        ArgumentOutOfRangeException.ThrowIfNegative(startingFoodPortions);

        RequireUniqueIds(races, race => race.Id.Value, "race");
        RequireUniqueIds(classes, characterClass => characterClass.Id.Value, "class", "classes");
        RequireUniqueIds(portraits, portrait => portrait.Id.Value, "portrait");

        // A portrait that is drawn as a race creation does not offer would let a player pick a face with no
        // attribute table behind it, so the lists must agree before anyone can choose.
        foreach (CreationPortrait portrait in portraits)
        {
            bool known = false;
            foreach (CreationRace race in races) known |= race.Id == portrait.Race;
            if (!known)
            {
                throw new ArgumentException(
                    $"Portrait '{portrait.Name}' is drawn as race '{portrait.Race}', which creation does not offer.",
                    nameof(portraits));
            }
        }

        MemberCount = memberCount;
        Races = races;
        Classes = classes;
        Portraits = portraits;
        AttributePool = attributePool;
        ChosenSkillCount = chosenSkillCount;
        NameMaximumLength = nameMaximumLength;
        StartingSkillTier = startingSkillTier;
        StartingLevel = startingLevel;
        StartingCoins = startingCoins;
        StartingFoodPortions = startingFoodPortions;
        StartingFoodUnit = startingFoodUnit;
        StartingReputation = startingReputation;
        StartingFame = startingFame;
    }

    /// <summary>How many characters the party is created with.</summary>
    public int MemberCount { get; }

    /// <summary>The races a player may choose.</summary>
    public IReadOnlyList<CreationRace> Races { get; }

    /// <summary>The classes a player may choose.</summary>
    public IReadOnlyList<CreationClass> Classes { get; }

    /// <summary>The portraits a player may choose.</summary>
    public IReadOnlyList<CreationPortrait> Portraits { get; }

    /// <summary>How many attribute points each character has to spend.</summary>
    public int AttributePool { get; }

    /// <summary>How many skills beyond the class's fixed ones a player chooses.</summary>
    public int ChosenSkillCount { get; }

    /// <summary>The longest name a character may be given.</summary>
    public int NameMaximumLength { get; }

    /// <summary>The rung a skill learned at creation stands at.</summary>
    public SkillTier StartingSkillTier { get; }

    /// <summary>The level a created character begins at.</summary>
    public int StartingLevel { get; }

    /// <summary>What the new party's purse holds.</summary>
    public int StartingCoins { get; }

    /// <summary>What the new party's larder holds.</summary>
    public int StartingFoodPortions { get; }

    /// <summary>The unit the larder measures provisions in.</summary>
    public ProvisionUnit StartingFoodUnit { get; }

    /// <summary>What the world initially thinks of the new party.</summary>
    public int StartingReputation { get; }

    /// <summary>How widely the new party is initially known.</summary>
    public int StartingFame { get; }

    /// <summary>Finds a race by id.</summary>
    /// <param name="id">The race to find.</param>
    /// <returns>The race, or null when creation does not offer it.</returns>
    public CreationRace? FindRace(RaceId id)
    {
        foreach (CreationRace race in Races)
        {
            if (race.Id == id) return race;
        }

        return null;
    }

    /// <summary>Finds a class by id.</summary>
    /// <param name="id">The class to find.</param>
    /// <returns>The class, or null when creation does not offer it.</returns>
    public CreationClass? FindClass(ClassId id)
    {
        foreach (CreationClass characterClass in Classes)
        {
            if (characterClass.Id == id) return characterClass;
        }

        return null;
    }

    /// <summary>Finds a portrait by id.</summary>
    /// <param name="id">The portrait to find.</param>
    /// <returns>The portrait, or null when creation does not offer it.</returns>
    public CreationPortrait? FindPortrait(PortraitId id)
    {
        foreach (CreationPortrait portrait in Portraits)
        {
            if (portrait.Id == id) return portrait;
        }

        return null;
    }

    /// <summary>Refuses a list that offers nothing at all, or offers the same definition twice.</summary>
    private static void RequireUniqueIds<T>(IReadOnlyList<T> items, Func<T, string> id, string kind, string? parameterName = null)
    {
        string parameter = parameterName ?? kind + "s";
        if (items.Count == 0)
        {
            throw new ArgumentException(
                $"Creation offers no {kind}, so a player would have nothing to choose from.",
                parameter);
        }

        HashSet<string> seen = [];
        foreach (T item in items)
        {
            if (!seen.Add(id(item)))
            {
                throw new ArgumentException(
                    $"Creation offers the {kind} '{id(item)}' more than once, so which one a player picked would be ambiguous.",
                    parameter);
            }
        }
    }
}
