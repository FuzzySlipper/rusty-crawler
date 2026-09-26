using System.Numerics;
using PartyRpg.Kit.Movement;
using PartyRpg.Kit.World;
using Rusty.Engine;

namespace PartyRpg.Kit.Combat;

/// <summary>
/// The engine-backed creature mover: one creature's step, resolved by the engine's own collision.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the same service and the same scene the party walks in.</b> A step is handed to the engine's
/// spatial session with the creature's own position and the continuation the engine gave back last time,
/// and the displacement the engine resolved is what moves it — walls slide it, a step-up carries it, a
/// slope holds it, and a drop lands it. Nothing here casts a ray, sweeps a body, or decides whether a
/// creature fits anywhere: a second opinion about that would disagree with the first the moment a slope
/// fell between them.
/// </para>
/// <para>
/// <b>Each creature owns its own continuation.</b> The engine's continuation — velocity, timers, support —
/// belongs to the stream of steps one body takes, so it is kept per creature and handed back unchanged.
/// Its position is kept with it, because a creature that has moved is not where content placed it, and the
/// pose is what the fight and the panel read.
/// </para>
/// <para>
/// <b>What is approximated is stated.</b> Every creature is swept with the party's own body profile, scaled
/// by the speed its own ruleset answer gives it, and creatures do not collide with one another or with the
/// party: the population submits no colliders to the scene yet, which is the same limit the party's own
/// line of sight states. A creature therefore walks through a body rather than around it, and stands
/// wherever the ground admits it.
/// </para>
/// </remarks>
public sealed class EngineCreatureMotion : ICreatureMover, IDisposable
{
    /// <summary>How far ahead the engine's navigation is asked to look for a walkable step, in place units.</summary>
    /// <remarks>
    /// A creature steers at the next walkable waypoint rather than at its target, so a wall between the two
    /// is walked around instead of pushed against. The distance is the scale of a doorway or a corridor
    /// bend in this game's places; it is not a claim about the creature's stride, because the engine's own
    /// character step is what moves it.
    /// </remarks>
    private const float NavigationStepUnits = 512;

    /// <summary>How many navigation cells one steering query may visit before it gives up.</summary>
    /// <remarks>
    /// A budget rather than a promise: a creature that cannot find a way walks straight at its target, which
    /// is what this mover did before it asked at all, and a query that explored a whole region every update
    /// for every creature would cost more than the fight it serves.
    /// </remarks>
    private const uint NavigationBudget = 1024;

    private readonly ISpatialService _spatial;
    private readonly SpatialSession _session;
    private readonly PlaceSpace _space;
    private readonly CharacterControllerConfig _controller;
    private readonly Dictionary<CombatantId, Walker> _walkers = [];
    private ulong _sequence;
    private bool _disposed;

    /// <summary>Creates the mover over the collision scene the party walks in.</summary>
    /// <param name="spatial">The engine service that owns collision and resolves character steps.</param>
    /// <param name="session">The scene the place's collision was admitted to, which is the party's own.</param>
    /// <param name="space">How a place's own coordinates and facing unit become the engine's world.</param>
    /// <param name="controller">
    /// The engine controller profile a creature is swept with, which is the party's profile scaled per
    /// creature by its own speed. It states the body, the acceleration, the slopes, and the step-up.
    /// </param>
    /// <exception cref="ArgumentNullException">A required collaborator is missing.</exception>
    public EngineCreatureMotion(
        ISpatialService spatial,
        SpatialSession session,
        PlaceSpace space,
        CharacterControllerConfig controller)
    {
        _spatial = spatial ?? throw new ArgumentNullException(nameof(spatial));
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
        _space = space;
        _controller = controller;
    }

    /// <summary>How many creatures this mover has moved at least once.</summary>
    public int Walkers => _walkers.Count;

