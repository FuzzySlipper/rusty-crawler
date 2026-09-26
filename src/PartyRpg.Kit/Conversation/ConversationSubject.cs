using PartyRpg.Kit.Party;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Conversation;

/// <summary>Who is standing where the party is talking, as the ruleset answers it.</summary>
/// <remarks>
/// <para>
/// A subject is a group rather than one person because that is what a place holds: a shop has a keeper, a
/// house has whoever lives in it, and a road has the one person standing on it. The party speaks with one of
/// them at a time and can turn to another, which is what makes "who" part of the conversation's own state
/// rather than a fact fixed when it opened.
/// </para>
/// <para>
/// The greeting is deliberately not here. What a person says when the party arrives changes as the party's
/// own history does — a stranger and an acquaintance are greeted differently by the same content — so the
/// greeting is asked for every time it is read rather than frozen into the answer that opened the
/// conversation.
/// </para>
/// </remarks>
/// <param name="Id">The subject's identity in content, which must not be blank.</param>
/// <param name="People">Everybody present, in the order they are offered; at least one.</param>
/// <exception cref="ArgumentException">The identity is blank or nobody is present.</exception>
public sealed record ConversationSubject
{
    /// <summary>Creates a subject.</summary>
    /// <param name="id">The subject's identity in content.</param>
    /// <param name="people">Everybody present, in the order they are offered.</param>
    /// <exception cref="ArgumentException">The identity is blank or nobody is present.</exception>
    public ConversationSubject(string id, IReadOnlyList<ConversationPerson> people)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(people);
        if (people.Count == 0)
        {
            throw new ArgumentException(
                "A subject with nobody present is not somebody the party can speak with.",
                nameof(people));
        }

        Id = id;
        People = people;
    }

    /// <summary>The subject's identity in content.</summary>
    public string Id { get; }

    /// <summary>Everybody present, in the order they are offered.</summary>
    public IReadOnlyList<ConversationPerson> People { get; }

    /// <summary>The person the party speaks with first when it opens the conversation.</summary>
    public ConversationPerson First => People[0];

    /// <summary>One of the people present by identity, or null when nobody there has it.</summary>
    /// <param name="person">The identity to look for.</param>
    public ConversationPerson? Person(string person)
    {
        foreach (ConversationPerson present in People)
        {
            if (string.Equals(present.Id, person, StringComparison.Ordinal)) return present;
        }

        return null;
    }

    /// <inheritdoc />
    public override string ToString() => $"{Id} ({People.Count} present)";
}

/// <summary>What a placement is asked about before a conversation opens.</summary>
/// <remarks>
/// The placement is handed over whole, with its entry, exactly as the interaction mechanism hands it over:
/// the fields a person's own content carries — which row of a table, which record of a map — are read from
/// the entry rather than copied into a vocabulary here.
/// </remarks>
/// <param name="Place">The place the placement stands in.</param>
/// <param name="Placement">The placement content declares.</param>
public readonly record struct ConversationTargetRequest(PlaceId Place, PlacementDefinition Placement);

/// <summary>What one thing said in a conversation is resolved against.</summary>
/// <remarks>
/// <para>
/// The party and the clock are handed over whole rather than as answers to questions the kit thought a rule
/// would ask: what a flag means, how much standing is enough, which member's class or race counts, what the
/// hour is, and what an errand is are the ruleset's, so a rule reads the owners it needs instead of the kit
/// guessing at one shape for every game.
/// </para>
/// <para>
/// Both are optional because a world can exist without either: a place with no scenario party is still a
/// place somebody stands in, and a session whose ruleset composed no clock has no hour of the day. A
/// condition that needs one of them is then unmet, named by the rule that could not answer it, rather than
/// satisfied by an invented fact.
/// </para>
/// </remarks>
/// <param name="Place">The place the conversation is happening in.</param>
/// <param name="Placement">The placement the person stands at.</param>
/// <param name="Subject">Who is present.</param>
/// <param name="Speaker">The identity of whoever is speaking now, which is one of the subject's people.</param>
/// <param name="Said">
/// What has been said so far in this conversation, oldest first, which is how a rule knows what the person
/// has already answered without the mechanism deciding what that means.
/// </param>
/// <param name="Party">The party that is speaking, or null when the world holds none.</param>
/// <param name="Clock">The session's one clock, or null when its ruleset composed none.</param>
public sealed record ConversationContext(
    PlaceId Place,
    PlacementDefinition Placement,
    ConversationSubject Subject,
    string Speaker,
    IReadOnlyList<ConversationLine> Said,
    PartyEntity? Party,
    GameClock? Clock);
