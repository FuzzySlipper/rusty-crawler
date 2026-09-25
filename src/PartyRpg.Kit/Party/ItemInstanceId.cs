using System.Text.Json.Serialization;

using System.Globalization;

namespace PartyRpg.Kit.Party;

/// <summary>
/// One item instance's durable identity: what makes a specific artifact stay that artifact through a save.
/// </summary>
/// <remarks>
/// This is durable save identity and the reason item instances exist apart from item definitions. Two
/// instances of one definition are different things — one is identified, one is damaged, one carries an
/// enchantment — and a save that recorded only the definition would turn a named artifact back into an
/// anonymous copy of its kind. It is neither runtime identity nor content identity: the definition is
/// content's, and nothing about a visit names an instance.
/// </remarks>
public readonly record struct ItemInstanceId
{
    /// <summary>Creates an item instance identity.</summary>
    /// <param name="value">The identity's value, which must not be zero.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero, which names no instance.</exception>
    /// <remarks>
    /// A save reads this value back through this constructor: the metadata the product writes saves with
    /// binds a document's field to a constructor parameter, so a value created empty would silently lose it.
    /// </remarks>
    [JsonConstructor]
    public ItemInstanceId(ulong value)
    {
        if (value == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Zero names no item instance; a durable identity starts at one so an unset value is never mistaken for an item.");
        }

        Value = value;
    }

    /// <summary>The identity's value.</summary>
    public ulong Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
