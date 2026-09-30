namespace PartyRpg.Kit.Time;

/// <summary>An owner that sets deadlines on the one clock and can say which ones are its own.</summary>
/// <remarks>
/// A deadline is a number the clock hands back when it comes due; what it means belongs to whoever set it. The
/// session reports every deadline an advance brought due, and asks the owners it composed whether one is theirs,
/// so a report says who acted on it — and a deadline nobody holds is visible as exactly that.
/// </remarks>
public interface IDeadlineOwner
{
    /// <summary>Whether this owner set the deadline and is still waiting for it.</summary>
    /// <param name="deadline">The deadline the clock reported.</param>
    bool Holds(DeadlineId deadline);
}
