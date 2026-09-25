using System.Text.Json.Serialization;

using System.Globalization;

namespace PartyRpg.Kit.Party;

/// <summary>
/// A member's durable identity: the name every reference inside the party uses and a save round-trips.
/// </summary>
/// <remarks>
/// This is durable save identity. It is not runtime identity — the engine's <c>EntityId</c> for a member
/// belongs to the store holding the party, differs after every load, and is never written down — and it
/// is not content identity, because a character is created rather than defined, so no catalog names one.
/// Equipment, followers, custody, and effects all name a person by this value precisely so that a save
/// can be read without the store that wrote it.
/// </remarks>
public readonly record struct PartyMemberId
{
    /// <summary>Creates a member identity.</summary>
    /// <param name="value">The identity's value, which must not be zero.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero, which names no member.</exception>
    /// <remarks>
    /// A save reads this value back through this constructor: the metadata the product writes saves with
    /// binds a document's field to a constructor parameter, so a value created empty would silently lose it.
    /// </remarks>
    [JsonConstructor]
    public PartyMemberId(ulong value)
    {
        if (value == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Zero names no member; a durable identity starts at one so that an unset value is never mistaken for a person.");
        }

        Value = value;
    }

    /// <summary>The identity's value.</summary>
    public ulong Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
