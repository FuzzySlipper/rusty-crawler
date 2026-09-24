namespace PartyRpg.Kit.World;

/// <summary>A named reason a transition did not happen, with a message a caller can show.</summary>
/// <remarks>
/// The code is the part a caller branches on — whether to offer another route, to say the party is too
/// poor, or to treat the answer as a defect — and the message is the part a person reads. Keeping both
/// means "the party cannot pay" is never mistaken for "there is no such road", and never disappears into
/// a silent no-op.
/// </remarks>
public sealed record TravelRefusal
{
    /// <summary>Creates a refusal.</summary>
    /// <param name="code">A short stable code naming the kind of refusal.</param>
    /// <param name="message">Why the transition was refused, in terms a person can act on.</param>
    /// <exception cref="ArgumentException">The code or the message is blank.</exception>
    public TravelRefusal(string code, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Code = code;
        Message = message;
    }

    /// <summary>A short stable code for the kind of refusal.</summary>
    public string Code { get; }

    /// <summary>Why the transition was refused.</summary>
    public string Message { get; }

    /// <inheritdoc />
    public override string ToString() => $"{Code}: {Message}";
}
