using PartyRpg.Kit.Content;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>Bundle selection: what a product starts with, and what stops it.</summary>
public sealed class BundleSelectionTests
{
    private static readonly ContentLayout Layout = new("packs", "imports", "bundles");

    [Fact]
    public void A_bundle_resolves_the_packs_it_names()
    {
        InMemoryContentSource source = new InMemoryContentSource()
            .Add("packs/places/pack.json", Pack("places", "definitions", "places", "place"))
            .Add("packs/places/places.json", """{ "documentId": "places", "definitionKind": "place", "entries": [ { "id": "1" } ] }""")
            .Add("bundles/default/bundle.json", Bundle("default", "partyrpg", """["places"]"""));

        ContentBootstrapResult result = ContentBootstrap.Load(source, Layout, "default");

        Assert.True(result.IsValid);
        ResolvedBundle selection = Assert.IsType<ResolvedBundle>(result.Selection);
        Assert.Equal("default", selection.Bundle.BundleId);
        Assert.Single(selection.Packs);
        Assert.Equal("partyrpg", selection.Bundle.Ruleset);
    }

    [Fact]
    public void A_bundle_that_names_a_pack_which_is_not_there_stops_the_product()
    {
        InMemoryContentSource source = new InMemoryContentSource()
            .Add("bundles/default/bundle.json", Bundle("default", "partyrpg", """["absent-pack"]"""));

        ContentBootstrapResult result = ContentBootstrap.Load(source, Layout, "default");

        Assert.False(result.IsValid);
        Assert.Null(result.Selection);
        ContentValidationIssue issue = Assert.Single(result.Issues);
        Assert.Equal("bundle-pack-missing", issue.Code);
        Assert.Contains("absent-pack", issue.Message);
    }

    [Fact]
    public void A_bundle_selects_the_packs_it_names_and_the_catalog_reads_only_those()
    {
        InMemoryContentSource source = new InMemoryContentSource()
            .Add("packs/places/pack.json", Pack("places", "definitions", "places", "place"))
            .Add("packs/places/places.json", """{ "documentId": "places", "definitionKind": "place", "entries": [ { "id": "1" } ] }""")
            .Add("packs/monsters/pack.json", Pack("monsters", "definitions", "monsters", "monster"))
            .Add("packs/monsters/monsters.json", """{ "documentId": "monsters", "definitionKind": "monster", "entries": [ { "id": "7" } ] }""")
            .Add("packs/scenario/pack.json", Pack("scenario", "scenario", "scenario", "scenario-start"))
            .Add("packs/scenario/scenario.json", """{ "documentId": "scenario", "definitionKind": "scenario-start", "entries": [ { "id": "start", "place": "1" } ] }""")
            .Add("bundles/default/bundle.json", Bundle("default", "partyrpg", """["places"]"""));

        ContentBootstrapResult result = ContentBootstrap.Load(source, Layout, "default");

        Assert.True(result.IsValid);
        LoadedPack selected = Assert.Single(result.Catalog.Packs);
        Assert.Equal("places", selected.PackId);
        // Every pack is still read and judged, so a selection is a narrowing of content rather than a way
        // to stop looking at it.
        Assert.Equal(3, ContentCatalogLoader.Load(source, Layout).Packs.Count);
        // What the bundle did not name contributes nothing: no definitions of its own, and no scenario start
        // to begin the game in.
        Assert.Null(result.Catalog.Find("monsters"));
        Assert.Null(result.Catalog.Find("scenario"));
        Assert.Equal("1", Assert.Single(result.Catalog.Entries("place")).Entry.Id);
        Assert.Empty(result.Catalog.Entries("monster"));
        Assert.Empty(result.Catalog.Entries("scenario-start"));
    }

