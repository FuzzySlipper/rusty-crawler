using PartyRpg.Kit.Time;

namespace PartyRpg.Kit.Combat;

/// <summary>
/// One actor's answer to "what is this thing, as far as a fight is concerned".
/// </summary>
/// <remarks>
/// <para>
/// The kit owns the mechanism and the ruleset owns the meaning, so every question the mechanism asks about
/// an actor is asked here. A ruleset reads whatever it can of the subject — a party member's class, skills,
/// and attributes; a world entity's placement, its content entry, and the components attached to its engine
/// actor — and answers in the kit's own words. Nothing in the kit learns a monster, a skill, or an armour
/// name from these answers.
/// </para>
/// <para>
/// <b>The names are read as display text, not parsed.</b> A name is what the projection publishes and a
/// diagnostic reports, so it may be anything a ruleset wants a person to read; nothing in the kit branches
/// on it.
/// </para>
/// </remarks>
public interface ICombatRule
{
    /// <summary>What this actor is called, as a person reads it on the panel.</summary>
    /// <param name="subject">The actor to name.</param>
    string NameOf(CombatSubject subject);

    /// <summary>
    /// What this actor is by nature: whether it is a creature that can fight at all, and whether it attacks
    /// the party on sight.
    /// </summary>
    /// <remarks>
    /// This is the "what it is" half of hostility. The "what the party has done" half is the fight's own
    /// state, so an answer of <see cref="Hostility.Peaceful"/> does not mean the actor is harmless — it means
    /// nothing about it makes it an enemy yet.
    /// </remarks>
    /// <param name="subject">The actor to judge.</param>
    Hostility NatureOf(CombatSubject subject);

    /// <summary>Which kind of attack this actor makes when it acts on its own.</summary>
    /// <param name="subject">The actor about to act.</param>
    AttackKind AttackKindFor(CombatSubject subject);

    /// <summary>
    /// How long this actor must recover after one attack of the given kind, which is the quantity that
    /// decides when it may act again.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the one pacing quantity of a fight, and it is a length of <b>game time</b>: recovery is
    /// advanced from the session's one clock inside the admitted update, exactly as a deadline or a
    /// schedule is, so nothing counts frames and a held session recovers nobody.
    /// </para>
    /// <para>
    /// <b>The turn-based contract.</b> When a second pacing is composed over this state, its initiative
    /// order is derived from this same quantity — the order actors act in is the recovery that paces them in
    /// real time — so an actor's recovery must be a total, comparable length: the same actor and the same
    /// kind must answer the same value on every call, and a value must never depend on how much of it has
    /// already elapsed. See <see cref="Combatant.Recovery"/> for what a reader of that order may assume.
    /// </para>
    /// </remarks>
    /// <param name="subject">The actor that would attack.</param>
    /// <param name="kind">How it would attack.</param>
    GameDuration RecoveryAfter(CombatSubject subject, AttackKind kind);

    /// <summary>
    /// How long this actor must recover the first time it is seen in a fight, before it has acted at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Separate from <see cref="RecoveryAfter"/> because a fight's first moment is not its steady state: an
    /// actor that has just noticed the party is not owed the full recovery of an attack it has not made, and
    /// a group placed together must not strike on the same update forever. This is the one place a fight's
    /// pacing may involve randomness, which is why the actor's identity is handed over with it: a ruleset
    /// that draws must key its draw on the actor, not on the order it happened to be asked in, so that the
    /// same state produces the same fight.
    /// </para>
    /// <para>
    /// A party member begins ready: nothing about standing in a place is a reason for a character to be
    /// unable to act.
    /// </para>
    /// </remarks>
    /// <param name="subject">The actor seen for the first time.</param>
    /// <param name="kind">How it would attack.</param>
    /// <returns>How long it must recover before its first action.</returns>
    GameDuration InitialRecovery(CombatSubject subject, AttackKind kind);

    /// <summary>
    /// How far this actor can reach with the given kind of attack, in the place's own units, which is how a
    /// target is chosen.
    /// </summary>
    /// <param name="subject">The actor that would attack.</param>
    /// <param name="kind">How it would attack.</param>
    /// <returns>The reach, in the units the place's positions are stated in.</returns>
    double ReachOf(CombatSubject subject, AttackKind kind);
}
