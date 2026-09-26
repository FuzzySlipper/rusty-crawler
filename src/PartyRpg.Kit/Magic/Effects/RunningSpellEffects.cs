using PartyRpg.Kit.Party;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;

namespace PartyRpg.Kit.Magic;

/// <summary>One effect a spell left running, and the moment the clock ends it.</summary>
/// <remarks>
/// <para>
/// The identity and the magnitude are the ruleset's own — the kit holds them and hands them back — and the
/// end is a point on the session's calendar rather than a count of anything, so a panel can print when a ward
/// lapses and a save that carries the same effect can be read against the same clock.
/// </para>
/// <para>
/// <b>An effect runs on the party or on one character.</b> <see cref="Member"/> is the character a spell
/// aimed at one of the band left its effect on, and is unset for an effect the whole party carries; the
/// difference is where the effect is read, not how long it lasts, because both are the same deadline on the
/// same clock.
/// </para>
/// </remarks>
/// <param name="Effect">Which effect definition is running.</param>
/// <param name="Magnitude">The magnitude it acts at.</param>
/// <param name="EndsAt">When the clock ends it, or null when nothing has said when it ends.</param>
/// <param name="Member">The character it runs on, or null when the whole party carries it.</param>
public readonly record struct RunningSpellEffect(EffectId Effect, int Magnitude, GameDate? EndsAt, PartyMemberId? Member = null)
{
    /// <summary>Whether this effect runs on one character rather than on the whole party.</summary>
    public bool IsOnMember => Member is not null;
}

/// <summary>What spells have left on the party's own characters, as the place that reads one asks.</summary>
/// <remarks>
/// <para>
/// A ward cast on one character is read by that character's own answer — the resistance the fight sums for
/// them, the luck their saving throw uses, what their blow is worth — so the reader asks about a character
/// rather than about the party. The magnitude it answers with is the party's own state read under the
/// character's own entry, so there is one place the effect is held and one place a reader looks.
/// </para>
/// <para>
/// A character who no longer carries what a spell left — one the game has laid out — reads nothing, and the
/// effect ends where it is read, because nothing else in the product is told that a wound killed somebody.
/// </para>
/// </remarks>
public interface IMemberSpellEffects
{
    /// <summary>What one character carries of one effect, zero when nothing runs.</summary>
    /// <param name="member">The character to read.</param>
    /// <param name="effect">The effect definition to read.</param>
    /// <returns>The magnitude it acts at, or zero when the character does not carry it.</returns>
    int MagnitudeOn(PartyMember member, EffectId effect);

    /// <summary>The effects running on the party's characters, in the order they were applied.</summary>
    IReadOnlyList<RunningSpellEffect> RunningOnMembers { get; }
}

/// <summary>
/// The effects spells have left running, each with the deadline the one clock ends it at.
/// </summary>
/// <remarks>
/// <para>
/// <b>A duration is a deadline on the clock, never a count in a step.</b> A wand of light, a ward against
/// fire, and a charm that lasts an hour all leave the same shape of state: a carried effect, which is where a
/// buff already lives, and a deadline registered with the session's one clock. The effect ends when the clock
/// reaches that deadline — in whatever update or journey carried it there — so a party that stands still and
/// one that crosses the world lose the ward at the same moment. Nothing here counts updates, frames, or
/// seconds of its own.
/// </para>
/// <para>
/// <b>The party holds the effect; this holds when it ends.</b> The magnitude is applied through
/// <see cref="PartyEffects"/>, the owner every reader of a carried effect already consults, so a game's
/// combat readings, its dispelling, and its panel all see the same state. What is kept here is only the
/// deadline and the reading a panel needs: which effects are running, on whom, and when each one lapses.
/// </para>
/// <para>
/// <b>One character's effect is that character's own entry.</b> An effect a casting aimed at one character is
/// written into the party's effect state under an identity that names them, so the fight's reading of that
/// character finds it and no other member carries it; the party-wide entries are unchanged, which is what
/// keeps one owner of the magnitudes rather than a second store beside it. Because that state is what a save
/// already records, a spell's effects travel in the party the way every other party-carried effect does — see
/// <see cref="RunningSpellEffects"/> for what a save does to the deadline itself.
/// </para>
/// <para>
/// <b>A session with no clock is not a session without effects.</b> A deadline can only be registered with a
/// clock, so an effect applied without one lasts until something removes it, and its end reads as unknown
/// rather than as a moment nobody can compute. That is the honest answer for a product running without time
/// — and the effect is still carried, read, and dispelled.
/// </para>
/// </remarks>
public sealed class RunningSpellEffects : IGameTimeObserver, IRunningSpellEffects, IMemberSpellEffects
{
    private readonly PartyEntity _party;
    private readonly GameClock? _clock;
    private readonly Func<PartyMember, bool>? _carries;
    private readonly List<Held> _held = [];

