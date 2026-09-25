using System.Buffers;
using System.Text;
using System.Text.Json;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.World;
using PartyRpg.Rulesets.MightAndMagic7;
using Rusty.Engine;
using Rusty.Engine.Persistence;
using Xunit;

namespace PartyRpg.Host.Tests;

/// <summary>
/// The product's save path through the engine's own store: a session writes its current schema into the
/// engine's persistence service, and resumes from those bytes.
/// </summary>
/// <remarks>
/// <para>
/// This is the composition a running product takes and the kit's suite cannot: the ruleset composes the
/// session over an engine context, the save goes through the engine's product state store and its JSON
/// codec over build-generated metadata, and a resume composes a fresh session from what came back. The
/// double here is the engine's persistence <em>service</em>, which only a running host can supply; nothing
/// about the save schema, the store, or the session is doubled.
/// </para>
/// <para>
/// A played session's full round trip — party, items, equipment, portraits, clock, place, per-place state,
/// food — is proved by the kit's suite, which drives a world without an engine runtime. What is proved here
/// is the wiring: the bytes really land in the engine's store, resume through it, and a save this world
/// cannot hold fails with every problem named.
/// </para>
/// </remarks>
public sealed class SessionPersistenceTests
{
    private const string Scope = "sessions";
    private const string Slot = "session";

    [Fact]
    public void A_session_saves_into_the_engines_store_and_resumes_from_it()
    {
        InMemoryPersistenceService persistence = new();
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(
            persistence,
            [ProductTestContext.Bundle(BuiltInBundles.Default, "world", "scenario"), .. World(), .. Scenario()]);

        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(Context(context, ui));
        session.Start();

        // Half an hour of game time admitted in one batch, so the resumed clock has somewhere to come back to.
        session.Update(ProductTestContext.Update(1, admittedSteps: 60, stepSeconds: 1.0));

        ProjectedNode before = ProjectedNode.Of(ui.Latest().Value);
        Assert.Equal("09:30", before.Field("clock").Field("time").AsString());
        Assert.Equal(137d, before.Field("party").Field("coins").AsNumber());

        SessionSave written = MightAndMagic7Ruleset.Instance.Save(session);

        // The bytes are in the engine's store, under this game's scope and slot, and they are the document
        // the codec writes — which is what "the product saves through the engine" means.
        byte[]? payload = persistence.Payload(Scope, Slot);
        Assert.NotNull(payload);
        Assert.Equal(Encode(written), payload);
        using JsonDocument document = JsonDocument.Parse(payload);
        Assert.Equal(["party", "clock", "world"], document.RootElement.EnumerateObject().Select(property => property.Name));

        // A resume composes a fresh session from those bytes, over the same content.
        (ProductCreateContext resumedContext, RecordingUiService resumedUi) = ProductTestContext.Create(
            persistence,
            [ProductTestContext.Bundle(BuiltInBundles.Default, "world", "scenario"), .. World(), .. Scenario()]);
        using IGameSession resumed = MightAndMagic7Ruleset.Instance.ResumeSession(Context(resumedContext, resumedUi));

        ProjectedNode after = ProjectedNode.Of(resumedUi.Latest().Value);
        Assert.Equal(before.Field("clock").Field("date").AsString(), after.Field("clock").Field("date").AsString());
        Assert.Equal(before.Field("clock").Field("time").AsString(), after.Field("clock").Field("time").AsString());
        Assert.Equal(before.Field("clock").Field("elapsedDays").AsNumber(), after.Field("clock").Field("elapsedDays").AsNumber());
        Assert.Equal(before.Field("world").Field("place").AsString(), after.Field("world").Field("place").AsString());
        Assert.Equal(before.Field("world").Field("name").AsString(), after.Field("world").Field("name").AsString());
        Assert.Equal(before.Field("world").Field("visited").AsNumber(), after.Field("world").Field("visited").AsNumber());
        Assert.Equal(before.Field("party").Field("coins").AsNumber(), after.Field("party").Field("coins").AsNumber());
        Assert.Equal(before.Field("party").Field("provisions").AsNumber(), after.Field("party").Field("provisions").AsNumber());
        Assert.Equal(before.Field("party").Field("members").AsNumber(), after.Field("party").Field("members").AsNumber());

        // And it is a session that can be played, not a document held in memory.
        resumed.Start();
        resumed.Update(ProductTestContext.Update(2, admittedSteps: 60, stepSeconds: 1.0));
        Assert.Equal(SessionMode.Running, resumed.Mode);
    }

