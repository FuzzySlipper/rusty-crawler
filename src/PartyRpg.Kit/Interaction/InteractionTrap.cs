namespace PartyRpg.Kit.Interaction;

/// <summary>One number a use has to beat, and the number the party brings to it.</summary>
/// <remarks>
/// <para>
/// A challenge is deliberately two numbers rather than a verdict: what the party knows and what the thing
/// demands are both this game's, so the ruleset states them and the workflow compares them. The comparison
/// is the kit's because it is the same comparison for every game — a party that brings at least what
/// something demands gets past it — while what counts towards the party's number is policy the kit cannot
/// guess: the best level in the party, one member's, a sum, or an item's bonus.
/// </para>
/// <para>
/// The ruleset states the difficulty it wants compared, including any multiplier its game applies (a
/// donor that doubles a map's disarm difficulty before testing a character's skill states the doubled
/// number), so no scaling is hidden here.
/// </para>
/// </remarks>
public sealed record InteractionChallenge
{
    /// <summary>Creates a challenge.</summary>
    /// <param name="label">
    /// What the challenge is called where a person reads it — a skill, a sense, a member's name. It is
    /// presentation only and never what the challenge is resolved by.
    /// </param>
    /// <param name="attempt">What the party brings to the challenge.</param>
    /// <param name="difficulty">What the challenge demands.</param>
    /// <exception cref="ArgumentException">The label is blank, which names nothing a person could be shown.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Either number is negative, which is not a number a check can compare.</exception>
    public InteractionChallenge(string label, int attempt, int difficulty)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentOutOfRangeException.ThrowIfNegative(attempt);
        ArgumentOutOfRangeException.ThrowIfNegative(difficulty);
        Label = label;
        Attempt = attempt;
        Difficulty = difficulty;
    }

    /// <summary>What the challenge is called where a person reads it.</summary>
    public string Label { get; init; }

    /// <summary>What the party brings to the challenge.</summary>
    public int Attempt { get; init; }

    /// <summary>What the challenge demands.</summary>
    public int Difficulty { get; init; }

    /// <summary>
    /// Whether the party gets past it. What the party brings must reach what the challenge demands, which is
    /// the shape every such test in this game family takes: a value either meets the difficulty it is
    /// checked against or it does not.
    /// </summary>
    public bool Succeeds => Attempt >= Difficulty;

    /// <summary>How the challenge reads in a report: what was brought, and what was demanded.</summary>
    public string Describe() => $"{Label} {Attempt} against {Difficulty}";

    /// <inheritdoc />
    public override string ToString() => Describe();
}

/// <summary>What a sprung trap does to the party.</summary>
/// <remarks>
/// The amount is per member because a party is a band and a trap that catches one catches the ones
/// standing with it: the original damages every character within reach of the container, and this
/// product's party shares one pose, so the whole band is in reach when the container is used. The ruleset
/// states the amount; the kit is what takes it off the members' own resources, which is what makes a
/// consequence something a player can be shown rather than a sentence in a report.
/// </remarks>
public sealed record InteractionHarm
{
    /// <summary>Creates the harm a trap does.</summary>
    /// <param name="perMember">How much each member loses, which cannot be negative.</param>
    /// <param name="label">What a person calls the harm — the game's own word for it, such as damage or a condition's name.</param>
    /// <exception cref="ArgumentOutOfRangeException">The amount is negative, which would heal rather than harm.</exception>
    /// <exception cref="ArgumentException">The label is blank, which names nothing a person could be shown.</exception>
    public InteractionHarm(int perMember, string label = "damage")
    {
        ArgumentOutOfRangeException.ThrowIfNegative(perMember);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        PerMember = perMember;
        Label = label;
    }

    /// <summary>How much each member loses.</summary>
    public int PerMember { get; init; }

    /// <summary>What a person calls the harm.</summary>
    public string Label { get; init; }

    /// <summary>Whether the harm takes nothing, which is what a trap whose consequence is not a loss states.</summary>
    public bool IsNone => PerMember == 0;

    /// <inheritdoc />
    public override string ToString() => $"{PerMember} {Label} each";
}

