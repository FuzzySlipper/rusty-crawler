using System.Globalization;
using System.Text.Json;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Conversation;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Progression;
using PartyRpg.Kit.Promotion;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using PartyRpg.Testing;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Rulesets.MightAndMagic7.Tests;

/// <summary>
/// A promoter's rank is their topic: the world's own program answers it, through the ladder's rank and the one writer
/// of ranks, and the ladder does not offer it a second time.
/// </summary>
public sealed class PromoterTopicTests
{
    [Fact]
    public void A_promoters_rank_is_offered_once_as_their_topic_says_its_own_words_and_rises_through_the_one_writer()
    {
        (ProductCreateContext context, RecordingUiService ui) = RulesetTestContext.Create(Promoter());
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            RulesetTestContext.RulesetContext(context, ui) with
            {
                Use = new UseIntentNames(Declared.UseIntent, Declared.UiActionContract),
                Conversation = new ConversationIntentNames(Declared.ConversationLeaveIntent, Declared.UiActionContract),
            });
        session.Start();
        MightAndMagic7Session played = (MightAndMagic7Session)session;
        PartyEntity party = played.Party!;
        PartyProgression progression = played.Owners.Progression!;
        ulong frame = 0;

        // The rank is offered once, as the promoter's topic: the ladder names the same person as the giver of the
        // Rogue rank, and it does not offer it beside the topic.
        session.Update(RulesetTestContext.Update(++frame, 1));
        session.Update(RulesetTestContext.Update(++frame, 1, RulesetTestContext.Digital(Declared.UseIntent)));
        Assert.Equal(["topic-44"], RankTopics(ui));

        // The first topic sets the errand and turns the slot to the turn-in.
        session.Update(RulesetTestContext.Update(++frame, 1, RulesetTestContext.ChooseTopic("topic-44")));
        Assert.Contains("Bring me the vase", played.World!.Interaction!.LastResult!.Message, StringComparison.Ordinal);
        Assert.True(party.Records.Has(MightAndMagic7Quests.ErrandRecord("18")));
        session.Update(RulesetTestContext.Update(++frame, 1, RulesetTestContext.Digital(Declared.ConversationLeaveIntent)));

        // The turn-in is the one offer now; without the vase the program says so and nobody rises.
        session.Update(RulesetTestContext.Update(++frame, 1, RulesetTestContext.Digital(Declared.UseIntent)));
        Assert.Equal(["topic-45"], RankTopics(ui));
        session.Update(RulesetTestContext.Update(++frame, 1, RulesetTestContext.ChooseTopic("topic-45")));
        Assert.Contains("No vase, no title", played.World.Interaction.LastResult!.Message, StringComparison.Ordinal);
        Assert.Equal("Thief", party.Members[0].Profile.Class.Value);
        Assert.Null(progression.LastPromotion);
        session.Update(RulesetTestContext.Update(++frame, 1, RulesetTestContext.Digital(Declared.ConversationLeaveIntent)));

        // With the vase, the program's own words are what the promoter says, and the thief rises through the
        // progression owner — class and rank together, and the rank's record — while the knight beside them is
        // given the program's lesser award instead.
        Assert.True(party.AcquireItem(new ItemDefinitionId("624")).Admitted);
        session.Update(RulesetTestContext.Update(++frame, 1, RulesetTestContext.Digital(Declared.UseIntent)));
        session.Update(RulesetTestContext.Update(++frame, 1, RulesetTestContext.ChooseTopic("topic-45")));
        InteractionResult said = played.World.Interaction.LastResult!;
        Assert.Contains("Well taken", said.Message, StringComparison.Ordinal);
        Assert.Equal(string.Empty, said.Residue);
        PromotionResult promoted = progression.LastPromotion!;
        Assert.Equal("thief-rogue", promoted.Promotion);
        Assert.Equal("Lasse", Assert.Single(promoted.Granted).Name);
        Assert.Equal("Rogue", party.Members[0].Profile.Class.Value);
        Assert.Equal(2, party.Members[0].Progression.ClassRank);
        Assert.Equal("Knight", party.Members[1].Profile.Class.Value);
        Assert.True(party.Records.Has("promotion:thief-rogue"));
        Assert.True(party.Records.Has(MightAndMagic7Fixtures.AwardRecord(10)));
        Assert.True(party.Records.Has(MightAndMagic7Fixtures.AwardRecord(11)));
        Assert.Equal(0, party.Inventory.TotalOf(new ItemDefinitionId("624")));
        Assert.Equal("granted", ProjectedNode.Of(ui.Latest().Value).Field("promotion").Field("outcome").AsString());

