using PartyRpg.Kit.Content;
using PartyRpg.Kit.Magic;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Services;
using PartyRpg.Kit.World;
using System.Text.Json;
using Xunit;

namespace PartyRpg.Rulesets.MightAndMagic7.Tests;

/// <summary>
/// The authored opening supplies and the ordinary carried-book Study action. These cases stay below the
/// session so they can assert the ruleset owners directly: service stock remains the imported counter's
/// identity, and a book is learned through the same spellbook and party custody owners the session composes.
/// </summary>
public sealed class OpeningAcquisitionPolicyTests
{
    [Fact]
    public void Authored_Tor_stock_keeps_the_existing_service_identity_and_fits_four_archer_loadouts()
    {
        ContentCatalog catalog = ContentCatalogLoader.Load(StockSource(), Layout).RequireValid();
        MightAndMagic7Services services = MightAndMagic7Services.Read(catalog)
            ?? throw new InvalidOperationException("The stock fixture declares a service, so it must be read.");
        ServiceDefinition tor = services.Find(new ServiceId("1"))
            ?? throw new InvalidOperationException("The stock fixture must keep Tor's existing service id.");
        int openingCoins = MightAndMagic7Creation.Options(catalog).StartingCoins;
        Assert.Equal(3000, openingCoins);
        using PartyEntity party = Party(coins: openingCoins, memberCount: 4, classId: new ClassId("Archer"));

        IReadOnlyList<ServiceStockLine> stock = services.Stock(new ServiceStockRequest(tor, party, null));

        Assert.Equal("Weapon Shop", tor.Kind.Value);
        Assert.Equal(4, Assert.Single(stock, line => line.Definition.Value == "1").Count);
        Assert.Equal(50, Assert.Single(stock, line => line.Definition.Value == "1").Value);
        Assert.Equal(4, Assert.Single(stock, line => line.Definition.Value == "42").Count);
        Assert.Equal(4, Assert.Single(stock, line => line.Definition.Value == "66").Count);
        Assert.Equal(4, Assert.Single(stock, line => line.Definition.Value == "401").Count);
        Assert.Equal(200, Assert.Single(stock, line => line.Definition.Value == "401").Value);
        Assert.Equal(4, Assert.Single(stock, line => line.Definition.Value == "220").Count);
        Assert.Equal(4, Assert.Single(stock, line => line.Definition.Value == "200").Count);

        PartyResourceLedger accounts = new(party);
        PartyServices counter = new(services, party, accounts);
        Assert.True(counter.Open(tor).IsApplied);

        // Browse and buy through the same service mechanism the counter screen uses. The imported Tor
        // multiplier is 1.5, so the Longsword's item-table value of 50 must quote 75 here. Every purchase
        // below then settles that quote through the party's one purse.
        ServiceStockOffer longsword = Assert.Single(counter.Browse()!.Stock, offer => offer.Definition.Value == "1");
        Assert.Equal(75, longsword.Price);

        int spent = 0;
        foreach (string definition in Enumerable.Repeat("42", 4)
            .Concat(Enumerable.Repeat("66", 4))
            .Concat(Enumerable.Repeat("401", 4)))
        {
            ServiceStockOffer offer = Assert.Single(counter.Browse()!.Stock, candidate => candidate.Definition.Value == definition);
            ServiceResult bought = counter.Transact(new ServiceCommand(ServiceOperationKind.Buy, offer.Lot.Value));
            Assert.True(bought.IsApplied, bought.Message);
            Assert.Equal(offer.Price, bought.Paid);
            spent += bought.Paid;
        }

        // One bottle can satisfy the opening delivery and another plus a reagent are available for the
        // ordinary mixing path. The purchases remain canonical and leave coin for the first local service.
        foreach (string definition in new[] { "220", "220", "200", "200" })
        {
            ServiceStockOffer offer = Assert.Single(counter.Browse()!.Stock, candidate => candidate.Definition.Value == definition);
            ServiceResult bought = counter.Transact(new ServiceCommand(ServiceOperationKind.Buy, offer.Lot.Value));
            Assert.True(bought.IsApplied, bought.Message);
            Assert.Equal(offer.Price, bought.Paid);
            spent += bought.Paid;
        }

        Assert.Equal(4, party.Inventory.TotalOf(new ItemDefinitionId("42")));
        Assert.Equal(4, party.Inventory.TotalOf(new ItemDefinitionId("66")));
        Assert.Equal(4, party.Inventory.TotalOf(new ItemDefinitionId("401")));
        Assert.Equal(2, party.Inventory.TotalOf(new ItemDefinitionId("220")));
        Assert.Equal(2, party.Inventory.TotalOf(new ItemDefinitionId("200")));
        Assert.Equal(openingCoins - spent, party.Purse.Coins);

        // The same counter also admits the carried Fire Bolt book for sale. Its sale quote uses the same
        // item-table value of 200 (57 at Tor's multiplier), proving the authored shelves did not introduce
        // a cheaper book valuation that could be arbitraged back through Sell.
        ItemInstance fireBolt = party.Inventory.Items.First(item => item.Definition.Value == "401");
        ServiceSaleOffer sale = Assert.Single(counter.Browse()!.Sales, offer => offer.Item == fireBolt.Id);
        Assert.Equal(57, sale.Price);
        ServiceResult sold = counter.Transact(new ServiceCommand(ServiceOperationKind.Sell, fireBolt.Id.ToString()));
        Assert.True(sold.IsApplied, sold.Message);
        Assert.Equal(57, sold.Earned);
        Assert.Equal(3, party.Inventory.TotalOf(new ItemDefinitionId("401")));
        Assert.Equal(openingCoins - spent + sold.Earned, party.Purse.Coins);
        Assert.True(party.Purse.Coins >= 250, $"opening purchases left only {party.Purse.Coins} coin(s)");
    }

