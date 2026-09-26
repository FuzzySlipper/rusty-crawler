using Rusty.Engine.Entities;

namespace PartyRpg.Kit.Combat;

/// <summary>
/// A creature's own health: how much harm it has taken and how much it can take, held on the creature
/// itself rather than by whoever is fighting it.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the health handoff.</b> A world actor has no pool of its own the way a party member has one,
/// so until something owned a creature's health a fight kept a tally of what it had done to one — a record
/// that lived exactly as long as the fight did and that nothing else could read. This is that record
/// replaced by the creature's own: it is a component on the entity's engine actor, attached the first time
/// a fight reads the creature, and it is read and written by anything holding the entity. A trap, a rest, a
/// kill count, and the panel therefore all read one quantity rather than each keeping its own opinion.
/// </para>
/// <para>
/// <b>It is a component and not a store.</b> The entity's actor is the engine's own entity, and attaching to
/// it is what the population's contract asks whoever owns a behavior to do. Nothing here allocates an
/// entity, steps anything, or survives the visit that made the creature: the population destroys the actor
/// and this goes with it.
/// </para>
/// <para>
/// <b>Zero maximum is a creature nobody can bring down.</b> A world actor a ruleset can state no hit points
/// for takes harm without ever reaching empty, which is the honest reading of a creature whose row the
/// content does not carry rather than a creature that dies at the first blow.
/// </para>
/// </remarks>
public sealed class CreatureHealth
{
    private CreatureHealth(int maximum)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximum);
        Maximum = maximum;
        Current = maximum;
    }

    /// <summary>How much harm the creature can take altogether.</summary>
    public int Maximum { get; }

    /// <summary>How much harm it has left to take.</summary>
    public int Current { get; private set; }

    /// <summary>Whether it can be brought down at all, which is false when nothing states its hit points.</summary>
    public bool IsMortal => Maximum > 0;

    /// <summary>Whether it is out of the fight: its harm has reached what it can take.</summary>
    /// <remarks>
    /// A creature nobody can bring down is never down, whatever has been done to it: a body that cannot be
    /// killed must not read as one that has been.
    /// </remarks>
    public bool IsDown => IsMortal && Current <= 0;

    /// <summary>How much of what the creature can take it has left, from zero to one.</summary>
    public double Fraction => IsMortal ? Math.Clamp(Current / (double)Maximum, 0, 1) : 0;

    /// <summary>Lands harm on the creature and reports whether this is what brought it down.</summary>
    /// <param name="damage">How much harm landed, which cannot be negative.</param>
    /// <returns>Whether the creature was standing before and is down now.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The harm is negative, which would heal rather than harm.</exception>
    public bool Wound(int damage)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(damage);
        if (damage == 0 || !IsMortal) return false;
        bool standing = !IsDown;
        Current = Math.Max(0, Current - damage);
        return standing && IsDown;
    }

    /// <summary>
    /// The creature's own health, attached to its actor the first time anything asks for it.
    /// </summary>
    /// <remarks>
    /// The maximum is only used when the component is attached: a creature that already has health keeps
    /// the health it has, so re-reading a fight cannot heal a wounded creature back to full and a ruleset
    /// that changed its answer mid-fight could not move the goalposts under a fight in progress.
    /// </remarks>
    /// <param name="actor">The creature's engine actor, which is the entity itself.</param>
    /// <param name="maximum">What the creature can take, as whoever owns the creature's numbers answered.</param>
    /// <returns>The creature's health, attached or already there.</returns>
    /// <exception cref="ArgumentNullException">No actor was supplied.</exception>
    public static CreatureHealth Of(Actor actor, int maximum)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (actor.TryGet(out CreatureHealth? existing) && existing is not null) return existing;

        CreatureHealth health = new(maximum);
        actor.Add(health);
        return health;
    }

    /// <summary>The creature's own health when it has any, or null when nothing has read it yet.</summary>
    /// <param name="actor">The creature's engine actor.</param>
    /// <returns>Its health, or null.</returns>
    /// <exception cref="ArgumentNullException">No actor was supplied.</exception>
    public static CreatureHealth? Find(Actor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        return actor.TryGet(out CreatureHealth? health) ? health : null;
    }

    /// <inheritdoc />
    public override string ToString() => IsMortal
        ? $"{Current}/{Maximum} hp{(IsDown ? " (down)" : string.Empty)}"
        : "unhurt and unhurtable";
}