        // The slot is spent, so the rank is offered nowhere again and cannot be granted twice.
        session.Update(RulesetTestContext.Update(++frame, 1, RulesetTestContext.Digital(Declared.ConversationLeaveIntent)));
        session.Update(RulesetTestContext.Update(++frame, 1, RulesetTestContext.Digital(Declared.UseIntent)));
        Assert.Empty(RankTopics(ui));
    }

    [Fact]
    public void A_class_step_the_ladder_states_no_rank_for_or_the_member_cannot_take_changes_nothing()
    {
        ContentCatalog catalog = Catalog(
            """
            { "id": "1", "event": 1, "topic": true, "steps": [
              { "step": 0, "op": "for-party-member", "who": "member", "member": 0 },
              { "step": 1, "op": "set", "variable": "class", "value": 7, "which": "Assassin" },
              { "step": 2, "op": "add", "variable": "gold", "value": 10 } ] }
            """,
            """
            { "id": "2", "event": 2, "topic": true, "steps": [
              { "step": 0, "op": "for-party-member", "who": "member", "member": 0 },
              { "step": 1, "op": "compare", "variable": "class", "value": 4, "which": "Thief", "target": 3 },
              { "step": 2, "op": "exit" },
              { "step": 3, "op": "set", "variable": "class", "value": 5, "which": "Rogue" } ] }
            """);
        MightAndMagic7MapEvents events = MightAndMagic7MapEvents.Read(catalog);
        using PartyEntity party = Party(("Lasse", "Thief", 1));
        PartyProgression progression = new(MightAndMagic7Progression.Instance, party, promotions: MightAndMagic7Promotions.Read(catalog));
        MightAndMagic7Interaction rule = new(fixtures: new MightAndMagic7Fixtures(
            events,
            progression: () => progression,
            topics: topic => new SpokenTopic(topic, "Rank", string.Empty, int.Parse(topic[6..], CultureInfo.InvariantCulture))));
        int coins = party.Purse.Coins;

        // A thief straight to an assassin skips the rank between, which the ladder states no rank for: refused by
        // name, and the coin the run would have found is not found.
        InteractionOutcome skipped = Answer(rule, party, "topic-1");
        Assert.Contains("from Thief to Assassin", skipped.Residue, StringComparison.Ordinal);
        Assert.Contains("nothing was changed", skipped.Residue, StringComparison.Ordinal);
        Assert.Equal("Thief", party.Members[0].Profile.Class.Value);
        Assert.Equal(coins, party.Purse.Coins);

        // The program's own comparison is of the class itself, and the rank goes through the progression owner.
        InteractionOutcome risen = Answer(rule, party, "topic-2");
        Assert.Equal(string.Empty, risen.Residue);
        Assert.Equal("Rogue", party.Members[0].Profile.Class.Value);
        Assert.Equal("thief-rogue", progression.LastPromotion!.Promotion);

        // Run again, the member is no longer of the class the program compares, so nothing rises twice.
        PromotionResult last = progression.LastPromotion;
        InteractionOutcome again = Answer(rule, party, "topic-2");
        Assert.Equal(string.Empty, again.Residue);
        Assert.Same(last, progression.LastPromotion);
        Assert.Equal(2, party.Members[0].Progression.ClassRank);
    }

    [ImportedFact("global-events.json")]
    public void Every_rank_is_answered_by_its_promoters_program_and_the_ladder_offers_none_of_them()
    {
        ContentCatalog catalog = ImportedContent.Load();
        MightAndMagic7MapEvents events = MightAndMagic7MapEvents.Read(catalog);
        MightAndMagic7Promotions promotions = MightAndMagic7Promotions.Read(catalog);
        MightAndMagic7Fixtures? fixtures = null;
        PartyProgression? progression = null;
        MightAndMagic7Conversation conversation = MightAndMagic7Conversation.Read(catalog, MightAndMagic7Services.Read(catalog), promotions, events: () => fixtures)!;
        fixtures = new MightAndMagic7Fixtures(
            events,
            random: new KeyedTestRandom(),
            progression: () => progression,
            people: conversation.PersonOf,
            topics: conversation.SpokenTopic,
            greetings: conversation.HasGreeting);
        MightAndMagic7Interaction rule = new(fixtures: fixtures);
        GameClock clock = TestClock.Create(scale: 1);

        // Every one of the ladder's ranks is a class a promoter's own program makes a character, so at every giver,
        // with a member standing where the rank is given, the ladder offers nothing of its own.
        Assert.Equal(27, promotions.RankCount);
        foreach (PromotionRank rank in promotions.Ladder.Ranks)
        {
            Assert.True(events.Grants(rank.To.Value), rank.ToString());
            using PartyEntity standing = Party(("Candidate", rank.From.Value, rank.Rank - 1));
            ConversationContext asking = new(new PlaceId("2"), Standing(rank.Giver), new ConversationSubject("person-0", [conversation.PersonOf(rank.Giver)!]), rank.Giver, [], standing, clock);
            Assert.DoesNotContain(conversation.Offers(asking), offer => offer.Id.StartsWith(MightAndMagic7Identities.PromotionTopicPrefix, StringComparison.Ordinal));
        }

        // At William Lasker, the rank of Rogue is his first topic, offered once; its turn-in, with the vase, says the
        // shipped words and raises the thief through the progression owner.
        using PartyEntity party = Party(("Lasse", "Thief", 1), ("Mira", "Knight", 1));
        progression = new PartyProgression(MightAndMagic7Progression.Instance, party, promotions: promotions);
        PlacementDefinition lasker = Standing("npc-15");
        ConversationContext talking = new(new PlaceId("2"), lasker, new ConversationSubject("person-0", [conversation.PersonOf("npc-15")!]), "npc-15", [], party, clock);
        Assert.Single(conversation.Offers(talking), offer => offer.Id == "topic-44");
        Assert.Equal(string.Empty, Answer(rule, party, "topic-44", lasker, clock).Residue);
        Assert.Single(conversation.Offers(talking), offer => offer.Id == "topic-45");
        Assert.True(party.AcquireItem(new ItemDefinitionId("624")).Admitted);
        InteractionOutcome turnedIn = Answer(rule, party, "topic-45", lasker, clock);
        Assert.Equal(string.Empty, turnedIn.Residue);
        Assert.Contains("vase", turnedIn.Message, StringComparison.Ordinal);
        Assert.Equal("thief-rogue", progression.LastPromotion!.Promotion);
        Assert.Equal("Rogue", party.Members[0].Profile.Class.Value);
        Assert.Equal("Knight", party.Members[1].Profile.Class.Value);
        Assert.DoesNotContain(conversation.Offers(talking), offer => offer.Id == "topic-45");
    }

    /// <summary>The ids of what the open conversation offers for the Rogue rank: the promoter's topics and the ladder's own offer.</summary>
    private static string[] RankTopics(RecordingUiService ui)
    {
        ProjectedNode topics = ProjectedNode.Of(ui.Latest().Value).Field("conversation").Field("topics");
        return [.. Enumerable.Range(0, topics.Length())
            .Select(topics.Item)
            .Select(topic => topic.Field("id").AsString())
            .Where(id => id.StartsWith(MightAndMagic7Conversation.TopicIdPrefix, StringComparison.Ordinal) || id == $"{MightAndMagic7Identities.PromotionTopicPrefix}thief-rogue")];
    }

    private static InteractionOutcome Answer(IInteractionRule rule, PartyEntity party, string raised, PlacementDefinition? placement = null, GameClock? clock = null)
    {
        PlacementDefinition at = placement ?? Standing("npc-15");
        InteractionTargetDefinition target = rule.Describe(new InteractionTargetRequest(new PlaceId("2"), at, string.Empty) { Raised = raised })!;
        InteractionOutcome outcome = rule.Apply(target, new InteractionContext(new PlaceId("2"), at, target, party, clock ?? TestClock.Create(scale: 1))
        {
            PlaceTargets = [at],
            Raised = raised,
        });
        Assert.True(outcome.IsApplied, outcome.Refusal?.Message);
        if (outcome is { Gain.IsFree: false }) party.Purse.Credit(outcome.Gain.Coins);
        foreach (InteractionItemYield item in outcome.Items) party.AcquireItem(item.Definition, item.Count);
        return outcome;
    }

    private static PlacementDefinition Standing(string person)
    {
        string json = string.Create(
            CultureInfo.InvariantCulture,
            $$"""{ "id": "person-0", "kind": "person", "x": 0, "y": 0, "z": 0, "people": [ {{JsonSerializer.Serialize(person)}} ] }""");
        return new PlacementDefinition(new PlacementContentId("person", "person-0"), "placements", 0, PlacePose.Origin, new ContentEntry("person-0", JsonDocument.Parse(json).RootElement));
    }

    private static PartyEntity Party(params (string Name, string Class, int Rank)[] members) =>
        new PartyEntityFactory().Create(new PartyCreation(
            [.. members.Select(member => new MemberCreation(new PartyMemberSeed(
                member.Name,
                new RaceId("Human"),
                new ClassId(member.Class),
                [new AttributeScore(new AttributeId("Might"), 15)],
                skills: [],
                spells: [],
                experience: 0,
                level: 1,
                skillPoints: 0,
                classRank: Math.Max(1, member.Rank),
                conditions: [],
                hitPoints: ResourcePool.Full(40),
                spellPoints: ResourcePool.Full(0))))],
            coins: 100,
            foodPortions: 2,
            reputation: 0,
            fame: 0));

    /// <summary>A catalog of the given global events, staged as the importer writes them.</summary>
    private static ContentCatalog Catalog(params string[] globalEvents)
    {
        (ProductCreateContext context, _) = RulesetTestContext.Create(
            RulesetTestContext.Bundle("partyrpg-default", "events"),
            ($"{RulesetTestContext.ContentDirectory}/content-packs/events/pack.json",
                """
                {
                  "schemaVersion": 1,
                  "packId": "events",
                  "kind": "definitions",
                  "provenance": { "description": "authored for a test" },
                  "documents": [ { "path": "global-events.json", "documentId": "global-events", "definitionKind": "global-event" } ]
                }
                """),
            ($"{RulesetTestContext.ContentDirectory}/content-packs/events/global-events.json",
                $$"""{ "documentId": "global-events", "definitionKind": "global-event", "entries": [ {{string.Join(",", globalEvents)}} ] }"""));
        ContentBootstrapResult bootstrap = ContentBootstrap.Load(
            RulesetTestContext.Content(context),
            ContentLayout.Under(RulesetTestContext.ContentDirectory),
            RulesetTestContext.BundleId);
        Assert.True(bootstrap.IsValid, string.Join("; ", bootstrap.Issues.Select(issue => issue.ToString())));
        return bootstrap.Catalog!;
    }

    /// <summary>
    /// A promoter staged as the importer writes one: the person the ladder names for the Rogue rank, whose first slot
    /// raises the errand's topic, and the turn-in it turns the slot to — each a global event in the shipped shape, with
    /// this test's own words — and a party of a thief and a knight.
    /// </summary>
    private static (string Path, string Text)[] Promoter() =>
    [
        RulesetTestContext.Bundle("partyrpg-default", "world"),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/pack.json",
            """
            {
              "schemaVersion": 1,
              "packId": "world",
              "kind": "definitions",
              "provenance": { "description": "authored for a test" },
              "documents": [
                { "path": "places.json", "documentId": "places", "definitionKind": "place" },
                { "path": "people.json", "documentId": "people", "definitionKind": "person", "references": [ "person:npc-15" ] },
                { "path": "topics.json", "documentId": "topics", "definitionKind": "person-topic" },
                { "path": "global-events.json", "documentId": "global-events", "definitionKind": "global-event" },
                { "path": "start.json", "documentId": "start", "definitionKind": "scenario-start" },
                { "path": "party.json", "documentId": "party", "definitionKind": "scenario-party" }
              ]
            }
            """),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/places.json",
            """
            { "documentId": "places", "definitionKind": "place", "entries": [
              { "id": "1", "kind": "interior", "name": "Sewers", "respawnDays": 7,
                "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
                "placements": [ { "id": "person-0", "kind": "person", "x": 100, "y": 0, "z": 0, "people": [ "npc-15" ] } ] } ] }
            """),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/people.json",
            """
            { "documentId": "people", "definitionKind": "person", "entries": [
              { "id": "npc-15", "npcId": 15, "name": "The Fence", "portrait": "700", "greeting": "'Quietly, now.'", "dialogueEvents": 1,
                "topicSlots": [ 44, 0, 0, 0, 0, 0 ],
                "topics": [ { "id": "topic-44", "label": "Rogue", "text": "", "textCount": 0, "event": 44 } ] } ] }
            """),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/topics.json",
            """
            { "documentId": "topics", "definitionKind": "person-topic", "entries": [
              { "id": "topic-44", "label": "Rogue", "text": "", "event": 44 },
              { "id": "topic-45", "label": "Rogue", "text": "", "event": 45 } ] }
            """),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/global-events.json",
            """
            { "documentId": "global-events", "definitionKind": "global-event", "entries": [
              { "id": "44", "event": 44, "topic": true, "steps": [
                { "step": 0, "op": "show-message", "textId": 1, "text": "Bring me the vase from the manor and I shall call you Rogue." },
                { "step": 1, "op": "add", "variable": "quest-bit", "value": 18 },
                { "step": 2, "op": "set-npc-topic", "index": 0, "person": 15, "raises": 45 } ] },
              { "id": "45", "event": 45, "topic": true, "steps": [
                { "step": 0, "op": "for-party-member", "who": "party" },
                { "step": 1, "op": "compare", "variable": "item", "value": 624, "target": 4 },
                { "step": 2, "op": "show-message", "textId": 2, "text": "No vase, no title." },
                { "step": 3, "op": "exit" },
                { "step": 4, "op": "show-message", "textId": 3, "text": "Well taken. You are Rogues now." },
                { "step": 5, "op": "for-party-member", "who": "member", "member": 0 },
                { "step": 6, "op": "compare", "variable": "class", "value": 4, "which": "Thief", "target": 10 },
                { "step": 7, "op": "add", "variable": "award", "value": 11 },
                { "step": 8, "op": "add", "variable": "experience", "value": 150 },
                { "step": 9, "op": "jump", "target": 13 },
                { "step": 10, "op": "set", "variable": "class", "value": 5, "which": "Rogue" },
                { "step": 11, "op": "add", "variable": "award", "value": 10 },
                { "step": 12, "op": "add", "variable": "experience", "value": 300 },
                { "step": 13, "op": "for-party-member", "who": "member", "member": 1 },
                { "step": 14, "op": "compare", "variable": "class", "value": 4, "which": "Thief", "target": 18 },
                { "step": 15, "op": "add", "variable": "award", "value": 11 },
                { "step": 16, "op": "add", "variable": "experience", "value": 150 },
                { "step": 17, "op": "jump", "target": 21 },
                { "step": 18, "op": "set", "variable": "class", "value": 5, "which": "Rogue" },
                { "step": 19, "op": "add", "variable": "award", "value": 10 },
                { "step": 20, "op": "add", "variable": "experience", "value": 300 },
                { "step": 21, "op": "subtract", "variable": "item", "value": 624 },
                { "step": 22, "op": "subtract", "variable": "quest-bit", "value": 18 },
                { "step": 23, "op": "set-npc-topic", "index": 0, "person": 15, "raises": 0 } ] } ] }
            """),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/start.json",
            """
            { "documentId": "start", "definitionKind": "scenario-start", "entries": [ { "id": "start", "place": "1", "entryPoint": "Party Start" } ] }
            """),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/party.json",
            """
            { "documentId": "party", "definitionKind": "scenario-party", "entries": [
              { "id": "party", "coins": 120, "food": 4, "reputation": 0, "fame": 0,
                "members": [
                  { "name": "Lasse", "race": "human", "class": "Thief", "level": 1, "hitPoints": 30, "spellPoints": 0,
                    "attributes": [ { "id": "Might", "value": 12 } ], "skills": [], "spells": [], "conditions": [] },
                  { "name": "Mira", "race": "human", "class": "Knight", "level": 1, "hitPoints": 40, "spellPoints": 0,
                    "attributes": [ { "id": "Might", "value": 15 } ], "skills": [], "spells": [], "conditions": [] } ] } ] }
            """),
    ];
}
