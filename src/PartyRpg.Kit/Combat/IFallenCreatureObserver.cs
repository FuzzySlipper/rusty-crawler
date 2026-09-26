using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Combat;

/// <summary>
/// What a fight reports about the creatures it brought down, so that what they left can be found and
/// searched.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is a reading, not a change.</b> A fight owns no body: it re-reads the place every update and
/// states what it read, whole, for one place. Whoever keeps the bodies decides what they mean, and a fight
/// that is handed nobody to tell simply fights — a session whose ruleset answers no such owner loses
/// nothing about the fight itself.
/// </para>
/// <para>
/// <b>Why the fight's own answer carries it.</b> The mechanism is the kit's and the composition is this
/// game's, exactly as it is for attack resolution: the fight asks the rule it was given whether it also
/// keeps what the fallen leave, and hands it the reading. A second composition path into the session's world
/// would be a second owner of one fact, and the world a fight stands over is the session's.
/// </para>
/// </remarks>
public interface IFallenCreatureObserver
{
    /// <summary>States what a fight read as down in one place, replacing whatever was read before.</summary>
    /// <remarks>
    /// What the answer is for is generation: a death is decided once, when it is first read, so whoever owns
    /// loot is handed the bodies the reading produced — with the serials that name those deaths — rather than
    /// being asked later which body it is looking at.
    /// </remarks>
    /// <param name="place">The place the fight read.</param>
    /// <param name="fallen">Every creature it read as down there, in the order it read them.</param>
    /// <returns>The bodies lying in that place now, in the order the fight read them.</returns>
    IReadOnlyList<Corpse> Observe(PlaceId place, IReadOnlyList<FallenCreature> fallen);
}
