using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Party;

/// <summary>
/// The caller's rule for the pose a place admits: whether the place takes the party at all, and the pose
/// it takes the party at when it does not take the one it was offered.
/// </summary>
/// <remarks>
/// The kit holds no geometry, so a place's bounds can only come from whoever owns that place's data —
/// the same rule covers a region edge, a dungeon wall, and a scripted arrival without the kit learning
/// what any of them are. The rule is asked at the two boundaries where a pose arrives from outside the
/// running simulation, entering a place and restoring a saved pose, so a party can never be resumed
/// outside the world it is resumed into.
/// </remarks>
/// <param name="place">The place the pose is being set in.</param>
/// <param name="pose">The pose the party would hold, already normalized by the facing rule.</param>
/// <param name="admitted">The pose the place admits when the rule returns true, which must be finite.</param>
/// <returns>
/// Whether the place admits a pose. Returning false refuses the move outright rather than placing the
/// party somewhere it did not ask for, because a silent relocation is far harder to diagnose than a
/// refused transition.
/// </returns>
public delegate bool PlacePoseAdmission(PlaceId place, PlacePose pose, out PlacePose admitted);