/// <summary>What a use has to get past before it reaches what a target holds: a trap, and its answers.</summary>
/// <remarks>
/// <para>
/// <b>The kit owns the workflow and the ruleset owns every word and number in it.</b> The trap names
/// itself, states what noticing it takes and what defeating it takes, what it does when it goes off, and
/// the three words a target reads as once it has been noticed, defeated, or sprung. The kit decides the
/// order — an unnoticed trap is noticed before it can be defeated, a defeated one is done, and a sprung
/// one has already spent itself — and applies the harm through the party's own members.
/// </para>
/// <para>
/// <b>Whether the party already knows.</b> A trap the party has noticed is offered as the act that
/// defeats it rather than as the act that finds it, which is why the ruleset states
/// <see cref="IsKnown"/>: only the ruleset knows what its own state words mean, and this is where it says
/// which of them means "they have seen it".
/// </para>
/// <para>
/// <b>A trap is not a lock.</b> What a target requires to be used at all — a key, a skill, a flag, a part
/// of the day — is judged first, in the same vocabulary doors use; a trap is what happens to a party that
/// gets that far. The two are separate answers about one target, which is why a chest can be both locked
/// and trapped without either being a special case.
/// </para>
/// <para>
/// A trap's members are settable through a <c>with</c>, like a target definition's: a ruleset that answers
/// a trap from content can adjust what it answered — the two attempts are read from the party, which
/// content knows nothing about — without a second type for the same answer.
/// </para>
/// </remarks>
public sealed record InteractionTrap
{
    /// <summary>Creates a trap.</summary>
    /// <param name="name">What the trap is called where a person reads it.</param>
    /// <param name="detect">What noticing the trap takes, judged when the party has not noticed it yet.</param>
    /// <param name="disarm">What defeating the trap takes, judged once the party knows it is there.</param>
    /// <param name="harm">What the trap does to the party when the check against it fails.</param>
    /// <param name="isKnown">Whether the party has already noticed this trap, as the ruleset's own state says.</param>
    /// <param name="noticedState">The word the target reads as once the party has noticed the trap.</param>
    /// <param name="disarmedState">The word the target reads as once the trap has been defeated.</param>
    /// <param name="sprungState">The word the target reads as once the trap has gone off.</param>
    /// <exception cref="ArgumentException">A name or a state word is blank, which states nothing a person could be shown.</exception>
    /// <exception cref="ArgumentNullException">A challenge or the harm is missing, which leaves the trap unable to answer.</exception>
    public InteractionTrap(
        string name,
        InteractionChallenge detect,
        InteractionChallenge disarm,
        InteractionHarm harm,
        bool isKnown,
        string noticedState,
        string disarmedState,
        string sprungState)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(detect);
        ArgumentNullException.ThrowIfNull(disarm);
        ArgumentNullException.ThrowIfNull(harm);
        ArgumentException.ThrowIfNullOrWhiteSpace(noticedState);
        ArgumentException.ThrowIfNullOrWhiteSpace(disarmedState);
        ArgumentException.ThrowIfNullOrWhiteSpace(sprungState);
        Name = name;
        Detect = detect;
        Disarm = disarm;
        Harm = harm;
        IsKnown = isKnown;
        NoticedState = noticedState;
        DisarmedState = disarmedState;
        SprungState = sprungState;
    }

    /// <summary>What the trap is called where a person reads it.</summary>
    public string Name { get; init; }

    /// <summary>What noticing the trap takes, judged when the party has not noticed it yet.</summary>
    public InteractionChallenge Detect { get; init; }

    /// <summary>What defeating the trap takes, judged once the party knows it is there.</summary>
    public InteractionChallenge Disarm { get; init; }

    /// <summary>What the trap does to the party when the check against it fails.</summary>
    public InteractionHarm Harm { get; init; }

    /// <summary>Whether the party has already noticed this trap, as the ruleset's own state says.</summary>
    public bool IsKnown { get; init; }

    /// <summary>The word the target reads as once the party has noticed the trap.</summary>
    public string NoticedState { get; init; }

    /// <summary>The word the target reads as once the trap has been defeated.</summary>
    public string DisarmedState { get; init; }

    /// <summary>The word the target reads as once the trap has gone off.</summary>
    public string SprungState { get; init; }

    /// <summary>The check the party's next use of the target is judged by: defeating it when it is known, noticing it when it is not.</summary>
    public InteractionChallenge Next => IsKnown ? Disarm : Detect;

    /// <summary>The word the target reads as once the party gets past this check.</summary>
    public string PassedState => IsKnown ? DisarmedState : NoticedState;
}
