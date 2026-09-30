namespace PartyRpg.Kit;

/// <summary>A named reason something the party asked for did not happen, with a message a caller can show.</summary>
/// <remarks>
/// This is the kit's one refusal: every mechanism that can say no — travel, a service, a spell, a blow, a
/// rest, a use, a conversation, a quest, a promotion, the ledger — answers with one of these, and a result
/// that can be refused carries one or carries nothing. The code is what a caller and a test branch on and
/// the message is what a person reads, so "the rule refused this" never collapses into a silent no-op and
/// nothing has to match an English sentence to know which refusal it was. Each mechanism states its codes
/// as constants beside itself.
/// </remarks>
public sealed record Refusal
{
    /// <summary>Creates a refusal.</summary>
    /// <param name="code">A short stable code naming the kind of refusal.</param>
    /// <param name="message">Why the change was refused, in terms a person can act on.</param>
    /// <exception cref="ArgumentException">The code or the message is blank.</exception>
    public Refusal(string code, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Code = code;
        Message = message;
    }

    /// <summary>A short stable code for the kind of refusal.</summary>
    public string Code { get; }

    /// <summary>Why the change was refused.</summary>
    public string Message { get; }

    /// <inheritdoc />
    public override string ToString() => $"{Code}: {Message}";
}
