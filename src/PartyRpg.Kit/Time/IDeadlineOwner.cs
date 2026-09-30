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

    /// <summary>
    /// Whether a save may leave this deadline out because the owner builds it again when a session is loaded.
    /// </summary>
    /// <remarks>
    /// The save schema records the game time a clock has lived through and no deadlines, so a deadline the
    /// owner cannot rebuild — one whose moment is state the party would lose — refuses the save by name rather
    /// than being dropped. One the owner rebuilds from what the save does carry does not.
    /// </remarks>
    /// <param name="deadline">A deadline this owner holds.</param>
    bool RebuildsOnLoad(DeadlineId deadline);

    /// <summary>What the deadline is, in the words a refused save names it by.</summary>
    /// <param name="deadline">A deadline this owner holds.</param>
    string Describe(DeadlineId deadline);
}