    [Fact]
    public void A_carried_book_is_studied_by_the_spellbook_and_consumed_through_party_custody()
    {
        ContentCatalog catalog = StudyCatalog();
        MightAndMagic7Spells spells = MightAndMagic7Spells.Read(catalog)
            ?? throw new InvalidOperationException("The study fixture declares spells, so it must be read.");
        using PartyEntity party = Party(
            skills: [new SkillEntry(new SkillId("Fire"), 1, new SkillTier(1), 1)]);
        MightAndMagic7ItemMagic itemMagic = new(catalog, spells, null, null, () => party);
        ItemInstance book = party.CreateItem(new ItemDefinitionId("401"));
        Assert.True(party.AcquireItem(book).Admitted);
        book.TakeDamage(1);

        Assert.Equal("Study", itemMagic.ActionOf(book));
        ItemUseResult result = itemMagic.Use(party, party.Members[0], book);

        Assert.True(result.Applied);
        Assert.Equal("item-use-applied", result.Code);
        Assert.True(party.Members[0].Spells.Knows(new SpellId("2")));
        Assert.Equal(0, party.Inventory.TotalOf(new ItemDefinitionId("401")));

        // A book has no weapon repair gate in this adaptation: the damage flag is unrelated to whether a
        // carried lesson can be read. The spell and the consumed custody are both durable save state.
        using PartyEntity restored = new PartyEntityFactory().Restore(party.Capture());
        Assert.True(restored.Members[0].Spells.Knows(new SpellId("2")));
        Assert.Equal(0, restored.Inventory.TotalOf(new ItemDefinitionId("401")));
    }

