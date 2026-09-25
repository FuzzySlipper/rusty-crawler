namespace PartyRpg.Kit.Party;

/// <summary>A named reason a party-scoped change did not happen, with a message a caller can show.</summary>
/// <remarks>
/// The same split the world's travel refusal uses: the code is what a caller branches on and the message
/// is what a person reads, so "the rule refused this" never collapses into a silent no-op. Rules that gate
/// equipment and inventory answer with one of these, and so does the party when an action names an item or
/// a person the party does not hold.
/// </remarks>
public sealed record PartyRefusal
{
    /// <summary>Creates a refusal.</summary>
    /// <param name="code">A short stable code naming the kind of refusal.</param>
    /// <param name="message">Why the change was refused, in terms a person can act on.</param>
    /// <exception cref="ArgumentException">The code or the message is blank.</exception>
    public PartyRefusal(string code, string message)
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
