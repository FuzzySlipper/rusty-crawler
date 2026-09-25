using System.Text.RegularExpressions;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.World;
using PartyRpg.Rulesets.MightAndMagic7;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Host.Tests;

/// <summary>
/// This game's own answers to the kit's cost and larder seams, and the party its content describes.
/// </summary>
/// <remarks>
/// <para>
/// The ruleset's policy types are internal because nothing outside the product composes them, so this
/// suite reaches them through the ruleset's own friend declaration. What it proves is what no kit test
/// can: the donor-cited numbers this game states — a day on the road, a ration a day, the condition
/// hunger puts on a member — and that the party a scenario declares is the party the product leads.
/// </para>
/// <para>
/// Every claim about the donor in the code under test is checked here as a shape rather than as a copy of
/// its source: a day is one ration, a walk quotes what the manual's overland crossing quotes, and a
/// crossing that cannot be walked into one of the game's kinds is refused by name.
/// </para>
/// </remarks>
public sealed class TravelPolicyTests
{
    private static readonly PlaceId Home = new("1");

    [Fact]
    public void Walking_a_road_costs_a_day_on_it_and_the_rations_that_day_eats()
    {
        ContentCatalog catalog = Catalog(World());
        PlaceGraph graph = PlaceGraphLoader.Load(catalog);
        PlaceTransition road = Assert.Single(graph.TransitionsFrom(Home));
        MightAndMagic7TravelCostRule rule = new();

        // Both ways of walking a road are the same journey, and both are quoted as one: the manual's
        // overland crossing lasts days and eats a food unit a day, so the quote is a day and that day's
        // rations rather than nothing at all.
        foreach (TransitionKind kind in new[] { TransitionKind.Walking, TransitionKind.Entrance })
        {
            TravelCostQuote quote = rule.Quote(new TransitionRequest(graph, road, kind, Home, PlacePose.Origin));

            Assert.Null(quote.Refusal);
            Assert.Equal(new TravelTime(MightAndMagic7TravelCostRule.DaysPerCrossing, TravelTimeUnit.Days), quote.Cost.Time);
            Assert.Equal(
                MightAndMagic7Provisions.RationsPerDay * MightAndMagic7TravelCostRule.DaysPerCrossing,
                quote.Cost.Food.Amount);
            Assert.Equal(ProvisionUnit.Portions, quote.Cost.Food.Unit);
        }

        // A scripted move is the world placing the party, not a journey it made, so it quotes nothing.
        TravelCostQuote scripted = rule.Quote(new TransitionRequest(graph, road, TransitionKind.Scripted, Home, PlacePose.Origin));
        Assert.True(scripted.Cost.IsFree);
    }

