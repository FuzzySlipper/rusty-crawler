namespace PartyRpg.Kit.Party;

/// <summary>
/// Where one character stands in the game's progression: experience, level, unspent skill points, and class
/// rank.
/// </summary>
/// <remarks>
/// Bookkeeping, deliberately. The experience curve, the level cap, what a rank permits, and the order of
/// promotions are ruleset policy, so this holds the four values a save round-trips and the plain
/// transitions those policies perform. Nothing here computes a level from experience or decides what a
/// rank means, because a formula in this layer would become a second, silently disagreeing copy of the
/// ruleset's.
/// </remarks>
public sealed class CharacterProgression
{
    /// <summary>Creates a character's progression.</summary>
    /// <param name="experience">Total experience earned, which cannot be negative.</param>
    /// <param name="level">The character's current level, which is at least one.</param>
    /// <param name="skillPoints">Skill points not yet spent, which cannot be negative.</param>
    /// <param name="classRank">The character's rank in its class ladder, which is at least one.</param>
    /// <exception cref="ArgumentOutOfRangeException">A value is outside what it can mean.</exception>
    public CharacterProgression(long experience = 0, int level = 1, int skillPoints = 0, int classRank = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(experience);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(level);
        ArgumentOutOfRangeException.ThrowIfNegative(skillPoints);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(classRank);
        Experience = experience;
        Level = level;
        SkillPoints = skillPoints;
        ClassRank = classRank;
    }

    /// <summary>Total experience earned.</summary>
    public long Experience { get; private set; }

    /// <summary>The character's current level.</summary>
    public int Level { get; private set; }

    /// <summary>Skill points granted and not yet spent.</summary>
    public int SkillPoints { get; private set; }

    /// <summary>The character's rank in its class ladder, where one is the starting rank.</summary>
    public int ClassRank { get; private set; }

    /// <summary>Awards experience. Whether it is enough to advance a level is the ruleset's decision, made after this.</summary>
    /// <param name="amount">How much experience to award, which cannot be negative.</param>
    /// <exception cref="ArgumentOutOfRangeException">The amount is negative.</exception>
    /// <exception cref="OverflowException">The total would leave the numbers experience is described in.</exception>
    public void AwardExperience(long amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        Experience = checked(Experience + amount);
    }

    /// <summary>Records the level the ruleset advanced the character to.</summary>
    /// <param name="level">The new level, which is at least one.</param>
    /// <exception cref="ArgumentOutOfRangeException">The level is below one.</exception>
    public void SetLevel(int level)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(level);
        Level = level;
    }

    /// <summary>Grants skill points, which a level-up or a reward does.</summary>
    /// <param name="points">How many points to grant, which cannot be negative.</param>
    /// <exception cref="ArgumentOutOfRangeException">The points are negative.</exception>
    /// <exception cref="OverflowException">The pool would leave the numbers it is described in.</exception>
    public void GrantSkillPoints(int points)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(points);
        SkillPoints = checked(SkillPoints + points);
    }

    /// <summary>Spends skill points from the pool.</summary>
    /// <param name="points">How many points to spend, which cannot be negative.</param>
    /// <returns>Whether the pool held enough; a refused spend leaves the pool untouched.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The points are negative.</exception>
    public bool SpendSkillPoints(int points)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(points);
        if (points > SkillPoints) return false;
        SkillPoints -= points;
        return true;
    }

    /// <summary>Records the rank the ruleset promoted the character to.</summary>
    /// <param name="rank">The new rank in the class ladder, which is at least one.</param>
    /// <exception cref="ArgumentOutOfRangeException">The rank is below one.</exception>
    public void SetClassRank(int rank)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rank);
        ClassRank = rank;
    }
}
