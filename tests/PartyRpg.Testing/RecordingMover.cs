using System.Numerics;
using PartyRpg.Kit.Movement;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.World;
using Rusty.Engine;

namespace PartyRpg.Testing;

/// <summary>
/// A party mover that records what the session asked of it and moves the party the way a suite states.
/// </summary>
/// <remarks>
/// <para>
/// It does not collide: collision is the engine's, and a double that resolved it would be a second opinion about
/// geometry. What it does is what the engine does in miniature — the party is moved through its pose owner and
/// nowhere else, so a suite holds the same single-writer rule the product does — and it records every step and
/// every place entered, so a suite can check how the session drove it.
/// </para>
/// <para>
/// How far a step goes is the suite's statement, chosen by the factory it composes with: <see cref="Walking"/>
/// moves at the speed a held key would, <see cref="Standing"/> goes nowhere, <see cref="Stepping"/> moves one
/// fixed distance a step, and <see cref="Scripted"/> takes the displacements it is handed in order.
/// </para>
/// </remarks>
public sealed class RecordingMover : IPartyMover
{
    /// <summary>How far a party walks in one second of full forward intent, in world units.</summary>
    public const double WalkingSpeed = 180;

    private readonly PartyPoseOwner _party;
    private readonly Func<MovementIntent, double, int, Motion> _motion;

    private RecordingMover(PartyPoseOwner party, Func<MovementIntent, double, int, Motion> motion)
    {
        _party = party;
        _motion = motion;
    }

    /// <summary>Every step the session asked for, with the admitted interval it covered, in order.</summary>
    public List<(MovementIntent Intent, double Seconds)> Steps { get; } = [];

    /// <summary>Every place the session entered, in order.</summary>
    public List<PlaceId> Entered { get; } = [];

    /// <summary>A place whose ground the engine refuses, or null when it takes every place's.</summary>
    public PlaceId? Refuses { get; set; }

    /// <summary>The fall every step reports.</summary>
    public FallOutcome Fall { get; set; }

    /// <summary>Whether every step ends on the ground.</summary>
    public bool Grounded { get; set; } = true;

    /// <summary>A mover that walks forward at <see cref="WalkingSpeed"/> and turns at the intent's own rate.</summary>
    public static RecordingMover Walking(PartyPoseOwner party) =>
        new(party, (intent, seconds, _) => new Motion(intent.Forward * seconds * WalkingSpeed, 0, 0, intent.TurnRate * seconds));

    /// <summary>A mover that goes nowhere, whatever it is asked.</summary>
    public static RecordingMover Standing(PartyPoseOwner party) => new(party, (_, _, _) => new Motion(0, 0, 0, 0));

    /// <summary>A mover that moves the party the same distance along X on every step.</summary>
    public static RecordingMover Stepping(PartyPoseOwner party, double distance = 1) =>
        new(party, (_, _, _) => new Motion(distance, 0, 0, 0));

    /// <summary>A mover that takes the displacements it is handed in order, repeating the last.</summary>
    public static RecordingMover Scripted(PartyPoseOwner party, IReadOnlyList<(double X, double Y, double Z)> displacements) =>
        new(party, (_, _, step) =>
        {
            (double x, double y, double z) = displacements[Math.Min(step, displacements.Count - 1)];
            return new Motion(x, y, z, 0);
        });

    /// <summary>These movers hold no collision, so nothing occludes anything in them.</summary>
    public bool InSight(Vector3 from, Vector3 to) => true;

    /// <inheritdoc />
    public PlaceGeometryAdmission Enter(PlaceId place)
    {
        Entered.Add(place);
        if (place == Refuses) throw new EngineCallException("Spatial", "ReplaceContentArtifact", 0);
        return PlaceGeometryAdmission.Empty(place);
    }

    /// <inheritdoc />
    public MovementOutcome Step(MovementIntent intent, double elapsedSeconds)
    {
        Motion motion = _motion(intent, elapsedSeconds, Steps.Count);
        Steps.Add((intent, elapsedSeconds));
        if (motion.Turn != 0) _party.Turn(motion.Turn, 0);
        if (motion.X != 0 || motion.Y != 0 || motion.Z != 0) _party.Move(motion.X, motion.Y, motion.Z);
        return new MovementOutcome(
            _party.Capture().Pose,
            new Vector3((float)motion.X, (float)motion.Y, (float)motion.Z),
            Grounded,
            default,
            CharacterBlockFlags.None,
            default,
            SurfaceEffect.Ordinary,
            Fall);
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }

    /// <summary>What one step does: a displacement and a turn.</summary>
    private readonly record struct Motion(double X, double Y, double Z, double Turn);
}
