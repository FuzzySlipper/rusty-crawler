using PartyRpg.Kit;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using PartyRpg.Rulesets.MightAndMagic7;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Host.Tests;

/// <summary>
/// The product's own lifecycle: what each engine call does to the session it holds, what every guard in front of
/// those calls refuses to forward, what a restart that cannot compose a session leaves behind, which bundle an
/// explicit selection starts from, and the save slot a resumed run reads through the engine's persistence.
/// </summary>
/// <remarks>
/// The product owns no gameplay state, so what it does is observed at its one seam: the ruleset it was handed.
/// <see cref="RecordingRuleset"/> composes the real ruleset's sessions and records every call the product forwards
/// to each, which is what lets a test tell a guard that swallowed a call from a session that ignored it. The engine's
/// UI service is counted the same way, because a session's projection stream is the one engine resource the
/// product opens on its behalf and must release.
/// </remarks>
public sealed class ProductLifecycleTests
{
    [Fact]
    public void Pause_resume_and_update_before_start_reach_no_session()
    {
        (CrawlerProduct product, RecordingRuleset ruleset, _) = Compose();
        using (product)
        {
            product.Pause();
            product.Resume();
            Assert.Equal(ProductUpdateResult.None, product.Update(ProductTestContext.Update(1, 4)));

            Assert.Empty(Assert.Single(ruleset.Sessions).Calls);
            Assert.Equal(SessionMode.Starting, product.Mode);
        }
    }

    [Fact]
    public void An_engine_pause_holds_a_running_session_and_its_resume_releases_it()
    {
        (CrawlerProduct product, RecordingRuleset ruleset, _) = Compose();
        using (product)
        {
            product.Start();
            Accept(product, simulationStep: 1);
            Assert.Equal(SessionMode.Running, product.Mode);

            product.Pause();
            Assert.Equal(SessionMode.Paused, product.Mode);

            product.Resume();
            Assert.Equal(SessionMode.Running, product.Mode);

            Assert.Equal(["Start", "Update", "Pause", "Resume"], Assert.Single(ruleset.Sessions).Calls);
        }
    }

    [Fact]
    public void Updates_admitted_while_the_engine_paused_measure_no_session_time()
    {
        (CrawlerProduct product, _, CountingUi ui) = Compose();
        using (product)
        {
            product.Start();
            Accept(product, simulationStep: 1);
            product.Update(ProductTestContext.Update(2, 4));
            Assert.Equal(4d, Session(ui).Field("admittedSteps").AsNumber());

            product.Pause();
            int published = ui.Projections.Count;
            Assert.Equal("paused", Session(ui).Field("mode").AsString());

            // Twenty admitted steps arrive while the engine holds the session: the held session hears nothing,
            // so it publishes nothing, and none of those steps is counted against its time.
            product.Update(ProductTestContext.Update(3, 10));
            product.Update(ProductTestContext.Update(13, 10));
            Assert.Equal(published, ui.Projections.Count);

            product.Resume();
            product.Update(ProductTestContext.Update(23, 4));

            ProjectedNode session = Session(ui);
            Assert.Equal("running", session.Field("mode").AsString());
            // The count is the session's running total: the four steps before the pause and the four after it.
            Assert.Equal(8d, session.Field("admittedSteps").AsNumber());
            Assert.Equal(8.0 / 60.0, session.Field("simulationSeconds").AsNumber(), precision: 9);
        }
    }

    [Fact]
    public void An_engine_resume_does_not_release_a_hold_the_player_asked_for()
    {
        (CrawlerProduct product, _, _) = Compose();
        using (product)
        {
            product.Start();
            Accept(product, simulationStep: 1);

            // The player holds the session with the declared pause control, then the engine pauses and resumes it.
            product.Update(ProductTestContext.Update(2, 1, ProductTestContext.Digital(ProductIdentity.PauseToggleIntent)));
            Assert.Equal(SessionMode.Paused, product.Mode);
            product.Pause();
            product.Resume();

            Assert.Equal(SessionMode.Paused, product.Mode);

            // The player's own control is what releases it.
            product.Update(ProductTestContext.Update(3, 1, ProductTestContext.Digital(ProductIdentity.PauseToggleIntent)));
            Assert.Equal(SessionMode.Running, product.Mode);
        }
    }

