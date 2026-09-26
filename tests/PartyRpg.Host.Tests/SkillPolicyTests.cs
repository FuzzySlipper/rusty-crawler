using PartyRpg.Kit.Content;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Progression;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Skills;
using PartyRpg.Rulesets.MightAndMagic7;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Host.Tests;

/// <summary>
/// This game's skills against the real composition: the shipped rows read into their blocks, the ceilings
/// the class and rank tables state, the equipment gate, the guild keeper who teaches a mastery rung through
/// the conversation that already exists, and what a learned rung changes.
/// </summary>
/// <remarks>
/// The kit's own suite proves the owner with rules it states itself. What only this suite can prove is that
/// this game's own tables reach it: which of the 37 shipped rows the four blocks carry and which three are
/// leftovers, what a class and rank permit, what a mastery teacher requires and what it charges, which items
/// need a skill and which five places need none, and that a rung learned at a counter moves a number.
/// </remarks>
public sealed class SkillPolicyTests
{
    private static readonly SkillId Sword = new("Sword");
    private static readonly SkillId Fire = new("Fire");
    private static readonly SkillId Learning = new("Learning");

    private static readonly ServiceIntentNames ServiceControls = new(
        ProductIdentity.ServiceLeaveIntent,
        ProductIdentity.UiActionContract);

    private static readonly UseIntentNames UseControls = new(
        ProductIdentity.UseIntent,
        ProductIdentity.UseAction,
        ProductIdentity.UiActionContract);

    private static readonly ConversationIntentNames ConversationControls = new(
        ProductIdentity.ConversationLeaveIntent,
        ProductIdentity.UiActionContract);

    private static readonly SkillRaiseIntentNames SkillControls = new(
        ProductIdentity.SkillRaiseAction,
        ProductIdentity.UiActionContract);

    [Fact]
    public void The_shipped_rows_are_read_into_four_blocks_and_the_three_row_the_game_does_not_use_are_reported()
    {
        // The shipped skill table is the operator's own data: it is generated from their installation and no
        // checkout commits it, so a machine without it has nothing to check here and says so by returning.
        // The counts this asserts are the inventory's own — 37 rows, four blocks of 34, three leftovers
        // (docs/research/mm7-data-inventory.md, Skills; docs/gameplay-design.md §3.2).
        string content = Path.Combine(RepositoryRoot(), "content");
        if (!File.Exists(Path.Combine(content, "partyrpg", "imports", "mm7-tables", "skills.json"))) return;

        ContentCatalog catalog = ContentCatalogLoader.Load(
            new FileContentSource(content),
            new ContentLayout("partyrpg/content-packs", "partyrpg/imports", "partyrpg/bundles"));
        MightAndMagic7Skills skills = MightAndMagic7Skills.Read(catalog)
            ?? throw new InvalidOperationException("The operator's pack declares skills, so the policy must be read.");

        SkillCatalog shipped = skills.Catalog;
        Assert.Equal(37, shipped.Count);
        Assert.Equal(34, shipped.Used.Count);
        Assert.Equal(8, shipped.CountOf(SkillBlock.Weapon));
        Assert.Equal(5, shipped.CountOf(SkillBlock.Armour));
        Assert.Equal(9, shipped.CountOf(SkillBlock.Magic));
        Assert.Equal(12, shipped.CountOf(SkillBlock.Miscellaneous));

        // The three the manual's four blocks do not carry are in the catalog and reported as unused: the
        // evidence is each row's own shipped text and the donor's own table, which the policy cites.
        Assert.Equal(
            ["Blaster", "Diplomacy", "Thievery"],
            shipped.Unused.Select(row => row.Id.Value).Order(StringComparer.Ordinal));
        Assert.Equal(SkillBlock.Unused, shipped.Read(new SkillId("Thievery")).Block);
    }

