using PartyRpg.Kit.Party;
using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Time;

/// <summary>What one stop did, or why the party did not take it, as a report a panel can show.</summary>
/// <remarks>
/// <para>
/// A result always says what was asked for and what came of it, whether anything happened: a refusal leaves
/// the clock, the larder, and the party exactly as they were, so a caller can read the result unconditionally
/// and never has to infer what happened from the branch it took.
/// </para>
/// <para>
/// <b>What a night cost is part of the answer.</b> The clock's own before and after, the period that actually
/// passed, what the larder was charged and what it covered, and whether a completed sleep restored the party
/// are all here, because "the party rested" and "the party rested and it cost two portions" are different
/// facts and a panel that showed only the first would hide the price.
/// </para>
/// <para>
/// An interrupted sleep is reported as what it was: a shorter period, nothing restored, nothing spent, and
/// the sentence naming what broke it. Its outcome is still applied — the time passed — which is why the
/// interruption is a fact beside the result rather than a refusal.
/// </para>
/// </remarks>
public sealed record RestResult
{
    private RestResult(
        RestKind kind,
        bool isApplied,
        string code,
        string message,
        GameDate from,
        GameDate to,
        GameDuration elapsed,
        Provisions charge,
        int covered,
        bool interrupted,
        bool recovered,
        int restored,
        IReadOnlyList<ConditionId> cleared,
        ActiveCondition? shortage)
    {
        Kind = kind;
        IsApplied = isApplied;
        Code = code;
        Message = message;
        From = from;
        To = to;
        Elapsed = elapsed;
        Charge = charge;
        Covered = covered;
        Interrupted = interrupted;
        Recovered = recovered;
        Restored = restored;
        Cleared = cleared;
        Shortage = shortage;
    }

    /// <summary>The period happened: this is where the clock went and what it did to the party.</summary>
    /// <param name="kind">What the party asked for.</param>
    /// <param name="message">What happened, in the words a person reads.</param>
    /// <param name="advance">The advance of the one clock the period produced, which says where it went.</param>
    /// <param name="charge">What the period cost the larder, or nothing when it cost nothing.</param>
    /// <param name="covered">How much of that charge the larder covered.</param>
    /// <param name="interrupted">What broke the period, or null when nothing did.</param>
    /// <param name="recovered">Whether the period restored the party.</param>
    /// <param name="restored">How many members a completed sleep restored.</param>
    /// <param name="cleared">The conditions a completed sleep ended.</param>
    /// <param name="shortage">The state the larder's own rule left on the party, or null when it left none.</param>
    /// <returns>The result.</returns>
    /// <exception cref="ArgumentNullException">The advance is null.</exception>
    /// <exception cref="ArgumentException">The message is blank.</exception>
    public static RestResult Applied(
        RestKind kind,
        string message,
        ClockAdvance advance,
        Provisions charge = default,
        int covered = 0,
        RestInterruption? interrupted = null,
        bool recovered = false,
        int restored = 0,
        IReadOnlyList<ConditionId>? cleared = null,
        ActiveCondition? shortage = null)
    {
        ArgumentNullException.ThrowIfNull(advance);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new RestResult(
            kind,
            isApplied: true,
            string.Empty,
            message,
            advance.From,
            advance.To,
            advance.Elapsed,
            charge,
            covered,
            interrupted is not null,
            recovered,
            restored,
            cleared ?? [],
            shortage);
    }

    /// <summary>Nothing happened, and this is why: the clock, the larder, and the party are untouched.</summary>
    /// <param name="kind">What the party asked for.</param>
    /// <param name="at">Where the clock still stands.</param>
    /// <param name="code">A short stable code naming the kind of refusal.</param>
    /// <param name="message">Why nothing happened, in terms a person can act on.</param>
    /// <returns>The result.</returns>
    /// <exception cref="ArgumentException">The code or the message is blank.</exception>
    public static RestResult Refused(RestKind kind, GameDate at, string code, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new RestResult(
            kind,
            isApplied: false,
            code,
            message,
            at,
            at,
            GameDuration.None,
            Provisions.None,
            0,
            interrupted: false,
            recovered: false,
            restored: 0,
            [],
            shortage: null);
    }

    /// <summary>What the party asked for.</summary>
    public RestKind Kind { get; }

    /// <summary>Whether the period happened. A refusal moved nothing at all.</summary>
    public bool IsApplied { get; }

    /// <summary>The refusal's code, or empty when the period happened.</summary>
    public string Code { get; }

    /// <summary>What happened, in the words a person reads.</summary>
    public string Message { get; }

    /// <summary>Where the clock stood before the period, which is where it still stands after a refusal.</summary>
    public GameDate From { get; }

    /// <summary>Where the clock stands now.</summary>
    public GameDate To { get; }

    /// <summary>How much game time the period covered, which is the part that actually passed.</summary>
    public GameDuration Elapsed { get; }

    /// <summary>What the period took from the larder, or nothing when it took nothing.</summary>
    public Provisions Charge { get; }

    /// <summary>How much of what was asked the larder covered, which is nothing on a broken night.</summary>
    public int Covered { get; }

    /// <summary>Whether something broke the period.</summary>
    public bool Interrupted { get; }

    /// <summary>Whether the period restored the party.</summary>
    public bool Recovered { get; }

    /// <summary>How many members were restored, which is zero for a wait and for a broken sleep.</summary>
    public int Restored { get; }

    /// <summary>The conditions a completed sleep ended, in the order they were cleared.</summary>
    public IReadOnlyList<ConditionId> Cleared { get; }

    /// <summary>The state the larder's own rule left on the party, or null when it left none.</summary>
    public ActiveCondition? Shortage { get; }

    /// <inheritdoc />
    public override string ToString() => IsApplied
        ? $"{Kind}: {Message}"
        : $"{Kind} refused ({Code}): {Message}";
}
