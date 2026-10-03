using System.Buffers.Binary;
using System.Numerics;
using PartyRpg.Kit.Movement;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Scene;
using PartyRpg.Kit.World;
using PartyRpg.Testing;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// The world view draws the party's place from content through the Engine: one mesh per part, a door part where the
/// game's rule puts its door, the camera at the party's eye along its facing, and nothing published that did not change.
/// </summary>
public sealed class WorldViewTests
{
    private static readonly FacingRule Facing = new(unitsPerTurn: 2048, minimumPitch: -512, maximumPitch: 512);
    private static readonly PlaceSpace Space = PlaceSpace.HeightIsThird(Facing, Math.PI / 2, eyeHeight: 160);
    private static readonly PlaceId Place = new("1");

    [Fact]
    public void The_mesh_reader_reads_parts_groups_and_door_travel_and_refuses_a_short_document()
    {
        byte[] bytes = Mesh();

        RenderMesh mesh = RenderMesh.Read(bytes, "place.mesh");

        Assert.Equal(3, mesh.Parts.Count);
        Assert.Null(mesh.Parts[0].Door);
        Assert.Equal(4, mesh.Parts[1].Door);
        Assert.Equal((null, 7, true), (mesh.Parts[2].Door, mesh.Parts[2].Switch, mesh.Parts[2].StartsHidden));
        Assert.Equal(new RenderMeshGroup(1, 0, 3), Assert.Single(mesh.Parts[1].Groups));
        Assert.Equal(new Vector3(0, 0, 50), mesh.Travel.Span[4]);
        Assert.Equal(2, mesh.Triangles);
        Assert.Throws<InvalidDataException>(() => RenderMesh.Read(bytes[..^4], "place.mesh"));
    }

    [Fact]
    public void A_place_is_drawn_once_with_its_closed_door_moved_by_its_travel_and_the_camera_at_the_eye()
        => WithView((view, rule, graphics, cameras, party) =>
        {
            rule.Closed = true;
            view.Present(party, 0);

            // Three parts, three meshes; the door part's corners stand at rest plus their travel.
            IReadOnlyList<object?[]> meshes = graphics.Calls.CallsTo(nameof(IGraphicsService.CreateMeshResource));
            Assert.Equal(3, meshes.Count);
            MeshResourceCreateRequest door = (MeshResourceCreateRequest)meshes[1][0]!;
            Assert.Equal(new Vector3(10, 0, 50), door.Positions.Span[1]);
            Assert.Single(graphics.Snapshots);
            Assert.Equal(new WorldViewReport(Place, 2, 2, 1, 1, 1, false), view.Report);

            // The party faces along its first ground axis at yaw zero, which is the Engine's +x — a derived yaw of 90 degrees
            // from -z — and the eye is the movement space's.
            CameraDescriptor camera = (CameraDescriptor)Assert.Single(cameras.CallsTo(nameof(ICameraViewService.CreateCamera)))[0]!;
            Assert.Equal(CameraBasisMode.Derived, camera.BasisMode);
            Assert.Equal(90d, camera.Pose.YawDegrees, 6);
            Assert.Equal(0d, camera.Pose.PitchDegrees, 6);
            Assert.Equal(new Vector3(100, 160, -200), camera.Pose.Position);

            // Distance fades into the background colour between the rule's two distances.
            FogRequest fog = (FogRequest)Assert.Single(cameras.CallsTo(nameof(ICameraViewService.SetFog)))[0]!;
            Assert.Equal((FogMode.Linear, 2_000f, 9_000f), (fog.Mode, fog.Start, fog.End));

            // Nothing changed, so nothing is rebuilt or republished; the camera follows the party every update.
            view.Present(party, 0);
            Assert.Equal(3, graphics.Calls.CallsTo(nameof(IGraphicsService.CreateMeshResource)).Count);
            Assert.Single(graphics.Snapshots);
            Assert.Single(cameras.CallsTo(nameof(ICameraViewService.UpdateCamera)));
        });

