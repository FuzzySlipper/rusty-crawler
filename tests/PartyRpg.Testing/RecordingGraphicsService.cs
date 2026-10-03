using Rusty.Engine;

namespace PartyRpg.Testing;

/// <summary>
/// A recording graphics service: every member is recorded by the <see cref="RecordingEngineService{T}"/> it forwards to,
/// except <see cref="PublishSnapshot"/>, whose span a proxy cannot carry, which keeps each published snapshot whole.
/// Its members are the pinned SDK's <c>IGraphicsService</c>, one forward each.
/// </summary>
public sealed class RecordingGraphicsService : IGraphicsService
{
    private readonly IGraphicsService _inner;
    private readonly List<AppearanceFact[]> _snapshots = [];

    /// <summary>Creates the service over a recording proxy.</summary>
    public RecordingGraphicsService(Func<System.Reflection.MethodInfo, object?[], (bool, object?)>? handler = null)
    {
        (_inner, Calls) = RecordingEngineService<IGraphicsService>.Create(handler);
    }

    /// <summary>Every forwarded call.</summary>
    public RecordingEngineService<IGraphicsService> Calls { get; }

    /// <summary>Every published snapshot, in order.</summary>
    public IReadOnlyList<AppearanceFact[]> Snapshots => _snapshots;

