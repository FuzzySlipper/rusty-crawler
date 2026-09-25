using System.Text;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Movement;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using PartyRpg.Rulesets.MightAndMagic7;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// How a product reaches the explicit save boundary: the save control the host declares, the one admitted
/// update a request travels in, and the outcome the panel is shown.
/// </summary>
/// <remarks>
/// <para>
/// Persistence itself is proved elsewhere: the schema, the bytes, and the load that rebuilds what is
/// transient. What is proved here is reach — that the trigger exists, that it writes exactly once for one
/// request and never by itself, and that a save which could not land is a named outcome rather than a
/// control that did nothing.
/// </para>
/// <para>
/// The party is this game's own creation default, created through the real flow, so a document written
/// here carries what a player's party carries rather than a hand-written seed. The world, the clock, and
/// the placeholder cost rule are the test's own, because the kit holds none of them.
/// </para>
/// </remarks>
public sealed class SaveReachTests
{
    private static readonly ContentLayout Layout = new("packs", "imports", "bundles");
    private static readonly SessionComposition Composition = new(new RulesetId("test.ruleset"), "Save reach");
    private static readonly FacingRule Facing = new(unitsPerTurn: 2048, minimumPitch: -512, maximumPitch: 512);
    private static readonly SaveIntentNames Controls = new("session.save", "session.save", "crawler.ui.action.v1");

    [Fact]
    public void A_save_request_arriving_in_an_admitted_update_writes_exactly_once()
    {
        using Fixture fixture = new();
        fixture.Session.Start();

        // A played session: admitted updates with no save request in them write nothing at all, however
        // many of them pass and however much game time they carry. That is what "no implicit save" means
        // as a property of the code rather than as an intention about it.
        for (ulong step = 1; step <= 120; step++) fixture.Session.Update(Update(step, 60, stepSeconds: 1.0));
        Assert.Empty(fixture.Store.Writes);
        Assert.Equal("none", fixture.Published().Field("save").Field("state").AsString());
        // A session with somewhere to write is the premise of every case below; the panel's own report of
        // that fact is asserted in the host suite, which is where the persistence root is selected.
        Assert.NotNull(fixture.Session.Saves);

        // The player asks: the declared save key, in the input of one admitted update. The moment is read
        // from the clock before that update steps anything, because the player asked at the moment whose
        // projection they were reading.
        GameDate moment = fixture.Clock.Now;
        ClockSave momentRecorded = ClockSave.Capture(fixture.Clock);
        fixture.Session.Update(Update(121, 60, Digital(Controls.Intent)));

        SessionSave written = Assert.Single(fixture.Store.Writes);
        Assert.Equal("session", fixture.Store.WrittenSlots[0]);
        // The document holds the clock as it stood when the player asked, not one admitted interval later:
        // the request is settled before the step its own update takes.
        Assert.Equal(momentRecorded, written.Clock);
        Assert.Equal(fixture.World.Party.Place, written.World.Pose.Place);
        Assert.Equal(fixture.Party.Purse.Coins, written.Party.Coins);
        Assert.Equal(fixture.Party.Members.Count, written.Party.Members.Count);

        // And the panel is told what happened, at the game moment it happened: the outcome, the moment the
        // slot now holds, and the slot a player would come back to. The moment is the clock's, read before
        // the saving update stepped anything, because the player asked at the moment they were looking at.
        ProjectedNode save = fixture.Published().Field("save");
        Assert.Equal("saved", save.Field("state").AsString());
        Assert.Equal("session", save.Field("slot").AsString());
        Assert.Equal($"{moment.Year:0000}-{moment.Month:00}-{moment.Day:00} {moment.Hour:00}:{moment.Minute:00}", save.Field("at").AsString());
        Assert.Equal(string.Empty, save.Field("code").AsString());
        Assert.Contains("Saved the session", save.Field("message").AsString(), StringComparison.Ordinal);

        // The save does not replace the update it arrived in: the same update still measured its interval,
        // so a player who saves while walking keeps walking.
        Assert.Equal(121ul * 60, fixture.Session.AdmittedSteps);
        Assert.NotEqual(0d, fixture.Session.SimulationSeconds);

        // Releasing the session writes nothing either: releasing is not a save.
        fixture.Session.Dispose();
        Assert.Single(fixture.Store.Writes);
    }

