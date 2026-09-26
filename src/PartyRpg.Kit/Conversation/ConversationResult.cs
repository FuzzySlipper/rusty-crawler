namespace PartyRpg.Kit.Conversation;

/// <summary>What one thing said in a conversation did, or why it did nothing, as a report a screen shows.</summary>
/// <remarks>
/// <para>
/// A result always says what was asked and what came of it, whether it happened: a refusal changes nothing,
/// so a caller can read the result unconditionally and never has to guess the conversation's state from the
/// branch it took. It carries what was said, who said it, what the answer could not carry out, and the owner
/// the party was handed to when one was.
/// </para>
/// <para>
/// A refusal is never silence. It carries the code a caller branches on — nobody is here, the conversation
/// is not open, no topic has that identity, the topic is withheld and this is why, the owner it names has no
/// routing in this build — and the sentence a person reads.
/// </para>
/// </remarks>
public sealed record ConversationResult
{
    private ConversationResult(
        bool isApplied,
        string action,
        string code,
        string message,
        string speaker,
        string topic,
        string text,
        string residue,
        ConversationHandoff? handoff,
        int offers)
    {
        IsApplied = isApplied;
        Action = action;
        Code = code;
        Message = message;
        Speaker = speaker;
        Topic = topic;
        Text = text;
        Residue = residue;
        Handoff = handoff;
        Offers = offers;
    }

    /// <summary>It happened, and this is what it did.</summary>
    /// <param name="action">The word for what happened: opened, said, turned, left.</param>
    /// <param name="message">What happened, in the words a person reads.</param>
    /// <param name="speaker">Who the conversation is with afterwards, empty when it is over.</param>
    /// <param name="topic">The topic that was taken, empty when none was.</param>
    /// <param name="text">What the person said, empty when nothing was said.</param>
    /// <param name="residue">What the answer could not carry out, empty when it carried all of it.</param>
    /// <param name="handoff">The owner the party was handed to, or null when it was handed to nobody.</param>
    /// <param name="offers">How many topics the speaker has on offer afterwards.</param>
    /// <returns>The result.</returns>
    /// <exception cref="ArgumentException">The action or the message is blank.</exception>
    public static ConversationResult Applied(
        string action,
        string message,
        string speaker = "",
        string topic = "",
        string text = "",
        string residue = "",
        ConversationHandoff? handoff = null,
        int offers = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new ConversationResult(true, action, string.Empty, message, speaker, topic, text, residue, handoff, offers);
    }

    /// <summary>It changed nothing, and this is why.</summary>
    /// <param name="action">The word for what was asked and refused.</param>
    /// <param name="code">A short stable code naming the kind of refusal.</param>
    /// <param name="message">Why nothing happened, in terms a person can act on.</param>
    /// <param name="speaker">Who the conversation is with, empty when it is not open.</param>
    /// <param name="topic">The topic that was asked for, empty when none was named.</param>
    /// <returns>The result.</returns>
    /// <exception cref="ArgumentException">The action, the code, or the message is blank.</exception>
    public static ConversationResult Refused(string action, string code, string message, string speaker = "", string topic = "") =>
        new(false, Require(action, nameof(action)), Require(code, nameof(code)), Require(message, nameof(message)), speaker, topic, string.Empty, string.Empty, null, 0);

    /// <summary>Whether it happened. A refusal changed nothing at all.</summary>
    public bool IsApplied { get; }

    /// <summary>The word for what happened, or for what was refused.</summary>
    public string Action { get; }

    /// <summary>The refusal's code, or empty when it happened.</summary>
    public string Code { get; }

    /// <summary>What happened, or why nothing did.</summary>
    public string Message { get; }

    /// <summary>Who the conversation is with afterwards, empty when it is over.</summary>
    public string Speaker { get; }

    /// <summary>The topic that was taken, empty when none was.</summary>
    public string Topic { get; }

    /// <summary>What the person said, empty when nothing was said.</summary>
    public string Text { get; }

    /// <summary>What the answer could not carry out, empty when it carried all of it.</summary>
    public string Residue { get; }

    /// <summary>The owner the party was handed to, or null when it was handed to nobody.</summary>
    public ConversationHandoff? Handoff { get; }

    /// <summary>How many topics the speaker has on offer afterwards.</summary>
    public int Offers { get; }

    /// <inheritdoc />
    public override string ToString() => IsApplied ? $"{Action}: {Message}" : $"{Action} refused ({Code}): {Message}";

    private static string Require(string value, string parameterName) =>
        !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException($"The conversation result '{parameterName}' is blank, so a refusal would say nothing.", parameterName);
}
