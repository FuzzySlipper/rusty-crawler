using Rusty.Engine;

namespace PartyRpg.Host.Tests;

/// <summary>
/// An engine context for exercising the product without an engine runtime.
/// </summary>
/// <remarks>
/// Every service except the UI is unreachable: the product uses no other engine service yet, and a
/// double that quietly answered for one would let a product start depending on a service its tests
/// never proved. When a stone attaches a mechanism to another service, the double gains it then, and
/// the test that needs it is written with it.
/// </remarks>
internal sealed class FakeEngineContext : IEngineContext
{
    internal FakeEngineContext(RecordingUiService ui) => Ui = ui;

    public IUiService Ui { get; }

    public IInputService Input => Unsupported<IInputService>();

    public IImplicitSurfacesService ImplicitSurfaces => Unsupported<IImplicitSurfacesService>();

    public IDiagnosticsService Diagnostics => Unsupported<IDiagnosticsService>();

    public IDynamicsService Dynamics => Unsupported<IDynamicsService>();

    public IMotionService Motion => Unsupported<IMotionService>();

    public IKinematicService Kinematic => Unsupported<IKinematicService>();

    public ISpatialService Spatial => Unsupported<ISpatialService>();

    public IPerceptionService Perception => Unsupported<IPerceptionService>();

    public IWorldOriginService WorldOrigin => Unsupported<IWorldOriginService>();

    public IVoxelService Voxel => Unsupported<IVoxelService>();

    public IVoxelContentService VoxelContent => Unsupported<IVoxelContentService>();

    public IVoxelScenePresentationService VoxelScenePresentation => Unsupported<IVoxelScenePresentationService>();

    public IContentService Content => Unsupported<IContentService>();

    public IAuthoredContentService AuthoredContent => Unsupported<IAuthoredContentService>();

    public IGraphicsService Graphics => Unsupported<IGraphicsService>();

    public IPresentationService Presentation => Unsupported<IPresentationService>();

    public IAnimationService Animation => Unsupported<IAnimationService>();

    public IAudioService Audio => Unsupported<IAudioService>();

    public IVideoService Video => Unsupported<IVideoService>();

    public ICameraViewService CameraView => Unsupported<ICameraViewService>();

    public IRandomService Random => Unsupported<IRandomService>();

    public IPersistenceService Persistence => Unsupported<IPersistenceService>();

    public IContentStoreService ContentStore => Unsupported<IContentStoreService>();

    private static T Unsupported<T>() =>
        throw new NotSupportedException($"{typeof(T).Name} is not used by this product yet, so the test context does not provide it.");
}

/// <summary>An engine UI service that records what the product publishes.</summary>
internal sealed class RecordingUiService : IUiService
{
    private readonly List<UiProjection> _projections = [];

    internal IReadOnlyList<UiProjection> Projections => _projections;

    internal UiStreamRequest? LastRequest { get; private set; }

    internal UiProjection Latest() =>
        _projections.Count > 0 ? _projections[^1] : throw new InvalidOperationException("Nothing was published.");

    public UiStream OpenStream(UiStreamRequest request)
    {
        LastRequest = request;
        return new UiStream(new UiStreamHandle(1), () => { });
    }

    public void PublishProjection(UiProjection projection) => _projections.Add(projection);
}
