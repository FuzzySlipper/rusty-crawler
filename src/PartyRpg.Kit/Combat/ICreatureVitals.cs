namespace PartyRpg.Kit.Combat;

/// <summary>What an actor can take altogether, which is the one fact a creature's health is made from.</summary>
/// <remarks>
/// A party member's pool is the party's own, so for a member this is the pool's capacity; a creature's health is
/// attached when it is placed, measured against this answer about its own row. A game that resolves attacks
/// states it as part of its resolution; a world is composed over it alone, because placing a creature needs to
/// know what it can take and nothing else about a fight.
/// </remarks>
public interface ICreatureVitals
{
    /// <summary>The total harm an actor can take.</summary>
    /// <param name="subject">The actor being measured.</param>
    /// <returns>The total harm it can take, zero when nothing here can say.</returns>
    int HitPointsOf(CombatSubject subject);
}