    [Fact]
    public void Shutdown_disposes_the_session_once_stops_it_and_releases_its_projection_stream()
    {
        (CrawlerProduct product, RecordingRuleset ruleset, CountingUi ui) = Compose();
        product.Start();
        Assert.Equal(1, ui.Opened);
        Assert.Equal(0, ui.Released);

        product.Shutdown();
        product.Shutdown();
        product.Dispose();

        RecordingSession session = Assert.Single(ruleset.Sessions);
        Assert.Equal(["Start", "Dispose"], session.Calls);
        Assert.Equal(SessionMode.Stopped, product.Mode);
        Assert.Equal("stopped", Session(ui).Field("mode").AsString());
        Assert.Equal(1, ui.Released);
    }

    [Fact]
    public void After_shutdown_start_pause_resume_restart_and_update_are_ignored()
    {
        (CrawlerProduct product, RecordingRuleset ruleset, CountingUi ui) = Compose();
        product.Start();
        product.Shutdown();
        int published = ui.Projections.Count;

        // Each of these would reach a disposed session, which refuses every call, if the product forwarded it.
        product.Start();
        product.Pause();
        product.Resume();
        product.Restart();
        Assert.Equal(ProductUpdateResult.None, product.Update(ProductTestContext.Update(1, 4)));

        RecordingSession session = Assert.Single(ruleset.Sessions);
        Assert.Equal(["Start", "Dispose"], session.Calls);
        Assert.Equal(1, ui.Opened);
        Assert.Equal(published, ui.Projections.Count);
        Assert.Equal(SessionMode.Stopped, product.Mode);
    }

    [Fact]
    public void Start_after_shutdown_of_a_product_never_started_does_not_start_the_session()
    {
        (CrawlerProduct product, RecordingRuleset ruleset, _) = Compose();
        product.Shutdown();

        product.Start();

        Assert.Equal(["Dispose"], Assert.Single(ruleset.Sessions).Calls);
        Assert.Equal(SessionMode.Stopped, product.Mode);
    }

    [Fact]
    public void A_restart_before_start_replaces_the_session_without_starting_the_replacement()
    {
        (CrawlerProduct product, RecordingRuleset ruleset, CountingUi ui) = Compose();
        using (product)
        {
            product.Restart();

            Assert.Equal(2, ruleset.Sessions.Count);
            Assert.Equal(["Dispose"], ruleset.Sessions[0].Calls);
            Assert.Empty(ruleset.Sessions[1].Calls);
            Assert.Equal(SessionMode.Starting, product.Mode);
            Assert.Equal(1, ui.Opened);
            Assert.Equal(0, ui.Released);

            // The replacement is the session a later start starts.
            product.Start();
            Assert.Equal(["Start"], ruleset.Sessions[1].Calls);
            Assert.Equal(SessionMode.Creating, product.Mode);
        }
    }

    [Fact]
    public void A_restart_after_start_disposes_the_previous_session_and_starts_the_replacement()
    {
        (CrawlerProduct product, RecordingRuleset ruleset, _) = Compose();
        using (product)
        {
            product.Start();
            Accept(product, simulationStep: 1);
            Assert.Equal(SessionMode.Running, product.Mode);

            product.Restart();

            Assert.Equal(2, ruleset.Sessions.Count);
            Assert.Equal(["Start", "Update", "Dispose"], ruleset.Sessions[0].Calls);
            Assert.Equal(["Start"], ruleset.Sessions[1].Calls);
            Assert.Equal(SessionMode.Creating, product.Mode);

            // Updates now reach the replacement and nothing reaches the released session.
            product.Update(ProductTestContext.Update(2, 1));
            Assert.Equal(["Start", "Update", "Dispose"], ruleset.Sessions[0].Calls);
            Assert.Equal(["Start", "Update"], ruleset.Sessions[1].Calls);
        }
    }

