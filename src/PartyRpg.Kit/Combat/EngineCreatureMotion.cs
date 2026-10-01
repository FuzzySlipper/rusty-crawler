using System.Globalization;
using System.Numerics;
using PartyRpg.Kit.Movement;
using PartyRpg.Kit.Sessions;
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
/// slope holds it, and a drop lands it. Nothing here sweeps a body or decides whether a creature fits
/// anywhere: a second opinion about that would disagree with the first the moment a slope fell between them.
/// </para>
/// <para>
/// <b>A creature content stood inside the ground stands on it, or is held by name.</b> The engine refuses a step
/// whose body starts deeper inside collision than its controller recovers, and leaves what happens to that actor
/// to the product. A creature content placed below a hillside — a record whose height is nominal — is stood on
/// the first surface the engine's own ray meets straight above its feet, within the settling reach the ruleset
/// states, and steps from there; one with no such surface, or one the engine still cannot step once stood on it,
/// is held where it stands with a <see cref="CreatureMoveCodes.Embedded"/> refusal and is not asked again until it
/// leaves the field. Either way the session goes on: a creature that cannot be placed is never a fault.
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
public sealed class EngineCreatureMotion : ICreatureMover
{
    /// <summary>The engine's code for a step refused because the body could not be resolved out of collision.</summary>
    private const string UnresolvedPenetration = "unresolved-character-controller-penetration";

    private readonly ISpatialService _spatial;
    private readonly EnginePartyMover _scene;
    private readonly PlaceSpace _space;
    private readonly CharacterControllerConfig _controller;
    private readonly double _settleReach;
    private readonly Dictionary<CombatantId, Walker> _walkers = [];
    private readonly Dictionary<CombatantId, Refusal> _held = [];
    private ulong _sequence;
    private bool _disposed;

    /// <summary>Creates the mover over the collision scene the party walks in.</summary>
    /// <param name="spatial">The engine service that owns collision and resolves character steps.</param>
    /// <param name="scene">
    /// The party's own mover, which owns the scene a place's collision is admitted to. A creature is stepped
    /// in that scene and no other, and what the scene admitted for the place is what says whether there is
    /// any navigation to steer by.
    /// </param>
    /// <param name="space">How a place's own coordinates and facing unit become the engine's world.</param>
    /// <param name="controller">
    /// The engine controller profile a creature is swept with, which is the party's profile scaled per
    /// creature by its own speed. It states the body, the acceleration, the slopes, and the step-up.
    /// </param>
    /// <param name="settleReach">
    /// How far above its feet, in place units, the ground over a creature content stood inside it may lie and the
    /// creature still be stood on it: the ruleset's statement of how deep its content can bury a record. Zero
    /// stands nobody up, and a creature the engine cannot step is then held where it stands.
    /// </param>
    /// <exception cref="ArgumentNullException">A required collaborator is missing.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The settling reach is negative or not a number.</exception>
    public EngineCreatureMotion(
        ISpatialService spatial,
        EnginePartyMover scene,
        PlaceSpace space,
        CharacterControllerConfig controller,
        double settleReach)
    {
        _spatial = spatial ?? throw new ArgumentNullException(nameof(spatial));
        _scene = scene ?? throw new ArgumentNullException(nameof(scene));
        if (!double.IsFinite(settleReach) || settleReach < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(settleReach),
                settleReach,
                "A settling reach is a distance, zero or more; a creature cannot be stood on ground an unstated distance away.");
        }

