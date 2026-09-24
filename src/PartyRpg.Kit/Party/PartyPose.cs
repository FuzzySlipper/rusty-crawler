using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Party;

/// <summary>
/// Where the party is: one place, and one position and facing inside that place.
/// </summary>
/// <remarks>
/// Every place keeps its own coordinate space, so a position is only meaningful together with the place
/// it was measured in. Holding both in one value is what stops a pose from surviving the transition that
/// left its place behind, and it is why the party owns this rather than each character: the party walks
/// as one body, and a character owns only what it carries.
/// </remarks>
/// <param name="Place">The place the party is in.</param>
/// <param name="Pose">The party's position and facing in that place's own coordinates.</param>
public readonly record struct PartyPose(PlaceId Place, PlacePose Pose)
{
    /// <summary>
    /// Whether every number in the pose is real, so the pose can be stored and a view derived from it.
    /// </summary>
    /// <remarks>
    /// A pose with one non-finite number is not a place a party can stand in: it compares unequal to
    /// itself, so it slips through range checks that would catch any other bad value, and it poisons
    /// every view derived from it. This is therefore an invariant rather than a caller's policy.
    /// </remarks>
    public bool IsFinite =>
        double.IsFinite(Pose.X) &&
        double.IsFinite(Pose.Y) &&
        double.IsFinite(Pose.Z) &&
        double.IsFinite(Pose.Yaw) &&
        double.IsFinite(Pose.Pitch);
}
