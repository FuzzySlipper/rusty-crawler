namespace PartyRpg.Kit.Conversation;

/// <summary>One thing a person can be asked about, and what makes it available.</summary>
/// <remarks>
/// <para>
/// A topic is content's identity and content's word for itself — what a person calls it in a list of
/// things to bring up — plus the conditions its availability is read from. What the person then <em>says</em>
/// is not here: it is the ruleset's answer when the topic is taken, because the same topic can say
/// different things depending on what the party has already done.
/// </para>
/// <para>
/// Conditions are stated but never evaluated here. The mechanism asks the ruleset what each one means, so
/// a topic that waits for a party-carried flag, a standing, a class, a race, an hour, or a finished errand
/// is one mechanism over one vocabulary rather than six special cases.
/// </para>
/// </remarks>
/// <param name="Id">The topic's identity, which the screen names when it is chosen; it must not be blank.</param>
/// <param name="Label">How the topic reads in the list a person is shown; it must not be blank.</param>
/// <param name="Conditions">What must hold for the topic to be on offer; empty when nothing must.</param>
public sealed record ConversationTopic
{
    /// <summary>Creates a topic.</summary>
    /// <param name="id">The topic's identity, which the screen names when it is chosen.</param>
    /// <param name="label">How the topic reads in the list a person is shown.</param>
    /// <param name="conditions">What must hold for the topic to be on offer.</param>
    /// <exception cref="ArgumentException">The identity or the label is blank, which names nothing to choose.</exception>
    public ConversationTopic(string id, string label, IReadOnlyList<ConversationCondition>? conditions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        Id = id;
        Label = label;
        Conditions = conditions ?? [];
    }

    /// <summary>The topic's identity, which the screen names when it is chosen.</summary>
    public string Id { get; }

    /// <summary>How the topic reads in the list a person is shown.</summary>
    public string Label { get; }

    /// <summary>What must hold for the topic to be on offer.</summary>
    public IReadOnlyList<ConversationCondition> Conditions { get; }

    /// <inheritdoc />
    public override string ToString() => Label;
}

/// <summary>One topic of a speaker's list, with what its conditions make of it right now.</summary>
/// <param name="Topic">The topic.</param>
/// <param name="Availability">Whether every condition holds, and why not when one does not.</param>
public readonly record struct ConversationOffer(ConversationTopic Topic, ConversationAvailability Availability)
{
    /// <summary>The topic's identity.</summary>
    public string Id => Topic.Id;

    /// <summary>How the topic reads in the list a person is shown.</summary>
    public string Label => Topic.Label;

    /// <summary>Whether the topic may be chosen.</summary>
    public bool IsOnOffer => Availability.IsOnOffer;

    /// <inheritdoc />
    public override string ToString() => IsOnOffer ? Label : $"{Label} ({Availability.Reason})";
}
