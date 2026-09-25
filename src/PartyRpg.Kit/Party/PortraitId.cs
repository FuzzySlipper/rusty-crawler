using System.Text.Json.Serialization;

namespace PartyRpg.Kit.Party;

/// <summary>Which portrait a character was created with, as the ruleset or content names it.</summary>
/// <remarks>
/// The portrait is more than a picture: in this game family the face a player picks is what decides a
/// character's race, so the reference is durable and creation's other choices hang off it. What a portrait
/// is called, which race it is drawn as, and which art it names are all outside this layer, so the kit
/// holds an identity and never a gallery.
/// </remarks>
public readonly record struct PortraitId
{
    /// <summary>Creates a portrait reference.</summary>
    /// <param name="value">The portrait's id, which must not be blank.</param>
    /// <exception cref="ArgumentException">The id is blank, which names no portrait.</exception>
    /// <remarks>
    /// A save reads this value back through this constructor: the metadata the product writes saves with
    /// binds a document's field to a constructor parameter, so a value created empty would silently lose it.
    /// </remarks>
    [JsonConstructor]
    public PortraitId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>The portrait's id.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}