    /// <summary>Creates the effects a party carries.</summary>
    /// <param name="party">The party whose effects are the state every reader already consults.</param>
    /// <param name="clock">
    /// The session's one clock, which a duration is registered against, or null when the session keeps no
    /// time. Without one an effect lasts until it is removed.
    /// </param>
    /// <param name="carries">
    /// Whether a character still carries what a spell left on them, as the game answers it, or null when the
    /// game states nothing that ends an effect early. The kit names no condition, so a game that lays a
    /// character out hands that answer here: a character it reports as no longer carrying their effects loses
    /// them where the ledger next reads or advances, which is death ending a duration rather than a ward
    /// standing over a body.
    /// </param>
    /// <exception cref="ArgumentNullException">No party was supplied.</exception>
    public RunningSpellEffects(PartyEntity party, GameClock? clock = null, Func<PartyMember, bool>? carries = null)
    {
        _party = party ?? throw new ArgumentNullException(nameof(party));
        _clock = clock;
        _carries = carries;
    }

    /// <summary>The party whose effects this state is about.</summary>
    public PartyEntity Party => _party;

    /// <summary>The effects a spell has left running on the party itself, in the order they were applied.</summary>
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
                if (held.Member is not null) continue;
                if (_party.Effects.Has(held.Effect)) running.Add(new RunningSpellEffect(held.Effect, held.Magnitude, held.EndsAt));
            }

            return running;
        }
    }

    /// <summary>
    /// The effects running on the party's characters, read from the state that holds them.
    /// </summary>
    /// <remarks>
    /// This is derived from the party's own effect entries rather than from what this ledger started, so what
    /// a panel shows and what a fight reads are the same state: an effect the party carries under a
    /// character's identity is reported even when the ledger that applied it was rebuilt — a session resumed
    /// after the effect was written carries it, and a panel that showed nothing while the fight read it would
    /// be two answers about one ward. The moment each one ends is the clock's, and reads as unknown where
    /// nothing stated one.
    /// </remarks>
    public IReadOnlyList<RunningSpellEffect> RunningOnMembers
    {
        get
        {
            List<RunningSpellEffect> running = [];
            foreach (PartyEffect effect in _party.Effects.Active)
            {
                if (MemberSpellEffectIds.Split(effect.Effect) is not { } on) continue;

                // What a character no longer carries ends here, where their own state is in hand.
                if (_party.TryMember(on.Member, out PartyMember? member) && member is not null && !Carries(member)) continue;
                GameDate? endsAt = null;
                foreach (Held held in _held)
                {
                    if (held.Member == on.Member && held.Effect == on.Effect) endsAt = held.EndsAt;
                }

                running.Add(new RunningSpellEffect(on.Effect, effect.Magnitude, endsAt, on.Member));
            }

            return running;
        }
    }

    /// <summary>Whether an effect a spell applied to the party is still running.</summary>
    /// <param name="effect">The effect to look for.</param>
    public bool IsRunning(EffectId effect) => _party.Effects.Has(effect);

    /// <summary>Whether an effect a spell applied to one character is still running on them.</summary>
    /// <param name="member">The character to look at.</param>
    /// <param name="effect">The effect to look for.</param>
    public bool IsRunningOn(PartyMemberId member, EffectId effect) =>
        _party.Effects.Has(MemberSpellEffectIds.For(member, effect));

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

    /// <inheritdoc />
    /// <remarks>
    /// A character who no longer carries what a spell left reads nothing, and the effects end here: their own
    /// entries are taken out of the party's state and their deadlines dropped, so a fight reading their
    /// resistance, a panel listing what runs, and a dispelling all see the same absence.
    /// </remarks>
    public int MagnitudeOn(PartyMember member, EffectId effect)
    {
        ArgumentNullException.ThrowIfNull(member);
        if (!Carries(member))
        {
            EndAll(member.Id);
            return 0;
        }

        return _party.Effects.MagnitudeOf(MemberSpellEffectIds.For(member.Id, effect));
    }

    /// <summary>
    /// Leaves an effect running on the party, ending it when the clock reaches the end of its duration.
    /// </summary>
    /// <remarks>
    /// Applying an effect that is already running replaces its magnitude and its deadline rather than
    /// stacking a second copy: two castings of one ward are one ward at the stronger reading, which is what
    /// the party's own effects owner already does with a carried effect.
    /// </remarks>
    /// <param name="effect">Which effect the spell leaves.</param>
    /// <param name="magnitude">The magnitude it acts at.</param>
    /// <param name="lasts">How long it lasts, or null when nothing states an end.</param>
    /// <returns>What is running under that identity after the call.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A stated duration is no time at all.</exception>
    public RunningSpellEffect Start(EffectId effect, int magnitude, GameDuration? lasts) =>
        HoldEffect(member: null, effect, magnitude, lasts);

    /// <summary>
    /// Leaves an effect running on one character, with its own deadline, as a spell aimed at them does.
    /// </summary>
    /// <remarks>
    /// The character must be one the party holds, because the effect is written into the party's own state
    /// under their identity: an effect on somebody who is not in the band would be state nothing could ever
    /// read or end.
    /// </remarks>
    /// <param name="member">The character the spell was cast on.</param>
    /// <param name="effect">Which effect the spell leaves.</param>
    /// <param name="magnitude">The magnitude it acts at.</param>
    /// <param name="lasts">How long it lasts, or null when nothing states an end.</param>
    /// <returns>What is running on that character after the call.</returns>
    /// <exception cref="ArgumentNullException">No character was supplied.</exception>
    /// <exception cref="ArgumentException">The party holds no such character.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A stated duration is no time at all.</exception>
    public RunningSpellEffect StartOn(PartyMember member, EffectId effect, int magnitude, GameDuration? lasts)
    {
        ArgumentNullException.ThrowIfNull(member);
        if (!_party.TryMember(member.Id, out _))
        {
            throw new ArgumentException(
                $"The party holds no member {member.Id}, so an effect on them would be state nothing could read or end.",
                nameof(member));
        }

        return HoldEffect(member.Id, effect, magnitude, lasts);
    }

    /// <summary>Ends one effect a spell left running on the party.</summary>
    /// <param name="effect">The effect to end.</param>
    /// <returns>Whether it was running.</returns>
    public bool End(EffectId effect)
    {
        bool wasHeld = Drop(effect, member: null);
        return _party.Effects.Remove(effect) || wasHeld;
    }

    /// <summary>Ends one effect a spell left running on one character.</summary>
    /// <param name="member">The character whose effect ends.</param>
    /// <param name="effect">The effect to end.</param>
    /// <returns>Whether it was running.</returns>
    public bool EndOn(PartyMemberId member, EffectId effect)
    {
        bool wasHeld = Drop(effect, member);
        return _party.Effects.Remove(MemberSpellEffectIds.For(member, effect)) || wasHeld;
    }

    /// <summary>Ends every effect a spell left running on one character, and says which ones those were.</summary>
    /// <param name="member">The character whose effects end.</param>
    /// <returns>The effects that were running on them, in the order they were applied.</returns>
    public IReadOnlyList<RunningSpellEffect> EndAll(PartyMemberId member)
    {
        List<RunningSpellEffect> running = [];
        foreach (RunningSpellEffect effect in RunningOnMembers)
        {
            if (effect.Member == member) running.Add(effect);
        }

        foreach (RunningSpellEffect effect in running) _party.Effects.Remove(MemberSpellEffectIds.For(member, effect.Effect));
        for (int index = _held.Count - 1; index >= 0; index--)
        {
            if (_held[index].Member != member) continue;
            if (_held[index].Deadline is { } deadline) _clock?.Cancel(deadline);
            _held.RemoveAt(index);
        }

        return running;
    }

    /// <summary>Ends every effect a spell left running, and says which ones those were.</summary>
    /// <remarks>
    /// Only what a spell applied is ended: a passage bought at a counter and a guild membership are
    /// carried effects too, and they are not magic — a dispelling that took them would sell a player a
    /// ticket and burn it. What this ends is exactly what was started here, on the party and on its
    /// characters alike.
    /// </remarks>
    /// <returns>The effects that were running, in the order they were applied.</returns>
    public IReadOnlyList<RunningSpellEffect> EndAll()
    {
        List<RunningSpellEffect> running = [.. Running, .. RunningOnMembers];
        foreach (RunningSpellEffect effect in running)
        {
            _party.Effects.Remove(effect.Member is { } member
                ? MemberSpellEffectIds.For(member, effect.Effect)
                : effect.Effect);
        }

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
    /// ledger, or a rule that dropped the effect another way, leaves state that must not run on. A character
    /// the game has laid out loses their effects here too, so a death the clock outlives is still a death.
    /// </remarks>
    /// <param name="advance">Where the clock was, where it went, and what it brought due.</param>
    /// <exception cref="ArgumentNullException">No advance was supplied.</exception>
    public void Observe(ClockAdvance advance)
    {
        ArgumentNullException.ThrowIfNull(advance);
        EndWhatDeathTook();
        if (advance.Due.Count == 0) return;
        HashSet<long> due = [.. advance.Due.Select(deadline => deadline.Deadline.Value)];
        foreach (Held held in _held.ToArray())
        {
            if (held.Deadline is not { } deadline || !due.Contains(deadline.Value)) continue;
            _held.Remove(held);
            _party.Effects.Remove(held.Member is { } member
                ? MemberSpellEffectIds.For(member, held.Effect)
                : held.Effect);
        }
    }

    /// <summary>Whether one character still carries what a spell left on them.</summary>
    private bool Carries(PartyMember member) => _carries?.Invoke(member) ?? true;

    /// <summary>Ends what every character the game has laid out was carrying.</summary>
    private void EndWhatDeathTook()
    {
        if (_carries is null) return;
        foreach (PartyMember member in _party.Members)
        {
            if (!Carries(member)) EndAll(member.Id);
        }
    }

    /// <summary>Applies one effect to the party's own state and records the deadline it ends at.</summary>
    private RunningSpellEffect HoldEffect(PartyMemberId? member, EffectId effect, int magnitude, GameDuration? lasts)
    {
        if (lasts is { IsNone: true })
        {
            throw new ArgumentOutOfRangeException(
                nameof(lasts),
                lasts,
                "A spell effect that lasts no time at all would end in the advance that applied it; a duration is a length of game time.");
        }

        EffectId held = member is { } on ? MemberSpellEffectIds.For(on, effect) : effect;
        Drop(effect, member);
        GameDate? endsAt = null;
        DeadlineId? deadline = null;
        if (lasts is { } duration && _clock is { } clock)
        {
            deadline = clock.ScheduleAfter(duration);
            endsAt = clock.Calendar.Add(clock.Now, duration);
        }

        _party.Effects.Apply(new PartyEffect(held, magnitude));
        _held.Add(new Held(effect, member, magnitude, deadline, endsAt));
        return new RunningSpellEffect(effect, magnitude, endsAt, member);
    }

    /// <summary>Forgets one held effect and cancels the deadline it was registered under.</summary>
    private bool Drop(EffectId effect, PartyMemberId? member)
    {
        int index = _held.FindIndex(held => held.Effect == effect && held.Member == member);
        if (index < 0) return false;
        if (_held[index].Deadline is { } deadline) _clock?.Cancel(deadline);
        _held.RemoveAt(index);
        return true;
    }

    /// <summary>One effect this ledger started, with the deadline the clock ends it at.</summary>
    /// <param name="Effect">Which effect definition is running.</param>
    /// <param name="Member">The character it runs on, or null when the whole party carries it.</param>
    /// <param name="Magnitude">The magnitude it acts at.</param>
    /// <param name="Deadline">The clock's own handle, or null when no clock was given one.</param>
    /// <param name="EndsAt">When it ends, or null when nothing stated an end.</param>
    private readonly record struct Held(EffectId Effect, PartyMemberId? Member, int Magnitude, DeadlineId? Deadline, GameDate? EndsAt);
}

