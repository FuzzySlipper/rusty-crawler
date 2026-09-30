using System.Globalization;
using System.Text.Json;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Quests;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using PartyRpg.Rulesets.MightAndMagic7;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Host.Tests;

/// <summary>
/// This game's quests against the real composition: the errands the shipped quest table states over the
/// ranks that ask for them, an errand offered in a conversation and finished with the person who gave it,
/// what a turn-in pays, and the errand a promotion rank reads before it is given.
/// </summary>
/// <remarks>
/// <para>
/// The kit's own suite proves the owner with quests it states itself. What only this suite can prove is that
/// this game's readings reach it: the shipped quest table's own words become the note a player reads, the
/// ladder's givers become the people an errand is taken from and handed back to, the places and creatures
/// content carries resolve the objectives, and a finished errand is the record a rank's requirement and a
/// shipped topic's own gate both read.
/// </para>
/// <para>
/// The composed case is written the way the importer writes a world — a place, the person standing in it,
/// the tables around them — so the same suite proves the shipped policy rather than a fixture invented for
/// it. The readings are checked against the operator's own imported packs where they are staged, and a
/// machine without that data reports those cases skipped.
/// </para>
/// </remarks>
public sealed class QuestPolicyTests
{
    private static readonly UseIntentNames UseControls = new(
        ProductIdentity.UseIntent,
        ProductIdentity.UseAction,
        ProductIdentity.UiActionContract);

    private static readonly ConversationIntentNames ConversationControls = new(
        ProductIdentity.ConversationLeaveIntent,
        ProductIdentity.UiActionContract);

    private static readonly ServiceIntentNames ServiceControls = new(
        ProductIdentity.ServiceLeaveIntent,
        ProductIdentity.UiActionContract);

