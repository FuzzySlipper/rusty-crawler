using PartyRpg.Kit.Content;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Services;
using PartyRpg.Kit.Sessions;
using PartyRpg.Rulesets.MightAndMagic7;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Host.Tests;

/// <summary>
/// This game's own answers about services, and the control the host declares for them.
/// </summary>
/// <remarks>
/// <para>
/// The ruleset's service policy is internal because nothing outside the product composes it, so this suite
/// reaches it through the ruleset's own friend declaration. What it proves is what no kit test can: the
/// prices the donor states over the multipliers the building table carries, that a guild gates its shelves
/// on party-carried membership, and that a staged counter can be walked into, bought from, identified,
/// repaired, sold back, and left with the party's one purse moving the whole way.
/// </para>
/// <para>
/// The session is composed through the ruleset's one entry with the use and service controls the host
/// declares, so the flow under test is the flow a player takes: the interaction mechanism reaches the
/// person, the service mechanism opens the counter, and the screen's commands arrive on the declared
/// contract.
/// </para>
/// </remarks>
public sealed class ServicePolicyTests
{
    private static readonly ServiceIntentNames ServiceControls = new(
        ProductIdentity.ServiceLeaveIntent,
        ProductIdentity.UiActionContract);

    private static readonly UseIntentNames UseControls = new(
        ProductIdentity.UseIntent,
        ProductIdentity.UseAction,
        ProductIdentity.UiActionContract);

    [Fact]
    public void The_service_controls_are_declared_in_code_in_the_project_file_and_in_the_companion()
    {
        string source = File.ReadAllText(Path.Combine(SourceDirectory(), "ProductIdentity.cs"));
        string project = File.ReadAllText(Path.Combine(SourceDirectory(), "PartyRpg.Host.csproj"));
        string product = File.ReadAllText(Path.Combine(SourceDirectory(), "CrawlerProduct.cs"));
        string ui = File.ReadAllText(Path.Combine(SourceDirectory(), "..", "ui", "main.ts"));

        string intent = Constant(source, "ServiceLeaveIntent");
        Assert.Equal(ServiceActions.Leave, intent);

        // Declared in code and in the project file, and mapped there: both halves are what make the key a
        // control, because the engine refuses a mapping whose intent was never declared.
        Assert.Contains($"RustyEngineProductInputIntent Include=\"{intent}\" Value=\"digital\"", project, StringComparison.Ordinal);
        Assert.Contains($"Intent=\"{intent}\"", project, StringComparison.Ordinal);
        Assert.Contains("Trigger=\"key:key-x:pressed\"", project, StringComparison.Ordinal);

        // The host hands the ruleset the declared names, and the companion sends the kit's own action names
        // on the product's contract: the counter's commands are the ones the session reads.
        Assert.Contains("new ServiceIntentNames(", product, StringComparison.Ordinal);
        Assert.Contains("Service: _service", product, StringComparison.Ordinal);
        foreach ((string constant, string action) in new[]
        {
            ("ACTION_SERVICE_BUY", ServiceActions.Buy),
            ("ACTION_SERVICE_SELL", ServiceActions.Sell),
            ("ACTION_SERVICE_IDENTIFY", ServiceActions.Identify),
            ("ACTION_SERVICE_REPAIR", ServiceActions.Repair),
            ("ACTION_SERVICE_TEACH", ServiceActions.Teach),
            ("ACTION_SERVICE_LEAVE", ServiceActions.Leave),
        })
        {
            AssertUiConstant(ui, constant, action);
        }

        AssertUiConstant(ui, "UI_ACTION_CONTRACT", Constant(source, "UiActionContract"));
        AssertUiConstant(ui, "UI_ACTION_INTENT", Constant(source, "UiActionIntent"));
    }

