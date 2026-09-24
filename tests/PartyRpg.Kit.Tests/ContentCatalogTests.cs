using PartyRpg.Kit.Content;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>The content loader and validator, over in-memory content roots.</summary>
public sealed class ContentCatalogTests
{
    private static readonly ContentLayout Layout = new("packs", "imports", "bundles");

    [Fact]
    public void A_valid_pack_loads_with_its_documents_and_entries()
    {
        InMemoryContentSource source = new InMemoryContentSource()
            .Add("packs/places/pack.json", Manifest("places", "definitions", documents: Document("places.json", "places", "place")))
            .Add("packs/places/places.json", """
                { "documentId": "places", "definitionKind": "place", "entries": [ { "id": "1", "name": "One" }, { "id": "2", "name": "Two" } ] }
                """);

        ContentCatalog catalog = ContentCatalogLoader.Load(source, Layout);

        Assert.True(catalog.IsValid);
        LoadedPack pack = Assert.Single(catalog.Packs);
        Assert.Equal("places", pack.PackId);
        Assert.Equal(ContentPackKind.Definitions, pack.Manifest.Kind);
        Assert.Equal(2, catalog.Entries("place").Count());
        Assert.Equal("One", catalog.Entries("place").First().Entry.GetString("name"));
    }

    [Fact]
    public void A_directory_without_a_manifest_is_named_rather_than_ignored()
    {
        InMemoryContentSource source = new InMemoryContentSource().Add("packs/notes/readme.txt", "not a pack");

        ContentCatalog catalog = ContentCatalogLoader.Load(source, Layout);

        ContentValidationIssue issue = Assert.Single(catalog.Issues);
        Assert.Equal("manifest-missing", issue.Code);
        Assert.Contains("notes", issue.Message);
    }

    [Fact]
    public void A_pack_whose_id_disagrees_with_its_directory_is_rejected()
    {
        InMemoryContentSource source = new InMemoryContentSource()
            .Add("packs/places/pack.json", Manifest("somewhere-else", "definitions", documents: Document("places.json", "places", "place")))
            .Add("packs/places/places.json", """{ "documentId": "places", "definitionKind": "place", "entries": [] }""");

        ContentCatalog catalog = ContentCatalogLoader.Load(source, Layout);

        Assert.Contains(catalog.Issues, issue => issue.Code == "pack-id-mismatch");
    }

    [Fact]
    public void Two_packs_that_declare_the_same_entry_name_both_sides()
    {
        InMemoryContentSource source = new InMemoryContentSource()
            .Add("packs/first/pack.json", Manifest("first", "definitions", documents: Document("a.json", "first-places", "place")))
            .Add("packs/first/a.json", """{ "documentId": "first-places", "definitionKind": "place", "entries": [ { "id": "7" } ] }""")
            .Add("packs/second/pack.json", Manifest("second", "definitions", documents: Document("b.json", "second-places", "place")))
            .Add("packs/second/b.json", """{ "documentId": "second-places", "definitionKind": "place", "entries": [ { "id": "7" } ] }""");

        ContentCatalog catalog = ContentCatalogLoader.Load(source, Layout);

        ContentValidationIssue issue = Assert.Single(catalog.Issues);
        Assert.Equal("entry-id-reused", issue.Code);
        Assert.Contains("first", issue.Message);
        Assert.Equal("second", issue.PackId);
    }

    [Fact]
    public void A_reference_to_something_no_pack_declares_is_an_issue()
    {
        InMemoryContentSource source = new InMemoryContentSource()
            .Add("packs/places/pack.json", Manifest("places", "definitions", documents:
                """{ "path": "places.json", "documentId": "places", "definitionKind": "place", "references": [ "monster:12" ] }"""))
            .Add("packs/places/places.json", """{ "documentId": "places", "definitionKind": "place", "entries": [ { "id": "1" } ] }""");

        ContentCatalog catalog = ContentCatalogLoader.Load(source, Layout);

        ContentValidationIssue issue = Assert.Single(catalog.Issues);
        Assert.Equal("reference-unresolved", issue.Code);
        Assert.Contains("monster:12", issue.Message);
    }

    [Fact]
    public void An_imported_pack_must_say_which_edition_it_came_from()
    {
        InMemoryContentSource source = new InMemoryContentSource()
            .Add("imports/tables/pack.json", Manifest("tables", "world", provenance: "\"description\": \"generated\"", documents: Document("t.json", "tables", "table")))
            .Add("imports/tables/t.json", """{ "documentId": "tables", "definitionKind": "table", "entries": [ { "id": "one" } ] }""");

        ContentCatalog catalog = ContentCatalogLoader.Load(source, Layout);

        ContentValidationIssue issue = Assert.Single(catalog.Issues);
        Assert.Equal("provenance-incomplete", issue.Code);
    }

    [Fact]
    public void A_schema_version_this_build_does_not_read_is_refused_by_name()
    {
        InMemoryContentSource source = new InMemoryContentSource()
            .Add("packs/places/pack.json", """{ "schemaVersion": 99, "packId": "places", "kind": "definitions", "provenance": { "description": "x" }, "documents": [] }""");

        ContentCatalog catalog = ContentCatalogLoader.Load(source, Layout);

        Assert.Contains(catalog.Issues, issue => issue.Code == "schema-version-unsupported");
        Assert.Contains("99", catalog.Issues[0].Message);
    }

    [Fact]
    public void Require_valid_reports_every_problem_at_once()
    {
        InMemoryContentSource source = new InMemoryContentSource()
            .Add("packs/broken/pack.json", """{ "schemaVersion": 1, "packId": "wrong", "kind": "nonsense", "provenance": { "description": "x" }, "documents": [] }""");

        ContentCatalog catalog = ContentCatalogLoader.Load(source, Layout);

        ContentValidationException error = Assert.Throws<ContentValidationException>(() => catalog.RequireValid());
        Assert.True(error.Issues.Count >= 2);
        Assert.Contains("problem", error.Message);
    }

    [Fact]
    public void A_content_root_on_disk_cannot_be_escaped_by_a_pack_path()
    {
        string root = Path.Combine(Path.GetTempPath(), $"content-root-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            FileContentSource source = new(root);
            Assert.Throws<ContentValidationException>(() => source.ReadText("../outside.json"));
            Assert.Empty(source.ListFiles("absent"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string Manifest(
        string packId,
        string kind,
        string? provenance = null,
        string? documents = null) =>
        $$"""
        {
          "schemaVersion": 1,
          "packId": "{{packId}}",
          "kind": "{{kind}}",
          "provenance": { {{provenance ?? "\"description\": \"authored\""}} },
          "documents": [ {{documents ?? string.Empty}} ]
        }
        """;

    private static string Document(string path, string documentId, string definitionKind) =>
        $$"""{ "path": "{{path}}", "documentId": "{{documentId}}", "definitionKind": "{{definitionKind}}" }""";
}
