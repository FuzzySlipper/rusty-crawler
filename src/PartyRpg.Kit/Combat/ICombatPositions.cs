using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Combat;

/// <summary>
/// Where the actors of a fight stand now, when something owns a live position for them.
/// </summary>
/// <remarks>
/// <para>
/// A fight reads where the party is and what stands in the place, and every distance it measures is between
/// those positions. A party member's position is the party's own and is always current. A world actor's is
/// the position its placement put it at — unless something has moved it, which is what a creature that
/// closes on the party does.
/// </para>
/// <para>
/// <b>This is the second half of a creature that acts.</b> Movement is the other half of a creature's
/// behavior, and a creature that moved while the fight went on measuring it against the placement it came
/// from would be a fight against a ghost: the distances, the notice ranges, and the targets would all be
/// where the creature used to be. A world that owns live positions answers here; one that owns none leaves
/// every actor where content placed it, which is the honest reading of a world nothing moves in.
/// </para>
/// <para>
/// Positions are in the place's own units, the same units a placement and the party's pose are stated in,
/// so nothing here converts a coordinate and nothing a ruleset reads changes meaning between the two.
/// </para>
/// </remarks>
public interface ICombatPositions
{
    /// <summary>Where an actor stands now, or null when this world has no live position for it.</summary>
    /// <param name="actor">The actor to look for, by the identity the fight knows it under.</param>
    PlacePose? PoseOf(CombatantId actor);
}
