namespace PartyRpg.Kit.Presentation;

/// <summary>
/// The answer to the party's latest act, whichever owner gave it: what was done, by whom, to what, and how it came out.
/// </summary>
/// <remarks>
/// <para>
/// Every owner keeps its own last result, and a screen that showed whichever of them it happened to look at first would
/// leave an old answer standing as if it were the answer to what the player just did: a sign read before an attack would
/// still be the line under the reticle after it. This is the one answer that is the latest, numbered so a screen can tell
/// a new answer from the same one republished, and naming the act and its subject so it reads as the answer to that act.
/// </para>
/// <para>
/// It answers the party's own acts. A creature's blow is the fight's news, which the fight's block carries.
/// </para>
/// </remarks>
/// <param name="Serial">How many answers the session has given; zero before the first.</param>
/// <param name="Source">Which act it answers, as the wire spells it (<see cref="FeedbackSources"/>); empty before the first.</param>
/// <param name="Actor">Who acted, empty when the party acted as one.</param>
/// <param name="Subject">What the act was aimed at or concerned, empty when it named nothing.</param>
/// <param name="Outcome"><c>applied</c> or <c>refused</c>; <c>none</c> before the first.</param>
/// <param name="Code">The refusal's code, empty when the act was applied.</param>
/// <param name="Message">The owner's own sentence.</param>
public sealed record FeedbackSnapshot(long Serial, string Source, string Actor, string Subject, string Outcome, string Code, string Message)
{
    /// <summary>The session has answered nothing yet.</summary>
    public static FeedbackSnapshot None { get; } = new(0, string.Empty, string.Empty, string.Empty, "none", string.Empty, string.Empty);

    /// <summary>Writes the latest answer.</summary>
    /// <param name="builder">The projection being built.</param>
    /// <returns>The block's node.</returns>
    internal uint Write(UiValueBuilder builder) =>
        builder.Object(
            ("serial", builder.Number(Serial)),
            ("source", builder.String(Source)),
            ("actor", builder.String(Actor)),
            ("subject", builder.String(Subject)),
            ("outcome", builder.String(Outcome)),
            ("code", builder.String(Code)),
            ("message", builder.String(Message)));
}

/// <summary>The acts a <see cref="FeedbackSnapshot"/> answers, as the wire spells them.</summary>
public static class FeedbackSources
{
    /// <summary>A use of what the party faces.</summary>
    public const string Use = "use";

    /// <summary>A member's attack.</summary>
    public const string Attack = "attack";

    /// <summary>A casting.</summary>
    public const string Cast = "cast";

    /// <summary>A rest, a camp or a wait.</summary>
    public const string Stop = "stop";

    /// <summary>A save.</summary>
    public const string Save = "save";

    /// <summary>A transaction at a counter.</summary>
    public const string Counter = "counter";

    /// <summary>A word in a conversation, or leaving it.</summary>
    public const string Conversation = "conversation";

    /// <summary>Using an item from the pack.</summary>
    public const string Item = "item";

    /// <summary>A mixture.</summary>
    public const string Mix = "mix";

    /// <summary>Putting on or taking off an item.</summary>
    public const string Equip = "equip";
}
