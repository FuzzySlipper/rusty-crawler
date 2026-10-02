namespace PartyRpg.Kit.World;

/// <summary>
/// What a placement that stands for something the game resolves turns into when its place is populated.
/// </summary>
/// <remarks>
/// <para>
/// Most placements are what they say: a door is a door and a chest is a chest. Some state a request instead
/// of an answer — a mark that asks for "some of these creatures" rather than naming which and how many — and
/// deciding the answer is the game's policy, not content's and not the kit's. The population asks this seam
/// once per placement while it reads the world's placements; what it answers stands in the placement's stead
/// for every reader, so the entities the population creates, a detection over the place, and a count a rule
/// takes of what a place holds are one reading rather than several.
/// </para>
/// <para>
/// <b>The answer must be the same every time it is asked.</b> A population is rebuilt on every visit and
/// every restore, and a load rebuilds it from nothing, so an expansion that drew anything must draw it under
/// a key that names the place and the placement, never from a stream whose position depends on what happened
/// before. That is what lets a save carry no copy of what was resolved and a load see the same creatures.
/// </para>
/// </remarks>
public interface IPlacementExpansion
{
    /// <summary>What one placement stands for, or null when it stands as content states it.</summary>
    /// <param name="place">The place the placement stands in.</param>
    /// <param name="placement">The placement as content states it.</param>
    /// <returns>
    /// Null to keep the placement as it is; otherwise the placements it resolves to, in order, which may be
    /// none when it resolves to nothing. Each must be unique among its place's placements.
    /// </returns>
    IReadOnlyList<PlacementDefinition>? Expand(PlaceId place, PlacementDefinition placement);

    /// <summary>Whether a placement stands on the field when its place is populated, as the game's own kept state says.</summary>
    /// <remarks>
    /// What a place holds is read once and is the same on every read; whether each of it stands this visit can
    /// depend on what the game keeps for the place — a group its events hid, or showed again — so the population
    /// asks this every time it populates, and an entity it answers no for is not created. A game that keeps no
    /// such state stands everything.
    /// </remarks>
    /// <param name="place">The place being populated.</param>
    /// <param name="placement">One of its placements, as <see cref="Expand"/> left it.</param>
    /// <returns>Whether it stands.</returns>
    bool Stands(PlaceId place, PlacementDefinition placement) => true;
}
