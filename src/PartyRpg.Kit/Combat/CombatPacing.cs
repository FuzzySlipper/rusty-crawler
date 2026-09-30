namespace PartyRpg.Kit.Combat;

/// <summary>
/// Which of the two pacings a fight is being played in.
/// </summary>
/// <remarks>
/// <para>
/// This is a mode of one fight and not a second fight: switching it changes when actors act and nothing
/// else — no health, position, condition, recovery, corpse, or provocation is touched by the switch — which
/// is why it lives on <see cref="CombatState"/> rather than beside it.
/// </para>
/// <para>
/// <b>Real time is the default and the door.</b> Recovery elapses with the game time the session's one
/// clock advances inside the admitted update, and an actor acts whenever it is ready. Turn-based pacing is
/// the same state read differently: the actors act one at a time in the order their own remaining recovery
/// states, in rounds, and the session waits for the player's committed action rather than stepping the
/// world. One flag chooses between the pair, set and cleared by one toggle, and one recovery quantity stands
/// behind both pacings, so an actor's place in a turn-based queue is the recovery it already owes rather than
/// a second number kept beside it.
/// </para>
/// </remarks>
public enum CombatPacing
{
    /// <summary>Actors act as their recovery elapses, while the world keeps stepping.</summary>
    RealTime,

    /// <summary>Actors act one at a time in initiative order, in rounds, while the session waits for turns.</summary>
    TurnBased,
}
