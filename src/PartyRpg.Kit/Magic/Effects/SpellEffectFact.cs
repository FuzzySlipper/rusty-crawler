namespace PartyRpg.Kit.Magic;

/// <summary>One thing a cast left behind, as the name it answers to and the value it has.</summary>
/// <remarks>
/// <para>
/// A cast's outcome is a sentence and, beside it, the facts the sentence is about: who was healed and by how
/// much, which condition was lifted, where the party arrived, what the light ends at. The kit carries them as
/// name and value pairs rather than as a field per category, because what a spell does is a game's own
/// vocabulary and a panel that had to learn a field per effect would grow with every spell added — while a
/// list of named facts is published, shown, and compared by a test without the mechanism understanding one of
/// them.
/// </para>
/// <para>
/// <b>A fact is a reading, not a promise.</b> The value is what the state holds now — a pool's count, a
/// calendar moment, a count of places — so a panel showing facts is showing the world rather than a log of
/// intentions.
/// </para>
/// </remarks>
public readonly record struct SpellEffectFact
{
    /// <summary>Creates a fact.</summary>
    /// <param name="name">What the fact is about, which must not be blank.</param>
    /// <param name="value">What it reads as, empty when the fact has no value to state.</param>
    /// <exception cref="ArgumentException">The name is blank, which would publish a fact nothing can name.</exception>
    public SpellEffectFact(string name, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        Value = value ?? string.Empty;
    }

    /// <summary>What the fact is about.</summary>
    public string Name { get; }

    /// <summary>What it reads as.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => $"{Name}: {Value}";
}
