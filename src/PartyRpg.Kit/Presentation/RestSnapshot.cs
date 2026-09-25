using System.Globalization;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Time;

namespace PartyRpg.Kit.Presentation;

/// <summary>
/// What the party's last stop did and what it cost, and what going without sleep is doing to it.
/// </summary>
/// <remarks>
/// <para>
/// These are the rest mechanism's own facts copied into one presentation value, never a second opinion about
/// them: which stop was asked for, whether it happened, where the clock went, what the larder was charged and
/// what it covered, whether a night was broken, and which conditions a completed sleep cleared. A refusal
/// keeps its own code and sentence, because a stop that silently did nothing must not look like one that did.
/// </para>
/// <para>
/// The fatigue facts are read from the clock's own deadline rather than counted here: whether the state is
/// acting, when the debt next falls due, and how many times it has landed. A session with no clock owes no
/// sleep and publishes the not-known value for all of them.
/// </para>
/// <para>
/// <see cref="None"/> is what a session whose ruleset answered no rest policy publishes, so a panel says the
/// mechanism is not there rather than showing a rest that never happened.
/// </para>
/// </remarks>
/// <param name="Available">Whether the session holds a rest mechanism at all.</param>
/// <param name="Kind">What the last stop asked for, as the wire spells it; empty before the party has stopped.</param>
/// <param name="Outcome">What the last stop did: <c>none</c>, <c>applied</c>, or <c>refused</c>.</param>
/// <param name="Code">The last refusal's code, empty when the last stop happened or none has.</param>
/// <param name="Message">What the last stop reported, empty before the party has stopped.</param>
/// <param name="From">Where the clock stood when the stop was asked for, empty when there is no clock.</param>
/// <param name="To">Where the clock stands now, empty when there is no clock.</param>
/// <param name="ElapsedSeconds">How much game time the stop covered, which is zero for a refusal.</param>
/// <param name="Charged">How many provisions the stop cost, zero when it cost none.</param>
/// <param name="Covered">How many of those the larder covered.</param>
/// <param name="Unit">The unit the provisions are stated in, so a number is never shown without its measure.</param>
/// <param name="Interrupted">Whether a night was broken before it ended.</param>
/// <param name="Recovered">Whether the stop restored the party.</param>
/// <param name="Restored">How many members were restored, which is zero for a wait and a broken night.</param>
/// <param name="Cleared">The conditions a completed sleep ended, in the order it cleared them.</param>
/// <param name="Shortage">The state the larder's own rule left on the party, empty when it left none.</param>
/// <param name="Tired">Whether the party currently carries the state going without sleep puts on it.</param>
/// <param name="FatigueDue">When the debt of sleep next falls due, empty while the party is asleep.</param>
/// <param name="FatigueLanded">How many times the debt has fallen due since the session began.</param>
public readonly record struct RestSnapshot(
    bool Available,
    string Kind,
    string Outcome,
    string Code,
    string Message,
    string From,
    string To,
    double ElapsedSeconds,
    int Charged,
    int Covered,
    string Unit,
    bool Interrupted,
    bool Recovered,
    int Restored,
    string Cleared,
    string Shortage,
    bool Tired,
    string FatigueDue,
    int FatigueLanded)
{
    /// <summary>No rest mechanism: nothing can be asked for and nothing has happened.</summary>
    public static RestSnapshot None => new(
        Available: false,
        Kind: string.Empty,
        Outcome: "none",
        Code: string.Empty,
        Message: string.Empty,
        From: string.Empty,
        To: string.Empty,
        ElapsedSeconds: 0,
        Charged: 0,
        Covered: 0,
        Unit: string.Empty,
        Interrupted: false,
        Recovered: false,
        Restored: 0,
        Cleared: string.Empty,
        Shortage: string.Empty,
        Tired: false,
        FatigueDue: string.Empty,
        FatigueLanded: 0);

    /// <summary>Reads the rest facts out of the session's mechanism.</summary>
    /// <param name="rest">The session's rest mechanism, or null when it holds none.</param>
    /// <param name="clock">The session's one clock, or null when its ruleset composed none.</param>
    /// <returns>The facts the panel shows, or <see cref="None"/> when there is no mechanism.</returns>
    public static RestSnapshot Read(PartyRest? rest, GameClock? clock)
    {
        if (rest is null) return None;

        RestResult? last = rest.Last;
        FatigueWatch? fatigue = rest.Fatigue;
        List<string> cleared = [];
        if (last is { } result)
        {
            foreach (ConditionId condition in result.Cleared) cleared.Add(condition.ToString());
        }

        return new RestSnapshot(
            Available: true,
            Kind: last is { } kind ? SessionProjection.WireName(kind.Kind) : string.Empty,
            Outcome: last is null ? "none" : last.IsApplied ? "applied" : "refused",
            Code: last?.Code ?? string.Empty,
            Message: last?.Message ?? string.Empty,
            // A stop that never happened still says where the clock stands: "the party did not rest" and
            // "the party did not rest, and it is midnight" are different answers to a player.
            From: Date(last?.From ?? clock?.Now),
            To: Date(last?.To ?? clock?.Now),
            ElapsedSeconds: last?.Elapsed.TotalSeconds ?? 0,
            Charged: last?.Charge.Amount ?? 0,
            Covered: last?.Covered ?? 0,
            Unit: last is { Charge.Amount: > 0 } charged ? charged.Charge.Unit.ToString().ToLowerInvariant() : string.Empty,
            Interrupted: last?.Interrupted ?? false,
            Recovered: last?.Recovered ?? false,
            Restored: last?.Restored ?? 0,
            Cleared: string.Join(", ", cleared),
            Shortage: last?.Shortage?.ToString() ?? string.Empty,
            Tired: fatigue?.IsWeak ?? false,
            FatigueDue: Date(fatigue?.Due),
            FatigueLanded: fatigue?.Landed ?? 0);
    }

    /// <summary>Where on the calendar a moment is, as the panel shows it, or empty when there is none.</summary>
    private static string Date(GameDate? at) =>
        at is not { Year: > 0 } moment
            ? string.Empty
            : string.Create(
                CultureInfo.InvariantCulture,
                $"{moment.Year:0000}-{moment.Month:00}-{moment.Day:00} {moment.Hour:00}:{moment.Minute:00}");
}
