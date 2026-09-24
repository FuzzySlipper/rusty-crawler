using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Party;

/// <summary>
/// The party's first-person view: the place it looks into, where the eye is, and which way it looks.
/// </summary>
/// <remarks>
/// A view is derived, never owned. It is computed from the party's pose and the offsets on every ask, and
/// nothing reads it back, so there is no camera state that can drift away from where the party actually
/// is and no second opinion about the party's facing. The view stays in the place's own coordinates and
/// the world's own facing unit; carrying it to a renderer that speaks another unit or another axis
/// convention is a separate, explicit step, because only the caller knows those conventions.
/// </remarks>
/// <param name="Place">The place the view looks into.</param>
/// <param name="X">The eye's position along the place's first axis.</param>
/// <param name="Y">The eye's position along the place's second axis.</param>
/// <param name="Z">The eye's height in the place: the party's own height plus the view offsets.</param>
/// <param name="Yaw">Facing around the vertical axis, in the world's facing unit, exactly as the party holds it.</param>
/// <param name="Pitch">Facing above or below the horizon, in the world's facing unit: the party's pitch plus the look pitch.</param>
public readonly record struct PartyView(PlaceId Place, double X, double Y, double Z, double Yaw, double Pitch)
{
    /// <summary>Derives the view from the party's pose and the offsets the caller tunes it with.</summary>
    /// <param name="pose">The party's place, position, and facing.</param>
    /// <param name="offsets">The eye height, look pitch, and stride offset to derive the view with.</param>
    /// <exception cref="ArgumentOutOfRangeException">The pose is not made of numbers.</exception>
    /// <exception cref="InvalidOperationException">The offsets would carry the view outside the numbers a place is described in.</exception>
    public static PartyView Derive(PartyPose pose, PartyViewOffsets offsets)
    {
        if (!pose.IsFinite)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pose),
                pose,
                "A view can only be derived from a pose made of numbers, and this one is not.");
        }

        double z = pose.Pose.Z + offsets.EyeHeight + offsets.BobOffset;
        double pitch = pose.Pose.Pitch + offsets.LookPitch;
        if (!double.IsFinite(z) || !double.IsFinite(pitch))
        {
            throw new InvalidOperationException(
                "Those view offsets would put the eye outside the numbers the place is described in, so no view was derived.");
        }

        return new PartyView(pose.Place, pose.Pose.X, pose.Pose.Y, z, pose.Pose.Yaw, pitch);
    }
}
