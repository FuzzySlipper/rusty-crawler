using PartyRpg.Kit.Content;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>A ruleset's adjustable values: declared handles, read from a tuning pack, defaulted where unstated.</summary>
public sealed class TuningTests
{
    private static readonly ContentLayout Layout = new("packs", "imports", "bundles");
    private static readonly TuningHandle Hours = new("rest.hours", 8, 1, 24, "how long a night lasts");
    private static readonly TuningHandle Rate = new("price.rate", 0.5, 0, 1, "a share of the price", Whole: false);
    private static readonly TuningHandle[] Handles = [Hours, Rate];

    [Fact]
    public void A_stated_value_replaces_its_default_and_an_unstated_one_keeps_it()
    {
        TuningProfile profile = TuningProfile.Read(Catalog("""{ "id": "rest.hours", "value": 6 }"""), Handles);

        Assert.Equal(6, profile.Whole(Hours));
        Assert.True(profile.States(Hours));
        Assert.Equal(0.5, profile[Rate]);
        Assert.False(profile.States(Rate));
        Assert.Equal(8, TuningProfile.Read(null, Handles).Whole(Hours));
    }

    [Fact]
    public void Every_value_the_ruleset_cannot_take_is_named_at_once()
    {
        ContentValidationException refused = Assert.Throws<ContentValidationException>(() => TuningProfile.Read(
            Catalog(
                """{ "id": "rest.minutes", "value": 5 }""",
                """{ "id": "rest.hours", "value": 30 }""",
                """{ "id": "price.rate", "value": "half" }"""),
            Handles));

        Assert.Equal(
            ["tuning-unknown", "tuning-out-of-range", "tuning-unreadable"],
            refused.Issues.Select(issue => issue.Code));
        Assert.Contains("from 1 to 24", refused.Issues[1].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_whole_handle_refuses_a_fraction_and_a_fractional_one_takes_it()
    {
        Assert.Throws<ContentValidationException>(() => TuningProfile.Read(Catalog("""{ "id": "rest.hours", "value": 6.5 }"""), Handles));
        Assert.Equal(0.25, TuningProfile.Read(Catalog("""{ "id": "price.rate", "value": 0.25 }"""), Handles)[Rate]);
    }

    private static ContentCatalog Catalog(params string[] entries) =>
        ContentCatalogLoader.Load(
            new InMemoryContentSource()
                .Add("packs/tuning/pack.json", """
                    {
                      "schemaVersion": 1, "packId": "tuning", "kind": "tuning",
                      "provenance": { "description": "authored for a test" },
                      "documents": [ { "path": "tuning.json", "documentId": "tuning", "definitionKind": "tuning" } ]
                    }
                    """)
                .Add("packs/tuning/tuning.json", $$"""{ "documentId": "tuning", "definitionKind": "tuning", "entries": [ {{string.Join(", ", entries)}} ] }"""),
            Layout);
}
