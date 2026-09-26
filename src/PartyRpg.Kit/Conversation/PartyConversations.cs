using PartyRpg.Kit.Party;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Conversation;

/// <summary>One thing said while a conversation is open: who said it, what they said, and what it left out.</summary>
/// <remarks>
/// The line names the topic it answered as well as the words it used, because what has already been said in
/// a conversation is part of the conversation's own state: the ruleset is asked what is on offer with the
/// lines said so far in hand, so a person who has already answered something is not asked about it twice
/// within one conversation. Nothing here is durable — closing the conversation forgets it — which is what
/// keeps a topic a person has already answered available the next time the party speaks with them.
/// </remarks>
/// <param name="Speaker">Who said it, as the person's identity among the people present.</param>
/// <param name="Topic">The topic it answered, or empty when it was a greeting or a turn.</param>
/// <param name="Text">What was said.</param>
/// <param name="Residue">What saying it could not carry out, or empty when it carried all of it.</param>
public readonly record struct ConversationLine(string Speaker, string Topic, string Text, string Residue);

/// <summary>
/// The one conversation mechanism: the party speaks with whoever is here, is offered what the state allows,
/// chooses, and hears what the content says.
/// </summary>
/// <remarks>
/// <para>
/// <b>One mechanism serves every person.</b> A shopkeeper, a household, and a stranger standing on a road
/// are the same thing to this class: somebody the ruleset says is at a placement, with a greeting, topics,
/// and answers. There is no dialogue logic per person and no script interpreter: what a person will talk
/// about and what they say are content's, and what a topic is available from is state, judged by the
/// ruleset's own answer about each condition.
/// </para>
/// <para>
/// <b>Availability is recomputed, never remembered.</b> The topic list is the ruleset's answer read at the
/// moment it is asked for, so a topic whose conditions stopped holding since the screen was drawn is gone
/// from the list and refused if it is named anyway. Nothing invalidates anything here, because nothing is
/// cached to invalidate.
/// </para>
/// <para>
/// <b>A conversation hands off; it does not absorb.</b> A topic that offers something names the owner it
/// belongs to, and this mechanism reports that handoff rather than carrying it out: the session that holds
/// both mechanisms routes it, which is what keeps a shop's shelves, its hours, and its prices in one place
/// instead of a second copy behind a shopkeeper's line.
/// </para>
/// <para>
/// <b>The party is the only thing that carries anything.</b> What an answer records — that the party has met
/// somebody, that a line has been heard — is kept as a party-wide effect through the party's own owner, so
/// content can gate a later topic on it and a save carries it without this mechanism owning a store of its
/// own. With no party to carry it, the record is reported as not kept rather than silently dropped.
/// </para>
/// <para>
/// A conversation is open or it is not, and opening, choosing, turning, and leaving are all explicit. It
/// holds no more than who is here, who is speaking, what has been said while it is open, and the last
/// answer, so two sessions that say the same things hear the same conversation.
/// </para>
/// </remarks>
public sealed class PartyConversations
{
    /// <summary>
    /// How many lines of the conversation are kept for a screen to show. A transcript is what a player reads
    /// back, not a record any owner needs, so the oldest lines fall off rather than growing without bound
    /// while somebody keeps talking.
    /// </summary>
    public const int MaxLines = 64;

    private readonly IConversationRule _rule;
    private readonly PartyEntity? _party;
    private readonly GameClock? _clock;
    private readonly List<ConversationLine> _said = [];
    private PlaceId _place;
    private PlacementDefinition? _placement;
    private ConversationSubject? _subject;
    private ConversationPerson? _speaker;
    private string _topic = string.Empty;
    private ConversationResult? _last;

