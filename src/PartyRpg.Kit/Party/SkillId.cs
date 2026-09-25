namespace PartyRpg.Kit.Party;

/// <summary>Which skill definition a character has learned, as content names it.</summary>
/// <remarks>
/// Content identity: the ceiling a class and rank impose on a skill, the cost of raising it, and what each
/// mastery tier unlocks are ruleset policy over content definitions. The kit stores a level and a tier
/// against this reference and knows none of those names.
/// </remarks>
public readonly record struct SkillId
{
    /// <summary>Creates a skill reference.</summary>
    /// <param name="value">The skill definition's id, which must not be blank.</param>
    /// <exception cref="ArgumentException">The id is blank, which names no definition.</exception>
    public SkillId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>The skill definition's id in content.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}
