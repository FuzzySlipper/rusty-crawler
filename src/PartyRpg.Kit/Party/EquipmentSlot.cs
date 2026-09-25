using System.Text.Json.Serialization;

namespace PartyRpg.Kit.Party;

/// <summary>One place on a character's equipped figure that an item can occupy, as its game names it.</summary>
/// <remarks>
/// The kit enumerates no slots: how many exist, what they are called, and which of them take more than one
/// item belong to whoever owns a game's paper doll, so a slot arrives here as a name and is never a number
/// this layer assigns. A save records the name it was written under, which makes a slot the game no longer
/// has something restoration reports rather than a silent relocation.
/// </remarks>
public readonly record struct EquipmentSlot
{
    /// <summary>Creates a slot identity.</summary>
    /// <param name="value">The slot's name, which must not be blank.</param>
    /// <exception cref="ArgumentException">The name is blank, which names no place on a figure.</exception>
    /// <remarks>
    /// A save reads this value back through this constructor: the metadata the product writes saves with
    /// binds a document's field to a constructor parameter, so a value created empty would silently lose it.
    /// </remarks>
    [JsonConstructor]
    public EquipmentSlot(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>The slot's name.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}