    [Fact]
    public void The_ceiling_differs_across_the_nine_shipped_classes_and_their_ranks()
    {
        MightAndMagic7Skills skills = Policy();
        using PartyEntity party = Party(skills, ("Knight", 1, Sword), ("Knight", 3, Sword), ("Sorcerer", 1, Fire));

        // A Knight's own blade: the class table gives the base Knight the master rung, the second promotion
        // the grand master rung, and this game's level bands give the first twelve levels and the third the
        // donor's own sixty.
        Assert.Equal(new SkillCeiling(12, new SkillTier(3)), CeilingOf(skills, party, 0));
        Assert.Equal(new SkillCeiling(60, new SkillTier(4)), CeilingOf(skills, party, 1));

        // A Sorcerer may reach expert in Fire at its first rank — nine levels, whose band sits above the
        // level a mastery teacher wants for expert and below the one it wants for master.
        Assert.Equal(new SkillCeiling(9, new SkillTier(2)), CeilingOf(skills, party, 2));

        // A class that may hold no such skill answers none, which is a different fact from a ceiling reached.
        using PartyEntity knight = Party(skills, ("Knight", 3, Fire));
        Assert.True(skills.Ceiling(knight.Members[0], Fire).IsNone);

        // Across the nine shipped classes the ceilings are not one number: the sweep is what makes this a
        // ceiling rather than a constant, and the same class at two ranks differs as well.
        HashSet<string> seen = [];
        foreach (string characterClass in new[]
        {
            "Knight", "Thief", "Monk", "Paladin", "Archer", "Ranger", "Cleric", "Druid", "Sorcerer",
        })
        {
            foreach (int rank in new[] { 1, 2, 3 })
            {
                using PartyEntity member = Party(skills, (characterClass, rank, Sword));
                SkillCeiling ceiling = skills.Ceiling(member.Members[0], Sword);
                seen.Add($"{ceiling.MaximumLevel}/{ceiling.MaximumTier.Value}");
            }
        }

        Assert.True(seen.Count >= 2, $"the ceilings were all '{string.Join(", ", seen)}'");
    }

    [Fact]
    public void An_equipment_gate_refuses_a_weapon_without_the_skill_and_honours_the_five_places_that_need_none()
    {
        ContentCatalog catalog = Catalog(
            skills: ["Sword", "Leather"],
            items:
            [
                (Id: "blade", Skill: "sword"),
                (Id: "skirts", Skill: "leather"),
                (Id: "boots", Skill: "leather"),
                (Id: "club", Skill: "club"),
                (Id: "ring", Skill: string.Empty),
            ]);
        MightAndMagic7Skills skills = MightAndMagic7Skills.Read(catalog)
            ?? throw new InvalidOperationException("The content declares skills, so the policy must be read.");
        MightAndMagic7EquipmentUse gate = MightAndMagic7EquipmentUse.Read(catalog, skills)
            ?? throw new InvalidOperationException("The content declares items, so the gate must be read.");

        using PartyEntity party = Party(skills, ("Knight", 1, Sword));
        PartyMember knight = party.Members[0];
        PartyEntityFactory factory = new(equipmentUse: gate);
        using PartyEntity equipped = factory.Create(new PartyCreation(
            [Member("Knight", [new SkillEntry(Sword, 1, new SkillTier(1), 0)])],
            coins: 0,
            foodPortions: 0,
            reputation: 0,
            fame: 0));

        // A weapon the member has the skill for is wielded; one it has not learned is refused by name.
        Assert.Null(gate.Judge(knight, new EquipmentSlot("Weapon"), Instance(party, "blade")));
        PartyRefusal refused = gate.Judge(knight, new EquipmentSlot("Weapon"), Instance(party, "skirts"))!;
        Assert.Equal("equipment-skill-missing", refused.Code);
        Assert.Contains("Leather", refused.Message, StringComparison.Ordinal);

        // The manual's five exceptions are about the place rather than the goods: a leather item needs no
        // skill in the boots, however it would be refused anywhere else.
        Assert.Null(gate.Judge(knight, new EquipmentSlot("Boots"), Instance(party, "boots")));
        foreach (string exempt in new[] { "Belt", "Cloak", "Helm", "Gauntlets" })
        {
            Assert.Null(gate.Judge(knight, new EquipmentSlot(exempt), Instance(party, "skirts")));
        }

        // An item whose row names a skill this game's skill table does not carry cannot be said to be usable
        // by anybody, so it is refused with its own word rather than quietly allowed: guessing that an
        // unknown requirement is no requirement would hand out a weapon the game's table says is not theirs.
        PartyRefusal unknown = gate.Judge(knight, new EquipmentSlot("Weapon"), Instance(party, "club"))!;
        Assert.Equal("equipment-skill-unknown", unknown.Code);
        Assert.Contains("club", unknown.Message, StringComparison.Ordinal);

        // An item that names no skill needs none: a ring is not a trade.
        Assert.Null(gate.Judge(knight, new EquipmentSlot("Ring"), Instance(party, "ring")));
    }

