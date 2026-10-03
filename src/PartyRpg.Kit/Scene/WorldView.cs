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
/// <b>Nothing here decides anything.</b> The place is the world's, the eye is the party's pose at the movement space's
/// <see cref="PlaceSpace.EyeHeight"/> along the heading the reticle aims by, the doors are the interaction ledger's as the game's rule reads them, and the light is
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
/// looks along the heading that space gives the party's facing, which the Engine's derived camera basis reads directly.
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
    private PlaceId? _shown;
    private bool _disposed;
    private readonly Dictionary<string, Sheet?> _sheets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Drawn> _drawn = new(StringComparer.Ordinal);
    private readonly List<Appearance> _retiredSprites = [];
    private readonly List<Sheet> _setAside = [];

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
    /// <param name="seconds">The session's admitted time, which the world's animations are read against.</param>
    public void Present(PartyPoseOwner party, double seconds)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(party);
        bool changed = false;
        if (_shown != party.Place)
        {
            _shown = party.Place;
            changed = Enter(party.Place);
        }

        if (_loaded is { } loaded)
        {
            changed |= Doors(loaded);
            Light(loaded);
        }

        List<ulong> moved = [];
        List<ulong> removed = [];
        Objects(party, seconds, moved, removed);
        if (_loaded is not null) Bursts(party.Place, seconds);

        Aim(party.PlacePose);
        // A place or a door that changed republishes the whole scene; otherwise only the objects that moved, appeared
        // or left are sent, so a walking town costs what walks rather than the whole place.
        if (changed) Publish();
        else if (moved.Count > 0 || removed.Count > 0) PublishObjects(moved, removed);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_shown is null) return;
        try { Graphics.PublishSnapshot(ReadOnlySpan<AppearanceFact>.Empty); } catch (EngineCallException) { }
        try { Cameras.ClearSkyBackground(new ClearSkyBackgroundRequest(0)); } catch (EngineCallException) { }
        try { Cameras.ClearActiveCamera(new ClearActiveCameraRequest(0)); } catch (EngineCallException) { }
        _retired?.Dispose();
        _loaded?.Dispose();
        foreach (Drawn drawn in _drawn.Values) drawn.Appearance.Dispose();
        foreach (Appearance retired in _retiredSprites) retired.Dispose();
        foreach (Sheet? sheet in _sheets.Values) sheet?.Dispose();
        foreach (Sheet sheet in _setAside) sheet.Dispose();
        _drawn.Clear();
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

        // A place whose content cannot be drawn — its mesh absent or malformed, a group naming a material the scene
        // does not list, a part the Engine refuses — is said once and drawn as nothing, as a place with no scene is:
        // content that is wrong must not fault the update the party walked into it in.
        Loaded? loaded = null;
        try
        {
            RenderMesh mesh = ReadMesh(scene.MeshPath);
            foreach (RenderMeshGroup group in mesh.Parts.SelectMany(part => part.Groups))
            {
                if (group.Material < 0 || group.Material >= scene.Materials.Count)
                    throw new InvalidDataException($"'{scene.MeshPath}' draws material {group.Material}, and the scene lists {scene.Materials.Count}.");
            }
            loaded = new Loaded(scene, mesh);
            foreach (SceneMaterial material in scene.Materials) loaded.Materials.Add(Material(material));
            foreach (RenderMeshPart part in loaded.Mesh.Parts)
            {
                bool closed = part.Door is { } door && scene.Doors.TryGetValue(door, out string? id) && _rule.IsClosed(place, id);
                loaded.Parts.Add(Part(loaded, part, closed));
            }
        }
        catch (Exception refused) when (refused is EngineCallException or InvalidDataException)
        {
            loaded?.Dispose();
            Note($"Place '{place}' cannot be drawn, so nothing is drawn there: {refused.Message}");
            Report = null;
            return true;
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
            roughness: 1f,
            textureTint: white,
            emissionColor: material.Emissive ? Vector3.One : Vector3.Zero,
            emissionIntensity: material.Emissive ? 1f : 0f,
            doubleSided: true,
            alphaMode: material.Alpha == SceneAlpha.Cutout ? MaterialAlphaMode.Mask : MaterialAlphaMode.Opaque,
            alphaCutoff: 0.5f));
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

    private RenderResource? Texture(string path, TextureWrap wrap, TextureFilter filter = TextureFilter.Linear)
    {
        string key = $"{path}|{wrap}|{filter}";
        if (_textures.TryGetValue(key, out RenderResource? cached)) return cached;
        RenderResource? texture = null;
        try
        {
            using ContentReference reference = ContentService.OpenReference(new ContentOpenRequest(path));
            texture = Graphics.OpenResourceFromContent(new RenderResourceContentRequest(reference, filter, wrap)).Handle;
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

        foreach (Drawn drawn in _drawn.Values)
        {
            facts.Add(new AppearanceFact(drawn.ObjectId, false, 0, new Transform(drawn.Position, Quaternion.Identity, Vector3.One),
                drawn.Appearance, Visible: true, RenderLayer.Scene));
        }

        Graphics.PublishSnapshot(CollectionsMarshal.AsSpan(facts));
        foreach (Appearance retired in _retiredSprites) retired.Dispose();
        _retiredSprites.Clear();
        _retired?.Dispose();
        _retired = null;
        if (_loaded is { } current)
        {
            foreach (DrawnPart retired in current.Retired) retired.Dispose();
            current.Retired.Clear();
        }
    }

    /// <summary>
    /// Emits the bursts the rule reports as Engine particle bursts at their points; a burst the Engine refuses is noted
    /// once and the update goes on, since a mark that cannot be shown changes nothing it marks.
    /// </summary>
    private void Bursts(PlaceId place, double seconds)
    {
        foreach (SceneBurst burst in _rule.Bursts(place, seconds))
        {
            Vector3 at = _space.GroundPosition(burst.At);
            Color start = new(burst.Colour.X, burst.Colour.Y, burst.Colour.Z, 1f);
            Color end = new(burst.Colour.X, burst.Colour.Y, burst.Colour.Z, 0f);
            uint count = (uint)Math.Clamp(burst.Count, 1, 256);
            try
            {
                PresentationParticleEmissionReceipt receipt = _engine.Presentation.EmitParticles(new PresentationParticleDescriptor
                {
                    SignalId = burst.Label,
                    Visible = true,
                    Anchor = new PresentationAnchor(PresentationAnchorKind.World, at, 0, Vector3.Zero),
                    Visual = PresentationParticleVisual.Cube,
                    BurstCount = count,
                    MaxParticles = count,
                    LifetimeMinSeconds = burst.Seconds * 0.6f,
                    LifetimeMaxSeconds = burst.Seconds,
                    SizeCurve = new PresentationParticleScalarKey[] { new(0, burst.Size), new(1, burst.Size * 0.3f) },

                    // Particles fly out every way at up to the burst's speed and fall back at twice it a second, so a
                    // burst sprays from its point rather than sitting on it.
                    VelocityMin = new Vector3(-burst.Speed),
                    VelocityMax = new Vector3(burst.Speed),
                    Acceleration = new Vector3(0, -2 * burst.Speed, 0),
                    ColorCurve = new PresentationParticleColorKey[] { new(0, start), new(1, end) },
                    Seed = (ulong)(Math.Abs(seconds * 1000) % 9_000_000_000_000_000),
                });
                if (receipt.Outcome == PresentationParticleEmissionOutcome.Dropped)
                    Note($"A '{burst.Label}' burst was dropped by the Engine's particle budget.");
            }
            catch (EngineCallException refused)
            {
                Note($"A '{burst.Label}' burst could not be shown: {refused.Message}");
            }
        }
    }

    /// <summary>Sends only the objects that moved, appeared or left, then releases the sprites that left.</summary>
    private void PublishObjects(List<ulong> moved, List<ulong> removed)
    {
        HashSet<ulong> wanted = [.. moved];
        AppearanceFact[] upserts = [.. _drawn.Values.Where(drawn => wanted.Contains(drawn.ObjectId)).Select(drawn =>
            new AppearanceFact(drawn.ObjectId, false, 0, new Transform(drawn.Position, Quaternion.Identity, Vector3.One),
                drawn.Appearance, Visible: true, RenderLayer.Scene))];
        Graphics.PublishChanges(new AppearanceChangesRequest(upserts, removed.ToArray(), ReadOnlyMemory<MeshJointAttachment>.Empty));
        foreach (Appearance retired in _retiredSprites) retired.Dispose();
        _retiredSprites.Clear();
    }

    /// <summary>Lights the place as the game says it is lit now, changing the Engine's lights only when the answer moved.</summary>
    /// <remarks>
    /// The carried light has no inverse-power decay: a place's units are a few hundred to a body, so a physical falloff
    /// would leave it dark one step away. Its range's smooth cutoff is its whole falloff.
    /// </remarks>
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
        // Distance fades linearly into the background colour, which the Engine never fogs, so far geometry meets it.
        Cameras.SetFog(lighting.Fog is { } fog
            ? new FogRequest(FogMode.Linear, new Color(lighting.Background.X, lighting.Background.Y, lighting.Background.Z, 1f), fog.Start, fog.End, 0f)
            : new FogRequest(FogMode.Off, default, 0f, 0f, 0f));
        _ambient = Replace(_ambient, 1, new LightDescriptor(LightKind.Ambient, lighting.Ambient, lighting.AmbientIntensity, true,
            Vector3.Zero, -Vector3.UnitY, false, 0f, 0f, 0f, 0f, LightShadowIntent.Disabled));
        _sun = Replace(_sun, 2, new LightDescriptor(LightKind.Directional, lighting.Sun, lighting.SunIntensity, lighting.SunDirection is not null,
            Vector3.Zero, lighting.SunDirection ?? -Vector3.UnitY, false, 0f, 0f, 0f, 0f, LightShadowIntent.Disabled));
        (Vector3 colour, float intensity, float range) = lighting.Carried ?? (Vector3.One, 0f, 1f);
        _carried = Replace(_carried, 3, new LightDescriptor(LightKind.Point, colour, intensity, lighting.Carried is not null,
            Vector3.Zero, -Vector3.UnitY, true, range, 0f, 0f, 0f, LightShadowIntent.Disabled));
    }

    private Light Replace(Light? light, ulong id, LightDescriptor descriptor)
    {
        LightRequest request = new(id, false, 0, descriptor);
        if (light is null) return Graphics.CreateLight(request);
        Graphics.UpdateLight(new LightUpdateRequest(light, request));
        return light;
    }

    /// <summary>Puts the camera at the party's eye, looking along its facing, and carries its light with it.</summary>
    private void Aim(PlacePose pose)
    {
        Vector3 eye = _space.EyePosition(pose);
        double heading = _space.FacingRadians(pose.Yaw);
        double pitch = _space.PitchRadians(pose.Pitch);

        // The Engine derives the basis from the pose: yaw zero looks along -z and grows toward +x, which is the heading
        // the movement's space gives the party's facing, and a positive pitch looks up.
        CameraDescriptor descriptor = new(
            new CameraPose(eye, pitch * 180d / Math.PI, heading * 180d / Math.PI),
            CameraBasisMode.Derived,
            default,
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
                LightKind.Point, carried.Colour, carried.Intensity, true, eye, -Vector3.UnitY, true, carried.Range, 0f, 0f, 0f,
                LightShadowIntent.Disabled))));
        }
    }

    /// <summary>Keeps a note once and reports it where the session's other composition notes go.</summary>
    private void Note(string note)
    {
        if (_notes.Contains(note, StringComparer.Ordinal)) return;
        _notes.Add(note);
        try
        {
            _engine.Diagnostics?.Publish(new DiagnosticsPublishRequest(
                DiagnosticsSeverity.Warning, DiagnosticsDisposition.Degraded, Source: "scene", Code: "scene-note", Message: note, Correlation: string.Empty));
        }
        catch (EngineCallException)
        {
            // The note is kept in Notes; a diagnostics sink that refuses it must not stop the update it describes.
        }
    }

    /// <summary>
    /// Draws what stands in the place: one sprite per object, showing its group's frame for its time and the view its
    /// facing turns to the eye, standing at its feet. Returns whether the published scene must change.
    /// </summary>
    /// <remarks>
    /// A frame or a view that changes is set on the object's own sprite; an object that moved, appeared, left or changed
    /// its group is collected for the next publication. An object whose group content does not carry is left out and noted once.
    /// </remarks>
    private void Objects(PartyPoseOwner party, double seconds, List<ulong> moved, List<ulong> removed)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        PlacePose eye = party.PlacePose;
        foreach (SceneObject entity in _loaded is null ? [] : _rule.Objects(party.Place, seconds))
        {
            if (SheetOf(entity.Sprite) is not { } sheet || !seen.Add(entity.Id)) continue;
            int frame = sheet.Sprite.FrameAt(entity.Seconds, entity.Loop);
            int view = sheet.Sprite.Views == 1 ? 0 : View(entity.Feet, eye);
            uint cell = (uint)((frame * sheet.Sprite.Views) + view);
            Vector3 position = _space.GroundPosition(entity.Feet);
            if (_drawn.TryGetValue(entity.Id, out Drawn? drawn) && drawn.Sprite == entity.Sprite)
            {
                if (drawn.Cell != cell)
                {
                    try
                    {
                        Graphics.SetSpriteFrame(new SpriteFrameUpdateRequest(drawn.Appearance, cell));
                        drawn.Cell = cell;
                    }
                    catch (EngineCallException refused)
                    {
                        Note($"Sprite '{entity.Sprite}' refused frame {cell}, so it keeps the frame it shows: {refused.Message}");
                    }
                }

                if (drawn.Position != position)
                {
                    drawn.Position = position;
                    moved.Add(drawn.ObjectId);
                }

                continue;
            }

            Appearance appearance;
            try
            {
                appearance = Graphics.CreateSpriteFromAtlas(Request(sheet, cell));
            }
            catch (EngineCallException refused)
            {
                // A group the Engine will not draw is set aside for the session, said once, and its objects not drawn.
                // The atlas is kept until the view is released: sprites already cut from it may still be drawn.
                Note($"Sprite '{entity.Sprite}' could not be drawn, so what shows it is not drawn: {refused.Message}");
                _setAside.Add(sheet);
                _sheets[entity.Sprite] = null;
                continue;
            }

            if (drawn is not null) _retiredSprites.Add(drawn.Appearance);
            _drawn[entity.Id] = new Drawn(entity.Sprite, ObjectIdOf(entity.Id), appearance) { Cell = cell, Position = position };
            moved.Add(_drawn[entity.Id].ObjectId);
        }

        foreach (string gone in _drawn.Keys.Where(id => !seen.Contains(id)).ToList())
        {
            _retiredSprites.Add(_drawn[gone].Appearance);
            removed.Add(_drawn[gone].ObjectId);
            _drawn.Remove(gone);
        }
    }

    /// <summary>
    /// Which of eight views an object shows the eye: its facing, less the bearing from the eye to it, in eighths of a
    /// turn offset by half a turn and a sixteenth, so view 0 faces the eye and the views run around the object's own
    /// turn; stated in radians in the place's own frame.
    /// </summary>
    private int View(PlacePose feet, PlacePose eye)
    {
        double facing = feet.Yaw * _space.RadiansPerFacingUnit;
        double bearing = Math.Atan2(feet.Y - eye.Y, feet.X - eye.X);
        double turn = Math.PI + (Math.PI / 8) + facing - bearing;
        int view = (int)Math.Floor(turn / (Math.PI / 4)) % 8;
        return view < 0 ? view + 8 : view;
    }

    private static SpriteFromAtlasRequest Request(Sheet sheet, uint cell) => new(
        sheet.Atlas,
        cell,
        new Vector2(0.5f, sheet.Sprite.Centred ? 0.5f : 0f),
        new Vector2((float)(sheet.Sprite.CellWidth * sheet.Sprite.Scale), (float)(sheet.Sprite.CellHeight * sheet.Sprite.Scale)),
        BillboardMode.Cylindrical,
        SpriteSizeMode.World,
        0,
        SpriteDepthPolicy.Default,
        new Color(1f, 1f, 1f, 1f),
        new SpriteMaterialDescriptor(sheet.Sprite.SelfLit ? SpriteLightingMode.Unlit : SpriteLightingMode.Synthetic,
            default, default, 1f, 0f, SpriteAlphaMode.Mask, 0.5f, SpriteShadowPolicy.None));

    /// <summary>A sprite group's atlas, opened once for the session; null when content or the Engine refuses it.</summary>
    private Sheet? SheetOf(string id)
    {
        if (_sheets.TryGetValue(id, out Sheet? cached)) return cached;
        Sheet? sheet = null;
        if (_scenes.Sprite(id) is not { } sprite)
        {
            Note($"Sprite '{id}' is not in the loaded content, so what shows it is not drawn.");
        }
        else if (sprite.Columns <= 0 || sprite.CellWidth <= 0 || sprite.CellHeight <= 0 || sprite.Width <= 0 || sprite.Height <= 0
            || sprite.Scale <= 0 || sprite.Seconds.Count == 0 || sprite.Views is not (1 or 8))
        {
            Note($"Sprite '{id}' states no drawable cells, so what shows it is not drawn.");
        }
        // A sprite is sampled texel by texel, as the original's are drawn, so a cell never blends with its neighbour's.
        else if (Texture(sprite.Texture, TextureWrap.Clamp, TextureFilter.Nearest) is { } texture)
        {
            int cells = sprite.Seconds.Count * sprite.Views;
            SpriteAtlasFrame[] frames = new SpriteAtlasFrame[cells];
            for (int cell = 0; cell < cells; cell++)
            {
                float left = (float)(cell % sprite.Columns) * sprite.CellWidth / sprite.Width;
                float top = (float)(cell / sprite.Columns) * sprite.CellHeight / sprite.Height;
                frames[cell] = new SpriteAtlasFrame((uint)cell, new Vector2(left, top),
                    new Vector2(left + ((float)sprite.CellWidth / sprite.Width), top + ((float)sprite.CellHeight / sprite.Height)), false, default);
            }

            try
            {
                sheet = new Sheet(sprite, Graphics.CreateSpriteAtlas(new SpriteAtlasCreateRequest(texture, frames)));
            }
            catch (EngineCallException refused)
            {
                Note($"Sprite '{id}' could not be cut from its atlas, so what shows it is not drawn: {refused.Message}");
            }
        }

        _sheets[id] = sheet;
        return sheet;
    }

    /// <summary>The Engine object id an object's canonical identity is drawn under: a stable hash in a range of its own.</summary>
    private static ulong ObjectIdOf(string id)
    {
        ulong hash = 14695981039346656037UL;
        foreach (char c in id) hash = (hash ^ c) * 1099511628211UL;
        return (1UL << 44) | (hash & ((1UL << 44) - 1));
    }

    /// <summary>A sprite group's atlas in the Engine.</summary>
    private sealed record Sheet(SceneSprite Sprite, SpriteAtlas Atlas) : IDisposable
    {
        public void Dispose() => Atlas.Dispose();
    }

    /// <summary>One drawn object: its group, its Engine object id, its sprite, the cell it shows, where it stands.</summary>
    private sealed record Drawn(string Sprite, ulong ObjectId, Appearance Appearance)
    {
        public uint Cell { get; set; }

        public Vector3 Position { get; set; }
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
