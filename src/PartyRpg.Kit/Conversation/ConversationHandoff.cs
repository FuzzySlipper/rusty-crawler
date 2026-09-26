namespace PartyRpg.Kit.Conversation;

/// <summary>What taking a topic hands the party over to, and which one of that owner's things it is.</summary>
/// <remarks>
/// <para>
/// A person who keeps a counter does not sell anything themselves: they offer to step up to the counter, and
/// the counter's own mechanism takes it from there. That is what a handoff is — a topic naming the owner it
/// belongs to and the thing of that owner's it means — so the conversation never grows a second copy of
/// what another mechanism already does.
/// </para>
/// <para>
/// The kind is content's word rather than a closed list, exactly as a target's kind is: this build routes
/// the one owner it has, and a handoff naming an owner nothing routes is refused by name at the moment it
/// is taken rather than pretending to have happened. When the quest owner lands, an offer that belongs to
/// it routes there by the same seam and nothing here changes.
/// </para>
/// </remarks>
/// <param name="Kind">The owner the handoff belongs to, which must not be blank.</param>
/// <param name="Target">
/// Which of that owner's things it is, or empty when the owner needs nothing more than the person the
/// conversation is with.
/// </param>
/// <exception cref="ArgumentException">The kind is blank, which names no owner.</exception>
public sealed record ConversationHandoff
{
    /// <summary>Creates a handoff.</summary>
    /// <param name="kind">The owner the handoff belongs to.</param>
    /// <param name="target">Which of that owner's things it is, or empty when the person is enough.</param>
    /// <exception cref="ArgumentException">The kind is blank, which names no owner.</exception>
    public ConversationHandoff(string kind, string target = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        Kind = kind;
        Target = target;
    }

    /// <summary>The owner the handoff belongs to.</summary>
    public string Kind { get; }

    /// <summary>Which of that owner's things it is, or empty when the person is enough.</summary>
    public string Target { get; }

    /// <inheritdoc />
    public override string ToString() => Target.Length == 0 ? Kind : $"{Kind}:{Target}";
}

/// <summary>The handoff kinds this kit routes, named as the words content and the session share.</summary>
/// <remarks>
/// Only the service mechanism is named here, because it is the only owner that exists: the counter a person
/// keeps. A kind no owner routes is refused by name where it is taken, so a later stone adds its own word
/// and its routing without this list pretending to know about it.
/// </remarks>
public static class ConversationHandoffs
{
    /// <summary>The service mechanism: the counter whoever the party spoke with keeps.</summary>
    public const string Service = "service";
}
