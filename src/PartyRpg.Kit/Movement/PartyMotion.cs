using System.Numerics;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.World;
using Rusty.Engine;

namespace PartyRpg.Kit.Movement;

/// <summary>
/// The party's motion: the engine continuation it is part-way through, and what one engine step means
/// for its pose, its surface, and its fall.
/// </summary>
/// <remarks>
/// <para>
/// This holds no collision, no geometry, and no physics. Every geometric fact it acts on arrives as an
/// engine value — a resolved transform, the ground the engine found, the step its solver took — and what
/// happens here is interpretation: turn the intent into the command the engine solves, put the resolved
/// displacement on the party's pose, and say what the landing cost. A local ray, sweep, or penetration
/// fix would be a second answer to a question the engine has already answered, and the two would differ
/// the first time a slope or a step fell between them.
/// </para>
/// <para>
/// The engine's continuation — velocity, timers, and support — is carried here because it belongs to the
/// stream of steps and not to a pose or a save. It is handed back to the engine unchanged; nothing in the
/// kit reads it as physics.
/// </para>
/// <para>
/// This is separate from <see cref="PartyMovement"/> so that the interpretation of an engine step can be
/// exercised against engine values directly. Without that split, proving what a blocked step, a step-up,
/// or a landing does would need a live engine runtime, and the alternative — a stand-in for the spatial
/// service — would prove the stand-in rather than the engine.
/// </para>
/// </remarks>
public sealed class PartyMotion
{
    private readonly PartyPoseOwner _party;
    private readonly PlaceSpace _space;
    private readonly MovementTuning _tuning;
    private readonly SurfaceClassifier? _classify;
    private CharacterMotion _continuation;
    private SurfaceEffect _surface = SurfaceEffect.Ordinary;
    private ulong _sequence;
    private int _admitted;

    /// <summary>Creates the party's motion over the pose it moves.</summary>
    /// <param name="party">The party's one pose, which this asks to move and never replaces.</param>
    /// <param name="space">How a place's own coordinates and facing unit become the engine's world.</param>
    /// <param name="tuning">The vertical and terrain profile the party moves by.</param>
    /// <param name="surfaces">The rule that names the ground the engine reports. Without one every surface is ordinary.</param>
    /// <exception cref="ArgumentNullException">A required collaborator is missing.</exception>
    public PartyMotion(PartyPoseOwner party, PlaceSpace space, MovementTuning tuning, SurfaceClassifier? surfaces = null)
    {
        _party = party ?? throw new ArgumentNullException(nameof(party));
        _space = space;
        _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
        _classify = surfaces;
    }

    /// <summary>
    /// The engine's continuation for the next step, as the last admitted step returned it.
    /// </summary>
    /// <remarks>
    /// A caller capturing a session for a save takes this and the party's pose together; restoring one
    /// without the other would resume a party whose motion disagrees with where it stands.
    /// </remarks>
    public CharacterMotion Continuation => _continuation;

    /// <summary>The surface the party is on: the last ground the engine resolved, or ordinary ground.</summary>
    public SurfaceEffect Surface => _surface;

    /// <summary>The party's current position in the engine's world axes, read from the party's own pose.</summary>
    public Vector3 Position => _space.Position(_party.PlacePose);