    [Fact]
    public void An_empty_bundle_selects_none_of_the_packs_on_disk()
    {
        InMemoryContentSource source = new InMemoryContentSource()
            .Add("packs/places/pack.json", Pack("places", "definitions", "places", "place"))
            .Add("packs/places/places.json", """{ "documentId": "places", "definitionKind": "place", "entries": [ { "id": "1" } ] }""")
            .Add("packs/scenario/pack.json", Pack("scenario", "scenario", "scenario", "scenario-start"))
            .Add("packs/scenario/scenario.json", """{ "documentId": "scenario", "definitionKind": "scenario-start", "entries": [ { "id": "start", "place": "1" } ] }""")
            .Add("bundles/default/bundle.json", Bundle("default", "partyrpg", "[]"));

        ContentBootstrapResult result = ContentBootstrap.Load(source, Layout, "default");

        Assert.True(result.IsValid);
        Assert.NotNull(result.Selection);
        Assert.Empty(result.Catalog.Packs);
        Assert.Empty(result.Catalog.Entries("place"));
        Assert.Empty(result.Catalog.Entries("scenario-start"));
    }

    [Fact]
    public void The_bundle_and_the_packs_it_resolved_to_are_both_reported()
    {
        InMemoryContentSource source = new InMemoryContentSource()
            .Add("packs/places/pack.json", Pack("places", "definitions", "places", "place"))
            .Add("packs/places/places.json", """{ "documentId": "places", "definitionKind": "place", "entries": [ { "id": "1" } ] }""")
            .Add("packs/scenario/pack.json", Pack("scenario", "scenario", "scenario", "scenario-start"))
            .Add("packs/scenario/scenario.json", """{ "documentId": "scenario", "definitionKind": "scenario-start", "entries": [ { "id": "start", "place": "1" } ] }""")
            .Add("bundles/default/bundle.json", Bundle("default", "partyrpg", """["places", "scenario"]"""));

        ContentBootstrapResult result = ContentBootstrap.Load(source, Layout, "default");

        ResolvedBundle selection = Assert.IsType<ResolvedBundle>(result.Selection);
        Assert.Equal(2, selection.Packs.Count);
        Assert.Equal(2, result.Catalog.Packs.Count);
        Assert.Equal(["places", "scenario"], result.Catalog.Packs.Select(pack => pack.PackId));
    }

    [Fact]
    public void A_bundle_that_names_the_same_pack_twice_is_refused()
    {
        InMemoryContentSource source = new InMemoryContentSource()
            .Add("packs/places/pack.json", Pack("places", "definitions", "places", "place"))
            .Add("packs/places/places.json", """{ "documentId": "places", "definitionKind": "place", "entries": [ { "id": "1" } ] }""")
            .Add("bundles/default/bundle.json", Bundle("default", "partyrpg", """["places", "places"]"""));

        ContentBootstrapResult result = ContentBootstrap.Load(source, Layout, "default");

        Assert.False(result.IsValid);
        Assert.Null(result.Selection);
        Assert.Contains(result.Issues, issue => issue.Code == "bundle-pack-repeated" && issue.Message.Contains("places"));
    }

    [Fact]
    public void A_selection_that_did_not_come_from_the_catalog_is_refused()
    {
        InMemoryContentSource source = new InMemoryContentSource()
            .Add("packs/places/pack.json", Pack("places", "definitions", "places", "place"))
            .Add("packs/places/places.json", """{ "documentId": "places", "definitionKind": "place", "entries": [ { "id": "1" } ] }""");

        ContentCatalog catalog = ContentCatalogLoader.Load(source, Layout);
        LoadedPack invented = new(
            new PackManifest(1, "invented", ContentPackKind.Definitions, new ContentProvenance("authored"), []),
            []);

        ArgumentException error = Assert.Throws<ArgumentException>(() => catalog.Selected([invented]));

        Assert.Contains("invented", error.Message);
    }

    [Fact]
    public void Content_that_has_not_been_generated_yet_is_not_a_failure()
    {
        InMemoryContentSource source = new InMemoryContentSource();

        ContentBootstrapResult result = ContentBootstrap.Load(source, Layout, "default");

        Assert.True(result.IsValid);
        Assert.Null(result.Selection);
        Assert.Empty(result.Catalog.Packs);
    }