    [Fact]
    public void A_staged_shop_is_entered_bought_from_identified_repaired_sold_and_left()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(ShopContent());

        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            ProductTestContext.RulesetContext(context, ui) with { Use = UseControls, Service = ServiceControls });
        session.Start();

        // A session that holds the mechanism and stands at no counter says exactly that, which is a different
        // fact from a counter that is shut.
        ProjectedNode before = ProjectedNode.Of(ui.Latest().Value).Field("service");
        Assert.True(before.Field("available").AsBoolean());
        Assert.False(before.Field("open").AsBoolean());
        Assert.Equal("none", before.Field("outcome").AsString());

        // The admitted update faces the counter, and the declared use control walks the party in.
        session.Update(ProductTestContext.Update(1, 1));
        Assert.Equal("The Sword and Shield, kept by Bertram", ProjectedNode.Of(ui.Latest().Value).Field("interaction").Field("label").AsString());
        session.Update(ProductTestContext.Update(2, 1, ProductTestContext.Digital(ProductIdentity.UseIntent)));
        ProjectedNode opened = ProjectedNode.Of(ui.Latest().Value).Field("service");
        Assert.True(opened.Field("open").AsBoolean());
        Assert.Equal("applied", opened.Field("outcome").AsString());
        Assert.Equal("The Sword and Shield", opened.Field("name").AsString());
        Assert.Equal("Weapon Shop", opened.Field("kind").AsString());
        Assert.Equal("open", opened.Field("state").AsString());
        Assert.Equal("06:00–18:00", opened.Field("hours").AsString());
        Assert.Equal("buy", opened.Field("operations").Item(0).AsString());

        // What the shelves hold is priced by this game's policy over the content's multipliers: the sword is
        // worth a hundred and the shop asks half again, and the party carries no merchant skill to soften it.
        Assert.Equal(150, opened.Field("stock").Item(0).Field("price").AsNumber());
        Assert.Equal(2, opened.Field("stock").Item(0).Field("count").AsNumber());
        Assert.Equal(75, opened.Field("lessons").Item(0).Field("price").AsNumber());

        // Buying settles through the party's own purse: the same account a fare and a road charge.
        session.Update(ProductTestContext.Update(3, 1, ProductTestContext.Payload("""{"action":"service.buy","target":"stock:sword","count":2}""")));
        ProjectedNode bought = ProjectedNode.Of(ui.Latest().Value).Field("service");
        Assert.Equal("applied", bought.Field("outcome").AsString());
        Assert.Equal(300, bought.Field("paid").AsNumber());
        Assert.Equal(200, bought.Field("coins").AsNumber());
        Assert.Equal(0, bought.Field("stock").Item(0).Field("count").AsNumber());

        // The party's own items are what a counter would buy, and identifying and repairing are fees for item
        // state that move the same purse.
        ProjectedNode browsed = ProjectedNode.Of(ui.Latest().Value).Field("service");
        string instance = browsed.Field("sales").Item(0).Field("item").AsString();
        Assert.Equal(2, browsed.Field("sales").Length());
        // Selling is the donor's own sum: the item's worth over the shop's multiplier plus two, plus the
        // merchant's share of it, which a party with no merchant skill earns none of.
        Assert.Equal(28, browsed.Field("sales").Item(0).Field("price").AsNumber());

        session.Update(ProductTestContext.Update(4, 1, ProductTestContext.Payload($$"""{"action":"service.identify","target":"{{instance}}"}""")));
        ProjectedNode identified = ProjectedNode.Of(ui.Latest().Value).Field("service");
        Assert.Equal(75, identified.Field("paid").AsNumber());
        Assert.Equal(125, identified.Field("coins").AsNumber());
        Assert.True(identified.Field("sales").Item(0).Field("identified").AsBoolean());

        session.Update(ProductTestContext.Update(5, 1, ProductTestContext.Payload("""{"action":"service.repair","target":"1"}""")));
        ProjectedNode repaired = ProjectedNode.Of(ui.Latest().Value).Field("service");
        Assert.Equal("service-not-damaged", repaired.Field("code").AsString());

        // A lesson raises a member's own skill, and the fee comes out of the same purse. The scenario's
        // member already has the sword this counter might otherwise teach, so the lesson is the axe; a
        // lesson the member already stands at is refused rather than sold twice.
        session.Update(ProductTestContext.Update(6, 1, ProductTestContext.Payload("""{"action":"service.teach","target":"Axe","member":0}""")));
        ProjectedNode taught = ProjectedNode.Of(ui.Latest().Value).Field("service");
        Assert.Equal("applied", taught.Field("outcome").AsString());
        Assert.Equal(75, taught.Field("paid").AsNumber());
        Assert.Equal(50, taught.Field("coins").AsNumber());
        session.Update(ProductTestContext.Update(7, 1, ProductTestContext.Payload("""{"action":"service.teach","target":"Axe","member":0}""")));
        Assert.Equal("service-nothing-to-learn", ProjectedNode.Of(ui.Latest().Value).Field("service").Field("code").AsString());

        // Selling gives one of the party's items back to the counter and pays the party for it: the earning
        // direction of the same one path.
        session.Update(ProductTestContext.Update(8, 1, ProductTestContext.Payload($$"""{"action":"service.sell","target":"{{instance}}"}""")));
        ProjectedNode sold = ProjectedNode.Of(ui.Latest().Value).Field("service");
        Assert.Equal("applied", sold.Field("outcome").AsString());
        Assert.Equal(28, sold.Field("earned").AsNumber());
        Assert.Equal(78, sold.Field("coins").AsNumber());
        // What the shop bought sits on its shelves, and the party can buy the very item back.
        Assert.Equal(2, sold.Field("stock").Length());

        // The declared leave control ends the visit, and the party is in the street again.
        session.Update(ProductTestContext.Update(9, 1, ProductTestContext.Digital(ProductIdentity.ServiceLeaveIntent)));
        ProjectedNode left = ProjectedNode.Of(ui.Latest().Value).Field("service");
        Assert.False(left.Field("open").AsBoolean());
        Assert.Equal("leave", left.Field("action").AsString());
        Assert.Contains("leaves The Sword and Shield", left.Field("message").AsString(), StringComparison.Ordinal);
        Assert.Equal(78, left.Field("coins").AsNumber());
    }

    [Fact]
    public void The_party_s_own_merchant_skill_changes_what_the_same_counter_charges()
    {
        // The same content twice: a party with no merchant skill, and one whose member has trained it. The
        // counter is identical, so the difference is the policy reading the party.
        (ProductCreateContext plain, RecordingUiService plainUi) = ProductTestContext.Create(ShopContent());
        (ProductCreateContext trader, RecordingUiService traderUi) = ProductTestContext.Create(ShopContent(merchant: true));

        foreach ((ProductCreateContext context, RecordingUiService ui, int expected) in new[]
        {
            (plain, plainUi, 150),
            (trader, traderUi, 133),
        })
        {
            using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
                ProductTestContext.RulesetContext(context, ui) with { Use = UseControls, Service = ServiceControls });
            session.Start();
            session.Update(ProductTestContext.Update(1, 1));
            session.Update(ProductTestContext.Update(2, 1, ProductTestContext.Digital(ProductIdentity.UseIntent)));

            ProjectedNode shop = ProjectedNode.Of(ui.Latest().Value).Field("service");
            Assert.Equal(expected, shop.Field("stock").Item(0).Field("price").AsNumber());

            // A merchant also gets more for what it sells, which is the same discount arithmetic in the other
            // direction: the donor's selling price plus the merchant's share of the item's worth.
            session.Update(ProductTestContext.Update(3, 1, ProductTestContext.Payload("""{"action":"service.buy","target":"stock:sword","count":1}""")));
            ProjectedNode bought = ProjectedNode.Of(ui.Latest().Value).Field("service");
            string instance = bought.Field("sales").Item(0).Field("item").AsString();
            int expectedSale = expected == 150 ? 28 : 39;
            Assert.Equal(expectedSale, bought.Field("sales").Item(0).Field("price").AsNumber());
            session.Update(ProductTestContext.Update(4, 1, ProductTestContext.Payload($$"""{"action":"service.sell","target":"{{instance}}"}""")));
            Assert.Equal(expectedSale, ProjectedNode.Of(ui.Latest().Value).Field("service").Field("earned").AsNumber());
        }
    }

    [Fact]
    public void A_guild_gates_its_shelves_on_membership_the_party_carries()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(GuildContent());

        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            ProductTestContext.RulesetContext(context, ui) with { Use = UseControls, Service = ServiceControls });
        session.Start();
        session.Update(ProductTestContext.Update(1, 1));
        session.Update(ProductTestContext.Update(2, 1, ProductTestContext.Digital(ProductIdentity.UseIntent)));
        ProjectedNode guild = ProjectedNode.Of(ui.Latest().Value).Field("service");
        Assert.Equal("Fire Guild", guild.Field("name").AsString());
        Assert.Equal(0, guild.Field("memberships").Length());

        // Not a member: the shelf is refused by name, and nothing moves.
        session.Update(ProductTestContext.Update(3, 1, ProductTestContext.Payload("""{"action":"service.buy","target":"stock:spellbook","count":1}""")));
        ProjectedNode refused = ProjectedNode.Of(ui.Latest().Value).Field("service");
        Assert.Equal("refused", refused.Field("outcome").AsString());
        Assert.Equal("service-membership-required", refused.Field("code").AsString());
        Assert.Equal(500, refused.Field("coins").AsNumber());

        // Joining is one lesson, and it puts the membership on the party where the access check reads it.
        session.Update(ProductTestContext.Update(4, 1, ProductTestContext.Payload("""{"action":"service.teach","target":"guild.fire","member":0}""")));
        ProjectedNode joined = ProjectedNode.Of(ui.Latest().Value).Field("service");
        Assert.Equal("applied", joined.Field("outcome").AsString());
        Assert.Equal(75, joined.Field("paid").AsNumber());
        Assert.Equal(1, joined.Field("memberships").Length());
        Assert.Equal("Fire Guild membership", joined.Field("memberships").Item(0).AsString());

        // The same shelf is served now, and a non-member lesson behind the same gate is not sold twice.
        session.Update(ProductTestContext.Update(5, 1, ProductTestContext.Payload("""{"action":"service.buy","target":"stock:spellbook","count":1}""")));
        ProjectedNode member = ProjectedNode.Of(ui.Latest().Value).Field("service");
        Assert.Equal("applied", member.Field("outcome").AsString());
        Assert.Equal(125, member.Field("coins").AsNumber());
        session.Update(ProductTestContext.Update(6, 1, ProductTestContext.Payload("""{"action":"service.teach","target":"guild.fire","member":0}""")));
        Assert.Equal("service-membership-held", ProjectedNode.Of(ui.Latest().Value).Field("service").Field("code").AsString());
    }

    /// <summary>
    /// A shop, its street, and the party that walks in: one service definition, the item table its prices
    /// are read from, the place the counter stands in, and the scenario's party.
    /// </summary>
    private static (string Path, string Text)[] ShopContent(bool merchant = false) =>
    [
        ProductTestContext.Bundle("partyrpg-default", "world"),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/pack.json",
            """
            {
              "schemaVersion": 1,
              "packId": "world",
              "kind": "definitions",
              "provenance": { "description": "authored for a test" },
              "documents": [
                { "path": "places.json", "documentId": "places", "definitionKind": "place" },
                { "path": "start.json", "documentId": "start", "definitionKind": "scenario-start" },
                { "path": "party.json", "documentId": "party", "definitionKind": "scenario-party" },
                { "path": "services.json", "documentId": "services", "definitionKind": "service" },
                { "path": "items.json", "documentId": "items", "definitionKind": "item" }
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/places.json",
            """
            {
              "documentId": "places",
              "definitionKind": "place",
              "entries": [
                { "id": "1", "kind": "interior", "name": "The Sword and Shield", "respawnDays": 7,
                  "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
                  "placements": [ { "id": "sword-and-shield", "kind": "service", "x": 100, "y": 0, "z": 0 } ] }
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/start.json",
            """
            { "documentId": "start", "definitionKind": "scenario-start", "entries": [ { "id": "start", "place": "1", "entryPoint": "Party Start" } ] }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/party.json",
            $$"""
            {
              "documentId": "party",
              "definitionKind": "scenario-party",
              "entries": [
                { "id": "party", "coins": 500, "food": 6, "reputation": 0, "fame": 0,
                  "members": [ {{Member(merchant)}} ] }
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/items.json",
            """
            {
              "documentId": "items",
              "definitionKind": "item",
              "entries": [
                { "id": "sword", "name": "A fine sword", "value": 100 },
                { "id": "shield", "name": "A shield", "value": 40 }
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/services.json",
            """
            {
              "documentId": "services",
              "definitionKind": "service",
              "entries": [
                {
                  "id": "sword-and-shield",
                  "kind": "Weapon Shop",
                  "name": "The Sword and Shield",
                  "proprietor": "Bertram",
                  "operations": [ "buy", "sell", "identify", "repair", "teach" ],
                  "openHour": 6,
                  "closedHour": 18,
                  "priceMultiplier": 1.5,
                  "skillPriceMultiplier": 1.5,
                  "stockIntervalDays": 7,
                  "stock": [ { "item": "sword", "count": 2, "value": 100, "name": "A fine sword" } ],
                  "lessons": [ { "kind": "skill", "subject": "Axe", "amount": 1, "value": 50, "name": "Basic Axe" } ]
                }
              ]
            }
            """),
    ];

    /// <summary>A guild that sells nothing to anybody who is not a member, and the membership it sells.</summary>
    private static (string Path, string Text)[] GuildContent()
    {
        (string Path, string Text)[] shop = ShopContent();
        for (int index = 0; index < shop.Length; index++)
        {
            if (!shop[index].Path.EndsWith("services.json", StringComparison.Ordinal)) continue;
            shop[index] = (shop[index].Path,
                """
                {
                  "documentId": "services",
                  "definitionKind": "service",
                  "entries": [
                    {
                      "id": "sword-and-shield",
                      "kind": "Fire Guild",
                      "name": "Fire Guild",
                      "proprietor": "Master Ash",
                      "operations": [ "buy", "teach" ],
                      "openHour": 6,
                      "closedHour": 18,
                      "priceMultiplier": 1.5,
                      "skillPriceMultiplier": 1.5,
                      "membership": "guild.fire",
                      "stock": [ { "item": "spellbook", "count": 2, "value": 200, "name": "A fire spellbook" } ],
                      "lessons": [
                        { "kind": "effect", "subject": "guild.fire", "amount": 1, "value": 50, "name": "Fire Guild membership" },
                        { "kind": "skill", "subject": "Fire", "amount": 1, "value": 50, "name": "Basic Fire" }
                      ]
                    }
                  ]
                }
                """);
        }

        return shop;
    }

    /// <summary>One member of the scenario's party, with or without the merchant's own skill.</summary>
    private static string Member(bool merchant) =>
        $$"""
        {
          "name": "Roderick",
          "race": "Human",
          "class": "Knight",
          "level": 1,
          "hitPoints": 40,
          "spellPoints": 0,
          "attributes": [ { "id": "Might", "value": 13 } ],
          "skills": [ { "id": "Sword", "level": 1, "tier": 1, "pointsSpent": 1 }{{(merchant ? ", { \"id\": \"Merchant\", \"level\": 2, \"tier\": 2, \"pointsSpent\": 0 }" : string.Empty)}} ],
          "spells": [],
          "conditions": []
        }
        """;

    private static string SourceDirectory() =>
        Path.Combine(RepositoryRoot(), "src", "PartyRpg.Host");

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")) &&
                Directory.Exists(Path.Combine(directory.FullName, "src")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find the repository root above the test assembly.");
    }

    private static void AssertUiConstant(string uiSource, string name, string expected)
    {
        System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(
            uiSource,
            $@"const {name} = '([^']*)';",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        Assert.True(match.Success, $"src/ui/main.ts must declare {name}.");
        Assert.Equal(expected, match.Groups[1].Value);
    }

    private static string Constant(string source, string name)
    {
        System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(
            source,
            $@"const string {name} = ""([^""]*)"";",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        Assert.True(match.Success, $"ProductIdentity.cs must declare {name}.");
        return match.Groups[1].Value;
    }
}