    [Fact]
    public void Repeated_replacements_keep_one_stream_and_strictly_increasing_projection_sequences()
    {
        (CrawlerProduct product, _, CountingUi ui) = Compose();
        using (product)
        {
            product.Start();
            Accept(product, simulationStep: 1);
            product.Restart();
            product.Restart();

            Assert.Equal(1, ui.Opened);
            Assert.Equal(0, ui.Released);
            Assert.NotEmpty(ui.Projections);
            UiStreamHandle stream = ui.Projections[0].Stream.Handle;
            Assert.All(ui.Projections, projection => Assert.Equal(stream, projection.Stream.Handle));
            Assert.True(ui.Projections
                .Zip(ui.Projections.Skip(1), (before, after) => after.Sequence > before.Sequence)
                .All(increasing => increasing));
            Assert.Equal("creating", Session(ui).Field("mode").AsString());
        }

        Assert.Equal(1, ui.Released);
    }

    [Fact]
    public void A_restart_whose_session_cannot_be_composed_throws_and_keeps_the_running_session()
    {
        (CrawlerProduct product, RecordingRuleset ruleset, CountingUi ui) = Compose();
        product.Start();
        Accept(product, simulationStep: 1);

        ruleset.Refuse = true;
        InvalidOperationException refused = Assert.Throws<InvalidOperationException>(product.Restart);
        Assert.Same(ruleset.Refusal, refused);

        // The running session is still the product's: not released, still running, and still the one the next
        // admitted update reaches.
        RecordingSession running = Assert.Single(ruleset.Sessions);
        Assert.Equal(["Start", "Update"], running.Calls);
        Assert.Equal(SessionMode.Running, product.Mode);
        product.Update(ProductTestContext.Update(2, 4));
        Assert.Equal(["Start", "Update", "Update"], running.Calls);
        Assert.Equal("running", Session(ui).Field("mode").AsString());

        // The product's one stream stays open across the failed replacement attempt.
        Assert.Equal(1, ui.Opened);
        Assert.Equal(0, ui.Released);

        // And a later restart that can compose one succeeds over it.
        ruleset.Refuse = false;
        product.Restart();
        Assert.Equal(2, ruleset.Sessions.Count);
        Assert.Equal(["Start", "Update", "Update", "Dispose"], running.Calls);
        Assert.Equal(SessionMode.Creating, product.Mode);

        product.Shutdown();
        Assert.Equal(ui.Opened, ui.Released);
    }

    [Fact]
    public void A_product_whose_first_session_cannot_be_composed_fails_where_it_is_created_and_releases_its_stream()
    {
        CountingUi ui = new();
        RecordingRuleset ruleset = new() { Refuse = true };

        InvalidOperationException refused = Assert.Throws<InvalidOperationException>(
            () => new CrawlerProduct(Context(ui, StagedContent()), ruleset, bundleId: null));

        Assert.Same(ruleset.Refusal, refused);
        Assert.Empty(ruleset.Sessions);
        Assert.Equal(1, ui.Opened);
        Assert.Equal(1, ui.Released);
    }

    [Fact]
    public void Creating_the_product_without_a_context_or_a_ruleset_is_refused()
    {
        CountingUi ui = new();

        Assert.Equal("context", Assert.Throws<ArgumentNullException>(
            () => new CrawlerProduct(null!, new RecordingRuleset(), bundleId: null)).ParamName);
        Assert.Equal("ruleset", Assert.Throws<ArgumentNullException>(
            () => new CrawlerProduct(Context(ui, StagedContent()), null!, bundleId: null)).ParamName);
        Assert.Equal(0, ui.Opened);
    }

