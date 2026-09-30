using System.Globalization;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Time;

/// <summary>
/// The one mechanism that turns a stop into time: a rest, a camp, or a wait, applied inside the admitted
/// update that asked for it.
/// </summary>
/// <remarks>
/// <para>
/// <b>One workflow serves every kind of stop.</b> Read what the kind is and what it costs, refuse it before
/// anything moves when the party will not or cannot take it, advance the session's one clock by the period
/// that actually passed, restore what a completed sleep restores, settle the day through the party's own
/// ledger, and report the whole of it. Resting, camping, and waiting differ only in the answers they are
/// given, exactly as searching and opening differ in the interaction mechanism.
/// </para>
/// <para>
/// <b>Nothing here counts frames or holds a second clock.</b> A period is a game-time duration handed to
/// <see cref="GameClock.Advance"/>, and the clock's own report is handed on to whoever keeps a schedule
/// against it — so a shop's shelves refresh and a fatigue debt falls due whether the party stood still,
/// camped, or crossed a region. The one exception is the deadline this mechanism owns: it cancels the
/// fatigue debt before a completed sleep and re-arms it when the party wakes.
/// </para>
/// <para>
/// <b>A refusal is an answer, not an exception.</b> No roof, hostiles too near, a larder too thin, a risk
/// this product cannot roll — each comes back as a named refusal with the clock, the larder, and the party
/// exactly as they were, which is what lets a panel show why the party did not sleep.
/// </para>
/// <para>
/// <b>The day is settled after the sleep recovers.</b> Recovery fills the pools and clears the conditions the
/// ruleset names, and the day's provisions are spent afterwards so the larder's own rule has the last word:
/// a night that ate the last portion leaves the party weak, rather than a rest clearing a hunger it just
/// caused.
/// </para>
/// </remarks>
public sealed class PartyRest : IGameTimeObserver, IDeadlineOwner
{
    private readonly IRestRule _rule;
    private readonly PartyEntity _party;
    private readonly PartyResourceLedger? _accounts;
    private readonly GameClock? _clock;
    private readonly IRestSite? _site;
    private readonly FatigueWatch? _fatigue;
    private RestResult? _last;

    /// <summary>Creates the mechanism over the party that stops and the clock that moves.</summary>
    /// <param name="rule">This game's answers about sleeping, camping, waiting, and going without sleep.</param>
    /// <param name="party">The party whose pools are restored and whose members carry the fatigue state.</param>
    /// <param name="clock">
    /// The session's one clock, which every period is advanced on. Without one nothing can pass, and every
    /// stop is refused by name rather than applied to a clock the mechanism invented.
    /// </param>
    /// <param name="site">
    /// Where the party stands, which is what a ruleset reads to decide whether it may sleep here. Without one
    /// the party stands nowhere and a stop is refused by name.
    /// </param>
    /// <param name="accounts">
    /// The party's own accounts as the one settlement path, which a night's provisions are spent through.
    /// Without one a period that costs provisions is refused rather than made free.
    /// </param>
    /// <exception cref="ArgumentNullException">The rule or the party is missing.</exception>
    public PartyRest(
        IRestRule rule,
        PartyEntity party,
        GameClock? clock = null,
        IRestSite? site = null,
        PartyResourceLedger? accounts = null)
    {
        _rule = rule ?? throw new ArgumentNullException(nameof(rule));
        _party = party ?? throw new ArgumentNullException(nameof(party));
        _clock = clock;
        _site = site;
        _accounts = accounts;
        _fatigue = clock is null ? null : new FatigueWatch(clock, party, rule.Fatigue, rule.SleepInterval);
    }

    /// <summary>The last stop this mechanism resolved, or null before the party has stopped at all.</summary>
    public RestResult? Last => _last;

    /// <summary>
    /// The debt of going without sleep, or null when the session keeps no clock and so owes none.
    /// </summary>
    public FatigueWatch? Fatigue => _fatigue;

    /// <summary>Whether a stop can be applied at all: this session keeps a clock and the party stands somewhere.</summary>
    public bool Available => _clock is not null && _site is not null;

    /// <summary>Whether this mechanism is holding the deadline the clock reported.</summary>
    /// <param name="deadline">The handle the clock reported.</param>
    public bool Holds(DeadlineId deadline) => _fatigue?.Holds(deadline) ?? false;

