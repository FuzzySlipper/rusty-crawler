using System.Globalization;
using PartyRpg.Kit.Persistence;
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

    /// <summary>The party every stop is applied to.</summary>
    public PartyEntity Party => _party;

    /// <summary>Whether this mechanism is holding the deadline the clock reported.</summary>
    /// <param name="deadline">The handle the clock reported.</param>
    public bool Holds(DeadlineId deadline) => _fatigue?.Holds(deadline) ?? false;

    /// <inheritdoc />
    public DeadlineSave? CaptureDeadline(DeadlineId deadline, GameClock clock) => Holds(deadline)
        ? new DeadlineSave(DeadlineKind.Fatigue, string.Empty, clock.DueElapsedMilliseconds(deadline), _fatigue!.Interval.Milliseconds)
        : null;

    /// <inheritdoc />
    public bool RestoreDeadline(DeadlineSave deadline, PartyEntity party)
    {
        if (deadline.Kind != DeadlineKind.Fatigue || _fatigue is null) return false;
        _fatigue.RestoreDue(deadline.DueElapsedMilliseconds);
        return true;
    }

    /// <inheritdoc />
    public string Describe(DeadlineId deadline) =>
        _fatigue?.Due is { } due
            ? string.Create(CultureInfo.InvariantCulture, $"the debt of sleep, due {due.MinuteText}")
            : "the debt of sleep";

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
    /// <summary>Whether a night in a room could be slept here, and why not when it could not, without moving anything.</summary>
    /// <returns>Why no night could pass, or null when one can.</returns>
    public Refusal? JudgeRoom()
    {
        if (_clock is null)
        {
            return new Refusal(RestCodes.RestNoClock, "The party cannot sleep here: this session keeps no clock, so no night could pass.");
        }

        return _site is null
            ? new Refusal(RestCodes.RestNowhere, "The party cannot sleep here: it stands in no place a room could be in.")
            : null;
    }

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
    /// sleep here, nothing wanders in, and the larder is not drawn on.
    /// </para>
    /// <para>
    /// It does not become the last stop the rest mechanism reports: the counter that rented the room reports
    /// the night as its own service.
    /// </para>
    /// </remarks>
    /// <param name="period">How long the night lasts, which the room states; what could refuse it is <see cref="JudgeRoom"/>'s answer, which a counter asks before it takes the price.</param>
    /// <param name="ends">The conditions the room says a night in it ends, beside those every night ends.</param>
    /// <returns>The night, or why the party could not take it.</returns>
    public RestResult SleepInRoom(GameDuration period, IReadOnlyList<ConditionId> ends)
    {
        ArgumentNullException.ThrowIfNull(ends);
        if (JudgeRoom() is { } refused) return RestResult.Refused(RestKind.Rest, _clock?.Now ?? default, refused);
        GameClock clock = _clock!;
        IRestSite site = _site!;

        RestRequest request = new(RestKind.Rest, site, _party, clock);
        _fatigue?.Pay();
        ClockAdvance advance = clock.Advance(period);
        List<ConditionId> cleared = [];
        List<string> left = [];
        int restored = Recover(request, ends, cleared, left);
        _fatigue?.Arm();
        return RestResult.Applied(
            RestKind.Rest,
            string.Create(
                CultureInfo.InvariantCulture,
                $"The party sleeps for {Describe(advance.Elapsed)}, to {Describe(advance.To)}. {Recovery(restored, left)} The service's price covered the rest's board."),
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
    /// night ends cleared, with any the night's own source adds; then the ruleset says what the member keeps of it.
    /// </summary>
    /// <returns>How many members recovered.</returns>
    private int Recover(RestRequest request, IReadOnlyList<ConditionId> alsoEnds, List<ConditionId> cleared, List<string> left)
    {
        IReadOnlyList<ConditionId> night = _rule.RecoveredBy(request);
        List<ConditionId> ends = [.. night, .. alsoEnds.Where(condition => !night.Contains(condition))];
        int restored = 0;
        foreach (PartyMember member in _party.Members)
        {
            if (_rule.Unrestored(request, member) is { } reason)
            {
                left.Add(LeftAsTheyWere(member, reason));
                continue;
            }
            member.Resources.RestoreAll();
            foreach (ConditionId condition in ends)
                if (member.Conditions.Clear(condition) && !cleared.Contains(condition)) cleared.Add(condition);
            _rule.Rested(request, member);
            restored++;
        }

        return restored;
    }

    /// <summary>
    /// What one kind of stop would be here and now — how long, what it would charge, and why it would be refused —
    /// judged by the same checks the stop itself runs, and moving nothing.
    /// </summary>
    /// <remarks>
    /// A screen offers each stop with this answer before it is pressed, so a party short of food or standing where it
    /// cannot sleep reads why beside the option. Whether a night would be broken is not judged: that is a roll the
    /// night itself makes. The members a completed sleep would leave as they are are named with the ruleset's reason.
    /// </remarks>
    /// <param name="kind">The kind of stop.</param>
    /// <returns>The offer.</returns>
    public RestOffer Judge(RestKind kind)
    {
        (RestRequest? request, RestQuote? quote, Refusal? refusal, _) = Plan(kind);
        List<string> unrestored = [];
        if (refusal is null && request is { } asked && RestKinds.Sleeps(kind))
        {
            foreach (PartyMember member in _party.Members)
                if (_rule.Unrestored(asked, member) is { } reason) unrestored.Add(LeftAsTheyWere(member, reason));
        }

        return refusal is null && quote is { } offered
            ? new RestOffer(kind, offered.Duration, offered.Charge, null, unrestored)
            : new RestOffer(kind, GameDuration.None, Provisions.None, refusal, unrestored);
    }

    private RestResult Resolve(RestKind kind)
    {
        (RestRequest? planned, RestQuote? planning, Refusal? refused, GameDate at) = Plan(kind);
        if (refused is { } refusal) return RestResult.Refused(kind, at, refusal);

        // A plan that was not refused names its request and its quote over the clock it was asked on.
        RestRequest request = planned!;
        RestQuote quote = planning!;
        GameClock clock = request.Clock;
        bool sleeps = RestKinds.Sleeps(kind);

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
        List<string> left = [];
        if (completes) restored = Recover(request, [], cleared, left);

        // Then the day's provisions, through the party's one settlement path, so the larder's own consequence
        // has the last word over what the sleep restored. A broken night pays nothing: the day it would have
        // paid for never happened, so a rest an encounter broke costs nothing.
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
            Message(kind, advance, quote, covered, interruption, completes, restored, left),
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

    /// <summary>The checks a stop runs before anything moves: where and when it is asked, what it is, and why it would be refused.</summary>
    private (RestRequest? Request, RestQuote? Quote, Refusal? Refusal, GameDate At) Plan(RestKind kind)
    {
        if (_clock is not { } clock)
        {
            return (null, null, new Refusal(RestCodes.RestNoClock, "The party cannot stop here: this session keeps no clock, so no period could pass."), default);
        }

        GameDate at = clock.Now;
        if (_site is not { } site)
        {
            return (null, null, new Refusal(RestCodes.RestNowhere, "The party cannot stop here: it stands in no place whose ground could be slept on or waited in."), at);
        }

        RestRequest request = new(kind, site, _party, clock);
        if (_rule.Stop(request) is { } halted) return (request, null, halted, at);

        // What the period is and what it costs: a sleep is the ruleset's answer about this place, and a wait
        // is the clock's own — until dawn is read off the daylight window, and an hour is an hour.
        if (!RestKinds.Sleeps(kind)) return (request, RestQuote.Planned(WaitPeriod(kind, clock), Provisions.None), null, at);
        RestQuote quote = _rule.Quote(request);
        if (quote.Refusal is { } refusal) return (request, quote, refusal, at);
        if (!quote.Charge.IsNone && _accounts is null)
        {
            return (request, quote, new Refusal(RestCodes.RestNoAccounts, $"A sleep here costs {Amounts(quote.Charge)}, and this session holds no party accounts to settle it from."), at);
        }

        // A night the larder cannot provision is refused before it starts: a rest the party cannot feed
        // is a decision about the action, not a shortfall the day absorbs.
        if (!quote.Charge.IsNone && !_party.Food.CanCover(quote.Charge))
        {
            return (request, quote, new Refusal(RestCodes.RestLarderShort, $"A sleep here costs {Amounts(quote.Charge)} and the party's larder holds {_party.Food.Portions} {Unit(_party.Food.Unit)}."), at);
        }

        return (request, quote, null, at);
    }

    /// <summary>How long a wait lasts, which is a clock read: until dawn, an hour, or a short interval.</summary>
    /// <remarks>
    /// Waiting until dawn is measured from the clock's own daylight window, so the party rises when this game
    /// says morning is and not at an hour the mechanism chose, and always the next such morning.
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
        int restored,
        IReadOnlyList<string> left)
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
        return string.Create(
            CultureInfo.InvariantCulture,
            $"The party {slept} for {period}, to {where}, {cost}: {Recovery(restored, left)}");
    }

    private static string Recovery(int restored, IReadOnlyList<string> left) => left.Count > 0
        ? $"{restored} member(s) restored. Left as they were: {string.Join("; ", left)}."
        : restored > 0 ? $"every member is restored ({restored})." : "there was nobody to restore.";

    /// <summary>How a member a night leaves as they were is named, by the offer before the night and the result after it.</summary>
    private static string LeftAsTheyWere(PartyMember member, string reason) => $"{member.Profile.Name}: {reason}";

    /// <summary>How much game time a period is, in the units a person reads.</summary>
    internal static string Describe(GameDuration period)
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
            : at.MinuteText;

    /// <summary>What a charge is, in the words a refusal uses.</summary>
    internal static string Amounts(Provisions charge) =>
        string.Create(CultureInfo.InvariantCulture, $"{charge.Amount} {Unit(charge.Unit)}");

    /// <summary>The word a unit of provisions is written as.</summary>
    private static string Unit(ProvisionUnit unit) => unit.ToString().ToLowerInvariant();
}
