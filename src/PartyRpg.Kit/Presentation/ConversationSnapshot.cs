using PartyRpg.Kit.Conversation;

namespace PartyRpg.Kit.Presentation;

/// <summary>One person present in a conversation, as the panel shows them.</summary>
/// <param name="Id">The person's identity, which a turn command names.</param>
/// <param name="Name">What the person is called.</param>
/// <param name="Portrait">The portrait content gives them, empty when it gives none.</param>
/// <param name="Speaking">Whether this is the person speaking now.</param>
public readonly record struct ConversationPersonSnapshot(string Id, string Name, string Portrait, bool Speaking);

/// <summary>One topic a speaker has, as the panel shows it.</summary>
/// <param name="Id">The topic's identity, which a topic command names.</param>
/// <param name="Label">How the topic reads in the list.</param>
/// <param name="Available">Whether the topic may be chosen now.</param>
/// <param name="Reason">Why it may not, empty when it may.</param>
public readonly record struct ConversationTopicSnapshot(string Id, string Label, bool Available, string Reason);

/// <summary>One thing said while the conversation has been open, as the panel shows it.</summary>
/// <param name="Speaker">Who said it.</param>
/// <param name="Text">What was said.</param>
/// <param name="Residue">What saying it could not carry out, empty when it carried all of it.</param>
public readonly record struct ConversationLineSnapshot(string Speaker, string Text, string Residue);

/// <summary>What the party is saying, and to whom, as the panel needs it.</summary>
/// <remarks>
/// <para>
/// These are the mechanism's own facts copied into one presentation value, never a second opinion about
/// them: who is here, who is speaking, what they said as the party arrived, what has been said since, which
/// topics are on offer and which the state withholds with the reason, and what the last thing said did.
/// Every word here was content's or the ruleset's; the panel spells none of them.
/// </para>
/// <para>
/// A session whose ruleset answered no conversation policy has no facts at all, and <see cref="None"/> is
/// that state, so the panel says the mechanism is not there instead of showing an empty conversation that
/// looks like somebody with nothing to say. A session that holds the mechanism but is speaking with nobody
/// publishes that it is not open, which is a different fact again.
/// </para>
/// </remarks>
/// <param name="Available">Whether the session holds a conversation mechanism at all.</param>
/// <param name="Open">Whether a conversation is open.</param>
/// <param name="Subject">Who the party is speaking with, as content names the group, empty when none.</param>
/// <param name="Speaker">Who is speaking now, empty when nobody is.</param>
/// <param name="Greeting">What was said as the party arrived, empty before anybody has said anything.</param>
/// <param name="People">Everybody present, in the order they are offered.</param>
/// <param name="Topics">The topics on offer now, which are the ones a screen may offer as choices.</param>
/// <param name="Withheld">The topics the state withholds, each with the reason it is withheld.</param>
/// <param name="Said">What has been said while the conversation has been open, oldest first.</param>
/// <param name="Action">What the last thing said was: <c>open</c>, <c>say</c>, <c>turn</c>, <c>leave</c>, or empty before any.</param>
/// <param name="Outcome">What the last thing did: <c>none</c>, <c>applied</c>, or <c>refused</c>.</param>
/// <param name="Code">The last refusal's code, empty when the last thing applied or none has happened.</param>
/// <param name="Message">What the last thing reported, empty before anything has happened.</param>
/// <param name="Residue">What the last answer could not carry out, empty when it carried all of it.</param>
/// <param name="Handoff">The owner the party was last handed to, empty when it was handed to nobody.</param>
/// <param name="Topic">The topic the last answer came from, empty when none did.</param>
public readonly record struct ConversationSnapshot(
    bool Available,
    bool Open,
    string Subject,
    string Speaker,
    string Greeting,
    IReadOnlyList<ConversationPersonSnapshot> People,
    IReadOnlyList<ConversationTopicSnapshot> Topics,
    IReadOnlyList<ConversationTopicSnapshot> Withheld,
    IReadOnlyList<ConversationLineSnapshot> Said,
    string Action,
    string Outcome,
    string Code,
    string Message,
    string Residue,
    string Handoff,
    string Topic)
{
    /// <summary>No conversation mechanism: there is nobody to speak with and nothing to say.</summary>
    public static ConversationSnapshot None => new(
        Available: false,
        Open: false,
        Subject: string.Empty,
        Speaker: string.Empty,
        Greeting: string.Empty,
        People: [],
        Topics: [],
        Withheld: [],
        Said: [],
        Action: string.Empty,
        Outcome: "none",
        Code: string.Empty,
        Message: string.Empty,
        Residue: string.Empty,
        Handoff: string.Empty,
        Topic: string.Empty);

    /// <summary>Reads the conversation facts out of the session's mechanism.</summary>
    /// <param name="conversations">The session's conversation mechanism, or null when it holds none.</param>
    /// <returns>The facts the panel shows, or <see cref="None"/> when there is no mechanism.</returns>
    public static ConversationSnapshot From(PartyConversations? conversations)
    {
        if (conversations is null) return None;

        List<ConversationPersonSnapshot> people = [];
        if (conversations.Subject is { } subject)
        {
            string speaking = conversations.Speaker?.Id ?? string.Empty;
            foreach (ConversationPerson person in subject.People)
            {
                people.Add(new ConversationPersonSnapshot(
                    person.Id,
                    person.Name,
                    person.Portrait,
                    string.Equals(person.Id, speaking, StringComparison.Ordinal)));
            }
        }

        List<ConversationTopicSnapshot> topics = [];
        foreach (ConversationOffer offer in conversations.OnOffer)
        {
            topics.Add(new ConversationTopicSnapshot(offer.Id, offer.Label, true, string.Empty));
        }

        List<ConversationTopicSnapshot> withheld = [];
        foreach (ConversationOffer offer in conversations.Withheld)
        {
            withheld.Add(new ConversationTopicSnapshot(offer.Id, offer.Label, false, offer.Availability.Reason));
        }

        List<ConversationLineSnapshot> said = [];
        foreach (ConversationLine line in conversations.Said)
        {
            said.Add(new ConversationLineSnapshot(line.Speaker, line.Text, line.Residue));
        }

        ConversationResult? last = conversations.Last;
        return new ConversationSnapshot(
            Available: true,
            Open: conversations.IsOpen,
            Subject: conversations.Subject?.Id ?? string.Empty,
            Speaker: conversations.Speaker?.Name ?? string.Empty,
            Greeting: conversations.Greeting,
            People: people,
            Topics: topics,
            Withheld: withheld,
            Said: said,
            Action: last?.Action ?? string.Empty,
            Outcome: last is null ? "none" : last.IsApplied ? "applied" : "refused",
            Code: last?.Code ?? string.Empty,
            Message: last?.Message ?? string.Empty,
            Residue: last?.Residue ?? string.Empty,
            Handoff: last?.Handoff?.ToString() ?? string.Empty,
            Topic: last?.Topic ?? string.Empty);
    }
}