    /// <summary>
    /// Takes the debt of sleep off the one clock for the length of a capture, and puts it back afterwards.
    /// </summary>
    /// <remarks>
    /// The session's save schema records game time and refuses a clock that is holding a deadline, so the one
    /// owner that holds one takes it off the clock while the document is being read and restores it at the
    /// point it was due. A save therefore changes nothing about the running session — and the debt it cannot
    /// carry is a stated loss, routed to the owner of the save schema.
    /// </remarks>
    public void Suspend() => _fatigue?.Suspend();

    /// <summary>Puts a suspended debt of sleep back where it stood.</summary>
    public void Resume() => _fatigue?.Resume();

    /// <summary>
    /// Takes one advance of the session's one clock, which is how the fatigue debt lands whether the clock
    /// was moved by a step, a journey, a stop, or a night at an inn — the clock tells this mechanism about its
    /// own stops exactly as it tells it about everything else.
    /// </summary>
    /// <param name="advance">Where the clock was, where it went, and what it brought due.</param>
    public void Observe(ClockAdvance advance) => _fatigue?.Observe(advance);

    /// <summary>Applies one stop and reports what it did, or why the party did not take it.</summary>
    /// <param name="kind">What the party asked for.</param>
    /// <returns>The result, which is what the projection publishes and the panel shows.</returns>
    public RestResult Perform(RestKind kind)
    {
        RestResult result = Resolve(kind);
        _last = result;
        return result;
    }

    /// <summary>Runs the one stop workflow over a kind, and answers with what came of it.</summary>
    /// <summary>
    /// Sleeps the party through a night somebody else provides — a room at an inn — and reports it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is the same sleep a rest is: the debt of going without sleep is paid before the clock moves and
    /// registered again when the party wakes, the clock moves by the night and tells every owner of game time,
    /// and each member recovers through the one recovery a completed night gives — the pools and what this
    /// game says a night ends — plus whatever the room itself states it ends. What differs is what the party
    /// is spared: the night was paid for at the counter, so the ruleset is not asked whether the party may
    /// sleep here, nothing wanders in, and the larder is not drawn on. The donor's own room is that shape: it
    /// heals the party and resets its days without rest, and spends no food
    /// (OpenEnroth <c>src/Application/Game.cpp:1054-1068</c>).
    /// </para>
    /// <para>
    /// It does not become the last stop the rest mechanism reports: the counter that rented the room reports
    /// the night as its own service.
    /// </para>
    /// </remarks>
    /// <param name="period">How long the night lasts, which the room states.</param>
    /// <param name="ends">The conditions the room says a night in it ends, beside those every night ends.</param>
    /// <returns>The night, or why the party could not take it.</returns>
    public RestResult Lodge(GameDuration period, IReadOnlyList<ConditionId> ends)
    {
        ArgumentNullException.ThrowIfNull(ends);
        if (_clock is not { } clock)
        {
            return RestResult.Refused(
                RestKind.Rest,
                default,
                "rest-no-clock",
                "The party cannot sleep here: this session keeps no clock, so no night could pass.");
        }

        if (_site is not { } site)
        {
            return RestResult.Refused(
                RestKind.Rest,
                clock.Now,
                "rest-nowhere",
                "The party cannot sleep here: it stands in no place a room could be in.");
        }

        RestRequest request = new(RestKind.Rest, site, _party, clock);
        _fatigue?.Pay();
        ClockAdvance advance = clock.Advance(period);
        List<ConditionId> cleared = [];
        int restored = Recover(request, ends, cleared);
        _fatigue?.Arm();
        return RestResult.Applied(
            RestKind.Rest,
            string.Create(
                CultureInfo.InvariantCulture,
                $"The party sleeps in a room for {Describe(advance.Elapsed)}, to {Describe(advance.To)}. Every member's pools are full again and the room's price covered the night's board."),
            advance,
            Provisions.None,
            covered: 0,
            interrupted: null,
            recovered: true,
            restored,
            cleared,
            shortage: null);
    }

    /// <summary>
    /// What a completed night gives each member: both pools filled, and every condition the ruleset says a
    /// night ends cleared, with any the night's own source adds.
    /// </summary>
    /// <returns>How many members recovered.</returns>
    private int Recover(RestRequest request, IReadOnlyList<ConditionId> alsoEnds, List<ConditionId> cleared)
    {
        IReadOnlyList<ConditionId> night = _rule.RecoveredBy(request);
        List<ConditionId> ends = [.. night, .. alsoEnds.Where(condition => !night.Contains(condition))];
        int restored = 0;
        foreach (PartyMember member in _party.Members)
        {
            member.Resources.RestoreAll();
            foreach (ConditionId condition in ends) member.Conditions.Clear(condition);
            restored++;
        }

        cleared.AddRange(ends);
        return restored;
    }

