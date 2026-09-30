using PartyRpg.Kit.Time;

namespace PartyRpg.Kit.Sessions;

/// <summary>Something that keeps a schedule against game time, told every time the one clock moves.</summary>
/// <remarks>
/// <para>
/// The clock reports what an advance crossed and brought due, and hands that report to every owner
/// registered with it (<see cref="GameClock.Observe"/>) before the advance returns. Whatever moved it — the
/// admitted update, a paced turn, a journey's charge, a rest, a wait, a night at an inn — every owner hears
/// the same advance, so a shop's shelves refresh, a ward ends, and a debt of sleep falls due whether the
/// party stood at the counter, spent a week on the road, or slept through it.
/// </para>
/// <para>
/// <b>The alternative is a schedule that misses what nobody forwarded.</b> A one-shot deadline stops being
/// held the moment it is reported, so an owner that heard only the advances some caller remembered to pass on
/// would never learn that its deadline had come. Nothing here counts frames or holds a second clock: an
/// observer is told when game time moved and decides nothing about when it does, and it may not move the
/// clock while hearing it.
/// </para>
/// </remarks>
public interface IGameTimeObserver
{
    /// <summary>Takes one advance of the session's one clock.</summary>
    /// <param name="advance">Where the clock was, where it went, and what it brought due.</param>
    void Observe(ClockAdvance advance);
}
