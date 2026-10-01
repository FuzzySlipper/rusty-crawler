using PartyRpg.Kit.Content;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using PartyRpg.Rulesets.MightAndMagic7;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Host.Tests;

/// <summary>
/// The bundle's pack selection against the real composition: which packs load, which scenario start wins,
/// and what a selection that states none of it plays.
/// </summary>
/// <remarks>
/// <para>
/// The kit's own suite proves the selection rule where the catalog is resolved. What only this suite can
/// prove is that the world follows it end to end: several scenario packs sit under the content root, the
/// bundle names one of them, and the place the party begins in, the party it leads, and the count the panel
/// prints are that scenario's — while an empty bundle plays no world at all however much content is on disk.
/// </para>
/// <para>
/// The content is written the way the operator's own root is shaped: one pack carrying the places, and one
/// pack per scenario carrying a start and a party. Selecting a scenario is therefore a decision about a
/// whole game, not about a document.
/// </para>
/// </remarks>
public sealed class PackSelectionTests
{
    [Fact]
    public void The_bundle_selects_which_scenario_plays()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(Content(["world", "scenario-b"]));
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            ProductTestContext.RulesetContext(context, ui));
        session.Start();
        session.Update(ProductTestContext.Update(1, 1));

        ProjectedNode value = ProjectedNode.Of(ui.Latest().Value);
        ProjectedNode world = value.Field("world");
        // The panel's own line is the selection, and the world is the one that selection names: place 2 and
        // the party that goes with it, not whichever scenario pack happened to load first.
        Assert.Equal(2d, value.Field("composition").Field("contentPacks").AsNumber());
        Assert.Equal("2", world.Field("place").AsString());
        Assert.Equal("Beta Keep", world.Field("name").AsString());
        Assert.Equal(3d, world.Field("places").AsNumber());
        Assert.True(value.Field("party").Field("present").AsBoolean());
        Assert.Equal(22d, value.Field("party").Field("coins").AsNumber());
    }

    [Fact]
    public void A_bundle_that_names_no_packs_loads_no_world_and_no_party()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(Content([]));
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            ProductTestContext.RulesetContext(context, ui));
        session.Start();
        session.Update(ProductTestContext.Update(1, 1));

        // The panel says zero packs, and every block agrees with it: three places, three scenario starts, and
        // three scenario parties are sitting under the content root and none of them is loaded, so the session
        // reports no world and no party rather than a place nobody selected.
        ProjectedNode value = ProjectedNode.Of(ui.Latest().Value);
        Assert.Equal(BuiltInBundles.Default, value.Field("composition").Field("bundle").AsString());
        Assert.Equal(0d, value.Field("composition").Field("contentPacks").AsNumber());
        ProjectedNode world = value.Field("world");
        Assert.Equal(string.Empty, world.Field("place").AsString());
        Assert.Equal(string.Empty, world.Field("name").AsString());
        Assert.Equal(0d, world.Field("places").AsNumber());
        Assert.False(value.Field("party").Field("present").AsBoolean());
        Assert.Equal(0d, value.Field("party").Field("members").AsNumber());
    }

    [Fact]
    public void A_start_named_by_a_pack_the_bundle_did_not_select_can_never_win()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(Content(["world"]));
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            ProductTestContext.RulesetContext(context, ui));
        session.Start();
        session.Update(ProductTestContext.Update(1, 1));

        // The selected pack carries places, so the content root has a world in it — but the only starts are in
        // packs the bundle did not name, and a session with nowhere to begin reports no world rather than
        // borrowing a scenario nobody selected.
        ProjectedNode value = ProjectedNode.Of(ui.Latest().Value);
        Assert.Equal(1d, value.Field("composition").Field("contentPacks").AsNumber());
        Assert.Equal(0d, value.Field("world").Field("places").AsNumber());
        Assert.Equal(string.Empty, value.Field("world").Field("place").AsString());
        Assert.False(value.Field("party").Field("present").AsBoolean());
    }

    [Fact]
    public void A_broken_scenario_pack_the_bundle_did_not_name_stops_the_product_saying_it_is_not_selected()
    {
        // The live shape that caused this rule: a leftover scenario pack beside the one the bundle names — here
        // broken, so the product meets it. The whole root is judged, so the start is refused; what this proves
        // is that the refusal reads as the leftover's, not the selection's: not selected, and where it was read.
        (string Path, string Text)[] content =
        [
            .. Content(["world", "scenario-b"]).Select(file => file.Path.EndsWith("/scenario-a/start.json", StringComparison.Ordinal)
                ? (file.Path, "{ not json")
                : file),
        ];
        (ProductCreateContext context, _) = ProductTestContext.Create(content);

        ContentValidationException error = Assert.Throws<ContentValidationException>(
            () => new CrawlerProduct(context, ProductTestContext.NoVariables));

        Assert.All(error.Issues, issue => Assert.Equal("scenario-a", issue.PackId));
        Assert.All(error.Issues, issue => Assert.NotNull(issue.NotSelected));
        Assert.Contains(error.Issues, issue => issue.Code == "document-not-json");
        Assert.Contains($"bundle '{BuiltInBundles.Default}' does not name it", error.Message, StringComparison.Ordinal);
        Assert.Contains($"'{ProductTestContext.ContentDirectory}/content-packs/scenario-a'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Two_starts_inside_the_selection_are_refused_with_both_candidates_named()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(
            Content(["world", "scenario-a", "scenario-c"]));

        ContentValidationException error = Assert.Throws<ContentValidationException>(() =>
            MightAndMagic7Ruleset.Instance.CreateSession(ProductTestContext.RulesetContext(context, ui)));

        // Which place the party begins in would otherwise be the order the packs loaded in, so the refusal
        // names both candidates: the pack, the document, the entry, and the place each one would start in.
        Assert.Equal(2, error.Issues.Count);
        Assert.All(error.Issues, issue => Assert.Equal("scenario-start-ambiguous", issue.Code));
        Assert.Contains("scenario-a", error.Message);
        Assert.Contains("scenario-c", error.Message);
        Assert.Contains("place '1'", error.Message);
        Assert.Contains("place '3'", error.Message);
    }

    [Fact]
    public void Two_parties_inside_one_pack_are_refused_with_both_candidates_named()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(
        [
            ProductTestContext.Bundle(BuiltInBundles.Default, "world", "twins"),
            .. Places(),
            .. Twins(),
        ]);

        ContentValidationException error = Assert.Throws<ContentValidationException>(() =>
            MightAndMagic7Ruleset.Instance.CreateSession(ProductTestContext.RulesetContext(context, ui)));

        // One pack, one start, and two answers to who the party is: which band the player leads would be the
        // order the document's entries happened to be read in, so the refusal names both candidates — the pack,
        // the document, the entry, and the members each one would have created the party from.
        Assert.Equal(2, error.Issues.Count);
        Assert.All(error.Issues, issue => Assert.Equal("scenario-party-ambiguous", issue.Code));
        Assert.All(error.Issues, issue => Assert.Equal("twins", issue.PackId));
        Assert.All(error.Issues, issue => Assert.Equal("twins-party", issue.DocumentId));
        Assert.Contains("party-one", error.Message);
        Assert.Contains("party-two", error.Message);
        Assert.Contains("with 1 member", error.Message);
        Assert.Contains("with 2 members", error.Message);
    }

    /// <summary>The operator's content root in miniature: one place pack, and one pack per scenario.</summary>
    /// <param name="bundlePacks">The packs the shipped bundle names, in load order.</param>
    private static (string Path, string Text)[] Content(string[] bundlePacks) =>
    [
        ProductTestContext.Bundle(BuiltInBundles.Default, bundlePacks),
        .. Places(),
        .. Scenario("scenario-a", "Alpha", "1", coins: 11),
        .. Scenario("scenario-b", "Beta", "2", coins: 22),
        .. Scenario("scenario-c", "Gamma", "3", coins: 33),
    ];

    /// <summary>The pack that carries the places every scenario begins in one of.</summary>
    private static (string Path, string Text)[] Places() =>
    [
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/pack.json",
            """
            {
              "schemaVersion": 1,
              "packId": "world",
              "kind": "definitions",
              "provenance": { "description": "authored for a test" },
              "documents": [ { "path": "places.json", "documentId": "places", "definitionKind": "place" } ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/places.json",
            $$"""
            {
              "documentId": "places",
              "definitionKind": "place",
              "entries": [
                {{Place("1", "Alpha Keep")}},
                {{Place("2", "Beta Keep")}},
                {{Place("3", "Gamma Keep")}}
              ]
            }
            """),
    ];

    /// <summary>One scenario pack: where it starts, and the party it starts with.</summary>
    /// <remarks>
    /// Document and entry ids are the pack's own, because the catalog validates identity across every pack
    /// it reads: two scenarios that both called their document "start" would be a content defect rather than
    /// two scenarios.
    /// </remarks>
    private static (string Path, string Text)[] Scenario(string packId, string name, string place, int coins) =>
    [
        ($"{ProductTestContext.ContentDirectory}/content-packs/{packId}/pack.json",
            $$"""
            {
              "schemaVersion": 1,
              "packId": "{{packId}}",
              "kind": "scenario",
              "provenance": { "description": "authored for a test" },
              "documents": [
                { "path": "start.json", "documentId": "{{packId}}-start", "definitionKind": "scenario-start" },
                { "path": "party.json", "documentId": "{{packId}}-party", "definitionKind": "scenario-party" }
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/{packId}/start.json",
            $$"""
            { "documentId": "{{packId}}-start", "definitionKind": "scenario-start", "entries": [ { "id": "start-{{packId}}", "place": "{{place}}", "entryPoint": "Party Start" } ] }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/{packId}/party.json",
            $$"""
            {
              "documentId": "{{packId}}-party",
              "definitionKind": "scenario-party",
              "entries": [
                {
                  "id": "party-{{packId}}", "coins": {{coins}}, "food": 6, "reputation": 0, "fame": 0,
                  "members": [
                    { "name": "{{name}}", "race": "Human", "class": "Knight", "level": 1, "hitPoints": 40, "spellPoints": 0 }
                  ]
                }
              ]
            }
            """),
    ];

    /// <summary>One scenario pack that answers who the party is twice: its one start, and two parties.</summary>
    /// <remarks>
    /// The two parties are two entries of one document, because that is the shape the start rule cannot reach:
    /// two scenario packs would each state a start and be refused by that rule, while one pack with one start
    /// has nothing but the party rule to say which band the player leads.
    /// </remarks>
    private static (string Path, string Text)[] Twins() =>
    [
        ($"{ProductTestContext.ContentDirectory}/content-packs/twins/pack.json",
            """
            {
              "schemaVersion": 1,
              "packId": "twins",
              "kind": "scenario",
              "provenance": { "description": "authored for a test" },
              "documents": [
                { "path": "start.json", "documentId": "twins-start", "definitionKind": "scenario-start" },
                { "path": "party.json", "documentId": "twins-party", "definitionKind": "scenario-party" }
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/twins/start.json",
            """{ "documentId": "twins-start", "definitionKind": "scenario-start", "entries": [ { "id": "start-twins", "place": "1", "entryPoint": "Party Start" } ] }"""),
        ($"{ProductTestContext.ContentDirectory}/content-packs/twins/party.json",
            """
            {
              "documentId": "twins-party",
              "definitionKind": "scenario-party",
              "entries": [
                {
                  "id": "party-one", "coins": 11, "food": 6, "reputation": 0, "fame": 0,
                  "members": [ { "name": "Alpha", "race": "Human", "class": "Knight", "level": 1, "hitPoints": 40, "spellPoints": 0 } ]
                },
                {
                  "id": "party-two", "coins": 22, "food": 6, "reputation": 0, "fame": 0,
                  "members": [
                    { "name": "Beta", "race": "Human", "class": "Knight", "level": 1, "hitPoints": 40, "spellPoints": 0 },
                    { "name": "Gamma", "race": "Human", "class": "Knight", "level": 1, "hitPoints": 40, "spellPoints": 0 }
                  ]
                }
              ]
            }
            """),
    ];

    private static string Place(string id, string name) =>
        $$"""
        { "id": "{{id}}", "kind": "interior", "name": "{{name}}", "respawnDays": 1,
          "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ] }
        """;
}