    [Fact]
    public void Study_refuses_missing_school_mastery_and_known_spell_before_consuming_the_book()
    {
        ContentCatalog catalog = StudyCatalog();
        MightAndMagic7Spells spells = MightAndMagic7Spells.Read(catalog)!;

        using (PartyEntity unskilled = Party())
        {
            ItemInstance book = Carry(unskilled, "401");
            ItemUseResult refused = new MightAndMagic7ItemMagic(catalog, spells, null, null, () => unskilled)
                .Use(unskilled, unskilled.Members[0], book);
            Assert.Equal("spell-school-missing", refused.Code);
            Assert.False(unskilled.Members[0].Spells.Knows(new SpellId("2")));
            Assert.Equal(1, unskilled.Inventory.TotalOf(new ItemDefinitionId("401")));
        }

        using (PartyEntity novice = Party(
            skills: [new SkillEntry(new SkillId("Fire"), 1, new SkillTier(1), 1)]))
        {
            ItemInstance advancedBook = Carry(novice, "405");
            ItemUseResult refused = new MightAndMagic7ItemMagic(catalog, spells, null, null, () => novice)
                .Use(novice, novice.Members[0], advancedBook);
            Assert.Equal("spell-mastery-too-low", refused.Code);
            Assert.Equal(1, novice.Inventory.TotalOf(new ItemDefinitionId("405")));
        }

        using (PartyEntity known = Party(
            skills: [new SkillEntry(new SkillId("Fire"), 1, new SkillTier(1), 1)],
            spells: [new SpellId("2")]))
        {
            ItemInstance book = Carry(known, "401");
            ItemUseResult refused = new MightAndMagic7ItemMagic(catalog, spells, null, null, () => known)
                .Use(known, known.Members[0], book);
            Assert.Equal("spell-already-known", refused.Code);
            Assert.Equal(1, known.Inventory.TotalOf(new ItemDefinitionId("401")));
        }
    }

    private static ItemInstance Carry(PartyEntity party, string definition)
    {
        ItemInstance item = party.CreateItem(new ItemDefinitionId(definition));
        Assert.True(party.AcquireItem(item).Admitted);
        return item;
    }

    private static PartyEntity Party(
        IReadOnlyList<SkillEntry>? skills = null,
        IReadOnlyList<SpellId>? spells = null,
        int coins = 0,
        int memberCount = 1,
        ClassId? classId = null)
    {
        List<MemberCreation> members = [];
        for (int index = 0; index < memberCount; index++)
        {
            members.Add(new MemberCreation(new PartyMemberSeed(
                index == 0 ? "Aelina" : $"Aelina {index + 1}",
                new RaceId("Elf"),
                classId ?? new ClassId("Sorcerer"),
                [new AttributeScore(new AttributeId("Intellect"), 20)],
                skills ?? Array.Empty<SkillEntry>(),
                spells ?? Array.Empty<SpellId>(),
                experience: 0,
                level: 1,
                skillPoints: 0,
                classRank: 1,
                conditions: [],
                hitPoints: ResourcePool.Full(20),
                spellPoints: ResourcePool.Full(10))));
        }

        return new PartyEntityFactory().Create(new PartyCreation(
            members,
            coins,
            foodPortions: 0,
            foodUnit: ProvisionUnit.Portions,
            reputation: 0,
            fame: 0));
    }

