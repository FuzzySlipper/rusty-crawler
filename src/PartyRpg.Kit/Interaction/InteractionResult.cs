using PartyRpg.Kit.Party;

namespace PartyRpg.Kit.Interaction;

/// <summary>What one use of one target did, or why it did nothing, as a report a panel can show.</summary>
/// <remarks>
/// <para>
/// A result is the workflow's own answer and always says what was used and what came of it, whether the use
/// happened: a refusal leaves the target exactly as it was, so a caller can read the result unconditionally
/// and never has to guess the target's state from the branch it took.
/// </para>
/// <para>
/// A refusal is never silence. It carries the code a caller branches on — the requirement the party does not
/// meet, the charge it cannot pay, the event nothing executes — and the sentence a person reads, which is
/// what makes a locked door say what it needs rather than doing nothing.
/// </para>
/// </remarks>
public sealed record InteractionResult
{
    private InteractionResult(
        bool isApplied,
        InteractionTarget? target,
        InteractionVerb? verb,
        string state,
        string message,
        string residue,
        PartyRefusal? refusal)
    {
        IsApplied = isApplied;
        Target = target;
        Verb = verb;
        State = state;
        Message = message;
        Residue = residue;
        Refusal = refusal;
    }

    /// <summary>The use happened: the target now holds this state, and this is what it did.</summary>
    /// <param name="target">What was used, holding the state the use left it in.</param>
    /// <param name="outcome">What the ruleset's outcome stated.</param>
    /// <param name="message">What the panel reports, which is the outcome's message plus what the kit moved.</param>
    /// <exception cref="ArgumentNullException">The target or the outcome is null.</exception>
    /// <exception cref="ArgumentException">The outcome is a refusal, which cannot be reported as applied.</exception>
    public static InteractionResult Applied(InteractionTarget target, InteractionOutcome outcome, string message)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(outcome);
        if (!outcome.IsApplied)
        {
            throw new ArgumentException(
                "A refused outcome changed nothing, so it cannot be reported as a use that happened.",
                nameof(outcome));
        }

        return new InteractionResult(true, target, target.Definition.Verb, outcome.State, message, outcome.Residue, null);
    }

    /// <summary>The use did nothing, and this is why.</summary>
    /// <param name="target">What the party was using, or null when nothing was focused at all.</param>
    /// <param name="code">A short stable code naming the kind of refusal.</param>
    /// <param name="message">Why nothing happened, in terms a person can act on.</param>
    /// <exception cref="ArgumentException">The code or the message is blank.</exception>
    public static InteractionResult Refused(InteractionTarget? target, string code, string message) =>
        new(false, target, target?.Definition.Verb, target?.State.State ?? string.Empty, message, string.Empty, new PartyRefusal(code, message));

    /// <summary>Whether the use happened. A refusal left the target and the party exactly as they were.</summary>
    public bool IsApplied { get; }

    /// <summary>What was used, or null when the use was refused because nothing was focused.</summary>
    public InteractionTarget? Target { get; }

    /// <summary>Which use applied, or null when nothing was focused to have a verb.</summary>
    public InteractionVerb? Verb { get; }

    /// <summary>The target's state after the use, or what it held before a refusal.</summary>
    public string State { get; }

    /// <summary>What the use did, in the words a person reads.</summary>
    public string Message { get; }

    /// <summary>What the use could not deliver, or empty when it delivered all of it.</summary>
    public string Residue { get; }

    /// <summary>The refusal, or null when the use happened.</summary>
    public PartyRefusal? Refusal { get; }

    /// <summary>The refusal's code, or empty when the use happened.</summary>
    public string Code => Refusal?.Code ?? string.Empty;

    /// <summary>What a person calls the target, or empty when nothing was focused.</summary>
    public string TargetName => Target?.Definition.Name ?? string.Empty;

    /// <inheritdoc />
    public override string ToString() =>
        IsApplied ? $"{TargetName} {Verb}: {Message}" : $"{TargetName} refused ({Code}): {Message}";
}