    [ImportedFact("quests.json")]
    public void The_shipped_quest_table_states_the_words_and_this_game_states_the_errand()
    {
        ContentCatalog catalog = ImportedContent.Load();
        MightAndMagic7Promotions ladder = MightAndMagic7Promotions.Read(catalog);
        MightAndMagic7Quests questsRead = MightAndMagic7Quests.Read(catalog, ladder)!;

        // Seventeen errands, one per rank whose errand its own words state as a deed rather than as something
        // carried: the bits are the ones the ladder names, so what a rank asks for and what this reads as an
        // errand are the same rows.
        Assert.Equal(17, questsRead.ErrandCount);
        Assert.Equal(17, questsRead.ErrandBits.Count);
        Assert.Equal(17, ladder.QuestRequirementCount);
        Assert.Equal(
            ladder.Ladder.Ranks.SelectMany(rank => rank.Requirements)
                .Where(requirement => requirement.Name.StartsWith("errand:", StringComparison.Ordinal))
                .Select(requirement => requirement.Name["errand:".Length..])
                .Order(StringComparer.Ordinal),
            questsRead.ErrandBits.Order(StringComparer.Ordinal));

        // Nothing this game reads is left unstaged: every objective resolved against the places and the
        // monster table the packs carry, so no errand is stated with an objective nothing could satisfy.
        Assert.DoesNotContain(questsRead.Notes, note => note.Contains("cannot be stated", StringComparison.Ordinal));
        Assert.DoesNotContain(questsRead.Notes, note => note.Contains("is not stated", StringComparison.Ordinal));

        // The words are the shipped table's own, and the giver is the ladder's: bit 35 is Frederick Org's,
        // named in the shipped row's own text ("return to Frederick Org in Erathia").
        QuestDefinition treasury = questsRead.Definition(new QuestId("35"))!;
        Assert.Equal("npc-43", treasury.Giver);
        Assert.Contains("Elven Treasury", treasury.Note, StringComparison.Ordinal);
        Assert.Contains("Frederick Org", treasury.Note, StringComparison.Ordinal);
        QuestObjective reach = Assert.Single(treasury.Objectives);
        Assert.Equal(QuestObjectiveKind.Reach, reach.Kind);
        Assert.Equal("Reach Castle Navan", reach.Label);

        // An errand whose words say "all" of something counts every one the place's own placements hold: the
        // Haunted Mansion's undead, read from the same packs the creatures are placed from.
        QuestDefinition house = questsRead.Definition(new QuestId("34"))!;
        Assert.All(house.Objectives, objective => Assert.Equal(QuestObjectiveKind.Kill, objective.Kind));
        Dictionary<string, int> placed = [];
        foreach ((_, _, ContentEntry place) in catalog.Entries(PlaceGraphLoader.PlaceDefinitionKind))
        {
            if (!string.Equals(place.GetString("name"), "The Haunted Mansion", StringComparison.Ordinal)) continue;
            foreach (JsonElement placement in place.GetArray("placements"))
            {
                if (!string.Equals(ContentEntry.ReadString(placement, "kind"), "monster", StringComparison.Ordinal)) continue;
                string row = ContentEntry.ReadId(placement, "monster");
                placed[row] = placed.GetValueOrDefault(row) + 1;
            }
        }

        Assert.NotEmpty(placed);
        foreach (QuestObjective objective in house.Objectives)
        {
            Assert.Equal(placed[objective.Target], objective.Count);
        }

        // One errand nobody authors: the town hall's board. The beast is the place's own encounter row by the
        // month the clock stands in, what it pays is the donor's hundred times its level, and the keeper who
        // offers it is the one the placement's own identity names.
        string hall = string.Empty;
        foreach ((_, _, ContentEntry place) in catalog.Entries(PlaceGraphLoader.PlaceDefinitionKind))
        {
            foreach (JsonElement placement in place.GetArray("placements"))
            {
                if (string.Equals(ContentEntry.ReadString(placement, "fixture"), "Town Hall", StringComparison.Ordinal))
                {
                    hall = ContentEntry.ReadId(placement, "id");
                }
            }
        }

        Assert.NotEqual(string.Empty, hall);
        string posted = questsRead.BountyQuest(hall, new GameDate(1168, 3, 1));
        Assert.StartsWith(MightAndMagic7Quests.BountyPrefix, posted, StringComparison.Ordinal);
        QuestDefinition bounty = questsRead.Definition(new QuestId(posted))!;
        Assert.Equal($"keeper:{hall}", bounty.Giver);
        Assert.Equal(1, bounty.Objectives[0].Count);
        Assert.True(bounty.Rewards.Coins > 0);
        Assert.Contains("bounty", bounty.Note, StringComparison.Ordinal);

        // A month that names another beast names another contract, and the identity carries both, so a save
        // that records it can resolve the same errand after the month has turned.
        string later = questsRead.BountyQuest(hall, new GameDate(1168, 4, 1));
        Assert.NotEqual(posted, later);
        Assert.NotNull(questsRead.Definition(new QuestId(later)));
    }