    /// <summary>
    /// Shapes one step's intent into the command the engine solves, turning the party first.
    /// </summary>
    /// <remarks>
    /// The turn is applied before the command is read, so the heading the engine receives is the heading
    /// the party will actually hold when the step lands. Turning is the party's own act and collision
    /// cannot refuse it, which is why it happens here rather than after the engine has answered. The walk
    /// and strafe magnitudes stay fractions of the tuning's speeds and are not scaled here: the engine
    /// owns acceleration, braking, and friction, and pre-scaling would apply them twice.
    /// </remarks>
    /// <param name="intent">What the player asked for this step.</param>
    /// <param name="elapsedSeconds">The admitted world time this step covers, which must be finite and positive.</param>
    /// <returns>The command the engine is asked to resolve.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The step's time is not a finite, positive interval.</exception>
    public CharacterControllerCommand Command(MovementIntent intent, double elapsedSeconds)
    {
        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(elapsedSeconds),
                elapsedSeconds,
                "A step must cover a finite, positive interval; the engine rejects a step that covers no time, and a party cannot be moved by one.");
        }

        if (intent.TurnRate != 0) _party.Turn(intent.TurnRate * elapsedSeconds, 0);

        return new CharacterControllerCommand(
            PlanarIntent: new Vector2((float)intent.Strafe, (float)intent.Forward),
            HeadingYawRadians: (float)_space.FacingRadians(_party.PlacePose.Yaw),
            JumpPressed: intent.JumpPressed,
            JumpHeld: intent.JumpHeld,
            CrouchRequested: intent.Crouch,
            ExternalVelocity: Vector3.Zero,
            ExternalImpulse: Vector3.Zero,
            StepSeconds: (float)elapsedSeconds,
            Sequence: ++_sequence);
    }

    /// <summary>
    /// Puts one resolved engine step onto the party's pose and reports what it means.
    /// </summary>
    /// <remarks>
    /// The party is moved by the displacement the engine resolved, taken between the transform it
    /// reported before the step and the one after. Moving by that difference rather than by the wish is
    /// what makes a wall slide the party along it instead of stopping it dead: the engine has already
    /// projected the motion onto the surfaces it hit, and nothing here second-guesses it. The engine
    /// remains the authority on where the party is, because the position the next step starts from is
    /// this pose.
    /// </remarks>
    /// <param name="receipt">The step the engine resolved for the party.</param>
    /// <returns>Where the party ended up and what the engine and the tuning said about it.</returns>
    public MovementOutcome Admit(CharacterStepReceipt receipt)
    {
        FallOutcome fall = Landed(receipt);
        SurfaceEffect surface = Recognise(receipt.Ground);

        PlacePose before = _space.Position(receipt.TransformBefore.Translation, _party.PlacePose);
        PlacePose after = _space.Position(receipt.Transform.Translation, before);
        _party.Move(after.X - before.X, after.Y - before.Y, after.Z - before.Z);

        _continuation = receipt.Motion;
        _surface = surface;
        _admitted++;

        return new MovementOutcome(
            _party.Capture().Pose,
            receipt.Displacement,
            receipt.Motion.Grounded,
            receipt.Step,
            receipt.BlockFlags,
            receipt.Stance,
            surface,
            fall);
    }

    /// <summary>
    /// What a landing in this step cost, measured from the engine's own fall facts.
    /// </summary>
    /// <remarks>
    /// The engine keeps the highest point the party has reached since it was last supported, so a jump
    /// that comes back down to where it started is a fall of the height it rose — which is what a
    /// threshold is for, and why jumping needs no rule of its own. The first step of a session is never a
    /// landing: there is no previous motion to have fallen from, and reading a starting height as a drop
    /// would charge the party for being placed where the game starts it.
    /// </remarks>
    private FallOutcome Landed(CharacterStepReceipt receipt)
    {
        if (_admitted == 0 || _continuation.Grounded || !receipt.Motion.Grounded) return FallOutcome.None;

        // Both heights are the engine's capsule centre rather than the party's feet, so their difference
        // is the drop itself and needs no shape of its own to be worked out.
        return _tuning.Falls.Consequence(_continuation.PeakY - receipt.Transform.Translation.Y);
    }

    /// <summary>
    /// The surface of the ground the engine resolved, keeping the last one while the party is airborne.
    /// </summary>
    /// <remarks>
    /// A party in the air is over the ground it left, so keeping that surface is what makes a jump across
    /// water land in water and a jump from a road keep the road's speeds. Ground the classifier does not
    /// recognise is ordinary ground and not an error: content may name a surface before the tuning prices
    /// it, and movement must not stop for a table that has not caught up.
    /// </remarks>
    private SurfaceEffect Recognise(CharacterGround ground)
    {
        if (_classify is null || !ground.Present) return _surface;
        return _classify(ground, out string surfaceId) ? _tuning.Surface(surfaceId) : SurfaceEffect.Ordinary;
    }
}
