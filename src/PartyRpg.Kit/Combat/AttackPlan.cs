namespace PartyRpg.Kit.Combat;

/// <summary>
/// One attack's numbers against one target, as the ruleset stated them: whether it lands, what it rolls,
/// of what kind, and what the target resists.
/// </summary>
/// <remarks>
/// <para>
/// This is the answer a ruleset gives before anything is rolled. Every number in it is the ruleset's — a
/// chance, a handful of dice, a kind of harm, a resistance reading — and the fight's work is to roll them
/// and apply what comes out. Reading the numbers and rolling them apart is what lets a test state a certain
/// hit and a certain miss, and lets a panel say what the chance was when it did not land.
/// </para>
/// <para>
/// <b>The resistance is read before the roll, not after it.</b> A target that is immune takes nothing, and
/// that is a fact about the target rather than an outcome of arithmetic; a panel that showed only a final
/// number could not tell immunity from a good resistance check.
/// </para>
/// </remarks>
/// <param name="Chance">How likely the attack is to land.</param>
/// <param name="Kind">What kind of harm it does.</param>
/// <param name="Damage">What it rolls for harm.</param>
/// <param name="Resistance">What the target resists of that kind.</param>
public sealed record AttackPlan(HitChance Chance, DamageKindId Kind, DamageRoll Damage, Resistance Resistance);
