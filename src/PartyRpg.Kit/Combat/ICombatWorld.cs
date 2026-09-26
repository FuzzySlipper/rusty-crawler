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
}
