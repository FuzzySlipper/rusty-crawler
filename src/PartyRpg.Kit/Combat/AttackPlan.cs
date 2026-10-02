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
/// <param name="Additional">Further damage kinds, each checked against its own resistance on the same hit.</param>
/// <param name="Divisor">
/// What the rolled harm is divided by before the target's resistance has its say, one when nothing divides it:
/// a defence that turns part of a blow aside — a ward against missiles, an attacker made smaller — is a fact
/// about this attack and this target, which is why the ruleset states it here rather than inside the dice.
/// </param>
public sealed record AttackPlan(HitChance Chance, DamageKindId Kind, DamageRoll Damage, Resistance Resistance, int Divisor = 1, IReadOnlyList<AttackDamagePart>? Additional = null)
{
    /// <summary>What the rolled harm is divided by, which is never less than one.</summary>
    public int Divisor { get; } = Divisor >= 1
        ? Divisor
        : throw new ArgumentOutOfRangeException(
            nameof(Divisor),
            Divisor,
            "A blow's harm is divided by one or more; a divisor below one would multiply what a defence was stated to turn aside.");
}
