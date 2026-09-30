using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Combat;

/// <summary>A creature went down: the placement it was, where it fell, and what it is called.</summary>
/// <remarks>
/// <para>
/// This is a completed change, reported once, where it happens: the blow that takes a creature's health to
/// nothing is the one moment it dies, so that is where this is raised and nowhere else. A death is never read
/// back out of the place afterwards, so nothing downstream needs to remember which deaths it has already seen.
/// </para>
/// <para>
/// Where it fell is the creature's live position and not its placement's, because a creature that closed on
/// the party lies where it was killed. The placement travels whole, standing where it fell, so what a creature's
/// row says about its worth and its treasure is read from the record the living creature was read from.
/// </para>
/// </remarks>
/// <param name="Place">The place it died in.</param>
/// <param name="Placement">The placement it was created from, standing where it fell.</param>
/// <param name="Name">What the ruleset calls it, which is what a body is named by.</param>
public sealed record CreatureDeath(PlaceId Place, PlacementDefinition Placement, string Name);

/// <summary>Hears every creature's death, once, at the moment it happens.</summary>
/// <remarks>
/// A fight is composed with the observers a game names — what a death pays, what it counts toward, what it
/// leaves lying — and tells each of them in turn. None of them hands the death to another, so what one does
/// with it cannot change what the next is told.
/// </remarks>
public interface ICreatureDeathObserver
{
    /// <summary>A creature died.</summary>
    /// <param name="death">Which creature, where, and what it is called.</param>
    void Died(CreatureDeath death);
}