    [Fact]
    public void A_later_request_writes_again_and_two_events_in_one_update_write_once()
    {
        using Fixture fixture = new();
        fixture.Session.Start();

        // Two requests in one update are one moment, so they are one save: a key repeat or a button and a
        // key together must not write the same state twice.
        fixture.Session.Update(Update(1, 60, Digital(Controls.Intent), Digital(Controls.Intent)));
        Assert.Single(fixture.Store.Writes);

        fixture.Session.Update(Update(2, 60));
        Assert.Single(fixture.Store.Writes);

        // A request in a later update is a second save, and the slot holds the newer document.
        fixture.Session.Update(Update(3, 60, Digital(Controls.Intent)));
        Assert.Equal(2, fixture.Store.Writes.Count);
        Assert.Equal("saved", fixture.Published().Field("save").Field("state").AsString());
    }

    [Fact]
    public void The_payload_action_the_panel_sends_asks_for_the_same_save()
    {
        using Fixture fixture = new();
        fixture.Session.Start();

        // A payload that names another action, and a payload on another contract, ask for nothing.
        fixture.Session.Update(Update(1, 60, Payload("crawler.ui.action.v1", """{"action":"session.pause"}""")));
        fixture.Session.Update(Update(2, 60, Payload("someone.else.v1", """{"action":"session.save"}""")));
        fixture.Session.Update(Update(3, 60, Payload("crawler.ui.action.v1", """{"action":"creation.accept"}""")));
        Assert.Empty(fixture.Store.Writes);
        Assert.Equal("none", fixture.Published().Field("save").Field("state").AsString());

        // The action the DOM companion's save button sends is the same request as the key.
        fixture.Session.Update(Update(4, 60, Payload("crawler.ui.action.v1", """{"action":"session.save"}""")));
        Assert.Single(fixture.Store.Writes);
        Assert.Equal("saved", fixture.Published().Field("save").Field("state").AsString());
    }

    [Fact]
    public void A_session_whose_host_declared_no_save_control_never_saves()
    {
        using Fixture fixture = new(controls: false);
        fixture.Session.Start();

        // The key is pressed, but this product never declared it as a save control, so it is not one. A
        // session without the declaration has no save trigger at all rather than one it invented.
        fixture.Session.Update(Update(1, 60, Digital(Controls.Intent)));
        fixture.Session.Update(Update(2, 60, Payload("crawler.ui.action.v1", """{"action":"session.save"}""")));

        Assert.Empty(fixture.Store.Writes);
        Assert.Equal("none", fixture.Published().Field("save").Field("state").AsString());
    }

    [Fact]
    public void A_write_the_store_refuses_surfaces_by_name_and_leaves_the_session_playable()
    {
        using Fixture fixture = new(refusal: new SessionSaveException(
            "The save in slot 'session' could not be written: the disk refused it."));
        fixture.Session.Start();

        fixture.Session.Update(Update(1, 60, Digital(Controls.Intent)));

        ProjectedNode save = fixture.Published().Field("save");
        Assert.Equal("failed", save.Field("state").AsString());
        Assert.Equal("save-failed", save.Field("code").AsString());
        Assert.Contains("the disk refused it", save.Field("message").AsString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, save.Field("at").AsString());

        // The refusal is the player's answer, not the end of the session: the same update measured its
        // interval and the session is still running.
        Assert.Equal(60ul, fixture.Session.AdmittedSteps);
        Assert.Equal(SessionMode.Running, fixture.Session.Mode);
    }

    [Fact]
    public void A_store_the_engine_refuses_is_reported_rather_than_thrown_out_of_the_update()
    {
        // A store the product handed the bytes to can fail after it opened, which the engine reports as its
        // own call failure. The player's request must not take the session down with it: it is the same
        // named write failure as any other.
        using Fixture fixture = new(refusal: new EngineCallException("Persistence", "Save", 0));
        fixture.Session.Start();

        fixture.Session.Update(Update(1, 60, Digital(Controls.Intent)));

        ProjectedNode save = fixture.Published().Field("save");
        Assert.Equal("failed", save.Field("state").AsString());
        Assert.Equal("save-failed", save.Field("code").AsString());
        Assert.Contains("could not be written to slot 'session'", save.Field("message").AsString(), StringComparison.Ordinal);
        Assert.Equal(SessionMode.Running, fixture.Session.Mode);
        Assert.Empty(fixture.Store.Writes);
    }

