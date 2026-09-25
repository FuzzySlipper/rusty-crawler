namespace PartyRpg.Kit.Interaction;

/// <summary>The kind of thing an interaction target is, as data: a door, a chest, a lever, a sign, a person.</summary>
/// <remarks>
/// <para>
/// A kind is content's word rather than a type. The mechanism discovers targets from the place's content and
/// asks the ruleset what each one is, so a game that adds a kind adds content and a ruleset answer and never
/// a class here; two targets of one kind share the policy that answers for them.
/// </para>
/// <para>
/// The kind is what a projection, a report, and a person name a target by. It is deliberately not an enum:
/// an enum here would be the kit claiming to know the kinds a game has, and the first kind a game adds
/// would have to edit the kit to exist.
/// </para>
/// </remarks>
public readonly record struct InteractionTargetKind
{
    /// <summary>Creates a target kind.</summary>
    /// <param name="value">The kind's name, which must not be blank.</param>
    /// <exception cref="ArgumentException">The name is blank, which names no kind.</exception>
    public InteractionTargetKind(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>The kind's name, as content and the ruleset state it.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}
