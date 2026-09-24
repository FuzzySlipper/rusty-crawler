using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Party;

/// <summary>
/// The party's one position and facing in the world.
/// </summary>
/// <remarks>
/// This is the only place a party pose is held. Entering, walking, and turning all replace the one private
/// field, and nothing outside this class can write it, so there is no second pose for a camera, a view,
/// or a character to keep in step: every view is computed from this value at the moment it is asked for,
/// and no path leads from a view back into it. A stored pose is always finite and always normalized by
/// the facing rule, because a pose the ruleset would never let the party reach would otherwise surface
/// later as a camera looking somewhere the game cannot describe.
/// </remarks>
public sealed class PartyPoseOwner
{
    private readonly FacingRule _facing;
    private readonly PlacePoseAdmission? _admission;
    private PartyPose _pose;

    /// <summary>Creates the owner of the party's pose at the pose the party starts its game in.</summary>
    /// <param name="startingPose">The place and pose the party starts in, which must be finite.</param>
    /// <param name="facing">The facing rule the party's yaw wraps and its pitch clamps by.</param>
    /// <param name="admission">
    /// The rule for the pose a place admits, supplied by whoever owns the places' bounds. Without one the
    /// kit still refuses poses that are not numbers, which is the only rule it can state about a place it
    /// holds no geometry for.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">The starting pose is not made of numbers.</exception>
    /// <exception cref="ArgumentException">The starting pose names no place, or the place's rule refuses it.</exception>
    public PartyPoseOwner(PartyPose startingPose, FacingRule facing, PlacePoseAdmission? admission = null)
    {
        _facing = facing;
        _admission = admission;
        _pose = Admit(startingPose);
    }

    /// <summary>The place the party is in.</summary>
    public PlaceId Place => _pose.Place;

    /// <summary>The party's position and facing inside <see cref="Place"/>.</summary>
    public PlacePose PlacePose => _pose.Pose;

    /// <summary>
    /// Sets the party's place and pose deliberately at a transition boundary.
    /// </summary>
    /// <remarks>
    /// A transition is the one moment a pose is asserted rather than moved into, whether it crosses to
    /// another place or a script moves the party within the one it is already in. The pose the world's
    /// graph resolved is the pose that is set: deriving it from the previous facing would put the party
    /// somewhere the data never said it arrives.
    /// </remarks>
    /// <param name="place">The place the party arrives in.</param>
    /// <param name="pose">The pose the party arrives at, in that place's coordinates.</param>
    /// <exception cref="ArgumentOutOfRangeException">The pose is not made of numbers.</exception>
    /// <exception cref="ArgumentException">The place has no identity, or its rule refuses the pose.</exception>
    public void Enter(PlaceId place, PlacePose pose) => _pose = Admit(new PartyPose(place, pose));

    /// <summary>Restores a captured pose, for a session resuming from saved state.</summary>
    /// <remarks>
    /// A restored pose is put through the same rules as an entered one: a save written by another content
    /// revision can name a spot this world no longer holds, and a party resumed outside its place is not
    /// a party that can be played.
    /// </remarks>
    /// <param name="pose">The captured place and pose to resume at.</param>
    /// <exception cref="ArgumentOutOfRangeException">The pose is not made of numbers.</exception>
    /// <exception cref="ArgumentException">The place has no identity, or its rule refuses the pose.</exception>
    public void Restore(PartyPose pose) => _pose = Admit(pose);

    /// <summary>Captures the party's place and pose as the data a save carries and reloads.</summary>
    public PartyPose Capture() => _pose;

    /// <summary>Moves the party by a delta inside its place, leaving the facing untouched.</summary>
    /// <remarks>
    /// Walking is a translation, not a new claim about where the party is, so the place rule is not asked
    /// again here: keeping the party inside the walkable ground of its place is the collision owner's
    /// work, and a rule that clamped every step would quietly stand in for collision that is not built.
    /// </remarks>
    /// <param name="deltaX">How far to move along the place's first axis.</param>
    /// <param name="deltaY">How far to move along the place's second axis.</param>
    /// <param name="deltaZ">How far to move in height.</param>
    /// <exception cref="ArgumentOutOfRangeException">A delta is not a number.</exception>
    /// <exception cref="InvalidOperationException">The move would not land on a position that is a number.</exception>
    public void Move(double deltaX, double deltaY, double deltaZ)
    {
        RequireFinite(deltaX, nameof(deltaX), "A move delta must be a number; a step that is not one cannot be taken.");
        RequireFinite(deltaY, nameof(deltaY), "A move delta must be a number; a step that is not one cannot be taken.");
        RequireFinite(deltaZ, nameof(deltaZ), "A move delta must be a number; a step that is not one cannot be taken.");

        PlacePose current = _pose.Pose;
        double x = current.X + deltaX;
        double y = current.Y + deltaY;
        double z = current.Z + deltaZ;
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(z))
        {
            throw new InvalidOperationException(
                "That move would leave the party at a position that is not a number, so the party stays where it is.");
        }

