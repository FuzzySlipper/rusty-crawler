namespace PartyRpg.Kit.Conversation;

/// <summary>What somebody says, and what saying it changes.</summary>
/// <remarks>
/// <para>
/// The ruleset answers with this and the kit applies it, which is the split the whole mechanism rests on:
/// a rule says what a person says about the castle, what a keeper offers, and what an answer leaves
/// behind; the kit records what the answer carries on the party, turns the conversation to whoever now
/// speaks, and hands off to the owner the answer names.
/// </para>
/// <para>
/// <b>The residue is what an answer could not carry out.</b> A line the shipped data states is a line: the
/// errand the original hangs behind it — an item changing hands, a quest bit being set — belongs to owners
/// this build does not have, and an answer that said nothing about that would look like one that did it.
/// Where a rule cannot deliver what a line implies, it says so here and the panel shows it beside the line.
/// </para>
/// </remarks>
public sealed record ConversationAnswer
{
    /// <summary>Creates an answer.</summary>
    /// <param name="text">What the person says, which must not be blank.</param>
    /// <param name="residue">What the answer could not carry out, or empty when it carried all of it.</param>
    /// <param name="records">The party-carried flags the answer records, or empty when it records none.</param>
    /// <param name="handoff">What the answer hands the party to, or null when it hands to nobody.</param>
    /// <param name="speaker">Who speaks afterwards, or empty to keep the speaker the conversation has.</param>
    /// <exception cref="ArgumentException">The text is blank, so nobody would say anything.</exception>
    public ConversationAnswer(
        string text,
        string residue = "",
        IReadOnlyList<string>? records = null,
        ConversationHandoff? handoff = null,
        string speaker = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        Text = text;
        Residue = residue;
        Records = records ?? [];
        Handoff = handoff;
        Speaker = speaker;
    }

    /// <summary>What the person says.</summary>
    public string Text { get; }

    /// <summary>What the answer could not carry out, or empty when it carried all of it.</summary>
    public string Residue { get; }

    /// <summary>The party-carried flags the answer records, as the identities the party holds them under.</summary>
    public IReadOnlyList<string> Records { get; }

    /// <summary>What the answer hands the party to, or null when it hands to nobody.</summary>
    public ConversationHandoff? Handoff { get; }

    /// <summary>Who speaks afterwards, or empty to keep the speaker the conversation has.</summary>
    public string Speaker { get; }

    /// <inheritdoc />
    public override string ToString() => Text;
}
