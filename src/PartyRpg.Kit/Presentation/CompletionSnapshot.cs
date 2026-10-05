using Rusty.Engine;

namespace PartyRpg.Kit.Presentation;

/// <summary>The ending the party earned, read from its durable state by the game's rule.</summary>
public sealed record CompletionSnapshot(string Id, string Title, string Text, bool Continues)
{
    /// <summary>The expedition has not reached an ending.</summary>
    public static CompletionSnapshot None { get; } = new("", "", "", true);

    /// <summary>Whether the party earned an ending.</summary>
    public bool Completed => Id.Length > 0;

    internal uint Write(UiValueBuilder builder) => builder.Object(
        ("completed", builder.Boolean(Completed)), ("id", builder.String(Id)),
        ("title", builder.String(Title)), ("text", builder.String(Text)),
        ("continues", builder.Boolean(Continues)));
}