    [Fact]
    public void An_explicit_bundle_id_selects_that_staged_bundle_rather_than_the_default()
    {
        CountingUi ui = new();
        (string Path, string Text)[] content =
        [
            .. StagedContent(),
            ProductTestContext.Bundle("other", "places", "extra", "creation-tables"),
            .. ProductTestContext.Pack("extra", "somewhere"),
        ];

        using CrawlerProduct product = new(Context(ui, content), BuiltInRulesets.Default, bundleId: "other");
        product.Start();

        Assert.Equal("other", product.Selection.BundleId);
        Assert.Equal(3, product.Selection.PackCount);
        ProjectedNode composition = ProjectedNode.Of(ui.Latest().Value).Field("composition");
        Assert.Equal("other", composition.Field("bundle").AsString());
        Assert.Equal(3d, composition.Field("contentPacks").AsNumber());
    }

    [Fact]
    public void An_explicit_bundle_id_the_staged_bundles_do_not_hold_stops_the_product_naming_it()
    {
        CountingUi ui = new();

        ContentValidationException refused = Assert.Throws<ContentValidationException>(
            () => new CrawlerProduct(Context(ui, StagedContent()), BuiltInRulesets.Default, bundleId: "absent-bundle"));

        ContentValidationIssue missing = Assert.Single(refused.Issues);
        Assert.Equal("requested-bundle-missing", missing.Code);
        Assert.Equal("absent-bundle", missing.PackId);
        // Nothing was composed, so no stream was opened for a session that never existed.
        Assert.Equal(0, ui.Opened);
    }

    [Fact]
    public void An_explicit_bundle_id_over_a_content_root_with_no_bundles_starts_with_no_selection()
    {
        CountingUi ui = new();

        using CrawlerProduct product = new(Context(ui), BuiltInRulesets.Default, bundleId: "absent-bundle");
        product.Start();

        Assert.Equal(BundleSelection.None, product.Selection);
        Assert.Equal(SessionMode.Creating, product.Mode);
        Assert.Equal(string.Empty, ProjectedNode.Of(ui.Latest().Value).Field("composition").Field("bundle").AsString());
    }

    /// <remarks>
    /// <see cref="SessionPersistenceTests"/> already proves a save written by the product's save control resumes in
    /// a second product built with <see cref="SessionStart.Resume"/> passed to its constructor. What this adds is the
    /// switch as the running product actually receives it — the declared environment variable, read by the entry
    /// the engine calls — and that the bytes it resumes are the ones the product wrote into the engine's service,
    /// under this game's store scope and slot.
    /// </remarks>
    [Fact]
    public void A_save_written_through_the_product_resumes_in_a_new_product_told_to_resume_by_its_start_variable()
    {
        InMemoryPersistenceService persistence = new();
        (string Path, string Text)[] content = WorldContent();

        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(persistence, content);
        ProjectedNode before;
        using (CrawlerProduct product = new(context, ProductTestContext.NoVariables))
        {
            product.Start();
            Accept(product, simulationStep: 1);
            product.Update(ProductTestContext.Update(2, admittedSteps: 60, stepSeconds: 1.0));
            product.Update(ProductTestContext.Update(3, 1, ProductTestContext.Digital(ProductIdentity.SaveIntent)));
            before = ProjectedNode.Of(ui.Latest().Value);
            Assert.Equal("saved", before.Field("save").Field("state").AsString());
        }

        byte[]? written = persistence.Payload(MightAndMagic7Persistence.StoreScope, MightAndMagic7Persistence.SaveSlot);
        Assert.NotNull(written);

        List<string> asked = [];
        (ProductCreateContext resumedContext, RecordingUiService resumedUi) = ProductTestContext.Create(persistence, content);
        using CrawlerProduct resumed = new(resumedContext, name =>
        {
            asked.Add(name);
            return name == ProductIdentity.StartVariable ? "resume" : null;
        });

        Assert.Equal([ProductIdentity.BundleVariable, ProductIdentity.StartVariable], asked);
        Assert.Equal(SessionStart.Resume, resumed.StartMode);
        resumed.Start();
        Assert.Equal(SessionMode.Running, resumed.Mode);

        ProjectedNode after = ProjectedNode.Of(resumedUi.Latest().Value);
        Assert.True(after.Field("save").Field("resumed").AsBoolean());
        Assert.Equal(before.Field("clock").Field("date").AsString(), after.Field("clock").Field("date").AsString());
        Assert.Equal(before.Field("clock").Field("time").AsString(), after.Field("clock").Field("time").AsString());
        Assert.Equal(before.Field("party").Field("members").AsNumber(), after.Field("party").Field("members").AsNumber());

        // Resuming reads the slot and writes nothing back into it.
        Assert.Equal(written, persistence.Payload(MightAndMagic7Persistence.StoreScope, MightAndMagic7Persistence.SaveSlot));
    }