    [Fact]
    public void A_guild_keeper_teaches_the_rung_its_house_reaches_and_a_class_that_may_not_is_refused_by_promotion()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(GuildContent());
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            ProductTestContext.RulesetContext(context, ui) with
            {
                Use = UseControls,
                Service = ServiceControls,
                Conversation = ConversationControls,
                Skills = SkillControls,
            });
        session.Start();
        ulong step = 0;

        // The party talks to the keeper of the guild content places, and the counter is offered as the
        // person's own topic: entering a service is the conversation handing off, not a second mechanism.
        session.Update(ProductTestContext.Update(++step, 1));
        session.Update(ProductTestContext.Update(++step, 1, ProductTestContext.Digital(ProductIdentity.UseIntent)));
        session.Update(ProductTestContext.Update(++step, 1, ProductTestContext.ChooseTopic(MightAndMagic7Conversation.CounterTopicId)));
        ProjectedNode counter = ProjectedNode.Of(ui.Latest().Value).Field("service");
        Assert.True(counter.Field("open").AsBoolean());

        // The house stands third in its school's ladder, so its keeper teaches the school's skill up to the
        // master rung: the basic lesson every guild sells, and two mastery lessons on top, each priced at the
        // donor's own flat fee for that rung (1000 for expert, 4000 for master).
        Dictionary<string, (int Tier, int Price)> lessons = [];
        for (int index = 0; index < counter.Field("lessons").Length(); index++)
        {
            ProjectedNode lesson = counter.Field("lessons").Item(index);
            lessons[lesson.Field("name").AsString()] = ((int)lesson.Field("tier").AsNumber(), (int)lesson.Field("price").AsNumber());
        }

        Assert.Equal(2, lessons["Fire, expert"].Tier);
        Assert.Equal(1000, lessons["Fire, expert"].Price);
        Assert.Equal(3, lessons["Fire, master"].Tier);
        Assert.Equal(4000, lessons["Fire, master"].Price);

        // The guild serves its members, so the membership comes first, then the expert rung: the member's
        // school skill rises a rung and the panel publishes the rung the master teacher left it at.
        session.Update(ProductTestContext.Update(++step, 1, ProductTestContext.Payload("""{"action":"service.teach","target":"guild.fire","member":0}""")));
        session.Update(ProductTestContext.Update(++step, 1, ProductTestContext.Payload("""{"action":"service.teach","target":"Fire","tier":2,"member":0}""")));
        ProjectedNode taught = ProjectedNode.Of(ui.Latest().Value).Field("service");
        Assert.Equal("applied", taught.Field("outcome").AsString());
        ProjectedNode fire = Skill(ui, member: 0, "Fire");
        Assert.Equal("expert", fire.Field("tier").AsString());
        Assert.Equal(4, fire.Field("level").AsNumber());
        Assert.Equal(9, fire.Field("ceilingLevel").AsNumber());
        Assert.Equal("expert", fire.Field("ceilingTier").AsString());

        // The master rung is beyond a Sorcerer's first rank: the class table reaches it at the third rank,
        // and the refusal says which promotion would open it rather than only that it is closed.
        session.Update(ProductTestContext.Update(++step, 1, ProductTestContext.Payload("""{"action":"service.teach","target":"Fire","tier":3,"member":0}""")));
        ProjectedNode refused = ProjectedNode.Of(ui.Latest().Value).Field("service");
        Assert.Equal("refused", refused.Field("outcome").AsString());
        Assert.Equal("service-lesson-needs-promotion", refused.Field("code").AsString());
        Assert.Contains("rank 3", refused.Field("message").AsString(), StringComparison.Ordinal);
        Assert.Equal("expert", Skill(ui, member: 0, "Fire").Field("tier").AsString());
    }

    [Fact]
    public void The_rung_a_master_teacher_grants_changes_what_an_award_pays()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(GuildContent());
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            ProductTestContext.RulesetContext(context, ui) with
            {
                Use = UseControls,
                Service = ServiceControls,
                Conversation = ConversationControls,
                Skills = SkillControls,
            });
        session.Start();
        ulong step = 0;

        PartyEntity party = ((MightAndMagic7Session)session).Party
            ?? throw new InvalidOperationException("The scenario declares a party, so the session must hold one.");

        // What the party's own learning skill is worth before the lesson: the donor's nine percent plus the
        // skill's level at the member's rung.
        long before = MightAndMagic7Progression.Instance
            .Divide(new ProgressionDivision(party, "kill", 4000))
            .Single(share => share.Member == party.Members[0].Id).Amount;

        session.Update(ProductTestContext.Update(++step, 1));
        session.Update(ProductTestContext.Update(++step, 1, ProductTestContext.Digital(ProductIdentity.UseIntent)));
        session.Update(ProductTestContext.Update(++step, 1, ProductTestContext.ChooseTopic(MightAndMagic7Conversation.CounterTopicId)));

        // The element guild's second skill is learning, which the donor's own guild table states, so the
        // same keeper who teaches the school teaches the trade — and the house's rung is the rung it teaches.
        session.Update(ProductTestContext.Update(++step, 1, ProductTestContext.Payload("""{"action":"service.teach","target":"guild.fire","member":0}""")));
        session.Update(ProductTestContext.Update(++step, 1, ProductTestContext.Payload("""{"action":"service.teach","target":"Learning","tier":2,"member":0}""")));
        Assert.Equal("applied", ProjectedNode.Of(ui.Latest().Value).Field("service").Field("outcome").AsString());

        long after = MightAndMagic7Progression.Instance
            .Divide(new ProgressionDivision(party, "kill", 4000))
            .Single(share => share.Member == party.Members[0].Id).Amount;

        // The donor's own multipliers (src/Engine/Objects/Character.cpp:625-641, learningPercent, and the
        // 1/2/3/5 ladder in src/Engine/Objects/CharacterEnumFunctions.h:33-45): nine percent plus the
        // skill's level at the basic rung, doubled at the expert rung the teacher granted. The party is one
        // member, so a 4,000 award divides to 4,000 and the bonus is on top of it.
        Assert.Equal(5, party.Members[0].Skills.LevelOf(Learning));
        Assert.Equal(new SkillTier(2), party.Members[0].Skills.TierOf(Learning));
        Assert.Equal(4560, before);
        Assert.Equal(4760, after);
        Assert.True(after > before, $"the expert rung paid {after} against {before}");
    }

    [Fact]
    public void The_panel_publishes_every_members_skills_and_the_raise_control_spends_points_through_the_owner()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(RaiseContent());
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            ProductTestContext.RulesetContext(context, ui) with { Skills = SkillControls });
        session.Start();
        ulong step = 0;

        // The panel publishes each member's own rows with the ceiling their class and rank impose and what the
        // next point would buy: the donor's own price — the next level's number — and the level it reaches.
        session.Update(ProductTestContext.Update(++step, 1));
        ProjectedNode skills = ProjectedNode.Of(ui.Latest().Value).Field("skills");
        Assert.True(skills.Field("available").AsBoolean());
        Assert.Equal(2, skills.Field("members").Length());

        ProjectedNode fire = Skill(ui, member: 0, "Fire");
        Assert.Equal(4, fire.Field("level").AsNumber());
        Assert.Equal("basic", fire.Field("tier").AsString());
        Assert.Equal(9, fire.Field("ceilingLevel").AsNumber());
        Assert.Equal("expert", fire.Field("ceilingTier").AsString());
        Assert.Equal(5, fire.Field("reached").AsNumber());
        Assert.Equal(5, fire.Field("cost").AsNumber());
        Assert.Equal(string.Empty, fire.Field("refusal").AsString());

        // The member with no points has a refusal instead of a price: the panel shows the reason a raise would
        // not land rather than a control the product would refuse.
        ProjectedNode sword = Skill(ui, member: 1, "Sword");
        Assert.Contains("costs 2 skill point(s)", sword.Field("refusal").AsString(), StringComparison.Ordinal);
        Assert.Contains("0 remain unspent", sword.Field("refusal").AsString(), StringComparison.Ordinal);

        // The screen's own control spends the point: the member's skill rises a level, the pool drops by the
        // price the rule quoted, and the panel reports what the raise did.
        session.Update(ProductTestContext.Update(
            ++step,
            1,
            ProductTestContext.Payload("{\"action\":\"party.raise-skill\",\"member\":0,\"skill\":\"Fire\"}")));
        Assert.Equal("raised", ProjectedNode.Of(ui.Latest().Value).Field("skills").Field("outcome").AsString());
        Assert.Equal(5, Skill(ui, member: 0, "Fire").Field("level").AsNumber());
        Assert.Equal(195, Points(ui, member: 0));
        Assert.Contains(
            "raised Fire to level 5 for 5 skill point(s)",
            ProjectedNode.Of(ui.Latest().Value).Field("skills").Field("message").AsString(),
            StringComparison.Ordinal);

        // A raise that would pass the ceiling is refused whole with the limit named, and the pool does not
        // move: the game's own class table answered, rather than a number the screen worked out.
        session.Update(ProductTestContext.Update(
            ++step,
            1,
            ProductTestContext.Payload("{\"action\":\"party.raise-skill\",\"member\":0,\"skill\":\"Fire\",\"levels\":10}")));
        ProjectedNode capped = ProjectedNode.Of(ui.Latest().Value).Field("skills");
        Assert.Equal("refused", capped.Field("outcome").AsString());
        Assert.Equal("skill-ceiling-reached", capped.Field("code").AsString());
        Assert.Contains("may raise it to 9 and no further", capped.Field("message").AsString(), StringComparison.Ordinal);
        Assert.Contains("rank 1", capped.Field("message").AsString(), StringComparison.Ordinal);
        Assert.Equal(5, Skill(ui, member: 0, "Fire").Field("level").AsNumber());
        Assert.Equal(195, Points(ui, member: 0));

        // A member with nothing to spend is refused by name through the same entry.
        session.Update(ProductTestContext.Update(
            ++step,
            1,
            ProductTestContext.Payload("{\"action\":\"party.raise-skill\",\"member\":1,\"skill\":\"Sword\"}")));
        ProjectedNode short_ = ProjectedNode.Of(ui.Latest().Value).Field("skills");
        Assert.Equal("refused", short_.Field("outcome").AsString());
        Assert.Equal("insufficient-skill-points", short_.Field("code").AsString());
        Assert.Equal(1, Skill(ui, member: 1, "Sword").Field("level").AsNumber());
    }

    /// <summary>How many skill points a member holds, as the progression block publishes them.</summary>
    private static int Points(RecordingUiService ui, int member) =>
        (int)ProjectedNode.Of(ui.Latest().Value)
            .Field("progression").Field("members").Item(member).Field("skillPoints").AsNumber();

    /// <summary>Reads one member's row for one skill out of the panel's own skills block.</summary>
    private static ProjectedNode Skill(RecordingUiService ui, int member, string skill)
    {
        ProjectedNode skills = ProjectedNode.Of(ui.Latest().Value).Field("skills");
        return skills.Field("members").Item(member).Field("skills")
            .Item(Enumerable.Range(0, skills.Field("members").Item(member).Field("skills").Length())
                .Single(position => skills.Field("members").Item(member).Field("skills").Item(position).Field("skill").AsString() == skill));
    }

    /// <summary>The ceiling one member of a one-member party has for a skill, read through the real policy.</summary>
    private static SkillCeiling CeilingOf(MightAndMagic7Skills skills, PartyEntity party, int member) =>
        skills.Ceiling(party.Members[member], party.Members[member].Skills.Entries[0].Skill);

    /// <summary>This game's skill policy over a catalog that declares the skills the test asks about.</summary>
    private static MightAndMagic7Skills Policy() => MightAndMagic7Skills.Read(Catalog(
        skills: ["Sword", "Fire", "Learning"],
        items: []))!;

    /// <summary>
    /// A catalog declaring the skills and items a test names, written the way the importer writes them.
    /// </summary>
    private static ContentCatalog Catalog(
        IReadOnlyList<string> skills,
        IReadOnlyList<(string Id, string Skill)> items)
    {
        MemoryContent source = new MemoryContent()
            .Add(
                "packs/tables/pack.json",
                """
                {
                  "schemaVersion": 1,
                  "packId": "tables",
                  "kind": "definitions",
                  "provenance": { "description": "authored for a test" },
                  "documents": [
                    { "path": "skills.json", "documentId": "skills", "definitionKind": "skill" },
                    { "path": "items.json", "documentId": "items", "definitionKind": "item" }
                  ]
                }
                """)
            .Add(
                "packs/tables/skills.json",
                $$"""{ "documentId": "skills", "definitionKind": "skill", "entries": [ {{string.Join(", ", skills.Select(name => $$"""{ "id": "{{name}}" }"""))}} ] }""")
            .Add(
                "packs/tables/items.json",
                $$"""{ "documentId": "items", "definitionKind": "item", "entries": [ {{string.Join(", ", items.Select(item => $$"""{ "id": "{{item.Id}}", "skill": "{{item.Skill}}" }"""))}} ] }""");
        return ContentCatalogLoader.Load(source, new ContentLayout("packs", "absent", "bundles"));
    }

    /// <summary>A party whose members stand at the classes and ranks a test names.</summary>
    private static PartyEntity Party(MightAndMagic7Skills skills, params (string Class, int Rank, SkillId Skill)[] members)
    {
        _ = skills;
        return new PartyEntityFactory().Create(new PartyCreation(
            [.. members.Select(member => Member(member.Class, [new SkillEntry(member.Skill, 1, new SkillTier(1), 0)], rank: member.Rank))],
            coins: 0,
            foodPortions: 0,
            reputation: 0,
            fame: 0));
    }

    /// <summary>One member of a test's own party.</summary>
    private static MemberCreation Member(string characterClass, IReadOnlyList<SkillEntry> skills, int rank = 1) =>
        new(new PartyMemberSeed(
            "Ann",
            new RaceId("Human"),
            new ClassId(characterClass),
            [new AttributeScore(new AttributeId("Might"), 12)],
            skills,
            spells: [],
            experience: 0,
            level: 1,
            skillPoints: 0,
            classRank: rank,
            conditions: [],
            hitPoints: ResourcePool.Full(40),
            spellPoints: ResourcePool.Full(0)));

    /// <summary>One item instance in the party's own pack, acquired through the party's own path.</summary>
    private static ItemInstance Instance(PartyEntity party, string definition)
    {
        ItemAcquisition acquisition = party.AcquireItem(new ItemDefinitionId(definition));
        return acquisition.Item
            ?? throw new InvalidOperationException($"The pack refused {definition}: {acquisition.Refusal}");
    }

    /// <summary>
    /// A guild of the Fire at each of three rungs, with the third placed where the party stands, and a party
    /// whose member is a Sorcerer that already holds Fire at the level a mastery teacher wants.
    /// </summary>
    private static (string Path, string Text)[] GuildContent() =>
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
                { "path": "skills.json", "documentId": "skills", "definitionKind": "skill" }
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/places.json",
            """
            {
              "documentId": "places",
              "definitionKind": "place",
              "entries": [
                { "id": "1", "kind": "interior", "name": "The Guild of Fire", "respawnDays": 7,
                  "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
                  "placements": [ { "id": "the-guild", "kind": "service", "houseId": "fire-master", "x": 100, "y": 0, "z": 0 } ] }
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/start.json",
            """
            { "documentId": "start", "definitionKind": "scenario-start", "entries": [ { "id": "start", "place": "1", "entryPoint": "Party Start" } ] }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/party.json",
            """
            {
              "documentId": "party",
              "definitionKind": "scenario-party",
              "entries": [
                { "id": "party", "coins": 20000, "food": 6, "reputation": 0, "fame": 0,
                  "members": [
                    { "name": "Aelina", "race": "Elf", "class": "Sorcerer", "level": 1, "hitPoints": 20,
                      "spellPoints": 15,
                      "attributes": [ { "id": "Intellect", "value": 30 } ],
                      "skills": [ { "id": "Fire", "level": 4, "tier": 1, "pointsSpent": 10 },
                                  { "id": "Learning", "level": 5, "tier": 1, "pointsSpent": 15 } ],
                      "spells": [], "conditions": [] }
                  ] }
              ] }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/skills.json",
            """
            {
              "documentId": "skills",
              "definitionKind": "skill",
              "entries": [ { "id": "Fire" }, { "id": "Learning" }, { "id": "Sword" } ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/services.json",
            """
            {
              "documentId": "services",
              "definitionKind": "service",
              "entries": [
                { "id": "fire-initiate", "kind": "Fire Guild", "name": "Initiate Guild of Fire", "proprietor": "Sethric",
                  "mapId": 1, "typeSequence": 1, "openHour": 0, "closedHour": 24,
                  "priceMultiplier": 1, "skillPriceMultiplier": 1 },
                { "id": "fire-adept", "kind": "Fire Guild", "name": "Adept Guild of Fire", "proprietor": "Jani",
                  "mapId": 1, "typeSequence": 2, "openHour": 0, "closedHour": 24,
                  "priceMultiplier": 1, "skillPriceMultiplier": 1 },
                { "id": "fire-master", "kind": "Fire Guild", "name": "Master Guild of Fire", "proprietor": "Halion",
                  "mapId": 1, "typeSequence": 3, "openHour": 0, "closedHour": 24,
                  "priceMultiplier": 1, "skillPriceMultiplier": 1 }
              ]
            }
            """),
    ];

    /// <summary>An in-memory content root, so these tests read a catalog without touching the file system.</summary>
