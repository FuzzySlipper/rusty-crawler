using PartyRpg.Kit.Conversation;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Progression;
using PartyRpg.Kit.Quests;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Rulesets.MightAndMagic7.Tests;

/// <summary>
/// The authored new-game errand over the same conversation, journal, custody, service, and reward owners a
/// session uses. The world rows are deliberately small, but keep the imported placement identities and the
/// ordinary person and counter shapes the committed opening pack names.
/// </summary>
public sealed class OpeningContentTests
{
    private static readonly ContentLayout Layout = ContentLayout.Under(RulesetTestContext.ContentDirectory);
    private static readonly PlaceId Island = new("1");
    private static readonly QuestId OpeningQuest = new("opening-island-guide");

    [Fact]
    public void Opening_errand_is_offered_taken_progressed_and_paid_once_through_the_canonical_owners()
    {
        (ProductCreateContext context, _) = RulesetTestContext.Create(OpeningFiles());
        ContentCatalog catalog = ContentCatalogLoader.Load(RulesetTestContext.Content(context), Layout).RequireValid();
        MightAndMagic7Quests quests = MightAndMagic7Quests.Read(catalog, promotions: null)!;
        QuestDefinition definition = quests.Definition(OpeningQuest)!;

        Assert.Equal("npc-4", definition.Giver);
        Assert.Equal(250, definition.Rewards.Experience);
        Assert.Equal(25, definition.Rewards.Coins);
        Assert.Equal(["met:npc-5", "220"], definition.Objectives.Select(objective => objective.Target).ToArray());
        Assert.Equal("supply-bottle", definition.Objectives[1].Id);
        Assert.Equal(QuestObjectiveKind.Talk, definition.Objectives[0].Kind);
        Assert.Equal(QuestObjectiveKind.Deliver, definition.Objectives[1].Kind);
        Assert.Equal("met:npc-4", definition.Objectives[1].Person);

        PlaceGraph graph = PlaceGraphLoader.Load(catalog);
        PlacePopulationContent population = PlacePopulationContent.Read(graph);
        PlacementDefinition ailyssa = population.PlacementsOf(Island).Single(placement => placement.Content.Id == "person-8");
        PlacementDefinition sally = population.PlacementsOf(Island).Single(placement => placement.Content.Id == "person-7");
        PlacementDefinition supplies = population.PlacementsOf(Island).Single(placement => placement.Content.Id == "service-42");
        PlacementDefinition shopsSign = population.PlacementsOf(Island).Single(placement => placement.Content.Id == "fixture-69");
        Assert.Equal("Shops", shopsSign.Source.GetString("name"));
        Assert.Equal("service", supplies.Content.Kind);

        MightAndMagic7Services services = MightAndMagic7Services.Read(catalog, quests: quests)!;
        MightAndMagic7Conversation? conversation = null;
        PartyEntity party = CreateParty();
        using (party)
        {
            GameClock clock = Clock();
            PartyResourceLedger ledger = new(party);
            PartyProgression progression = new(MightAndMagic7Progression.Instance, party);
            PartyQuests? journal = null;
            conversation = MightAndMagic7Conversation.Read(
                catalog,
                services,
                quests: quests,
                journal: () => journal,
                party: () => party);
            journal = new PartyQuests(quests, party, ledger, progression, clock);
            PartyConversations dialogue = new(conversation!, party, clock);

            ConversationResult opened = dialogue.OpenTarget(Island, ailyssa)!;
            Assert.True(opened.IsApplied);
            Assert.Equal("Ailyssa", dialogue.Speaker!.Name);
            ConversationResult offeredLine = dialogue.Choose("quest:opening-island-guide");
            Assert.Equal(HandoffOwner.ErrandOffer, offeredLine.Handoff!.Owner);
            Assert.True(journal.Offer(OpeningQuest, "npc-4", Island).IsApplied);

            ConversationResult acceptedLine = dialogue.Choose("accept:opening-island-guide");
            Assert.Equal(HandoffOwner.ErrandAccept, acceptedLine.Handoff!.Owner);
            Assert.True(journal.Accept(OpeningQuest).IsApplied);
            Assert.Equal(QuestStage.Accepted, journal.Instance(OpeningQuest)!.Stage);

            dialogue.Close();
            ConversationResult metShore = dialogue.OpenTarget(Island, sally)!;
            Assert.True(metShore.IsApplied);
            Assert.Equal("Sally", dialogue.Speaker!.Name);
            Assert.True(party.Records.Has("met:npc-5"));
            Assert.False(journal.Read(OpeningQuest)!.IsComplete);

            dialogue.Close();
            ConversationResult serviceGreeting = dialogue.OpenTarget(Island, supplies)!;
            Assert.True(serviceGreeting.IsApplied);
            Assert.Equal("Kethry", dialogue.Speaker!.Name);
            ConversationResult serviceLine = dialogue.Choose("counter");
            Assert.Equal(HandoffOwner.Counter, serviceLine.Handoff!.Owner);

            QuestNeed needed = journal.Needs(new ItemDefinitionId("220")) ?? throw new Xunit.Sdk.XunitException("the accepted opening errand must retain its bottle objective");
            Assert.Equal(OpeningQuest.Value, needed.Quest.Value);
            Assert.Equal("quest-item-needed", journal.Retains(new ItemDefinitionId("220"))!.Code);
            Assert.True(party.AcquireItem(new ItemDefinitionId("220")).Admitted);
            QuestReading carrying = journal.Read(OpeningQuest)!;
            Assert.True(carrying.IsComplete);
            Assert.Equal(2, carrying.Objectives.Count(objective => objective.IsMet));

            dialogue.Close();
            dialogue.OpenTarget(Island, ailyssa);
            ConversationResult turnInLine = dialogue.Choose("turn-in:opening-island-guide");
            Assert.Equal(HandoffOwner.ErrandTurnIn, turnInLine.Handoff!.Owner);
            int coinsBefore = party.Purse.Coins;
            long experienceBefore = party.Members[0].Progression.Experience;
            QuestResult paid = journal.TurnIn(OpeningQuest, "npc-4");
            Assert.True(paid.IsApplied);
            Assert.Equal(25, paid.Payment.Coins);
            Assert.Equal(250, paid.Payment.Experience!.Awarded);
            Assert.Equal("220", Assert.Single(paid.Payment.Delivered).Item);
            Assert.Equal(coinsBefore + 25, party.Purse.Coins);
            Assert.Equal(experienceBefore + 250, party.Members[0].Progression.Experience);
            Assert.Equal(0, party.Inventory.TotalOf(new ItemDefinitionId("220")));
            Assert.Null(journal.Retains(new ItemDefinitionId("220")));
            Assert.True(party.Records.Has("opening:shore-guide"));

            QuestResult repeated = journal.TurnIn(OpeningQuest, "npc-4");
            Assert.False(repeated.IsApplied);
            Assert.Equal("quest-already-finished", repeated.Refusal!.Code);
            Assert.Equal(coinsBefore + 25, party.Purse.Coins);
            Assert.Equal(experienceBefore + 250, party.Members[0].Progression.Experience);
        }
    }

