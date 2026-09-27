using System.Text.Json.Serialization;

namespace PartyRpg.Kit.Quests;

/// <summary>Which quest a definition describes and an instance is of, as content names it.</summary>
/// <remarks>
/// <para>
/// Content identity: the id is the key an instance records, the key a person's offer names, and the key a
/// save writes, so a quest this game states and a quest a party has taken are the same identity rather than
/// two that could drift. What the id <em>means</em> — a task from a person, a notice on a town hall's board,
/// an errand a rank asks for — is the ruleset's reading of its own content, never this type's.
/// </para>
/// <para>
/// The value is carried exactly as written and compared as text, exactly as an item or an effect identity
/// is: content may write an id as a number or a word, and both name the same quest.
/// </para>
/// </remarks>
public readonly record struct QuestId
{
    /// <summary>Creates a quest reference.</summary>
    /// <param name="value">The quest's id, which must not be blank.</param>
    /// <exception cref="ArgumentException">The id is blank, which names no quest.</exception>
    /// <remarks>
    /// A save reads this value back through this constructor: the metadata the product writes saves with
    /// binds a document's field to a constructor parameter, so a value created empty would silently lose it.
    /// </remarks>
    [JsonConstructor]
    public QuestId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>The quest's id in content.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}
