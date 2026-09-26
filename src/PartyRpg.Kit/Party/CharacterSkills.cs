namespace PartyRpg.Kit.Party;

/// <summary>The skills one character has learned, with the level and tier of each.</summary>
/// <remarks>
/// <para>
/// The ceiling a class and rank impose, the cost of the next level, and what a tier unlocks are all the
/// ruleset's answers; this holds what the character actually has. A skill that is known at all carries at
/// least one level, because a character who has bought a skill and gained nothing by it is not a state the
/// game has.
/// </para>
/// <para>
/// Spending skill points is one operation, not two calls a caller could leave half done: see
/// <see cref="Progression.PartyProgression.RaiseSkill"/>, which charges the progression pool and records the
/// raise together.
/// </para>
/// </remarks>
public sealed class CharacterSkills
{
    private readonly List<SkillEntry> _entries;

    /// <summary>Creates a character's skills.</summary>
    /// <param name="entries">The skills and their levels, in the order the character learned them.</param>
    /// <exception cref="ArgumentException">A skill is declared twice, or a learned skill has no level.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An entry records negative points spent.</exception>
    public CharacterSkills(IEnumerable<SkillEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _entries = [];
        HashSet<SkillId> seen = [];
        foreach (SkillEntry entry in entries)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(entry.PointsSpent);
            if (entry.Level < 1)
            {
                throw new ArgumentException(
                    $"Skill '{entry.Skill}' is listed with level {entry.Level}; a skill the character has learned has at least one level.",
                    nameof(entries));
            }

            if (!seen.Add(entry.Skill))
            {
                throw new ArgumentException(
                    $"Skill '{entry.Skill}' is declared more than once, so which level the character has would be ambiguous.",
                    nameof(entries));
            }

            _entries.Add(entry);
        }
    }

    /// <summary>The character's skills, in the order they were learned.</summary>
    public IReadOnlyList<SkillEntry> Entries => _entries;

    /// <summary>How many skills the character has learned.</summary>
    public int Count => _entries.Count;

    /// <summary>Whether the character has learned a skill at all.</summary>
    /// <param name="skill">The skill to look for.</param>
    public bool Knows(SkillId skill) => IndexOf(skill) >= 0;

    /// <summary>How many levels of a skill the character has, or zero when the skill is unknown.</summary>
    /// <param name="skill">The skill to read.</param>
    public int LevelOf(SkillId skill)
    {
        int index = IndexOf(skill);
        return index >= 0 ? _entries[index].Level : 0;
    }

    /// <summary>How far up a skill's ladder the character has trained, or untrained when the skill is unknown.</summary>
    /// <param name="skill">The skill to read.</param>
    public SkillTier TierOf(SkillId skill)
    {
        int index = IndexOf(skill);
        return index >= 0 ? _entries[index].Tier : SkillTier.None;
    }

    /// <summary>Reads one skill's entry.</summary>
    /// <param name="skill">The skill to read.</param>
    /// <param name="entry">The entry, when the character has learned the skill.</param>
    /// <returns>Whether the character has learned that skill.</returns>
    public bool TryGet(SkillId skill, out SkillEntry entry)
    {
        int index = IndexOf(skill);
        if (index >= 0)
        {
            entry = _entries[index];
            return true;
        }

        entry = default;
        return false;
    }

    /// <summary>
    /// Records that the character has learned a skill, or moves an already-learned skill to a new tier.
    /// </summary>
    /// <remarks>
    /// Whether the character is allowed to learn it — by class, by rank, by a teacher — is the ruleset's
    /// answer before this is called. What happens here is only the bookkeeping: a newly learned skill starts
    /// at level one, and learning one already known changes its tier without disturbing its level.
    /// </remarks>
    /// <param name="skill">The skill to learn.</param>
    /// <param name="tier">The tier the character learns it at.</param>
    public void Learn(SkillId skill, SkillTier tier)
    {
        int index = IndexOf(skill);
        if (index < 0)
        {
            _entries.Add(new SkillEntry(skill, 1, tier, 0));
            return;
        }

        _entries[index] = _entries[index] with { Tier = tier };
    }

    /// <summary>Raises a learned skill's level and records the points the caller charged for it.</summary>
    /// <param name="skill">The skill to raise, which the character must already know.</param>
    /// <param name="levels">How many levels to add, which must be at least one.</param>
    /// <param name="points">How many skill points the raise cost, which cannot be negative.</param>
    /// <exception cref="InvalidOperationException">The character has not learned the skill.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The levels are below one or the points are negative.</exception>
    public void RaiseLevel(SkillId skill, int levels, int points)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(levels);
        ArgumentOutOfRangeException.ThrowIfNegative(points);
        int index = IndexOf(skill);
        if (index < 0)
        {
            throw new InvalidOperationException(
                $"The character has not learned '{skill}', so it has nothing to raise; learning comes first.");
        }

        SkillEntry entry = _entries[index];
        _entries[index] = entry with
        {
            Level = checked(entry.Level + levels),
            PointsSpent = checked(entry.PointsSpent + points),
        };
    }

    /// <summary>Moves a learned skill to a new tier without disturbing its level.</summary>
    /// <param name="skill">The skill to move, which the character must already know.</param>
    /// <param name="tier">The tier to record.</param>
    /// <exception cref="InvalidOperationException">The character has not learned the skill.</exception>
    public void SetTier(SkillId skill, SkillTier tier)
    {
        int index = IndexOf(skill);
        if (index < 0)
        {
            throw new InvalidOperationException(
                $"The character has not learned '{skill}', so it has no tier to change; learning comes first.");
        }

        _entries[index] = _entries[index] with { Tier = tier };
    }

    private int IndexOf(SkillId skill)
    {
        for (int index = 0; index < _entries.Count; index++)
        {
            if (_entries[index].Skill == skill) return index;
        }

        return -1;
    }
}
