using System.Text.Json.Serialization;

namespace PartyRpg.Kit.Party;

/// <summary>How far up a skill's ladder a character has trained, as a rung number.</summary>
/// <remarks>
/// A tier is a first-class value rather than a boolean per feature, because a game's ladder has several
/// rungs and each unlocks or multiplies something different. The kit owns the rung number and nothing
/// else: how many rungs a ladder has, what each is called, which class may reach which, and what each
/// unlocks are all ruleset policy over its own definitions.
/// </remarks>
public readonly record struct SkillTier
{
    /// <summary>Creates a tier value.</summary>
    /// <param name="value">The rung, where zero means untrained.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative, which is below the bottom of any ladder.</exception>
    /// <remarks>
    /// A save reads this value back through this constructor: the metadata the product writes saves with
    /// binds a document's field to a constructor parameter, so a value created empty would silently lose it.
    /// </remarks>
    [JsonConstructor]
    public SkillTier(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Value = value;
    }

    /// <summary>Untrained, which is what a skill nobody has opened reads as.</summary>
    public static SkillTier None => default;

    /// <summary>The rung's number, zero being untrained.</summary>
    public int Value { get; }

    /// <summary>Whether this is the untrained rung.</summary>
    public bool IsNone => Value == 0;

    /// <inheritdoc />
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
