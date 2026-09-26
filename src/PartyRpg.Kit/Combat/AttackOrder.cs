using PartyRpg.Kit.Time;

namespace PartyRpg.Kit.Combat;

/// <summary>
/// One actor's decision to attack, as whoever drives that actor stated it.
/// </summary>
/// <remarks>
/// <para>
/// An order is a value and not a permission: it says who acts, how, and against what, and the fight decides
/// whether the actor may — a recovering actor's order is refused by name and nothing happens. Nothing here
/// resolves anything either: what an attack does to its target is the ruleset's, and the record of an order
/// that was accepted is an <see cref="AttackInitiation"/>.
/// </para>
/// <para>
/// <b>Anybody can order any combatant.</b> The player's control orders the party's members and a later AI
/// owner orders the creatures it drives, through this same type: the fight does not care who asked, which is
/// what keeps a monster's pacing and a character's pacing one mechanism instead of two.
/// </para>
/// </remarks>
/// <param name="Actor">The combatant that acts.</param>
/// <param name="Kind">How it attacks.</param>
/// <param name="Target">
/// What it attacks, or null when there is nothing to attack: an actor with nothing in reach still acts, and
/// an attack at nothing is a real outcome rather than a refused order.
/// </param>
/// <param name="Ability">
/// Which of the actor's own ways of attacking this is, when whoever ordered it wants a particular one — a
/// creature's second attack, one of its spells — named in the ruleset's own vocabulary and handed back to
/// it untouched. The kit never learns one of these names and never branches on one: an order naming none is
/// resolved by whatever the ruleset answers for the kind alone, which is what every order gives today.
/// </param>
public sealed record AttackOrder(CombatantId Actor, AttackKind Kind, CombatantId? Target, string? Ability = null);

/// <summary>
/// What one accepted attack was, as the fight initiated it.
/// </summary>
/// <remarks>
/// <para>
/// This is the whole of what an attack is until something resolves it: who acted, how, against what, when,
/// and what the attempt cost in recovery. It is deliberately a value rather than a callback, because
/// resolution is not this mechanism's work — the damage, the resistance, the conditions, and the death that
/// follow an attack are the ruleset's, and they arrive by consuming this record rather than by the fight
/// growing a branch per effect.
/// </para>
/// <para>
/// <see cref="Target"/> and <see cref="TargetName"/> are both empty for an attack at nothing, which is a
/// real outcome and not a failure: an actor with no target in reach has still acted and still pays for it.
/// </para>
/// </remarks>
/// <param name="Actor">The combatant that acted.</param>
/// <param name="ActorName">What that actor is called, as the ruleset named it.</param>
/// <param name="Kind">How it attacked.</param>
/// <param name="Target">What it attacked, or null when it attacked nothing.</param>
/// <param name="TargetName">What the target is called, empty when there was none.</param>
/// <param name="At">When on the game calendar it happened, or null when the session keeps no clock.</param>
/// <param name="Recovery">How much game time the actor must now recover before it may act again.</param>
/// <param name="Ability">
/// Which of the actor's own ways of attacking this was, as the order named it, empty when it named none. It
/// is what makes a creature's second attack and its spells readable as themselves rather than as more of the
/// same blow.
/// </param>
public sealed record AttackInitiation(
    CombatantId Actor,
    string ActorName,
    AttackKind Kind,
    CombatantId? Target,
    string TargetName,
    GameDate? At,
    GameDuration Recovery,
    string Ability = "")
{
    /// <summary>Whether the attack was aimed at anything.</summary>
    public bool HasTarget => Target is not null;
}