    [Fact]
    public void A_session_holding_nothing_to_save_is_refused_with_every_missing_part_named()
    {
        RecordingSaveStore store = new(null);
        using RecordingUiProjectionChannel channel = new();
        using PartyRpgSession empty = new(Composition, channel, saveStore: store, saveInput: Controls);
        empty.Start();

        empty.Update(Update(1, 60, Digital(Controls.Intent)));

        Assert.Empty(store.Writes);
        ProjectedNode save = channel.Latest().Field("save");
        Assert.Equal("failed", save.Field("state").AsString());
        Assert.Equal("save-refused", save.Field("code").AsString());
        string message = save.Field("message").AsString();
        Assert.Contains("holds no party", message, StringComparison.Ordinal);
        Assert.Contains("holds no clock", message, StringComparison.Ordinal);
        Assert.Contains("holds no world", message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_session_with_nowhere_to_write_says_so_rather_than_saving_nowhere()
    {
        using Fixture fixture = new(store: false);
        fixture.Session.Start();

        // The explicit entry point a caller uses: it reports the loss instead of throwing, and publishes it.
        SaveSnapshot outcome = fixture.Session.RequestSave();

        Assert.False(outcome.Available);
        Assert.Equal(SaveState.Failed, outcome.State);
        Assert.Equal("save-unavailable", outcome.Code);
        Assert.Contains("nowhere to write one", outcome.Message, StringComparison.Ordinal);

        Assert.Equal("failed", fixture.Published().Field("save").Field("state").AsString());

        // The boundary itself still fails loudly for a caller that demanded a write: a product with no
        // persistence must not be able to mistake a refusal for a save.
        Assert.Throws<InvalidOperationException>(() => fixture.Session.Save());
    }

    [Fact]
    public void A_session_composed_from_a_save_starts_with_no_save_of_its_own()
    {
        using Fixture resumed = new(resumed: true);
        ProjectedNode save = resumed.Published().Field("save");

        // A resumed session publishes the slot it came from and has saved nothing itself: resuming is not a
        // save, and the resumed fact the host decided is asserted where the host decides it.
        Assert.Equal("none", save.Field("state").AsString());
        Assert.Equal("session", save.Field("slot").AsString());
        Assert.Equal(string.Empty, save.Field("message").AsString());
    }

    [Fact]
    public void A_save_control_with_a_blank_name_is_refused_where_it_is_declared()
    {
        // A control with no name is a key that could never ask for anything, so it is refused where the
        // product declares it rather than composed as a save nobody can request.
        Assert.Throws<ArgumentException>(() => new SaveIntentNames("", "session.save", "crawler.ui.action.v1"));
        Assert.Throws<ArgumentException>(() => new SaveIntentNames("session.save", " ", "crawler.ui.action.v1"));
        Assert.Throws<ArgumentException>(() => new SaveIntentNames("session.save", "session.save", ""));
    }

    /// <summary>A played session over a party created through this game's own creation flow.</summary>
    private sealed class Fixture : IDisposable
    {
        internal Fixture(bool store = true, bool controls = true, bool resumed = false, Exception? refusal = null)
        {
            Clock = ClockFixture();
            Party = CreatedParty();
            World = WorldFixture(Clock, Party);
            Store = new RecordingSaveStore(refusal);
            Session = new PartyRpgSession(
                Composition,
                Channel,
                World,
                clock: Clock,
                party: Party,
                saveStore: store ? Store : null,
                saveInput: controls ? Controls : null,
                resumed: resumed);
        }

        internal GameClock Clock { get; }

        internal PartyEntity Party { get; }

        internal SessionWorld World { get; }

        internal RecordingSaveStore Store { get; }

        internal RecordingUiProjectionChannel Channel { get; } = new();

        internal PartyRpgSession Session { get; }

        internal ProjectedNode Published() => Channel.Latest();

        public void Dispose()
        {
            Session.Dispose();
            Channel.Dispose();
        }
    }

    private static PartyEntity CreatedParty()
    {
        PartyCreationFlow flow = MightAndMagic7Creation.Start();
        Assert.True(flow.IsComplete);
        return new PartyEntityFactory().Create(flow.ToCreation());
    }

    private static GameClock ClockFixture() => new(
        GameCalendar.TwelveMonthsOfFourWeeks,
        new GameDate(1168, 1, 1, 9, 0, 0),
        new GameTimeScale(30),
        new DaylightWindow(new TimeOfDay(5, 0), new TimeOfDay(21, 0)));

    private static SessionWorld WorldFixture(GameClock clock, PartyEntity party) =>
        new(
            Graph(),
            new PartyPoseOwner(new PartyPose(new PlaceId("1"), new PlacePose(1, 2, 3, 512, 0)), Facing),
            new PlaceStateLedger(Graph(), PlaceRespawnRule.FromContent()),
            new FreeTravel(),
            clock,
            clock: clock,
            resources: new PartyResourceLedger(party));

    private static PlaceGraph Graph() => PlaceGraphLoader.Load(
        ContentCatalogLoader.Load(
            new InMemoryContentSource()
                .Add("packs/world/pack.json",
                    """
                    {
                      "schemaVersion": 1,
                      "packId": "world",
                      "kind": "definitions",
                      "provenance": { "description": "test content" },
                      "documents": [ { "path": "places.json", "documentId": "places", "definitionKind": "place" } ]
                    }
                    """)
                .Add("packs/world/places.json",
                    """
                    {
                      "documentId": "places",
                      "definitionKind": "place",
                      "entries": [
                        { "id": "1", "kind": "region", "name": "Home", "respawnDays": 1,
                          "entryPoints": [ { "id": "Party Start", "x": 1, "y": 2, "z": 3, "yaw": 512 } ] }
                      ]
                    }
                    """),
            Layout).RequireValid());

    private static ProductUpdate Update(ulong step, uint admitted, params ProductInputEvent[] input) =>
        Update(step, admitted, 1.0 / 60.0, input);

    private static ProductUpdate Update(ulong step, uint admitted, double stepSeconds, params ProductInputEvent[] input)
    {
        ProductUpdateFacts facts = new(
            ProductUpdateMode.Realtime,
            ProductLifecycleState.Running,
            1,
            1,
            0,
            step,
            60,
            admitted,
            0,
            stepSeconds);
        return new ProductUpdate(facts, input);
    }

    /// <summary>One digital event on a product intent, in the shape the engine admits it.</summary>
    private static ProductInputEvent Digital(string intent) => new(
        InputEventKind.MappedDigital, InputEdge.Pressed, default, default, default, default, default, default, default, default,
        InputValueKind.Digital, InputPhase.Pressed, InputProvenance.Physical, default, default, default, 0f, 0f,
        ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, Encoding.UTF8.GetBytes(intent),
        ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty);

    /// <summary>One payload action, as the DOM companion sends it.</summary>
    private static ProductInputEvent Payload(string contract, string json) => new(
        InputEventKind.DirectProductPayload, InputEdge.None, default, default, default, default, default, default, default, default,
        InputValueKind.ProductPayload, InputPhase.DirectUi, InputProvenance.DirectUi, default, default, default, 0f, 0f,
        ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty,
        Encoding.UTF8.GetBytes(contract), Encoding.UTF8.GetBytes(json));

    /// <summary>A cost rule that charges nothing, so this suite tests the save and not a journey.</summary>
    private sealed class FreeTravel : ITravelCostRule
    {
        public TravelCostQuote Quote(TransitionRequest request) => TravelCostQuote.Payable(TravelCost.Free);
    }

    /// <summary>
    /// A store that records what was written, or refuses to write when a test hands it the refusal. The
    /// recorded slot is kept beside the document so "the right slot was written" is observable too.
    /// </summary>
    private sealed class RecordingSaveStore(Exception? refusal) : ISessionSaveStore
    {
        internal List<SessionSave> Writes { get; } = [];

        internal List<string> WrittenSlots { get; } = [];

        public void Write(string slot, SessionSave save)
        {
            if (refusal is not null) throw refusal;
            WrittenSlots.Add(slot);
            Writes.Add(save);
        }

        public SessionSave? Read(string slot) => Writes.Count > 0 ? Writes[^1] : null;

        public void Dispose()
        {
        }
    }
}
