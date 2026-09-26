using PartyRpg.Kit.Party;

namespace PartyRpg.Kit.Progression;

/// <summary>What one training step costs and how far it may take a member, as the counter states it.</summary>
/// <remarks>
/// <para>
/// The terms travel into the owner rather than being decided by it: the fee is the price policy's quote and
/// the ceiling is content's own number for that hall, so the owner judges the step against what the counter
/// states rather than keeping a table of halls. The fee is already settled when the owner is called — the
/// party's one ledger is what charges it — and it travels here so the step can be reported with what it
/// cost.
/// </para>
/// <para>
/// The counter's name is the word a person reads, so a refusal and a report name the hall rather than a
/// placement's identity.
/// </para>
/// </remarks>
/// <param name="Counter">What the counter is called, which must not be blank.</param>
/// <param name="Fee">What the step cost the party, which cannot be negative.</param>
/// <param name="Cap">The highest level the counter trains to, or <see cref="int.MaxValue"/> when it states no ceiling.</param>
/// <exception cref="ArgumentException">The counter is unnamed.</exception>
/// <exception cref="ArgumentOutOfRangeException">The fee is negative, or the ceiling is below one.</exception>
public readonly record struct ProgressionTrainingTerms
{
    /// <summary>States the terms of one training step.</summary>
    /// <param name="counter">What the counter is called.</param>
    /// <param name="fee">What the step cost, already settled through the party's own accounts.</param>
    /// <param name="cap">The highest level the counter trains to.</param>
    public ProgressionTrainingTerms(string counter, long fee, int cap)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(counter);
        ArgumentOutOfRangeException.ThrowIfNegative(fee);
        ArgumentOutOfRangeException.ThrowIfLessThan(cap, 1);
        Counter = counter;
        Fee = fee;
        Cap = cap;
    }

    /// <summary>What the counter is called.</summary>
    public string Counter { get; }

    /// <summary>What the step cost the party.</summary>
    public long Fee { get; }

    /// <summary>The highest level the counter trains to.</summary>
    public int Cap { get; }
}

/// <summary>What one training step did: the level reached, what the level gave, or why nobody trained.</summary>
/// <param name="Member">The member who trained.</param>
/// <param name="Name">What that member is called.</param>
/// <param name="Level">The level the member now stands at, or the level it stood at when the step was refused.</param>
/// <param name="Growth">What the level added to the member's pools and how many skill points it granted.</param>
/// <param name="Fee">What the step cost, zero when nothing was charged.</param>
/// <param name="Counter">What the counter is called.</param>
/// <param name="Standing">How much the step moved the party's reputation and fame.</param>
/// <param name="Refusal">Why the member was not trained, or null when the step landed.</param>
public sealed record ProgressionTrainingResult(
    PartyMemberId Member,
    string Name,
    int Level,
    ProgressionGrowth Growth,
    long Fee,
    string Counter,
    ProgressionStanding Standing,
    PartyRefusal? Refusal)
{
    /// <summary>Whether the member rose a level.</summary>
    public bool IsTrained => Refusal is null;

    /// <summary>A step that was refused, with the member exactly where they stood.</summary>
    /// <param name="member">The member.</param>
    /// <param name="name">What the member is called.</param>
    /// <param name="level">The level the member stands at.</param>
    /// <param name="counter">The counter that refused.</param>
    /// <param name="refusal">Why the step was refused.</param>
    /// <exception cref="ArgumentNullException">No refusal was supplied.</exception>
    public static ProgressionTrainingResult Refused(
        PartyMemberId member,
        string name,
        int level,
        string counter,
        PartyRefusal refusal)
    {
        ArgumentNullException.ThrowIfNull(refusal);
        return new ProgressionTrainingResult(
            member,
            name,
            level,
            ProgressionGrowth.None,
            Fee: 0,
            counter,
            ProgressionStanding.None,
            refusal);
    }
}
