namespace PartyRpg.Kit.Party;

/// <summary>
/// Who one member is: the durable identity a save carries, the name a player reads, and the two content
/// references that say what kind of character this is.
/// </summary>
/// <remarks>
/// Race and class are references, not resolved rules: the kit records which definitions a character was
/// created under and asks the ruleset about everything they imply. A promotion changes the class
/// reference, which is why it can be replaced while the durable identity and the race cannot.
/// </remarks>
public sealed class CharacterProfile
{
    /// <summary>Creates a character's identity.</summary>
    /// <param name="id">The member's durable identity, minted once and kept for the character's whole life.</param>
    /// <param name="name">The name a player reads, which must not be blank.</param>
    /// <param name="race">The race definition the character was created under.</param>
    /// <param name="characterClass">The class definition the character currently belongs to.</param>
    /// <exception cref="ArgumentException">The name is blank.</exception>
    public CharacterProfile(PartyMemberId id, string name, RaceId race, ClassId characterClass)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Id = id;
        Name = name;
        Race = race;
        Class = characterClass;
    }

    /// <summary>The member's durable identity, which a save round-trips.</summary>
    public PartyMemberId Id { get; }

    /// <summary>The name a player reads.</summary>
    public string Name { get; private set; }

    /// <summary>The race definition the character was created under.</summary>
    public RaceId Race { get; }

    /// <summary>The class definition the character currently belongs to.</summary>
    public ClassId Class { get; private set; }

    /// <summary>Renames the character, which is what a scripted rename does.</summary>
    /// <param name="name">The new name, which must not be blank.</param>
    /// <exception cref="ArgumentException">The name is blank, which would leave a character nobody can name.</exception>
    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    /// <summary>
    /// Replaces the class the character belongs to, which is what a promotion does. Whether a promotion is
    /// earned, and what the new class may do, is the ruleset's policy; this records where it landed.
    /// </summary>
    /// <param name="promotedClass">The class definition the character now belongs to.</param>
    public void ChangeClass(ClassId promotedClass) => Class = promotedClass;
}
