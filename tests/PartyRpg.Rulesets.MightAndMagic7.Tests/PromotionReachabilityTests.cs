using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.World;
using Rusty.Engine;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Conversation;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Progression;
using PartyRpg.Kit.Promotion;
using PartyRpg.Testing;
using Xunit;

namespace PartyRpg.Rulesets.MightAndMagic7.Tests;

public sealed class PromotionReachabilityTests
{
    // Explicit prerequisite fixtures, not an expedition or an item-acquisition claim.
    private static readonly Dictionary<string, (int Event, string Proof)> Cases = new()
    {
        ["Rogue"] = (45, "item:624"),
        ["Spy"] = (47, "quest:20"),
        ["Assassin"] = (50, "item:540"),
        ["Crusader"] = (52, "quest:23"),
        ["Hero"] = (54, "follower:54"),
        ["Villain"] = (57, "follower:54"),
        ["Initiate"] = (60, ""),
        ["Master"] = (62, "quest:243"),
        ["Ninja"] = (64, "quest:242"),
        ["Master Archer"] = (66, "item:542"),
        ["Warrior Mage"] = (68, "quest:58"),
        ["Sniper"] = (70, "item:542"),
        ["Champion"] = (72, "award:arena-wins=5"),
        ["Cavalier"] = (74, "quest:140"),
        ["Black Knight"] = (76, "quest:60"),
        ["Ranger Lord"] = (78, "quest:41"),
        ["Bounty Hunter"] = (82, "award:bounties=10000"),
        ["Hunter"] = (83, "quest:37"),
        ["Priest of the Light"] = (87, "quest:62"),
        ["Priest"] = (89, "item:683"),
        ["Priest of the Dark"] = (91, "quest:63"),
        ["Wizard"] = (93, "quest:73"),
        ["Arch Mage"] = (95, "item:487"),
        ["Lich"] = (97, "item:615"),
        ["Great Druid"] = (99, "quest:50"),
        ["Arch Druid"] = (101, "quest:65"),
        ["Warlock"] = (103, "item:647"),
    };

    [ImportedFact("global-events.json")]
    public void Every_authored_rank_executes_its_imported_quest_completion_and_cannot_switch_to_its_sibling()
    {
        ContentCatalog catalog = ImportedContent.Playable();
        var promotions = MightAndMagic7Promotions.Read(catalog);
        var events = MightAndMagic7MapEvents.Read(catalog);
        Assert.Equal(27, promotions.RankCount);
        Assert.Equal(36, promotions.Ladder.Ranks.SelectMany(r => new[] { r.From.Value, r.To.Value }).Distinct().Count());
        foreach (PromotionRank rank in promotions.Ladder.Ranks)
        {
            using PartyEntity party = PromoterTopicTests.Party(Enumerable.Range(0, 4).Select(i => ($"Candidate {i}", rank.From.Value, rank.Rank - 1)).ToArray());
            var progression = new PartyProgression(MightAndMagic7Progression.Instance, party, promotions: promotions);
            MightAndMagic7Fixtures? fixtures = null;
            var conversation = MightAndMagic7Conversation.Read(catalog, MightAndMagic7Services.Read(catalog), promotions, events: () => fixtures, party: () => party)!;
            fixtures = new MightAndMagic7Fixtures(events, random: new KeyedTestRandom(), progression: () => progression,
                people: conversation.PersonOf, topics: conversation.SpokenTopic, greetings: conversation.HasGreeting,
                followers: conversation.Followers, news: conversation.News);
            var rule = new MightAndMagic7Interaction(fixtures: fixtures);
            var (eventId, proof) = Cases[rank.To.Value];
            if (eventId is not (60 or 83))
            {
                var absent = PromoterTopicTests.Answer(rule, party, $"topic-{eventId}");
                Assert.Equal(string.Empty, absent.Residue);
                Assert.All(party.Members, m => Assert.Equal(rank.From, m.Profile.Class));
            }
            Seed(party, proof);
            var outcome = PromoterTopicTests.Answer(rule, party, $"topic-{eventId}");
            Assert.True(string.IsNullOrEmpty(outcome.Residue), $"{rank.To}: {outcome.Residue}");
            Assert.All(party.Members, m => Assert.Equal(rank.To, m.Profile.Class));
            Assert.All(party.Members, m => Assert.Equal(rank.Rank, m.Progression.ClassRank));
            foreach (var sibling in promotions.Ladder.From(rank.From).Where(r => r.To != rank.To))
            {
                var other = Cases[sibling.To.Value];
                Seed(party, other.Proof);
                var attempted = PromoterTopicTests.Answer(rule, party, $"topic-{other.Event}");
                Assert.True(string.IsNullOrEmpty(attempted.Residue), attempted.Residue);
                Assert.All(party.Members, m => Assert.Equal(rank.To, m.Profile.Class));
            }
        }
    }

