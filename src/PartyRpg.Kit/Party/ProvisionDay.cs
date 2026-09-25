using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Party;

/// <summary>What one day on the road took from the larder and what it did to the party.</summary>
/// <remarks>
/// <para>
/// A day is never refused. The party sets out, the larder gives what it holds, and the day ends short when
/// that was less than the day cost — the donor's shape, where the food store is spent down to empty and the
/// party carries the shortfall rather than the road being cancelled. That is why this records what was
/// charged and what was covered instead of a success or a refusal.
/// </para>
/// <para>
/// <see cref="Shortage"/> is the ruleset's own consequence, already applied to every member by the ledger
/// that spent the day. What ends it — rest, a cure, a day's recovery — is recovery's work over the
/// conditions a member carries, not this record's.
/// </para>
/// </remarks>
public sealed record ProvisionDay
{
    /// <summary>Records a day the party spent.</summary>
    /// <param name="charged">What the day cost, in the unit the larder measures.</param>
    /// <param name="covered">How much of the charge the larder covered.</param>
    /// <param name="shortage">The condition every member suffered, or null when the party came through the day fed.</param>
    /// <exception cref="ArgumentOutOfRangeException">The covered amount is negative or larger than the charge.</exception>
    public ProvisionDay(Provisions charged, int covered, ActiveCondition? shortage)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(covered);
        if (covered > charged.Amount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(covered),
                covered,
                $"A day that charged {charged.Amount} cannot have covered more than that.");
        }

        Charged = charged;
        Covered = covered;
        Shortage = shortage;
    }

    /// <summary>What the day cost.</summary>
    public Provisions Charged { get; }

    /// <summary>How much of the charge the larder covered.</summary>
    public int Covered { get; }

    /// <summary>How much of the charge went unpaid, which is what the party went short of.</summary>
    public int Unpaid => Charged.Amount - Covered;

    /// <summary>Whether the larder covered the whole day.</summary>
    public bool PaidInFull => Unpaid == 0;

    /// <summary>The condition every member suffered because the larder stands below the ruleset's threshold, or null.</summary>
    public ActiveCondition? Shortage { get; }

    /// <inheritdoc />
    public override string ToString() => Shortage is null
        ? $"covered {Covered} of {Charged.Amount} {Charged.Unit}"
        : $"covered {Covered} of {Charged.Amount} {Charged.Unit}, and {Shortage} was applied";
}
