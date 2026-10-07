using System.Text.Json;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.World;
using PartyRpg.Testing;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Rulesets.MightAndMagic7.Tests;

public sealed class ConditionalContainerTests
{
    [Theory]
    [InlineData("{\"step\":2,\"op\":\"open-chest\",\"index\":99}", "container-record-missing")]
    [InlineData("{\"step\":2,\"op\":\"open-chest\",\"index\":1}", "container-multiple-records")]
    [InlineData("{\"step\":2,\"op\":\"unimplemented\"}", "fixture-step-not-interpreted")]
    public void A_refused_surface_program_does_not_settle_its_earlier_writes_or_touch_custody(string finalStep, string code)
    {
        var catalog = ContentCatalogLoader.Load(new InMemoryContentSource()
            .Add("packs/events/pack.json", """
                {"schemaVersion":1,"packId":"events","kind":"definitions","provenance":{"description":"test"},
                 "documents":[{"path":"events.json","documentId":"events","definitionKind":"place-event"}]}
                """)
            .Add("packs/events/events.json", $$"""
                {"documentId":"events","definitionKind":"place-event","entries":[
                 {"id":"31.176","place":"31","event":176,"steps":[
                  {"step":0,"op":"add","variable":"quest-bit","value":148},
                  {"step":1,"op":"open-chest","index":0},{{finalStep}}]},
                 {"id":"31.177","place":"31","event":177,"steps":[{"step":0,"op":"open-chest","index":0}]}]}
                """), new ContentLayout("packs", "imports", "bundles")).RequireValid();
        PlacementDefinition Placement(string kind, int index, string fields) => new(new(kind, $"{kind}-{index}"), "test", index,
            PlacePose.Origin, new ContentEntry($"{kind}-{index}", JsonDocument.Parse("{" + fields + "}").RootElement));
        var surface = Placement("container-surface", 176, "\"eventId\":176");
        var first = Placement("container", 0, "\"sourceIndex\":0,\"flags\":0");
        var second = Placement("container", 1, "\"sourceIndex\":1,\"flags\":0");
        using var party = PromoterTopicTests.Party(("Candidate", "Wizard", 2));
        Assert.True(party.AcquireItem(new("615"), 1).Admitted);
        var before = party.Inventory.Items.Select(item => item.Id).ToArray();
        var rule = new MightAndMagic7Interaction(fixtures: new MightAndMagic7Fixtures(MightAndMagic7MapEvents.Read(catalog)));
        var target = rule.Describe(new(new("31"), surface, ""))!;
        var selected = rule.SelectUse(new(new("31"), surface, target, party, null) { PlaceTargets = [surface, first, second] })!;
        Assert.Equal(code, selected.Refusal?.Code);
        Assert.Null(selected.AfterApply);
        Assert.False(party.Records.Has("errand:148"));
        Assert.Equal(before, party.Inventory.Items.Select(item => item.Id));
    }

