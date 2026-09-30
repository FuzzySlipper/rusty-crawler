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
/// The draws one attack or one generation makes, taken from the engine's keyed random service under a key
/// that names it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every draw is keyed, so a fight and a generation are reproducible.</b> The engine takes an explicit
/// seed and reads no wall clock, so a hit roll, a handful of dice, and a weighted pick drawn under one key
/// are the same values every time they are asked for. That is what makes a fight replay identically and a
/// chest's contents the same on a second look, without anything being recorded: the key is the attack, the
/// body, or the container, and the same key resolves to the same draws.
/// </para>
/// <para>
/// <b>The purposes are separate draws.</b> A hit roll must not be the damage roll of the same swing, and a
/// chance must not be the pick it guards, so every draw is asked for under a purpose that names it.
/// </para>
/// <para>
/// <b>A product that cannot draw gets no rolls at all.</b> The ruleset holds the random service and states
/// the seed and scope; with no service it answers null where rolls are asked for, and the fight or the
/// generator then resolves nothing rather than drawing from an invented source.
/// </para>
/// </remarks>
public sealed class KeyedRolls : IAttackRolls
{
    private readonly IRandomService _random;
    private readonly ulong _seed;
    private readonly string _scope;
    private readonly string _key;

    /// <summary>Creates the rolls of one attack or one generation.</summary>
    /// <param name="random">The engine's random service, which draws the values.</param>
    /// <param name="seed">The seed the owner's draws are made from.</param>
    /// <param name="scope">The scope the draws live under, so they cannot collide with another owner's.</param>
    /// <param name="key">What this attack or generation is called, which must not be blank.</param>
    /// <exception cref="ArgumentNullException">No service was supplied.</exception>
    /// <exception cref="ArgumentException">The scope or the key is blank, so two generations could share draws.</exception>
    public KeyedRolls(IRandomService random, ulong seed, string scope, string key)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _seed = seed;
        _scope = scope;
        _key = key;
    }

    /// <summary>What this generation is called, as the key its draws are made under.</summary>
    public string Key => _key;

    /// <summary>Whether a chance stated in percent comes in.</summary>
    /// <remarks>
    /// A chance of a hundred or more always comes in and one of nothing never does, both without a draw —
    /// which is the same answer a hundred-sided roll gives, and one fewer draw to explain.
    /// </remarks>
    /// <param name="percent">The chance, in percent.</param>
    /// <returns>Whether the roll came in under it.</returns>
    public bool Chance(int percent) => percent >= 100 || (percent > 0 && Roll("chance", 1, 100) <= percent);

    /// <summary>Rolls a handful of dice and answers their total.</summary>
    /// <param name="rolls">How many dice, which may be none for a total of nothing.</param>
    /// <param name="sides">How many sides each die has.</param>
    /// <returns>The total, from <paramref name="rolls"/> to <paramref name="rolls"/> times <paramref name="sides"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A count or a side count is negative.</exception>
    public int Dice(int rolls, int sides)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rolls);
        ArgumentOutOfRangeException.ThrowIfNegative(sides);
        if (rolls == 0 || sides == 0) return 0;

        int total = 0;
        for (int die = 0; die < rolls; die++) total += Roll($"die/{die}", 1, sides);
        return total;
    }

    /// <summary>Draws one value from a range, both ends included.</summary>
    /// <param name="minimum">The least value the draw may be.</param>
    /// <param name="maximum">The most value the draw may be.</param>
    /// <returns>The drawn value.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The range is not a range.</exception>
    public int Between(int minimum, int maximum) => Roll("between", minimum, maximum);

    /// <summary>Picks one of a number of entries with an even chance.</summary>
    /// <param name="count">How many entries there are.</param>
    /// <returns>The chosen entry's index, from zero.</returns>
    /// <exception cref="ArgumentOutOfRangeException">There is nothing to pick from.</exception>
    public int Pick(int count)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        return Roll("pick", 0, count - 1);
    }

    /// <summary>Picks one entry out of a weighted table, by its total weight.</summary>
    /// <param name="totalWeight">The sum of every entry's weight.</param>
    /// <returns>A value from zero to the total weight less one, which is what a weighted walk consumes.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The total weight is not positive, so nothing can be picked.</exception>
    public int Weighted(int totalWeight)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(totalWeight, 1);
        return Roll("weight", 0, totalWeight - 1);
    }

    /// <summary>The same generation, drawing under one more name.</summary>
    /// <remarks>
    /// A draw of one purpose is the same value every time it is asked for, which is what makes a generation
    /// reproducible — and what would make a rule repeated five times produce five copies of one thing. A
    /// repeat therefore draws under a name of its own, and the whole of what it draws is distinct from the
    /// repetition before it.
    /// </remarks>
    /// <param name="purpose">What the repetition is, which must not be blank.</param>
    /// <returns>The rolls, drawing under the longer key.</returns>
    /// <exception cref="ArgumentException">The purpose is blank, so the repetition would repeat a draw.</exception>
    public KeyedRolls Under(string purpose)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        return new KeyedRolls(_random, _seed, _scope, $"{_key}/{purpose}");
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