    private static InMemoryContentSource StockSource() =>
        new InMemoryContentSource()
            .Add(
                "packs/opening/pack.json",
                TestPacks.Manifest(
                    "opening",
                    ("services", "service"),
                    ("items", "item"),
                    ("spells", "spell"),
                    ("classes", "class"),
                    ("skills", "skill"),
                    ("creation", "creation-policy"),
                    ("stock", "service-stock")))
            .Add(
                "packs/opening/services.json",
                """
                {
                  "documentId": "services", "definitionKind": "service",
                  "entries": [ { "id": "1", "kind": "Weapon Shop", "name": "Tor", "operations": [ "buy", "sell" ], "priceMultiplier": 1.5 } ]
                }
                """)
            .Add(
                "packs/opening/items.json",
                """
                {
                  "documentId": "items", "definitionKind": "item",
                  "entries": [
                    { "id": "1", "name": "Crude Longsword", "value": 50, "equipStat": "Weapon", "type": "weapon" },
                    { "id": "42", "name": "Crude Bow", "value": 100, "equipStat": "Bow", "type": "weapon" },
                    { "id": "66", "name": "Leather Armor", "value": 150, "equipStat": "Armor", "type": "armor" },
                    { "id": "200", "name": "Widowsweep Berries", "value": 1, "equipStat": "Reagent", "type": "reagent" },
                    { "id": "220", "name": "Potion Bottle", "value": 1, "equipStat": "Bottle", "type": "misc" },
                    { "id": "401", "name": "Fire Bolt", "value": 200, "equipStat": "Book", "type": "book", "spell": "2" }
                  ]
                }
                """)
            .Add("packs/opening/classes.json", CreationClassesDocument())
            .Add("packs/opening/skills.json", CreationSkillsDocument())
            .Add(
                "packs/opening/creation.json",
                """
                {
                  "documentId": "creation", "definitionKind": "creation-policy",
                  "entries": [ { "id": "mm7-new-game", "startingCoins": 3000 } ]
                }
                """)
            .Add(
                "packs/opening/spells.json",
                """
                {
                  "documentId": "spells", "definitionKind": "spell",
                  "entries": [ { "id": "2", "school": "Fire", "level": 2, "name": "Fire Bolt", "resist": "Fire" } ]
                }
                """)
            .Add(
                "packs/opening/stock.json",
                """
                {
                  "documentId": "stock", "definitionKind": "service-stock",
                  "entries": [
                    {
                      "id": "opening-tor", "service": "1",
                      "stock": [
                        { "item": "1", "count": 4, "name": "Crude Longsword" },
                        { "item": "42", "count": 4, "name": "Crude Bow" },
                        { "item": "66", "count": 4, "name": "Leather Armor" },
                        { "item": "401", "count": 4, "name": "Fire Bolt" },
                        { "item": "200", "count": 4, "name": "Widowsweep Berries" },
                        { "item": "220", "count": 4, "name": "Potion Bottle" }
                      ]
                    }
                  ]
                }
                """);

    private static string CreationClassesDocument()
    {
        IReadOnlyList<CreationClass> classes = MightAndMagic7Creation.Options().Classes;
        return JsonSerializer.Serialize(new
        {
            documentId = "classes",
            definitionKind = "class",
            entries = classes.Select(characterClass => new
            {
                id = characterClass.Id.Value,
                baseClass = characterClass.Id.Value,
            }),
        });
    }

    private static string CreationSkillsDocument()
    {
        IReadOnlyList<CreationClass> classes = MightAndMagic7Creation.Options().Classes;
        string[] skills = classes
            .SelectMany(characterClass => characterClass.FixedSkills.Concat(characterClass.ChoosableSkills))
            .Select(skill => skill.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return JsonSerializer.Serialize(new
        {
            documentId = "skills",
            definitionKind = "skill",
            entries = skills.Select(skill => new { id = skill }),
        });
    }

    private static ContentCatalog StudyCatalog() =>
        ContentCatalogLoader.Load(
            new InMemoryContentSource()
                .Add(
                    "packs/study/pack.json",
                    TestPacks.Manifest("study", ("spells", "spell"), ("skills", "skill"), ("items", "item")))
                .Add(
                    "packs/study/spells.json",
                    """
                    {
                      "documentId": "spells", "definitionKind": "spell",
                      "entries": [
                        { "id": "2", "school": "Fire", "level": 2, "name": "Fire Bolt", "resist": "Fire" },
                        { "id": "6", "school": "Fire", "level": 6, "name": "Fireball", "resist": "Fire" }
                      ]
                    }
                    """)
                .Add(
                    "packs/study/skills.json",
                    """
                    { "documentId": "skills", "definitionKind": "skill", "entries": [ { "id": "Fire" } ] }
                    """)
                .Add(
                    "packs/study/items.json",
                    """
                    {
                      "documentId": "items", "definitionKind": "item",
                      "entries": [
                        { "id": "401", "name": "Fire Bolt", "value": 200, "equipStat": "Book", "type": "book", "spell": "2" },
                        { "id": "405", "name": "Fireball", "value": 750, "equipStat": "Book", "type": "book", "spell": "6" }
                      ]
                    }
                    """),
            Layout);

    private static readonly ContentLayout Layout = new("packs", "imports", "bundles");
}
