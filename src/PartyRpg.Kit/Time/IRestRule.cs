using PartyRpg.Kit.Party;
using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Time;

/// <summary>What a sleep of one kind is, or why the party will not take it here.</summary>
/// <remarks>
/// <para>
/// One value rather than a period beside a separate permission, so a caller cannot obtain a duration and
/// forget to ask whether the party would lie down at all — the same shape a settlement quote and a travel
/// cost use. The charge is stated in provisions because that is what a night costs, and it is settled
/// through the party's one ledger rather than counted here.
/// </para>
/// <para>
/// A refusal names the rule it broke: no roof to sleep under, hostiles too near, or a risk this product
/// cannot take because it has no random service to draw it from. The sentence is what a player reads, so a
/// refused night is never silence.
/// </para>
/// </remarks>
public sealed record RestQuote
{
    private readonly GameDuration _duration;
    private readonly Provisions _charge;

    private RestQuote(GameDuration duration, Provisions charge, PartyRefusal? refusal)
    {
        _duration = duration;
        _charge = charge;
        Refusal = refusal;
    }

    /// <summary>The party may take this sleep: it lasts this long and costs this much.</summary>
    /// <param name="duration">How long the period lasts, which must be more than no time at all.</param>
    /// <param name="charge">What the period costs the party's larder.</param>
    /// <returns>The quote.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The period is no time at all.</exception>
    public static RestQuote Planned(GameDuration duration, Provisions charge)
    {
        if (duration.IsNone)
        {
            throw new ArgumentOutOfRangeException(
                nameof(duration),
                duration,
                "A sleep that lasts no time at all would recover the party for free; a period is a length of game time.");
        }

        return new RestQuote(duration, charge, null);
    }

    /// <summary>The party will not sleep here, and this is why.</summary>
    /// <param name="refusal">Why the period cannot be taken.</param>
    /// <returns>The quote.</returns>
    /// <exception cref="ArgumentNullException">The refusal is null.</exception>
    public static RestQuote Refused(PartyRefusal refusal) =>
        new(GameDuration.None, Provisions.None, refusal ?? throw new ArgumentNullException(nameof(refusal)));

    /// <summary>The refusal, or null when the period may be taken.</summary>
    public PartyRefusal? Refusal { get; }

    /// <summary>Whether the party may take the period.</summary>
    public bool Admitted => Refusal is null;

    /// <summary>What the period costs the party's larder, which exists only on a quote that was not refused.</summary>
    /// <exception cref="InvalidOperationException">This quote is a refusal; read <see cref="Refusal"/> instead.</exception>
    public Provisions Charge => RequireAdmitted(_charge);

    /// <summary>How long the period lasts, which exists only on a quote that was not refused.</summary>
    /// <exception cref="InvalidOperationException">This quote is a refusal; read <see cref="Refusal"/> instead.</exception>
    public GameDuration Duration => RequireAdmitted(_duration);

    /// <inheritdoc />
    public override string ToString() => Refusal is null
        ? $"a sleep of {_duration.TotalSeconds / 3600.0:0.##} hour(s) costing {_charge.Amount} {_charge.Unit}"
        : $"refused: {Refusal}";

    private T RequireAdmitted<T>(T value) => Admitted
        ? value
        : throw new InvalidOperationException("A refused rest quote holds no period and no charge; read Refusal instead.");
}

/// <summary>What broke a night in the open, and how much of it the party got before it did.</summary>
/// <remarks>
/// <para>
/// Camping carries a risk the donor states: a sleeping party may be attacked, and the attack ends the rest
/// where it stands (<c>src/Application/Game.cpp:1147-1170</c>, the encounter roll taken when a rest begins
/// and the one-hour nap it leaves the party with). What this value carries is the whole consequence the
/// mechanism applies — the period that actually passed, and the words a player reads — so a broken night is
/// a shorter night rather than a second kind of night.
/// </para>
/// <para>
/// The period is the part before the break, which is why it is stated here rather than as the period that
/// was asked for: what happened is what the clock is advanced by, and a report that advanced the clock by
/// the night the party did not get would be telling a story the party cannot see.
/// </para>
/// </remarks>
public sealed record RestInterruption
{
    private RestInterruption(string code, string message, GameDuration duration)
    {
        Code = code;
        Message = message;
        Duration = duration;
    }

