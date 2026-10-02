using System.Text.Json;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Quests;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using Xunit;

namespace PartyRpg.Rulesets.MightAndMagic7.Tests;

/// <summary>Actual monthly bounty settlement and explicitly authored counted rewards.</summary>
public sealed class CountedDeedPolicyTests
{
    [Fact]
    public void Each_monthly_bounty_counts_its_gold_once_and_the_next_month_adds_to_it()
    {
        MightAndMagic7Quests rule = MightAndMagic7Quests.Read(Catalog(), null)!;
        using PartyEntity party = Party();
        PartyQuests quests = new(rule, party, new PartyResourceLedger(party), clock: TestClock.Create(scale: 1));
        for (int month = 1; month <= 2; month++)
        {
            QuestId id = new(rule.BountyQuest("hall-1", new GameDate(1168, month, 1)));
            QuestDefinition definition = rule.Definition(id)!;
            Assert.Equal(5000, definition.Rewards.Coins);
            Assert.True(Assert.Single(definition.Rewards.Records).Accumulate);
            Assert.True(quests.Offer(id, definition.Giver).IsApplied);
            Assert.True(quests.Accept(id).IsApplied);
            using JsonDocument source = JsonDocument.Parse("""{"id":"beast","kind":"monster","monster":"7"}""");
            PlacementDefinition placement = new(new PlacementContentId("monster", "beast"), "actors", 0,
                PlacePose.Origin, new ContentEntry("beast", source.RootElement.Clone()));
            quests.ObserveDeath(new CreatureDeath(new PlaceId("1"), placement, "A beast"));
            Assert.True(quests.TurnIn(id, definition.Giver).IsApplied);
            Assert.Equal(month * 5000, party.Records.CountOf(MightAndMagic7Deeds.Bounties));
            Assert.Equal(month * 5000, party.Purse.Coins);
            Assert.Equal(QuestCodes.QuestAlreadyFinished, quests.TurnIn(id, definition.Giver).Refusal!.Code);
        }
        // Reconstructed earning owner resolves the same month from its durable identity.
        using PartyEntity restored = new PartyEntityFactory().Restore(party.Capture());
        PartyQuests resumed = new(rule, restored, new PartyResourceLedger(restored), save: quests.Capture());
        QuestId second = new(rule.BountyQuest("hall-1", new GameDate(1168, 2, 1)));
        Assert.Equal(QuestCodes.QuestAlreadyFinished, resumed.TurnIn(second, "keeper:hall-1").Refusal!.Code);
        Assert.Equal(10000, restored.Records.CountOf(MightAndMagic7Deeds.Bounties));
        Assert.Equal(10000, restored.Purse.Coins);
    }

    [Fact]
    public void Authored_counted_rewards_accumulate_through_the_real_quest_owner()
    {
        MightAndMagic7Quests rule = MightAndMagic7Quests.Read(Catalog(), null)!;
        using PartyEntity party = Party();
        PartyQuests quests = new(rule, party);
        foreach (string id in new[] { "win-1", "win-2" })
        {
            QuestId quest = new(id);
            quests.Offer(quest, "marshal"); quests.Accept(quest); quests.Observe(new PlaceId("1"));
            Assert.True(quests.TurnIn(quest, "marshal").IsApplied);
        }
        Assert.Equal(2, party.Records.CountOf(MightAndMagic7Deeds.ArenaWins));
        Assert.Empty(party.Capture().Effects);
    }

    [Fact]
    public void An_ambiguous_counting_word_is_a_named_content_defect()
    {
        ContentValidationException error = Assert.Throws<ContentValidationException>(
            () => MightAndMagic7Quests.Read(Catalog(counting: "\"yes\""), null));
        Assert.Contains(error.Issues, issue => issue.Code == "quest-reward-count-invalid");
    }

    private static PartyEntity Party() => new PartyEntityFactory().Create(new PartyCreation(
        [new MemberCreation(new PartyMemberSeed("Tester", new RaceId("Human"), new ClassId("Knight"),
            [], [], [], 0, 1, 0, 1, [], ResourcePool.Full(40), ResourcePool.Full(20)))],
        coins: 0, foodPortions: 0, reputation: MightAndMagic7Standing.WellRegarded, fame: 0));

    private static ContentCatalog Catalog(string counting = "true")
    {
        string Quest(string id) => $$$"""
            {"id":"{{{id}}}","reading":{"giver":"marshal","objectives":[{"id":"reach","kind":"reach","target":"Town"}],
              "records":[{"record":"award:arena-wins","amount":1,"accumulate":{{{counting}}}}]}}
            """;
        InMemoryContentSource source = new InMemoryContentSource()
            .Add("packs/world/pack.json", """
                {"schemaVersion":1,"packId":"world","kind":"definitions","provenance":{"description":"authored deed test"},"documents":[
                  {"path":"places.json","documentId":"places","definitionKind":"place"},
                  {"path":"monsters.json","documentId":"monsters","definitionKind":"monster"},
                  {"path":"quests.json","documentId":"quests","definitionKind":"quest"}]}
                """)
            .Add("packs/world/places.json", """
                {"documentId":"places","definitionKind":"place","entries":[
                  {"id":"1","kind":"region","name":"Town","monsters":["A beast"],
                    "placements":[{"id":"hall-1","kind":"fixture","fixture":"Town Hall"}]}]}
                """)
            .Add("packs/world/monsters.json", """
                {"documentId":"monsters","definitionKind":"monster","entries":[{"id":"7","name":"A beast","level":50}]}
                """)
            .Add("packs/world/quests.json", $$$"""
                {"documentId":"quests","definitionKind":"quest","entries":[{{{Quest("win-1")}}},{{{Quest("win-2")}}}]}
                """);
        return ContentCatalogLoader.Load(source, new ContentLayout("packs", "imports", "bundles")).RequireValid();
    }
}
