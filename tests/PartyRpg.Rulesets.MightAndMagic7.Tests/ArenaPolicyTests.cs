using PartyRpg.Kit;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Conversation;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Quests;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.World;
using PartyRpg.Testing;
using Xunit;

namespace PartyRpg.Rulesets.MightAndMagic7.Tests;

public sealed class ArenaPolicyTests
{
    [Fact]
    public void An_actual_bout_resumes_its_creatures_and_settles_the_canonical_count_once()
    {
        using Mission mission = new();
        QuestId bout = mission.TakeBout();
        var live = mission.Live;
        Assert.Equal(2, live.World!.Population.Entities.Count(e => e.IsSummoned));
        Assert.Equal(0, live.Party!.Records.CountOf(MightAndMagic7Deeds.ArenaWins));
        Assert.False(live.Owners.Quests!.TurnIn(bout, "npc-300").IsApplied);
        SessionSave saved = MightAndMagic7Ruleset.Instance.Save(mission.Session);
        Assert.Equal(2, saved.Combat.Creatures.Count(c => c.Origin is not null));
        Assert.All(saved.Combat.Creatures.Where(c => c.Origin is not null), c => Assert.StartsWith("arena-", c.Placement.Id));

        using Mission resumed = new(mission.Persistence, resume: true);
        SessionSave restored = MightAndMagic7Ruleset.Instance.Save(resumed.Session);
        Assert.Equal(saved.Combat.Creatures.Select(c => (c.Placement, c.Kind, c.Health, c.Pose)),
            restored.Combat.Creatures.Select(c => (c.Placement, c.Kind, c.Health, c.Pose)));
        Assert.Equal(bout, Assert.Single(resumed.Live.Owners.Quests!.Instances).Quest);

        resumed.Win(bout);
        resumed.Use();
        int before = resumed.Live.Party!.Purse.Coins;
        resumed.Choose(MightAndMagic7Identities.TurnInTopicPrefix + bout.Value);
        Assert.Equal(QuestStage.TurnedIn, resumed.Live.Owners.Quests!.Instance(bout)!.Stage);
        Assert.Equal(1, resumed.Live.Party.Records.CountOf(MightAndMagic7Deeds.ArenaWins));
        Assert.Equal(before + 4000, resumed.Live.Party.Purse.Coins);
        resumed.Use(); resumed.Choose(MightAndMagic7Identities.TurnInTopicPrefix + bout.Value);
        Assert.False(resumed.Live.Owners.Quests.Last!.IsApplied);
        Assert.Equal(QuestCodes.QuestAlreadyFinished, resumed.Live.Owners.Quests.Last.Refusal!.Code);
        Assert.Equal(before + 4000, resumed.Live.Party.Purse.Coins);
        MightAndMagic7Ruleset.Instance.Save(resumed.Session);
        using Mission settled = new(mission.Persistence, resume: true);
        Assert.Equal(1, settled.Live.Party!.Records.CountOf(MightAndMagic7Deeds.ArenaWins));
        Assert.False(settled.Live.Owners.Quests!.TurnIn(bout, "npc-300").IsApplied);
        Assert.Equal(before + 4000, settled.Live.Party.Purse.Coins);
    }

    [Fact]
    public void Unrelated_deaths_do_not_count_and_leaving_retains_only_actual_bout_progress()
    {
        using Mission mission = new();
        QuestId bout = mission.TakeBout();
        PartyQuests quests = mission.Live.Owners.Quests!;
        var opponent = mission.Live.World!.Population.Entities.First(e => e.IsSummoned);
        using System.Text.Json.JsonDocument foreign = System.Text.Json.JsonDocument.Parse("""{"id":"unrelated","kind":"monster","monster":"7","x":100,"y":0,"z":0}""");
        var unrelated = PlacePopulationContent.Definition(new("monster", "unrelated"), foreign.RootElement.Clone());
        quests.ObserveDeath(new(new("1"), unrelated, "A beast"));
        Assert.All(quests.Read(bout)!.Objectives, o => Assert.Equal(0, o.Count));
        mission.DefeatOne(bout);
        Assert.Equal(1, quests.Read(bout)!.Objectives.Sum(o => o.Count));
        mission.Live.World.Party.Enter(new("2"), PlacePose.Origin);
        mission.Live.World.Populate();
        Assert.Empty(mission.Live.World.Population.Entities);
        Assert.False(quests.TurnIn(bout, "npc-300").IsApplied);
        mission.Live.World.Party.Enter(new("1"), PlacePose.Origin);
        mission.Live.World.Populate();
        mission.Use();
        mission.Choose("arena-return:" + bout.Value);
        var again = mission.Live.World.Population.Entities.Where(e => e.IsSummoned).ToArray();
        Assert.Single(again);
        Assert.DoesNotContain(again, e => e.Content == opponent.Content);
        mission.Choose("arena-return:" + bout.Value);
        Assert.Single(mission.Live.World.Population.Entities, e => e.IsSummoned);
        Assert.Equal(0, mission.Live.Party!.Records.CountOf(MightAndMagic7Deeds.ArenaWins));
    }

