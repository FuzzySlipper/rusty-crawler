namespace PartyRpg.Kit.Services;

/// <summary>The sort of service a counter is, as data: a weapon shop, a temple, a guild, a tavern.</summary>
/// <remarks>
/// <para>
/// A kind is content's word rather than a type, exactly as an interaction target's kind is. Every kind is
/// served by the one service mechanism, so a game that adds a kind adds content — a definition naming the
/// kind, the operations it offers, its shelves, its lessons, its hours, and its access requirement — and
/// never a class here. The kinds a game has are its own: this product's shipped content names twenty-one
/// of them, and nothing in the kit enumerates them.
/// </para>
/// <para>
/// The kind is what a projection, a report, and a person name a service by, and it is the word policy is
/// keyed on when a game's rules differ per kind — a guild teaches and gates its shelves, a shop trades —
/// without any of that becoming a branch in the mechanism.
/// </para>
/// </remarks>
public readonly record struct ServiceKind
{
    /// <summary>Creates a service kind.</summary>
    /// <param name="value">The kind's name, which must not be blank.</param>
    /// <exception cref="ArgumentException">The name is blank, which names no kind.</exception>
    public ServiceKind(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>The kind's name, as content and the ruleset state it.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}
