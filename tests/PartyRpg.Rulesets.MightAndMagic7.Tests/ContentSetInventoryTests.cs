using System.Text.Json;
using System.Security.Cryptography;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Magic;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Services;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.World;
using PartyRpg.Kit.Time;
using PartyRpg.Testing;
using Xunit;

namespace PartyRpg.Rulesets.MightAndMagic7.Tests;

/// <summary>Exhaustive imported identities through the owners that consume them, with explicit exclusions.</summary>
public sealed class ContentSetInventoryTests
{
    [ImportedFact("monsters.json")]
    public void Every_imported_identity_resolves_through_its_ruleset_owner()
    {
        ContentCatalog catalog = ImportedContent.Playable();
        catalog.RequireValid();
        var skills = MightAndMagic7Skills.Read(catalog)!;
        var spells = MightAndMagic7Spells.Read(catalog, skills)!;
        var learnedSpells = spells.Catalog.Definitions.Where(spell => spells.InSpellbook(spell.School)).ToArray();
        var equipment = MightAndMagic7EquipmentUse.Read(catalog, skills)!;
        var services = MightAndMagic7Services.Read(catalog, skills, spells)!;
        var monsters = catalog.Entries("monster").Select(row => row.Entry).ToArray();
        var items = catalog.Entries("item").Select(row => row.Entry).ToArray();
        var counters = catalog.Entries("service").Select(row => row.Entry).ToArray();
        Assert.Equal(276, monsters.Length);
        Assert.Equal(800, items.Length);
        Assert.Equal(99, learnedSpells.Length);
        Assert.Equal(37, skills.Catalog.Count);
        Assert.Equal(136, counters.Length);
        var entries = skills.Catalog.Used.Concat(skills.Catalog.Unused)
            .Select(skill => new SkillEntry(skill.Id, 10, new SkillTier(4), 0)).ToArray();
        using PartyEntity party = new PartyEntityFactory(equipmentUse: equipment).Create(new PartyCreation(
            [Member("Arch Mage", entries), Member("Lich", entries), Member("Priest of the Light", entries)], 100000, 50, 0, 0));
        var member = party.Members[0];
        var readings = MightAndMagic7ItemReadings.Read(catalog, equipment.Figure)!;
        var magic = new MightAndMagic7ItemMagic(catalog, spells, null, null, () => party);
        List<object> itemResults = [];
        foreach (var row in items)
        {
            ItemInstance item = party.CreateItem(new(row.Id));
            Assert.True(party.AcquireItem(item).Admitted);
            Assert.Same(item, party.FindItem(item.Id));
            string type = row.GetString("type");
            string path;
            if (equipment.Figure.Worn(item.Definition) is { } worn)
            {
                var slot = MightAndMagic7Figure.SlotsFor(worn.Kind)[0];
                Assert.Null(equipment.Judge(member, slot, item));
                Assert.True(party.Equip(member.Id, slot, item.Id).Admitted, row.Id);
                Assert.True(party.Unequip(member.Id, slot).Admitted);
                path = "equipped-and-returned";
            }
            else if (type == "book")
            {
                Assert.NotNull(spells.TaughtBy(item.Definition));
                Assert.Equal("Study", magic.ActionOf(item));
                path = "spell-learning";
            }
            else if (type == "spell-scroll")
            {
                Assert.NotNull(spells.Reading(item.Definition));
                path = "spell-casting";
            }
            else path = type switch
            {
                "potion" => "alchemy-or-potion",
                "reagent" => "alchemy",
                "gem" => "trade-good",
                "gold" => "loot-currency",
                "message-scroll" when row.GetString("readingText").Length > 0 => "inventory-reading",
                "message-scroll" => "excluded-empty-source-reading",
                _ when magic.ActionOf(item) is not null => "ordinary-item-use",
                _ when row.GetString("name").Length == 0 || row.GetString("name").StartsWith('_') => "excluded-reserved-source-row",
                _ => "contextual-quest-or-world-item",
            };
            Assert.Equal(row.GetString("readingText"), readings.Read(item).Text);
            itemResults.Add(new { id = row.Id, type, path });
        }
        Assert.Equal(800, party.Items.Count);
        Assert.Equal(72, items.Count(row => row.GetString("readingText").Length > 0));
        foreach (var spell in learnedSpells)
        {
            // Both final paths are represented: the closed opposite school remains closed.
            var learner = party.Members.FirstOrDefault(candidate => spells.MayLearn(candidate, spell) is null && skills.Ceiling(candidate, spell.SchoolSkill).MaximumTier.Value >= spell.Tier.Value);
            Assert.NotNull(learner);
            var book = party.Items.Single(item => spells.TaughtBy(item.Definition) == spell.Id);
            var learned = magic.Use(party, learner, book);
            Assert.True(learned.Applied, learned.Message);
            Assert.True(learner.Spells.Knows(spell.Id));
            Assert.Null(party.FindItem(book.Id));
        }
        var itemIds = items.Select(item => item.Id).ToHashSet();
        foreach (var row in counters)
        {
            var counter = services.Find(new(row.Id));
            Assert.NotNull(counter);
            Assert.All(services.Stock(new(counter, party, null)), line => Assert.Contains(line.Definition.Value, itemIds));
            foreach (var lesson in services.Lessons(new(counter, party, null)))
                if (lesson.Kind == ServiceLessonKind.Spell) Assert.True(spells.Catalog.Declares(new(lesson.Subject)));
                else if (lesson.Kind == ServiceLessonKind.Skill) Assert.NotNull(skills.Resolve(lesson.Subject));
        }
        // Each source monster becomes an actual population entity and admits its own attack against a party.
        // Spatial placement is synthetic so this checks content mechanics, not reachability in original maps.
        var combat = MightAndMagic7Combat.Compose(catalog, random: new TestRandomService(), spells: spells, party: () => party);
        string placements = JsonSerializer.Serialize(monsters.Select(row => new { id = row.Id, kind = "monster", monster = row.Id, x = 1, y = 0, z = 0 }));
        using var payload = JsonDocument.Parse("{\"id\":\"audit\",\"placements\":" + placements + "}");
        var place = new PlaceDefinition(new("audit"), PlaceKind.Region, "Content audit", [], new ContentEntry("audit", payload.RootElement.Clone()));
        var graph = PlaceGraph.From([place], []);
        using var world = new SessionWorld(graph,
            new PartyPoseOwner(new(place.Id, new(0, 0, 0, 0, 0)), new FacingRule(2048, -512, 512)),
            new PlaceStateLedger(graph, PlaceRespawnRule.FromContent()), new MightAndMagic7TravelCostRule(),
            partyEntity: party, vitals: combat);
        world.Populate();
        var fight = new CombatState(Capabilities.Combat(combat), party, world);
        fight.Step();
        var foes = fight.Combatants.Where(actor => !actor.Subject.IsMember).ToArray();
        Assert.Equal(276, foes.Length);
        var target = fight.Combatants.First(actor => actor.Subject.IsMember);
        fight.Observe(new ClockAdvance(default, default, GameDuration.FromHours(1), PeriodCrossings.None, []));
        foreach (var foe in foes)
        {
            Assert.True(combat.HitPointsOf(foe.Subject) > 0, foe.Name);
            Assert.True(combat.CanAct(foe.Subject), foe.Name);
            Assert.True(combat.RecoveryAfter(foe.Subject, combat.AttackKindFor(foe.Subject)).Milliseconds > 0, foe.Name);
            var plan = combat.PlanOf(foe.Subject, target.Subject, combat.AttackKindFor(foe.Subject));
            Assert.True(plan.Damage.Maximum >= 0, foe.Name);
            fight.Provoke(foe.Id);
            var strike = fight.Order(new(foe.Id, foe.PreferredKind, target.Id));
            Assert.NotNull(strike.Initiated);
            Assert.NotNull(strike.Resolution);
        }
        if (Environment.GetEnvironmentVariable("CRAWLER_CONTENT_REPORT") is not { Length: > 0 } report) return;
        File.WriteAllText(report, JsonSerializer.Serialize(new
        {
            validation = "passed-with-explicit-exclusions",
            monsters = foes.Length, items = items.Length, spells = learnedSpells.Length,
            skills = skills.Catalog.Count, services = counters.Length,
            sourceDigests = new[] { "monsters.json", "items.json", "spells.json", "skills.json", "services.json" }
                .ToDictionary(file => file, file => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(ImportedContent.Table(file))))),
            policyOwners = new { ceilings = "MightAndMagic7Skills", guilds = "MightAndMagic7Services", calendar = "MightAndMagic7Time", raceCreation = "MightAndMagic7CreationTables" },
            readableItems = items.Count(row => row.GetString("readingText").Length > 0),
            unusedSkillRows = skills.Catalog.Unused.Select(row => row.Id.Value),
            limits = new[] { "Monster population and resolved attacks use synthetic positions, not a playthrough of every placement.",
                "Equipment uses a staged trained member; spell learning covers both final paths. This does not prove every class can learn every spell.",
                "Empty message-scroll source rows have no text to display and are explicitly listed.",
                "Reserved and quest rows remain represented; their presence is not a claim of independent item use.",
                "Artifact equipment is checked; only the fixed powers documented by the ruleset are implemented." },
            itemInventory = itemResults,
        }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
    }

    private static MemberCreation Member(string characterClass, IReadOnlyList<SkillEntry> skills) =>
        new(new PartyMemberSeed("Auditor", new("Human"), new(characterClass),
            [.. new[] { "Might", "Intellect", "Personality", "Endurance", "Accuracy", "Speed", "Luck" }.Select(name => new AttributeScore(new(name), 15))], skills, [], 0, 1, 0, 3, [],
            ResourcePool.Full(100000), ResourcePool.Full(100000)));
}
