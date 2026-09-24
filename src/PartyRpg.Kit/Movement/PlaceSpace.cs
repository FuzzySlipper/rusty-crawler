using System.Numerics;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Movement;

/// <summary>
/// The caller's rule for how one place's own coordinates and facing unit become the engine's world axes
/// and radians.
/// </summary>
/// <remarks>
/// A place keeps the coordinates its own data carries, and the engine's world is a different space: the
/// engine's controller treats Y as height and moves a body along X and Z, so a place whose third
/// coordinate is height has to be laid onto those axes before anything can walk in it. The kit fixes the
/// part it owns — <see cref="PlacePose"/> declares that a place holds a first ground axis, a second
/// ground axis, and height, and that a party's facing grows from the first ground axis toward the second
/// — and leaves the two things it cannot know to the caller: which engine heading a place facing unit of
/// zero means, and how far above a party's pose the engine's own body is centred. Both belong to the
/// place data and to whatever renders it, so both are stated rather than guessed, and a world that
/// stores its places differently passes its own rule instead of editing this one.
/// </remarks>
public readonly record struct PlaceSpace
{
    /// <summary>Creates the rule from the place's facing unit, the heading its zero facing means, and where the engine's body sits.</summary>
    /// <param name="radiansPerFacingUnit">
    /// Radians one place facing unit turns through: one full turn is <c>2π</c> radians, so a world whose
    /// data counts <c>n</c> units to a turn passes <c>2π / n</c>.
    /// </param>
    /// <param name="radiansAtZeroFacing">The engine heading, in radians, that a place facing unit of zero means.</param>
    /// <param name="bodyCentreHeight">
    /// How far above a party's pose, in the place's own length unit, the engine's character body is
    /// centred. The engine sweeps its body about that centre, and a party's pose is where it stands, so
    /// the two differ by half a body; the caller states the difference because it follows from the party's
    /// own height and the shape the tuning hands the engine, and a value worked out here would be a
    /// second copy of the engine's own shape rule that could drift from it.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A value is not a number, the unit turns through nothing, or the body centre is below the pose.
    /// </exception>
    public PlaceSpace(double radiansPerFacingUnit, double radiansAtZeroFacing, double bodyCentreHeight = 0)
    {
        if (!double.IsFinite(radiansPerFacingUnit) || radiansPerFacingUnit <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(radiansPerFacingUnit),
                radiansPerFacingUnit,
                "A place facing unit must turn through a finite, positive angle; a facing cannot be converted through a unit that turns through nothing.");
        }

        if (!double.IsFinite(radiansAtZeroFacing))
        {
            throw new ArgumentOutOfRangeException(
                nameof(radiansAtZeroFacing),
                radiansAtZeroFacing,
                "A place's zero facing must land on a heading that is a number; a heading that is not one cannot be walked toward.");
        }

        if (!double.IsFinite(bodyCentreHeight) || bodyCentreHeight < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bodyCentreHeight),
                bodyCentreHeight,
                "A party's body must be centred at a finite height at or above its pose; a body below the ground it stands on is not a body the engine can walk.");
        }

        RadiansPerFacingUnit = radiansPerFacingUnit;
        RadiansAtZeroFacing = radiansAtZeroFacing;
        BodyCentreHeight = bodyCentreHeight;
    }

    /// <summary>Radians one place facing unit turns through.</summary>
    public double RadiansPerFacingUnit { get; }

    /// <summary>The engine heading, in radians, that a place facing unit of zero means.</summary>
    public double RadiansAtZeroFacing { get; }

    /// <summary>How far above a party's pose the engine's character body is centred, in the place's own length unit.</summary>
    public double BodyCentreHeight { get; }

    /// <summary>
    /// The rule for a world whose places count a whole turn in <paramref name="facing"/>'s units and put
    /// height in their third coordinate.
    /// </summary>
    /// <param name="facing">The facing rule the party's own yaw is stored and wrapped in.</param>
    /// <param name="radiansAtZeroFacing">The engine heading a place facing unit of zero means, in radians.</param>
    /// <param name="bodyCentreHeight">How far above a party's pose the engine's character body is centred.</param>
    /// <exception cref="ArgumentOutOfRangeException">A value is not a number, or the facing rule holds no units.</exception>
    public static PlaceSpace HeightIsThird(FacingRule facing, double radiansAtZeroFacing, double bodyCentreHeight = 0) =>
        new(2 * Math.PI / facing.UnitsPerTurn, radiansAtZeroFacing, bodyCentreHeight);

    /// <summary>The engine world position of a pose in a place.</summary>
    /// <param name="pose">The pose to place, whose facing is ignored.</param>
    /// <exception cref="ArgumentOutOfRangeException">The position is not made of numbers.</exception>
    public Vector3 Position(PlacePose pose)
    {
        RequireFinite(pose.X, nameof(pose));
        RequireFinite(pose.Y, nameof(pose));
        RequireFinite(pose.Z, nameof(pose));

        // The engine's second component is height and its third is the place's second ground axis,
        // negated. That is the one orientation of the place's axes that keeps the engine's own
        // right-handed frame, so a strafe to the party's right stays on the party's right. Height is
        // lifted by the height the caller centres the party's body at, because the engine sweeps a body
        // about its centre while a pose says where the party stands.
        return new Vector3((float)pose.X, (float)(pose.Z + BodyCentreHeight), (float)-pose.Y);
    }

    /// <summary>A pose in a place for an engine world position, keeping the place and facing it is given.</summary>
    /// <param name="position">The engine world position to read back.</param>
    /// <param name="facingFrom">The pose whose place, yaw, and pitch the result keeps.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The position is not made of numbers, so it cannot be read back into a place.
    /// </exception>
    public PlacePose Position(Vector3 position, PlacePose facingFrom)
    {
        double x = position.X;
        double ground = position.Z;
        double height = position.Y;
        if (!double.IsFinite(x) || !double.IsFinite(ground) || !double.IsFinite(height))
        {
            throw new ArgumentOutOfRangeException(
                nameof(position),
                position,
                "An engine position must be made of numbers; one that is not cannot be read back into a place.");
        }

        return facingFrom with { X = x, Y = -ground, Z = height - BodyCentreHeight };
    }

    /// <summary>The engine heading, in radians, of a place facing.</summary>
    /// <param name="yaw">The yaw to convert, in the world's own facing unit.</param>
    /// <exception cref="ArgumentOutOfRangeException">The yaw is not a number.</exception>
    public double FacingRadians(double yaw)
    {
        if (!double.IsFinite(yaw))
        {
            throw new ArgumentOutOfRangeException(
                nameof(yaw),
                yaw,
                "A yaw must be a number; a facing that is not one has no heading to walk toward.");
        }

        // The engine reads a heading as a direction, not as a rotation about the place's own height
        // axis, so a place yaw that grows from the first ground axis toward the second runs against the
        // engine's heading and is subtracted. Pitch never converts: the engine's controller carries no
        // pitch, and where the party looks stays the place's own business.
        return RadiansAtZeroFacing - (yaw * RadiansPerFacingUnit);
    }

    private static void RequireFinite(double value, string parameterName)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "A place position must be made of numbers; a position that is not is not a spot in a place.");
        }
    }
}