    [Fact]
    public void A_bout_cannot_be_accepted_outside_its_place_and_refuses_before_any_creation()
    {
        using Mission mission = new();
        mission.Use();
        string offer = Assert.Single(mission.Live.Owners.Conversations!.Offers, o => o.Id.StartsWith(MightAndMagic7Identities.QuestTopicPrefix, StringComparison.Ordinal)).Id;
        mission.Choose(offer);
        QuestId bout = Assert.Single(mission.Live.Owners.Quests!.Instances).Quest;
        mission.Live.World!.Party.Enter(new("2"), PlacePose.Origin);
        mission.Live.World.Populate();
        QuestResult result = mission.Live.Owners.Quests.Accept(bout);
        Assert.False(result.IsApplied);
        Assert.Equal("arena-not-present", result.Refusal!.Code);
        Assert.Equal(QuestStage.Offered, mission.Live.Owners.Quests.Instance(bout)!.Stage);
        Assert.Equal(0, mission.Live.Party!.Records.CountOf(MightAndMagic7Summons.IdentityRecord));
    }

    [Fact]
    public void Five_earned_bouts_reach_the_existing_Champion_and_topic_readers()
    {
        using Mission mission = new();
        var party = mission.Live.Party!;
        party.Records.Mark(MightAndMagic7Quests.ErrandRecord("140"));
        Assert.True(mission.Live.Owners.Progression!.Promote("knight-cavalier", "npc-43").IsGranted);
        party.Records.Mark(MightAndMagic7Quests.ErrandRecord("33"));
        for (int count = 1; count <= 5; count++)
        {
            Assert.False(mission.Live.Owners.Progression.Promote("cavalier-champion", "npc-42").IsGranted);
            mission.Use(); mission.Choose("topic-777");
            Assert.Equal("Still needed.", mission.Live.World!.Interaction!.LastResult!.Message);
            QuestId bout = mission.TakeBout(); mission.Win(bout); mission.Use();
            mission.Choose(MightAndMagic7Identities.TurnInTopicPrefix + bout.Value);
            Assert.Equal(count, party.Records.CountOf(MightAndMagic7Deeds.ArenaWins));
        }
        mission.Use(); mission.Choose("topic-777");
        Assert.Equal("Earned.", mission.Live.World!.Interaction!.LastResult!.Message);
        Assert.True(mission.Live.Owners.Progression.Promote("cavalier-champion", "npc-42").IsGranted);
        Assert.Equal("Champion", party.Members[1].Profile.Class.Value);
    }

