namespace PartyRpg.Kit.Party;

/// <summary>Which enchantment an item carries, as content names it.</summary>
/// <remarks>
/// Content identity: what an enchantment does to damage, resistance, or anything else is the ruleset's
/// reading of its definitions. The kit records that an instance carries one, with a magnitude to scale it,
/// so that identifying and enchanting are item state rather than a shop's side effect.
/// </remarks>
public readonly record struct EnchantmentId
{
    /// <summary>Creates an enchantment reference.</summary>
    /// <param name="value">The enchantment definition's id, which must not be blank.</param>
    /// <exception cref="ArgumentException">The id is blank, which names no definition.</exception>
    public EnchantmentId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>The enchantment definition's id in content.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}
