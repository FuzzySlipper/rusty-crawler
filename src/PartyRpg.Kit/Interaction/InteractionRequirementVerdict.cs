namespace PartyRpg.Kit.Interaction;

/// <summary>The ruleset's answer about one requirement: met, or the reason it is not.</summary>
/// <remarks>
/// <para>
/// An unmet requirement carries the sentence a person reads, and it is the ruleset's sentence because only
/// the ruleset knows what the requirement meant: a door that needs a key says the key is not carried, and a
/// door that needs daylight says what the party must wait for. A bare "met or not" would leave the refusal
/// to be composed by whoever asked, which is how a lock ends up saying nothing.
/// </para>
/// <para>
/// The verdict is only ever asked about one requirement, and the workflow stops at the first unmet one, so a
/// use that needs two things says what it needs first rather than every lack at once: an answer a player can
/// act on is one thing at a time.
/// </para>
/// </remarks>
public readonly record struct InteractionRequirementVerdict
{
    private InteractionRequirementVerdict(bool isMet, string explanation)
    {
        IsMet = isMet;
        Explanation = explanation;
    }

    /// <summary>The party meets the requirement, so the use may go on.</summary>
    public static InteractionRequirementVerdict Satisfied { get; } = new(true, string.Empty);

    /// <summary>The party does not meet the requirement, and this is why.</summary>
    /// <param name="explanation">Why the requirement is not met, in terms a person can act on.</param>
    /// <returns>The verdict.</returns>
    /// <exception cref="ArgumentException">The explanation is blank, which states no consequence.</exception>
    public static InteractionRequirementVerdict Unsatisfied(string explanation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(explanation);
        return new InteractionRequirementVerdict(false, explanation);
    }

    /// <summary>Whether the party meets the requirement.</summary>
    public bool IsMet { get; }

    /// <summary>Why the requirement is not met, or empty when it is.</summary>
    public string Explanation { get; }
}