    [Fact]
    public void Saved_opponents_must_belong_to_the_taken_bout_and_agree_with_its_progress()
    {
        using Mission mission = new();
        QuestId bout = mission.TakeBout();
        SessionSave save = MightAndMagic7Ruleset.Instance.Save(mission.Session);
        var (context, _) = RulesetTestContext.Create(Content());
        ContentCatalog content = ContentCatalogLoader.Load(RulesetTestContext.Content(context),
            ContentLayout.Under(RulesetTestContext.ContentDirectory)).RequireValid();
        var quests = (MightAndMagic7Quests)mission.Live.Owners.Quests!.Rule;
        SessionSave noQuest = new(save.Party, save.Clock, save.World, journal: save.Journal,
            knowledge: save.Knowledge, maps: save.Maps, combat: save.Combat);
        SessionSaveException unknown = Assert.Throws<SessionSaveException>(() => MightAndMagic7Persistence.RequireLoadable(noQuest, content, quests));
        Assert.Equal(2, unknown.Problems.Count(p => p.Code == "save-arena-opponent-unknown"));
        QuestInstanceSave quest = Assert.Single(save.Quests.Instances);
        SessionSave falseVictory = new(save.Party, save.Clock, save.World,
            new([quest with { Progress = [new("opponent-0", 1)] }]), save.Journal, save.Knowledge, save.Maps, save.Combat);
        SessionSaveException contradiction = Assert.Throws<SessionSaveException>(() => MightAndMagic7Persistence.RequireLoadable(falseVictory, content, quests));
        Assert.Single(contradiction.Problems, p => p.Code == "save-arena-opponent-contradictory");
        MightAndMagic7Persistence.RequireLoadable(save, content, quests);
    }

    [ImportedFact("places.json")]
    public void The_imported_Arena_uses_its_existing_official_event_and_stated_monster_rows()
    {
        ContentCatalog content = ImportedContent.Load();
        Assert.Contains(MightAndMagic7MapEvents.Read(content).Events,
            e => e.Place == new PlaceId("76") && e.Id == 376 && e.Steps.Any(s => s.Op == "speak-npc" && s.Person == 300));
        QuestDefinition bout = MightAndMagic7Arena.Read(content)!.Definition(new("arena:knight-76:1:1"))!;
        Assert.NotNull(bout);
        Assert.Equal("npc-300", bout.Giver);
        Assert.Equal(10, bout.Objectives.Count);
        Assert.All(bout.Objectives, objective => Assert.Equal("76", objective.Place));
        Assert.Equal(200, bout.Rewards.Coins);
        Assert.All(content.Entries("monster").Where(x => x.Entry.GetString("aiType") == "Wimp" ||
            x.Entry.GetString("internalName").StartsWith("z", StringComparison.OrdinalIgnoreCase)),
            x => Assert.Equal(false, x.Entry.GetBoolean("arenaEligible")));
    }

    private sealed class Mission : IDisposable
    {
        private ulong _step;
        internal InMemoryPersistenceService Persistence { get; }
        internal IGameSession Session { get; }
        internal MightAndMagic7Session Live => (MightAndMagic7Session)Session;
        internal Mission(InMemoryPersistenceService? persistence = null, bool resume = false)
        {
            Persistence = persistence ?? new();
            var (context, ui) = RulesetTestContext.Create(Persistence, Content());
            var rules = RulesetTestContext.RulesetContext(context, ui, combat: true) with
            {
                Use = new UseIntentNames(Declared.UseIntent, Declared.UiActionContract),
                Conversation = new ConversationIntentNames(Declared.ConversationLeaveIntent, Declared.UiActionContract),
            };
            Session = resume ? MightAndMagic7Ruleset.Instance.ResumeSession(rules) : MightAndMagic7Ruleset.Instance.CreateSession(rules);
            Session.Start();
        }
        internal void Use()
        {
            if (Live.Owners.Conversations!.IsOpen) Action("conversation.leave", "");
            Live.World!.Party.Enter(new("1"), PlacePose.Origin);
            Session.Update(RulesetTestContext.Update(++_step, 1, RulesetTestContext.Digital(Declared.UseIntent)));
            Assert.True(Live.Owners.Conversations.IsOpen);
        }
        internal QuestId TakeBout()
        {
            Use();
            string offer = Assert.Single(Live.Owners.Conversations!.Offers, o => o.Id.StartsWith(MightAndMagic7Identities.QuestTopicPrefix, StringComparison.Ordinal)).Id;
            Choose(offer);
            QuestId bout = Live.Owners.Quests!.Instances.Last().Quest;
            Choose(MightAndMagic7Identities.AcceptTopicPrefix + bout.Value);
            Assert.Equal(QuestStage.Accepted, Live.Owners.Quests.Instance(bout)!.Stage);
            return bout;
        }
        internal void DefeatOne(QuestId bout)
        {
            Action("conversation.leave", "");
            for (int tries = 0; tries < 20 && Live.Owners.Quests!.Read(bout)!.Objectives.Sum(o => o.Count) == 0; tries++) Attack();
            Assert.Equal(1, Live.Owners.Quests!.Read(bout)!.Objectives.Sum(o => o.Count));
        }
        private void Attack()
        {
            Session.Update(RulesetTestContext.Update(++_step, 1, RulesetTestContext.Digital(Declared.AttackIntent)));
            Session.Update(RulesetTestContext.Update(++_step, 60, Admitted.Released(Declared.AttackIntent)));
        }
        internal void Win(QuestId bout)
        {
            Action("conversation.leave", "");
            for (int tries = 0; tries < 20 && Live.Owners.Quests!.Read(bout)!.Unmet.Count > 0; tries++)
                Attack();
            Assert.True(Live.Owners.Quests!.Read(bout)!.Unmet.Count == 0,
                $"Unmet: {string.Join(';', Live.Owners.Quests.Read(bout)!.Unmet)}; attack: {Live.Combat!.LastOrder}; resolution: {Live.Combat.LastResolution}");
        }
        internal void Choose(string topic) => Action("conversation.topic", topic);
        internal void Action(string action, string target) => Session.Update(RulesetTestContext.Update(++_step, 1,
            RulesetTestContext.Payload($$"""{"action":"{{action}}","target":"{{target}}","member":0}""")));
        public void Dispose() => Session.Dispose();
    }

