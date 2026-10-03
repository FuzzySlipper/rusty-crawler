using PartyRpg.Kit.Content;
using PartyRpg.Testing;
using Xunit;

namespace PartyRpg.Rulesets.MightAndMagic7.Tests;

/// <summary>
/// This game's portraits: a creation portrait is drawn with the first installed face of its race and sex, and a
/// person's portrait with the interface image of its own name, both read from the media pack's icons.
/// </summary>
public sealed class PortraitPolicyTests
{
    [Fact]
    public void A_creation_portrait_is_its_races_first_face_and_a_person_is_their_own_image()
    {
        InMemoryContentSource source = new InMemoryContentSource()
            .Add("packs/media/pack.json", TestPacks.Manifest("media", ("icons", MightAndMagic7Portraits.IconDefinitionKind)))
            .Add("packs/media/icons.json", TestPacks.Document("icons", MightAndMagic7Portraits.IconDefinitionKind,
                """{"id":"pc01-01","path":"icons/pc01-01.png"}""",
                """{"id":"pc1501","path":"icons/pc1501.png"}""",
                """{"id":"npc123","path":"icons/npc123.png"}""",
                """{"id":"npc007","path":"icons/npc007.png"}"""));
        MightAndMagic7Portraits portraits = MightAndMagic7Portraits.Read(
            ContentCatalogLoader.Load(source, new ContentLayout("packs", "imports", "bundles")).RequireValid());

        Assert.Equal("packs/media/icons/pc01-01.png", portraits.PathOf("human-man"));
        Assert.Equal("packs/media/icons/pc1501.png", portraits.PathOf("dwarf-woman"));
        Assert.Equal("packs/media/icons/npc123.png", portraits.PathOf("npc123"));
        // A person's portrait is the number of their face in the people table, drawn with that number's interface image.
        Assert.Equal("packs/media/icons/npc123.png", portraits.PathOf("123"));
        Assert.Equal("packs/media/icons/npc007.png", portraits.PathOf("7"));

        // A face the install lacks, and a portrait nobody named, are drawn with nothing.
        Assert.Null(portraits.PathOf("elf-man"));
        Assert.Null(portraits.PathOf(string.Empty));
        Assert.Null(MightAndMagic7Portraits.Read(null).PathOf("human-man"));
    }
}