    [Fact]
    public void An_errand_is_offered_in_a_conversation_finished_with_its_giver_and_judged_by_a_rank()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(ErrandContent());
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            ProductTestContext.RulesetContext(context, ui) with
            {
                Use = UseControls,
                Conversation = ConversationControls,
                Service = ServiceControls,
            });
        session.Start();

        // The party stands where the person who gives the errand lives, which is not the place the errand
        // asks for: a quest spans places, and what one of them reports is progress only once the errand is
        // the party's own.
        session.Update(ProductTestContext.Update(1, 1));
        Assert.Equal("Erathia", ProjectedNode.Of(ui.Latest().Value).Field("world").Field("name").AsString());

        // Talking to the person the ladder names as the rank's giver offers the errand, in the shipped words
        // the pack carries for that bit.
        session.Update(ProductTestContext.Update(2, 1, ProductTestContext.Digital(ProductIdentity.UseIntent)));
        ProjectedNode talking = ProjectedNode.Of(ui.Latest().Value).Field("conversation");
        Assert.True(talking.Field("open").AsBoolean());
        Assert.Equal("Frederick Org", talking.Field("speaker").AsString());
        Assert.Contains(
            "errand:35",
            Enumerable.Range(0, talking.Field("topics").Length()).Select(position => talking.Field("topics").Item(position).Field("id").AsString()));

        session.Update(ProductTestContext.Update(3, 1, ProductTestContext.ChooseTopic("errand:35")));
        ProjectedNode offered = ProjectedNode.Of(ui.Latest().Value).Field("quests");
        Assert.True(offered.Field("available").AsBoolean());
        Assert.Equal("offer", offered.Field("action").AsString());
        Assert.Equal("applied", offered.Field("outcome").AsString());
        Assert.Equal("offered", offered.Field("journal").Item(0).Field("state").AsString());
        Assert.Contains("Elven Treasury", offered.Field("journal").Item(0).Field("note").AsString(), StringComparison.Ordinal);

        // Agreeing to it makes it the party's own work, and the topic the person has to say changes with the
        // stage: what a player is offered is what the party's own state allows.
        session.Update(ProductTestContext.Update(4, 1, ProductTestContext.ChooseTopic("accept:35")));
        ProjectedNode accepted = ProjectedNode.Of(ui.Latest().Value).Field("quests");
        Assert.Equal("accept", accepted.Field("action").AsString());
        Assert.Equal("accepted", accepted.Field("journal").Item(0).Field("state").AsString());

        // The errand asks for a place the party is not standing in, and standing somewhere else is not
        // progress: handing it in here is refused, naming the objective that is unmet.
        Assert.False(accepted.Field("journal").Item(0).Field("canTurnIn").AsBoolean());
        session.Update(ProductTestContext.Update(5, 1, ProductTestContext.ChooseTopic("turn-in:35")));
        ProjectedNode refused = ProjectedNode.Of(ui.Latest().Value).Field("quests");
        Assert.Equal("turn-in", refused.Field("action").AsString());
        Assert.Equal("refused", refused.Field("outcome").AsString());
        Assert.Equal("quest-objectives-unmet", refused.Field("code").AsString());
        Assert.Contains("Reach Castle Navan", refused.Field("message").AsString(), StringComparison.Ordinal);

        // The journey itself is not what this proves, so the party is put where the errand asks and the next
        // admitted update reads the place it is in: the objective is met by the world's own report rather
        // than by anything a screen worked out. The conversation is left open across the journey because a
        // player cannot walk one with a conversation open; what is being proved is the plumbing, and the
        // turn-in is judged from the place the party stands in and the person it is speaking with.
        ((MightAndMagic7Session)session).World!.ArriveAt(new PlaceId("1"), PlacePose.Origin);
        session.Update(ProductTestContext.Update(6, 1));
        ProjectedNode completed = ProjectedNode.Of(ui.Latest().Value).Field("quests");
        Assert.Equal("completed", completed.Field("journal").Item(0).Field("state").AsString());
        Assert.True(completed.Field("journal").Item(0).Field("canTurnIn").AsBoolean());
        Assert.True(completed.Field("journal").Item(0).Field("objectives").Item(0).Field("met").AsBoolean());

        // Handing it back to the giver pays it: experience arrives at the one award entry, coin through the
        // ledger, and the record a finished errand leaves is on the party.
        long banked = (long)ProjectedNode.Of(ui.Latest().Value).Field("progression").Field("members").Item(0).Field("experience").AsNumber();
        session.Update(ProductTestContext.Update(7, 1, ProductTestContext.ChooseTopic("turn-in:35")));
        ProjectedNode finished = ProjectedNode.Of(ui.Latest().Value).Field("quests");
        Assert.Equal("applied", finished.Field("outcome").AsString());
        Assert.Equal("turned-in", finished.Field("journal").Item(0).Field("state").AsString());
        Assert.Equal(MightAndMagic7Quests.ErrandExperience, finished.Field("experience").AsNumber());
        Assert.Equal(MightAndMagic7Quests.ErrandCoins, finished.Field("coins").AsNumber());
        Assert.Equal(
            (double)(banked + MightAndMagic7Quests.ErrandExperience),
            ProjectedNode.Of(ui.Latest().Value).Field("progression").Field("members").Item(0).Field("experience").AsNumber());

        // The rank that asks for that errand is now given: the requirement the ladder states is the record the
        // turn-in wrote, so a promotion rank's errand is judged by real quest state rather than refused. The
        // errand was handed in with the person who gave it, which closed the conversation, so the party goes
        // back to them and speaks again — a rank is taken from somebody, in the place that somebody is.
        ((MightAndMagic7Session)session).World!.ArriveAt(new PlaceId("2"), PlacePose.Origin);
        session.Update(ProductTestContext.Update(8, 1, ProductTestContext.Digital(ProductIdentity.UseIntent)));
        session.Update(ProductTestContext.Update(9, 1, ProductTestContext.ChooseTopic("promote:knight-cavalier")));
        ProjectedNode promotion = ProjectedNode.Of(ui.Latest().Value).Field("promotion");
        Assert.Equal("granted", promotion.Field("outcome").AsString());
        Assert.Equal("Cavalier", promotion.Field("members").Item(0).Field("class").AsString());
        Assert.Equal(2, promotion.Field("members").Item(0).Field("rank").AsNumber());
        // What the member met reads in the ladder's own words for the errand it asked for, which is the
        // shipped row's deed rather than a second sentence this game would have to keep in step.
        Assert.Contains(
            "Elven Treasury",
            promotion.Field("granted").Item(0).Field("met").Item(1).AsString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void A_quest_item_cannot_be_sold_while_the_errand_needs_it_and_the_refusal_names_the_quest()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(CounterContent());
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            ProductTestContext.RulesetContext(context, ui) with
            {
                Use = UseControls,
                Conversation = ConversationControls,
                Service = ServiceControls,
            });
        session.Start();

        // The party talks to whoever keeps the counter — the person the pack's own quest names as its giver —
        // takes the errand on, and steps up to the counter. The one way into a shop is also the one way an
        // errand is taken, so a pack states an errand and a person with no second path between them.
        session.Update(ProductTestContext.Update(1, 1));
        session.Update(ProductTestContext.Update(2, 1, ProductTestContext.Digital(ProductIdentity.UseIntent)));
        ProjectedNode talking = ProjectedNode.Of(ui.Latest().Value).Field("conversation");
        Assert.True(talking.Field("open").AsBoolean());
        Assert.Contains(
            "errand:seal-of-office",
            Enumerable.Range(0, talking.Field("topics").Length()).Select(position => talking.Field("topics").Item(position).Field("id").AsString()));

        session.Update(ProductTestContext.Update(3, 1, ProductTestContext.ChooseTopic("errand:seal-of-office")));
        Assert.Equal("offered", ProjectedNode.Of(ui.Latest().Value).Field("quests").Field("journal").Item(0).Field("state").AsString());
        session.Update(ProductTestContext.Update(4, 1, ProductTestContext.ChooseTopic("accept:seal-of-office")));
        Assert.Equal("accepted", ProjectedNode.Of(ui.Latest().Value).Field("quests").Field("journal").Item(0).Field("state").AsString());

        // The party carries the very thing the errand asks for, and the counter buys what lies in its pack.
        PartyEntity party = ((MightAndMagic7Session)session).Party!;
        party.AcquireItem(new ItemDefinitionId("1"));

        session.Update(ProductTestContext.Update(5, 1, ProductTestContext.ChooseTopic(MightAndMagic7Conversation.CounterTopicId)));
        ProjectedNode counter = ProjectedNode.Of(ui.Latest().Value).Field("service");
        Assert.True(counter.Field("open").AsBoolean());
        Assert.Equal(1, counter.Field("sales").Length());

        // The errand the pack states asks the party to carry that very thing to its giver, so the item is
        // refused for sale and the refusal says which errand and what it asks for.
        session.Update(ProductTestContext.Update(6, 1, ProductTestContext.Payload("""{"action":"service.sell","target":"1"}""")));
        ProjectedNode refused = ProjectedNode.Of(ui.Latest().Value).Field("service");
        Assert.Equal("refused", refused.Field("outcome").AsString());
        Assert.Equal("service-item-needed-by-quest", refused.Field("code").AsString());
        Assert.Contains("The seal of office", refused.Field("message").AsString(), StringComparison.Ordinal);
        Assert.Contains("Carry the box", refused.Field("message").AsString(), StringComparison.Ordinal);
        Assert.Equal(0, refused.Field("earned").AsNumber());
        Assert.Equal(1, ProjectedNode.Of(ui.Latest().Value).Field("party").Field("pack").AsNumber());
        Assert.Equal(1, party.Inventory.TotalOf(new ItemDefinitionId("1")));

        // The counter is left, and the errand is handed in early: the refusal names the objective that is
        // unmet — the place the errand asks for — and the thing the errand asked for is still the party's.
        session.Update(ProductTestContext.Update(7, 1, ProductTestContext.Payload("""{"action":"service.leave"}""")));
        session.Update(ProductTestContext.Update(8, 1, ProductTestContext.Digital(ProductIdentity.UseIntent)));
        session.Update(ProductTestContext.Update(9, 1, ProductTestContext.ChooseTopic("turn-in:seal-of-office")));
        ProjectedNode unmet = ProjectedNode.Of(ui.Latest().Value).Field("quests");
        Assert.Equal("refused", unmet.Field("outcome").AsString());
        Assert.Equal("quest-objectives-unmet", unmet.Field("code").AsString());
        Assert.Contains("Reach the vault", unmet.Field("message").AsString(), StringComparison.Ordinal);
        Assert.Equal(1, party.Inventory.TotalOf(new ItemDefinitionId("1")));
    }

    /// <summary>The world, the person, and the tables an errand's own words and objectives are read over.</summary>
    private static (string Path, string Text)[] ErrandContent() =>
    [
        ProductTestContext.Bundle("partyrpg-default", "world"),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/pack.json",
            """
            {
              "schemaVersion": 1,
              "packId": "world",
              "kind": "definitions",
              "provenance": { "description": "authored for a test" },
              "documents": [
                { "path": "places.json", "documentId": "places", "definitionKind": "place" },
                { "path": "people.json", "documentId": "people", "definitionKind": "person",
                  "references": [ "person:npc-43" ] },
                { "path": "quests.json", "documentId": "quests", "definitionKind": "quest" },
                { "path": "start.json", "documentId": "start", "definitionKind": "scenario-start" },
                { "path": "party.json", "documentId": "party", "definitionKind": "scenario-party" }
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/places.json",
            """
            {
              "documentId": "places",
              "definitionKind": "place",
              "entries": [
                { "id": "2", "kind": "region", "name": "Erathia", "respawnDays": 7,
                  "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
                  "placements": [
                    { "id": "residence-304", "kind": "residence", "name": "Org House", "proprietor": "Placeholder",
                      "fixture": "House", "x": 100, "y": 0, "z": 0, "people": [ "npc-43" ] } ] },
                { "id": "1", "kind": "interior", "name": "Castle Navan", "respawnDays": 7,
                  "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ] }
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/people.json",
            """
            {
              "documentId": "people",
              "definitionKind": "person",
              "entries": [
                { "id": "npc-43", "npcId": 43, "name": "Frederick Org", "portrait": "700",
                  "greeting": "'Well met, travellers.'", "greetingAgain": "'You again.'", "dialogueEvents": 1,
                  "topics": [ { "id": "topic-1", "label": "The castle", "text": "'This is my castle.'", "textCount": 1 } ] }
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/quests.json",
            """
            {
              "documentId": "quests",
              "definitionKind": "quest",
              "entries": [
                { "id": "35", "text": "Raid the Elven Treasury at Castle Navan and return to Frederick Org.", "owner": "authored" }
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/start.json",
            """
            { "documentId": "start", "definitionKind": "scenario-start", "entries": [ { "id": "start", "place": "2", "entryPoint": "Party Start" } ] }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/party.json",
            """
            {
              "documentId": "party",
              "definitionKind": "scenario-party",
              "entries": [
                { "id": "party", "coins": 0, "food": 6, "reputation": 0, "fame": 0,
                  "members": [
                    { "name": "Roderick", "race": "Human", "class": "Knight", "level": 1, "classRank": 1,
                      "hitPoints": 40, "spellPoints": 0,
                      "attributes": [ { "id": "Might", "value": 13 } ],
                      "skills": [], "spells": [], "conditions": [] } ] }
              ]
            }
            """),
    ];

    /// <summary>A shop, the box it would buy, and the errand that needs the box carried elsewhere.</summary>
    private static (string Path, string Text)[] CounterContent() =>
    [
        ProductTestContext.Bundle("partyrpg-default", "world"),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/pack.json",
            """
            {
              "schemaVersion": 1,
              "packId": "world",
              "kind": "definitions",
              "provenance": { "description": "authored for a test" },
              "documents": [
                { "path": "places.json", "documentId": "places", "definitionKind": "place" },
                { "path": "items.json", "documentId": "items", "definitionKind": "item" },
                { "path": "services.json", "documentId": "services", "definitionKind": "service" },
                { "path": "quests.json", "documentId": "quests", "definitionKind": "quest" },
                { "path": "start.json", "documentId": "start", "definitionKind": "scenario-start" },
                { "path": "party.json", "documentId": "party", "definitionKind": "scenario-party" }
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/places.json",
            """
            {
              "documentId": "places",
              "definitionKind": "place",
              "entries": [
                { "id": "1", "kind": "interior", "name": "A market street", "respawnDays": 7,
                  "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
                  "placements": [ { "id": "1", "kind": "service", "x": 100, "y": 0, "z": 0 } ] },
                { "id": "2", "kind": "interior", "name": "The vault", "respawnDays": 7,
                  "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ] }
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/items.json",
            """
            {
              "documentId": "items",
              "definitionKind": "item",
              "entries": [ { "id": "1", "name": "A locked box", "value": 100 } ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/services.json",
            """
            {
              "documentId": "services",
              "definitionKind": "service",
              "entries": [
                { "id": "1", "kind": "Weapon Shop", "name": "The Sword and Shield", "proprietor": "Bertram",
                  "mapId": 1, "priceMultiplier": 1.5, "skillPriceMultiplier": 1, "stockIntervalDays": 7 }
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/quests.json",
            """
            {
              "documentId": "quests",
              "definitionKind": "quest",
              "entries": [
                {
                  "id": "seal-of-office",
                  "name": "The seal of office",
                  "text": "Carry the box to the vault and leave it there.",
                  "reading": {
                    "giver": "keeper:1",
                    "objectives": [
                      { "id": "box", "kind": "retrieve", "target": "A locked box", "count": 1, "label": "Carry the box" },
                      { "id": "vault", "kind": "reach", "target": "The vault", "count": 1, "label": "Reach the vault" }
                    ],
                    "experience": 500,
                    "coins": 10
                  }
                }
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/start.json",
            """
            { "documentId": "start", "definitionKind": "scenario-start", "entries": [ { "id": "start", "place": "1", "entryPoint": "Party Start" } ] }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/party.json",
            """
            {
              "documentId": "party",
              "definitionKind": "scenario-party",
              "entries": [
                { "id": "party", "coins": 0, "food": 6, "reputation": 0, "fame": 0,
                  "members": [
                    { "name": "Roderick", "race": "Human", "class": "Knight", "level": 1, "classRank": 1,
                      "hitPoints": 40, "spellPoints": 0,
                      "attributes": [ { "id": "Might", "value": 13 } ],
                      "skills": [], "spells": [], "conditions": [] } ],
                  "items": [ { "id": "1", "definition": "1", "custody": "pack" } ] }
              ]
            }
            """),
    ];
}
