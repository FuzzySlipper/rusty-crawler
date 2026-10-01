namespace PartyRpg.Kit.Conversation;

/// <summary>The owners a conversation hands the party to: the only ones a session routes.</summary>
/// <remarks>
/// A closed list rather than content's word, because every handoff is carried out by an owner the session
/// composes, and a word no owner takes would be an offer a player hears and nothing could honour. A game adds
/// a kind of offer by adding an owner and its entry here, which is what makes the router exhaustive.
/// </remarks>
public enum HandoffOwner
{
    /// <summary>The counter whoever the party spoke with keeps, which the service mechanism serves.</summary>
    Counter,

    /// <summary>A rank the person is empowered to grant, which the progression owner moves.</summary>
    Rank,

    /// <summary>Hearing an errand the person states, which the quest owner records as offered.</summary>
    ErrandOffer,

    /// <summary>Agreeing to an errand already heard, which the quest owner records as taken.</summary>
    ErrandAccept,

    /// <summary>Handing a finished errand back to its giver, which the quest owner pays.</summary>
    ErrandTurnIn,

    /// <summary>
    /// Something the topic sets going that the ruleset runs as one use of the speaker's placement
    /// (<see cref="Interaction.PartyInteraction.Answer"/>), whose words are the person's answer.
    /// </summary>
    Use,
}

/// <summary>What taking a topic hands the party over to, and which one of that owner's things it is.</summary>
/// <remarks>
/// <para>
/// A person who keeps a counter does not sell anything themselves: they offer to step up to the counter, and
/// the counter's own mechanism takes it from there. That is what a handoff is — a topic naming the owner it
/// belongs to and the thing of that owner's it means — so the conversation never grows a second copy of
/// what another mechanism already does.
/// </para>
/// <para>
/// Hearing an errand and agreeing to it are two owners' words rather than one, because they are different
/// facts: a journal that could not show an errand the party was offered and walked away from would lose half
/// of what a player was told.
/// </para>
/// </remarks>
/// <param name="Owner">The owner the handoff belongs to.</param>
/// <param name="Target">
/// Which of that owner's things it is, or empty when the owner needs nothing more than the person the
/// conversation is with.
/// </param>
public sealed record ConversationHandoff(HandoffOwner Owner, string Target = "")
{
    /// <summary>The word a projection publishes for the owner.</summary>
    public string Word => Owner switch
    {
        HandoffOwner.Counter => "service",
        HandoffOwner.Rank => "promotion",
        HandoffOwner.ErrandOffer => "quest-offer",
        HandoffOwner.ErrandAccept => "quest-accept",
        HandoffOwner.Use => "use",
        _ => "quest-turn-in",
    };

    /// <inheritdoc />
    public override string ToString() => Target.Length == 0 ? Word : $"{Word}:{Target}";
}