        _pose = new PartyPose(_pose.Place, current with { X = x, Y = y, Z = z });
    }

    /// <summary>Turns the party by yaw and pitch deltas, leaving the position untouched.</summary>
    /// <param name="yawDelta">How far to turn around the vertical axis, in the world's facing unit.</param>
    /// <param name="pitchDelta">How far to turn above or below the horizon, in the world's facing unit.</param>
    /// <exception cref="ArgumentOutOfRangeException">A delta is not a number, or the turn would not land on a facing that is one.</exception>
    public void Turn(double yawDelta, double pitchDelta)
    {
        RequireFinite(yawDelta, nameof(yawDelta), "A turn delta must be a number; a facing that is not one cannot be turned by.");
        RequireFinite(pitchDelta, nameof(pitchDelta), "A turn delta must be a number; a facing that is not one cannot be turned by.");

        PlacePose current = _pose.Pose;
        double yaw = current.Yaw + yawDelta;
        double pitch = current.Pitch + pitchDelta;
        if (!double.IsFinite(yaw))
        {
            throw new ArgumentOutOfRangeException(
                nameof(yawDelta),
                yawDelta,
                "That yaw turn would not land on a facing that is a number, so the party keeps the facing it has.");
        }

        if (!double.IsFinite(pitch))
        {
            throw new ArgumentOutOfRangeException(
                nameof(pitchDelta),
                pitchDelta,
                "That pitch turn would not land on a facing that is a number, so the party keeps the facing it has.");
        }

        _pose = new PartyPose(_pose.Place, current with
        {
            Yaw = _facing.WrapYaw(yaw),
            Pitch = _facing.ClampPitch(pitch),
        });
    }

    /// <summary>
    /// Derives the first-person view of the party from its current pose and the caller's view offsets.
    /// </summary>
    /// <remarks>
    /// This is a read: it computes a value from the pose and returns it, so a caller that derives a view
    /// every update gets exactly the party's pose plus the offsets it passed, with nothing carried over
    /// from the previous update and nothing to reconcile afterwards.
    /// </remarks>
    /// <param name="offsets">The eye height, look pitch, and stride offset to derive the view with.</param>
    public PartyView DeriveView(PartyViewOffsets offsets) => PartyView.Derive(_pose, offsets);

    /// <summary>Puts a pose through the kit's invariants and the caller's place rule before it is stored.</summary>
    private PartyPose Admit(PartyPose pose)
    {
        if (!pose.IsFinite)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pose),
                pose,
                "A party pose must be made of numbers; a pose that is not is a place the party cannot stand in.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(pose.Place.Value);

        // The place's own rule is asked about the pose the party would actually hold, and its answer is
        // normalized too: a rule that resets a pose must not be able to leave the party facing a
        // direction it could never have reached by turning.
        PlacePose admitted = _facing.Normalize(pose.Pose);
        if (_admission is not null)
        {
            if (!_admission(pose.Place, admitted, out PlacePose adjusted))
            {
                throw new ArgumentException(
                    $"Place '{pose.Place}' does not admit that pose, so the party stays where it is.",
                    nameof(pose));
            }

            PartyPose placed = new(pose.Place, adjusted);
            if (!placed.IsFinite)
            {
                throw new ArgumentException(
                    $"The rule for place '{pose.Place}' admitted a pose that is not made of numbers, which no party can hold.",
                    nameof(pose));
            }

            admitted = _facing.Normalize(placed.Pose);
        }

        return new PartyPose(pose.Place, admitted);
    }

    private static void RequireFinite(double value, string parameterName, string message)
    {
        if (!double.IsFinite(value)) throw new ArgumentOutOfRangeException(parameterName, value, message);
    }
}
