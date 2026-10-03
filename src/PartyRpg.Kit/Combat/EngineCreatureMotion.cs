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
/// <b>Each creature walks at its own pace, and the engine is what holds it to it.</b> The pace a request states is
/// put into the controller profile that request hands the engine — the profile's ground speeds become the
/// creature's, and its ground acceleration is scaled with them so a slow creature and a fast one each reach their
/// own pace in the time the profile states — so how far a step carries a creature is the engine's answer to its
/// own pace, not a correction made to the engine's displacement afterwards.
/// </para>
/// <para>
/// <b>What is approximated is stated.</b> Every creature is swept with the party's own body — its shape, slopes,
/// step-up and braking — and only its pace is its own. Creatures do not collide with one another or with the
/// party: the population submits no colliders to the scene yet, which is the same limit the party's own line of
/// sight states. A creature therefore walks through a body rather than around it, and stands wherever the ground
/// admits it.
/// </para>
/// </remarks>
public sealed class EngineCreatureMotion : ICreatureMover
{
    /// <summary>The engine's code for a step refused because the body could not be resolved out of collision.</summary>
    private const string UnresolvedPenetration = "unresolved-character-controller-penetration";

    /// <summary>The slowest a creature's pace may be, as a share of the profile's own.</summary>
    private const double MinimumPaceScale = 0.05;

    /// <summary>The fastest a creature's pace may be, as a multiple of the profile's own.</summary>
    private const double MaximumPaceScale = 4;

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
    /// The engine controller profile a creature is swept with: the party's own. It states the body, the slopes,
    /// and the step-up; each step hands the engine a copy whose ground pace is the creature's own.
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

    /// <inheritdoc />
    public bool IsWalking(CombatantId creature) => _walkers.ContainsKey(creature);

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
        Vector3 target = _space.GroundPosition(request.TargetPose);
        CharacterControllerConfig controller = Paced(request.Speed);

        // Navigation takes feet; the character solver takes the body centre. A refused pursuit holds by
        // name and is tried again on the next admitted update, so moving a target can make it reachable.
        bool placing = false;
        Refusal? navigationHold = null;
        Vector3 steer = target;
        if (request.Purpose == CreatureMovePurpose.Toward)
        {
            if (_scene.Current is not { NavigationCells: > 0 })
            {
                navigationHold = new Refusal(CreatureMoveCodes.Navigation,
                    $"Pursuit holds: {_scene.Current?.NavigationReason ?? "the place has no navigation"}");
                if (_walkers.ContainsKey(request.Creature)) return CreatureMoveOutcome.Held(request.From, navigationHold);
                placing = true;
            }
            else
            {
                NavigationStepResult nav = _spatial.EvaluateNavigationStep(new NavigationStepRequest(
                    _scene.Session, _space.GroundPosition(request.From), target,
                    _scene.Navigation.SteeringStep, _scene.Navigation.SteeringBudget));
                if (nav.Outcome == NavigationPathOutcome.Reached) steer = nav.NextWaypoint;
                else if (nav.Outcome == NavigationPathOutcome.StartNotWalkable && !_walkers.ContainsKey(request.Creature))
                    placing = true; // One stationary body step preserves the existing buried-record settling path.
                else return CreatureMoveOutcome.Held(request.From, new Refusal(CreatureMoveCodes.Navigation,
                    $"Pursuit holds because Engine navigation reports {nav.Outcome}; it will be tried again when the party moves."));
            }
        }
        double x = steer.X - position.X;
        double z = steer.Z - position.Z;
        double distance = Math.Sqrt((x * x) + (z * z));

        // The heading is the direction to what it is walking at, so a creature backing away from a target
        // keeps facing the target rather than the way it is going; a creature already on top of its target
        // keeps the heading it had, because a direction between two identical points is no direction at all.
        float heading = distance > double.Epsilon ? (float)Math.Atan2(x, -z) : walker.Heading;
        CharacterControllerCommand command = new(
            PlanarIntent: placing ? Vector2.Zero : new Vector2(0, request.Purpose == CreatureMovePurpose.Toward ? 1 : -1),
            HeadingYawRadians: heading,
            JumpPressed: false,
            JumpHeld: false,
            CrouchRequested: false,
            ExternalVelocity: Vector3.Zero,
            ExternalImpulse: Vector3.Zero,
            StepSeconds: (float)request.ElapsedSeconds,
            Sequence: ++_sequence);

        PlacePose from = request.From;
        if (Propose(position, walker.Motion, controller, command, out string? embedded) is not { } receipt)
        {
            // The engine could not resolve the body out of the collision it starts in. A creature whose content
            // stood it below the ground — a record that states a nominal height under a hillside — stands on the
            // ground over its feet, as the engine's own ray finds it, and takes its step from there with a fresh
            // continuation. One that has no such ground, or that the engine still cannot step once stood on it,
            // is held where it stands, by name, and not asked again until it leaves the field.
            if (Settle(from) is not { } settled ||
                Propose(_space.Position(settled), default, controller, command, out embedded) is not { } resettled)
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
        return new CreatureMoveOutcome(moved > 0, pose, moved, receipt.Motion.Grounded, navigationHold);
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
    /// <param name="controller">The profile the step is solved with, at the creature's own pace.</param>
    /// <param name="command">What the creature is trying to do.</param>
    /// <param name="embedded">The engine's own sentence when it could not place the body; null otherwise.</param>
    /// <returns>The engine's receipt, or null when it could not place the body.</returns>
    private CharacterStepReceipt? Propose(
        Vector3 position,
        CharacterMotion motion,
        CharacterControllerConfig controller,
        CharacterControllerCommand command,
        out string? embedded)
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
                controller,
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
    /// The party's own controller profile at a creature's stated pace: the profile the engine solves that
    /// creature's step with.
    /// </summary>
    /// <remarks>
    /// The body, the slopes, the step-up and the braking are the profile's; the ground speeds — forward, back and
    /// sideways, which are what the engine's controller walks a body at — become the creature's own pace, and the
    /// ground acceleration is scaled by the same ratio so every creature reaches its own pace in the time the
    /// profile reaches the party's. A creature that states no pace, or a profile with no pace to scale, keeps the
    /// profile's own; the ratio is bounded so a row stating an absurd pace cannot ask the engine for a step it
    /// cannot solve, nor one so slow it never visibly moves.
    /// </remarks>
    /// <param name="speed">The creature's own pace, in place units per second.</param>
    private CharacterControllerConfig Paced(double speed)
    {
        CharacterGroundConfig ground = _controller.Ground;
        double reference = ground.ForwardSpeed;
        if (!double.IsFinite(speed) || speed <= 0 || !(reference > 0)) return _controller;
        float scale = (float)Math.Clamp(speed / reference, MinimumPaceScale, MaximumPaceScale);
        return _controller with
        {
            Ground = ground with
            {
                ForwardSpeed = ground.ForwardSpeed * scale,
                BackwardSpeed = ground.BackwardSpeed * scale,
                StrafeSpeed = ground.StrafeSpeed * scale,
                Acceleration = ground.Acceleration * scale,
            },
        };
    }

    /// <summary>One creature's place in the stream of steps: what the engine gave back for its next one.</summary>
    /// <param name="Motion">The engine's continuation for its next step.</param>
    /// <param name="Heading">The engine heading it last walked along.</param>
    private readonly record struct Walker(CharacterMotion Motion, float Heading)
    {
        internal static Walker At(PlacePose pose, PlaceSpace space) => new(default, (float)space.FacingRadians(pose.Yaw));
    }
}