    [Fact]
    public void Paid_and_magical_travel_are_refused_by_name_rather_than_travelled_free()
    {
        ContentCatalog catalog = Catalog(World());
        PlaceGraph graph = PlaceGraphLoader.Load(catalog);
        PlaceTransition road = Assert.Single(graph.TransitionsFrom(Home));
        MightAndMagic7TravelCostRule rule = new();

        TravelRefusal paid = rule.Quote(new TransitionRequest(graph, road, TransitionKind.PaidService, Home, PlacePose.Origin)).Refusal!;
        Assert.Equal("travel-paid-unowned", paid.Code);
        Assert.Contains("fare", paid.Message, StringComparison.Ordinal);

        TravelRefusal portal = rule.Quote(new TransitionRequest(graph, road, TransitionKind.Portal, Home, PlacePose.Origin)).Refusal!;
        Assert.Equal("travel-portal-unowned", portal.Code);
        Assert.Contains("beacon", portal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_day_eats_one_ration_and_an_empty_larder_weakens_the_party_until_it_is_fed()
    {
        ContentCatalog catalog = Catalog(World(PartyDocument(food: 2)));
        using PartyEntity party = MightAndMagic7Party.Compose(catalog)
            ?? throw new InvalidOperationException("The scenario declares a party, so composing it must produce one.");
        PartyResourceLedger ledger = new(party, provisioning: new MightAndMagic7Provisions(party));

        // The donor's day takes one unit, and one unit only: the charge does not scale with the two members
        // the scenario declares, because the donor's food store is the party's own single number.
        Assert.Equal(2, party.Members.Count);
        ProvisionDay day = ledger.SpendDay();

        Assert.Equal(MightAndMagic7Provisions.RationsPerDay, day.Charged.Amount);
        Assert.Equal(1, party.Food.Portions);
        Assert.All(party.Members, member => Assert.False(member.Conditions.Has(MightAndMagic7Provisions.Weakness)));

        // The next day spends the larder empty: the party is weakened on the road, exactly as arriving
        // short of food weakens it.
        ProvisionDay hungry = ledger.SpendDay();

        Assert.Equal(1, hungry.Covered);
        Assert.Equal(0, party.Food.Portions);
        Assert.Equal(MightAndMagic7Provisions.Weakness, hungry.Shortage!.Value.Condition);
        Assert.All(party.Members, member => Assert.Equal(1, member.Conditions.SeverityOf(MightAndMagic7Provisions.Weakness)));

        // Provisions arriving through the party's own path, and the next day the larder covers, is what
        // ends the hunger: the rule states both ends of it.
        ledger.Credit(PartyCost.OfFood(new Provisions(2, ProvisionUnit.Portions)));
        ProvisionDay fed = ledger.SpendDay();

        Assert.Null(fed.Shortage);
        Assert.Equal(1, party.Food.Portions);
        Assert.All(party.Members, member => Assert.False(member.Conditions.Has(MightAndMagic7Provisions.Weakness)));
    }

    [Fact]
    public void A_scenario_that_declares_no_party_composes_none_and_one_it_cannot_build_is_refused_by_name()
    {
        // No party content: the product runs with no party rather than with four adventurers the ruleset
        // invented, which is what the world does when content declares no places.
        Assert.Null(MightAndMagic7Party.Compose(Catalog(World())));

        // Content that declares a party but no members is a defect of the scenario, named while the
        // session is composed rather than started as a band one character short.
        ContentValidationException empty = Assert.Throws<ContentValidationException>(() =>
            MightAndMagic7Party.Compose(Catalog(World(PartyDocument(food: 3, members: 0)))));
        Assert.Contains(empty.Issues, issue => issue.Code == "party-members-missing");

        // A member without the identities creation needs is the same kind of defect.
        ContentValidationException nameless = Assert.Throws<ContentValidationException>(() =>
            MightAndMagic7Party.Compose(Catalog(World(PartyDocument(food: 3, members: 1, name: string.Empty)))));
        Assert.Contains(nameless.Issues, issue => issue.Code == "party-member-incomplete");
    }

    [Fact]
    public void The_ruleset_holds_no_second_food_counter_beside_the_partys_larder()
    {
        // The party's larder is the one place food is counted, so the ruleset's policy may state a rate —
        // which it does — but may not keep a store of its own that could disagree with the party's.
        Regex store = new(
            @"(private|internal|protected|public)\s+(int|long|Provisions)\s+_?\w*(Food|Portions|Provisions|Rations)\w*\s*(\{[^}]*\bset\b|=(?!=|>)|;)",
            RegexOptions.CultureInvariant);

        string ruleset = Path.Combine(RepositoryRoot(), "src", "PartyRpg.Rulesets.MightAndMagic7");
        string[] sources =
        [
            .. Directory.EnumerateFiles(ruleset, "*.cs", SearchOption.AllDirectories)
                .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)),
        ];
        Assert.NotEmpty(sources);

        foreach (string source in sources)
        {
            Match declared = store.Match(File.ReadAllText(source));
            Assert.False(
                declared.Success,
                $"{Path.GetFileName(source)} declares a food store ('{declared.Value.Trim()}'): the party's larder is the one place food is counted, and a ruleset that kept a second count could disagree with it.");
        }
    }

    [Fact]
    public void The_product_publishes_the_clock_it_composed_and_the_party_its_scenario_declares()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(World(PartyDocument(food: 6)));

        using CrawlerProduct product = new(context);
        product.Start();
        product.Attach();

        ProjectedNode published = ProjectedNode.Of(ui.Latest().Value);
        ProjectedNode clock = published.Field("clock");
        Assert.True(clock.Field("present").AsBoolean());
        Assert.Equal("1168-01-01", clock.Field("date").AsString());
        Assert.Equal("09:00", clock.Field("time").AsString());
        Assert.Equal("day", clock.Field("daylight").AsString());
        Assert.Equal(0d, clock.Field("elapsedDays").AsNumber());

        ProjectedNode party = published.Field("party");
        Assert.True(party.Field("present").AsBoolean());
        Assert.Equal(2d, party.Field("members").AsNumber());
        Assert.Equal(200d, party.Field("coins").AsNumber());
        Assert.Equal(6d, party.Field("provisions").AsNumber());
        Assert.Equal("portions", party.Field("unit").AsString());
        Assert.Equal(4d, party.Field("reputation").AsNumber());
        Assert.Equal(2d, party.Field("fame").AsNumber());
        Assert.Equal(string.Empty, party.Field("conditions").AsString());