        _space = space;
        _controller = controller;
        _settleReach = settleReach;
    }

    /// <summary>How many creatures this mover has moved at least once.</summary>
    public int Walkers => _walkers.Count;

    /// <summary>The creatures the engine could not step, each with why it is held.</summary>
    public IReadOnlyDictionary<CombatantId, Refusal> Held => _held;

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

        // A creature the engine could not place stays held: asking again would only be refused again.
        if (_held.TryGetValue(request.Creature, out Refusal? held)) return CreatureMoveOutcome.Held(request.From, held);

        // Where the creature stands is its own live position, which the caller read from the creature; what
        // this mover keeps between steps is only the engine's motion state and the heading it last faced.
        Walker walker = _walkers.TryGetValue(request.Creature, out Walker found)
            ? found
            : Walker.At(request.From, _space);

        Vector3 position = _space.Position(request.From);
        Vector3 target = _space.Position(request.TargetPose);
        float speed = (float)SpeedScale(request.Speed);
        if (speed <= 0)
        {
            // A creature whose own answer is that it does not move stays where it stands, and the engine is
            // not asked to resolve a step that nothing asked for.
            return CreatureMoveOutcome.Still(request.From);
        }

        // Where to walk is the engine's own answer when it has one: a creature steers at the next walkable
        // point along the way to its target, so a corridor bend or a wall is walked around rather than
        // pushed against. A place with no navigation projection, or a target nothing can reach, leaves the
        // creature walking straight at its target, which is the honest answer when nothing can say better
        // (#8665).
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

        PlacePose from = request.From;
        if (Propose(position, walker.Motion, command, out string? embedded) is not { } receipt)
        {
            // The engine could not resolve the body out of the collision it starts in. A creature whose content
            // stood it below the ground — a record that states a nominal height under a hillside — stands on the
            // ground over its feet, as the engine's own ray finds it, and takes its step from there with a fresh
            // continuation. One that has no such ground, or that the engine still cannot step once stood on it,
            // is held where it stands, by name, and not asked again until it leaves the field.
            if (Settle(from) is not { } settled ||
                Propose(_space.Position(settled), default, command, out embedded) is not { } resettled)
            {
                Refusal refusal = Embedded(from, embedded!);
                _held[request.Creature] = refusal;
                _walkers.Remove(request.Creature);
                return CreatureMoveOutcome.Held(from, refusal);
            }

            from = settled;
            receipt = resettled;
        }

        // How far it moved is measured from where it stood, so a creature stood up out of the ground has moved.
        PlacePose pose = _space.Position(receipt.Transform.Translation, from);
        double moved = (receipt.Transform.Translation - position).Length();
        _walkers[request.Creature] = new Walker(receipt.Motion, heading);
        return new CreatureMoveOutcome(moved > 0, pose, moved, receipt.Motion.Grounded);
    }

    /// <summary>
    /// Asks the engine for one step, or says why the engine could not place the body to take it.
    /// </summary>
    /// <remarks>
    /// The engine's controller recovers a body that starts a little inside collision, within a bounded distance
    /// per step, and refuses one it cannot recover with <c>unresolved-character-controller-penetration</c>
    /// (the engine's C# lifecycle guide leaves what then happens to the actor to the product). That refusal is
    /// the one answered here; any other engine failure is not a fact about where a creature stands and is not
    /// caught.
    /// </remarks>
    /// <param name="position">Where the body's centre starts, in the engine's world.</param>
    /// <param name="motion">The continuation the step carries on from.</param>
    /// <param name="command">What the creature is trying to do.</param>
    /// <param name="embedded">The engine's own sentence when it could not place the body; null otherwise.</param>
    /// <returns>The engine's receipt, or null when it could not place the body.</returns>
    private CharacterStepReceipt? Propose(Vector3 position, CharacterMotion motion, CharacterControllerCommand command, out string? embedded)
    {
        embedded = null;
        try
        {
            return _spatial.ProposeCharacterStep(new CharacterStepRequest(
                _scene.Session,
                position,
                motion,
                default,
                ReadOnlyMemory<CharacterObstacle>.Empty,
                ReadOnlyMemory<CharacterMeshInstance>.Empty,
                _controller,
                command));
        }
        catch (EngineCallException error) when (Unresolved(error) is { } sentence)
        {
            embedded = sentence;
            return null;
        }
    }

    /// <summary>The engine's sentence when a step was refused because the body could not be resolved out of collision.</summary>
    private static string? Unresolved(EngineCallException error)
    {
        foreach (EngineDiagnostic diagnostic in error.Diagnostics.Span)
        {
            if (string.Equals(diagnostic.Code, UnresolvedPenetration, StringComparison.Ordinal))
            {
                return string.IsNullOrWhiteSpace(diagnostic.Message) ? diagnostic.Code : diagnostic.Message;
            }
        }

        return null;
    }

    /// <summary>
    /// Where a creature stands when the ground is over its feet: on the first surface the engine's collision holds
    /// straight above them, within the settling reach; null when there is none.
    /// </summary>
    /// <remarks>
    /// The ray is the engine's own query over the same scene the step is resolved in, cast up the creature's own
    /// column from its feet, so the surface it meets is the underside of whatever buries it. Nothing here decides
    /// whether a body fits there: the step that follows is the engine's answer to that.
    /// </remarks>
    /// <param name="from">Where content or the last step stood the creature.</param>
    private PlacePose? Settle(PlacePose from)
    {
        if (_settleReach <= 0) return null;
        SpatialHit hit = _spatial.CastRay(new SpatialRaycastRequest(
            _scene.Session,
            _space.GroundPosition(from),
            Vector3.UnitY,
            _settleReach,
            new SpatialQueryFilter(0, 0),
            ReadOnlyMemory<SpatialEntityCollider>.Empty,
            ReadOnlyMemory<ulong>.Empty,
            ReadOnlyMemory<SpatialEntityCollider>.Empty));
        if (!hit.Present || !float.IsFinite(hit.Point.Y)) return null;
        double ground = hit.Point.Y;
        PlacePose stood = from;
        return ground > stood.Z ? stood with { Z = ground } : null;
    }

    /// <summary>The refusal a creature the engine cannot place is held with.</summary>
    private Refusal Embedded(PlacePose from, string engine) => new(
        CreatureMoveCodes.Embedded,
        string.Create(
            CultureInfo.InvariantCulture,
            $"The creature at ({from.X:0.#}, {from.Y:0.#}, {from.Z:0.#}) stands inside collision the engine cannot step it out of ({engine}), and no ground over its feet within {_settleReach:0.#} units stands it clear; it is held where it stands."));

    /// <inheritdoc />
    /// <exception cref="ObjectDisposedException">The mover has been disposed.</exception>
    public void Forget(CombatantId creature)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _walkers.Remove(creature);
        _held.Remove(creature);
    }

    /// <inheritdoc />
    /// <exception cref="ObjectDisposedException">The mover has been disposed.</exception>
    public void ForgetAll()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _walkers.Clear();
        _held.Clear();
    }

    /// <summary>Releases the walkers; the collision scene is the party's own and is not this mover's to free.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _walkers.Clear();
        _held.Clear();
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
        // A place whose admission carried no navigation cells has nothing to steer by, so the engine is not
        // asked once per creature per update for an answer that can only be that there is none.
        if (_scene.Current is not { NavigationCells: > 0 }) return target;

        NavigationStepResult nav = _spatial.EvaluateNavigationStep(
            new NavigationStepRequest(_scene.Session, position, target, _scene.Navigation.SteeringStep, _scene.Navigation.SteeringBudget));

        // Only a path the engine found names a waypoint; any other outcome — no path, a budget spent, an end
        // that is not walkable — leaves the creature walking straight at what it wants.
        return nav.Outcome == NavigationPathOutcome.Reached ? nav.NextWaypoint : target;
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

    /// <summary>One creature's place in the stream of steps: what the engine gave back for its next one.</summary>
    /// <param name="Motion">The engine's continuation for its next step.</param>
    /// <param name="Heading">The engine heading it last walked along.</param>
    private readonly record struct Walker(CharacterMotion Motion, float Heading)
    {
        internal static Walker At(PlacePose pose, PlaceSpace space) => new(default, (float)space.FacingRadians(pose.Yaw));
    }
}
