using PartyRpg.Kit.Conversation;
using PartyRpg.Kit.Knowledge;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.World;

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
/// meet, the charge it cannot pay, the event the ruleset will not run — and the sentence a person reads, which is
/// what makes a locked door say what it needs rather than doing nothing.
/// </para>
/// </remarks>
public sealed record InteractionResult
{
    private InteractionResult(
        InteractionTarget? target,
        InteractionVerb? verb,
        string state,
        string message,
        string residue,
        IReadOnlyList<KnowledgeReport> learned,
        Refusal? refusal,
        ConversationSubject? speaks = null,
        InteractionTravel? travels = null,
        TransitionResult? journey = null,
        InteractionRelocation? relocates = null)
    {
        Relocates = relocates;
        Travels = travels;
        Journey = journey;
        Speaks = speaks;
        Target = target;
        Verb = verb;
        State = state;
        Message = message;
        Residue = residue;
        Learned = learned;
        Refusal = refusal;
    }

    /// <summary>Whether this result supplies player-facing feedback; it remains available to diagnostics either way.</summary>
    public bool ShowFeedback { get; init; } = true;

    /// <summary>The use happened: the target now holds this state, and this is what it did.</summary>
    /// <param name="target">What was used, holding the state the use left it in, or null for a word without a world placement.</param>
    /// <param name="outcome">What the ruleset's outcome stated.</param>
    /// <param name="message">What the panel reports, which is the outcome's message plus what the kit moved.</param>
    /// <exception cref="ArgumentNullException">The outcome is null.</exception>
    /// <exception cref="ArgumentException">The outcome is a refusal, which cannot be reported as applied.</exception>
    public static InteractionResult Applied(InteractionTarget? target, InteractionOutcome outcome, string message)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        if (!outcome.IsApplied)
        {
            throw new ArgumentException(
                "A refused outcome changed nothing, so it cannot be reported as a use that happened.",
                nameof(outcome));
        }

        return new InteractionResult(target, target?.Definition.Verb ?? InteractionVerb.Talk, outcome.State, message, outcome.Residue, outcome.Learned, null, outcome.Speaks, outcome.Travels, null, outcome.Relocates)
        {
            KeptOut = outcome.KeptOut,
            ShowFeedback = outcome.ShowFeedback,
        };
    }

    /// <summary>The use did nothing, and this is why.</summary>
    /// <param name="target">What the party was using, or null when nothing was focused at all.</param>
    /// <param name="refusal">Why nothing happened.</param>
    /// <exception cref="ArgumentNullException">No refusal was given.</exception>
    public static InteractionResult Refused(InteractionTarget? target, Refusal refusal)
    {
        ArgumentNullException.ThrowIfNull(refusal);
        return new(target, target?.Definition.Verb, target?.State.State ?? string.Empty, refusal.Message, string.Empty, [], refusal);
    }

    /// <summary>Whether the use happened. A refusal left the target and the party exactly as they were.</summary>
    public bool IsApplied => Refusal is null;

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

    /// <summary>
    /// What the use taught the party, or empty when it taught nothing. A refusal teaches nothing: what a
    /// use the party was not allowed to make would have taught was never learned.
    /// </summary>
    /// <remarks>
    /// The facts travel from the ruleset's own outcome so the caller that holds the knowledge owner can
    /// hand them over without reading the outcome's other parts: whether a fact is news is the knowledge
    /// owner's answer, and this result reports them all.
    /// </remarks>
    public IReadOnlyList<KnowledgeReport> Learned { get; }

    /// <summary>The refusal, or null when the use happened.</summary>
    public Refusal? Refusal { get; }

    /// <summary>Whom the use handed the party to speak with, or null when it handed it to nobody.</summary>
    public ConversationSubject? Speaks { get; }

    /// <summary>The journey the use takes the party on, or null when it takes none.</summary>
    public InteractionTravel? Travels { get; }

    /// <summary>Whether the use reached somebody's door and the party was kept outside (<see cref="InteractionOutcome.KeptOut"/>).</summary>
    public bool KeptOut { get; init; }

    /// <summary>Where in its own place the use sets the party down, or null when it moves it nowhere.</summary>
    public InteractionRelocation? Relocates { get; }

    /// <summary>
    /// What came of the journey once the world took it — the arrival, or the transition path's refusal — or null
    /// while it has not been taken or when the use takes none.
    /// </summary>
    public TransitionResult? Journey { get; }

    /// <summary>
    /// This use with the journey it led to: the arrival is stated in the message, and a journey the transition path
    /// refused is stated as residue, because the use itself happened and what it settled stays settled.
    /// </summary>
    /// <param name="journey">What the transition path made of the use's journey.</param>
    /// <param name="arrival">How the arrival reads, naming the place the party reached.</param>
    /// <returns>The result, with the journey.</returns>
    /// <exception cref="ArgumentNullException">The journey is null.</exception>
    /// <exception cref="InvalidOperationException">The use was refused or took no journey.</exception>
    public InteractionResult Travelled(TransitionResult journey, string arrival)
    {
        ArgumentNullException.ThrowIfNull(journey);
        if (!IsApplied || Travels is null)
        {
            throw new InvalidOperationException("Only a use that happened and leads somewhere can be said to have travelled.");
        }

        string message = journey.Arrived ? $"{Message} {arrival}" : Message;
        string residue = journey.Arrived
            ? Residue
            : string.Join(" ", new[] { Residue, $"The way on was refused: {journey.Refusal?.Message}" }.Where(part => part.Length > 0));
        return new InteractionResult(Target, Verb, State, message, residue, Learned, null, Speaks, Travels, journey, Relocates)
        {
            KeptOut = KeptOut,
            ShowFeedback = ShowFeedback,
        };
    }

    /// <summary>The refusal's code, or empty when the use happened.</summary>
    public string Code => Refusal?.Code ?? string.Empty;

    /// <summary>What a person calls the target, or empty when nothing was focused.</summary>
    public string TargetName => Target?.Definition.Name ?? string.Empty;

    /// <inheritdoc />
    public override string ToString() =>
        IsApplied ? $"{TargetName} {Verb}: {Message}" : $"{TargetName} refused ({Code}): {Message}";
}
