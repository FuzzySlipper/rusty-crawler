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
