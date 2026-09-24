namespace PartyRpg.Kit.Rulesets;

/// <summary>
/// Stable identity of a compiled ruleset. The value is the ruleset's own name, never a display
/// title, so it can travel in a projection and a save without carrying presentation wording.
/// </summary>
public readonly record struct RulesetId
{
    /// <summary>Creates a ruleset identity, refusing a blank value rather than accepting an anonymous ruleset.</summary>
    public RulesetId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>The ruleset's own name.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}
