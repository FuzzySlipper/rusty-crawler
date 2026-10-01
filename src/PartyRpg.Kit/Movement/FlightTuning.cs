namespace PartyRpg.Kit.Movement;

/// <summary>
/// How a party moves while it flies: the engine's flying mode in the tuning's own units, and how high it may go.
/// </summary>
/// <remarks>
/// <para>
/// Flight is the engine's: its controller sweeps a flying body through the same collision a walking one meets, with
/// no gravity and no floor snap, and turns a walk, a strafe and a rise into one velocity bounded by one speed. What
/// is the product's is the profile it asks for — how fast, how quickly it gets there, how fast it bleeds speed with
/// no input — and the height above which a rise asks for nothing more. Whether the party may fly at all is not here:
/// that is the game's answer, asked of an <see cref="IFlightRule"/> every step.
/// </para>
/// <para>
/// The ceiling is a height in the place's own coordinates, compared with the party's pose, so a game that states its
/// ceiling in its own units states it once.
/// </para>
/// </remarks>
public sealed record FlightTuning
{
    /// <summary>Creates a flight profile.</summary>
    /// <param name="speed">The greatest speed of a flying party, in the engine's length unit per second.</param>
    /// <param name="acceleration">How quickly a flying party reaches what it asks for, in the engine's length unit per second squared.</param>
    /// <param name="drag">How fast a flying party loses speed it is not asking for, as the engine's exponential drag per second.</param>
    /// <param name="ceiling">The height, in the place's own coordinates, at and above which a rise asks for nothing.</param>
    /// <exception cref="ArgumentOutOfRangeException">A value is not a finite number in its range.</exception>
    public FlightTuning(double speed, double acceleration, double drag, double ceiling)
    {
        Speed = Positive(speed, nameof(speed));
        Acceleration = Positive(acceleration, nameof(acceleration));
        if (!double.IsFinite(drag) || drag < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(drag), drag, "Drag is a finite rate of no less than nothing; the engine refuses any other.");
        }

        if (!double.IsFinite(ceiling))
        {
            throw new ArgumentOutOfRangeException(nameof(ceiling), ceiling, "A ceiling is a height somebody could measure.");
        }

        Drag = drag;
        Ceiling = ceiling;
    }

    /// <summary>The greatest speed of a flying party, in the engine's length unit per second.</summary>
    public double Speed { get; }

    /// <summary>How quickly a flying party reaches what it asks for.</summary>
    public double Acceleration { get; }

    /// <summary>How fast a flying party loses speed it is not asking for.</summary>
    public double Drag { get; }

    /// <summary>The height, in the place's own coordinates, at and above which a rise asks for nothing.</summary>
    public double Ceiling { get; }

    private static double Positive(double value, string name) =>
        double.IsFinite(value) && value > 0
            ? value
            : throw new ArgumentOutOfRangeException(name, value, "Flight needs a finite speed and acceleration above nothing; a party that cannot move in the air does not fly.");
}

/// <summary>The game's answer to whether the party may fly now.</summary>
/// <remarks>
/// The kit holds the mechanism — the flying mode asked of the engine, the rise and the sink, the landing, the fall when
/// flight ends in the air — and nothing about why a party flies. A game answers here from its own state: what the
/// party carries, where it stands, and what keeping it aloft still costs. The movement owner asks every step, so a
/// flight that ends in the air ends at the step it ended, and the party falls from where it was.
/// </remarks>
public interface IFlightRule
{
    /// <summary>Whether the party may fly now.</summary>
    bool MayFly { get; }
}