    /// <summary>Creates the mechanism over the party it speaks for and the ruleset that answers for it.</summary>
    /// <param name="rule">This game's answers about people: who is here, what they say, and what they offer.</param>
    /// <param name="party">
    /// The party that is speaking, which is what an answer records on and what a condition about a member's
    /// class, race, or standing is judged against. Without one the mechanism still runs, and a condition
    /// that needs the party is unmet rather than satisfied by an invented one.
    /// </param>
    /// <param name="clock">
    /// The session's one clock, which an hour condition is judged against. Without one a condition about the
    /// time of day cannot be known to hold, and a rule that needs it says so.
    /// </param>
    /// <exception cref="ArgumentNullException">No rule was supplied.</exception>
    public PartyConversations(IConversationRule rule, PartyEntity? party = null, GameClock? clock = null)
    {
        _rule = rule ?? throw new ArgumentNullException(nameof(rule));
        _party = party;
        _clock = clock;
    }

    /// <summary>This game's answers about people.</summary>
    public IConversationRule Rule => _rule;

    /// <summary>Whether a conversation is open.</summary>
    public bool IsOpen => _subject is not null;

    /// <summary>Who the party is speaking with, or null when no conversation is open.</summary>
    public ConversationSubject? Subject => _subject;

    /// <summary>The person speaking now, or null when no conversation is open.</summary>
    public ConversationPerson? Speaker => _speaker;

    /// <summary>The place the conversation is happening in, empty when none is open.</summary>
    public PlaceId Place => _place;

    /// <summary>The placement the person stands at, or null when no conversation is open.</summary>
    public PlacementDefinition? Placement => _placement;

    /// <summary>
    /// What the person speaking now said as the party arrived, or empty when no conversation is open.
    /// </summary>
    /// <remarks>
    /// The greeting is read from the transcript rather than remembered beside it, and it is the current
    /// speaker's own: a household of three greets the party once per person, and turning from one to another
    /// shows what each of them said when they were first addressed rather than the first thing anybody said.
    /// </remarks>
    public string Greeting
    {
        get
        {
            string speaker = _speaker?.Id ?? string.Empty;
            foreach (ConversationLine line in _said)
            {
                if (line.Topic.Length == 0 && string.Equals(line.Speaker, speaker, StringComparison.Ordinal)) return line.Text;
            }

            return _said.Count > 0 ? _said[0].Text : string.Empty;
        }
    }

    /// <summary>What has been said while the conversation has been open, oldest first.</summary>
    public IReadOnlyList<ConversationLine> Said => _said;

    /// <summary>
    /// Every topic the speaker has, on offer and withheld alike, each with the verdict its conditions reach
    /// against the state as it stands at this moment. Empty when no conversation is open.
    /// </summary>
    public IReadOnlyList<ConversationOffer> Offers => _subject is null ? [] : _rule.Offers(Context());

    /// <summary>The topics that may be chosen now, in the order the ruleset states them.</summary>
    public IReadOnlyList<ConversationOffer> OnOffer => [.. Offers.Where(offer => offer.IsOnOffer)];

    /// <summary>The topics the speaker has that their conditions withhold, with the reason each is withheld.</summary>
    public IReadOnlyList<ConversationOffer> Withheld => [.. Offers.Where(offer => !offer.IsOnOffer)];

    /// <summary>The last thing said in a conversation, or null before one has been opened.</summary>
    public ConversationResult? Last => _last;

    /// <summary>
    /// Opens a conversation with whoever stands at a placement, if anybody does, and reports what was said.
    /// </summary>
    /// <remarks>
    /// This is the handoff the interaction mechanism reaches: a use that talks to somebody arrives here with
    /// the placement it reached, and whether there is anybody to speak with is the ruleset's answer about
    /// that placement. A conversation already open is replaced, because a party speaks with one person at a
    /// time.
    /// </remarks>
    /// <param name="place">The place the placement stands in.</param>
    /// <param name="placement">The placement content declared.</param>
    /// <returns>What opening it did, or null when the placement is nobody the party can speak with.</returns>
    /// <exception cref="ArgumentNullException">The placement is null.</exception>
    public ConversationResult? OpenTarget(PlaceId place, PlacementDefinition placement)
    {
        ArgumentNullException.ThrowIfNull(placement);
        if (_rule.Describe(new ConversationTargetRequest(place, placement)) is not { } subject) return null;
        return Open(place, placement, subject);
    }

