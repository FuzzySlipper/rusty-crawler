namespace PartyRpg.Kit.Conversation;

/// <summary>
/// What this game answers about talking to somebody: who is here, what they say, what they can be asked
/// about, and what an answer does.
/// </summary>
/// <remarks>
/// <para>
/// This is the ruleset's whole contribution to the conversation mechanism, and it is deliberately four
/// answers rather than one. <see cref="Describe"/> says who is standing at a placement — a group of people
/// with identities and names, which is what makes a shopkeeper, a household, and a stranger on a road the
/// same kind of thing to this mechanism. <see cref="Greeting"/> says what is said when the party arrives,
/// which is read live because the same content greets a stranger and an acquaintance differently.
/// <see cref="Offers"/> says what the speaker can be asked about right now, each topic with what its
/// conditions make of it. <see cref="Take"/> says what taking one produces.
/// </para>
/// <para>
/// The split between <see cref="Offers"/> and <see cref="Take"/> is where availability lives: a topic is
/// only offered while its conditions hold, and taking it is a separate answer, so a topic that stops
/// holding between the projection and the choice is refused rather than applied from a stale list.
/// </para>
/// <para>
/// Nothing here is a script. A person's topics come from content's own tables — what they will talk about,
/// and what they say — and a game that wants a state machine behind a reply owns it in its own tables
/// rather than in an interpreter this mechanism would have to grow.
/// </para>
/// </remarks>
public interface IConversationRule
{
    /// <summary>
    /// Who is standing at a placement, or null when there is nobody there to speak with. A door, a chest,
    /// and a sign are not people; a counter, a household, and a person standing on a road are.
    /// </summary>
    /// <param name="request">The placement and the place it stands in.</param>
    /// <returns>Who is present, or null when the placement is nobody.</returns>
    ConversationSubject? Describe(ConversationTargetRequest request);

    /// <summary>
    /// What the person says as the party arrives: the greeting a player reads before choosing anything, and
    /// whatever saying it records about the party.
    /// </summary>
    /// <param name="context">Who is present, who is speaking, the party, and the clock.</param>
    /// <returns>What is said, and what saying it records.</returns>
    ConversationAnswer Greeting(ConversationContext context);

    /// <summary>
    /// What the speaker can be asked about right now, in the order a screen shows them, each topic with the
    /// verdict its own conditions reach against the state as it stands at this moment.
    /// </summary>
    /// <param name="context">Who is present, who is speaking, the party, and the clock.</param>
    /// <returns>Every topic the speaker has, on offer and withheld alike.</returns>
    IReadOnlyList<ConversationOffer> Offers(ConversationContext context);

    /// <summary>
    /// What taking a topic produces: what the person says, what it records on the party, and what owner it
    /// hands the party to when it offers something rather than only saying something.
    /// </summary>
    /// <param name="topic">The topic being taken.</param>
    /// <param name="context">Who is present, who is speaking, the party, and the clock.</param>
    /// <returns>The answer the kit applies and reports.</returns>
    ConversationAnswer Take(ConversationTopic topic, ConversationContext context);
}