    /// <inheritdoc />
    /// <exception cref="ObjectDisposedException">The mover has been disposed.</exception>
    public PlacePose? PoseOf(CombatantId creature)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _walkers.TryGetValue(creature, out Walker walker) ? walker.Pose : null;
    }

    /// <inheritdoc />
    /// <exception cref="ObjectDisposedException">The mover has been disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The step covers no time, so nothing could move.</exception>
    public CreatureMoveOutcome Move(CreatureMoveRequest request)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!double.IsFinite(request.ElapsedSeconds) || request.ElapsedSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                request.ElapsedSeconds,
                "A creature's step must cover a finite, positive interval; the engine rejects a step that covers no time.");
        }

        // Where the creature stands is what this mover last resolved, not what the caller believes: a
        // driver that missed a step would otherwise teleport the creature back to where it last looked.
        Walker walker = _walkers.TryGetValue(request.Creature, out Walker found)
            ? found
            : Walker.At(request.From, _space);

        Vector3 position = _space.Position(walker.Pose);
        Vector3 target = _space.Position(request.TargetPose);
        float speed = (float)SpeedScale(request.Speed);
        if (speed <= 0)
        {
            // A creature whose own answer is that it does not move stays where it stands, and the engine is
            // not asked to resolve a step that nothing asked for.
            return CreatureMoveOutcome.Still(walker.Pose);
        }

        // Where to walk is the engine's own answer when it has one: a creature steers at the next walkable
        // point along the way to its target, so a corridor bend or a wall is walked around rather than
        // pushed against. A place with no navigation projection, or a target nothing can reach, leaves the
        // creature walking straight at its target, which is the honest answer when nothing can say better.
        Vector3 steer = Steer(position, target);
        double x = steer.X - position.X;
        double z = steer.Z - position.Z;
        double distance = Math.Sqrt((x * x) + (z * z));

        // The heading is the direction to what it is walking at, so a creature backing away from a target
        // keeps facing the target rather than the way it is going; a creature already on top of its target
        // keeps the heading it had, because a direction between two identical points is no direction at all.
        float heading = distance > double.Epsilon ? (float)Math.Atan2(x, -z) : walker.Heading;
        CharacterControllerCommand command = new(
            PlanarIntent: new Vector2(0, request.Purpose == CreatureMovePurpose.Toward ? 1 : -1),
            HeadingYawRadians: heading,
            JumpPressed: false,
            JumpHeld: false,
            CrouchRequested: false,
            ExternalVelocity: Vector3.Zero,
            ExternalImpulse: Vector3.Zero,
            StepSeconds: (float)request.ElapsedSeconds,
            Sequence: ++_sequence);

        CharacterStepReceipt receipt = _spatial.ProposeCharacterStep(new CharacterStepRequest(
            _session,
            position,
            walker.Motion,
            default,
            ReadOnlyMemory<CharacterObstacle>.Empty,
            ReadOnlyMemory<CharacterMeshInstance>.Empty,
            _controller,
            command));

        PlacePose pose = _space.Position(receipt.Transform.Translation, walker.Pose);
        double moved = (receipt.Transform.Translation - position).Length();
        _walkers[request.Creature] = new Walker(pose, receipt.Motion, heading);
        return new CreatureMoveOutcome(moved > 0, pose, moved, receipt.Motion.Grounded);
    }

    /// <inheritdoc />
    /// <exception cref="ObjectDisposedException">The mover has been disposed.</exception>
    public void Forget(CombatantId creature)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _walkers.Remove(creature);
    }

    /// <inheritdoc />
    /// <exception cref="ObjectDisposedException">The mover has been disposed.</exception>
    public void ForgetAll()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _walkers.Clear();
    }

    /// <summary>Releases the walkers; the collision scene is the party's own and is not this mover's to free.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _walkers.Clear();
    }

    /// <summary>
    /// The point a creature should walk toward: the engine's next walkable waypoint, or the target itself
    /// when the engine has no navigation to steer by or no way to get there.
    /// </summary>
    /// <remarks>
    /// The query is the read-only one. The engine also offers a proposal that retains the path it found, and
    /// that retained path belongs to the scene rather than to a creature: one creature asking would throw
    /// away the answer another was about to use.
    /// </remarks>
    /// <param name="position">Where the creature stands, in the engine's world.</param>
    /// <param name="target">What it is walking at.</param>
    private Vector3 Steer(Vector3 position, Vector3 target)
    {
        NavigationStepReceipt nav = _spatial.EvaluateNavigationStep(
            new NavigationStepRequest(_session, position, target, NavigationStepUnits, NavigationBudget));

        // A waypoint the engine did not state is no waypoint: a creature in a place with no navigation, or
        // one whose target nothing can reach, walks straight at what it wants.
        return nav.NextWaypoint == Vector3.Zero ? target : nav.NextWaypoint;
    }

    /// <summary>
    /// How the party's own controller profile is scaled for a creature of a stated speed.
    /// </summary>
    /// <remarks>
    /// The profile is the party's — the body, the acceleration, the slopes, the step-up — and only the
    /// creature's own pace changes: a creature moves at the speed its own content states, which is a
    /// property of the creature rather than of the geometry it walks on, and the ruleset that reads its
    /// row is where that number comes from. A creature that states no speed, or one whose speed the
    /// profile cannot express, keeps the profile's own; the scale is capped so a row stating an absurd
    /// speed cannot ask the engine for a step it cannot solve.
    /// </remarks>
    private double SpeedScale(double speed)
    {
        double reference = _controller.Ground.ForwardSpeed;
        if (!double.IsFinite(speed) || speed <= 0 || reference <= 0) return 1;
        return Math.Clamp(speed / reference, 0.05, 4);
    }

    /// <summary>One creature's place in the stream of steps: where it stands and what the engine gave back.</summary>
    /// <param name="Pose">Where it stands, in the place's own units.</param>
    /// <param name="Motion">The engine's continuation for its next step.</param>
    /// <param name="Heading">The engine heading it last walked along.</param>
    private readonly record struct Walker(PlacePose Pose, CharacterMotion Motion, float Heading)
    {
        internal static Walker At(PlacePose pose, PlaceSpace space) => new(pose, default, (float)space.FacingRadians(pose.Yaw));
    }
}