    /// <summary>Opens a conversation with somebody the caller already knows is present.</summary>
    /// <param name="place">The place the person stands in.</param>
    /// <param name="placement">The placement the person stands at.</param>
    /// <param name="subject">Who is present.</param>
    /// <returns>What opening it did.</returns>
    /// <exception cref="ArgumentNullException">The placement or the subject is null.</exception>
    public ConversationResult Open(PlaceId place, PlacementDefinition placement, ConversationSubject subject)
    {
        ArgumentNullException.ThrowIfNull(placement);
        ArgumentNullException.ThrowIfNull(subject);

        _place = place;
        _placement = placement;
        _subject = subject;
        _speaker = subject.First;
        _said.Clear();
        _topic = string.Empty;
        return Greet("open");
    }

    /// <summary>Turns to another of the people present, who says their own greeting.</summary>
    /// <remarks>
    /// Turning is the conversation's own state changing rather than a topic being taken: who is speaking is
    /// part of what a conversation is, and a household of three is reached by turning to each of them rather
    /// than by a shop-like menu.
    /// </remarks>
    /// <param name="person">The identity of the person to speak with.</param>
    /// <returns>What turning did, or why it did nothing.</returns>
    public ConversationResult Turn(string person)
    {
        if (_subject is not { } subject) return NotOpen("turn");
        if (subject.Person(person) is not { } present)
        {
            return Record(ConversationResult.Refused(
                "turn",
                "conversation-person-unknown",
                $"Nobody here is '{person}'; the people present are {string.Join(", ", subject.People.Select(one => one.Name))}.",
                _speaker?.Name ?? string.Empty));
        }

        if (_speaker is { } speaking && string.Equals(speaking.Id, present.Id, StringComparison.Ordinal))
        {
            return Record(ConversationResult.Applied("turn", $"{present.Name} is already the one speaking.", present.Name, offers: OnOffer.Count));
        }

        _speaker = present;
        return Greet("turn");
    }

    /// <summary>Takes a topic the speaker offers, and reports what they said.</summary>
    /// <remarks>
    /// The list is re-read here rather than taken from whatever a screen was showing when the choice was
    /// made, so a topic whose conditions stopped holding is refused with the reason its condition states
    /// instead of being applied from a stale answer.
    /// </remarks>
    /// <param name="topic">The identity of the topic to take.</param>
    /// <returns>What taking it did, or why it did nothing.</returns>
    /// <exception cref="ArgumentException">The topic's identity is blank, which names nothing to take.</exception>
    public ConversationResult Choose(string topic)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        if (_subject is null) return NotOpen("say");

        ConversationOffer? offered = null;
        foreach (ConversationOffer offer in Offers)
        {
            if (!string.Equals(offer.Id, topic, StringComparison.Ordinal)) continue;
            offered = offer;
            break;
        }

        if (offered is not { } found)
        {
            return Record(ConversationResult.Refused(
                "say",
                "conversation-topic-unknown",
                $"{_speaker?.Name ?? "Whoever is here"} has nothing to say about '{topic}'.",
                _speaker?.Name ?? string.Empty,
                topic));
        }

        if (!found.IsOnOffer)
        {
            return Record(ConversationResult.Refused(
                "say",
                "conversation-topic-withheld",
                $"{_speaker?.Name ?? "Whoever is here"} does not bring up {found.Label} yet: {found.Availability.Reason}.",
                _speaker?.Name ?? string.Empty,
                topic));
        }

        ConversationAnswer answer = _rule.Take(found.Topic, Context());

        // Who speaks next is part of the answer because one person's line can hand the conversation to
        // another; an identity nobody present has is refused by name rather than leaving the conversation
        // pointed at somebody who is not here.
        if (answer.Speaker.Length > 0)
        {
            if (_subject.Person(answer.Speaker) is not { } next)
            {
                return Record(ConversationResult.Refused(
                    "say",
                    "conversation-speaker-unknown",
                    $"{found.Label} hands the conversation to '{answer.Speaker}', who is not among the people present.",
                    _speaker?.Name ?? string.Empty,
                    topic));
            }

            _speaker = next;
        }

