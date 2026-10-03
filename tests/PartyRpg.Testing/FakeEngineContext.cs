using Rusty.Engine;

namespace PartyRpg.Testing;

/// <summary>
/// An engine context for exercising the product without an engine runtime.
/// </summary>
/// <remarks>
/// Every service except the UI is unreachable: the product uses no other engine service yet, and a
/// double that quietly answered for one would let a product start depending on a service its tests
/// never proved. When a stone attaches a mechanism to another service, the double gains it then, and
/// the test that needs it is written with it. Persistence has arrived that way: a session composes its
/// save store over the engine's persistence service, so the double carries one when a test asks for it.
/// Movement has too: a test that asks for a scripted spatial service (and the content service a place's
/// collision artifact is admitted through) gets a product that composes its movers over them.
/// </remarks>
public sealed class FakeEngineContext : IEngineContext
{
    private readonly IPersistenceService? _persistence;
    private readonly ISpatialService? _spatial;
    private readonly IContentService? _content;
    private readonly IGraphicsService? _graphics;
    private readonly ICameraViewService? _cameras;
    private readonly IPresentationService? _presentation;
    private readonly TestRandomService _random = new();

    public FakeEngineContext(
        RecordingUiService ui,
        IPersistenceService? persistence = null,
        ISpatialService? spatial = null,
        IContentService? content = null,
        IGraphicsService? graphics = null,
        ICameraViewService? cameras = null,
        IPresentationService? presentation = null)
    {
        Ui = ui;
        _persistence = persistence;
        _spatial = spatial;
        _content = content;
        _graphics = graphics;
        _cameras = cameras;
        _presentation = presentation;
    }

    /// <summary>The random service this context answers with, which a test states the roll of.</summary>
    public TestRandomService RandomService => _random;

    public IUiService Ui { get; }

    public IInputService Input => Unsupported<IInputService>();

    public IImplicitSurfacesService ImplicitSurfaces => Unsupported<IImplicitSurfacesService>();

    /// <summary>
    /// Diagnostics are reported as absent, like the spatial service: this suite has no engine runtime,
    /// and a diagnostic sink that swallowed reports would hide exactly what a test should see.
    /// </summary>
    public IDiagnosticsService Diagnostics => null!;

    public IDynamicsService Dynamics => Unsupported<IDynamicsService>();

    public IMotionService Motion => Unsupported<IMotionService>();

    public IKinematicService Kinematic => Unsupported<IKinematicService>();

    /// <summary>
    /// Absent unless a test asks for the scripted one: the product composes movement only when an engine
    /// supplies a spatial service, and the scripted service records what the product asks of it rather than
    /// pretending to collide.
    /// </summary>
    public ISpatialService Spatial => _spatial!;

    public IPerceptionService Perception => Unsupported<IPerceptionService>();

    public IWorldOriginService WorldOrigin => Unsupported<IWorldOriginService>();

    public IVoxelService Voxel => Unsupported<IVoxelService>();

    public IVoxelContentService VoxelContent => Unsupported<IVoxelContentService>();

    public IVoxelScenePresentationService VoxelScenePresentation => Unsupported<IVoxelScenePresentationService>();

    public IContentService Content => _content ?? Unsupported<IContentService>();

    public IAuthoredContentService AuthoredContent => Unsupported<IAuthoredContentService>();

    /// <summary>
    /// Absent unless a test asks for a recording one: the world view draws through graphics and the camera, and a test
    /// that composes it states what it expects asked of them (<see cref="RecordingEngineService{T}"/>).
    /// </summary>
    public IGraphicsService Graphics => _graphics ?? Unsupported<IGraphicsService>();

    /// <summary>Absent unless a test asks for a recording one: the world view emits a resolved blow's burst through it.</summary>
    public IPresentationService Presentation => _presentation ?? Unsupported<IPresentationService>();

    public IAnimationService Animation => Unsupported<IAnimationService>();

    public IAudioService Audio => Unsupported<IAudioService>();

    public IVideoService Video => Unsupported<IVideoService>();

    public ICameraViewService CameraView => _cameras ?? Unsupported<ICameraViewService>();