    [Fact]
    public void A_switch_starts_hidden_and_is_shown_and_retextured_as_the_rule_says()
        => WithView((view, rule, graphics, _, party) =>
        {
            view.Present(party, 0);
            AppearanceFact[] first = graphics.Snapshots[^1];
            Assert.Equal(3, first.Count(fact => fact.ObjectId < 1UL << 44));
            Assert.Single(first, fact => fact.ObjectId < 1UL << 44 && !fact.Visible);

            // Shown with its own materials, it is only republished visible: nothing is rebuilt.
            int built = graphics.Calls.CallsTo(nameof(IGraphicsService.CreateMeshResource)).Count;
            rule.Switches[7] = new SceneSwitch(false, null);
            view.Present(party, 0);
            Assert.Equal(built, graphics.Calls.CallsTo(nameof(IGraphicsService.CreateMeshResource)).Count);
            Assert.All(graphics.Snapshots[^1].Where(fact => fact.ObjectId < 1UL << 44), fact => Assert.True(fact.Visible));

            // The rule gives it the place's second material: it is rebuilt drawing that one, and republished shown.
            rule.Switches[7] = new SceneSwitch(false, 1);
            view.Present(party, 0);
            MeshResourceCreateRequest rebuilt = (MeshResourceCreateRequest)graphics.Calls.CallsTo(nameof(IGraphicsService.CreateMeshResource))[^1][0]!;
            Assert.Equal(1u, Assert.Single(rebuilt.Groups.ToArray()).MaterialSlot);
            Assert.All(graphics.Snapshots[^1].Where(fact => fact.ObjectId < 1UL << 44), fact => Assert.True(fact.Visible));

            // A material the scene does not list is said once and the faces keep their own.
            rule.Switches[7] = new SceneSwitch(false, 9);
            view.Present(party, 0);
            Assert.Contains(view.Notes, note => note.Contains("names material 9", StringComparison.Ordinal));
        });

    [Fact]
    public void A_door_that_opens_is_rebuilt_at_rest_and_republished()
        => WithView((view, rule, graphics, _, party) =>
        {
            rule.Closed = true;
            view.Present(party, 0);
            rule.Closed = false;
            view.Present(party, 0);

            IReadOnlyList<object?[]> meshes = graphics.Calls.CallsTo(nameof(IGraphicsService.CreateMeshResource));
            Assert.Equal(4, meshes.Count);
            Assert.Equal(new Vector3(10, 0, 0), ((MeshResourceCreateRequest)meshes[3][0]!).Positions.Span[1]);
            Assert.Equal(2, graphics.Snapshots.Count);
            Assert.Equal(0, view.Report!.ClosedDoors);
        });

    [Fact]
    public void A_place_content_draws_nothing_for_is_said_once_and_an_image_the_engine_refuses_is_drawn_flat()
        => WithView((view, _, graphics, _, party) =>
        {
            view.Present(party, 0);
            Assert.Single(view.Notes, note => note.Contains("stone.png", StringComparison.Ordinal));
            MaterialRequest flat = (MaterialRequest)graphics.Calls.CallsTo(nameof(IGraphicsService.CreateMaterial))[0][0]!;
            Assert.Equal(default, flat.Texture);

            party.Enter(new PlaceId("2"), new PlacePose(0, 0, 0, 0, 0));
            view.Present(party, 0);
            view.Present(party, 0);

            Assert.Null(view.Report);
            Assert.Single(view.Notes, note => note.Contains("'2'", StringComparison.Ordinal));
            Assert.Equal(2, graphics.Snapshots.Count);
        }, refuseImages: true);

    [Fact]
    public void A_sprite_shows_its_frame_for_its_time_looping_or_holding_its_last()
    {
        SceneSprite sprite = new("s.png", 64, 32, 2, 32, 32, 1, 1, false, false, [0.5, 0.25]);

        Assert.Equal(0, sprite.FrameAt(0.2, loop: true));
        Assert.Equal(1, sprite.FrameAt(0.6, loop: true));
        Assert.Equal(0, sprite.FrameAt(0.8, loop: true));
        Assert.Equal(1, sprite.FrameAt(5, loop: false));
    }

