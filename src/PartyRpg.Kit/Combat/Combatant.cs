using PartyRpg.Kit.Time;

namespace PartyRpg.Kit.Combat;

/// <summary>
/// One actor in a fight, with the pacing state that decides when it may act again.
/// </summary>
/// <remarks>
/// <para>
/// A combatant is the fight's own state over an actor that belongs to somebody else: the party owns the
/// member, its resources, and its conditions, the place's population owns the entity, and the creature's own
/// health is a component on that entity. This holds what a fight adds — which side the actor is on, how long
/// until it may act, and where it stood when the world was last read — and duplicates none of the rest,
/// which is why a member's health, equipment, and skills and a creature's health are all read from their own
/// owners through <see cref="Subject"/> rather than copied beside it.
/// </para>
/// <para>
/// <b><see cref="Recovery"/> is the one pacing quantity, and it is game time.</b> It is advanced by the
/// session's one clock inside the admitted update and by nothing else — not by an update that admitted no
/// time, not by a paused session, and never by a frame or a timer — so the same state with the same clock
/// gives the same fight. A recovering actor cannot act: every attack initiation is judged against this value
/// before anything else happens.
/// </para>
/// <para>
/// <b>Its contract for the second pacing.</b> When turn-based mode is composed over this same state, the
/// order actors act in is derived from this quantity: ascending remaining recovery, which is the recovery
/// time the actor's own ruleset answer states. Two things follow, and a later stone may rely on them:
/// a ready actor's recovery is exactly zero, so "ready" and "elapsed" are one fact rather than two; and the
/// quantity is a length of game time, so it is comparable and orderable without any unit of its own. How
/// actors whose recovery has all elapsed are ordered among themselves is that mode's decision, not this
/// type's, and no ordering is implied here.
/// </para>
/// </remarks>
public sealed class Combatant
{
    private GameDuration _recovery;

    internal Combatant(
        CombatSubject subject,
        CombatSide side,
        string name,
        AttackKind preferredKind,
        double distance,
        GameDuration recovery)
    {
        Subject = subject;
        Side = side;
        Name = name;
        PreferredKind = preferredKind;
        Distance = distance;
        _recovery = recovery;
    }

    /// <summary>The identity this actor is a combatant under.</summary>
    public CombatantId Id => Subject.Id;

    /// <summary>The actor itself: the party member or the world entity a ruleset reads.</summary>
    public CombatSubject Subject { get; }

    /// <summary>What the actor is called, as the ruleset named it when the world was last read.</summary>
    public string Name { get; private set; }

    /// <summary>Which side of the fight the actor is on, as the world was last read.</summary>
    public CombatSide Side { get; private set; }

    /// <summary>How this actor attacks when it acts on its own, as the ruleset answered.</summary>
    public AttackKind PreferredKind { get; private set; }

    /// <summary>
    /// How far the actor stood from the party when the world was last read, in the place's own units.
    /// </summary>
    public double Distance { get; private set; }

    /// <summary>How much game time must pass before the actor may act again.</summary>
    public GameDuration Recovery => _recovery;

    /// <summary>Whether the actor may act now, which is true exactly when its recovery has elapsed.</summary>
    public bool IsReady => _recovery.IsNone;

    /// <summary>Charges one action's recovery, which is how an actor becomes unable to act.</summary>
    /// <param name="recovery">What the action costs, which the ruleset answered.</param>
    internal void Spend(GameDuration recovery) => _recovery = recovery;

    /// <summary>Advances the recovery by game time that has passed, never past ready.</summary>
    /// <param name="elapsed">The game time the clock moved by.</param>
    internal void Recover(GameDuration elapsed)
    {
        if (elapsed.IsNone || _recovery.IsNone) return;
        long remaining = _recovery.Milliseconds - elapsed.Milliseconds;
        _recovery = GameDuration.FromMilliseconds(remaining > 0 ? remaining : 0);
    }

    /// <summary>Records what the world says about the actor now, which the fight re-reads every update.</summary>
    internal void Observe(CombatSide side, string name, AttackKind preferredKind, double distance)
    {
        Side = side;
        Name = name;
        PreferredKind = preferredKind;
        Distance = distance;
    }

    /// <summary>
    /// Puts the actor into the fight, which is what being attacked does to a creature that was not in one.
    /// </summary>
    /// <remarks>
    /// The fight's remembered provocation is what makes this stick — the next read of the world would
    /// otherwise find a peaceful creature peaceful again — so this only spares the projection from showing an
    /// enemy as neutral until the next update.
    /// </remarks>
    internal void Provoke() => Side = CombatSide.Opposition;

    /// <inheritdoc />
    public override string ToString() =>
        $"{Name} ({Id}) is {Side} with {_recovery.Milliseconds}ms of recovery left";
}