    public Rusty.Engine.SpritePlaybackAdvanceResult AdvanceSpritePlayback(Rusty.Engine.SpritePlaybackAdvanceRequest arg0) => _inner.AdvanceSpritePlayback(arg0);
    public Rusty.Engine.SpritePlaybackReadout ControlSpritePlayback(Rusty.Engine.SpritePlaybackControlRequest arg0) => _inner.ControlSpritePlayback(arg0);
    public Rusty.Engine.Material CreateAuthoredMaterial(Rusty.Engine.AuthoredMaterialAppearanceRequest arg0) => _inner.CreateAuthoredMaterial(arg0);
    public Rusty.Engine.Light CreateLight(Rusty.Engine.LightRequest arg0) => _inner.CreateLight(arg0);
    public Rusty.Engine.Material CreateMaterial(Rusty.Engine.MaterialRequest arg0) => _inner.CreateMaterial(arg0);
    public Rusty.Engine.Appearance CreateMeshAppearance(Rusty.Engine.MeshResource arg0) => _inner.CreateMeshAppearance(arg0);
    public Rusty.Engine.MeshResource CreateMeshResource(Rusty.Engine.MeshResourceCreateRequest arg0) => _inner.CreateMeshResource(arg0);
    public Rusty.Engine.Appearance CreatePrimitive(Rusty.Engine.PrimitiveAppearanceRequest arg0) => _inner.CreatePrimitive(arg0);
    public Rusty.Engine.Appearance CreateSprite(Rusty.Engine.SpriteAppearanceRequest arg0) => _inner.CreateSprite(arg0);
    public Rusty.Engine.SpriteAtlas CreateSpriteAtlas(Rusty.Engine.SpriteAtlasCreateRequest arg0) => _inner.CreateSpriteAtlas(arg0);
    public Rusty.Engine.Appearance CreateSpriteFromAtlas(Rusty.Engine.SpriteFromAtlasRequest arg0) => _inner.CreateSpriteFromAtlas(arg0);
    public Rusty.Engine.SpritePlayback CreateSpritePlayback(Rusty.Engine.SpritePlaybackCreateRequest arg0) => _inner.CreateSpritePlayback(arg0);
    public Rusty.Engine.Appearance CreateStaticMesh(Rusty.Engine.StaticMeshAppearanceRequest arg0) => _inner.CreateStaticMesh(arg0);
    public Rusty.Engine.Appearance CreateStaticMeshFromContent(Rusty.Engine.StaticMeshContentAppearanceRequest arg0) => _inner.CreateStaticMeshFromContent(arg0);
    public Rusty.Engine.Appearance CreateStaticMeshFromContentReference(Rusty.Engine.StaticMeshContentReferenceRequest arg0) => _inner.CreateStaticMeshFromContentReference(arg0);
    public Rusty.Engine.RenderResourceInfo OpenResource(Rusty.Engine.RenderResourceRequest arg0) => _inner.OpenResource(arg0);
    public Rusty.Engine.RenderResourceInfo OpenResourceFromContent(Rusty.Engine.RenderResourceContentRequest arg0) => _inner.OpenResourceFromContent(arg0);
    public Rusty.Engine.MeshPartition PartitionMesh(Rusty.Engine.MeshPartitionRequest arg0) => _inner.PartitionMesh(arg0);
    public void PublishChanges(Rusty.Engine.AppearanceChangesRequest arg0) => _inner.PublishChanges(arg0);
    public void PublishSnapshot(System.ReadOnlySpan<Rusty.Engine.AppearanceFact> values) => _snapshots.Add(values.ToArray());
    public Rusty.Engine.LightReadout ReadLight(Rusty.Engine.Light arg0) => _inner.ReadLight(arg0);
    public Rusty.Engine.MeshPartitionReadout ReadMeshPartition(Rusty.Engine.MeshPartition arg0) => _inner.ReadMeshPartition(arg0);
    public Rusty.Engine.PresentationReadout ReadPresentation() => _inner.ReadPresentation();
    public Rusty.Engine.SpriteReadout ReadSprite(Rusty.Engine.Appearance arg0) => _inner.ReadSprite(arg0);
    public Rusty.Engine.SpritePlaybackReadout ReadSpritePlayback(Rusty.Engine.SpritePlayback arg0) => _inner.ReadSpritePlayback(arg0);
    public Rusty.Engine.TextureResourceInfo ReadTextureInfo(Rusty.Engine.RenderResource arg0) => _inner.ReadTextureInfo(arg0);
    public Rusty.Engine.Light ReplaceLight(Rusty.Engine.LightUpdateRequest arg0) => _inner.ReplaceLight(arg0);
    public Rusty.Engine.Material ReplaceMaterial(Rusty.Engine.MaterialUpdateRequest arg0) => _inner.ReplaceMaterial(arg0);
    public Rusty.Engine.Appearance ReplacePrimitive(Rusty.Engine.PrimitiveAppearanceReplaceRequest arg0) => _inner.ReplacePrimitive(arg0);
    public Rusty.Engine.Appearance ReplaceSprite(Rusty.Engine.SpriteAppearanceReplaceRequest arg0) => _inner.ReplaceSprite(arg0);
    public Rusty.Engine.Appearance ReplaceSpriteFromAtlas(Rusty.Engine.SpriteFromAtlasReplaceRequest arg0) => _inner.ReplaceSpriteFromAtlas(arg0);
    public Rusty.Engine.Appearance ReplaceStaticMesh(Rusty.Engine.Appearance arg0, Rusty.Engine.StaticMeshAppearanceRequest arg1) => _inner.ReplaceStaticMesh(arg0, arg1);
    public Rusty.Engine.Appearance ReplaceStaticMeshFromContent(Rusty.Engine.Appearance arg0, Rusty.Engine.StaticMeshContentAppearanceRequest arg1) => _inner.ReplaceStaticMeshFromContent(arg0, arg1);
    public Rusty.Engine.SpritePlaybackSample SampleSpritePlayback(Rusty.Engine.SpritePlaybackSampleRequest arg0) => _inner.SampleSpritePlayback(arg0);
    public Rusty.Engine.SpritePlaybackReadout SelectSpritePlaybackFrame(Rusty.Engine.SpritePlaybackFrameSelectionRequest arg0) => _inner.SelectSpritePlaybackFrame(arg0);
    public void SetSpriteFrame(Rusty.Engine.SpriteFrameUpdateRequest arg0) => _inner.SetSpriteFrame(arg0);
    public void SetSpriteViewport(Rusty.Engine.SpriteViewportUpdateRequest arg0) => _inner.SetSpriteViewport(arg0);
    public Rusty.Engine.MeshResource TakeMeshPartitionPart(Rusty.Engine.MeshPartitionPartRequest arg0) => _inner.TakeMeshPartitionPart(arg0);
    public void UpdateLight(Rusty.Engine.LightUpdateRequest arg0) => _inner.UpdateLight(arg0);
    public void UpdateMaterial(Rusty.Engine.MaterialUpdateRequest arg0) => _inner.UpdateMaterial(arg0);
    public void UpdateStaticMeshMaterials(Rusty.Engine.StaticMeshMaterialUpdateRequest arg0) => _inner.UpdateStaticMeshMaterials(arg0);
}
