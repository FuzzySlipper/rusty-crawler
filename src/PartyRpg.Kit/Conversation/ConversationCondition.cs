namespace PartyRpg.Kit.Conversation;

/// <summary>What kind of state a topic's availability can be read from, as the workflow's vocabulary.</summary>
/// <remarks>
/// <para>
/// The kinds are the shapes a topic's availability can take, and each one is answered by the ruleset rather
/// than by the kit: what a flag means, how much standing is enough, which class or race a member is, which
/// hours are day, and what an errand is are all this game's policy, while the shape — a named thing, and how
/// much of it — is the workflow's.
/// </para>
/// <para>
/// Every kind here reads state the party or the clock actually carries: a party-carried flag, the party's
/// standing, a member's class or race, the hour, and an errand the party has finished. A kind nobody can
/// answer would be a vocabulary of guesses, so the list holds nothing this build cannot judge.
/// </para>
/// </remarks>
public enum ConversationConditionKind
{
    /// <summary>Something the party carries as a party-wide effect, named by the flag's own identity.</summary>
    Flag,

    /// <summary>The party's standing in the world, which must be at least the stated amount.</summary>
    Reputation,

    /// <summary>A class somebody in the party holds, named by the class's identity.</summary>
    Class,

    /// <summary>A race somebody in the party belongs to, named by the race's identity.</summary>
    Race,

    /// <summary>The part of the day the conversation must fall in, named <c>day</c> or <c>night</c>.</summary>
    Hour,

    /// <summary>An errand the party must have finished, named by the errand's own identity.</summary>
    Errand,
}

/// <summary>One thing a topic's availability is judged against: a named thing, and how much of it.</summary>
/// <remarks>
/// <para>
/// Conditions come from content and the ruleset and are judged in the order they are stated, so the first
/// one that does not hold is the one a withheld topic names. The kit owns this vocabulary and the order the
/// checks happen in; the ruleset owns what each condition means for its game, which is why judging one is
/// that ruleset's answer and never a comparison here.
/// </para>
/// <para>
/// The label exists so a withheld topic can say what it is waiting for in the words a person reads, while
/// the name stays the identity the ruleset resolves — an effect, a standing, a class, a race, a part of the
/// day, or an errand.
/// </para>
/// </remarks>
public sealed record ConversationCondition
{
    /// <summary>Creates a condition.</summary>
    /// <param name="kind">What kind of state the condition is about.</param>
    /// <param name="name">The identity the ruleset resolves it by, which must not be blank.</param>
    /// <param name="amount">How much of it the party must have, at least one; a skill's level and a standing both read it.</param>
    /// <param name="label">
    /// What a person reads for the condition, or empty to read the name itself. A label is presentation only
    /// and is never what the condition is resolved by.
    /// </param>
    /// <exception cref="ArgumentException">The name is blank, which names nothing a rule could resolve.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The amount is below one, which would require nothing.</exception>
    public ConversationCondition(ConversationConditionKind kind, string name, int amount = 1, string label = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfLessThan(amount, 1);
        Kind = kind;
        Name = name;
        Amount = amount;
        Label = string.IsNullOrWhiteSpace(label) ? name : label;
    }

    /// <summary>What kind of state the condition is about.</summary>
    public ConversationConditionKind Kind { get; }

    /// <summary>The identity the ruleset resolves the condition by.</summary>
    public string Name { get; }

    /// <summary>How much of the named thing the party must have, or the standing it must reach.</summary>
    public int Amount { get; }

    /// <summary>What a person reads for the condition.</summary>
    public string Label { get; }

    /// <summary>How the condition reads in a list or in the reason a topic is withheld.</summary>
    /// <remarks>
    /// A standing is a threshold rather than a count of things, so it reads as one; every other kind's
    /// amount is a count of the named thing, which only a flag ever needs to say out loud.
    /// </remarks>
    public string Describe() => Kind switch
    {
        ConversationConditionKind.Reputation => $"{Label} {Amount}",
        _ => Amount <= 1 ? Label : $"{Amount} × {Label}",
    };

    /// <inheritdoc />
    public override string ToString() => $"{Kind}:{Name}";
}
