namespace PartyRpg.Kit.Combat;

/// <summary>
/// A ruleset's answer about one particular way an actor attacks, when an order named one.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is a second seam.</b> Most attacks need no name: a character swings, and what a swing is
/// worth is the ruleset's answer for the kind. A creature is different — its own table states more than one
/// way for it to hurt somebody, a second attack with its own dice and its own kind of harm, and spells with
/// their own kinds — and which of them it uses is a decision made before the blow is rolled. The order
/// therefore carries the name of the way it chose, and this answers what that way is worth. A ruleset that
/// answers no abilities keeps resolving every attack from its kind, which is what a game without a second
/// attack does.
/// </para>
/// <para>
/// <b>The name is the ruleset's own.</b> The kit passes it from the order to here without reading it,
/// comparing it, or keeping a list of them: a name is data about a game's own attack table, and the kit
/// learns nothing from it beyond that something asked for it. That is the same discipline the rest of the
/// combat seams keep — the mechanism decides when a question is asked, and the ruleset answers it.
/// </para>
/// </remarks>
public interface ICombatAbilityResolutionRule
{
    /// <summary>What one named way of attacking is worth against a target.</summary>
    /// <param name="attacker">The actor making the attack.</param>
    /// <param name="target">The actor it is aimed at.</param>
    /// <param name="kind">How the attack is made, which the order also stated.</param>
    /// <param name="ability">Which of the attacker's ways of attacking this is, as the order named it.</param>
    /// <returns>The chance, the damage, the kind of harm, and what the target resists.</returns>
    AttackPlan PlanOfAbility(CombatSubject attacker, CombatSubject target, AttackKind kind, string ability);
}