    private RestResult Resolve(RestKind kind)
    {
        if (_clock is not { } clock)
        {
            return RestResult.Refused(
                kind,
                default,
                "rest-no-clock",
                "The party cannot stop here: this session keeps no clock, so no period could pass.");
        }

        GameDate at = clock.Now;
        if (_site is not { } site)
        {
            return RestResult.Refused(
                kind,
                at,
                "rest-nowhere",
                "The party cannot stop here: it stands in no place whose ground could be slept on or waited in.");
        }

        RestRequest request = new(kind, site, _party, clock);
        bool sleeps = RestKinds.Sleeps(kind);

        // What the period is and what it costs: a sleep is the ruleset's answer about this place, and a wait
        // is the clock's own — until dawn is read off the daylight window, and an hour is an hour.
        RestQuote quote;
        if (sleeps)
        {
            quote = _rule.Quote(request);
            if (quote.Refusal is { } refusal) return RestResult.Refused(kind, at, refusal.Code, refusal.Message);
            if (!quote.Charge.IsNone && _accounts is null)
            {
                return RestResult.Refused(
                    kind,
                    at,
                    "rest-no-accounts",
                    $"A sleep here costs {Amounts(quote.Charge)}, and this session holds no party accounts to settle it from.");
            }

            // A night the larder cannot provision is refused before it starts, which is the donor's own
            // rule for a rest the party cannot feed.
            if (!quote.Charge.IsNone && !_party.Food.CanCover(quote.Charge))
            {
                return RestResult.Refused(
                    kind,
                    at,
                    "rest-larder-short",
                    $"A sleep here costs {Amounts(quote.Charge)} and the party's larder holds {_party.Food.Portions} {Unit(_party.Food.Unit)}.");
            }
        }
        else
        {
            quote = RestQuote.Planned(WaitPeriod(kind, clock), Provisions.None);
        }

        // Whether the night is broken is asked before anything moves: a broken night is one the clock was
        // never advanced for, and the party gets only the part that happened.
        RestInterruption? interruption = sleeps ? _rule.Interrupt(request) : null;
        GameDuration period = interruption?.Duration ?? quote.Duration;
        bool completes = sleeps && interruption is null;

        // A completed sleep pays the debt of going without sleep before the clock moves, so the hours the
        // party slept through cannot be the hours that weakened it.
        if (completes) _fatigue?.Pay();

        // Every owner of game time hears the stop's own hours from the clock itself, so a shop's shelves
        // refresh, a ward lapses, and a debt that a wait ran past lands in the same update that moved it.
        ClockAdvance advance = clock.Advance(period);

        // Recovery first: a completed sleep fills both pools and clears what the ruleset says a night ends.
        int restored = 0;
        List<ConditionId> cleared = [];
        if (completes) restored = Recover(request, [], cleared);

        // Then the day's provisions, through the party's one settlement path, so the larder's own consequence
        // has the last word over what the sleep restored. A broken night pays nothing: the day it would have
        // paid for never happened, which is the donor's own reading of a rest an encounter broke.
        int covered = 0;
        ActiveCondition? shortage = null;
        if (completes && !quote.Charge.IsNone && _accounts is { } accounts)
        {
            ProvisionDay day = accounts.SpendDay(quote.Charge);
            covered = day.Covered;
            shortage = day.Shortage;
        }

        // The debt is registered again from the moment the party wakes, which is when the next night's sleep
        // becomes due; a broken night or a wait leaves the debt exactly where it stood.
        if (completes) _fatigue?.Arm();

        return RestResult.Applied(
            kind,
            Message(kind, advance, quote, covered, interruption, completes, restored),
            advance,
            // What the period cost the larder is what it actually took: a broken night's day never happened,
            // so it neither restored nor charged, and a report that named the price anyway would tell a
            // player they paid for a night they did not get.
            completes ? quote.Charge : Provisions.None,
            covered,
            interruption,
            completes,
            restored,
            cleared,
            shortage);
    }

