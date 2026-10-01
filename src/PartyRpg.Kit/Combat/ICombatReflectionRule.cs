namespace PartyRpg.Kit.Combat;

/// <summary>
/// A ruleset's answer about harm a wound turns back onto whoever dealt it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why it is its own seam.</b> Most wounds are one-way: a blow lands and the target's own state answers for
/// it. A game may let a target carry something that sends what it took back at the attacker — a ward, a thorned
/// hide — and that is a question asked after the harm has landed, about two actors at once, which none of the
/// resolution's other answers can state. A game that answers nothing here fights with wounds that turn nothing
/// back.
/// </para>
/// <para>
/// <b>The fight applies what this answers, through the attacker's own health.</b> The answer is the harm that
/// lands on the attacker after its own defences, so an attacker immune to what was turned back takes nothing,
/// and a reflection that brings the attacker down is a death every observer hears exactly as it hears a death
/// a blow caused.
/// </para>
/// </remarks>
public interface ICombatReflectionRule
{
    /// <summary>What a landed wound turns back onto the actor that dealt it, after that actor's own defences.</summary>
    /// <param name="attacker">The actor that landed the hit.</param>
    /// <param name="target">The actor that took it.</param>
    /// <param name="kind">What kind of harm the hit did.</param>
    /// <param name="harm">How much harm landed on the target.</param>
    /// <param name="rolls">The attack's rolls, for a reflection whose own defences are checked rather than certain.</param>
    /// <returns>The harm that lands on the attacker, zero when nothing is turned back.</returns>
    int ReflectedOnto(CombatSubject attacker, CombatSubject target, DamageKindId kind, int harm, IAttackRolls rolls);
}