    [Fact]
    public void A_session_played_and_saved_by_its_declared_intent_resumes_to_the_state_before_it()
    {
        InMemoryPersistenceService persistence = new();
        (string Path, string Text)[] content =
        [
            ProductTestContext.Bundle(BuiltInBundles.Default, "world", "scenario"),
            .. World(),
            .. Scenario(),
            // The host offers a creation screen, so a new product starts in creation and its party is the one
            // the player accepts. A bundle a player creates a party in must declare the classes and skills
            // creation offers, which is what this fixture stages.
            .. ProductTestContext.CreationTables(),
        ];

        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(persistence, content);
        ProjectedNode before;
        using (CrawlerProduct product = new(context))
        {
            product.Start();
            Assert.Equal(SessionMode.Creating, product.Mode);
            Assert.Equal(SessionStart.Fresh, product.StartMode);

            // A played session: the party accepted through the product's own control, then a minute of
            // admitted time, which this game's clock reads as half an hour, so the clock the save carries is
            // not its starting value.
            product.Update(ProductTestContext.Update(1, 1, ProductTestContext.Digital(ProductIdentity.CreationAcceptIntent)));
            Assert.Equal(SessionMode.Running, product.Mode);
            product.Update(ProductTestContext.Update(2, admittedSteps: 60, stepSeconds: 1.0));

            before = ProjectedNode.Of(ui.Latest().Value);
            Assert.Equal("09:30", before.Field("clock").Field("time").AsString());
            Assert.Equal(4d, before.Field("party").Field("members").AsNumber());
            Assert.True(before.Field("world").Field("places").AsNumber() > 0);

            // The save key the host declared, inside one admitted update: nothing before it wrote, and the
            // boundary writes once when it arrives.
            Assert.Null(persistence.Payload(Scope, Slot));
            product.Update(ProductTestContext.Update(3, 1, ProductTestContext.Digital(ProductIdentity.SaveIntent)));

            ProjectedNode saved = ProjectedNode.Of(ui.Latest().Value);
            Assert.Equal("saved", saved.Field("save").Field("state").AsString());
            Assert.Equal(Slot, saved.Field("save").Field("slot").AsString());
            Assert.Equal("1168-01-01 09:30", saved.Field("save").Field("at").AsString());
            Assert.False(saved.Field("save").Field("resumed").AsBoolean());
            Assert.Contains("Saved the session", saved.Field("save").Field("message").AsString(), StringComparison.Ordinal);
            Assert.NotNull(persistence.Payload(Scope, Slot));
        }

        // A second product over the same bytes, told by its host to resume: the switch is explicit, and the
        // session it composes is the one the save holds.
        (ProductCreateContext resumedContext, RecordingUiService resumedUi) = ProductTestContext.Create(persistence, content);
        using CrawlerProduct resumed = new(resumedContext, BuiltInRulesets.Default, bundleId: null, start: SessionStart.Resume);
        Assert.Equal(SessionStart.Resume, resumed.StartMode);
        resumed.Start();
        Assert.Equal(SessionMode.Running, resumed.Mode);

        ProjectedNode after = ProjectedNode.Of(resumedUi.Latest().Value);
        Assert.True(after.Field("save").Field("resumed").AsBoolean());
        Assert.Equal("none", after.Field("save").Field("state").AsString());

        // The same clock, place, pose, party, and standing the session was saved at, field by field: the
        // save carried them and the load rebuilt a session that publishes them again.
        foreach (string block in new[] { "clock", "world", "party" })
        {
            foreach (string field in Fields(block))
            {
                Assert.Equal(Read(before, block, field), Read(after, block, field));
            }
        }

        static string Read(ProjectedNode node, string block, string field)
        {
            ProjectedNode value = node.Field(block).Field(field);
            return field is "date" or "time" or "daylight" or "place" or "name" or "kind" or "unit" or "conditions"
                ? value.AsString()
                : value.AsNumber().ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        // And the party a player reads, member by member: a resumed session publishes the same roster, from
        // the restored party rather than from the creation flow that no longer exists.
        Assert.Equal(before.Field("creation").Field("party").Count(), after.Field("creation").Field("party").Count());
        for (int index = 0; index < before.Field("creation").Field("party").Count(); index++)
        {
            foreach (string field in new[] { "name", "race", "class", "portrait" })
            {
                Assert.Equal(
                    before.Field("creation").Field("party").Element(index).Field(field).AsString(),
                    after.Field("creation").Field("party").Element(index).Field(field).AsString());
            }
        }

        // And it is played on rather than merely shown: the next admitted update steps the resumed session.
        resumed.Update(ProductTestContext.Update(4, 60));
        Assert.Equal(SessionMode.Running, resumed.Mode);
    }

    /// <summary>The fields of a projection block this round trip compares, in the order they are published.</summary>
    private static string[] Fields(string block) => block switch
    {
        "clock" => ["date", "time", "daylight", "elapsedDays"],
        "world" => ["place", "name", "kind", "x", "y", "z", "yaw", "visited", "places"],
        "party" => ["members", "coins", "provisions", "unit", "reputation", "fame", "conditions"],
        _ => [],
    };

    [Fact]
    public void Resuming_with_nothing_saved_fails_by_name()
    {
        InMemoryPersistenceService persistence = new();
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(
            persistence,
            [ProductTestContext.Bundle(BuiltInBundles.Default, "world", "scenario"), .. World(), .. Scenario()]);

        SessionSaveException refused = Assert.Throws<SessionSaveException>(
            () => MightAndMagic7Ruleset.Instance.ResumeSession(Context(context, ui)));

        Assert.Contains($"No session is saved in slot '{Slot}'", refused.Message, StringComparison.Ordinal);
        Assert.Empty(refused.Problems);
    }

    [Fact]
    public void A_save_the_engine_cannot_decode_fails_by_name_at_its_slot()
    {
        InMemoryPersistenceService persistence = new();
        persistence.Seed(Scope, Slot, Encoding.UTF8.GetBytes("{\"party\": {\"nextMemberValue\": 1"));
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(
            persistence,
            [ProductTestContext.Bundle(BuiltInBundles.Default, "world", "scenario"), .. World(), .. Scenario()]);

        SessionSaveException refused = Assert.Throws<SessionSaveException>(
            () => MightAndMagic7Ruleset.Instance.ResumeSession(Context(context, ui)));

        Assert.Contains($"The save in slot '{Slot}' cannot be read", refused.Message, StringComparison.Ordinal);
        Assert.Single(refused.Problems);
    }

    [Fact]
    public void A_save_naming_a_place_the_world_does_not_have_names_every_problem_at_once()
    {
        InMemoryPersistenceService persistence = new();
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(
            persistence,
            [ProductTestContext.Bundle(BuiltInBundles.Default, "world", "scenario"), .. World(), .. Scenario()]);

        using (IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(Context(context, ui)))
        {
            session.Start();
            MightAndMagic7Ruleset.Instance.Save(session);
        }

        // The same document, with the party moved to a place this world does not have: the load refuses it
        // and says which place, rather than resuming a party in a world that has no such spot.
        SessionSave sound = Decode(persistence.Payload(Scope, Slot)!);
        SessionSave broken = new(
            sound.Party,
            sound.Clock,
            new WorldSave(
                new PartyPose(new PlaceId("somewhere-else"), sound.World.Pose.Pose),
                sound.World.Places));
        persistence.Seed(Scope, Slot, Encode(broken));

        SessionSaveException refused = Assert.Throws<SessionSaveException>(
            () => MightAndMagic7Ruleset.Instance.ResumeSession(Context(context, ui)));

        Assert.Contains("The save cannot be loaded:", refused.Message, StringComparison.Ordinal);
        Assert.Contains(refused.Problems, problem => problem.Contains("which the world does not have", StringComparison.Ordinal));
    }

    [Fact]
    public void A_host_with_no_engine_cannot_resume_a_session()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(
            [ProductTestContext.Bundle(BuiltInBundles.Default, "world", "scenario"), .. World(), .. Scenario()]);

        SessionSaveException refused = Assert.Throws<SessionSaveException>(
            () => MightAndMagic7Ruleset.Instance.ResumeSession(Context(context, ui, engine: false)));

        Assert.Contains("not running inside an engine", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_engine_that_cannot_open_a_store_reports_the_loss_rather_than_writing_nothing()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(
            new NoPersistenceRoot(),
            [ProductTestContext.Bundle(BuiltInBundles.Default, "world", "scenario"), .. World(), .. Scenario()]);

        // A host that selected no persistence root still plays; asking it to write reports the engine's own
        // refusal, naming what is lost, instead of a session that looks saved and is not.
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(Context(context, ui));
        session.Start();

        SessionSaveException refused = Assert.Throws<SessionSaveException>(() => MightAndMagic7Ruleset.Instance.Save(session));
        Assert.Contains("could not open the persistence store", refused.Message, StringComparison.Ordinal);
        Assert.Single(refused.Problems);
    }

    /// <summary>The ruleset session context a product composes, over the context this test staged.</summary>
    private static RulesetSessionContext Context(ProductCreateContext context, RecordingUiService ui, bool engine = true)
    {
        ContentBootstrapResult bootstrap = ContentBootstrap.Load(
            new ProductContentSource(context.Content),
            ContentLayout.Under(ProductTestContext.ContentDirectory),
            BuiltInBundles.Default);
        Assert.True(bootstrap.IsValid, string.Join("; ", bootstrap.Issues.Select(issue => issue.ToString())));
        Assert.NotNull(bootstrap.Selection);

        EngineUiProjectionChannel channel = new(
            ui,
            new UiStreamRequest(ProductIdentity.UiStream, ProductIdentity.UiContract));
        return new RulesetSessionContext(
            channel,
            new BundleSelection(bootstrap.Selection.Bundle.BundleId, bootstrap.Selection.Packs.Count),
            bootstrap.Catalog,
            Engine: engine ? context.Engine : null);
    }

    private static byte[] Encode(SessionSave save)
    {
        JsonProductStateCodec<SessionSave> codec = new(SessionSaveJson.TypeInfo);
        ArrayBufferWriter<byte> buffer = new();
        codec.Encode(save, buffer);
        return [.. buffer.WrittenSpan];
    }

    private static SessionSave Decode(byte[] payload) =>
        new JsonProductStateCodec<SessionSave>(SessionSaveJson.TypeInfo).Decode(payload);

    /// <summary>The world pack a session needs: two places, one of them populated, and no links between them.</summary>
    private static (string Path, string Text)[] World() =>
    [
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/pack.json",
            """
            {
              "schemaVersion": 1,
              "packId": "world",
              "kind": "definitions",
              "provenance": { "description": "authored for a test" },
              "documents": [ { "path": "world.json", "documentId": "world", "definitionKind": "place" } ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/world.json",
            """
            {
              "documentId": "world",
              "definitionKind": "place",
              "entries": [
                { "id": "1", "kind": "region", "name": "Home", "respawnDays": 1,
                  "entryPoints": [ { "id": "Party Start", "x": 1, "y": 2, "z": 3, "yaw": 512 } ],
                  "placements": [ { "kind": "monster", "id": "wanderer", "x": 4, "y": 5, "z": 0 } ] },
                { "id": "2", "kind": "interior", "name": "Cave", "respawnDays": 1,
                  "entryPoints": [ { "id": "Party Start", "x": 5, "y": 6, "z": 7, "yaw": 0 } ] }
              ]
            }
            """),
    ];

    /// <summary>The scenario pack: where the party starts and who is in it, portraits included.</summary>
    private static (string Path, string Text)[] Scenario() =>
    [
        ($"{ProductTestContext.ContentDirectory}/content-packs/scenario/pack.json",
            """
            {
              "schemaVersion": 1,
              "packId": "scenario",
              "kind": "scenario",
              "provenance": { "description": "authored for a test" },
              "documents": [
                { "path": "scenario.json", "documentId": "scenario", "definitionKind": "scenario-start" },
                { "path": "party.json", "documentId": "party", "definitionKind": "scenario-party" }
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/scenario/scenario.json",
            """
            { "documentId": "scenario", "definitionKind": "scenario-start",
              "entries": [ { "id": "start", "place": "1", "entryPoint": "Party Start" } ] }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/scenario/party.json",
            """
            { "documentId": "party", "definitionKind": "scenario-party",
              "entries": [ { "id": "party", "coins": 137, "food": 9, "members": [
                { "name": "Marnie", "race": "Human", "class": "Knight", "portrait": "human-woman",
                  "level": 1, "hitPoints": 40, "attributes": [ { "id": "Might", "value": 13 } ] },
                { "name": "Roderick", "race": "Human", "class": "Cleric", "portrait": "human-man",
                  "level": 1, "hitPoints": 30, "spellPoints": 12, "attributes": [ { "id": "Personality", "value": 14 } ] }
              ] } ] }
            """),
    ];

    /// <summary>
    /// An engine whose persistence service cannot open a store, which is how the real service reports a host
    /// that selected no persistence root.
    /// </summary>
    private sealed class NoPersistenceRoot : IPersistenceService
    {
        public PersistenceStore OpenStore(PersistenceOpenRequest request) =>
            throw new EngineCallException("Persistence", "OpenStore", 0);

        public PersistenceSaveReceipt Save(PersistenceSaveRequest request) => throw new NotSupportedException();

        public PersistenceDeleteReceipt Delete(PersistenceDeleteRequest request) => throw new NotSupportedException();

        public PersistenceBlob Load(PersistenceLoadRequest request) => throw new NotSupportedException();

        public PersistenceBlobInfo DescribeBlob(PersistenceBlob blob) => throw new NotSupportedException();

        public void CopyBlob(PersistenceCopyBlobRequest request) => throw new NotSupportedException();

        public ReadOnlyMemory<byte> ReadBlobBytes(PersistenceBlob blob) => throw new NotSupportedException();
    }
}
