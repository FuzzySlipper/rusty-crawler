namespace PartyRpg.Kit.Party;

/// <summary>Which effect is acting on the party, as content names it.</summary>
/// <remarks>
/// Content identity: what an effect changes is the ruleset's reading of its definitions, and how long it
/// lasts is a game-time deadline the clock owns. The kit holds the effect and its magnitude as state, which
/// is what later systems credit and debit.
/// </remarks>
public readonly record struct EffectId
{
    /// <summary>Creates an effect reference.</summary>
    /// <param name="value">The effect definition's id, which must not be blank.</param>
    /// <exception cref="ArgumentException">The id is blank, which names no definition.</exception>
    public EffectId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>The effect definition's id in content.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}
