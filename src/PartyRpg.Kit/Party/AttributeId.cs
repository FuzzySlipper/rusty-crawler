namespace PartyRpg.Kit.Party;

/// <summary>Which attribute a score belongs to, as content or ruleset policy names it.</summary>
/// <remarks>
/// Content identity, and deliberately the only thing the kit knows about an attribute: how many a
/// character has, what they are called, what they feed, and how they grow are all outside this layer, so a
/// character's scores are a set of named values rather than a struct with fields this product would have
/// to keep in step with a game's vocabulary.
/// </remarks>
public readonly record struct AttributeId
{
    /// <summary>Creates an attribute reference.</summary>
    /// <param name="value">The attribute's id, which must not be blank.</param>
    /// <exception cref="ArgumentException">The id is blank, which names no attribute.</exception>
    public AttributeId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>The attribute's id.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}