    [ImportedFact("place-events.json")]
    public void Actual_conditional_surfaces_keep_each_records_trap_and_loot_through_revisit_and_native_resume()
    {
        var catalog = ImportedContent.Playable();
        catalog = catalog.Selected(catalog.Packs.Where(pack => pack.PackId != "mm7-media").ToArray());
        foreach (var (placeId, quest, proof) in new[] { ("31", "48", "148"), ("51", "99", "231") })
        {
            InMemoryPersistenceService persistence = new();
            RecordingUiService ui = new();
            using EngineUiProjectionChannel projection = new(ui, new(Declared.UiStream, Declared.UiContract));
            var (content, _) = RecordingEngineService<IContentService>.Create((method, _) =>
                method.Name == nameof(IContentService.OpenReference) ? throw new InvalidDataException("No rendered mesh in this semantic test.") : (false, null));
            var (cameras, _) = RecordingEngineService<ICameraViewService>.Create();
            FakeEngineContext engine = new(ui, persistence, content: content, graphics: new RecordingGraphicsService(), cameras: cameras);
            RulesetSessionContext context = new(projection, new BundleSelection("conditional-container-test", catalog.Packs.Count), catalog, engine,
                Creation: new CreationIntentNames(Declared.CreationAdvanceIntent, Declared.CreationAcceptIntent, Declared.UiActionContract), OwnProjection: false);
            using var session = (MightAndMagic7Session)MightAndMagic7Ruleset.Instance.CreateSession(context);
            session.Start();
            session.Update(RulesetTestContext.Update(1, 1, RulesetTestContext.Payload("""{"action":"creation.apply-default"}""")));
            session.Update(RulesetTestContext.Update(2, 1, RulesetTestContext.Payload("""{"action":"creation.accept"}""")));
            var place = new PlaceId(placeId);
            var party = session.Party!;
            var world = session.World!;
            world.ArriveAt(place, PlacePose.Origin);
            world.Populate();
            var placements = world.Population.PlacementsOf(place);
            var surface = Assert.Single(placements.Where(p => p.Content.Kind == "container-surface" && p.Source.GetInt32("eventId") == 176));
            var records = placements.Where(p => p.Content.Kind == "container" && p.SourceIndex is 0 or 1).OrderBy(p => p.SourceIndex).ToArray();
            Assert.Equal(2, records.Length);
            var rule = new MightAndMagic7Interaction(fixtures: new MightAndMagic7Fixtures(MightAndMagic7MapEvents.Read(catalog)));
            Assert.All(records, record => Assert.Null(rule.Describe(new(place, record, ""))));
            InteractionResult Use(SessionWorld current)
            {
                current.ArriveAt(place, surface.Pose with { X = surface.Pose.X - 96, Z = surface.Pose.Z - 100, Yaw = 0, Pitch = 0 });
                current.Interaction!.Update();
                Assert.Equal(surface.Content, current.Interaction.FocusedTarget!.Placement.Content);
                return current.Interaction.Use();
            }
            void Open(SessionWorld current, int selected, string expectedProof)
            {
                int priorItems = current == world ? party.Inventory.Items.Count : 0;
                for (int attempt = 0; attempt < 4 && !current.Interactions.StateOf(place, records[selected].Content).State.Contains("searched"); attempt++)
                {
                    var result = Use(current);
                    Assert.True(result.IsApplied, result.Message + " " + result.Refusal?.Message);
                    Assert.Equal(surface.Content, result.Target!.Placement.Content);
                    Assert.NotEqual("", current.Interactions.StateOf(place, records[selected].Content).State);
                    if (!result.State.Contains("searched") && current == world)
                    {
                        Assert.Equal(priorItems, party.Inventory.Items.Count);
                        Assert.False(party.Records.Has("errand:" + expectedProof));
                    }
                }
                Assert.Contains("searched", current.Interactions.StateOf(place, records[selected].Content).State);
            }
            Open(world, 1, proof);
            Assert.Equal("", world.Interactions.StateOf(place, records[0].Content).State);
            Assert.False(party.Records.Has("errand:" + proof));
            Assert.Equal(0, party.Inventory.TotalOf(new("615")));
            int itemsBefore = party.Inventory.Items.Count;
            int coinsBefore = party.Purse.Coins;
            Assert.False(Use(world).IsApplied);
            Assert.Equal(itemsBefore, party.Inventory.Items.Count);
            Assert.Equal(coinsBefore, party.Purse.Coins);
            MightAndMagic7Ruleset.Instance.Save(session);
            using var resumed = (MightAndMagic7Session)MightAndMagic7Ruleset.Instance.ResumeSession(context);
            resumed.Start();
            Assert.Contains("searched", resumed.World!.Interactions.StateOf(place, records[1].Content).State);
            Assert.Equal("", resumed.World.Interactions.StateOf(place, records[0].Content).State);
            resumed.World.ArriveAt(new("14"), PlacePose.Origin);
            Assert.False(Use(resumed.World).IsApplied);
            resumed.Party!.Records.Mark("errand:" + quest);
            Open(resumed.World, 0, proof);
            Assert.True(resumed.Party.Records.Has("errand:" + proof));
            if (placeId == "31") Assert.Equal(4, resumed.Party.Inventory.TotalOf(new("615")));
            var after = resumed.Party.Inventory.Items.Select(item => item.Id).ToArray();
            Assert.False(Use(resumed.World).IsApplied);
            Assert.Equal(after, resumed.Party.Inventory.Items.Select(item => item.Id));
            MightAndMagic7Ruleset.Instance.Save(resumed);
            using var twice = (MightAndMagic7Session)MightAndMagic7Ruleset.Instance.ResumeSession(context);
            twice.Start();
            Assert.False(Use(twice.World!).IsApplied);
            twice.Party!.Records.Remove("errand:" + quest);
            Assert.False(Use(twice.World!).IsApplied);
            Assert.Equal(after, twice.Party.Inventory.Items.Select(item => item.Id));
        }
    }
}
