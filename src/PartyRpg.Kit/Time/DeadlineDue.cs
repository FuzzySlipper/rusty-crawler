namespace PartyRpg.Kit.Time;

/// <summary>A deadline the clock passed during one advance.</summary>
/// <remarks>
/// <para>
/// The report names all three moments a deadline has: when it was due, when the clock actually reached it,
/// and how far apart those are. An effect that must happen at a game-time point happens when the clock
/// passes that point, and a clock that was advanced over a long stretch in one call passes it late — the
/// report says so instead of hiding the difference, and the owner decides whether a late effect still
/// matters.
/// </para>
/// <para>
/// A deadline is reported at most once per advance, however many times a repeating one was missed, because
/// an effect that fires once per missed interval would fire without bound after a long advance. A
/// repeating deadline re-arms from the moment it fired, which is the same rule the world's respawn already
/// follows.
/// </para>
/// </remarks>
/// <param name="Deadline">The handle the deadline was registered under.</param>
/// <param name="Scheduled">The point on the calendar the deadline was due at.</param>
/// <param name="Fired">The point on the calendar the clock actually reached, which is where it is now.</param>
/// <param name="Late">How much game time passed between the two, which is none for a deadline met on time.</param>
public sealed record DeadlineDue(DeadlineId Deadline, GameDate Scheduled, GameDate Fired, GameDuration Late);
