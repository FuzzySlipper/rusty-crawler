using PartyRpg.Kit.Combat;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// A part of a damage roll that counts again at a chance: a run of the roll's own dice and a share of its bonus,
/// drawn once per roll, with the bounds a reader is given still the bounds the roll produces.
/// </summary>
/// <remarks>Every number here is this suite's own; the kit states no multiplier of any game's.</remarks>
public sealed class DamageMultiplierTests
{
    [Fact]
    public void A_part_counts_its_own_dice_and_bonus_again_only_when_its_draw_lands()
    {
        // Three dice of six and a bonus of seven, of which the first two dice and two of the bonus are the part.
        DamageRoll roll = new DamageRoll(3, 6, 7, floor: 1)
            .WithMultiplier(new DamageMultiplier(firstDie: 0, dice: 2, bonus: 2, HitChance.Of(25, 100), factor: 3));

        // Dice of 1, 2 and 3: six on the dice, thirteen with the bonus; the part is 1 + 2 + 2 = 5.
        Faces faces = new(1, 2, 3);
        Assert.Equal(13, roll.Roll(faces.Drawing(chance: 2500), "damage"));
        Assert.Equal(13 + (2 * 5), roll.Roll(faces.Drawing(chance: 2499), "damage"));

        // The bounds: at least the dice at one and the bonus, at most every die at six and the part tripled.
        Assert.Equal(3 + 7, roll.Minimum);
        Assert.Equal(18 + 7 + (2 * (12 + 2)), roll.Maximum);
    }

    [Fact]
    public void Two_parts_draw_independently_over_their_own_dice()
    {
        DamageRoll roll = new DamageRoll(2, 4)
            .WithMultiplier(new DamageMultiplier(0, 1, 0, HitChance.Always, 2))
            .WithMultiplier(new DamageMultiplier(1, 1, 1, HitChance.Never, 3));

        // Faces of 3 and 4: the first part doubles its 3, the second never counts again.
        Assert.Equal(3 + 4 + 3, roll.Roll(new Faces(3, 4).Drawing(chance: 0), "damage"));

        // Only the certain part counts at the bounds.
        Assert.Equal(2 + 1, roll.Minimum);
        Assert.Equal(8 + 4, roll.Maximum);
        Assert.Equal(2, roll.Multipliers.Count);
    }

    [Fact]
    public void A_roll_with_no_part_draws_what_it_always_drew_and_compares_by_what_it_states()
    {
        DamageRoll plain = new(2, 6, 1);
        Recording drawn = new();
        plain.Roll(drawn, "damage");
        Assert.Equal(["damage/0", "damage/1"], drawn.Purposes);

        DamageMultiplier part = new(0, 1, 0, HitChance.Of(10, 100), 3);
        Assert.Equal(plain.WithMultiplier(part), new DamageRoll(2, 6, 1).WithMultiplier(part));
        Assert.Equal(plain.WithMultiplier(part).GetHashCode(), new DamageRoll(2, 6, 1).WithMultiplier(part).GetHashCode());
        Assert.NotEqual(plain, plain.WithMultiplier(part));
    }

    [Fact]
    public void A_part_naming_dice_the_roll_does_not_roll_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DamageRoll(1, 4).WithMultiplier(new DamageMultiplier(1, 1, 0, HitChance.Always, 3)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DamageMultiplier(0, 1, 0, HitChance.Always, 0));
    }

    /// <summary>Dice faces in order, and one value for every chance draw.</summary>
    private sealed class Faces(params int[] faces)
    {
        public IAttackRolls Drawing(int chance) => new Drawn(faces, chance);

        private sealed class Drawn(int[] faces, int chance) : IAttackRolls
        {
            public int Roll(string purpose, int minimum, int maximum) =>
                purpose.Contains("/multiplier/", StringComparison.Ordinal)
                    ? chance
                    : faces[int.Parse(purpose[(purpose.LastIndexOf('/') + 1)..], System.Globalization.CultureInfo.InvariantCulture)];
        }
    }

    /// <summary>Records which purposes a roll asked for.</summary>
    private sealed class Recording : IAttackRolls
    {
        public List<string> Purposes { get; } = [];

        public int Roll(string purpose, int minimum, int maximum)
        {
            Purposes.Add(purpose);
            return minimum;
        }
    }
}
