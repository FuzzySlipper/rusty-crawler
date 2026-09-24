using Rusty.Engine.Entities;

namespace PartyRpg.Kit.World;

/// <summary>
/// One entity living in a place, together with the content placement it was created from.
/// </summary>
/// <remarks>
/// <para>
/// The engine's <see cref="Actor"/> <i>is</i> the entity; this type only names the pair of an entity
/// and the placement that produced it. There is no component registry and no mirrored state graph:
/// reading through <see cref="Actor"/> reads the entity's actual attached components, and a ruleset
/// attaching its own component to the same actor changes this entity, not a copy of it.
/// </para>
/// <para>
/// A reference kept past the visit is not an error. <see cref="IsAlive"/> reports what the engine's
/// store reports, so a caller holding an entity the party left behind learns it is gone instead of
/// reading stale components from it.
/// </para>
/// </remarks>
public sealed class PlacePopulationEntity
{
    private readonly Actor _actor;

    internal PlacePopulationEntity(Actor actor, PlacementDefinition placement)
    {
        _actor = actor;
        Placement = placement;
    }

    /// <summary>The engine actor for this entity, through which components are attached and read.</summary>
    public Actor Actor => _actor;

    /// <summary>
    /// The entity's runtime identity: local to the store that created it, never reused, and never part
    /// of a save.
    /// </summary>
    public EntityId Id => _actor.Entity;

    /// <summary>The content placement the entity was created from.</summary>
    public PlacementDefinition Placement { get; }

    /// <summary>The placement's identity in content, which is the half of the identity a save records.</summary>
    public PlacementContentId Content => Placement.Content;

    /// <summary>Where the entity stands in its place.</summary>
    public PlacePose Pose => Placement.Pose;

    /// <summary>Whether the entity is still alive; leaving the place is what ends it.</summary>
    public bool IsAlive => _actor.IsAlive;
}
