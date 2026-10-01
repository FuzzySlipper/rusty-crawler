using PartyRpg.Kit.Party;
using PartyRpg.Kit.Time;
using Rusty.Engine.Entities;

namespace PartyRpg.Kit.Combat;

/// <summary>One effect running on a creature: what it is, what it is worth, and how much game time it has left.</summary>
/// <param name="Effect">The effect's identity, which the game that left it reads.</param>
/// <param name="Magnitude">What it is worth, in the game's own terms.</param>
/// <param name="Remaining">How much game time is left before it ends.</param>
public readonly record struct CreatureEffect(EffectId Effect, int Magnitude, GameDuration Remaining);

/// <summary>
/// What spells have left on one creature — a paralysis, a slowing, a fear, a charm — held on the creature itself.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the creature's own state, not the fight's.</b> A creature a spell paralysed is paralysed whoever is
/// looking at it: the fight's gate, the creature's own decisions, and the panel all read the same component, so
/// it is attached to the creature's engine actor where its health already lives, and it goes with the creature
/// when the visit that made it ends. The identities and the magnitudes are the game's own; nothing here knows
/// what a paralysis is.
/// </para>
/// <para>
/// <b>An effect runs out with the creature's own recovery.</b> Its length is counted down by the advances the
/// session's one clock hands the fight, exactly as a creature's recovery is, so a creature that is slowed for a
/// minute stops being slowed when a minute of game time has passed, in real time and in a paced fight alike,
/// and nothing here keeps a timer of its own. A creature's effects are not carried by a save, because the
/// creature itself is rebuilt from its placement on load; the fight a save is refused over names them.
/// </para>
/// </remarks>
public sealed class CreatureEffects
{
    private readonly List<CreatureEffect> _running = [];

    /// <summary>The effects running on the creature, in the order they were left.</summary>
    public IReadOnlyList<CreatureEffect> Active => _running;

    /// <summary>Whether any effect runs on the creature.</summary>
    public bool Any => _running.Count > 0;

    /// <summary>What one effect is worth on the creature, zero when it does not run.</summary>
    /// <param name="effect">The effect to read.</param>
    public int MagnitudeOf(EffectId effect)
    {
        foreach (CreatureEffect running in _running)
        {
            if (running.Effect == effect) return running.Magnitude;
        }

        return 0;
    }

    /// <summary>Whether one effect runs on the creature.</summary>
    /// <param name="effect">The effect to look for.</param>
    public bool Has(EffectId effect) => _running.Any(running => running.Effect == effect);

    /// <summary>Leaves an effect on the creature for a length of game time, replacing one of the same identity.</summary>
    /// <param name="effect">The effect.</param>
    /// <param name="magnitude">What it is worth.</param>
    /// <param name="lasts">How long it runs, which must be some game time.</param>
    /// <exception cref="ArgumentOutOfRangeException">The length is no time at all.</exception>
    public void Apply(EffectId effect, int magnitude, GameDuration lasts)
    {
        if (lasts.IsNone)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lasts),
                lasts,
                "An effect on a creature that lasts no time at all would end in the advance that left it; a length is some game time.");
        }

        Remove(effect);
        _running.Add(new CreatureEffect(effect, magnitude, lasts));
    }

    /// <summary>Ends one effect on the creature.</summary>
    /// <param name="effect">The effect to end.</param>
    /// <returns>Whether it was running.</returns>
    public bool Remove(EffectId effect) => _running.RemoveAll(running => running.Effect == effect) > 0;

    /// <summary>Counts every effect down by game time that has passed, ending what runs out.</summary>
    /// <param name="elapsed">The game time the clock moved by.</param>
    internal void Elapse(GameDuration elapsed)
    {
        if (elapsed.IsNone) return;
        for (int index = _running.Count - 1; index >= 0; index--)
        {
            CreatureEffect running = _running[index];
            long left = running.Remaining.Milliseconds - elapsed.Milliseconds;
            if (left <= 0) _running.RemoveAt(index);
            else _running[index] = running with { Remaining = GameDuration.FromMilliseconds(left) };
        }
    }

    /// <summary>Attaches a creature's effects, which happens once, when the creature is placed.</summary>
    /// <param name="actor">The creature's entity.</param>
    /// <returns>The effects attached.</returns>
    /// <exception cref="InvalidOperationException">The creature already has effects attached.</exception>
    internal static CreatureEffects Attach(Actor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (actor.Has<CreatureEffects>())
        {
            throw new InvalidOperationException("The creature already carries its effects: they are attached once, when the creature is placed.");
        }

        CreatureEffects effects = new();
        actor.Add(effects);
        return effects;
    }

    /// <summary>The creature's own effects when it has any state for them, or null for something that is not a creature.</summary>
    /// <param name="actor">The creature's engine actor.</param>
    /// <returns>Its effects, or null.</returns>
    /// <exception cref="ArgumentNullException">No actor was supplied.</exception>
    public static CreatureEffects? Find(Actor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        return actor.TryGet(out CreatureEffects? effects) ? effects : null;
    }
}
