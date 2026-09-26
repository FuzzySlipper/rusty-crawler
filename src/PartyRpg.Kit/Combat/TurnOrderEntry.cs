using PartyRpg.Kit.Time;

namespace PartyRpg.Kit.Combat;

/// <summary>
/// One actor's place in a round, as the panel reads it: how much game time it still owes before it may act,
/// and where the round has put it.
/// </summary>
/// <remarks>
/// <para>
/// This is the fight's own state read as an order, never a second copy of it: <see cref="Remaining"/> is the
/// actor's recovery as the fight holds it, and <see cref="Ready"/> is that same quantity being exactly none.
/// Nothing here is drawn, remembered, or re-rolled — the order is ascending remaining recovery, which is why
/// a faster actor appears again sooner and acts more than once in the same round as arithmetic.
/// </para>
/// <para>
/// <see cref="CanAct"/> is the fight's own answer about whether anything is left for the actor to do with a
/// turn: an actor it has laid out is passed over rather than stalling the round, exactly as the donor drops
/// an actor that cannot act from its queue (<c>src/Engine/TurnEngine/TurnEngine.cpp:40-51</c>,
/// where such an actor is given initiative 1001 and the queue is shortened past it).
/// </para>
/// </remarks>
/// <param name="Id">The actor's identity, as the fight names it.</param>
/// <param name="Name">What the actor is called, as the ruleset named it.</param>
/// <param name="Side">Which side of the fight the actor is on.</param>
/// <param name="Remaining">How much game time it must still recover, zero when it is ready.</param>
/// <param name="Ready">Whether it may act now, which is true exactly when <paramref name="Remaining"/> is none.</param>
/// <param name="CanAct">Whether the fight leaves it able to act at all.</param>
/// <param name="Waiting">Whether it has deferred its turn to the end of this round.</param>
/// <param name="Current">Whether this is the actor whose turn it is now.</param>
public readonly record struct TurnOrderEntry(
    CombatantId Id,
    string Name,
    CombatSide Side,
    GameDuration Remaining,
    bool Ready,
    bool CanAct,
    bool Waiting,
    bool Current);
