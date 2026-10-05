using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Conversation;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.World;
using Xunit;

namespace PartyRpg.Rulesets.MightAndMagic7.Tests;

public sealed class GroupNewsPolicyTests
{
    [Fact]
    public void News_changes_in_conversation_survives_party_save_and_later_refusal_discards_news_and_join()
    {
        ContentCatalog catalog = Catalog();
        using PartyEntity party = PromoterTopicTests.Party(("Tester", "Knight", 1));
        MightAndMagic7Fixtures? fixtures = null;
        var conversation = MightAndMagic7Conversation.Read(catalog, null, events: () => fixtures,
            party: () => party, canHearNews: (_, _) => true)!;
        fixtures = new(MightAndMagic7MapEvents.Read(catalog), people: conversation.PersonOf,
            topics: conversation.SpokenTopic, followers: conversation.Followers, news: conversation.News);
        var rule = new MightAndMagic7Interaction(fixtures: fixtures);
        var actor = Actor();
        string Said(MightAndMagic7Conversation reader, PartyEntity talking)
        {
            var subject = reader.Describe(new(new PlaceId("2"), actor))!;
            return reader.Greeting(new(new PlaceId("2"), actor, subject, subject.First.Id, [], talking, null)).Text;
        }
        Assert.Equal("Old news", Said(conversation, party));
        Assert.Empty(PromoterTopicTests.Answer(rule, party, "topic-1").Residue);
        Assert.Equal("New news", Said(conversation, party));
        Assert.Null(fixtures.JudgeRecord("person-group-news:1:2", 1));
        Assert.NotNull(fixtures.JudgeRecord("person-group-news:99:2", 1));
        Assert.NotNull(fixtures.JudgeRecord("person-group-news:1:999", 1));
        Assert.NotNull(fixtures.JudgeRecord("person-group-news:1:2", 2));

        // Current party schema carries the records; a newly composed conversation reads them.
        var metadata = (JsonTypeInfo<PartySave>)SessionSaveJson.TypeInfo.Options.GetTypeInfo(typeof(PartySave));
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(party.Capture(), metadata);
        using PartyEntity restored = new PartyEntityFactory().Restore(JsonSerializer.Deserialize(bytes, metadata)!);
        var resumed = MightAndMagic7Conversation.Read(catalog, null, party: () => restored, canHearNews: (_, _) => true)!;
        Assert.Equal("New news", Said(resumed, restored));
        Assert.Contains("nothing was changed", PromoterTopicTests.Answer(rule, party, "topic-2").Residue, StringComparison.Ordinal);
        Assert.Equal("New news", Said(conversation, party));
        Assert.Empty(party.Followers.All);
        Assert.Empty(PromoterTopicTests.Answer(rule, party, "topic-3").Residue);
        Assert.Null(conversation.Describe(new(new PlaceId("2"), actor))); // row zero silences the group
        Assert.Single(party.Records.All, record => record.Name.StartsWith("person-group-news:", StringComparison.Ordinal));

        var hostile = MightAndMagic7Conversation.Read(catalog, null, canHearNews: (_, _) => false)!;
        Assert.Null(hostile.Describe(new(new PlaceId("2"), actor)));
    }

    private static PlacementDefinition Actor()
    {
        using var source = JsonDocument.Parse("""{"id":"actor-0","kind":"actor","name":"Townsperson","group":1}""");
        return new(new("actor", "actor-0"), "actors", 0, PlacePose.Origin, new("actor-0", source.RootElement.Clone()));
    }

    private static ContentCatalog Catalog()
    {
        var source = new InMemoryContentSource();
        var documents = new (string Kind, string Entries)[]
        {
            ("person-group", """[{"id":"1","news":1}]"""),
            ("person-news", """[{"id":"1","text":"Old news"},{"id":"2","text":"New news"}]"""),
            ("person", """[{"id":"npc-15","npcId":15,"name":"Witness","canJoin":true,"profession":0,"hirePrice":0}]"""),
            ("person-topic", """[{"id":"topic-1","label":"News","event":1},{"id":"topic-2","label":"Refused news","event":2},{"id":"topic-3","label":"Silence","event":3}]"""),
            ("global-event", """
                [{"id":"1","event":1,"topic":true,"steps":[{"step":0,"op":"set-npc-group-news","newsGroup":1,"news":2}]},
                 {"id":"2","event":2,"topic":true,"steps":[
                   {"step":0,"op":"add","variable":"hireling","value":15},
                   {"step":1,"op":"set-npc-group-news","newsGroup":1,"news":1},
                   {"step":2,"op":"unknown-instruction"}]},
                 {"id":"3","event":3,"topic":true,"steps":[{"step":0,"op":"set-npc-group-news","newsGroup":1,"news":0}]}]
                """)
        };
        source.Add("packs/news/pack.json", JsonSerializer.Serialize(new { schemaVersion = 1, packId = "news", kind = "definitions",
            provenance = new { description = "authored group news regression" }, documents = documents.Select(row => new { path = row.Kind + ".json", documentId = row.Kind, definitionKind = row.Kind }) }));
        foreach (var row in documents)
            source.Add($"packs/news/{row.Kind}.json", $$"""{"documentId":"{{row.Kind}}","definitionKind":"{{row.Kind}}","entries":{{row.Entries}}}""");
        return ContentCatalogLoader.Load(source, new("packs", "imports", "bundles")).RequireValid();
    }
}
