namespace PartyRpg.Kit.Services;

/// <summary>
/// The one place a service turns values and multipliers into coins, so every price in the game rounds the
/// same way.
/// </summary>
/// <remarks>
/// <para>
/// <b>The numbers are the ruleset's; this is the arithmetic.</b> A price rule states the base value content
/// declares, the multiplier a shop carries, and whatever adjustment the party's own skill and standing earn
/// it, and composes them here. Keeping the conversion in one place is what stops one counter's price from
/// rounding up while the next one's rounds down, and it is why a rule never does the multiplication itself.
/// </para>
/// <para>
/// <b>Coins are whole and prices truncate.</b> A price is a number of coins, so a fractional product is cut
/// down toward zero, and anything that costs something costs at least one coin: a price of nothing would be
/// a free purchase nobody stated. A value of nothing stays nothing, because an item content prices at zero
/// is a thing with no price rather than a thing with a price of one.
/// </para>
/// <para>
/// <b>Percentages are the donor's own arithmetic.</b> A discount or a surcharge is stated in whole percent
/// and applied as the original applies it, with integer division, so a rule that transcribes the game's own
/// formula gets the game's own number rather than a rounded approximation of it.
/// </para>
/// </remarks>
public static class ServicePricing
{
    /// <summary>How many coins a value becomes once every multiplier is applied.</summary>
    /// <param name="value">The base value, which cannot be negative.</param>
    /// <param name="multipliers">The multipliers to apply, in order; each must be finite and positive.</param>
    /// <returns>The whole number of coins, at least one when the value is not nothing.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative, or a multiplier is not finite and positive.</exception>
    public static int Coins(int value, params double[] multipliers)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        ArgumentNullException.ThrowIfNull(multipliers);
        if (value == 0) return 0;

        double product = value;
        foreach (double multiplier in multipliers)
        {
            if (!double.IsFinite(multiplier) || multiplier <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(multipliers),
                    multiplier,
                    "A price multiplier is a finite, positive factor; content that states one states a number prices are scaled by.");
            }

            product *= multiplier;
        }

        if (!double.IsFinite(product) || product >= int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(multipliers),
                product,
                "That combination of multipliers prices an amount beyond the coins this kit counts.");
        }

        int coins = (int)product;
        return coins < 1 ? 1 : coins;
    }

    /// <summary>What an amount becomes after a whole-percent adjustment, in the donor's own arithmetic.</summary>
    /// <remarks>
    /// A positive percent takes that share off — a merchant's discount — and a negative one adds it, which
    /// is how a rule transcribes a formula whose adjustment can go either way. The division is integer
    /// division, exactly as the donor's <c>applyMerchantDiscount</c> performs it, so the number a rule
    /// computes is the number the game computed.
    /// </remarks>
    /// <param name="amount">The amount before the adjustment, which cannot be negative.</param>
    /// <param name="percent">The whole percent to take off, or to add when it is negative.</param>
    /// <returns>The adjusted amount, which is never negative.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The amount is negative.</exception>
    public static int Percent(int amount, int percent)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        long adjusted = (long)amount * (100 - percent) / 100;
        return adjusted < 0 ? 0 : (int)adjusted;
    }

    /// <summary>An amount raised to a floor, which is how a minimum price is stated.</summary>
    /// <param name="amount">The amount to raise.</param>
    /// <param name="floor">The least the amount may be.</param>
    /// <returns>The amount, or the floor when the amount is below it.</returns>
    public static int AtLeast(int amount, int floor) => Math.Max(amount, floor);

    /// <summary>An amount capped at a ceiling, which is how a limit on what a service pays is stated.</summary>
    /// <param name="amount">The amount to cap.</param>
    /// <param name="ceiling">The most the amount may be.</param>
    /// <returns>The amount, or the ceiling when the amount is above it.</returns>
    public static int AtMost(int amount, int ceiling) => Math.Min(amount, ceiling);

    /// <summary>The share of an amount a whole percent names, which is how a bonus is stated.</summary>
    /// <remarks>
    /// This is the donor's own arithmetic for a merchant's cut of a sale — a share of the item's value, in
    /// integer division — and it is the counterpart of <see cref="Percent"/>: there a percent is taken off,
    /// here a percent of the amount is added.
    /// </remarks>
    /// <param name="amount">The amount the share is taken from, which cannot be negative.</param>
    /// <param name="percent">The whole percent to take; a negative share takes nothing.</param>
    /// <returns>The share, which is never negative.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The amount is negative.</exception>
    public static int Share(int amount, int percent)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        if (percent <= 0) return 0;
        long share = (long)amount * percent / 100;
        return share > int.MaxValue ? int.MaxValue : (int)share;
    }
}
