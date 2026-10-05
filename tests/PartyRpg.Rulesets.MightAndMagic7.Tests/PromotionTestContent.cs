using PartyRpg.Kit.Content;

namespace PartyRpg.Rulesets.MightAndMagic7.Tests;

/// <summary>Explicitly supplies the authored ladder to focused fixtures that omit the normal bundle.</summary>
internal static class PromotionTestContent
{
    internal static (string Path, string Text)[] Files =>
    [
        ("partyrpg/content-packs/promotion-test/pack.json", """
          {"schemaVersion":1,"packId":"promotion-test","kind":"definitions","provenance":{"description":"normal authored rank content"},
           "documents":[{"path":"promotions.json","documentId":"promotion-ladders","definitionKind":"promotion"}]}
          """),
        ("partyrpg/content-packs/promotion-test/promotions.json", File.ReadAllText(Repository.PathOf("content/partyrpg/content-packs/mm7-new-game/promotions.json")))
    ];

    internal static ContentCatalog With(ContentCatalog? catalog)
    {
        if (catalog?.Entries("promotion").Any() == true) return catalog;
        var source = new InMemoryContentSource();
        foreach (var file in Files) source.Add(file.Path, file.Text);
        var ranks = ContentCatalogLoader.Load(source, ContentLayout.Under("partyrpg")).RequireValid();
        return ContentCatalog.From([.. catalog?.Packs ?? [], .. ranks.Packs], [.. catalog?.Issues ?? []]).RequireValid();
    }

    internal static MightAndMagic7Promotions Read(ContentCatalog? catalog) => MightAndMagic7Promotions.Read(With(catalog));
}
