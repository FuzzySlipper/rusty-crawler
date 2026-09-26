namespace PartyRpg.Kit.Party;

/// <summary>What a wound leaves on a character: the condition it applies, and the one it moves past.</summary>
/// <remarks>
/// <para>
/// A game's collapse is a ladder — a character who is emptied is unconscious, and one taken deeper than
/// their own body can stand is dead — so the answer to "what does this wound leave" has two halves, and a
/// rule that could only name the stage it reached would leave a character wearing every stage below it at
/// once. Both halves are applied together by the one damage entry, which is what makes the ladder a stated
/// path rather than a set of conditions that happen to accumulate.
/// </para>
/// <para>
/// <see cref="Replaces"/> is deliberately a list rather than one identity: a game may state that death ends
/// unconsciousness and that something deeper still ends both, and the kit neither knows nor needs to know
/// which ladder its ruleset keeps.
/// </para>
/// </remarks>
public sealed record CharacterCollapse
{
    private static readonly IReadOnlyList<ConditionId> Nothing = [];

    /// <summary>States what a wound leaves.</summary>
    /// <param name="condition">The condition the wound leaves, or null when it leaves none.</param>
    /// <param name="replaces">The conditions this wound moves past, which it ends.</param>
    /// <exception cref="ArgumentNullException">The list of conditions to end is null.</exception>
    public CharacterCollapse(ActiveCondition? condition, IReadOnlyList<ConditionId>? replaces = null)
    {
        Condition = condition;
        Replaces = replaces ?? Nothing;
    }

    /// <summary>A wound that leaves the character standing, and ends nothing.</summary>
    public static CharacterCollapse None { get; } = new(condition: null);

    /// <summary>The condition the wound leaves, or null when the character is still standing.</summary>
    public ActiveCondition? Condition { get; }

    /// <summary>The conditions the wound moves past, which it ends.</summary>
    public IReadOnlyList<ConditionId> Replaces { get; }

    /// <summary>Whether this wound changes anything about the character's conditions at all.</summary>
    public bool IsEmpty => Condition is null && Replaces.Count == 0;

    /// <inheritdoc />
    public override string ToString() => (Condition, Replaces.Count) switch
    {
        (null, 0) => "standing",
        (null, _) => $"ends {string.Join(", ", Replaces)}",
        ({ } left, 0) => left.ToString(),
        ({ } left, _) => $"{left}, ending {string.Join(", ", Replaces)}",
    };
}
