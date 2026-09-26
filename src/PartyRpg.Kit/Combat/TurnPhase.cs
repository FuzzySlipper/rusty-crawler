namespace PartyRpg.Kit.Combat;

/// <summary>
/// Which part of a turn-based round the fight is in.
/// </summary>
/// <remarks>
/// <para>
/// The donor's own turn engine has exactly these two stages beside the opposition's thinking
/// (<c>src/Engine/TurnEngine/TurnEngineEnums.h:26-29</c>: <c>TE_ATTACK</c>, the party attacking,
/// and <c>TE_MOVEMENT</c>, the party moving), and the game's manual describes the same round from the
/// player's side: combatants act one at a time in order, and at the end of a round the party gets a
/// movement phase (the manual's own account of the round, p.34).
/// </para>
/// <para>
/// A fight that is not being paced turn-based is in no phase at all, which is not the same fact as being in
/// the action phase of a round: the first says the state is being played in real time, and the second says
/// a round is under way and it is someone's turn.
/// </para>
/// </remarks>
public enum TurnPhase
{
    /// <summary>No round is under way: the fight is not being paced turn-based, or nothing is being fought.</summary>
    None,

    /// <summary>The action phase: the round's actors take their turns in initiative order.</summary>
    Action,

    /// <summary>The party's movement phase, which closes the round.</summary>
    Movement,
}
