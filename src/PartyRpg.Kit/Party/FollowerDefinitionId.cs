namespace PartyRpg.Kit.Party;

/// <summary>Which follower definition joined the party, as content names it.</summary>
/// <remarks>
/// Content identity: who a follower is, what they say, and what they contribute are content and ruleset
/// matters. The kit records that a person joined, which definition they came from, and whether they are
/// hired or part of the story, because that distinction decides which limit applies to them.
/// </remarks>
public readonly record struct FollowerDefinitionId
{
    /// <summary>Creates a follower definition reference.</summary>
    /// <param name="value">The follower definition's id, which must not be blank.</param>
    /// <exception cref="ArgumentException">The id is blank, which names no definition.</exception>
    public FollowerDefinitionId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>The follower definition's id in content.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}