    [ImportedFact("global-events.json")]
    public void The_golem_assembles_through_the_companion_handoff_and_event_notes_read_the_same_party_records()
    {
        var catalog = ImportedContent.Playable();
        var (context, ui) = RulesetTestContext.Create();
        using var session = MightAndMagic7Ruleset.Instance.CreateSession(new RulesetSessionContext(
            new EngineUiProjectionChannel(ui, new UiStreamRequest(Declared.UiStream, Declared.UiContract)),
            new BundleSelection("promotion-test", catalog.Packs.Count), catalog) with
        {
            Content = catalog,
            Creation = new CreationIntentNames(Declared.CreationAdvanceIntent, Declared.CreationAcceptIntent, Declared.UiActionContract),
            Conversation = new ConversationIntentNames(Declared.ConversationLeaveIntent, Declared.UiActionContract),
        });
        session.Start();
        session.Update(RulesetTestContext.Update(1, 1, RulesetTestContext.Payload("""{"action":"creation.apply-default"}""")));
        session.Update(RulesetTestContext.Update(2, 1, RulesetTestContext.Payload("""{"action":"creation.accept"}""")));
        var live = (MightAndMagic7Session)session;
        var party = live.Party!;
        var conversations = live.Owners.Conversations!;
        var conversation = (MightAndMagic7Conversation)live.Owners.Rules.Conversation!;
        ulong frame = 2;
        void Action(string action, string target) => session.Update(RulesetTestContext.Update(++frame, 1,
            RulesetTestContext.Payload($$"""{"action":"{{action}}","target":"{{target}}"}""")));
        conversations.Open(live.World!.Party.Place, PromoterTopicTests.Standing("npc-48"),
            new ConversationSubject("test-thomas", [conversation.PersonOf("npc-48")!]));
        Action("conversation.topic", "topic-92");
        Assert.NotNull(party.Followers.Find(new("npc-56")));
        Assert.Contains(live.Inspect().Quests.Journal, q => q.Quest == "45" && q.Note.Contains("golem", StringComparison.OrdinalIgnoreCase));
        foreach (int part in new[] { 639, 641, 642, 643, 644, 645 }) Assert.True(party.AcquireItem(new ItemDefinitionId(part.ToString())).Admitted);
        Action("conversation.leave", "");
        for (int part = 0; part < 7; part++)
        {
            Action("conversation.follower", "npc-56");
            Assert.Null(conversations.Placement);
            Assert.Contains(conversations.OnOffer, o => o.Id == "topic-104");
            Action("conversation.topic", "topic-104");
            Assert.Equal(string.Empty, live.World.LastInteraction!.Residue);
            Action("conversation.leave", "");
        }
        Assert.True(party.Records.Has(MightAndMagic7Quests.ErrandRecord("74")));
        Action("conversation.follower", "npc-56");
        Assert.DoesNotContain(conversations.OnOffer, o => o.Id == "topic-104");
        Assert.Null(conversations.Placement);
        party.Records.Remove(MightAndMagic7Quests.ErrandRecord("45"));
        Assert.DoesNotContain(live.Inspect().Quests.Journal, q => q.Quest == "45");
        Action("conversation.leave", "");
        var mansion = new PlaceId("19");
        live.World.ArriveAt(mansion, PlacePose.Origin);
        session.Update(RulesetTestContext.Update(++frame, 1));
        Assert.False(party.Records.Has(MightAndMagic7Quests.ErrandRecord("140")));
        Assert.False(live.World.LastInteraction!.ShowFeedback);
        // This UI-only composition has no Engine random service and thus no generated encounters.
        // Supply real content-expanded actor readings to check the source program's kill predicate.
        var events = MightAndMagic7MapEvents.Read(catalog);
        using var population = new PlacePopulation(live.World.Graph, live.World.Places,
            expansion: MightAndMagic7Spawns.Compose(catalog, new KeyedTestRandom()));
        var actors = population.PlacementsOf(mansion).Where(MightAndMagic7Fixtures.IsActor).ToArray();
        Assert.NotEmpty(actors);
        bool dead = false;
        var hooks = new MightAndMagic7Fixtures(events, actors: _ => actors.Select(p => new PlaceActor(p, dead)).ToArray());
        var hookRule = new MightAndMagic7Interaction(fixtures: hooks);
        string departure = Assert.Single(hooks.Departures(mansion));
        var hookTarget = hookRule.Describe(new(mansion, null, "") { Raised = departure })!;
        var hookContext = new InteractionContext(mansion, null, hookTarget, party, live.Owners.Clock) { Raised = departure };
        Assert.True(hookRule.Apply(hookTarget, hookContext).IsApplied);
        Assert.False(party.Records.Has(MightAndMagic7Quests.ErrandRecord("140")));
        dead = true;
        Assert.True(hookRule.Apply(hookTarget, hookContext).IsApplied);
        Assert.True(party.Records.Has(MightAndMagic7Quests.ErrandRecord("140")));
        party.Records.Remove(MightAndMagic7Quests.ErrandRecord("140"));
        var exit = live.World.Graph.Transitions.First(t => t.From == mansion && t.To != mansion && !t.IsFare);
        Assert.True(live.World.Travel(exit, TransitionKind.Entrance).Arrived);
        Assert.True(party.Records.Has(MightAndMagic7Quests.ErrandRecord("140"))); // Admitted departure dispatches the hook.

        var tomb = new PlaceId("73");
        live.World.ArriveAt(tomb, PlacePose.Origin);
        session.Update(RulesetTestContext.Update(++frame, 1));
        Assert.False(party.Records.Has(MightAndMagic7Quests.ErrandRecord("242")));
        var program = events.Events.Single(e => e.Place == tomb && e.Id == 181);
        int chest = program.Steps.Single(s => s.Op == "open-chest").Index;
        var placement = live.World.Population.PlacementsOf(tomb).Single(p => p.Content.Kind == "container" && p.Source.GetInt32("sourceIndex") == chest);
        var chestHooks = new MightAndMagic7Fixtures(events);
        var rule = new MightAndMagic7Interaction(fixtures: chestHooks, loot: MightAndMagic7Loot.Compose(catalog, new KeyedTestRandom()));
        var target = rule.Describe(new(tomb, placement, "disarmed"))!;
        Assert.Equal(InteractionVerb.Search, target.Verb);
        foreach (var verb in new[] { InteractionVerb.Unlock, InteractionVerb.Disarm })
        {
            var other = target with { Verb = verb };
            chestHooks.AfterSearch(other, new(tomb, placement, other, party, live.Owners.Clock), InteractionOutcome.Applied("disarmed", "Prepared."));
            Assert.False(party.Records.Has(MightAndMagic7Quests.ErrandRecord("242")));
        }
        var outcome = rule.Apply(target, new(tomb, placement, target, party, live.Owners.Clock));
        Assert.True(outcome.IsApplied, outcome.Refusal?.Message);
        Assert.True(party.Records.Has(MightAndMagic7Quests.ErrandRecord("242")));

    }

