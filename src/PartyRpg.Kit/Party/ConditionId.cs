using System.Text.Json.Serialization;

namespace PartyRpg.Kit.Party;

/// <summary>Which condition a character is suffering, as content names it.</summary>
/// <remarks>
/// Content identity: what a condition does, how severe it may become, what cures it, and whether it ends
/// with rest are the ruleset's answers. The kit holds the condition and its severity as state and names
/// none of them.
/// </remarks>
public readonly record struct ConditionId
{
    /// <summary>Creates a condition reference.</summary>
    /// <param name="value">The condition definition's id, which must not be blank.</param>
    /// <exception cref="ArgumentException">The id is blank, which names no definition.</exception>
    /// <remarks>
    /// A save reads this value back through this constructor: the metadata the product writes saves with
    /// binds a document's field to a constructor parameter, so a value created empty would silently lose it.
    /// </remarks>
    [JsonConstructor]
    public ConditionId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>The condition definition's id in content.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}