    [Fact]
    public void Objects_are_drawn_as_sprites_under_their_identity_showing_the_view_their_facing_turns_to_the_eye()
        => WithView((view, rule, graphics, _, party) =>
        {
            // The party stands at (100, 200) facing +x; one object stands ahead at (500, 200) facing back at it.
            rule.Things = [new SceneObject("actor:a", "walk", new PlacePose(500, 200, 0, 1024, 0), 0)];
            view.Present(party, 0);

            Assert.Single(graphics.Calls.CallsTo(nameof(IGraphicsService.CreateSpriteAtlas)));
            SpriteFromAtlasRequest created = (SpriteFromAtlasRequest)Assert.Single(graphics.Calls.CallsTo(nameof(IGraphicsService.CreateSpriteFromAtlas)))[0]!;
            Assert.Equal(0u, created.FrameId);
            Assert.Equal(new Vector2(64, 64), created.Size);
            Assert.Equal(BillboardMode.Cylindrical, created.Billboard);
            AppearanceFact drawn = Assert.Single(graphics.Snapshots[^1], fact => fact.ObjectId >= 1UL << 44);
            Assert.Equal(new Vector3(500, 0, -200), drawn.Transform.Translation);

            // Half a second on it shows its second frame from the same side, set on its own sprite without republishing.
            view.Present(party, 0.6);
            rule.Things = [rule.Things[0] with { Seconds = 0.6 }];
            view.Present(party, 0.6);
            Assert.Contains(graphics.Calls.CallsTo(nameof(IGraphicsService.SetSpriteFrame)),
                call => ((SpriteFrameUpdateRequest)call[0]!).FrameId == 8u);

            // Turned away, it shows its back; moved, only it is sent; gone, it is removed.
            rule.Things = [rule.Things[0] with { Feet = new PlacePose(500, 200, 0, 0, 0), Seconds = 0 }];
            view.Present(party, 0.7);
            Assert.Contains(graphics.Calls.CallsTo(nameof(IGraphicsService.SetSpriteFrame)),
                call => ((SpriteFrameUpdateRequest)call[0]!).FrameId == 4u);
            int published = graphics.Snapshots.Count;
            rule.Things = [rule.Things[0] with { Feet = new PlacePose(450, 200, 0, 0, 0) }];
            view.Present(party, 0.8);
            Assert.Equal(published, graphics.Snapshots.Count);
            AppearanceChangesRequest step = (AppearanceChangesRequest)graphics.Calls.CallsTo(nameof(IGraphicsService.PublishChanges))[^1][0]!;
            Assert.Equal(new Vector3(450, 0, -200), Assert.Single(step.Upserts.ToArray()).Transform.Translation);
            rule.Things = [];
            view.Present(party, 0.9);
            AppearanceChangesRequest gone = (AppearanceChangesRequest)graphics.Calls.CallsTo(nameof(IGraphicsService.PublishChanges))[^1][0]!;
            Assert.Single(gone.Removals.ToArray());
        });

    [Fact]
    public void A_burst_the_rule_reports_is_emitted_once_where_it_happened()
    {
        (IPresentationService presentation, RecordingEngineService<IPresentationService> emitted) = RecordingEngineService<IPresentationService>.Create();
        WithView((view, rule, _, _, party) =>
        {
            view.Present(party, 0);
            Assert.Empty(emitted.Calls);

            rule.Due.Add(new SceneBurst(new PlacePose(500, 200, 96, 0, 0), new Vector3(1, 0, 0), 8, 14, 0.3f, 120, "blow-struck"));
            view.Present(party, 0.1);
            view.Present(party, 0.2);
            PresentationParticleDescriptor burst = (PresentationParticleDescriptor)Assert.Single(emitted.CallsTo(nameof(IPresentationService.EmitParticles)))[0]!;
            Assert.Equal("blow-struck", burst.SignalId);
            Assert.Equal(new Vector3(500, 96, -200), burst.Anchor.Position);
            Assert.Equal(8u, burst.BurstCount);
            Assert.Equal((new Vector3(-120), new Vector3(120)), (burst.VelocityMin, burst.VelocityMax));
        }, presentation: presentation);
    }

    [Fact]
    public void Disposing_a_view_that_never_drew_asks_nothing_of_the_engine()
    {
        RecordingUiService ui = new();
        FakeEngineContext engine = new(ui);
        WorldView view = new(engine, new Scenes(), new Rule(), Space);

        view.Dispose();
    }

    private static void WithView(Action<WorldView, Rule, RecordingGraphicsService, RecordingEngineService<ICameraViewService>, PartyPoseOwner> test, bool refuseImages = false,
        IPresentationService? presentation = null)
    {
        byte[] mesh = Mesh();
        RecordingGraphicsService graphics = new((method, arguments) =>
            refuseImages && method.Name == nameof(IGraphicsService.OpenResourceFromContent)
                ? throw new EngineCallException("Graphics", "OpenResourceFromContent", 1)
                : (false, null));
        (ICameraViewService cameras, RecordingEngineService<ICameraViewService> cameraCalls) = RecordingEngineService<ICameraViewService>.Create();
        (IContentService content, _) = RecordingEngineService<IContentService>.Create((method, arguments) => method.Name switch
        {
            nameof(IContentService.ReadReferenceInfo) => (true, (ReadOnlyMemory<ContentReferenceInfo>)new[] { new ContentReferenceInfo("place.mesh", default, (ulong)mesh.Length) }),
            nameof(IContentService.ReadBytes) => (true, (ReadOnlyMemory<byte>)mesh.AsMemory((int)((ContentReadBytesRequest)arguments[0]!).Offset)),
            _ => (false, null),
        });
        FakeEngineContext engine = new(new RecordingUiService(), content: content, graphics: graphics, cameras: cameras, presentation: presentation);
        Rule rule = new();
        using WorldView view = new(engine, new Scenes(), rule, Space);
        PartyPoseOwner party = new(new PartyPose(Place, new PlacePose(100, 200, 0, 0, 0)), Facing);
        test(view, rule, graphics, cameraCalls, party);
    }

