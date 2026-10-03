using System.Numerics;
using System.Runtime.InteropServices;
using PartyRpg.Kit.Movement;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.World;
using Rusty.Engine;

namespace PartyRpg.Kit.Scene;

/// <summary>What the view drew for the place the party is in, for a report or an observation.</summary>
/// <param name="Place">The place drawn.</param>
/// <param name="Triangles">How many triangles its mesh holds.</param>
/// <param name="Materials">How many materials it binds.</param>
/// <param name="Untextured">How many of them name no image, and are drawn in a flat colour.</param>
/// <param name="Doors">How many door parts it draws.</param>
/// <param name="ClosedDoors">How many of them stand closed now.</param>
/// <param name="Sky">Whether a sky is drawn behind it.</param>
public sealed record WorldViewReport(PlaceId Place, int Triangles, int Materials, int Untextured, int Doors, int ClosedDoors, bool Sky);

/// <summary>
/// Draws the live world through the Engine: the party's place as content describes it, seen from the party's own eye,
/// with each door where the place's interaction state puts it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing here decides anything.</b> The place is the world's, the eye is the party's pose through
/// <see cref="PartyView.Derive"/>, the doors are the interaction ledger's as the game's rule reads them, and the light is
/// the game's answer for the place at the current hour. The view keeps the Engine resources those answers need and
/// publishes them once per admitted update; it holds no clock, no camera state of its own and no copy of the world.
/// </para>
/// <para>
/// <b>Lifetimes.</b> Textures are opened once per session and shared by every place that binds them. A place's
/// materials, meshes and appearances are created when the party enters it and released after the next place is
/// published, so the renderer never loses what it was drawing mid-swap; a door's mesh is replaced when its state
/// changes. Everything is released when the view is disposed.
/// </para>
/// <para>
/// <b>Axes.</b> The scene's content is already in the Engine's axes; the eye is converted through the same
/// <see cref="PlaceSpace"/> movement walks in, so the camera stands where the collision says the party stands, and it
/// looks along the heading that space gives the party's facing.
/// </para>
/// </remarks>
public sealed class WorldView : IWorldPresenter
{
    /// <summary>The object id of a place's static geometry in the published scene.</summary>
    public const ulong PlaceObjectId = 1;

    /// <summary>The first object id a door part takes; a door is this plus its part's position.</summary>
    public const ulong FirstDoorObjectId = 1UL << 20;

    private static readonly CameraViewport FullViewport = new(0d, 0d, 1d, 1d);

    private readonly IEngineContext _engine;
    private readonly IPlaceSceneSource _scenes;
    private readonly ISceneRule _rule;
    private readonly PlaceSpace _space;
    private readonly Dictionary<string, RenderResource?> _textures = new(StringComparer.Ordinal);
    private readonly List<string> _notes = [];
    private Camera? _camera;
    private Light? _ambient;
    private Light? _sun;
    private Light? _carried;
    private SceneLighting? _lit;
    private Loaded? _loaded;
    private Loaded? _retired;
    private bool _disposed;

