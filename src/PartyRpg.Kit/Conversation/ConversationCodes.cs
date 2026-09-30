namespace PartyRpg.Kit.Conversation;

/// <summary>The codes a conversation refuses what was said with.</summary>
/// <remarks>
/// A code is what a caller and a test branch on and the message beside it is what a person reads, so every
/// refusal this mechanism can give is named here once rather than spelled at the point of use.
/// </remarks>
public static class ConversationCodes
{
    /// <summary>The refusal code <c>conversation-not-open</c>.</summary>
    public const string ConversationNotOpen = "conversation-not-open";

    /// <summary>The refusal code <c>conversation-person-unknown</c>.</summary>
    public const string ConversationPersonUnknown = "conversation-person-unknown";

    /// <summary>The refusal code <c>conversation-speaker-unknown</c>.</summary>
    public const string ConversationSpeakerUnknown = "conversation-speaker-unknown";

    /// <summary>The refusal code <c>conversation-topic-unknown</c>.</summary>
    public const string ConversationTopicUnknown = "conversation-topic-unknown";

    /// <summary>The refusal code <c>conversation-topic-unnamed</c>.</summary>
    public const string ConversationTopicUnnamed = "conversation-topic-unnamed";

    /// <summary>The refusal code <c>conversation-topic-withheld</c>.</summary>
    public const string ConversationTopicWithheld = "conversation-topic-withheld";
}
