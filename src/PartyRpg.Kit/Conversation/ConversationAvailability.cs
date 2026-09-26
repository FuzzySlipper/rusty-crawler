namespace PartyRpg.Kit.Conversation;

/// <summary>What one topic's conditions make of it at the moment they are read.</summary>
/// <remarks>
/// Availability is computed rather than remembered: a topic whose conditions held when the conversation
/// opened and stopped holding since is withheld the next time the list is read, with the reason the first
/// unmet condition states. That is what makes the same content offer different topics to a party that has
/// done more, and it is why nothing invalidates a list here — there is no list to invalidate.
/// </remarks>
public sealed record ConversationAvailability
{
    private ConversationAvailability(bool isOnOffer, string reason)
    {
        IsOnOffer = isOnOffer;
        Reason = reason;
    }

    /// <summary>Every condition holds, so the topic is on offer.</summary>
    public static ConversationAvailability OnOffer { get; } = new(true, string.Empty);

    /// <summary>A condition does not hold, so the topic is withheld and this is why.</summary>
    /// <param name="reason">Why the topic is not on offer, in terms a person can act on.</param>
    /// <returns>The verdict.</returns>
    /// <exception cref="ArgumentException">The reason is blank, so a withheld topic would say nothing.</exception>
    public static ConversationAvailability Withheld(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return new ConversationAvailability(false, reason);
    }

    /// <summary>Whether every condition holds.</summary>
    public bool IsOnOffer { get; }

    /// <summary>Why the topic is withheld, or empty when it is on offer.</summary>
    public string Reason { get; }

    /// <inheritdoc />
    public override string ToString() => IsOnOffer ? "on offer" : $"withheld: {Reason}";
}
