namespace PartyRpg.Kit.Time;

/// <summary>What a party asks the clock for when it stops: a sleep, or a wait.</summary>
/// <remarks>
/// <para>
/// The kinds are the mechanism's own vocabulary, not a game's: what each one costs, whether it may happen
/// here, and what it restores are the ruleset's answers, and this only names the shapes a party can ask
/// for. They are distinct because they are distinct acts — a sleep recovers and pays the debt of going
/// without sleep, while a wait passes time and restores nothing — and a mechanism that treated them as one
/// would let a party wait its wounds away.
/// </para>
/// <para>
/// <b>A sleep means the same thing wherever it happens.</b> Resting under a roof and camping in the open are
/// both sleep, so both recover and both settle the day's provisions through the party's one ledger; what
/// differs is policy the ruleset states — what the ground costs, whether the party will lie down there at
/// all, and what may break the night. A wait is the other shape: it moves the one clock and nothing else.
/// </para>
/// </remarks>
public enum RestKind
{
    /// <summary>Sleep where the party stands under a roof: time passes, and the party recovers.</summary>
    Rest,

    /// <summary>Sleep in the open: time passes, the ground's own cost is paid, and the night may be broken.</summary>
    Camp,

    /// <summary>Stay awake until the next dawn: time passes, and nothing is restored.</summary>
    WaitUntilDawn,

    /// <summary>Stay awake for an hour: time passes, and nothing is restored.</summary>
    WaitAnHour,

    /// <summary>Stay awake for a short interval: time passes, and nothing is restored.</summary>
    WaitFiveMinutes,
}

/// <summary>What each kind of stop means to the mechanism, as one answer rather than a test at every use.</summary>
public static class RestKinds
{
    /// <summary>Whether a kind is a sleep: it recovers the party and pays the debt of going without sleep.</summary>
    /// <remarks>
    /// Sleeping is what restores a party and what resets the fatigue deadline, so this one answer decides
    /// both. It is stated here rather than at the call sites so a kind added later cannot recover in one
    /// place and count as wakefulness in another.
    /// </remarks>
    /// <param name="kind">The kind of stop.</param>
    public static bool Sleeps(RestKind kind) => kind is RestKind.Rest or RestKind.Camp;
}