    private static (string Path, string Text)[] OpeningFiles() =>
    [
        RulesetTestContext.Bundle("partyrpg-default", "world", "mm7-new-game"),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/pack.json",
            TestPacks.Manifest(
                "world",
                ("places", "place"),
                ("people", "person"),
                ("items", "item"),
                ("services", "service"))),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/places.json",
            """
            {
              "documentId": "places",
              "definitionKind": "place",
              "entries": [
                {
                  "id": "1", "kind": "region", "name": "Emerald Island", "respawnDays": 7,
                  "entryPoints": [ { "id": "Party Start", "x": 12552, "y": 800, "z": 193, "yaw": 512 } ],
                  "placements": [
                    { "id": "person-8", "kind": "person", "x": 11904, "y": 4672, "z": 96, "people": [ "npc-4" ] },
                    { "id": "person-7", "kind": "person", "x": 15000, "y": 16000, "z": 96, "people": [ "npc-5" ] },
                    { "id": "fixture-69", "kind": "fixture", "name": "Shops", "x": 12552, "y": 7136, "z": 288 },
                    { "id": "service-42", "kind": "service", "houseId": "42", "x": 9000, "y": 9000, "z": 96 }
                  ]
                }
              ]
            }
            """),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/people.json",
            """
            {
              "documentId": "people",
              "definitionKind": "person",
              "entries": [
                { "id": "npc-4", "npcId": 4, "name": "Ailyssa", "portrait": "704",
                  "greeting": "'Welcome to the island.'", "greetingAgain": "'Back from the shore?'", "dialogueEvents": 1,
                  "topics": [] },
                { "id": "npc-5", "npcId": 5, "name": "Sally", "portrait": "705",
                  "greeting": "'The shore is this way.'", "greetingAgain": "'You found the shore.'", "dialogueEvents": 1,
                  "topics": [] }
              ]
            }
            """),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/items.json",
            """
            {
              "documentId": "items",
              "definitionKind": "item",
              "entries": [
                { "id": "220", "name": "Potion Bottle", "value": 1, "equipStat": "Bottle", "material": "Glass" }
              ]
            }
            """),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/services.json",
            """
            {
              "documentId": "services",
              "definitionKind": "service",
              "entries": [
                { "id": "42", "kind": "Opening Counter", "name": "The Blue Bottle", "proprietor": "Kethry",
                  "openHour": 6, "closedHour": 18 }
              ]
            }
            """),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/mm7-new-game/pack.json",
            // The authored pack now also carries stock and creation-policy documents; this opening-quest
            // fixture keeps only the two documents this suite owns, while OpeningAcquisitionPolicyTests
            // exercises the added definitions against their service and item readers.
            TestPacks.Manifest("mm7-new-game", ("new-game-start", "scenario-start"), ("opening-errand", "quest"))),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/mm7-new-game/new-game-start.json",
            Repository.Read("content", "partyrpg", "content-packs", "mm7-new-game", "new-game.json")),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/mm7-new-game/opening-errand.json",
            Repository.Read("content", "partyrpg", "content-packs", "mm7-new-game", "opening.json")),
    ];

    private static GameClock Clock() => new(
        GameCalendar.TwelveMonthsOfFourWeeks,
        new GameDate(1168, 1, 1, 9, 0, 0),
        new GameTimeScale(1),
        new DaylightWindow(new TimeOfDay(5, 0), new TimeOfDay(21, 0)));

    private static PartyEntity CreateParty() => new PartyEntityFactory().Create(new PartyCreation(
        [
            new MemberCreation(new PartyMemberSeed(
                "Tester",
                new RaceId("Human"),
                new ClassId("Knight"),
                [new AttributeScore(new AttributeId("Might"), 15)],
                skills: [],
                spells: [],
                experience: 0,
                level: 1,
                skillPoints: 0,
                classRank: 1,
                conditions: [],
                hitPoints: ResourcePool.Full(40),
                spellPoints: ResourcePool.Full(0))),
        ],
        coins: 200,
        foodPortions: 10,
        ProvisionUnit.Portions,
        reputation: 0,
        fame: 0));
}
