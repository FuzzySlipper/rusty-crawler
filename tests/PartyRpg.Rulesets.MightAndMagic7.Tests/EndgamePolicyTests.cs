using PartyRpg.Kit.Content;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.World;
using Rusty.Engine;
using PartyRpg.Testing;
using Xunit;

namespace PartyRpg.Rulesets.MightAndMagic7.Tests;

public sealed class EndgamePolicyTests
{
    [ImportedFact("global-events.json")]
    public void Both_real_final_quests_require_their_path_and_item_then_save_their_distinct_ending()
    {
        ContentCatalog catalog = ImportedContent.Playable();
        // This is a session/save regression, without an image-serving host.
        catalog = catalog.Selected(catalog.Packs.Where(pack => pack.PackId != "mm7-media").ToArray());
        foreach (var (id, path, offer, finish, quest) in new[] { ("light", 99, 169, 170, 130), ("dark", 100, 171, 172, 131) })
        {
            InMemoryPersistenceService persistence = new();
            RecordingUiService ui = new();
            using EngineUiProjectionChannel projection = new(ui, new(Declared.UiStream, Declared.UiContract));
            var (content, _) = RecordingEngineService<IContentService>.Create((method, _) =>
                method.Name == nameof(IContentService.OpenReference) ? throw new InvalidDataException("No rendered mesh in this semantic test.") : (false, null));
            var (cameras, _) = RecordingEngineService<ICameraViewService>.Create();
            FakeEngineContext engine = new(ui, persistence, content: content,
                graphics: new RecordingGraphicsService(), cameras: cameras);
            RulesetSessionContext context = new(projection,
                new BundleSelection("endgame-test", catalog.Packs.Count), catalog, engine,
                Creation: new CreationIntentNames(Declared.CreationAdvanceIntent, Declared.CreationAcceptIntent, Declared.UiActionContract), OwnProjection: false);
            using var session = (MightAndMagic7Session)MightAndMagic7Ruleset.Instance.CreateSession(context);
            session.Start();
            session.Update(RulesetTestContext.Update(1, 1, RulesetTestContext.Payload("""{"action":"creation.apply-default"}""")));
            session.Update(RulesetTestContext.Update(2, 1, RulesetTestContext.Payload("""{"action":"creation.accept"}""")));
            var party = session.Party!;
            var world = session.World!;
            Assert.False(session.Inspect().Completion.Completed);
            PartyRpg.Kit.Interaction.InteractionResult UseFixture(string id)
            {
                var placement = world.Population.PlacementsOf(new PlaceId("47")).Single(p => p.Content.Id == id);
                world.ArriveAt(new PlaceId("47"), placement.Pose with { X = placement.Pose.X - 96, Z = placement.Pose.Z - 100, Yaw = 0, Pitch = 0 });
                world.Interaction!.Update();
                return world.Interaction.Use();
            }
            party.Records.Mark("errand:120"); // prior arc complete, not a whole-campaign playthrough
            world.AnswerRaised($"topic-{offer}");
            Assert.True(party.Records.Has($"errand:{quest}"));
            world.AnswerRaised($"topic-{finish}");
            Assert.False(session.Inspect().Completion.Completed); // cannot deliver an absent overthruster
            // Exercise the real Lincoln device: it gives nothing without power, then yields the quest item once.
            world.ArriveAt(new PlaceId("47"), PlacePose.Origin);
            world.Populate();
            UseFixture("fixture-376");
            Assert.Equal(0, party.Inventory.TotalOf(new("605")));
            var power = UseFixture("fixture-475");
            Assert.True(power.IsApplied, power.Message);
            var device = UseFixture("fixture-376");
            Assert.True(device.IsApplied, device.Message);
            Assert.Equal(1, party.Inventory.TotalOf(new("605")));
            UseFixture("fixture-376");
            Assert.Equal(1, party.Inventory.TotalOf(new("605")));
            party.Records.Mark($"errand:{(path == 99 ? 100 : 99)}");
            world.AnswerRaised($"topic-{finish}");
            Assert.False(session.Inspect().Completion.Completed);
            Assert.Equal(1, party.Inventory.TotalOf(new("605"))); // refusal discards all pending effects
            party.Records.Remove($"errand:{(path == 99 ? 100 : 99)}");
            party.Records.Mark($"errand:{path}");
            foreach (string required in new[] { "errand:120", $"errand:{quest}" })
            {
                party.Records.Remove(required);
                var refused = world.AnswerRaised($"topic-{finish}")!;
                Assert.Contains("has not completed the arc", refused.Residue);
                Assert.False(session.Inspect().Completion.Completed);
                Assert.Equal(1, party.Inventory.TotalOf(new("605")));
                party.Records.Mark(required);
            }
            var result = world.AnswerRaised($"topic-{finish}")!;
            Assert.True(result.IsApplied, result.Message);
            Assert.Empty(result.Residue);
            CompletionSnapshot ending = session.Inspect().Completion;
            Assert.Equal(id, ending.Id);
            Assert.True(ending.Continues);
            Assert.Equal(0, party.Inventory.TotalOf(new("605")));
            Assert.False(party.Records.Has($"errand:{quest}"));
            Assert.True(party.Records.Has($"ending:{id}"));
            world.AnswerRaised($"topic-{finish}");
            Assert.Equal(ending, session.Inspect().Completion);
            MightAndMagic7Ruleset.Instance.Save(session);
            using var resumed = (MightAndMagic7Session)MightAndMagic7Ruleset.Instance.ResumeSession(context);
            resumed.Start();
            Assert.Equal(ending, resumed.Inspect().Completion);
            Assert.DoesNotContain(resumed.Inspect().Quests.Journal, note => note.Quest == quest.ToString());
            Assert.Equal(session.Inspect().Party.Coins, resumed.Inspect().Party.Coins);
        }
    }
}