    /// <summary>Creates the view. Nothing is drawn until the first <see cref="Present"/>.</summary>
    /// <param name="engine">
    /// The Engine, whose graphics, camera and content services the view draws through; it reaches for them when it first
    /// draws, so a session that never draws asks nothing of them.
    /// </param>
    /// <param name="scenes">Where each place's scene comes from.</param>
    /// <param name="rule">The game's answers about the eye, the light and the doors.</param>
    /// <param name="space">How a place's coordinates and facing become the Engine's axes and headings.</param>
    public WorldView(IEngineContext engine, IPlaceSceneSource scenes, ISceneRule rule, PlaceSpace space)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _scenes = scenes ?? throw new ArgumentNullException(nameof(scenes));
        _rule = rule ?? throw new ArgumentNullException(nameof(rule));
        _space = space;
    }

    /// <summary>What the view drew for the current place, or null before it drew one.</summary>
    public WorldViewReport? Report { get; private set; }

    private IGraphicsService Graphics => _engine.Graphics;

    private ICameraViewService Cameras => _engine.CameraView;

    private IContentService ContentService => _engine.Content;

    /// <summary>
    /// What the view could not draw as content asked — an image the Engine refused, a place with no scene — each said
    /// once, for the session's diagnostics to carry.
    /// </summary>
    public IReadOnlyList<string> Notes => _notes;

    /// <summary>Draws the party's place from the party's eye, as the world stands after this update.</summary>
    /// <param name="party">The party's pose.</param>
    public void Present(PartyPoseOwner party)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(party);
        bool changed = false;
        if (_loaded?.Scene.Place != party.Place)
        {
            changed = Enter(party.Place);
        }

        if (_loaded is { } loaded)
        {
            changed |= Doors(loaded);
            Light(loaded);
        }

        Aim(party.DeriveView(_rule.Eye));
        if (changed) Publish();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_camera is null && _loaded is null && _retired is null) return;
        try { Graphics.PublishSnapshot(ReadOnlySpan<AppearanceFact>.Empty); } catch (EngineCallException) { }
        try { Cameras.ClearSkyBackground(new ClearSkyBackgroundRequest(0)); } catch (EngineCallException) { }
        try { Cameras.ClearActiveCamera(new ClearActiveCameraRequest(0)); } catch (EngineCallException) { }
        _retired?.Dispose();
        _loaded?.Dispose();
        _camera?.Dispose();
        _ambient?.Dispose();
        _sun?.Dispose();
        _carried?.Dispose();
        foreach (RenderResource? texture in _textures.Values) texture?.Dispose();
        _textures.Clear();
    }

    /// <summary>Loads a place's scene, keeping the previous one until the new one is published.</summary>
    private bool Enter(PlaceId place)
    {
        _retired?.Dispose();
        _retired = _loaded;
        _loaded = null;
        PlaceScene? scene = _scenes.For(place);
        if (scene is null)
        {
            Note($"Place '{place}' has no scene in the loaded content, so nothing is drawn there.");
            Report = null;
            return true;
        }

        Loaded loaded = new(scene, ReadMesh(scene.MeshPath));
        try
        {
            foreach (SceneMaterial material in scene.Materials) loaded.Materials.Add(Material(material));
            foreach (RenderMeshPart part in loaded.Mesh.Parts)
            {
                bool closed = part.Door is { } door && scene.Doors.TryGetValue(door, out string? id) && _rule.IsClosed(place, id);
                loaded.Parts.Add(Part(loaded, part, closed));
            }
        }
        catch
        {
            loaded.Dispose();
            throw;
        }

        _loaded = loaded;
        loaded.Panorama = scene.Sky is { } sky ? Texture(sky, TextureWrap.Clamp) : null;
        loaded.Sky = loaded.Panorama is not null;
        _lit = null;
        Reported(loaded);
        return true;
    }

    /// <summary>Replaces every door part whose state changed since it was drawn.</summary>
    private bool Doors(Loaded loaded)
    {
        bool changed = false;
        for (int index = 0; index < loaded.Parts.Count; index++)
        {
            DrawnPart drawn = loaded.Parts[index];
            if (drawn.Part.Door is not { } door || !loaded.Scene.Doors.TryGetValue(door, out string? id)) continue;
            bool closed = _rule.IsClosed(loaded.Scene.Place, id);
            if (closed == drawn.Closed) continue;
            DrawnPart replaced = Part(loaded, drawn.Part, closed);
            loaded.Parts[index] = replaced;
            loaded.Retired.Add(drawn);
            changed = true;
        }

        if (changed) Reported(loaded);
        return changed;
    }

    private void Reported(Loaded loaded) => Report = new WorldViewReport(
        loaded.Scene.Place,
        loaded.Mesh.Triangles,
        loaded.Scene.Materials.Count,
        loaded.Scene.Materials.Count(material => material.Texture is null),
        loaded.Parts.Count(part => part.Part.Door is not null),
        loaded.Parts.Count(part => part.Part.Door is not null && part.Closed),
        loaded.Sky);

    private Material Material(SceneMaterial material)
    {
        RenderResource? texture = material.Texture is { } path ? Texture(path, TextureWrap.Repeat) : null;
        Color white = new(1f, 1f, 1f, 1f);

        // A material whose image could not be drawn is a flat mid grey rather than a guessed picture: what is missing
        // is visible, and the view's notes say which image it was.
        Color colour = texture is null ? new Color(0.5f, 0.5f, 0.5f, 1f) : white;
        return Graphics.CreateMaterial(new MaterialRequest(
            colour,
            texture is null ? default : new RenderResourceReference(texture),
            Roughness: 1f,
            TextureTint: white,
            EmissionColor: material.Emissive ? Vector3.One : Vector3.Zero,
            EmissionIntensity: material.Emissive ? 1f : 0f,
            DoubleSided: true,
            material.Alpha == SceneAlpha.Cutout ? MaterialAlphaMode.Mask : MaterialAlphaMode.Opaque,
            AlphaCutoff: 0.5f));
    }

    /// <summary>Reads a place's mesh document whole through the Engine's content service.</summary>
    private RenderMesh ReadMesh(string path)
    {
        using ContentReference reference = ContentService.OpenReference(new ContentOpenRequest(path));
        ulong length = ContentService.ReadReferenceInfo(reference).Span[0].ByteLength;
        byte[] bytes = new byte[checked((int)length)];
        int read = 0;
        while (read < bytes.Length)
        {
            ReadOnlyMemory<byte> chunk = ContentService.ReadBytes(new ContentReadBytesRequest(reference, (ulong)read, (uint)Math.Min(bytes.Length - read, 1 << 24)));
            if (chunk.IsEmpty) throw new InvalidDataException($"'{path}' ended after {read} of the {bytes.Length} bytes the Engine reported.");
            chunk.CopyTo(bytes.AsMemory(read));
            read += chunk.Length;
        }

        return RenderMesh.Read(bytes, path);
    }

    private RenderResource? Texture(string path, TextureWrap wrap)
    {
        string key = $"{path}|{wrap}";
        if (_textures.TryGetValue(key, out RenderResource? cached)) return cached;
        RenderResource? texture = null;
        try
        {
            using ContentReference reference = ContentService.OpenReference(new ContentOpenRequest(path));
            texture = Graphics.OpenResourceFromContent(new RenderResourceContentRequest(reference, TextureFilter.Linear, wrap)).Handle;
        }
        catch (EngineCallException refused)
        {
            Note($"The image '{path}' could not be opened, so what binds it is drawn untextured: {refused.Message}");
        }

        _textures[key] = texture;
        return texture;
    }

    /// <summary>Builds one part's mesh, with a door part's corners where its state puts them.</summary>
    private DrawnPart Part(Loaded loaded, RenderMeshPart part, bool closed)
    {
        RenderMesh mesh = loaded.Mesh;
        ReadOnlyMemory<Vector3> positions = mesh.Positions.Slice(part.VertexStart, part.VertexCount);
        if (closed)
        {
            Vector3[] moved = positions.ToArray();
            ReadOnlySpan<Vector3> travel = mesh.Travel.Span.Slice(part.VertexStart, part.VertexCount);
            for (int index = 0; index < moved.Length; index++) moved[index] += travel[index];
            positions = moved;
        }

        MeshGroup[] groups = [.. part.Groups.Select(group => new MeshGroup((uint)group.Material, (uint)group.Start, (uint)group.Count))];
        MeshMaterialBinding[] bindings = [.. part.Groups.Select(group => group.Material).Distinct()
            .Select(material => new MeshMaterialBinding((uint)material, loaded.Materials[material]))];
        MeshResource resource = Graphics.CreateMeshResource(new MeshResourceCreateRequest(
            positions,
            mesh.Normals.Slice(part.VertexStart, part.VertexCount),
            mesh.Uvs.Slice(part.VertexStart, part.VertexCount),
            mesh.Indices.Slice(part.IndexStart, part.IndexCount),
            groups,
            bindings));
        try
        {
            return new DrawnPart(part, closed, resource, Graphics.CreateMeshAppearance(resource));
        }
        catch
        {
            resource.Dispose();
            throw;
        }
    }

    /// <summary>Publishes the current place's geometry, then releases what the previous publication drew.</summary>
    private void Publish()
    {
        List<AppearanceFact> facts = [];
        if (_loaded is { } loaded)
        {
            for (int index = 0; index < loaded.Parts.Count; index++)
            {
                DrawnPart part = loaded.Parts[index];
                ulong id = part.Part.Door is null && index == 0 ? PlaceObjectId : FirstDoorObjectId + (ulong)index;
                facts.Add(new AppearanceFact(id, false, 0, new Transform(Vector3.Zero, Quaternion.Identity, Vector3.One),
                    part.Appearance, Visible: true, RenderLayer.Scene));
            }
        }

        Graphics.PublishSnapshot(CollectionsMarshal.AsSpan(facts));
        _retired?.Dispose();
        _retired = null;
        if (_loaded is { } current)
        {
            foreach (DrawnPart retired in current.Retired) retired.Dispose();
            current.Retired.Clear();
        }
    }

    /// <summary>Lights the place as the game says it is lit now, changing the Engine's lights only when the answer moved.</summary>
    private void Light(Loaded loaded)
    {
        SceneLighting lighting = _rule.Lighting(loaded.Scene.Place, loaded.Sky);
        if (lighting == _lit) return;
        _lit = lighting;

        // The background is either the place's sky panorama or a clear colour; selecting the colour replaces the sky.
        if (loaded.Panorama is { } panorama && lighting.SkyVisible)
        {
            Cameras.SetSkyBackground(panorama);
        }
        else
        {
            Cameras.ClearSkyBackground(new ClearSkyBackgroundRequest(0));
            Cameras.SetBackgroundColor(new SetBackgroundColorRequest(new Color(lighting.Background.X, lighting.Background.Y, lighting.Background.Z, 1f)));
        }
        _ambient = Replace(_ambient, 1, new LightDescriptor(LightKind.Ambient, lighting.Ambient, lighting.AmbientIntensity, true,
            Vector3.Zero, -Vector3.UnitY, false, 0f, 0f, 0f, 0f, LightShadowIntent.Disabled));
        _sun = Replace(_sun, 2, new LightDescriptor(LightKind.Directional, lighting.Sun, lighting.SunIntensity, lighting.SunDirection is not null,
            Vector3.Zero, lighting.SunDirection ?? -Vector3.UnitY, false, 0f, 0f, 0f, 0f, LightShadowIntent.Disabled));
        (Vector3 colour, float intensity, float range) = lighting.Carried ?? (Vector3.One, 0f, 1f);
        _carried = Replace(_carried, 3, new LightDescriptor(LightKind.Point, colour, intensity, lighting.Carried is not null,
            Vector3.Zero, -Vector3.UnitY, true, range, 2f, 0f, 0f, LightShadowIntent.Disabled));
    }

    private Light Replace(Light? light, ulong id, LightDescriptor descriptor)
    {
        LightRequest request = new(id, false, 0, descriptor);
        if (light is null) return Graphics.CreateLight(request);
        Graphics.UpdateLight(new LightUpdateRequest(light, request));
        return light;
    }

    /// <summary>Puts the camera at the party's eye, looking along its facing, and carries its light with it.</summary>
    private void Aim(PartyView view)
    {
        Vector3 eye = _space.GroundPosition(new PlacePose(view.X, view.Y, view.Z, view.Yaw, view.Pitch));
        double heading = _space.FacingRadians(view.Yaw);
        double pitch = view.Pitch * _space.RadiansPerFacingUnit;
        Vector3 forward = Vector3.Normalize(new Vector3(
            (float)(Math.Sin(heading) * Math.Cos(pitch)),
            (float)Math.Sin(pitch),
            (float)(-Math.Cos(heading) * Math.Cos(pitch))));
        Vector3 right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        Vector3 up = Vector3.Cross(right, forward);
        CameraDescriptor descriptor = new(
            new CameraPose(eye, pitch * 180d / Math.PI, heading * 180d / Math.PI),
            CameraBasisMode.Explicit,
            new CameraBasis(forward, right, up),
            new CameraProjection(CameraProjectionKind.Perspective, _rule.FieldOfViewDegrees, 0d, 8d, _rule.ViewDistance),
            FullViewport);
        if (_camera is null)
        {
            _camera = Cameras.CreateCamera(descriptor);
            Cameras.SetActiveCamera(_camera);
        }
        else
        {
            Cameras.UpdateCamera(new CameraUpdateRequest(_camera, descriptor));
        }

        if (_carried is not null && _lit?.Carried is { } carried)
        {
            Graphics.UpdateLight(new LightUpdateRequest(_carried, new LightRequest(3, false, 0, new LightDescriptor(
                LightKind.Point, carried.Colour, carried.Intensity, true, eye, -Vector3.UnitY, true, carried.Range, 2f, 0f, 0f,
                LightShadowIntent.Disabled))));
        }
    }

    private void Note(string note)
    {
        if (!_notes.Contains(note, StringComparer.Ordinal)) _notes.Add(note);
    }

    /// <summary>A place's Engine resources.</summary>
    private sealed class Loaded(PlaceScene scene, RenderMesh mesh) : IDisposable
    {
        public PlaceScene Scene { get; } = scene;

        public RenderMesh Mesh { get; } = mesh;

        public List<Material> Materials { get; } = [];

        public List<DrawnPart> Parts { get; } = [];

        public List<DrawnPart> Retired { get; } = [];

        public bool Sky { get; set; }

        public RenderResource? Panorama { get; set; }

        public void Dispose()
        {
            foreach (DrawnPart part in Retired) part.Dispose();
            foreach (DrawnPart part in Parts) part.Dispose();
            foreach (Material material in Materials) material.Dispose();
            Retired.Clear();
            Parts.Clear();
            Materials.Clear();
        }
    }

    /// <summary>One part's mesh and the appearance that draws it, in one door state.</summary>
    private sealed record DrawnPart(RenderMeshPart Part, bool Closed, MeshResource Mesh, Appearance Appearance) : IDisposable
    {
        public void Dispose()
        {
            Appearance.Dispose();
            Mesh.Dispose();
        }
    }
}
