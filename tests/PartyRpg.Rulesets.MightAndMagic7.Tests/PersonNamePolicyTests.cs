using PartyRpg.Kit;
using PartyRpg.Kit.Content;
using PartyRpg.Testing;
using Xunit;

namespace PartyRpg.Rulesets.MightAndMagic7.Tests;

/// <summary>
/// This game's names for people: a person the world names by identity — a quest's giver — is called what the people
/// table calls them, and an identity the table does not carry stays as it is.
/// </summary>
public sealed class PersonNamePolicyTests
{
    [Fact]
    public void A_giver_is_called_by_the_people_tables_name()
    {
        InMemoryContentSource source = new InMemoryContentSource()
            .Add("packs/tables/pack.json", TestPacks.Manifest("tables", ("people", MightAndMagic7Conversation.PersonDefinitionKind)))
            .Add("packs/tables/people.json", TestPacks.Document("people", MightAndMagic7Conversation.PersonDefinitionKind,
                """{"id":"npc-1","name":"Lord Markham","portrait":"709"}"""));
        MightAndMagic7Names names = MightAndMagic7Names.Read(
            ContentCatalogLoader.Load(source, new ContentLayout("packs", "imports", "bundles")).RequireValid());

        Assert.Equal("Lord Markham", names.PersonName("npc-1"));
        Assert.Equal("Lord Markham", GameNames.Person(names, "npc-1"));
        Assert.Equal("npc-9", GameNames.Person(names, "npc-9"));
    }
}
