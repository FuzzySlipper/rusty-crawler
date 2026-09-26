namespace PartyRpg.Kit.Conversation;

/// <summary>Somebody standing in a place whom the party can speak with.</summary>
/// <remarks>
/// A person is content's identity and a name, never a type: the mechanism holds whoever the ruleset says is
/// there, and two people of one kind share the policy that answers for them. The identity is the half a
/// conversation's own state is addressed by when the party turns from one speaker to another.
/// </remarks>
/// <param name="Id">The person's identity among the people present, which must not be blank.</param>
/// <param name="Name">What a person calls them, which must not be blank.</param>
/// <param name="Portrait">
/// The portrait content gives them, or empty when it gives none. It is presentation: nothing in the
/// mechanism reads it, and a screen that shows one shows what content stated.
/// </param>
public sealed record ConversationPerson
{
    /// <summary>Creates a person.</summary>
    /// <param name="id">The person's identity among the people present.</param>
    /// <param name="name">What a person calls them.</param>
    /// <param name="portrait">The portrait content gives them, or empty when it gives none.</param>
    /// <exception cref="ArgumentException">The identity or the name is blank, which names nobody.</exception>
    public ConversationPerson(string id, string name, string portrait = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Id = id;
        Name = name;
        Portrait = portrait;
    }

    /// <summary>The person's identity among the people present.</summary>
    public string Id { get; }

    /// <summary>What a person calls them.</summary>
    public string Name { get; }

    /// <summary>The portrait content gives them, or empty when it gives none.</summary>
    public string Portrait { get; }

    /// <inheritdoc />
    public override string ToString() => Name;
}
