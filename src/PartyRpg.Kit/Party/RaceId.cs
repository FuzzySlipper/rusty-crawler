using System.Text.Json.Serialization;

namespace PartyRpg.Kit.Party;

/// <summary>Which race definition a character belongs to, as content names it.</summary>
/// <remarks>
/// Content identity: the value is the id content wrote for one definition, and this layer resolves nothing
/// about it. What a race grants, allows, or forbids is the ruleset's answer over its own catalog, so the
/// kit can carry the reference without learning a single race. It is neither runtime identity nor durable
/// identity: a save records the reference, never the meaning behind it.
/// </remarks>
public readonly record struct RaceId
{
    /// <summary>Creates a race reference.</summary>
    /// <param name="value">The race definition's id, which must not be blank.</param>
    /// <exception cref="ArgumentException">The id is blank, which names no definition.</exception>
    /// <remarks>
    /// A save reads this value back through this constructor: the metadata the product writes saves with
    /// binds a document's field to a constructor parameter, so a value created empty would silently lose it.
    /// </remarks>
    [JsonConstructor]
    public RaceId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>The race definition's id in content.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}
