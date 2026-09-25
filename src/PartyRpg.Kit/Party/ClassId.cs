namespace PartyRpg.Kit.Party;

/// <summary>Which class definition a character belongs to, as content names it.</summary>
/// <remarks>
/// Content identity: what a class permits, which skills it may learn, and how far each may grow are all
/// the ruleset's answers over its own definitions. The kit holds the reference so that a promotion can
/// replace it and a save can round-trip it, and it never grows a class table of its own.
/// </remarks>
public readonly record struct ClassId
{
    /// <summary>Creates a class reference.</summary>
    /// <param name="value">The class definition's id, which must not be blank.</param>
    /// <exception cref="ArgumentException">The id is blank, which names no definition.</exception>
    public ClassId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>The class definition's id in content.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}
