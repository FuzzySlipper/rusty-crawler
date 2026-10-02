using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Party;

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

    /// <summary>Reads the durable meaning of one held deadline; null refuses capture by name.</summary>
    DeadlineSave? CaptureDeadline(DeadlineId deadline, GameClock clock) => null;

    /// <summary>Rebuilds one judged deadline, returning false when it belongs to another owner.</summary>
    bool RestoreDeadline(DeadlineSave deadline, PartyEntity party) => false;

    /// <summary>What the deadline is, in the words a refused save names it by.</summary>
    /// <param name="deadline">A deadline this owner holds.</param>
    string Describe(DeadlineId deadline);
}