/// <summary>The effects a spell has left running, as a panel and a reader of the state ask for them.</summary>
/// <remarks>
/// It is deliberately the whole of what a reader outside the effect path may know about durations: which
/// effects are running, how strongly, and when each one lapses. A mechanism that had to reach the ledger
/// itself would be a second owner of the same deadlines.
/// </remarks>
public interface IRunningSpellEffects
{
    /// <summary>The effects a spell has running on the party itself, in the order they were applied.</summary>
    IReadOnlyList<RunningSpellEffect> Running { get; }

    /// <summary>Whether an effect a spell applied is still running.</summary>
    /// <param name="effect">The effect to look for.</param>
    bool IsRunning(EffectId effect);
}

/// <summary>
/// The identities a per-character effect is held under in the party's own effect state.
/// </summary>
/// <remarks>
/// <para>
/// One spelling, used by the ledger that writes the entry and by nothing else, so a character's effects are
/// the party's effects filed under that character and there is no second store of magnitudes beside the
/// party's. The name is derived rather than written twice: what a casting applies, what a fight reads, and
/// what a panel lists are the same entry.
/// </para>
/// <para>
/// <b>The marker is deliberately not a content name.</b> An effect identity belongs to the game's own
/// content, so a character's entry is that identity with the character appended after a marker no content
/// identity would carry — the same shape a spell's own carried state uses for the place a beacon stands in.
/// </para>
/// </remarks>
internal static class MemberSpellEffectIds
{
    /// <summary>The marker that separates an effect's own identity from the character it runs on.</summary>
    private const char Marker = '@';

    /// <summary>The identity one character's effect is held under.</summary>
    /// <param name="member">The character the effect runs on.</param>
    /// <param name="effect">The effect's own identity.</param>
    internal static EffectId For(PartyMemberId member, EffectId effect) =>
        new(string.Concat(effect.Value, Marker.ToString(), member.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)));

    /// <summary>The character and effect an identity stands for, or null when it names no character's effect.</summary>
    /// <param name="effect">The identity to read.</param>
    internal static (PartyMemberId Member, EffectId Effect)? Split(EffectId effect)
    {
        int at = effect.Value.LastIndexOf(Marker);
        if (at <= 0 || at == effect.Value.Length - 1) return null;
        if (!ulong.TryParse(
                effect.Value.AsSpan(at + 1),
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out ulong value) ||
            value == 0)
        {
            return null;
        }

        return (new PartyMemberId(value), new EffectId(effect.Value[..at]));
    }
}