    private static (string Path, string Text)[] Content()
    {
        string root = RulesetTestContext.ContentDirectory + "/content-packs/world/";
        string attack = MonsterRows.Combat(7, "Phys", "1d1+0");
        return SpellEffectPolicyTests.Content(places: (root + "places.json", """
            {"documentId":"places","definitionKind":"place","entries":[
              {"id":"1","kind":"interior","name":"Arena","respawnDays":0,"entryPoints":[{"id":"Party Start","x":0,"y":0,"z":0,"yaw":0}],"placements":[
                {"id":"fixture-376","kind":"fixture","sourceField":"events","sourceIndex":376,"eventId":376,"x":50,"y":0,"z":0,"name":"Arena official"}]},
              {"id":"2","kind":"interior","name":"Outside","respawnDays":1,"entryPoints":[{"id":"Party Start","x":0,"y":0,"z":0,"yaw":0}]}]}
            """), monster: (root + "monsters.json", $$"""
              {"documentId":"monsters","definitionKind":"monster","entries":[{"id":"7","name":"A beast","level":2,
              "hitPoints":1,"armorClass":0,"hostility":3,"recovery":10000,{{attack}}}]}
              """), extra: [
            (root + "global-events.json", """
              {"documentId":"global-events","definitionKind":"global-event","entries":[{"id":"777","event":777,"topic":true,"steps":[
                {"step":0,"op":"compare","variable":"arena-wins-knight","value":5,"target":3},
                {"step":1,"op":"status-text","textId":1,"text":"Still needed."},{"step":2,"op":"exit"},
                {"step":3,"op":"status-text","textId":2,"text":"Earned."},{"step":4,"op":"exit"}]}]}
              """,
                """{"path":"global-events.json","documentId":"global-events","definitionKind":"global-event"}"""),
            (root + "events.json", """{"documentId":"events","definitionKind":"place-event","entries":[{"id":"1.376","place":"1","event":376,"raised":true,"steps":[{"step":0,"op":"speak-npc","person":300},{"step":1,"op":"exit"}]}]}""",
                """{"path":"events.json","documentId":"events","definitionKind":"place-event"}"""),
            (root + "people.json", """{"documentId":"people","definitionKind":"person","entries":[{"id":"npc-300","npcId":300,"name":"Arena Master","portrait":"330","topics":[{"id":"topic-777","label":"Are our wins enough?","event":777}]}]}""",
                """{"path":"people.json","documentId":"people","definitionKind":"person"}"""),
            (root + "arena.json", """
              {"documentId":"arena","definitionKind":"arena-bout","entries":[{"id":"trial","place":"1","person":"npc-300","goldPerLevel":200,
              "challengers":[{"monster":"7","x":100,"y":0,"z":0},{"monster":"7","x":150,"y":0,"z":0}]}]}
              """,
                """{"path":"arena.json","documentId":"arena","definitionKind":"arena-bout"}""")]);
    }
}
