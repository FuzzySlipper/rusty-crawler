using System.Globalization;

namespace PartyRpg.Kit.Combat;

/// <summary>How likely one attack is to land, in the finest unit the kit rolls in.</summary>
/// <remarks>
/// <para>
/// A chance stated in ten-thousandths, which is fine enough for every formula this game's donor states and
/// coarse enough to be an integer a deterministic roll can be compared against: the fight draws one value in
/// <c>[0, 9999]</c> and the attack lands when the draw is below the chance. That is the whole mechanism —
/// where the number comes from is the ruleset's.
/// </para>
/// <para>
/// <b>Why not a floating-point probability.</b> A roll that compares exactly is what makes the same seed and
/// the same state produce the same hit, and a chance expressed as a ratio of two integers — which is what
/// the donor's hit test is — loses nothing an integer count of ten-thousandths cannot hold to the fourth
/// decimal. Where a ruleset rounds, it rounds once, here, and says so.
/// </para>
/// </remarks>
public readonly record struct HitChance
{
    /// <summary>The most a chance can be: this many ten-thousandths is a certain hit.</summary>
    public const int Certain = 10000;

    private HitChance(int basisPoints) => BasisPoints = basisPoints;

    /// <summary>An attack that cannot miss.</summary>
    public static HitChance Always { get; } = new(Certain);

    /// <summary>An attack that cannot land.</summary>
    public static HitChance Never { get; } = new(0);

    /// <summary>A chance of so many ten-thousandths.</summary>
    /// <param name="basisPoints">The chance, from zero to <see cref="Certain"/>.</param>
    /// <returns>The chance.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The value lies outside zero and a certain hit.</exception>
    public static HitChance Of(int basisPoints)
    {
        if (basisPoints < 0 || basisPoints > Certain)
        {
            throw new ArgumentOutOfRangeException(
                nameof(basisPoints),
                basisPoints,
                $"A hit chance is stated in ten-thousandths from 0 to {Certain}; anything else is not a probability a roll could be compared against.");
        }

        return new HitChance(basisPoints);
    }

    /// <summary>A chance from a hit count out of a stated number of equally likely outcomes.</summary>
    /// <param name="hits">How many outcomes land the attack; a negative count states none.</param>
    /// <param name="outcomes">How many outcomes there are, which must be at least one.</param>
    /// <returns>The chance, rounded to the nearest ten-thousandth.</returns>
    /// <exception cref="ArgumentOutOfRangeException">There are no outcomes to count.</exception>
    public static HitChance Of(int hits, int outcomes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(outcomes, 1);
        if (hits <= 0) return Never;
        if (hits >= outcomes) return Always;
        return new HitChance((int)Math.Round((double)hits * Certain / outcomes, MidpointRounding.AwayFromZero));
    }

    /// <summary>How many ten-thousandths of the time this attack lands.</summary>
    public int BasisPoints { get; }

    /// <summary>Whether one draw of ten thousand lands this attack.</summary>
    /// <param name="roll">The draw, from zero to <see cref="Certain"/> minus one.</param>
    /// <returns>Whether it hit.</returns>
    public bool Hits(int roll) => roll < BasisPoints;

    /// <inheritdoc />
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{BasisPoints / 100.0:0.##}%");
}
