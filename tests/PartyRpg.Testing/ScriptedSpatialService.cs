using System.Numerics;
using Rusty.Engine;

namespace PartyRpg.Testing;

/// <summary>
/// An engine spatial service that records what the product asks of it and answers from a script.
/// </summary>
/// <remarks>
/// It does not collide. It answers the calls the product's movement makes with values a test states, and it
/// records every request, so a test can check how a step, an admission, or a navigation query was shaped
/// and which scene it went to. Collision itself is the engine's, and a double that pretended to resolve it
/// would be a second opinion about geometry. Every operation the product does not use throws, so a product
/// that starts depending on one fails a test instead of reading an answer nobody wrote.
/// </remarks>
public sealed class ScriptedSpatialService : ISpatialService
{
    private ulong _nextSession;

    /// <summary>Every scene created, in order, with whether it has been released.</summary>
    public List<(SpatialSession Session, SpatialSessionConfig Config)> Sessions { get; } = [];

    /// <summary>The handles of the scenes that were released, in the order they were.</summary>
    public List<ulong> Released { get; } = [];

    /// <summary>Every content artifact admitted, in order.</summary>
    public List<SpatialContentArtifactReplaceRequest> Admissions { get; } = [];

    /// <summary>Every collision replacement, in order; the product sends one to empty a scene.</summary>
    public List<CollisionReplaceRequest> Replacements { get; } = [];

    /// <summary>Every character step proposed, in order.</summary>
    public List<CharacterStepRequest> Steps { get; } = [];

    /// <summary>Every navigation step evaluated, in order.</summary>
    public List<NavigationStepRequest> NavigationSteps { get; } = [];

    /// <summary>How many navigation cells an admission reports.</summary>
    public ulong NavigationCells { get; set; }

    /// <summary>What a navigation step answers; by default, that the scene has no projection.</summary>
    public Func<NavigationStepRequest, NavigationStepResult> Navigation { get; set; } =
        _ => new NavigationStepResult(default, NavigationPathOutcome.ProjectionUnavailable, default, default, 0, 0, 0, 0, 0);

    /// <summary>Where a character step ends; by default where it started, since this double collides with nothing.</summary>
    public Func<CharacterStepRequest, Vector3> StepEnds { get; set; } = request => request.Position;

    /// <summary>
    /// What a step's receipt says beyond where it ends — a contact, the footing — as the test scripts it; by default the
    /// receipt is left as this double builds it.
    /// </summary>
    public Func<CharacterStepRequest, CharacterStepReceipt, CharacterStepReceipt> Answer { get; set; } = (_, receipt) => receipt;

    /// <inheritdoc />
    public SpatialSession CreateSession(SpatialSessionConfig config)
    {
        ulong handle = ++_nextSession;
        SpatialSession session = new(new SpatialSessionHandle(handle), () => Released.Add(handle));
        Sessions.Add((session, config));
        return session;
    }

    /// <inheritdoc />
    /// <remarks>A body a person's height and a third as wide, which is all a product scales from.</remarks>
    public CharacterControllerConfig DefaultCharacterControllerConfig() => default(CharacterControllerConfig) with
    {
        Shape = default(CharacterShapeConfig) with { StandingHeight = 1.8f, CrouchedHeight = 1.2f, Radius = 0.3f },
    };

    /// <inheritdoc />
    public SpatialContentArtifactReplaceReceipt ReplaceContentArtifact(SpatialContentArtifactReplaceRequest request)
    {
        Admissions.Add(request);
        return new SpatialContentArtifactReplaceReceipt(0, default, 0, 1, 1, 3, 1, NavigationCells, 0, 0);
    }

    /// <inheritdoc />
    public CollisionReplaceReceipt ReplaceCollision(CollisionReplaceRequest request)
    {
        Replacements.Add(request);
        return new CollisionReplaceReceipt(0, 0, 0, 0, 0);
    }

    /// <inheritdoc />
    public CharacterStepReceipt ProposeCharacterStep(CharacterStepRequest request)
    {
        Steps.Add(request);
        Vector3 end = StepEnds(request);
        Transform before = new(request.Position, Quaternion.Identity, Vector3.One);
        Transform after = new(end, Quaternion.Identity, Vector3.One);
        return Answer(request, default(CharacterStepReceipt) with
        {
            TransformBefore = before,
            Transform = after,
            Displacement = end - request.Position,
            Motion = request.Motion with { Grounded = true, LastCommandSequence = request.Command.Sequence },
        });
    }

    /// <inheritdoc />
    public NavigationStepResult EvaluateNavigationStep(NavigationStepRequest request)
    {
        NavigationSteps.Add(request);
        return Navigation(request);
    }

    /// <inheritdoc />
    public CollisionReplaceReceipt ApplyCollisionResidency(CollisionResidencyRequest request) => throw Unsupported();

    /// <inheritdoc />
    public SpatialContentArtifactReadout ReadContentArtifact(SpatialContentArtifactReadRequest request) => throw Unsupported();

    /// <inheritdoc />
    public NavigationReplaceReceipt ReplaceNavigation(NavigationReplaceRequest request) => throw Unsupported();

