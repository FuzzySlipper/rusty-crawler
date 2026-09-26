using System.Text.Json.Serialization;

namespace PartyRpg.Kit.Combat;

/// <summary>What kind of harm a hit does, as content names it.</summary>
/// <remarks>
/// <para>
/// Content identity: which kinds of harm exist, what resists each of them, and what a kind means to a
/// creature are the ruleset's answers. The kit carries the identity and resists nothing by itself, so a game
/// whose bestiary resists fire, steel, and a school of magic says so with three of its own names rather than
/// with three the kit happened to know.
/// </para>
/// <para>
/// A kind travels with every resolved attack because both sides of it need one: the ruleset reads the
/// target's resistance <em>for that kind</em>, and the panel says what a hit was. An attack whose kind the
/// ruleset did not name is refused where the rule is composed rather than resolved as harm that nothing
/// resists.
/// </para>
/// </remarks>
public readonly record struct DamageKindId
{
    /// <summary>Creates a damage-kind reference.</summary>
    /// <param name="value">The kind's id, which must not be blank.</param>
    /// <exception cref="ArgumentException">The id is blank, which names no kind.</exception>
    /// <remarks>
    /// A save does not carry one — a fight is not saved — but the value is created through this constructor
    /// everywhere else, which is what keeps a blank id from resolving an attack nothing can resist.
    /// </remarks>
    [JsonConstructor]
    public DamageKindId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>The kind's id, as this game's own vocabulary spells it.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}
