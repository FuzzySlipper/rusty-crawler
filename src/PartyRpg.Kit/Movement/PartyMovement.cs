using PartyRpg.Kit.Party;
using Rusty.Engine;

namespace PartyRpg.Kit.Movement;

/// <summary>
/// The movement owner: it turns movement intents into world motion through the engine's spatial service
/// and reports where the party ended up.
/// </summary>
/// <remarks>
/// <para>
/// Everything geometric — what the party can stand on, what stops it, how high a ledge it will climb, how
/// steep a slope it will hold, how far it falls — is the engine's, which owns the collision scene, the
/// sweep, and the solver. This class owns none of it: it shapes an intent into a command, hands the
/// engine the party's current position and the continuation the engine gave back last time, and accepts
/// the transform the engine resolved. There is no cast, no sweep, no penetration correction, and no
/// gravity integration here, and there must never be one, because a second opinion about where the party
/// can stand would disagree with the first the moment a slope or a step fell between them.
/// </para>
/// <para>
/// The party's pose stays the pose owner's: a step asks that owner to move, so there is still exactly one
/// place a party position is held and a view derived from it is derived from it alone. What an engine
/// step then means — the surface, the fall, whether a step-up happened — is <see cref="PartyMotion"/>'s,
/// which holds the engine continuation and can be exercised against engine values without an engine
/// runtime.
/// </para>
/// </remarks>
public sealed class PartyMovement : IDisposable
{
    private readonly ISpatialService _spatial;
    private readonly SpatialSession _session;
    private readonly MovementTuning _tuning;
    private bool _disposed;

    /// <summary>Creates the party's movement over an engine spatial session of its own.</summary>
    /// <param name="spatial">The engine service that owns collision and resolves character steps.</param>
    /// <param name="party">The party's one pose, which movement asks to move and never replaces.</param>
    /// <param name="space">How a place's own coordinates and facing unit become the engine's world.</param>
    /// <param name="session">
    /// How the collision scene is chunked and what size its voxels are. The scale is the world's, so it
    /// comes from whoever owns the place data rather than from a default here.
    /// </param>
    /// <param name="tuning">
    /// The vertical and terrain profile the party moves by. Without one, the engine's own default
    /// controller configuration is used and every surface is ordinary, so a party moves correctly and
    /// costs nothing to fall.
    /// </param>
    /// <param name="surfaces">
    /// The rule that names the ground the engine reports. Without one every surface is ordinary ground.
    /// </param>
    /// <exception cref="ArgumentNullException">A required collaborator is missing.</exception>
    public PartyMovement(
        ISpatialService spatial,
        PartyPoseOwner party,
        PlaceSpace space,
        SpatialSessionConfig session,
        MovementTuning? tuning = null,
        SurfaceClassifier? surfaces = null)
    {
        _spatial = spatial ?? throw new ArgumentNullException(nameof(spatial));
        ArgumentNullException.ThrowIfNull(party);
        _tuning = tuning ?? new MovementTuning(spatial.DefaultCharacterControllerConfig(), FallPolicy.Free);
        _session = spatial.CreateSession(session);
        Motion = new PartyMotion(party, space, _tuning, surfaces);
    }

    /// <summary>
    /// The collision scene the party is walked in. Whoever owns a place's geometry admits its collision
    /// here, so the party and the ground beneath it are the same scene by construction.
    /// </summary>
    public SpatialSession Session => _session;

    /// <summary>The party's motion: the engine continuation, the surface, and what a step means.</summary>
    public PartyMotion Motion { get; }

    /// <summary>
    /// Moves the party by one step of the world's admitted time.
    /// </summary>
    /// <remarks>
    /// The surface the party stands on is known only once a step has resolved it, so a surface it has
    /// just stepped onto governs the step after that one. That is the honest order: the engine decides
    /// what the party is standing on, and tuning follows that rather than predicting it.
    /// </remarks>
    /// <param name="intent">What the player asked for this step.</param>
    /// <param name="elapsedSeconds">The admitted world time this step covers, which must be finite and positive.</param>
    /// <param name="obstacles">
    /// The colliders the place's population currently presents, which the engine reads for this step only.
    /// Whoever owns the population submits its current facts every step; the engine retains none of them.
    /// </param>
    /// <param name="support">
    /// The moving support the party stands on, when the population is carrying it. The engine carries a
    /// party from the support facts it is given each step rather than from anything it kept.
    /// </param>
    /// <returns>Where the party ended up and what the engine and the tuning said about it.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The step's time is not a finite, positive interval.</exception>
    /// <exception cref="ObjectDisposedException">The movement has been disposed.</exception>
    public MovementOutcome Step(
        MovementIntent intent,
        double elapsedSeconds,
        ReadOnlyMemory<CharacterObstacle> obstacles = default,
        CharacterSupport support = default,
        ReadOnlyMemory<CharacterMeshInstance> movingMeshes = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        CharacterStepRequest request = new(
            _session,
            Motion.Position,
            Motion.Continuation,
            support,
            obstacles,
            movingMeshes,
            _tuning.ControllerOn(Motion.Surface),
            Motion.Command(intent, elapsedSeconds));

        return Motion.Admit(_spatial.ProposeCharacterStep(request));
    }

    /// <summary>Releases the engine's spatial session, which destroys its collision scene with it.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _session.Dispose();
    }
}
