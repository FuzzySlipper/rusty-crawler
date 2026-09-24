namespace PartyRpg.Kit.Movement;

/// <summary>
/// What the player asked the party to do for one step of world motion.
/// </summary>
/// <remarks>
/// An intent is what input produces and what movement consumes: it carries no speed, no heading, and no
/// position, because those are the tuning's and the party's own. <see cref="Forward"/> and
/// <see cref="Strafe"/> are fractions of the speeds the tuning states rather than speeds, so one intent
/// means the same thing on a road, in water, and on ordinary ground, and a surface changes the motion
/// without input having to know what a surface is. <see cref="TurnRate"/> is a rate rather than an
/// amount for the same reason: the same held key turns the party the same way whatever length the
/// admitted step happens to be.
/// </remarks>
public readonly record struct MovementIntent
{
    /// <summary>Creates an intent. Walk and strafe magnitudes are clamped; the turn rate must be a number.</summary>
    /// <param name="forward">Walk input: <c>1</c> is full forward, <c>-1</c> full back, <c>0</c> neither.</param>
    /// <param name="strafe">Strafe input: <c>1</c> is full right, <c>-1</c> full left, <c>0</c> neither.</param>
    /// <param name="turnRate">Turn input, in the world's facing units per second.</param>
    /// <param name="jumpPressed">Whether a jump began in this step, which the engine buffers rather than dropping.</param>
    /// <param name="jumpHeld">Whether the jump control is held, which the tuning decides to honour or not.</param>
    /// <param name="crouch">Whether the party asked to crouch.</param>
    /// <exception cref="ArgumentOutOfRangeException">The turn rate is not a number.</exception>
    public MovementIntent(
        double forward,
        double strafe,
        double turnRate = 0,
        bool jumpPressed = false,
        bool jumpHeld = false,
        bool crouch = false)
    {
        if (!double.IsFinite(turnRate))
        {
            throw new ArgumentOutOfRangeException(
                nameof(turnRate),
                turnRate,
                "A turn rate must be a number; a party cannot be turned at a rate that is not one.");
        }

        // Walk and strafe are clamped rather than refused: the engine rejects a step whose planar intent
        // leaves the unit square, and a player holding two directions at once must not be able to
        // produce a step the engine throws away.
        Forward = Clamp(NonFiniteToZero(forward));
        Strafe = Clamp(NonFiniteToZero(strafe));
        TurnRate = turnRate;
        JumpPressed = jumpPressed;
        JumpHeld = jumpHeld;
        Crouch = crouch;
    }

    /// <summary>Walk input, from full back at <c>-1</c> to full forward at <c>1</c>.</summary>
    public double Forward { get; }

    /// <summary>Strafe input, from full left at <c>-1</c> to full right at <c>1</c>.</summary>
    public double Strafe { get; }

    /// <summary>Turn input, in the world's facing units per second.</summary>
    public double TurnRate { get; }

    /// <summary>Whether a jump began in this step.</summary>
    public bool JumpPressed { get; }

    /// <summary>Whether the jump control is held.</summary>
    public bool JumpHeld { get; }

    /// <summary>Whether the party asked to crouch.</summary>
    public bool Crouch { get; }

    /// <summary>An intent that asks for nothing: the party stands, and the engine still settles it.</summary>
    public static MovementIntent Still => default;

    /// <summary>Whether the player asked for anything at all this step.</summary>
    /// <remarks>
    /// This is for callers that describe the party rather than move it — a stride phase, a walking flag,
    /// a footstep to play — and it is deliberately not a reason to skip a step. A party that asked for
    /// nothing still has to be solved: gravity keeps pulling it, a ledge it walked off still has to land,
    /// and a moving platform under it still has to carry it, and all three belong to the engine.
    /// </remarks>
    public bool IsStill => Forward == 0 && Strafe == 0 && TurnRate == 0 && !JumpPressed && !JumpHeld && !Crouch;

    private static double Clamp(double value) => value switch
    {
        > 1 => 1,
        < -1 => -1,
        _ => value,
    };

    private static double NonFiniteToZero(double value) => double.IsFinite(value) ? value : 0;
}