    /// <summary>
    /// The engine's random service, which this stone attached the camping risk to: a product that camps
    /// draws a keyed roll for the night, so the double gains the one operation the product uses and says so
    /// for the rest rather than pretending to be a generator.
    /// </summary>
    public IRandomService Random => _random;

    public IPersistenceService Persistence => _persistence ?? Unsupported<IPersistenceService>();

    public IRenderOutputService RenderOutput => Unsupported<IRenderOutputService>();

    private static T Unsupported<T>() =>
        throw new NotSupportedException($"{typeof(T).Name} is not used by this product yet, so the test context does not provide it.");
}

/// <summary>
/// The engine's random service, answering the one operation this product draws with.
/// </summary>
/// <remarks>
/// The product draws a keyed roll for a night in the open — deterministic from the seed, the scope, and the
/// key — so the double answers that operation with a value the test states, clamped into the range the
/// caller asked for. Every other operation is refused rather than faked: a scoped stream this suite never
/// opens is a capability nobody proved, and a double that answered it would let the product start depending
/// on one.
/// </remarks>
public sealed class TestRandomService : IRandomService
{
    /// <summary>What every keyed draw answers with, clamped into the range the caller asked for.</summary>
    public long Roll { get; set; } = 100;

    /// <summary>
    /// What a draw answers for the request it was asked, or null to answer <see cref="Roll"/>.
    /// </summary>
    /// <remarks>
    /// Answering null leaves the fixed <see cref="Roll"/> in place, which is what lets a test script one
    /// kind of draw — a resistance check — without disturbing the hit roll and the damage dice of the same
    /// blow.
    /// </remarks>
    /// <remarks>
    /// Some mechanisms draw more than once for one act — a resistance is checked up to four times, and a
    /// check that fails ends the halving — and a single fixed answer can only ever show all four or none.
    /// A test that scripts the draws per request can show the middle the arithmetic is actually about.
    /// </remarks>
    public Func<KeyedRngRequest, long?>? Answer { get; set; }

    /// <summary>How many keyed draws were taken, so a test can tell a roll from a guess.</summary>
    public int Draws { get; private set; }

    /// <inheritdoc />
    public KeyedRngReceipt DrawKeyed(KeyedRngRequest request)
    {
        Draws++;
        long value = Answer?.Invoke(request) ?? Roll;
        return new KeyedRngReceipt(Math.Clamp(value, request.Minimum, request.Maximum));
    }

    /// <inheritdoc />
    public Lcg15Receipt DrawLcg15(Lcg15Request request) => Unsupported<Lcg15Receipt>();

    /// <inheritdoc />
    public Rng CreateScoped(ScopedRngCreateRequest request) => Unsupported<Rng>();

    /// <inheritdoc />
    public Rng ForkScoped(ScopedRngForkRequest request) => Unsupported<Rng>();

    /// <inheritdoc />
    public RngValue NextU64(Rng stream) => Unsupported<RngValue>();

    /// <inheritdoc />
    public RngValue NextBoundedU32(ScopedRngBoundedRequest request) => Unsupported<RngValue>();

    /// <inheritdoc />
    public RngValue NextBool(Rng stream) => Unsupported<RngValue>();

    private static T Unsupported<T>() =>
        throw new NotSupportedException("This product draws keyed rolls only, so the test random service answers that one operation.");
}

/// <summary>An engine UI service that records what the product publishes.</summary>
public sealed class RecordingUiService : IUiService
{
    private readonly List<UiProjection> _projections = [];

    public IReadOnlyList<UiProjection> Projections => _projections;

    public UiStreamRequest? LastRequest { get; private set; }

    public UiProjection Latest() =>
        _projections.Count > 0 ? _projections[^1] : throw new InvalidOperationException("Nothing was published.");

    public UiStream OpenStream(UiStreamRequest request)
    {
        LastRequest = request;
        return new UiStream(new UiStreamHandle(1), () => { });
    }

    public void PublishProjection(UiProjection projection) => _projections.Add(projection);

    /// <summary>The product opens no UI images; a test that reaches this has found a new dependency.</summary>
    public UiImage OpenImage(UiImageRequest request) =>
        throw new NotSupportedException("The product opens no UI images, so the recording UI service does not provide them.");
}