        _topic = found.Topic.Id;
        string residue = Say(answer);
        _topic = string.Empty;
        return Record(ConversationResult.Applied(
            "say",
            $"{_speaker?.Name ?? "Whoever is here"}: {answer.Text}",
            _speaker?.Name ?? string.Empty,
            topic,
            answer.Text,
            residue,
            answer.Handoff,
            OnOffer.Count));
    }

    /// <summary>Ends the conversation, which is what a player walking away or closing the screen does.</summary>
    /// <returns>What leaving did, or why it did nothing.</returns>
    public ConversationResult Close()
    {
        if (_subject is not { } subject) return NotOpen("leave");
        string who = _speaker?.Name ?? subject.First.Name;
        _subject = null;
        _speaker = null;
        _placement = null;
        _place = default;
        _said.Clear();
        return Record(ConversationResult.Applied("leave", $"The party takes its leave of {who}."));
    }

    /// <summary>What a condition about this conversation's state reads as, for a report or a test.</summary>
    /// <param name="topic">The identity of the topic to ask about.</param>
    /// <returns>The verdict, or null when the speaker has no topic with that identity.</returns>
    /// <exception cref="ArgumentException">The topic's identity is blank.</exception>
    public ConversationAvailability? Availability(string topic)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        foreach (ConversationOffer offer in Offers)
        {
            if (string.Equals(offer.Id, topic, StringComparison.Ordinal)) return offer.Availability;
        }

        return null;
    }

    /// <summary>Asks the rule what the current speaker says as the party arrives, and records it.</summary>
    private ConversationResult Greet(string action)
    {
        if (_speaker is not { } speaker) return NotOpen(action);
        ConversationAnswer greeting = _rule.Greeting(Context());
        _topic = string.Empty;
        string residue = Say(greeting);
        return Record(ConversationResult.Applied(
            action,
            $"{speaker.Name}: {greeting.Text}",
            speaker.Name,
            text: greeting.Text,
            residue: residue,
            offers: OnOffer.Count));
    }

    /// <summary>
    /// Puts an answer into the transcript and onto the party, and reports what could not be kept.
    /// </summary>
    /// <remarks>
    /// The records are the party's own effects, applied through the owner the party keeps them in: that is
    /// what makes "this party has met that person" survive a save and what lets content gate a later topic
    /// on it. A world with no party cannot keep a record, and says so in the residue rather than dropping it
    /// silently.
    /// </remarks>
    private string Say(ConversationAnswer answer)
    {
        List<string> unkept = [];
        foreach (string flag in answer.Records)
        {
            if (_party is { } party)
            {
                party.Effects.Apply(new PartyEffect(new EffectId(flag), 1));
                continue;
            }

            unkept.Add(flag);
        }

        _said.Add(new ConversationLine(_speaker?.Id ?? string.Empty, _topic, answer.Text, answer.Residue));
        while (_said.Count > MaxLines) _said.RemoveAt(0);

        return unkept.Count == 0
            ? answer.Residue
            : string.Join(
                " ",
                new[] { answer.Residue, $"What was said about {string.Join(", ", unkept)} could not be kept: this world holds no party to carry it." }
                    .Where(part => part.Length > 0));
    }

    /// <summary>The context one thing said is resolved against, from the state as it stands now.</summary>
    private ConversationContext Context() =>
        new(_place, _placement!, _subject!, _speaker?.Id ?? string.Empty, _said, _party, _clock);

    private ConversationResult NotOpen(string action) =>
        Record(ConversationResult.Refused(action, "conversation-not-open", "The party is not speaking with anybody."));

    private ConversationResult Record(ConversationResult result)
    {
        _last = result;
        return result;
    }
}
