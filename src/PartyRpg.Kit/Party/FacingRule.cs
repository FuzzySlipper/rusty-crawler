using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Party;

/// <summary>
/// The caller's rule for facing arithmetic: how many units one full turn is, and how far the party may
/// look up and down.
/// </summary>
/// <remarks>
/// A world stores a facing in whatever unit its own data carries — <see cref="PlacePose.Yaw"/> is not
/// degrees — so the kit names no unit of its own and converts nothing. Whoever knows the data supplies
/// the period, and the same wrapping and clamping then serve any unit, which keeps a silent conversion
/// from ever becoming a unit the rest of the game disagrees about.
/// </remarks>
public readonly record struct FacingRule
{
    /// <summary>Creates a facing rule for one world's facing unit.</summary>
    /// <param name="unitsPerTurn">How many facing units make one full turn; must be finite and positive.</param>
    /// <param name="minimumPitch">The lowest pitch the party may hold, in the same unit.</param>
    /// <param name="maximumPitch">The highest pitch the party may hold, in the same unit.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A limit is not a number, the turn holds no units, or the pitch range is empty.
    /// </exception>
    public FacingRule(double unitsPerTurn, double minimumPitch, double maximumPitch)
    {
        if (!double.IsFinite(unitsPerTurn) || unitsPerTurn <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(unitsPerTurn),
                unitsPerTurn,
                "A full turn must be a finite, positive number of facing units; a yaw cannot wrap in a turn that holds nothing.");
        }

        if (!double.IsFinite(minimumPitch))
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumPitch),
                minimumPitch,
                "A pitch limit must be a number; a limit that is not one clamps nothing.");
        }

        if (!double.IsFinite(maximumPitch))
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumPitch),
                maximumPitch,
                "A pitch limit must be a number; a limit that is not one clamps nothing.");
        }

        if (minimumPitch >= maximumPitch)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumPitch),
                minimumPitch,
                "The pitch range must leave room to look both up and down, so the lowest pitch must be below the highest.");
        }

        UnitsPerTurn = unitsPerTurn;
        MinimumPitch = minimumPitch;
        MaximumPitch = maximumPitch;
    }

    /// <summary>How many facing units make one full turn, in whatever unit the world's data uses.</summary>
    public double UnitsPerTurn { get; }

    /// <summary>The lowest pitch the party may hold, in the world's facing unit.</summary>
    public double MinimumPitch { get; }

    /// <summary>The highest pitch the party may hold, in the world's facing unit.</summary>
    public double MaximumPitch { get; }

    /// <summary>Puts a yaw back into the half-open range a full turn covers, wrapping either way.</summary>
    /// <remarks>
    /// The wrap is a remainder rather than a subtraction of whole turns, because a turn count multiplied
    /// back can overflow for a yaw that is itself finite. Rounding can land the remainder exactly on the
    /// turn, which is the same direction as no turn at all, so that case folds to zero.
    /// </remarks>
    /// <param name="yaw">The yaw to wrap, which must be a number.</param>
    /// <exception cref="ArgumentOutOfRangeException">The yaw is not a number.</exception>
    public double WrapYaw(double yaw)
    {
        if (!double.IsFinite(yaw))
        {
            throw new ArgumentOutOfRangeException(
                nameof(yaw),
                yaw,
                "A yaw must be a number; a facing that is not one cannot be wrapped into a turn.");
        }

        double wrapped = yaw % UnitsPerTurn;
        if (wrapped < 0) wrapped += UnitsPerTurn;
        return wrapped < UnitsPerTurn ? wrapped : 0d;
    }

    /// <summary>Puts a pitch inside the rule's limits.</summary>
    /// <param name="pitch">The pitch to clamp, which must be a number.</param>
    /// <exception cref="ArgumentOutOfRangeException">The pitch is not a number.</exception>
    public double ClampPitch(double pitch)
    {
        if (!double.IsFinite(pitch))
        {
            throw new ArgumentOutOfRangeException(
                nameof(pitch),
                pitch,
                "A pitch must be a number; a facing that is not one cannot be held inside a range.");
        }

        return Math.Clamp(pitch, MinimumPitch, MaximumPitch);
    }

    /// <summary>Puts a pose's facing through the rule, leaving its position where it is.</summary>
    /// <param name="pose">The pose whose facing to normalize.</param>
    /// <exception cref="ArgumentOutOfRangeException">A facing in the pose is not a number.</exception>
    public PlacePose Normalize(PlacePose pose) => pose with
    {
        Yaw = WrapYaw(pose.Yaw),
        Pitch = ClampPitch(pose.Pitch),
    };
}
