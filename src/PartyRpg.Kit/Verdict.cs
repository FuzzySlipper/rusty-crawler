namespace PartyRpg.Kit;

/// <summary>Whether one stated requirement holds for the party now, and the sentence that says why not.</summary>
/// <remarks>
/// A requirement — what a use asks for, what a topic is offered on, what a rank asks of a character — is the
/// game's to state and the ruleset's to judge, and every mechanism reads the answer the same way: met, or
/// unmet with an explanation a person can act on. It is a reading rather than a refusal, because a verdict is
/// asked of many requirements at once and a mechanism decides which unmet one it refuses by.
/// </remarks>
public readonly record struct Verdict
{
    private Verdict(bool isMet, string explanation)
    {
        IsMet = isMet;
        Explanation = explanation;
    }

    /// <summary>The requirement holds.</summary>
    public static Verdict Met { get; } = new(true, string.Empty);

    /// <summary>The requirement does not hold, and this is why.</summary>
    /// <param name="explanation">Why not, in terms a person can act on.</param>
    /// <returns>The verdict.</returns>
    /// <exception cref="ArgumentException">The explanation is blank, so the verdict would say nothing.</exception>
    public static Verdict Unmet(string explanation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(explanation);
        return new Verdict(false, explanation);
    }

    /// <summary>Whether the requirement holds.</summary>
    public bool IsMet { get; }

    /// <summary>Why the requirement does not hold, or empty when it does.</summary>
    public string Explanation { get; }

    /// <inheritdoc />
    public override string ToString() => IsMet ? "met" : $"unmet: {Explanation}";
}