    /// <summary>
    /// A three-part mesh: one static triangle, one triangle door 4 moves 50 along the Engine's z when closed, and switch 7's
    /// triangle, which starts hidden.
    /// </summary>
    private static byte[] Mesh()
    {
        Vector3[] positions = [new(0, 0, 0), new(10, 0, 0), new(0, 10, 0), new(0, 0, 0), new(10, 0, 0), new(0, 10, 0)];
        Vector3[] travel = [default, default, default, default, new(0, 0, 50), default];
        List<byte> bytes = [.. "PRMESH02"u8.ToArray()];
        void U32(uint value) { byte[] b = new byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(b, value); bytes.AddRange(b); }
        void F32(float value) { byte[] b = new byte[4]; BinaryPrimitives.WriteSingleLittleEndian(b, value); bytes.AddRange(b); }
        U32(6); U32(6); U32(3); U32(3);
        foreach (Vector3 p in positions) { F32(p.X); F32(p.Y); F32(p.Z); }
        foreach (Vector3 _ in positions) { F32(0); F32(1); F32(0); }
        foreach (Vector3 _ in positions) { F32(0); F32(0); }
        foreach (Vector3 t in travel) { F32(t.X); F32(t.Y); F32(t.Z); }
        foreach (uint index in new uint[] { 0, 1, 2, 0, 1, 2 }) U32(index);
        U32(unchecked((uint)-1)); U32(unchecked((uint)-1)); U32(0); U32(0); U32(3); U32(0); U32(3); U32(0); U32(1);
        U32(4); U32(unchecked((uint)-1)); U32(0); U32(3); U32(3); U32(3); U32(3); U32(1); U32(1);
        U32(unchecked((uint)-1)); U32(7); U32(1); U32(0); U32(3); U32(0); U32(3); U32(2); U32(1);
        U32(0); U32(0); U32(3);
        U32(1); U32(0); U32(3);
        U32(0); U32(0); U32(3);
        return [.. bytes];
    }

    private sealed class Scenes : IPlaceSceneSource
    {
        public PlaceScene? For(PlaceId place) => place == Place
            ? new PlaceScene(place, "place.mesh", [new SceneMaterial("stone.png", SceneAlpha.Opaque), new SceneMaterial(null, SceneAlpha.Cutout)],
                new Dictionary<int, string> { [4] = "door-4" }, null)
            : null;

        public SceneSprite? Sprite(string sprite) => sprite == "walk"
            ? new SceneSprite("walk.png", 256, 64, 8, 32, 32, 8, 2, false, false, [0.5, 0.5])
            : null;
    }

    private sealed class Rule : ISceneRule
    {
        public bool Closed { get; set; }

        public double FieldOfViewDegrees => 60;

        public double ViewDistance => 10_000;

        public SceneLighting Lighting(PlaceId place, bool outdoors) =>
            new(Vector3.One, 0.5f, null, Vector3.One, 0f, Vector3.Zero, Fog: (2_000f, 9_000f));

        public bool IsClosed(PlaceId place, string door) => Closed && door == "door-4";

        public IReadOnlyList<SceneObject> Things { get; set; } = [];

        public IReadOnlyList<SceneObject> Objects(PlaceId place, double seconds) => Things;

        public Dictionary<int, SceneSwitch> Switches { get; } = [];

        public SceneSwitch Switch(PlaceId place, int cog) => Switches.GetValueOrDefault(cog);

        public List<SceneBurst> Due { get; } = [];

        public IReadOnlyList<SceneBurst> Bursts(PlaceId place, double seconds)
        {
            SceneBurst[] due = [.. Due];
            Due.Clear();
            return due;
        }
    }
}