    /// <summary>Content a product can create a party in: the default bundle, one pack, and the creation tables.</summary>
    private static (string Path, string Text)[] StagedContent() =>
    [
        ProductTestContext.Bundle(BuiltInBundles.Default, "places", "creation-tables"),
        .. ProductTestContext.Pack("places", "emerald"),
        .. ProductTestContext.CreationTables(),
    ];

    /// <summary>
    /// Content a created party is placed in a world by: the creation tables and one region the scenario starts in,
    /// which is what a save has a place and a pose to carry from.
    /// </summary>
    private static (string Path, string Text)[] WorldContent() =>
    [
        ProductTestContext.Bundle(BuiltInBundles.Default, "world", "creation-tables"),
        .. ProductTestContext.CreationTables(),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/pack.json",
            TestPacks.Manifest("world", TestPacks.Places, ("start", "scenario-start"))),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/places.json",
            TestPacks.Document("places", "place",
                """{ "id": "1", "kind": "region", "name": "Island", "respawnDays": 7, "entryPoints": [ { "id": "Party Start", "x": 10, "y": 20, "z": 0, "yaw": 0 } ] }""")),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/start.json",
            TestPacks.Document("start", "scenario-start", """{ "id": "start", "place": "1", "entryPoint": "Party Start" }""")),
    ];

    /// <summary>A product over the recording ruleset, the counting UI, and <see cref="StagedContent"/>.</summary>
    private static (CrawlerProduct Product, RecordingRuleset Ruleset, CountingUi Ui) Compose()
    {
        CountingUi ui = new();
        RecordingRuleset ruleset = new();
        return (new CrawlerProduct(Context(ui, StagedContent()), ruleset, bundleId: null), ruleset, ui);
    }

    /// <summary>Accepts the default party through the product's declared control, which leaves creation for the world.</summary>
    private static void Accept(CrawlerProduct product, ulong simulationStep) =>
        product.Update(ProductTestContext.Update(simulationStep, 1, ProductTestContext.Digital(ProductIdentity.CreationAcceptIntent)));

    /// <summary>The session block of the latest projection.</summary>
    private static ProjectedNode Session(CountingUi ui) => ProjectedNode.Of(ui.Latest().Value).Field("session");

    /// <summary>A product context over staged files whose engine's UI service is the given counting one.</summary>
    private static ProductCreateContext Context(CountingUi ui, params (string Path, string Text)[] files)
    {
        (ProductCreateContext staged, _) = ProductTestContext.Create(files);
        return new ProductCreateContext(
            new CountingEngineContext(staged.Engine, ui),
            staged.Content,
            staged.Input,
            new Rusty.Engine.Debugging.DebugExecutionContext());
    }

    /// <summary>
    /// The real ruleset, recording every session it composes and every call the product forwards to each, and
    /// refusing to compose one when a test says so.
    /// </summary>
    private sealed class RecordingRuleset : IGameRuleset
    {
        private readonly IGameRuleset _inner = BuiltInRulesets.Default;

        public bool Refuse { get; set; }

        public InvalidOperationException Refusal { get; } = new("the ruleset could not compose a session");

        public List<RecordingSession> Sessions { get; } = [];

        public RulesetId Id => _inner.Id;

        public string Title => _inner.Title;

        public IGameSession CreateSession(RulesetSessionContext context)
        {
            if (Refuse) throw Refusal;
            RecordingSession session = new(_inner.CreateSession(context));
            Sessions.Add(session);
            return session;
        }
    }

    /// <summary>A composed session that records each lifecycle call and update the product forwards to it.</summary>
    private sealed class RecordingSession(IGameSession inner) : IGameSession
    {
        public List<string> Calls { get; } = [];

        public SessionMode Mode => inner.Mode;

        public void Start() { Calls.Add(nameof(Start)); inner.Start(); }

        public void Pause() { Calls.Add(nameof(Pause)); inner.Pause(); }

        public void Resume() { Calls.Add(nameof(Resume)); inner.Resume(); }

        public void Hold() { Calls.Add(nameof(Hold)); inner.Hold(); }

        public void ReleaseHold() { Calls.Add(nameof(ReleaseHold)); inner.ReleaseHold(); }

        public ProductUpdateResult Update(ProductUpdate update) { Calls.Add(nameof(Update)); return inner.Update(update); }

        public SessionSnapshot Inspect() => inner.Inspect();

        public Refusal? Look(double yawDegrees, double pitchDegrees) { Calls.Add(nameof(Look)); return inner.Look(yawDegrees, pitchDegrees); }

        public void Dispose() { Calls.Add(nameof(Dispose)); inner.Dispose(); }

        public void DisposeForReplacement() { Calls.Add(nameof(Dispose)); inner.DisposeForReplacement(); }
    }

    /// <summary>An engine UI service that records projections and counts the streams opened and released.</summary>
    private sealed class CountingUi : IUiService
    {
        private readonly List<UiProjection> _projections = [];
        private ulong _nextHandle = 1;

        public int Opened { get; private set; }

        public int Released { get; private set; }

        public IReadOnlyList<UiProjection> Projections => _projections;

        public UiProjection Latest() =>
            _projections.Count > 0 ? _projections[^1] : throw new InvalidOperationException("Nothing was published.");

        public UiStream OpenStream(UiStreamRequest request)
        {
            Opened++;
            return new UiStream(new UiStreamHandle(_nextHandle++), () => Released++);
        }

        public UiImage OpenImage(UiImageRequest request) =>
            throw new NotSupportedException("The product opens no UI images.");

        public void PublishProjection(UiProjection projection) => _projections.Add(projection);
    }

    /// <summary>The shared engine context double with its UI service replaced by a counting one.</summary>
    private sealed class CountingEngineContext(IEngineContext inner, IUiService ui) : IEngineContext
    {
        public IUiService Ui => ui;

        public IInputService Input => inner.Input;

        public IImplicitSurfacesService ImplicitSurfaces => inner.ImplicitSurfaces;

        public IDiagnosticsService Diagnostics => inner.Diagnostics;

        public IDynamicsService Dynamics => inner.Dynamics;

        public IMotionService Motion => inner.Motion;

        public IKinematicService Kinematic => inner.Kinematic;

        public ISpatialService Spatial => inner.Spatial;

        public IPerceptionService Perception => inner.Perception;

        public IWorldOriginService WorldOrigin => inner.WorldOrigin;

        public IVoxelService Voxel => inner.Voxel;

        public IVoxelContentService VoxelContent => inner.VoxelContent;

        public IVoxelScenePresentationService VoxelScenePresentation => inner.VoxelScenePresentation;

        public IContentService Content => inner.Content;

        public IAuthoredContentService AuthoredContent => inner.AuthoredContent;

        public IGraphicsService Graphics => inner.Graphics;

        public IPresentationService Presentation => inner.Presentation;

        public IAnimationService Animation => inner.Animation;

        public IAudioService Audio => inner.Audio;

        public IVideoService Video => inner.Video;

        public ICameraViewService CameraView => inner.CameraView;

        public IRandomService Random => inner.Random;

        public IPersistenceService Persistence => inner.Persistence;

        public IRenderOutputService RenderOutput => inner.RenderOutput;
    }
}
