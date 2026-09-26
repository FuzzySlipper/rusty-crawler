using PartyRpg.Kit.Party;

namespace PartyRpg.Kit.Progression;

/// <summary>One award of experience the party earned: what earned it, and how much it was worth.</summary>
/// <remarks>
/// <para>
/// <b>This is the one shape every award arrives in.</b> Bringing a creature down, finishing a quest,
/// finding something worth telling about, and a ruleset's own deed all report the same two facts, so the
/// owner has one entry rather than one per source. The source is a word the caller owns — a fight names a
/// kill, a quest names a quest — and it travels into what the award reports rather than being interpreted
/// here, because what earned experience is content's business and this is bookkeeping.
/// </para>
/// <para>
/// The amount is the party's whole award before it divides, not one member's share: who takes a share and
/// how much is <see cref="IProgressionRule.Divide"/>, which is the game's rule rather than the caller's.
/// </para>
/// </remarks>
public readonly record struct PartyExperienceAward
{
    /// <summary>States an award.</summary>
    /// <param name="source">What earned it, which must not be blank: an award that cannot say where it came from cannot be reported.</param>
    /// <param name="amount">How much experience the party earned, which cannot be negative.</param>
    /// <exception cref="ArgumentException">The source is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The amount is negative, which would take experience rather than give it.</exception>
    public PartyExperienceAward(string source, long amount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        Source = source;
        Amount = amount;
    }

    /// <summary>What earned the experience, as the caller names it: <c>kill</c>, <c>quest</c>, or an act.</summary>
    public string Source { get; }

    /// <summary>How much experience the party earned in total, before it is divided.</summary>
    public long Amount { get; }
}

/// <summary>One member's share of an award: who takes it and how much they take.</summary>
/// <remarks>
/// The name travels with the identity so a report of an award reads as people rather than as identities: a
/// panel that showed a share by durable identity alone would be showing a save field to a player.
/// </remarks>
/// <param name="Member">The member who takes the share.</param>
/// <param name="Name">What that member is called.</param>
/// <param name="Amount">How much experience the member takes, which cannot be negative.</param>
public readonly record struct ProgressionShare(PartyMemberId Member, string Name, long Amount);

/// <summary>What one award did: who took what, what the world made of it, or why nothing was awarded.</summary>
/// <remarks>
/// A refused award is a result rather than an exception for the same reason a refused service command is:
/// an award of nothing and a party with nobody able to take one are ordinary states a caller reports, not
/// defects. A refusal carries no shares, so a caller cannot read a half-applied award out of it.
/// </remarks>
/// <param name="Source">What earned the award, as the caller named it.</param>
/// <param name="Amount">How much was offered, before the division.</param>
/// <param name="Shares">What each member who took a share took, in the order they were credited.</param>
/// <param name="Standing">How much the award moved the party's reputation and fame.</param>
/// <param name="Refusal">Why nothing was awarded, or null when the award landed.</param>
public sealed record ProgressionAwardResult(
    string Source,
    long Amount,
    IReadOnlyList<ProgressionShare> Shares,
    ProgressionStanding Standing,
    PartyRefusal? Refusal)
{
    /// <summary>Whether the award landed.</summary>
    public bool IsAwarded => Refusal is null;

    /// <summary>How much experience the members actually took, which a division may leave short of the award.</summary>
    public long Awarded
    {
        get
        {
            long total = 0;
            foreach (ProgressionShare share in Shares) total = checked(total + share.Amount);
            return total;
        }
    }

    /// <summary>An award that was refused, with nothing divided and nothing moved.</summary>
    /// <param name="source">What earned it.</param>
    /// <param name="amount">How much was offered.</param>
    /// <param name="refusal">Why it was refused.</param>
    /// <exception cref="ArgumentNullException">No refusal was supplied.</exception>
    public static ProgressionAwardResult Refused(string source, long amount, PartyRefusal refusal)
    {
        ArgumentNullException.ThrowIfNull(refusal);
        return new ProgressionAwardResult(source, amount, [], ProgressionStanding.None, refusal);
    }
}
