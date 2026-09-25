namespace PartyRpg.Kit.Party;

/// <summary>Which item definition an instance is a copy of, as content names it.</summary>
/// <remarks>
/// <para>
/// Content identity, and the half of an item that never changes: a definition says what kind of thing this
/// is — its value, its art, the skill a game demands to use it — and the ruleset reads all of that from its
/// catalog. An <see cref="ItemInstance"/> pairs this reference with the durable identity that keeps one
/// artifact distinct from another copy of the same kind.
/// </para>
/// <para>
/// Content may write an id as a number or as a string, and both mean the same definition, so a value is
/// carried exactly as written and compared as text.
/// </para>
/// </remarks>
public readonly record struct ItemDefinitionId
{
    /// <summary>Creates an item definition reference.</summary>
    /// <param name="value">The item definition's id, which must not be blank.</param>
    /// <exception cref="ArgumentException">The id is blank, which names no definition.</exception>
    public ItemDefinitionId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>The item definition's id in content.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}
