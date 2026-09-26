using PartyRpg.Kit.Party;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;

namespace PartyRpg.Kit.Magic;

/// <summary>One effect a spell left running on the party, and the moment the clock ends it.</summary>
/// <remarks>
/// The identity and the magnitude are the ruleset's own — the kit holds them and hands them back — and the
/// end is a point on the session's calendar rather than a count of anything, so a panel can print when a ward
/// lapses and a save that carries the same effect can be read against the same clock.
/// </remarks>
/// <param name="Effect">Which effect definition is running.</param>
/// <param name="Magnitude">The magnitude it acts at.</param>
/// <param name="EndsAt">When the clock ends it, or null when nothing has said when it ends.</param>
public readonly record struct RunningSpellEffect(EffectId Effect, int Magnitude, GameDate? EndsAt);

/// <summary>
/// The effects spells have left running on the party, each with the deadline the one clock ends it at.
/// </summary>
/// <remarks>
/// <para>
/// <b>A duration is a deadline on the clock, never a count in a step.</b> A wand of light, a ward against
/// fire, and a charm that lasts an hour all leave the same shape of state: a party-carried effect, which is
/// where a party-wide buff already lives, and a deadline registered with the session's one clock. The effect
/// ends when the clock reaches that deadline — in whatever update or journey carried it there — so a party
/// that stands still and one that crosses the world lose the ward at the same moment. Nothing here counts
/// updates, frames, or seconds of its own.
/// </para>
/// <para>
/// <b>The party holds the effect; this holds when it ends.</b> The magnitude is applied through
/// <see cref="PartyEffects"/>, the owner every reader of a party-wide effect already consults, so a game's
/// combat readings, its dispelling, and its panel all see the same state. What is kept here is only the
/// deadline and the reading a panel needs: which effects are running and when each one lapses.
/// </para>
/// <para>
/// <b>A session with no clock is not a session without effects.</b> A deadline can only be registered with a
/// clock, so an effect applied without one lasts until something removes it, and its end reads as unknown
/// rather than as a moment nobody can compute. That is the honest answer for a product running without time
/// — and the effect is still carried, read, and dispelled.
/// </para>
/// </remarks>
public sealed class RunningSpellEffects : IGameTimeObserver, IRunningSpellEffects
{
    private readonly PartyEntity _party;
    private readonly GameClock? _clock;
    private readonly List<Held> _held = [];

    /// <summary>Creates the effects a party carries.</summary>
    /// <param name="party">The party whose effects are the state every reader already consults.</param>
    /// <param name="clock">
    /// The session's one clock, which a duration is registered against, or null when the session keeps no
    /// time. Without one an effect lasts until it is removed.
    /// </param>
    /// <exception cref="ArgumentNullException">No party was supplied.</exception>
    public RunningSpellEffects(PartyEntity party, GameClock? clock = null)
    {
        _party = party ?? throw new ArgumentNullException(nameof(party));
        _clock = clock;
    }

    /// <summary>The party whose effects this state is about.</summary>
    public PartyEntity Party => _party;

    /// <summary>The effects a spell has running now, in the order they were applied.</summary>
    /// <remarks>
    /// An effect something else removed — a dispelling, a load, a rule of the game's own — is not reported,
    /// because the party is the state and this is only the ledger of when each one ends. A deadline for an
    /// effect the party no longer holds is left with the clock and ends nothing when it comes due.
    /// </remarks>
    public IReadOnlyList<RunningSpellEffect> Running
    {
        get
        {
            List<RunningSpellEffect> running = [];
            foreach (Held held in _held)
            {
                if (_party.Effects.Has(held.Effect)) running.Add(new RunningSpellEffect(held.Effect, held.Magnitude, held.EndsAt));
            }

            return running;
        }
    }

    /// <summary>Whether an effect a spell applied is still running.</summary>
    /// <param name="effect">The effect to look for.</param>
    public bool IsRunning(EffectId effect) => _party.Effects.Has(effect);

    /// <summary>What is running under one identity, or null when nothing is.</summary>
    /// <param name="effect">The effect to read.</param>
    public RunningSpellEffect? Find(EffectId effect)
    {
        foreach (RunningSpellEffect running in Running)
        {
            if (running.Effect == effect) return running;
        }

        return null;
    }

