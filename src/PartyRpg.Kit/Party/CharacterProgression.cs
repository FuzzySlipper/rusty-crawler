namespace PartyRpg.Kit.Party;

/// <summary>
/// Where one character stands in the game's progression: experience, level, unspent skill points, and class
/// rank.
/// </summary>
/// <remarks>
/// <para>
/// Bookkeeping, deliberately. The experience curve, the level cap, what a rank permits, and the order of
/// promotions are ruleset policy, so this holds the four values a save round-trips and the plain
/// transitions those policies perform. Nothing here computes a level from experience or decides what a
/// rank means, because a formula in this layer would become a second, silently disagreeing copy of the
/// ruleset's.
/// </para>
/// <para>
/// <b>This holds the values; <see cref="Progression.PartyProgression"/> is what may move them.</b> The
/// transitions below are internal to the kit so that no other assembly can award experience, set a level,
/// grant or spend skill points, or set a rank: the only callers are the progression owner, which asks the
/// ruleset's policy first. A public setter here would be a second writer of one fact, and "the owner is the
/// only mutator" would be a promise rather than something the compiler holds.
/// </para>
/// </remarks>
public sealed class CharacterProgression
{
    /// <summary>
    /// The change stamp this state took when it last changed, or when it was made: a reader that kept what it built
    /// beside this stamp reads the state again only when the stamp has moved (<see cref="ChangeStamp"/>).
    /// </summary>
    public long Stamp { get; private set; } = ChangeStamp.Next();

    /// <summary>Creates a character's progression.</summary>
    /// <param name="experience">Total experience earned, which cannot be negative.</param>
    /// <param name="level">The character's current level, which is at least one.</param>
    /// <param name="skillPoints">Skill points not yet spent, which cannot be negative.</param>
    /// <param name="classRank">The character's rank in its class ladder, which is at least one.</param>
    /// <exception cref="ArgumentOutOfRangeException">A value is outside what it can mean.</exception>
    public CharacterProgression(long experience = 0, int level = 1, int skillPoints = 0, int classRank = 1, int ageOffset = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ageOffset);
        AgeOffset = ageOffset;
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

    /// <summary>
    /// How many years older than their natural age the character has been made, zero when nothing has aged them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A character's natural age is a game's own reading of when they were born and what the clock says now; what
    /// is kept here is only what something did to them on top of that — a creature's withering touch, a spell
    /// whose price is years — because that is the part a save must carry and a remedy can undo. How old the
    /// character therefore is, and what age does to them, is the game's answer.
    /// </para>
    /// <para>
    /// Unlike the rest of this component, which only progression moves, these years are written by whatever the
    /// game says ages a character or gives the years back, through <see cref="Age"/> and <see cref="Rejuvenate"/>.
    /// </para>
    /// </remarks>
    public int AgeOffset { get; private set; }

    /// <summary>Makes the character older than their natural age by a number of years, never past a stated ceiling.</summary>
    /// <param name="years">How many years are added, which cannot be negative.</param>
    /// <param name="ceiling">The most the years added can come to, which a game may state; null when nothing caps them.</param>
    /// <returns>How many years were actually added.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The years or the ceiling are negative.</exception>
    public int Age(int years, int? ceiling = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(years);
        if (ceiling is { } cap) ArgumentOutOfRangeException.ThrowIfNegative(cap);
        long aged = (long)AgeOffset + years;
        int next = (int)Math.Min(ceiling is { } most ? Math.Max(most, AgeOffset) : int.MaxValue, aged);
        int added = next - AgeOffset;
        AgeOffset = next;
        Stamp = ChangeStamp.Next();
        return added;
    }

    /// <summary>Gives back every year the character was aged beyond their natural age.</summary>
    /// <returns>How many years were given back.</returns>
    public int Rejuvenate()
    {
        int years = AgeOffset;
        AgeOffset = 0;
        Stamp = ChangeStamp.Next();
        return years;
    }

    /// <summary>Awards experience. Whether it is enough to advance a level is the ruleset's decision, made after this.</summary>
    /// <param name="amount">How much experience to award, which cannot be negative.</param>
    /// <exception cref="ArgumentOutOfRangeException">The amount is negative.</exception>
    /// <exception cref="OverflowException">The total would leave the numbers experience is described in.</exception>
    internal void AwardExperience(long amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        Experience = checked(Experience + amount);
        Stamp = ChangeStamp.Next();
    }

    /// <summary>Records the level the ruleset advanced the character to.</summary>
    /// <param name="level">The new level, which is at least one.</param>
    /// <exception cref="ArgumentOutOfRangeException">The level is below one.</exception>
    internal void SetLevel(int level)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(level);
        Level = level;
        Stamp = ChangeStamp.Next();
    }

    /// <summary>Grants skill points, which a level-up or a reward does.</summary>
    /// <param name="points">How many points to grant, which cannot be negative.</param>
    /// <exception cref="ArgumentOutOfRangeException">The points are negative.</exception>
    /// <exception cref="OverflowException">The pool would leave the numbers it is described in.</exception>
    internal void GrantSkillPoints(int points)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(points);
        SkillPoints = checked(SkillPoints + points);
        Stamp = ChangeStamp.Next();
    }

    /// <summary>Spends skill points from the pool.</summary>
    /// <param name="points">How many points to spend, which cannot be negative.</param>
    /// <returns>Whether the pool held enough; a refused spend leaves the pool untouched.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The points are negative.</exception>
    internal bool SpendSkillPoints(int points)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(points);
        if (points > SkillPoints) return false;
        SkillPoints -= points;
        Stamp = ChangeStamp.Next();
        return true;
    }

    /// <summary>Records the rank the ruleset promoted the character to.</summary>
    /// <param name="rank">The new rank in the class ladder, which is at least one.</param>
    /// <exception cref="ArgumentOutOfRangeException">The rank is below one.</exception>
    internal void SetClassRank(int rank)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rank);
        ClassRank = rank;
        Stamp = ChangeStamp.Next();
    }
}