        // The one admitted update moves the clock: ten admitted seconds are five game minutes at this
        // game's thirty-to-one rate, so the panel's time is the clock's rather than a value kept here.
        product.Update(ProductTestContext.Update(simulationStep: 0, admittedSteps: 600));
        Assert.Equal("09:05", ProjectedNode.Of(ui.Latest().Value).Field("clock").Field("time").AsString());
    }

    [Fact]
    public void The_product_runs_without_a_party_when_content_declares_none_but_keeps_the_clock()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(World());

        using CrawlerProduct product = new(context);
        product.Start();
        product.Attach();

        ProjectedNode published = ProjectedNode.Of(ui.Latest().Value);

        // The clock is the ruleset's own policy, so a session keeps time whether or not a scenario declares
        // a party to spend it.
        Assert.True(published.Field("clock").Field("present").AsBoolean());

        // And no party is invented for content that declares none: the panel is told there is none rather
        // than shown an empty purse the product made up.
        Assert.False(published.Field("party").Field("present").AsBoolean());
        Assert.Equal(0d, published.Field("party").Field("coins").AsNumber());
        Assert.Equal(string.Empty, published.Field("party").Field("conditions").AsString());
    }

    [Fact]
    public void A_scenario_whose_party_cannot_be_created_stops_the_product_by_name()
    {
        (ProductCreateContext context, _) = ProductTestContext.Create(World(PartyDocument(food: 6, members: 0)));

        // A party that cannot be built is a defect of the scenario, and the product refuses to start on it
        // rather than leading a band that quietly lost a member.
        ContentValidationException error = Assert.Throws<ContentValidationException>(() => new CrawlerProduct(context));
        Assert.Contains(error.Issues, issue => issue.Code == "party-members-missing");
    }

    /// <summary>The catalog the product would load from the files a test stages.</summary>
    private static ContentCatalog Catalog(params (string Path, string Text)[] files)
    {
        (ProductCreateContext context, _) = ProductTestContext.Create(files);
        return ContentCatalogLoader.Load(
            new ProductContentSource(context.Content),
            ContentLayout.Under(ProductTestContext.ContentDirectory)).RequireValid();
    }

    /// <summary>A world of two places joined one way, with no party unless a test stages one.</summary>
    private static (string Path, string Text)[] World(params (string Path, string Text)[] extra) =>
    [
        ProductTestContext.Bundle("partyrpg-default", "world"),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/pack.json", Manifest(extra.Length > 0)),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/places.json",
            """
            {
              "documentId": "places",
              "definitionKind": "place",
              "entries": [
                { "id": "1", "kind": "region", "name": "Home", "respawnDays": 1,
                  "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 512 } ] },
                { "id": "2", "kind": "interior", "name": "Cave", "respawnDays": 1,
                  "entryPoints": [ { "id": "Party Start", "x": 1, "y": 2, "z": 3, "yaw": 0 } ] }
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/links.json",
            """
            {
              "documentId": "links",
              "definitionKind": "travel-link",
              "entries": [ { "id": "edge", "fromPlace": "1", "toPlace": "2", "entryPoint": "Party Start" } ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/start.json",
            """
            { "documentId": "start", "definitionKind": "scenario-start", "entries": [ { "id": "start", "place": "1", "entryPoint": "Party Start" } ] }
            """),
        .. extra,
    ];

    /// <summary>A scenario's party: as many members as a test asks for, and what the purse and larder start with.</summary>
    private static (string Path, string Text) PartyDocument(int food, int members = 2, string name = "Roderick") =>
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/party.json",
            $$"""
            {
              "documentId": "party",
              "definitionKind": "scenario-party",
              "entries": [
                {
                  "id": "party",
                  "coins": 200,
                  "food": {{food}},
                  "reputation": 4,
                  "fame": 2,
                  "members": [
                    {{string.Join(",", Enumerable.Range(0, members).Select(index => Member(index == 0 ? name : $"{name} {index + 1}")))}} 
                  ]
                }
              ]
            }
            """);

    private static string Member(string name) =>
        $$"""
        {
          "name": "{{name}}",
          "race": "Human",
          "class": "Knight",
          "level": 1,
          "hitPoints": 40,
          "spellPoints": 0,
          "attributes": [ { "id": "Might", "value": 13 } ],
          "skills": [ { "id": "Sword", "level": 1, "tier": 1, "pointsSpent": 1 } ],
          "spells": [ "Fireball" ],
          "conditions": []
        }
        """;

    private static string Manifest(bool party) =>
        $$"""
        {
          "schemaVersion": 1,
          "packId": "world",
          "kind": "definitions",
          "provenance": { "description": "authored for a test" },
          "documents": [
            { "path": "places.json", "documentId": "places", "definitionKind": "place" },
            { "path": "links.json", "documentId": "links", "definitionKind": "travel-link" },
            { "path": "start.json", "documentId": "start", "definitionKind": "scenario-start" }{{(party ? "," : string.Empty)}}
            {{(party ? """{ "path": "party.json", "documentId": "party", "definitionKind": "scenario-party" }""" : string.Empty)}}
          ]
        }
        """;

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
}
