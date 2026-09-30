using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Combat;

/// <summary>
/// The world a fight stands over, as the fight reads it: where the party is, and what is alive there.
/// </summary>
/// <remarks>
/// <para>
/// This is a reader's seam and not a second world. A fight never moves the party, never populates a place,
/// and never creates an entity: it reads where the party stands and which entities the place's population
/// currently holds, exactly as the interaction mechanism reads what the party faces and the rest mechanism
/// reads what is near a camp. Nothing here is a scene, and entering a fight changes nothing about where the
/// party is, what exists, or what place it is in.
/// </para>
/// <para>
/// The population is the live entities of the party's own visit — created from the place's placements when
/// the party walks in, destroyed when it walks out — so a fight's world actors are the ones actually
/// standing there and a place the party left holds nobody to fight.
/// </para>
/// </remarks>
public interface ICombatWorld
{
    /// <summary>The place the party is in.</summary>
    PlaceId Place { get; }

    /// <summary>Where the party stands in that place, which is what every distance is measured from.</summary>
    PlacePose Pose { get; }

    /// <summary>The entities alive in the party's place right now.</summary>
    IReadOnlyList<PlacePopulationEntity> Population { get; }

    /// <summary>Where an actor stands now, or null when this world has no live position for it.</summary>
    /// <remarks>
    /// A world actor stands where its placement put it unless something has moved it, which is what a creature
    /// that closes on the party does; a fight that went on measuring a moved creature against its placement
    /// would be a fight against a ghost. A world that owns no live position for an actor answers null and the
    /// fight reads the placement, which is the honest reading of a world nothing moves in. Positions are in the
    /// place's own units, the units a placement and the party's pose are stated in.
    /// </remarks>
    /// <param name="actor">The actor to look for, by the identity the fight knows it under.</param>
    PlacePose? PoseOf(CombatantId actor);
}