internal sealed class MemoryContent : IContentSource
{
    private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);

    internal MemoryContent Add(string path, string text)
    {
        _files[path] = text;
        return this;
    }

    public IReadOnlyList<string> ListDirectories(string relativePath)
    {
        string prefix = Normalize(relativePath);
        HashSet<string> names = new(StringComparer.Ordinal);
        foreach (string path in _files.Keys)
        {
            if (!path.StartsWith(prefix, StringComparison.Ordinal)) continue;
            string remainder = path[prefix.Length..];
            int separator = remainder.IndexOf('/', StringComparison.Ordinal);
            if (separator > 0) names.Add(remainder[..separator]);
        }

        return [.. names.Order(StringComparer.Ordinal)];
    }

    public IReadOnlyList<string> ListFiles(string relativePath)
    {
        string prefix = Normalize(relativePath);
        return [.. _files.Keys
            .Where(path => path.StartsWith(prefix, StringComparison.Ordinal))
            .Select(path => path[prefix.Length..])
            .Where(remainder => remainder.Length > 0 && !remainder.Contains('/', StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)];
    }

    public bool FileExists(string relativePath) => _files.ContainsKey(relativePath);

    public string ReadText(string relativePath) =>
        _files.TryGetValue(relativePath, out string? text) ? text : throw new FileNotFoundException(relativePath);

    private static string Normalize(string relativePath)
    {
        string trimmed = relativePath.Trim('/');
        return trimmed.Length == 0 ? string.Empty : trimmed + "/";
    }
}

    /// <summary>A party of two: one member holds skill points and one holds none, so both answers are shown.</summary>
    private static (string Path, string Text)[] RaiseContent() =>
    [
        ProductTestContext.Bundle("partyrpg-default", "world"),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/pack.json", """
            {
              "schemaVersion": 1,
              "packId": "world",
              "kind": "definitions",
              "provenance": { "description": "authored for a test" },
              "documents": [
                { "path": "places.json", "documentId": "places", "definitionKind": "place" },
                { "path": "start.json", "documentId": "start", "definitionKind": "scenario-start" },
                { "path": "party.json", "documentId": "party", "definitionKind": "scenario-party" },
                { "path": "skills.json", "documentId": "skills", "definitionKind": "skill" }
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/places.json", """
            {
              "documentId": "places",
              "definitionKind": "place",
              "entries": [
                { "id": "1", "kind": "interior", "name": "A quiet room", "respawnDays": 7,
                  "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
                  "placements": [] }
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/start.json", """
            { "documentId": "start", "definitionKind": "scenario-start", "entries": [ { "id": "start", "place": "1", "entryPoint": "Party Start" } ] }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/skills.json", """
            {
              "documentId": "skills",
              "definitionKind": "skill",
              "entries": [ { "id": "Fire" }, { "id": "Sword" } ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/party.json", """
            {
              "documentId": "party",
              "definitionKind": "scenario-party",
              "entries": [
                { "id": "party", "coins": 0, "food": 6, "reputation": 0, "fame": 0,
                  "members": [
                    { "name": "Aelina", "race": "Elf", "class": "Sorcerer", "level": 1, "hitPoints": 20,
                      "spellPoints": 15, "skillPoints": 200,
                      "attributes": [ { "id": "Intellect", "value": 30 } ],
                      "skills": [ { "id": "Fire", "level": 4, "tier": 1, "pointsSpent": 10 } ],
                      "spells": [], "conditions": [] },
                    { "name": "Roderick", "race": "Human", "class": "Knight", "level": 1, "hitPoints": 40,
                      "spellPoints": 0, "skillPoints": 0,
                      "attributes": [ { "id": "Might", "value": 17 } ],
                      "skills": [ { "id": "Sword", "level": 1, "tier": 1, "pointsSpent": 0 } ],
                      "spells": [], "conditions": [] }
                  ] }
              ] }
            """),
    ];

/// <summary>The repository root, found by walking up from the test's own output directory.</summary>
    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src"))
                && File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"The repository root is not above {AppContext.BaseDirectory}.");
    }
}