    /// <inheritdoc />
    public NavigationReplaceReceipt ReplaceVoxelNavigation(NavigationVoxelReplaceRequest request) => throw Unsupported();

    /// <inheritdoc />
    public CollisionNavigationReplaceReceipt ReplaceCollisionNavigation(CollisionNavigationReplaceRequest request) => throw Unsupported();

    /// <inheritdoc />
    public NavigationTraversalReplaceReceipt ReplaceNavigationTraversal(NavigationTraversalReplaceRequest request) => throw Unsupported();

    /// <inheritdoc />
    public NavigationTraversalReplaceReceipt ClearNavigationTraversal(NavigationTraversalClearRequest request) => throw Unsupported();

    /// <inheritdoc />
    public NavigationVolumetricTraversalReplaceReceipt ReplaceVolumetricNavigationTraversal(NavigationVolumetricTraversalReplaceRequest request) => throw Unsupported();

    /// <inheritdoc />
    public NavigationVolumetricTraversalReplaceReceipt ClearVolumetricNavigationTraversal(NavigationVolumetricTraversalClearRequest request) => throw Unsupported();

    /// <inheritdoc />
    public NavigationProjectionReadout ReadNavigationProjection(NavigationProjectionReadRequest request) => throw Unsupported();

    /// <inheritdoc />
    public SpatialMapResult ReadMap(SpatialMapRequest request) => throw Unsupported();

    /// <inheritdoc />
    public NavigationPathResult RequestNavigationPath(NavigationPathRequest request) => throw Unsupported();

    /// <inheritdoc />
    public NavigationWeightedPathResult RequestWeightedNavigationPath(NavigationWeightedPathRequest request) => throw Unsupported();

    /// <inheritdoc />
    public NavigationVolumetricWeightedPathResult RequestWeightedVolumetricNavigationPath(NavigationVolumetricWeightedPathRequest request) => throw Unsupported();

    /// <inheritdoc />
    public NavigationPathResult RequestVolumetricNavigationPath(NavigationVolumetricPathRequest request) => throw Unsupported();

    /// <inheritdoc />
    public void ClearNavigation(NavigationClearRequest request) => throw Unsupported();

    /// <inheritdoc />
    public void ValidateCharacterControllerConfig(CharacterControllerConfig request) => throw Unsupported();

    /// <inheritdoc />
    public void ValidateCharacterControllerCommand(CharacterControllerValidationRequest request) => throw Unsupported();

    /// <inheritdoc />
    public CharacterContinuationCheckpoint CaptureCharacterContinuation(CharacterContinuationCaptureRequest request) => throw Unsupported();

    /// <inheritdoc />
    public CharacterContinuationRestoreReceipt RestoreCharacterContinuation(CharacterContinuationRestoreRequest request) => throw Unsupported();

    /// <inheritdoc />
    public CharacterControllerResult ReadCharacterController(CharacterControllerReadRequest request) => throw Unsupported();

    /// <inheritdoc />
    public SpatialProjectionReadout ReadProjection(SpatialProjectionReadRequest request) => throw Unsupported();

    /// <inheritdoc />
    public SpatialQueryReceipt ContainsPoint(SpatialContainsPointRequest request) => throw Unsupported();

    /// <inheritdoc />
    public SpatialHit CastRay(SpatialRaycastRequest request) => throw Unsupported();

    /// <inheritdoc />
    public SpatialHit CastSegment(SpatialSegmentCastRequest request) => throw Unsupported();

    /// <inheritdoc />
    public SpatialQueryReceipt OverlapAabb(SpatialAabbQueryRequest request) => throw Unsupported();

    /// <inheritdoc />
    public SpatialQueryReceipt SweepAabb(SpatialAabbQueryRequest request) => throw Unsupported();

    /// <inheritdoc />
    public SpatialHit CastCapsule(SpatialCapsuleQueryRequest request) => throw Unsupported();

    /// <inheritdoc />
    public SpatialHit OverlapCapsule(SpatialCapsuleQueryRequest request) => throw Unsupported();

    /// <inheritdoc />
    public SpatialHit PickVoxel(SpatialPickRequest request) => throw Unsupported();

    /// <inheritdoc />
    public void RegisterTrigger(SpatialTriggerRegisterRequest request) => throw Unsupported();

    /// <inheritdoc />
    public SpatialTriggerReconcileResult ReconcileTriggers(SpatialTriggerReconcileRequest request) => throw Unsupported();

    /// <inheritdoc />
    public SpatialTriggerLifecycleResult SetTriggerActive(SpatialTriggerSetActiveRequest request) => throw Unsupported();

    /// <inheritdoc />
    public SpatialTriggerRestoreReceipt RestoreTriggers(SpatialTriggerRestoreRequest request) => throw Unsupported();

    /// <inheritdoc />
    public SpatialTriggerReadResult ReadTrigger(SpatialTriggerReadRequest request) => throw Unsupported();

    private static NotSupportedException Unsupported() =>
        new("The product's movement does not use this spatial operation, so the scripted service does not answer it.");
}