    /// <summary>
    /// Leaves an effect running on the party, ending it when the clock reaches the end of its duration.
    /// </summary>
    /// <remarks>
    /// Applying an effect that is already running replaces its magnitude and its deadline rather than
    /// stacking a second copy: two castings of one ward are one ward at the stronger reading, which is what
    /// the party's own effects owner already does with a party-wide effect.
    /// </remarks>
    /// <param name="effect">Which effect the spell leaves.</param>
    /// <param name="magnitude">The magnitude it acts at.</param>
    /// <param name="lasts">How long it lasts, or null when nothing states an end.</param>
    /// <returns>What is running under that identity after the call.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A stated duration is no time at all.</exception>
    public RunningSpellEffect Start(EffectId effect, int magnitude, GameDuration? lasts)
    {
        if (lasts is { IsNone: true })
        {
            throw new ArgumentOutOfRangeException(
                nameof(lasts),
                lasts,
                "A spell effect that lasts no time at all would end in the advance that applied it; a duration is a length of game time.");
        }

        Drop(effect);
        GameDate? endsAt = null;
        DeadlineId? deadline = null;
        if (lasts is { } duration && _clock is { } clock)
        {
            deadline = clock.ScheduleAfter(duration);
            endsAt = clock.Calendar.Add(clock.Now, duration);
        }

        _party.Effects.Apply(new PartyEffect(effect, magnitude));
        _held.Add(new Held(effect, magnitude, deadline, endsAt));
        return new RunningSpellEffect(effect, magnitude, endsAt);
    }

    /// <summary>Ends one effect a spell left running.</summary>
    /// <param name="effect">The effect to end.</param>
    /// <returns>Whether it was running.</returns>
    public bool End(EffectId effect)
    {
        bool wasHeld = Drop(effect);
        return _party.Effects.Remove(effect) || wasHeld;
    }

    /// <summary>Ends every effect a spell left running, and says which ones those were.</summary>
    /// <remarks>
    /// Only what a spell applied is ended: a passage bought at a counter and a guild membership are
    /// party-carried effects too, and they are not magic — a dispelling that took them would sell a player a
    /// ticket and burn it. What this ends is exactly what was started here.
    /// </remarks>
    /// <returns>The effects that were running, in the order they were applied.</returns>
    public IReadOnlyList<RunningSpellEffect> EndAll()
    {
        IReadOnlyList<RunningSpellEffect> running = Running;
        foreach (RunningSpellEffect effect in running) _party.Effects.Remove(effect.Effect);
        foreach (Held held in _held)
        {
            if (held.Deadline is { } deadline) _clock?.Cancel(deadline);
        }

        _held.Clear();
        return running;
    }

    /// <summary>
    /// Ends what the clock has reached, which is how an effect's duration runs out.
    /// </summary>
    /// <remarks>
    /// This is handed every advance of the session's one clock, whether it came from an admitted update or
    /// from a journey's charge, so a ward lapses on the road exactly as it does standing still. An effect ends
    /// here whether or not its deadline is the one that came due: a load that restored a party without this
    /// ledger, or a rule that dropped the effect another way, leaves state that must not run on.
    /// </remarks>
    /// <param name="advance">Where the clock was, where it went, and what it brought due.</param>
    /// <exception cref="ArgumentNullException">No advance was supplied.</exception>
    public void Observe(ClockAdvance advance)
    {
        ArgumentNullException.ThrowIfNull(advance);
        if (advance.Due.Count == 0) return;
        HashSet<long> due = [.. advance.Due.Select(deadline => deadline.Deadline.Value)];
        foreach (Held held in _held.ToArray())
        {
            if (held.Deadline is not { } deadline || !due.Contains(deadline.Value)) continue;
            _held.Remove(held);
            _party.Effects.Remove(held.Effect);
        }
    }

    /// <summary>Forgets one held effect and cancels the deadline it was registered under.</summary>
    private bool Drop(EffectId effect)
    {
        int index = _held.FindIndex(held => held.Effect == effect);
        if (index < 0) return false;
        if (_held[index].Deadline is { } deadline) _clock?.Cancel(deadline);
        _held.RemoveAt(index);
        return true;
    }

    /// <summary>One effect this ledger started, with the deadline the clock ends it at.</summary>
    /// <param name="Effect">Which effect definition is running.</param>
    /// <param name="Magnitude">The magnitude it acts at.</param>
    /// <param name="Deadline">The clock's own handle, or null when no clock was given one.</param>
    /// <param name="EndsAt">When it ends, or null when nothing stated an end.</param>
    private readonly record struct Held(EffectId Effect, int Magnitude, DeadlineId? Deadline, GameDate? EndsAt);
}

/// <summary>The effects a spell has left running, as a panel and a reader of the state ask for them.</summary>
/// <remarks>
/// It is deliberately the whole of what a reader outside the effect path may know about durations: which
/// effects are running, how strongly, and when each one lapses. A mechanism that had to reach the ledger
/// itself would be a second owner of the same deadlines.
/// </remarks>
public interface IRunningSpellEffects
{
    /// <summary>The effects a spell has running now, in the order they were applied.</summary>
    IReadOnlyList<RunningSpellEffect> Running { get; }

    /// <summary>Whether an effect a spell applied is still running.</summary>
    /// <param name="effect">The effect to look for.</param>
    bool IsRunning(EffectId effect);
}