    [ImportedFact("global-events.json")]
    public void Second_rank_offers_require_their_story_path_and_remote_first_ranks_require_their_quest()
    {
        var catalog = ImportedContent.Playable();
        var promotions = MightAndMagic7Promotions.Read(catalog);
        var events = MightAndMagic7MapEvents.Read(catalog);
        foreach (var (_, _, row) in catalog.Entries("promotion"))
        {
            var rank = promotions.Ladder.Ranks.Single(r => r.Id == row.Id);
            if (rank.Choice.Length == 0) continue;
            var first = promotions.Ladder.Ranks.Single(r => r.To == rank.From);
            using var party = PromoterTopicTests.Party(Enumerable.Range(0, 4).Select(i => ($"Candidate {i}", first.From.Value, 1)).ToArray());
            var progression = new PartyProgression(MightAndMagic7Progression.Instance, party, promotions: promotions);
            var conversation = MightAndMagic7Conversation.Read(catalog, MightAndMagic7Services.Read(catalog), promotions)!;
            var fixtures = new MightAndMagic7Fixtures(events, random: new KeyedTestRandom(), progression: () => progression, topics: conversation.SpokenTopic, people: conversation.PersonOf, greetings: conversation.HasGreeting, followers: conversation.Followers, news: conversation.News);
            var rule = new MightAndMagic7Interaction(fixtures: fixtures);
            var prerequisite = Cases[first.To.Value];
            Seed(party, prerequisite.Proof);
            var completed = PromoterTopicTests.Answer(rule, party, $"topic-{prerequisite.Event}");
            Assert.Equal(string.Empty, completed.Residue);
            Assert.All(party.Members, m => Assert.Equal(rank.From, m.Profile.Class));
            int offer = Cases[rank.To.Value].Event - 1;
            string quest = MightAndMagic7Quests.ErrandRecord(row.GetString("quest"));
            PromoterTopicTests.Answer(rule, party, $"topic-{offer}");
            Assert.False(party.Records.Has(quest), rank.To.Value);
            string otherPath = MightAndMagic7Quests.ErrandRecord(rank.Choice == "light" ? "100" : "99");
            party.Records.Mark(otherPath);
            PromoterTopicTests.Answer(rule, party, $"topic-{offer}");
            Assert.False(party.Records.Has(quest), rank.To.Value);
            party.Records.Remove(otherPath);
            party.Records.Mark(MightAndMagic7Quests.ErrandRecord(rank.Choice == "light" ? "99" : "100"));
            var accepted = PromoterTopicTests.Answer(rule, party, $"topic-{offer}");
            Assert.Equal(string.Empty, accepted.Residue);
            Assert.True(party.Records.Has(quest), rank.To.Value);
        }
        using var initiate = PromoterTopicTests.Party(("Candidate", "Monk", 1));
        var hooks = new MightAndMagic7Fixtures(events, people: id => new ConversationPerson(id, id));
        var ruleAtWater = new MightAndMagic7Interaction(fixtures: hooks);
        var graph = MightAndMagic7World.Graph(catalog);
        using var population = new PlacePopulation(graph, new PlaceStateLedger(graph, PlaceRespawnRule.FromContent()));
        var pool = population.PlacementsOf(new("68")).Single(p => p.Content.Kind == "fixture" && p.Source.GetInt32("eventId") == 376);
        var target = ruleAtWater.Describe(new(new("68"), pool, ""))!;
        var context = new InteractionContext(new("68"), pool, target, initiate, null);
        Assert.Null(ruleAtWater.Apply(target, context).Speaks);
        initiate.Records.Mark(MightAndMagic7Quests.ErrandRecord("27"));
        Assert.NotNull(ruleAtWater.Apply(target, context).Speaks);
        using var hunter = PromoterTopicTests.Party(("Candidate", "Ranger", 1));
        Assert.False(hooks.Offers(83, new("46"), null, hunter, null).IsMet);
        hunter.Records.Mark(MightAndMagic7Quests.ErrandRecord("37"));
        Assert.True(hooks.Offers(83, new("46"), null, hunter, null).IsMet);
    }