    /// <summary>A night the party did not get: this broke it, and this much of it had passed.</summary>
    /// <param name="code">A short stable code naming what broke the night.</param>
    /// <param name="message">What broke it, in terms a person can act on.</param>
    /// <param name="duration">How much of the period passed before it was broken, which must be more than no time at all.</param>
    /// <returns>The interruption.</returns>
    /// <exception cref="ArgumentException">The code or the message is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The period is no time at all.</exception>
    public static RestInterruption Broke(string code, string message, GameDuration duration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        if (duration.IsNone)
        {
            throw new ArgumentOutOfRangeException(
                nameof(duration),
                duration,
                "An interrupted period that passed no time at all would leave the party exactly where it was and report a night it never began.");
        }

        return new RestInterruption(code, message, duration);
    }

    /// <summary>A short stable code naming what broke the night.</summary>
    public string Code { get; }

    /// <summary>What broke it, in terms a person can act on.</summary>
    public string Message { get; }

    /// <summary>How much of the period passed before it was broken.</summary>
    public GameDuration Duration { get; }

    /// <inheritdoc />
    public override string ToString() => $"{Code}: {Message}";
}

/// <summary>
/// What this game answers about stopping: what a sleep costs and may do here, what breaks it, what a
/// completed one restores, and what going without sleep does.
/// </summary>
/// <remarks>
/// <para>
/// Four answers, and every one of them is policy the kit cannot hold: whether a party may sleep in the open
/// at all, what the ground costs in provisions, whether anything hostile is near enough to keep it from
/// lying down, whether the night is broken, which conditions a night's sleep clears, and what the party
/// carries when it has gone too long without one. The kit supplies the workflow — refuse before anything
/// moves, advance the one clock by the period that actually passed, settle the day through the party's own
/// ledger, and recover what the rule names — and reports it.
/// </para>
/// <para>
/// Nothing here is asked about a wait. A wait is the mechanism's own shape: it passes time and restores
/// nothing, and a rule that could refuse it would be inventing a reason the donor never states.
/// </para>
/// </remarks>
public interface IRestRule
{
    /// <summary>What a sleep of this kind is here, or why the party will not take it.</summary>
    /// <param name="request">What the party asked for, where it stands, and the one clock.</param>
    /// <returns>The period and its charge, or the refusal that stands in their place.</returns>
    RestQuote Quote(RestRequest request);

    /// <summary>
    /// Whether something breaks a night in the open, and how much of it the party gets, or null when the
    /// night runs its course.
    /// </summary>
    /// <remarks>
    /// This is asked before anything moves, so a broken night is one the clock never advanced for: the party
    /// gets the part that happened, restores nothing, and spends nothing, which is the donor's own
    /// consequence for a rest an encounter breaks.
    /// </remarks>
    /// <param name="request">What the party asked for, where it stands, and the one clock.</param>
    /// <returns>What broke the night, or null when nothing did.</returns>
    RestInterruption? Interrupt(RestRequest request);

    /// <summary>The conditions a completed sleep clears from every member.</summary>
    /// <param name="request">What the party asked for, where it stands, and the one clock.</param>
    /// <returns>The conditions a night's sleep ends, in the order they are cleared.</returns>
    IReadOnlyList<ConditionId> RecoveredBy(RestRequest request);

    /// <summary>The state going too long without sleep puts on every member.</summary>
    ActiveCondition Fatigue { get; }

    /// <summary>How long a party may stay awake before that state lands.</summary>
    GameDuration SleepInterval { get; }
}
