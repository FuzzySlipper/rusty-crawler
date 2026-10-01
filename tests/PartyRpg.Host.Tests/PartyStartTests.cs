using System.Text.Json;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using PartyRpg.Rulesets.MightAndMagic7;
using Rusty.Engine;
using Rusty.Engine.Debugging;
using Rusty.Engine.NativeProduct;
using Xunit;

namespace PartyRpg.Host.Tests;

/// <summary>
/// Which start a new session's party takes — creation, or the party the scenario fixes — through the product
/// itself.
/// </summary>
/// <remarks>
/// <para>
/// The product always declares its creation screen, so before the scenario could say which start it means a
/// scenario's own party was unreachable in play: the screen replaced it. The scenario start's <c>party</c>
/// word now decides, with creation the default, and these cases drive <see cref="CrawlerProduct"/> rather than
/// the ruleset seam so the switch is proved where a player meets it: the mode the product starts in, the party
/// it plays, the world composed over that party, and the word the composition block publishes.
/// </para>
/// <para>
/// The content is shaped like the operator's own root: one pack of places, and scenario packs that each carry
/// a start and a party — two of them on disk and one selected, which is also the shape a leftover scenario
/// pack leaves behind.
/// </para>
/// </remarks>
public sealed class PartyStartTests
{
    [Fact]
    public void A_scenario_that_fixes_its_party_starts_the_product_playing_that_party_with_no_creation_screen()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(
            Content(["world", "scenario-b"], partyB: "scenario"));

        using CrawlerProduct product = new(context, ProductTestContext.NoVariables);
        product.Start();
        product.Update(ProductTestContext.Update(1, 1));

        // No creation screen: the session plays the party the selected scenario fixes, in the place that scenario
        // starts in — not the other scenario's, which sits on disk unselected.
        Assert.Equal(SessionMode.Running, product.Mode);
        ProjectedNode value = ProjectedNode.Of(ui.Latest().Value);
        Assert.Equal("scenario", value.Field("composition").Field("partyStart").AsString());
        Assert.False(value.Field("creation").Field("active").AsBoolean());
        Assert.True(value.Field("party").Field("present").AsBoolean());
        Assert.Equal(1d, value.Field("party").Field("members").AsNumber());
        Assert.Equal(22d, value.Field("party").Field("coins").AsNumber());
        Assert.Equal("2", value.Field("world").Field("place").AsString());
        Assert.Equal("Beta Keep", value.Field("world").Field("name").AsString());
        Assert.True(value.Field("clock").Field("present").AsBoolean());

