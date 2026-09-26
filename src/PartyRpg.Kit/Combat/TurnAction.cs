namespace PartyRpg.Kit.Combat;

/// <summary>
/// What an actor does with its turn in a paced fight.
/// </summary>
/// <remarks>
/// <para>
/// These are explicit because each one has a different consequence, and a mode in which "do nothing" is the
/// absence of an action cannot tell a player who is thinking from one who has decided to wait:
/// <see cref="Act"/> makes the actor attack what it can reach and charges it the recovery the ruleset states
/// for that attack; <see cref="Skip"/> forfeits the actor's turns for the rest of the round and charges it
/// the recovery of the action it did not take; <see cref="Wait"/> defers the actor's turn to the end of the
/// round without charging it anything, so that it is offered its turn after every actor still due in that
/// round — including the ones due at the very instant the round ends.
/// </para>
/// <para>
/// <b>The donor's own skip is <c>_406457</c></b>, the pass handling in its turn engine
/// (<c>src/Engine/TurnEngine/TurnEngine.cpp:322-350</c>): passing a character's turn charges its attack
/// recovery — floored at thirty ticks — and then the queue advances to whoever is due next, which is why a
/// passed turn is not a free one.
/// This game's manual states the same control from the player's side (the manual's own account of the round,
/// p.34: "B skips a turn"). The donor has no wait, so the wait is this build's own: it is the act the donor's
/// overlay leaves to the order of the queue, made explicit and given a consequence a player can read.
/// </para>
/// </remarks>
public enum TurnAction
{
    /// <summary>The actor attacks what it can reach, and owes the recovery the ruleset states for it.</summary>
    Act,

    /// <summary>The actor forfeits the rest of the round and owes the recovery of the action it did not take.</summary>
    Skip,

    /// <summary>The actor defers its turn to the end of the round, and owes nothing for the wait.</summary>
    Wait,
}
