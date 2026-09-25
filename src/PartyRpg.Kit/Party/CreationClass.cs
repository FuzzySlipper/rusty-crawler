namespace PartyRpg.Kit.Party;

/// <summary>
/// One class creation may choose: the skills it fixes, the skills it lets a player choose, and what a
/// character of that class starts with.
/// </summary>
/// <remarks>
/// <para>
/// Which skills a class may learn is not the kit's knowledge and not always the shipped data's either, so it
/// arrives here as two lists: the skills the class starts with because of what it is, and the skills a player
/// may pick from at creation. Creation checks a chosen skill against the second list and reads the first,
/// which is why the kit can refuse an illegal choice without ever knowing a skill's name.
/// </para>
/// <para>
/// The two lists are disjoint: a skill a class already fixes is not something a player chooses again.
/// </para>
/// </remarks>
public sealed record CreationClass
{
    /// <summary>States one class's creation terms.</summary>
    /// <param name="id">The class definition's id.</param>
    /// <param name="name">What the class is called, for the messages a refused choice carries.</param>
    /// <param name="fixedSkills">The skills the class starts with, in the order they are granted.</param>
    /// <param name="choosableSkills">The skills a player may pick for this class, in the order they are offered.</param>
    /// <param name="startingHitPoints">What a character of this class starts with to lose.</param>
    /// <param name="startingSpellPoints">What a character of this class starts with to cast.</param>
    /// <param name="startingRank">The rung in the class ladder a character of this class starts on.</param>
    /// <exception cref="ArgumentException">
    /// A name or list is blank or empty, a skill is listed twice, or a fixed skill is also choosable.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">A starting amount is negative or the starting rank is below one.</exception>
    public CreationClass(
        ClassId id,
        string name,
        IReadOnlyList<SkillId> fixedSkills,
        IReadOnlyList<SkillId> choosableSkills,
        int startingHitPoints,
        int startingSpellPoints,
        int startingRank)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(fixedSkills);
        ArgumentNullException.ThrowIfNull(choosableSkills);
        if (fixedSkills.Count == 0)
        {
            throw new ArgumentException(
                $"Class '{name}' fixes no skills, so a character of that class would start with nothing it was made for.",
                nameof(fixedSkills));
        }

        if (choosableSkills.Count == 0)
        {
            throw new ArgumentException(
                $"Class '{name}' offers no skills to choose, so a player would have nothing to pick.",
                nameof(choosableSkills));
        }

        HashSet<SkillId> fixedSet = [];
        foreach (SkillId skill in fixedSkills)
        {
            if (!fixedSet.Add(skill))
            {
                throw new ArgumentException(
                    $"Class '{name}' fixes '{skill}' twice, so creation would grant it twice.",
                    nameof(fixedSkills));
            }
        }

        HashSet<SkillId> choosableSet = [];
        foreach (SkillId skill in choosableSkills)
        {
            if (!choosableSet.Add(skill))
            {
                throw new ArgumentException(
                    $"Class '{name}' offers '{skill}' twice, so which pick it is would be ambiguous.",
                    nameof(choosableSkills));
            }

            if (fixedSet.Contains(skill))
            {
                throw new ArgumentException(
                    $"Class '{name}' fixes '{skill}' and also offers it as a choice; a skill the class grants anyway is not a choice.",
                    nameof(choosableSkills));
            }
        }

        ArgumentOutOfRangeException.ThrowIfNegative(startingHitPoints);
        ArgumentOutOfRangeException.ThrowIfNegative(startingSpellPoints);
        ArgumentOutOfRangeException.ThrowIfLessThan(startingRank, 1);
        Id = id;
        Name = name;
        FixedSkills = fixedSkills;
        ChoosableSkills = choosableSkills;
        StartingHitPoints = startingHitPoints;
        StartingSpellPoints = startingSpellPoints;
        StartingRank = startingRank;
    }

    /// <summary>The class definition's id.</summary>
    public ClassId Id { get; }

    /// <summary>What the class is called.</summary>
    public string Name { get; }

    /// <summary>The skills the class starts with, in the order they are granted.</summary>
    public IReadOnlyList<SkillId> FixedSkills { get; }

    /// <summary>The skills a player may pick for this class, in the order they are offered.</summary>
    public IReadOnlyList<SkillId> ChoosableSkills { get; }

    /// <summary>What a character of this class starts with to lose.</summary>
    public int StartingHitPoints { get; }

    /// <summary>What a character of this class starts with to cast.</summary>
    public int StartingSpellPoints { get; }

    /// <summary>The rung in the class ladder a character of this class starts on.</summary>
    public int StartingRank { get; }

    /// <summary>Whether the class may learn a skill at all, whether it fixes it or offers it.</summary>
    /// <param name="skill">The skill to look for.</param>
    public bool Allows(SkillId skill) => FixedSkills.Contains(skill) || ChoosableSkills.Contains(skill);
}
