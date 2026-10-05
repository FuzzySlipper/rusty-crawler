using System.Text.Json;
using PartyRpg.Kit.Conversation;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Progression;
using PartyRpg.Kit.Quests;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Services;
using PartyRpg.Kit.Skills;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using PartyRpg.Testing;
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Session_quest_custody_names_the_retained_item_before_and_after_resume(bool resumed)
    {
        (ProductCreateContext context, _) = RulesetTestContext.Create(OpeningFiles());
        ContentCatalog catalog = ContentCatalogLoader.Load(RulesetTestContext.Content(context), Layout).RequireValid();
        MightAndMagic7Quests rule = MightAndMagic7Quests.Read(catalog, promotions: null)!;
        PartyEntity party = CreateParty();
        QuestSave? saved = null;
        if (resumed)
        {
            PartyQuests before = new(rule, party);
            Assert.True(before.Offer(OpeningQuest, "npc-4", Island).IsApplied);
            Assert.True(before.Accept(OpeningQuest).IsApplied);
            saved = before.Capture();
            PartySave partySave = party.Capture();
            party.Dispose();
            party = new PartyEntityFactory().Restore(partySave);
        }

        SessionOwners owners = new(Clock());
        using RecordingUiProjectionChannel ui = new();
        using PartyRpgSession session = new(
            new SessionComposition(new RulesetId("test.opening"), "Opening"), ui, owners,
            new SessionParty.Playing(Party: party, Resumed: resumed ? new SessionRecords(Quests: saved) : null),
            new SessionRules { Quests = rule, Names = MightAndMagic7Names.Read(catalog) });
        session.Start();
        PartyQuests quests = owners.Quests!;
        if (!resumed)
        {
            Assert.True(quests.Offer(OpeningQuest, "npc-4", Island).IsApplied);
            Assert.True(quests.Accept(OpeningQuest).IsApplied);
        }

        ItemInstance bottle = party.AcquireItem(new ItemDefinitionId("220")).Item!;
        var refusal = party.JudgeItemRemoval(bottle.Id)!;
        Assert.Equal(QuestCodes.QuestItemNeeded, refusal.Code);
        Assert.Contains("so Potion Bottle stays with the party", refusal.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("so 220 stays", refusal.Message, StringComparison.Ordinal);
        Assert.Contains(bottle, party.Inventory.Items);
        Assert.Equal(QuestStage.Accepted, quests.Instance(OpeningQuest)!.Stage);
    }

    [Fact]
    public void Opening_errand_is_offered_taken_progressed_and_paid_once_through_the_canonical_owners()
    {
        (ProductCreateContext context, _) = RulesetTestContext.Create(OpeningFiles());
        ContentCatalog catalog = ContentCatalogLoader.Load(RulesetTestContext.Content(context), Layout).RequireValid();
        MightAndMagic7Quests quests = MightAndMagic7Quests.Read(catalog, promotions: null)!;
        QuestDefinition definition = quests.Definition(OpeningQuest)!;

        Assert.Equal("npc-4", definition.Giver);
        Assert.Equal(4000, definition.Rewards.Experience);
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
            ItemAcquisition bottle = party.AcquireItem(new ItemDefinitionId("220"));
            Assert.True(bottle.Admitted);
            Assert.Equal("quest-item-needed", party.JudgeItemRemoval(bottle.Item!.Id)!.Code);
            QuestReading carrying = journal.Read(OpeningQuest)!;
            Assert.True(carrying.IsComplete);
            Assert.Equal(2, carrying.Objectives.Count(objective => objective.IsMet));

            dialogue.Close();
            dialogue.OpenTarget(Island, ailyssa);
            ConversationResult turnInLine = dialogue.Choose("turn-in:opening-island-guide");
            Assert.Equal(HandoffOwner.ErrandTurnIn, turnInLine.Handoff!.Owner);
            int coinsBefore = party.Purse.Coins;
            long[] experienceBefore = [.. party.Members.Select(member => member.Progression.Experience)];
            QuestResult paid = journal.TurnIn(OpeningQuest, "npc-4");
            Assert.True(paid.IsApplied);
            Assert.Equal(25, paid.Payment.Coins);
            Assert.Equal(4000, paid.Payment.Experience!.Awarded);
            Assert.Equal("220", Assert.Single(paid.Payment.Delivered).Item);
            Assert.Equal(coinsBefore + 25, party.Purse.Coins);
            Assert.All(
                party.Members.Select((member, index) => (member, index)),
                result => Assert.Equal(experienceBefore[result.index] + 1000, result.member.Progression.Experience));
            Assert.Equal(0, party.Inventory.TotalOf(new ItemDefinitionId("220")));
            Assert.Null(journal.Retains(new ItemDefinitionId("220")));
            Assert.True(party.Records.Has("opening:shore-guide"));

            QuestResult repeated = journal.TurnIn(OpeningQuest, "npc-4");
            Assert.False(repeated.IsApplied);
            Assert.Equal("quest-already-finished", repeated.Refusal!.Code);
            Assert.Equal(coinsBefore + 25, party.Purse.Coins);
            Assert.All(
                party.Members.Select((member, index) => (member, index)),
                result => Assert.Equal(experienceBefore[result.index] + 1000, result.member.Progression.Experience));
        }
    }

    [Fact]
    public void Opening_errand_pending_and_completed_state_survive_save_load_with_custody_and_once_only_payment()
    {
        (ProductCreateContext context, _) = RulesetTestContext.Create(OpeningFiles());
        ContentCatalog catalog = ContentCatalogLoader.Load(RulesetTestContext.Content(context), Layout).RequireValid();
        MightAndMagic7Quests quests = MightAndMagic7Quests.Read(catalog, promotions: null)!;
        using PartyEntity party = CreateParty();
        GameClock clock = Clock();
        PartyResourceLedger ledger = new(party);
        PartyProgression progression = new(MightAndMagic7Progression.Instance, party);
        PartyQuests journal = new(quests, party, ledger, progression, clock);

        Assert.True(journal.Offer(OpeningQuest, "npc-4", Island).IsApplied);
        Assert.True(journal.Accept(OpeningQuest).IsApplied);
        party.Records.Set("met:npc-4", 1);
        party.Records.Set("met:npc-5", 1);
        ItemAcquisition bottle = party.AcquireItem(new ItemDefinitionId("220"));
        Assert.True(bottle.Admitted);
        Assert.Equal("quest-item-needed", party.JudgeItemRemoval(bottle.Item!.Id)!.Code);

        SessionSave pending = SaveRoundTrip(party, clock, journal);
        using PartyEntity pendingParty = new PartyEntityFactory().Restore(pending.Party);
        GameClock pendingClock = Clock();
        PartyResourceLedger pendingLedger = new(pendingParty);
        PartyProgression pendingProgression = new(MightAndMagic7Progression.Instance, pendingParty);
        PartyQuests pendingJournal = new(
            quests,
            pendingParty,
            pendingLedger,
            pendingProgression,
            pendingClock,
            pending.Quests);

        Assert.Equal(QuestStage.Accepted, pendingJournal.Instance(OpeningQuest)!.Stage);
        Assert.True(pendingJournal.Read(OpeningQuest)!.IsComplete);
        ItemInstance pendingBottle = Assert.Single(pendingParty.Inventory.Items, item => item.Definition.Value == "220");
        Assert.Equal("quest-item-needed", pendingParty.JudgeItemRemoval(pendingBottle.Id)!.Code);

        QuestResult paid = pendingJournal.TurnIn(OpeningQuest, "npc-4");
        Assert.True(paid.IsApplied);
        Assert.Equal(4000, paid.Payment.Experience!.Awarded);
        Assert.Equal(25, paid.Payment.Coins);
        Assert.Equal(0, pendingParty.Inventory.TotalOf(new ItemDefinitionId("220")));
        Assert.All(pendingParty.Members, member => Assert.Equal(1000, member.Progression.Experience));

        SessionSave completed = SaveRoundTrip(pendingParty, pendingClock, pendingJournal);
        using PartyEntity completedParty = new PartyEntityFactory().Restore(completed.Party);
        PartyResourceLedger completedLedger = new(completedParty);
        PartyProgression completedProgression = new(MightAndMagic7Progression.Instance, completedParty);
        PartyQuests completedJournal = new(
            quests,
            completedParty,
            completedLedger,
            completedProgression,
            Clock(),
            completed.Quests);

        Assert.Equal(QuestStage.TurnedIn, completedJournal.Instance(OpeningQuest)!.Stage);
        Assert.Equal(0, completedParty.Inventory.TotalOf(new ItemDefinitionId("220")));
        Assert.Equal(225, completedParty.Purse.Coins);
        Assert.True(completedParty.Records.Has("opening:shore-guide"));
        Assert.All(completedParty.Members, member => Assert.Equal(1000, member.Progression.Experience));

        QuestResult repeated = completedJournal.TurnIn(OpeningQuest, "npc-4");
        Assert.False(repeated.IsApplied);
        Assert.Equal(QuestCodes.QuestAlreadyFinished, repeated.Refusal!.Code);
        Assert.Equal(225, completedParty.Purse.Coins);
        Assert.All(completedParty.Members, member => Assert.Equal(1000, member.Progression.Experience));
    }

    [Fact]
    public void Opening_reward_reaches_first_training_and_skill_points_use_the_ruleset_owner_and_name_refusals()
    {
        (ProductCreateContext context, _) = RulesetTestContext.Create(OpeningFiles());
        ContentCatalog catalog = ContentCatalogLoader.Load(RulesetTestContext.Content(context), Layout).RequireValid();
        MightAndMagic7Quests quests = MightAndMagic7Quests.Read(catalog, promotions: null)!;
        MightAndMagic7Skills skills = MightAndMagic7Skills.Read(catalog)!;
        MightAndMagic7Services services = MightAndMagic7Services.Read(catalog, quests: quests)!;
        ServiceDefinition training = services.Find(new ServiceId("89"))!;
        using PartyEntity party = CreateParty();
        GameClock clock = Clock();
        PartyResourceLedger ledger = new(party);
        PartyProgression progression = new(MightAndMagic7Progression.Instance, party, skills);
        PartyQuests journal = new(quests, party, ledger, progression, clock);

        Assert.True(journal.Offer(OpeningQuest, "npc-4", Island).IsApplied);
        Assert.True(journal.Accept(OpeningQuest).IsApplied);
        party.Records.Set("met:npc-4", 1);
        party.Records.Set("met:npc-5", 1);
        Assert.True(party.AcquireItem(new ItemDefinitionId("220")).Admitted);
        Assert.True(journal.TurnIn(OpeningQuest, "npc-4").IsApplied);
        Assert.All(party.Members, member => Assert.Equal(1000, member.Progression.Experience));

        ServiceOffer offer = Assert.Single(services.Offers(new ServiceOfferRequest(training, party, clock)));
        Assert.Equal(ServiceOfferKind.Training, offer.Kind);
        Assert.Equal(5, offer.Limit);
        foreach (PartyMember member in party.Members)
        {
            ServiceSubject subject = ServiceSubject.OfOffer(offer);
            ServiceEligibility eligibility = services.Judge(new ServiceEligibilityRequest(
                training,
                ServiceOperationKind.Train,
                subject,
                member.Id,
                party,
                clock));
            Assert.Null(eligibility.Refusal);

            ServiceQuote quote = services.Quote(new ServiceQuoteRequest(
                training,
                ServiceOperationKind.Train,
                subject,
                member.Id,
                party,
                clock));
            // The training table's base is ten coins. The completed errand also raises the party's standing by
            // four points for its 4,000 experience, so the canonical merchant quote is nine coins here.
            Assert.Equal(9, quote.Charge.Coins);
            Assert.True(ledger.Settle(quote.Charge).Admitted);

            ProgressionTrainingResult trained = progression.Train(
                member.Id,
                new ProgressionTrainingTerms(training.Name, quote.Charge.Coins, offer.Limit));
            Assert.True(trained.IsTrained);
            Assert.Equal(2, member.Progression.Level);
            Assert.Equal(5, member.Progression.SkillPoints);

            SkillRaisePlan plan = progression.Plan(member.Id, new SkillId("Sword"));
            Assert.True(plan.IsPossible);
            Assert.Equal(2, plan.Points);
            SkillRaiseResult raised = progression.RaiseSkill(member.Id, new SkillId("Sword"));
            Assert.True(raised.IsRaised);
            Assert.Equal(2, member.Skills.LevelOf(new SkillId("Sword")));
            Assert.Equal(3, member.Progression.SkillPoints);

            SkillRaiseResult refused = progression.RaiseSkill(member.Id, new SkillId("Sword"), levels: 2);
            Assert.False(refused.IsRaised);
            Assert.Equal(ProgressionCodes.InsufficientSkillPoints, refused.Refusal!.Code);
            Assert.Equal(2, member.Skills.LevelOf(new SkillId("Sword")));
            Assert.Equal(3, member.Progression.SkillPoints);
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
                ("services", "service"),
                ("skills", "skill"))),
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
                    { "id": "service-42", "kind": "service", "houseId": "42", "x": 9000, "y": 9000, "z": 96 },
                    { "id": "service-89", "kind": "service", "houseId": "89", "x": 9000, "y": 10000, "z": 96 }
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
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/skills.json",
            """
            {
              "documentId": "skills",
              "definitionKind": "skill",
              "entries": [ { "id": "Sword" } ]
            }
            """),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/services.json",
            """
            {
              "documentId": "services",
              "definitionKind": "service",
              "entries": [
                { "id": "42", "kind": "Opening Counter", "name": "The Blue Bottle", "proprietor": "Kethry",
                  "openHour": 6, "closedHour": 18 },
                { "id": "89", "kind": "Training", "name": "Island Training Grounds", "proprietor": "Trajan",
                  "mapId": 1, "openHour": 6, "closedHour": 18, "priceMultiplier": 10, "trainingCap": 5 }
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

    private static SessionSave SaveRoundTrip(PartyEntity party, GameClock clock, PartyQuests journal)
    {
        PlaceStateLedger places = new(PlaceGraph.From([], []), PlaceRespawnRule.FromContent());
        SessionSave document = new(
            party.Capture(),
            ClockSave.Capture(clock),
            new WorldSave(new PartyPose(Island, PlacePose.Origin), places.Capture()),
            journal.Capture());
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(document, SessionSaveJson.TypeInfo);
        return JsonSerializer.Deserialize(bytes, SessionSaveJson.TypeInfo)
            ?? throw new Xunit.Sdk.XunitException("opening save did not deserialize");
    }

    private static PartyEntity CreateParty()
    {
        List<MemberCreation> members = [];
        for (int index = 0; index < 4; index++)
        {
            members.Add(new MemberCreation(new PartyMemberSeed(
                $"Tester {index + 1}",
                new RaceId("Human"),
                new ClassId("Knight"),
                [new AttributeScore(new AttributeId("Might"), 15)],
                skills: [new SkillEntry(new SkillId("Sword"), 1, new SkillTier(1), 1)],
                spells: [],
                experience: 0,
                level: 1,
                skillPoints: 0,
                classRank: 1,
                conditions: [],
                hitPoints: ResourcePool.Full(40),
                spellPoints: ResourcePool.Full(0))));
        }

        return new PartyEntityFactory().Create(new PartyCreation(
            members,
            coins: 200,
            foodPortions: 10,
            ProvisionUnit.Portions,
            reputation: 0,
            fame: 0));
    }
}