    /// <summary>How long a wait lasts, which is a clock read: until dawn, an hour, or a short interval.</summary>
    /// <remarks>
    /// Waiting until dawn is measured from the clock's own daylight window, so the party rises when this game
    /// says morning is and not at an hour the mechanism chose. The donor states the same option as a wait to
    /// its own dawn hour (<c>src/Engine/Engine.cpp:1447-1451</c>, five in the morning, always the next one).
    /// </remarks>
    private static GameDuration WaitPeriod(RestKind kind, GameClock clock) => kind switch
    {
        RestKind.WaitAnHour => GameDuration.FromHours(1),
        RestKind.WaitFiveMinutes => GameDuration.FromMinutes(5),
        _ => UntilDawn(clock),
    };

    /// <summary>The game time between where the clock stands and the next dawn after it.</summary>
    private static GameDuration UntilDawn(GameClock clock)
    {
        GameCalendar calendar = clock.Calendar;
        GameDate now = clock.Now;
        GameDate today = new(now.Year, now.Month, now.Day);
        GameDuration dawn = GameDuration.FromMinutes(clock.Daylight.Dawn.Minutes);
        long nowMilliseconds = calendar.AbsoluteMilliseconds(now);
        long dawnMilliseconds = calendar.AbsoluteMilliseconds(calendar.Add(today, dawn));
        if (dawnMilliseconds <= nowMilliseconds)
        {
            // Dawn already passed, so the party waits for the next one; a party waiting at the very moment of
            // dawn waits a whole day, which is what "until dawn" means when it is dawn.
            dawnMilliseconds = calendar.AbsoluteMilliseconds(calendar.Add(calendar.Add(today, calendar.Days(1)), dawn));
        }

        return GameDuration.FromMilliseconds(dawnMilliseconds - nowMilliseconds);
    }

    /// <summary>What one stop reports, in the words a person reads and with what it cost.</summary>
    private static string Message(
        RestKind kind,
        ClockAdvance advance,
        RestQuote quote,
        int covered,
        RestInterruption? interruption,
        bool completes,
        int restored)
    {
        string period = Describe(advance.Elapsed);
        string where = Describe(advance.To);
        string slept = kind switch
        {
            RestKind.Rest => "rests",
            RestKind.Camp => "camps",
            _ => "waits",
        };

        if (interruption is { } broke)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"The party {slept} for {period} and the night is broken at {where}: {broke.Message} Nothing was restored and no provisions were spent.");
        }

        if (!RestKinds.Sleeps(kind))
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"The party {slept} for {period}, to {where}. Waiting rests nobody: the clock moved and nothing was restored.");
        }

        string cost = quote.Charge.IsNone
            ? "and spends nothing"
            : string.Create(CultureInfo.InvariantCulture, $"and spends {covered} {Unit(quote.Charge.Unit)}");
        string recovered = restored > 0
            ? $"every member is restored ({restored})"
            : "there was nobody to restore";
        return string.Create(
            CultureInfo.InvariantCulture,
            $"The party {slept} for {period}, to {where}, {cost}: {recovered}.");
    }

    /// <summary>How much game time a period is, in the units a person reads.</summary>
    private static string Describe(GameDuration period)
    {
        long minutes = period.Milliseconds / (GameDuration.MillisecondsPerSecond * GameDuration.SecondsPerMinute);
        long hours = minutes / GameDuration.MinutesPerHour;
        long rest = minutes % GameDuration.MinutesPerHour;
        if (hours == 0) return $"{rest} minute(s)";
        return rest == 0 ? $"{hours} hour(s)" : $"{hours} hour(s) {rest} minute(s)";
    }

    /// <summary>Where on the calendar the clock stands, as a person reads it.</summary>
    private static string Describe(GameDate at) =>
        at.Year <= 0
            ? "no date"
            : string.Create(CultureInfo.InvariantCulture, $"{at.Year:0000}-{at.Month:00}-{at.Day:00} {at.Hour:00}:{at.Minute:00}");

    /// <summary>What a charge is, in the words a refusal uses.</summary>
    private static string Amounts(Provisions charge) =>
        string.Create(CultureInfo.InvariantCulture, $"{charge.Amount} {Unit(charge.Unit)}");

    /// <summary>The word a unit of provisions is written as.</summary>
    private static string Unit(ProvisionUnit unit) => unit.ToString().ToLowerInvariant();
}
