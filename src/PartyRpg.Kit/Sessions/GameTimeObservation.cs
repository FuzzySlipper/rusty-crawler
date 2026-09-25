using PartyRpg.Kit.Time;

namespace PartyRpg.Kit.Sessions;

/// <summary>Something the session tells every time its one clock moves, so a schedule has one driver.</summary>
/// <remarks>
/// <para>
/// The session's clock reports what an advance crossed and brought due rather than publishing it, and this
/// is the seam that carries those reports to whoever keeps a schedule against game time. Two owners of
/// time exist — the admitted update, which advances the clock by the interval it measured, and a journey,
/// which charges the time its transition quoted — and both hand their advance to the same observer, so a
/// shop's shelves refresh whether the party is standing at the counter or has spent a week on the road.
/// </para>
/// <para>
/// <b>The alternative is a schedule that misses what nobody watched.</b> A deadline brought due inside a
/// journey's advance re-arms from the moment the clock actually reached, so an owner that only heard about
/// the advances it happened to see would wait a whole further interval for a restock whose time had
/// already come. Nothing here counts frames or holds a second clock: an observer is told when game time
/// moved and decides nothing about when it does.
/// </para>
/// </remarks>
public interface IGameTimeObserver
{
    /// <summary>Takes one advance of the session's one clock.</summary>
    /// <param name="advance">Where the clock was, where it went, and what it brought due.</param>
    void Observe(ClockAdvance advance);
}
