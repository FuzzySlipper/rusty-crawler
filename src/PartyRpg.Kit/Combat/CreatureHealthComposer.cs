using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Combat;

/// <summary>Gives each placed creature the health the game states for it, the moment the creature is placed.</summary>
/// <remarks>
/// A creature's health is its own from the moment it exists: a spell, a trap, or a blow that reaches it before
/// any fight has read it finds it, and nothing that reads a creature ever attaches a component to it. What it
/// can take is the game's answer about that creature; a placement the game states no health for
/// — a door, a fountain, a creature it answers nothing about — carries none, and harm aimed at it lands nowhere.
/// </remarks>
public sealed class CreatureHealthComposer(ICreatureVitals vitals) : IPlacementComposer
{
    /// <inheritdoc />
    public void Compose(PlacePopulationEntity entity, PlaceId place)
    {
        ArgumentNullException.ThrowIfNull(entity);
        CombatSubject subject = new(CombatantId.Of(entity.Id), place, entity.Pose, member: null, entity);
        int maximum = vitals.HitPointsOf(subject);
        if (maximum > 0) CreatureHealth.Attach(entity.Actor, maximum);
    }
}