        // The live check's reading says the same: the session is on the scenario's start.
        IDebugCommandCatalog catalog = GeneratedDebugCommandCatalogFactory.Create(product);
        DebugCommandResult observation = catalog.Execute("playtest.observe");
        Assert.True(observation.Succeeded, observation.Message);
        using JsonDocument observed = JsonDocument.Parse(observation.Message);
        Assert.Equal("scenario", observed.RootElement.GetProperty("composition").GetProperty("partyStart").GetString());
        Assert.Equal(2, observed.RootElement.GetProperty("composition").GetProperty("contentPacks").GetInt32());
    }

    [Fact]
    public void A_scenario_that_does_not_say_starts_the_product_in_creation()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(
            Content(["world", "creation-tables", "scenario-b"], partyB: null));

        using CrawlerProduct product = new(context, ProductTestContext.NoVariables);
        product.Start();
        product.Update(ProductTestContext.Update(1, 1));

        // The default is the player-facing one: the screen, even though the scenario carries a party of its own.
        Assert.Equal(SessionMode.Creating, product.Mode);
        ProjectedNode value = ProjectedNode.Of(ui.Latest().Value);
        Assert.Equal("creation", value.Field("composition").Field("partyStart").AsString());
        Assert.True(value.Field("creation").Field("active").AsBoolean());
        Assert.False(value.Field("party").Field("present").AsBoolean());
    }

    [Fact]
    public void A_scenario_that_asks_for_creation_starts_the_product_in_creation()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(
            Content(["world", "creation-tables", "scenario-b"], partyB: "creation"));

        using CrawlerProduct product = new(context, ProductTestContext.NoVariables);
        product.Start();
        product.Update(ProductTestContext.Update(1, 1));

        Assert.Equal(SessionMode.Creating, product.Mode);
        Assert.Equal("creation", ProjectedNode.Of(ui.Latest().Value).Field("composition").Field("partyStart").AsString());
    }

    [Fact]
    public void A_scenario_that_fixes_a_party_it_cannot_build_stops_the_product_by_name()
    {
        // The scenario's party keeps the rules it always had: a member with no class is refused while the session
        // is composed, now on the product's own path rather than only on a host without creation.
        (string Path, string Text)[] content =
        [
            .. Content(["world", "scenario-b"], partyB: "scenario").Select(file => file.Path.EndsWith("/scenario-b/party.json", StringComparison.Ordinal)
                ? (file.Path, file.Text.Replace("\"class\": \"Knight\", ", string.Empty, StringComparison.Ordinal))
                : file),
        ];
        (ProductCreateContext context, _) = ProductTestContext.Create(content);

        ContentValidationException error = Assert.Throws<ContentValidationException>(
            () => new CrawlerProduct(context, ProductTestContext.NoVariables));

        Assert.Contains(error.Issues, issue => issue.PackId == "scenario-b");
    }

    [Fact]
    public void A_scenario_that_asks_for_its_own_party_and_states_none_stops_the_product_by_name()
    {
        // The selected scenario states a start and no party document at all: what is proved is a start that asks
        // for a party the selection never states, not a missing document.
        (ProductCreateContext context, _) = ProductTestContext.Create(
        [
            ProductTestContext.Bundle(BuiltInBundles.Default, "world", "scenario-b"),
            .. Places(),
            .. Scenario("scenario-b", "Beta", "2", coins: 22, party: "scenario", withParty: false),
        ]);

        ContentValidationException error = Assert.Throws<ContentValidationException>(
            () => new CrawlerProduct(context, ProductTestContext.NoVariables));

        ContentValidationIssue issue = Assert.Single(error.Issues);
        Assert.Equal("scenario-start-party-missing", issue.Code);
        Assert.Equal("scenario-b", issue.PackId);
    }

    [Fact]
    public void A_party_word_that_is_not_a_start_stops_the_product_by_name()
    {
        (ProductCreateContext context, _) = ProductTestContext.Create(
            Content(["world", "scenario-b"], partyB: "scripted"));

        ContentValidationException error = Assert.Throws<ContentValidationException>(
            () => new CrawlerProduct(context, ProductTestContext.NoVariables));

        ContentValidationIssue issue = Assert.Single(error.Issues);
        Assert.Equal("scenario-start-party-unknown", issue.Code);
        Assert.Contains("'scripted'", issue.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_scenario_that_asks_for_creation_from_a_host_with_no_creation_screen_is_refused_rather_than_played_as_its_party()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(
            Content(["world", "scenario-b"], partyB: "creation"));

        ContentValidationException error = Assert.Throws<ContentValidationException>(() =>
            MightAndMagic7Ruleset.Instance.CreateSession(ProductTestContext.RulesetContext(context, ui, creation: false)));

        Assert.Equal("scenario-start-creation-unoffered", Assert.Single(error.Issues).Code);
    }

    [Fact]
    public void A_host_with_no_creation_screen_plays_the_scenario_party_of_a_start_that_does_not_say()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(
            Content(["world", "scenario-b"], partyB: null));

        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            ProductTestContext.RulesetContext(context, ui, creation: false));
        session.Start();
        session.Update(ProductTestContext.Update(1, 1));

        Assert.Equal(SessionMode.Running, session.Mode);
        ProjectedNode value = ProjectedNode.Of(ui.Latest().Value);
        Assert.Equal("scenario", value.Field("composition").Field("partyStart").AsString());
        Assert.Equal(22d, value.Field("party").Field("coins").AsNumber());
    }

    /// <summary>The operator's root in miniature: places, the creation tables, and two scenario packs.</summary>
    /// <param name="bundlePacks">The packs the shipped bundle names.</param>
    /// <param name="partyB">The party word the selected scenario's start states, or null to leave it out.</param>
    private static (string Path, string Text)[] Content(string[] bundlePacks, string? partyB) =>
    [
        ProductTestContext.Bundle(BuiltInBundles.Default, bundlePacks),
        .. Places(),
        .. ProductTestContext.CreationTables(),
        .. Scenario("scenario-a", "Alpha", "1", coins: 11, party: "scenario"),
        .. Scenario("scenario-b", "Beta", "2", coins: 22, party: partyB),
    ];

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
              "entries": [ {{Place("1", "Alpha Keep")}}, {{Place("2", "Beta Keep")}} ]
            }
            """),
    ];

    private static (string Path, string Text)[] Scenario(string packId, string name, string place, int coins, string? party, bool withParty = true)
    {
        string partyDocument = withParty
            ? $", {{ \"path\": \"party.json\", \"documentId\": \"{packId}-party\", \"definitionKind\": \"scenario-party\" }}"
            : string.Empty;
        (string Path, string Text)[] files = Files();
        return withParty ? files : [.. files.Where(file => !file.Path.EndsWith("/party.json", StringComparison.Ordinal))];

        (string Path, string Text)[] Files() =>
    [
        ($"{ProductTestContext.ContentDirectory}/content-packs/{packId}/pack.json",
            $$"""
            {
              "schemaVersion": 1,
              "packId": "{{packId}}",
              "kind": "scenario",
              "provenance": { "description": "authored for a test" },
              "documents": [
                { "path": "start.json", "documentId": "{{packId}}-start", "definitionKind": "scenario-start" }{{partyDocument}}
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/{packId}/start.json",
            $$"""
            { "documentId": "{{packId}}-start", "definitionKind": "scenario-start", "entries": [ { "id": "start-{{packId}}", "place": "{{place}}", "entryPoint": "Party Start"{{(party is null ? string.Empty : $", \"party\": \"{party}\"")}} } ] }
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
    }

    private static string Place(string id, string name) =>
        $$"""
        { "id": "{{id}}", "kind": "interior", "name": "{{name}}", "respawnDays": 1,
          "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ] }
        """;
}