    [Fact]
    public void Asking_for_a_bundle_that_does_not_exist_is_a_failure_when_others_do()
    {
        InMemoryContentSource source = new InMemoryContentSource()
            .Add("bundles/default/bundle.json", Bundle("default", "partyrpg", "[]"));

        ContentBootstrapResult result = ContentBootstrap.Load(source, Layout, "missing");

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "requested-bundle-missing" && issue.Message.Contains("default"));
    }

    [Fact]
    public void A_tuning_pack_named_as_content_is_refused()
    {
        InMemoryContentSource source = new InMemoryContentSource()
            .Add("packs/places/pack.json", Pack("places", "definitions", "places", "place"))
            .Add("packs/places/places.json", """{ "documentId": "places", "definitionKind": "place", "entries": [ { "id": "1" } ] }""")
            .Add("bundles/default/bundle.json", """
                { "schemaVersion": 1, "bundleId": "default", "ruleset": "partyrpg", "contentPacks": [], "tuningPack": "places", "description": "x" }
                """);

        ContentBootstrapResult result = ContentBootstrap.Load(source, Layout, "default");

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "bundle-tuning-wrong-kind");
    }

    [Fact]
    public void A_broken_pack_the_bundle_does_not_name_is_refused_as_not_selected_with_where_it_was_read_from()
    {
        InMemoryContentSource source = new InMemoryContentSource()
            .Add("packs/places/pack.json", Pack("places", "definitions", "places", "place"))
            .Add("packs/places/places.json", """{ "documentId": "places", "definitionKind": "place", "entries": [ { "id": "1" } ] }""")
            .Add("packs/leftover/pack.json", Pack("leftover", "definitions", "leftover", "scenario-start"))
            .Add("packs/leftover/leftover.json", "{ this is not json")
            .Add("bundles/default/bundle.json", Bundle("default", "partyrpg", """["places"]"""));

        ContentBootstrapResult result = ContentBootstrap.Load(source, Layout, "default");

        // The whole root is judged, so the start is refused — but the refusal says the broken pack is not part
        // of the game the bundle chose, why, and the directory it came from, rather than reading as a defect in
        // the selection.
        Assert.False(result.IsValid);
        Assert.All(result.Issues, each => Assert.NotNull(each.NotSelected));
        ContentValidationIssue issue = Assert.Single(result.Issues, each => each.Code == "document-not-json");
        Assert.Equal("leftover", issue.PackId);
        Assert.NotNull(issue.NotSelected);
        Assert.Contains("bundle 'default' does not name it", issue.NotSelected, StringComparison.Ordinal);
        Assert.Contains("'packs/leftover'", issue.NotSelected, StringComparison.Ordinal);
        Assert.Contains(issue.NotSelected, issue.ToString(), StringComparison.Ordinal);
        // The selection is still exactly what the bundle named.
        Assert.Equal("places", Assert.Single(result.Catalog.Packs).PackId);
    }

    [Fact]
    public void A_broken_pack_the_bundle_names_is_refused_without_a_not_selected_mark()
    {
        InMemoryContentSource source = new InMemoryContentSource()
            .Add("packs/places/pack.json", Pack("places", "definitions", "places", "place"))
            .Add("packs/places/places.json", "{ this is not json")
            .Add("bundles/default/bundle.json", Bundle("default", "partyrpg", """["places"]"""));

        ContentBootstrapResult result = ContentBootstrap.Load(source, Layout, "default");

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "document-not-json");
        Assert.All(result.Issues, issue => Assert.Null(issue.NotSelected));
    }

    [Fact]
    public void An_unselected_root_reports_no_bundle_rather_than_a_name()
    {
        InMemoryContentSource source = new InMemoryContentSource();

        ContentBootstrapResult result = ContentBootstrap.Load(source, Layout, null);

        Assert.True(result.IsValid);
        Assert.Null(result.Selection);
    }

    private static string Pack(string packId, string kind, string documentId, string definitionKind) =>
        $$"""
        {
          "schemaVersion": 1,
          "packId": "{{packId}}",
          "kind": "{{kind}}",
          "provenance": { "description": "authored" },
          "documents": [ { "path": "{{packId}}.json", "documentId": "{{documentId}}", "definitionKind": "{{definitionKind}}" } ]
        }
        """;

    private static string Bundle(string bundleId, string ruleset, string packs) =>
        $$"""
        { "schemaVersion": 1, "bundleId": "{{bundleId}}", "ruleset": "{{ruleset}}", "contentPacks": {{packs}}, "description": "x" }
        """;
}
