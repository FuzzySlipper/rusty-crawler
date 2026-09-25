using System.Text.Json.Serialization;

namespace PartyRpg.Kit.Party;

/// <summary>One condition acting on one character, with how severe it is.</summary>
/// <remarks>
/// Severity is a number the game scales by: what counts as weak, what counts as severe, and what each
/// severity does are the ruleset's policy over its condition definitions. A character holds at most one
/// entry per condition, so applying one that is already present replaces its severity rather than stacking
/// a second case of the same affliction.
/// </remarks>
public readonly record struct ActiveCondition
{
    /// <summary>Creates an active condition.</summary>
    /// <param name="condition">Which condition definition is acting.</param>
    /// <param name="severity">How severe it is; zero states that it is present without a severity.</param>
    /// <exception cref="ArgumentOutOfRangeException">The severity is negative.</exception>
    /// <remarks>
    /// A save reads this value back through this constructor: the metadata the product writes saves with
    /// binds a document's field to a constructor parameter, so a value created empty would silently lose it.
    /// </remarks>
    [JsonConstructor]
    public ActiveCondition(ConditionId condition, int severity = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(severity);
        Condition = condition;
        Severity = severity;
    }

    /// <summary>Which condition definition is acting.</summary>
    public ConditionId Condition { get; }

    /// <summary>How severe the condition is.</summary>
    public int Severity { get; }

    /// <inheritdoc />
    public override string ToString() => Severity == 0 ? Condition.ToString() : $"{Condition} ({Severity})";
}