    [ImportedFact("place-events.json")]
    public void The_soul_jar_chest_uses_its_actual_face_event_and_requires_the_lich_quest()
    {
        var catalog = ImportedContent.Playable();
        var graph = MightAndMagic7World.Graph(catalog);
        using var population = new PlacePopulation(graph, new PlaceStateLedger(graph, PlaceRespawnRule.FromContent()));
        var place = new PlaceId("31");
        var chest = population.PlacementsOf(place).Single(p => p.Content.Kind == "container" && p.SourceIndex == 0);
        using var party = PromoterTopicTests.Party(("Candidate", "Wizard", 2));
        var rule = new MightAndMagic7Interaction(fixtures: new MightAndMagic7Fixtures(MightAndMagic7MapEvents.Read(catalog)));
        var target = rule.Describe(new(place, chest, ""))!;
        var context = new InteractionContext(place, chest, target, party, null);
        var absent = rule.Apply(target, context);
        Assert.False(absent.IsApplied);
        Assert.False(party.Records.Has(MightAndMagic7Quests.ErrandRecord("148")));
        party.Records.Mark(MightAndMagic7Quests.ErrandRecord("48"));
        var found = rule.Apply(target, context);
        Assert.True(found.IsApplied, found.Refusal?.Message);
        Assert.Equal(4, Assert.Single(found.Items, item => item.Definition.Value == "615").Count);
        Assert.True(party.Records.Has(MightAndMagic7Quests.ErrandRecord("148")));
        Assert.Equal(1, found.Kept["decoration-variable:23"]);
        Assert.False(found.Kept.ContainsKey("map-variable:23"));
        var saveJudge = new MightAndMagic7Fixtures(MightAndMagic7MapEvents.Read(catalog));
        Assert.Null(saveJudge.Judge(place, "decoration-variable:23", 1, 0));
        Assert.NotNull(saveJudge.Judge(place, "decoration-variable:25", 1, 0));
    }

    private static void Seed(PartyEntity party, string proof)
    {
        if (proof.StartsWith("quest:")) party.Records.Mark(MightAndMagic7Quests.ErrandRecord(proof[6..]));
        else if (proof.StartsWith("item:")) Assert.True(party.AcquireItem(new(proof[5..]), 4).Admitted);
        else if (proof.StartsWith("follower:")) party.Followers.Join(new("npc-" + proof[9..]), FollowerKind.Story);
        else if (proof.StartsWith("award:"))
        {
            var parts = proof.Split('='); party.Records.Set(parts[0], int.Parse(parts[1]));
        }
    }
}
