using Rusty.Engine;

namespace PartyRpg.Kit.Combat;

/// <summary>Where one attack's rolls come from, as the mechanism that resolves it asks for them.</summary>
/// <remarks>
/// <para>
/// Rolling is the kit's work and drawing is whoever owns randomness: an attack's hit roll, its damage dice,
/// and whatever checks its resistance needs are all drawn through this one object, so every draw of one
/// attack is made under keys that name that attack and nothing else. Two attacks can therefore never share
/// a draw by accident, and the same state replayed produces the same fight.
/// </para>
/// <para>
/// <b>Who supplies it.</b> A ruleset hands the fight one <see cref="IAttackRolls"/> per attack from
/// <see cref="ICombatResolutionRule.RollsFor"/>, because the ruleset is what holds the engine's random
/// service and states the seed and scope its draws live under. The kit never reaches for randomness itself.
/// </para>
/// </remarks>
public interface IAttackRolls
{
    /// <summary>Draws one value uniformly from a stated range, for a stated purpose.</summary>
    /// <param name="purpose">
    /// What the draw is for. Two draws of one attack must be asked for under different purposes, which is
    /// what keeps a hit roll from being the damage roll of the same swing.
    /// </param>
    /// <param name="minimum">The least value the draw may be.</param>
    /// <param name="maximum">The most value the draw may be, which must not lie below the least.</param>
    /// <returns>The drawn value, inside the range.</returns>
    int Roll(string purpose, int minimum, int maximum);
}

/// <summary>
/// One attack's rolls, drawn from the engine's keyed random service under a key that names the attack.
/// </summary>
/// <remarks>
/// <para>
/// The engine takes an explicit seed and reads no wall clock, so a fight's randomness is reproducible: the
/// key is the attack's own name within the fight, the purpose separates the draws of that attack, and the
/// seed and the scope are the ruleset's. Nothing is recorded for a save — a fight is rebuilt from the world
/// it stands in — and the same fight replayed draws the same values.
/// </para>
/// <para>
/// <b>A product that cannot draw gets no rolls at all.</b> <see cref="ICombatResolutionRule.RollsFor"/>
/// answers null when the product has no random service, and the fight then resolves nothing rather than
/// resolving against a value it invented; that a product cannot roll is a fact its composition states.
/// </para>
/// </remarks>
public sealed class AttackRolls : IAttackRolls
{
    private readonly IRandomService _random;
    private readonly ulong _seed;
    private readonly string _scope;
    private readonly string _key;

    /// <summary>Creates the rolls of one attack.</summary>
    /// <param name="random">The engine's random service, which draws the values.</param>
    /// <param name="seed">The seed this game's fights are drawn from.</param>
    /// <param name="scope">The scope the draws live under, so they cannot collide with another owner's.</param>
    /// <param name="key">What this attack is called inside the fight, which must not be blank.</param>
    /// <exception cref="ArgumentNullException">No service, seed, scope, or key was supplied.</exception>
    public AttackRolls(IRandomService random, ulong seed, string scope, string key)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _random = random;
        _seed = seed;
        _scope = scope;
        _key = key;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">The purpose is missing, so the draw could collide with another.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The range is not a range.</exception>
    public int Roll(string purpose, int minimum, int maximum)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        if (maximum < minimum)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximum),
                maximum,
                $"A draw from {minimum} to {maximum} is not a range, so the value it produced would depend on how the engine read it.");
        }

        return (int)_random
            .DrawKeyed(new KeyedRngRequest(_seed, _scope, $"{_key}/{purpose}", minimum, maximum))
            .Value;
    }

    /// <inheritdoc />
    public override string ToString() => $"{_scope}:{_key}";
}
