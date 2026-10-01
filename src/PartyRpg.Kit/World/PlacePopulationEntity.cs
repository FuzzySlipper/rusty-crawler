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

    internal PlacePopulationEntity(Actor actor, PlacementDefinition placement, bool summoned = false)
    {
        _actor = actor;
        Placement = placement;
        IsSummoned = summoned;
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

    /// <summary>
    /// Where the entity stands now: where its placement put it, until whatever moves it resolves a step.
    /// </summary>
    /// <remarks>
    /// It is the one home of the entity's position, attached when the population created the entity, so every
    /// reader — a fight measuring a distance, a body laid where a creature fell, the reticle — reads the same
    /// place. An entity the party left behind reads its placement's pose, because it stands nowhere now.
    /// </remarks>
    public PlacePose Pose => _actor.IsAlive && _actor.TryGet(out StandingPose? standing) && standing is not null
        ? standing.Pose
        : Placement.Pose;

    /// <summary>Stands the entity somewhere else, which is what a step a mover resolved does.</summary>
    /// <param name="pose">Where it now stands, in the place's own units.</param>
    /// <exception cref="InvalidOperationException">The entity is gone, so it stands nowhere.</exception>
    public void MoveTo(PlacePose pose)
    {
        if (!_actor.IsAlive) throw new InvalidOperationException("An entity the party left behind stands nowhere, so it cannot be moved.");
        _actor.Get<StandingPose>().Pose = pose;
    }

    /// <summary>Whether the entity is still alive; leaving the place is what ends it.</summary>
    public bool IsAlive => _actor.IsAlive;

    /// <summary>
    /// Whether something created the entity while the party stood in the place, rather than content placing it.
    /// </summary>
    /// <remarks>
    /// A summoned entity has no placement content states, so a place rebuilt from content never holds it again:
    /// it lives for the visit that made it, or until what made it lets it go, whichever is first.
    /// </remarks>
    public bool IsSummoned { get; }
}

/// <summary>Where a placed entity stands now, attached by the population when it creates the entity.</summary>
internal sealed class StandingPose(PlacePose pose)
{
    /// <summary>Where it stands.</summary>
    internal PlacePose Pose { get; set; } = pose;
}

/// <summary>What a place's entity is composed with beyond its placement and where it stands, when a game states more.</summary>
/// <remarks>
/// A population creates an entity for every placement a place states; what that entity also carries — a
/// creature's health, which the game's own row states — is composed here, the moment the entity exists, so
/// nothing downstream attaches a component to an entity it merely read.
/// </remarks>
public interface IPlacementComposer
{
    /// <summary>Composes what one newly placed entity carries.</summary>
    /// <param name="entity">The entity, standing where its placement put it.</param>
    /// <param name="place">The place it stands in.</param>
    void Compose(PlacePopulationEntity entity, PlaceId place);
}
