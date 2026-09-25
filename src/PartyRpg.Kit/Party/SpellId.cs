namespace PartyRpg.Kit.Party;

/// <summary>Which spell a character knows, as content names it.</summary>
/// <remarks>
/// Content identity: a school, its costs, and its effects are the ruleset's reading of its definitions.
/// The kit records that a character knows this spell and never resolves what casting it does.
/// </remarks>
public readonly record struct SpellId
{
    /// <summary>Creates a spell reference.</summary>
    /// <param name="value">The spell definition's id, which must not be blank.</param>
    /// <exception cref="ArgumentException">The id is blank, which names no definition.</exception>
    public SpellId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>The spell definition's id in content.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}
