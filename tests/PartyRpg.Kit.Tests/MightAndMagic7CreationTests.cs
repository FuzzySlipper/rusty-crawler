using System.Text.Json;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Party;
using PartyRpg.Rulesets.MightAndMagic7;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// This game's own creation definitions: every class and race the ruleset offers must be creatable, the
/// authored tables must say what the manual says, illegal choices must be refused by the rule they break, and
/// the content the product loads must agree with the classes and skills creation is built on.
/// </summary>
/// <remarks>
/// These tests reach the compiled ruleset on purpose: a sweep over the kit's own definitions would prove the
/// mechanism and not the game. Nothing here needs the operator's game data — the content fixture below is
/// written in memory — and the races and portraits are the ruleset's authored values, checked against the
/// manual's printed anchors and the donor transcription recorded in
/// <c>src/PartyRpg.Rulesets.MightAndMagic7/MightAndMagic7CreationTables.cs</c>.
/// </remarks>
public sealed class MightAndMagic7CreationTests
{
    /// <summary>The 37 shipped skills, as the imported skill document names them.</summary>
    private static readonly string[] ShippedSkills =
    [
        "Staff", "Sword", "Dagger", "Axe", "Spear", "Bow", "Mace", "Blaster", "Shield", "Leather", "Chain",
        "Plate", "Fire", "Air", "Water", "Earth", "Spirit", "Mind", "Body", "Light", "Dark", "Identify Item",
        "Merchant", "Repair", "Bodybuilding", "Meditation", "Perception", "Diplomacy", "Thievery",
        "Disarm Traps", "Dodging", "Unarmed", "Identify Monster", "Armsmaster", "Stealing", "Alchemy",
        "Learning",
    ];

    /// <summary>The nine base classes, as the imported class document names them.</summary>
    private static readonly string[] BaseClasses =
    [
        "Knight", "Thief", "Monk", "Paladin", "Archer", "Ranger", "Cleric", "Druid", "Sorcerer",
    ];

    [Fact]
    public void Every_base_class_and_race_the_ruleset_allows_can_be_created()
    {
        PartyCreationOptions options = MightAndMagic7Creation.Options();
        PartyEntityFactory factory = new();
        int combinations = 0;
        int characters = 0;

        foreach (CreationClass characterClass in options.Classes)
        {
            foreach (CreationRace race in options.Races)
            {
                combinations++;
                CreationPortrait portrait = options.Portraits.First(candidate => candidate.Race == race.Id);
                IReadOnlyList<int> clicks = SpendPool(race);
                SkillId[] chosen =
                [
                    .. characterClass.ChoosableSkills.Take(options.ChosenSkillCount),
                ];
                Assert.Equal(options.ChosenSkillCount, chosen.Length);

                // The whole party is created out of this one combination, so every slot creation offers is
                // exercised with a real class and race rather than only the first.
                PartyCreationFlow flow = new(options);
                for (int member = 0; member < options.MemberCount; member++)
                {
                    Assert.Null(flow.SelectPortrait(portrait.Id));
                    Assert.Null(flow.Advance());
                    Assert.Null(flow.SelectClass(characterClass.Id));
                    Assert.Null(flow.Advance());
                    Assert.Null(flow.SetName($"{characterClass.Id} {member + 1}"));
                    Assert.Null(flow.Advance());
                    for (int index = 0; index < race.Attributes.Count; index++)
                    {
                        for (int click = 0; click < clicks[index]; click++) Assert.Null(flow.RaiseAttribute(race.Attributes[index].Attribute));
                    }

                    Assert.Equal(0, flow.PoolRemaining);
                    Assert.Null(flow.Advance());
                    foreach (SkillId skill in chosen) Assert.Null(flow.ChooseSkill(skill));
                    Assert.Null(flow.Advance());
                }

                Assert.True(flow.IsComplete);
                using PartyEntity party = factory.Create(flow.ToCreation());
                Assert.Equal(options.MemberCount, party.Members.Count);
                foreach (PartyMember member in party.Members)
                {
                    characters++;
                    Assert.Equal(race.Id, member.Profile.Race);
                    Assert.Equal(characterClass.Id, member.Profile.Class);
                    Assert.Equal(1, member.Progression.Level);
                    Assert.Equal(MightAndMagic7Creation.StartingRank, member.Progression.ClassRank);
                    Assert.Equal(characterClass.StartingHitPoints, member.Resources.HitPoints.Maximum);
                    Assert.Equal(characterClass.StartingSpellPoints, member.Resources.SpellPoints.Maximum);
                    SkillId[] expectedSkills = [.. characterClass.FixedSkills, .. chosen];
                    Assert.Equal(expectedSkills, member.Skills.Entries.Select(entry => entry.Skill));
                    Assert.All(member.Skills.Entries, entry => Assert.Equal(MightAndMagic7Creation.StartingSkillTier, entry.Tier));

                    // Four skills: the two the class fixes and the two the player chose (manual p.12).
                    Assert.Equal(4, member.Skills.Count);
                    foreach (AttributeCreationRange range in race.Attributes)
                    {
                        Assert.True(
                            member.Attributes[range.Attribute] >= range.Start,
                            $"{characterClass.Id} {race.Name}: {range.Name} started below its starting value.");
                        Assert.True(member.Attributes[range.Attribute] <= range.Maximum);
                    }
                }
            }
        }

        // Nine base classes across four races: every combination the game allows, each as a whole party.
        Assert.Equal(36, combinations);
        Assert.Equal(36 * MightAndMagic7Creation.MemberCount, characters);
    }

    [Fact]
    public void The_authored_race_tables_carry_the_manual_anchors()
    {
        PartyCreationOptions options = MightAndMagic7Creation.Options();

        Assert.Equal(4, options.MemberCount);
        Assert.Equal(4, options.Races.Count);
        Assert.Equal(8, options.Portraits.Count);
        Assert.Equal(9, options.Classes.Count);
        Assert.Equal(50, options.AttributePool);
        Assert.Equal(2, options.ChosenSkillCount);

        // The manual's race-table anchors (manual outline §1, printed p.17): Human 9/11/25 across its
        // attributes, Elf Intellect 12/14/30, Goblin Might 12/14/30, Dwarf Endurance 12/14/30 and Dwarf
        // Accuracy 5/7/15 — read here as start, floor and ceiling.
        AssertRange(options, "Human", "Might", start: 11, floor: 9, ceiling: 25, stepSize: 1, stepCost: 1);
        AssertRange(options, "Human", "Intellect", start: 11, floor: 9, ceiling: 25, stepSize: 1, stepCost: 1);
        AssertRange(options, "Human", "Endurance", start: 9, floor: 7, ceiling: 25, stepSize: 1, stepCost: 1);
        AssertRange(options, "Human", "Luck", start: 9, floor: 7, ceiling: 25, stepSize: 1, stepCost: 1);

        // An elf pays two points for a point of might and gets two points of intellect for one, which is the
        // manual's "some attributes cost 2 points per +1, others give +2 per point spent" (p.12).
        AssertRange(options, "Elf", "Might", start: 7, floor: 5, ceiling: 15, stepSize: 1, stepCost: 2);
        AssertRange(options, "Elf", "Intellect", start: 14, floor: 12, ceiling: 30, stepSize: 2, stepCost: 1);
        AssertRange(options, "Goblin", "Might", start: 14, floor: 12, ceiling: 30, stepSize: 2, stepCost: 1);
        AssertRange(options, "Goblin", "Intellect", start: 7, floor: 5, ceiling: 15, stepSize: 1, stepCost: 2);
        AssertRange(options, "Dwarf", "Endurance", start: 14, floor: 12, ceiling: 30, stepSize: 2, stepCost: 1);
        AssertRange(options, "Dwarf", "Accuracy", start: 7, floor: 5, ceiling: 15, stepSize: 1, stepCost: 2);

        // Every race carries the game's seven attributes, and every portrait names a race creation offers.
        foreach (CreationRace race in options.Races)
        {
            Assert.Equal(7, race.Attributes.Count);
            Assert.Equal(
                new[] { "Might", "Intellect", "Personality", "Endurance", "Accuracy", "Speed", "Luck" },
                race.Attributes.Select(range => range.Attribute.Value));
        }

        foreach (CreationPortrait portrait in options.Portraits) Assert.NotNull(options.FindRace(portrait.Race));

        // The classes' starting hit and spell points are the manual's examples and the donor's base arrays,
        // which agree on every base class (manual p.17).
        AssertClass(options, "Knight", 40, 0, ["Sword", "Leather"]);
        AssertClass(options, "Thief", 35, 0, ["Dagger", "Stealing"]);
        AssertClass(options, "Monk", 35, 0, ["Dodging", "Unarmed"]);
        AssertClass(options, "Paladin", 30, 5, ["Mace", "Spirit"]);
        AssertClass(options, "Archer", 30, 5, ["Bow", "Air"]);
        AssertClass(options, "Ranger", 30, 0, ["Axe", "Perception"]);
        AssertClass(options, "Cleric", 25, 10, ["Mace", "Body"]);
        AssertClass(options, "Druid", 20, 10, ["Dagger", "Earth"]);
        AssertClass(options, "Sorcerer", 20, 15, ["Staff", "Fire"]);

        // Every class fixes two skills and offers nine choices, and no choice is also fixed.
        foreach (CreationClass characterClass in options.Classes)
        {
            Assert.Equal(2, characterClass.FixedSkills.Count);
            Assert.Equal(9, characterClass.ChoosableSkills.Count);
            Assert.All(characterClass.ChoosableSkills, skill => Assert.DoesNotContain(skill, characterClass.FixedSkills));
        }
    }

    [Fact]
    public void An_illegal_choice_is_refused_with_the_rule_it_broke()
    {
        PartyCreationOptions options = MightAndMagic7Creation.Options();

        // A portrait that is not offered, and a class that is not offered.
        PartyCreationFlow flow = new(options);
        Assert.Equal("portrait-unknown", flow.SelectPortrait(new PortraitId("knight-of-nowhere"))!.Code);
        Assert.Null(flow.SelectPortrait(Portrait("Human")));
        Assert.Null(flow.Advance());
        Assert.Equal("class-unknown", flow.SelectClass(new ClassId("Farmer"))!.Code);

        // A skill a class may not learn: the class decides, and the message names both.
        Assert.Null(flow.SelectClass(new ClassId("Knight")));
        Assert.Null(flow.Advance());
        Assert.Null(flow.SetName("Roderick"));
        Assert.Null(flow.Advance());
        Spend(flow, "Human", 50);
        Assert.Null(flow.Advance());
        PartyRefusal fire = flow.ChooseSkill(new SkillId("Fire"))!;
        Assert.Equal("skill-not-legal", fire.Code);
        Assert.Contains("'Fire' is not a skill the Knight class may learn at creation", fire.Message, StringComparison.Ordinal);
        Assert.Equal("skill-fixed", flow.ChooseSkill(new SkillId("Sword"))!.Code);

        // An attribute cannot pass the race's ceiling, its floor, or the pool that is left.
        PartyCreationFlow ceilings = Attributes(options, "Human", "Knight");
        for (int click = 0; click < 14; click++) Assert.Null(ceilings.RaiseAttribute(new AttributeId("Might")));
        Assert.Equal(25, AttributeOf(ceilings, "Might"));
        PartyRefusal might = ceilings.RaiseAttribute(new AttributeId("Might"))!;
        Assert.Equal("attribute-ceiling", might.Code);
        Assert.Contains("Might is already 25 and creation raises it at most to 25", might.Message, StringComparison.Ordinal);

        // Below its starting value an attribute moves by the amount that costs a point, so an elf's might
        // (7, two points per point) drops straight to its floor of 5 and no further.
        PartyCreationFlow floors = Attributes(options, "Elf", "Sorcerer");
        Assert.Null(floors.LowerAttribute(new AttributeId("Might")));
        Assert.Equal(5, AttributeOf(floors, "Might"));
        PartyRefusal below = floors.LowerAttribute(new AttributeId("Might"))!;
        Assert.Equal("attribute-floor", below.Code);
        Assert.Contains("creation lowers it at most to 5", below.Message, StringComparison.Ordinal);

        // Forty-nine of the fifty points are spent, and might costs two each: the last point cannot buy it.
        PartyCreationFlow short1 = Attributes(options, "Elf", "Sorcerer");
        Clicks(short1, "Personality", 14);
        Clicks(short1, "Speed", 14);
        Clicks(short1, "Luck", 11);
        Clicks(short1, "Might", 5);
        Assert.Equal(12, AttributeOf(short1, "Might"));
        Assert.Equal(1, short1.PoolRemaining);
        PartyRefusal poor = short1.RaiseAttribute(new AttributeId("Might"))!;
        Assert.Equal("attribute-pool-short", poor.Code);
        Assert.Contains("costs 2 of the 1 attribute point left", poor.Message, StringComparison.Ordinal);

        // The two rules that judge a whole step: the pool spent exactly and the skills chosen in full.
        PartyCreationFlow unspent = Attributes(options, "Human", "Knight");
        PartyRefusal unspentPool = unspent.Advance()!;
        Assert.Equal("attribute-pool-unspent", unspentPool.Code);
        Assert.Contains("50 remain unspent", unspentPool.Message, StringComparison.Ordinal);

        PartyCreationFlow skills = Attributes(options, "Human", "Knight");
        Spend(skills, "Human", 50);
        Assert.Null(skills.Advance());
        Assert.Null(skills.ChooseSkill(new SkillId("Shield")));
        PartyRefusal unchosen = skills.Advance()!;
        Assert.Equal("skills-unchosen", unchosen.Code);
        Assert.Contains("starts with 2 chosen skills and 1 remains unchosen", unchosen.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_default_party_is_offered_and_builds_a_party_a_save_round_trips()
    {
        PartyCreationOptions options = MightAndMagic7Creation.Options();
        PartyCreationFlow flow = new(options, MightAndMagic7Creation.Defaults);

        // The seam a session holds while creating: this game's creation, opened on its default party.
        PartyCreationFlow started = MightAndMagic7Creation.Start();
        Assert.True(started.IsComplete);
        Assert.Equal(4, started.ToCreation().Members.Count);

        Assert.True(flow.IsComplete);
        Assert.Equal(
            new[] { "Roderick", "Aelina", "Borin", "Nyx" },
            Enumerable.Range(0, flow.MemberCount).Select(index => flow.Member(index).Name));
        Assert.Equal(
            new[] { "Knight", "Sorcerer", "Cleric", "Thief" },
            Enumerable.Range(0, flow.MemberCount).Select(index => flow.Member(index).Class!.Value.Value));
        Assert.Equal(
            new[] { "Human", "Elf", "Dwarf", "Goblin" },
            Enumerable.Range(0, flow.MemberCount).Select(index => flow.Member(index).Race!.Value.Value));
        Assert.All(
            Enumerable.Range(0, flow.MemberCount),
            index => Assert.Equal(0, flow.Member(index).PoolRemaining));

        PartyEntityFactory factory = new();
        using PartyEntity party = factory.Create(flow.ToCreation());
        Assert.Equal(4, party.Members.Count);
        Assert.Equal(40, party.Members[0].Resources.HitPoints.Maximum);
        Assert.Equal(0, party.Members[0].Resources.SpellPoints.Maximum);
        Assert.Equal(20, party.Members[1].Resources.HitPoints.Maximum);
        Assert.Equal(15, party.Members[1].Resources.SpellPoints.Maximum);
        Assert.Equal(25, party.Members[2].Resources.HitPoints.Maximum);
        Assert.Equal(10, party.Members[2].Resources.SpellPoints.Maximum);
        Assert.Equal(35, party.Members[3].Resources.HitPoints.Maximum);
        Assert.Equal(0, party.Members[3].Resources.SpellPoints.Maximum);
        Assert.Equal(MightAndMagic7Creation.StartingCoins, party.Purse.Coins);
        Assert.Equal(MightAndMagic7Creation.StartingFoodPortions, party.Food.Portions);

        // The created party is the shape a save round-trips: capture and two restores agree by identity.
        PartySave save = party.Capture();
        using PartyEntity first = factory.Restore(save);
        using PartyEntity second = factory.Restore(save);
        Assert.Equal(party.Members.Select(member => member.Id), first.Members.Select(member => member.Id));
        Assert.Equal(first.Members.Select(member => member.Id), second.Members.Select(member => member.Id));
        Assert.Equal(5UL, save.NextMemberValue);

        // The default is offered, never trusted: a default that breaks a rule is refused by the same rule a
        // player's own choice would break.
        PartyCreationDefaults tampered = new(
        [
            new CreationMemberDefaults(
                new PortraitId("human-man"),
                new ClassId("Knight"),
                "Roderick",
                MightAndMagic7Creation.Defaults.Members[0].Attributes,
                [new SkillId("Fire"), new SkillId("Shield")]),
            .. MightAndMagic7Creation.Defaults.Members.Skip(1),
        ]);
        ArgumentException refused = Assert.Throws<ArgumentException>(() => new PartyCreationFlow(options, tampered));
        Assert.Contains("skill-not-legal", refused.Message, StringComparison.Ordinal);
        Assert.Contains("The default party's member 1 broke a creation rule", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Content_that_contradicts_the_class_or_skill_tables_is_refused()
    {
        // The operator's own tables, as the importer writes them: every base class, its promotions, and the
        // shipped skills. Creation is offered over that content unchanged.
        ContentCatalog catalog = Catalog(BaseClasses, ShippedSkills);
        Assert.True(catalog.IsValid);
        PartyCreationOptions options = MightAndMagic7Creation.Options(catalog);
        Assert.Equal(9, options.Classes.Count);
        Assert.Equal(BaseClasses, options.Classes.Select(characterClass => characterClass.Id.Value));

        // A class the content does not carry would let a player be created in a class this game's data has
        // no definition for, so the whole choice list is refused and the class is named.
        ContentValidationException missingClass = Assert.Throws<ContentValidationException>(
            () => MightAndMagic7Creation.Options(Catalog([.. BaseClasses.Where(name => name != "Cleric")], ShippedSkills)));
        Assert.Equal("creation-class-missing", Assert.Single(missingClass.Issues).Code);
        Assert.Contains("the class 'Cleric'", missingClass.Message, StringComparison.Ordinal);

        // A skill the content does not carry would grant a character a skill the data does not define.
        ContentValidationException missingSkill = Assert.Throws<ContentValidationException>(
            () => MightAndMagic7Creation.Options(Catalog(BaseClasses, [.. ShippedSkills.Where(name => name != "Dodging")])));
        Assert.All(missingSkill.Issues, issue => Assert.Equal("creation-skill-missing", issue.Code));
        Assert.Contains("uses the skill 'Dodging'", missingSkill.Message, StringComparison.Ordinal);

        // A base class the content carries and this ruleset has no creation rules for could be played but
        // never created, so it is named rather than ignored.
        ContentValidationException unknownClass = Assert.Throws<ContentValidationException>(
            () => MightAndMagic7Creation.Options(Catalog([.. BaseClasses, "Warlord"], ShippedSkills)));
        Assert.Equal("creation-class-unknown", Assert.Single(unknownClass.Issues).Code);
        Assert.Contains("the base class 'Warlord'", unknownClass.Message, StringComparison.Ordinal);
    }

    /// <summary>Finds a portrait drawn as one of the four races.</summary>
    private static PortraitId Portrait(string race) =>
        MightAndMagic7Creation.Options().Portraits.First(candidate => candidate.Race.Value == race).Id;

    /// <summary>A flow standing at the attribute step of its first member, created in a class and race.</summary>
    private static PartyCreationFlow Attributes(PartyCreationOptions options, string race, string characterClass)
    {
        PartyCreationFlow flow = new(options);
        Assert.Null(flow.SelectPortrait(Portrait(race)));
        Assert.Null(flow.Advance());
        Assert.Null(flow.SelectClass(new ClassId(characterClass)));
        Assert.Null(flow.Advance());
        Assert.Null(flow.SetName("Tester"));
        Assert.Null(flow.Advance());
        return flow;
    }

    /// <summary>Spends a pool of points on a race's attributes, in the race's own order.</summary>
    private static void Spend(PartyCreationFlow flow, string race, int points)
    {
        CreationRace definition = MightAndMagic7Creation.Options().FindRace(new RaceId(race))!;
        IReadOnlyList<int> clicks = Spend(definition, points);
        for (int index = 0; index < definition.Attributes.Count; index++)
        {
            for (int click = 0; click < clicks[index]; click++) Assert.Null(flow.RaiseAttribute(definition.Attributes[index].Attribute));
        }

        Assert.Equal(MightAndMagic7Creation.AttributePool - points, flow.PoolRemaining);
    }

    /// <summary>Raises one attribute a number of times, each one having to be admitted.</summary>
    private static void Clicks(PartyCreationFlow flow, string attribute, int clicks)
    {
        for (int click = 0; click < clicks; click++) Assert.Null(flow.RaiseAttribute(new AttributeId(attribute)));
    }

    /// <summary>Spends the whole pool on a race, which is what the sweep needs for every race.</summary>
    private static IReadOnlyList<int> SpendPool(CreationRace race) => Spend(race, MightAndMagic7Creation.AttributePool);

    /// <summary>
    /// Finds how many adjustments each attribute takes to spend a number of points exactly, searching the
    /// race's own ranges. A race that could not spend its whole pool would not be creatable at all.
    /// </summary>
    private static IReadOnlyList<int> Spend(CreationRace race, int points)
    {
        int[] clicks = new int[race.Attributes.Count];
        if (Place(0, points)) return clicks;

        throw new InvalidOperationException(
            $"Race '{race.Name}' cannot spend {points} attribute points at creation, so no character of it could be created.");

        bool Place(int index, int remaining)
        {
            if (index == race.Attributes.Count) return remaining == 0;
            AttributeCreationRange range = race.Attributes[index];
            int maximum = (range.Maximum - range.Start) / range.StepSize;
            for (int taken = maximum; taken >= 0; taken--)
            {
                int cost = taken * range.StepCost;
                if (cost > remaining) continue;
                clicks[index] = taken;
                if (Place(index + 1, remaining - cost)) return true;
            }

            clicks[index] = 0;
            return false;
        }
    }

    /// <summary>Reads one attribute of the member being created.</summary>
    private static int AttributeOf(PartyCreationFlow flow, string attribute)
    {
        foreach (AttributeScore score in flow.Member(flow.MemberIndex).Attributes)
        {
            if (score.Attribute.Value == attribute) return score.Value;
        }

        throw new InvalidOperationException($"The member being created has no '{attribute}'.");
    }

    /// <summary>Checks one race's attribute range against the manual's anchor.</summary>
    private static void AssertRange(
        PartyCreationOptions options,
        string race,
        string attribute,
        int start,
        int floor,
        int ceiling,
        int stepSize,
        int stepCost)
    {
        CreationRace definition = options.FindRace(new RaceId(race))!;
        Assert.True(definition.TryAttribute(new AttributeId(attribute), out AttributeCreationRange? range));
        Assert.Equal(start, range!.Start);
        Assert.Equal(floor, range.Minimum);
        Assert.Equal(ceiling, range.Maximum);
        Assert.Equal(stepSize, range.StepSize);
        Assert.Equal(stepCost, range.StepCost);
    }

    /// <summary>Checks one class's starting pools and fixed skills.</summary>
    private static void AssertClass(
        PartyCreationOptions options,
        string name,
        int hitPoints,
        int spellPoints,
        string[] fixedSkills)
    {
        CreationClass characterClass = options.FindClass(new ClassId(name))!;
        Assert.Equal(hitPoints, characterClass.StartingHitPoints);
        Assert.Equal(spellPoints, characterClass.StartingSpellPoints);
        Assert.Equal(fixedSkills, characterClass.FixedSkills.Select(skill => skill.Value));
        Assert.All(characterClass.FixedSkills.Concat(characterClass.ChoosableSkills), skill => Assert.Contains(skill.Value, ShippedSkills));
    }

    /// <summary>
    /// A content catalog written in memory: one definitions pack carrying the class and skill documents the
    /// importer writes, with the classes and skills the test wants it to declare.
    /// </summary>
    private static ContentCatalog Catalog(IReadOnlyList<string> baseClasses, IReadOnlyList<string> skills)
    {
        List<object> classes = [];
        int rank = 0;
        foreach (string name in baseClasses)
        {
            rank++;
            classes.Add(new { id = name, description = $"the {name} class", baseClass = name, rank });
            classes.Add(new { id = $"Promoted {name}", description = "a promotion", baseClass = name, rank = rank + 100 });
        }

        List<object> skillEntries = [.. skills.Select(name => (object)new { id = name, description = $"the {name} skill" })];
        InMemoryContentSource source = new InMemoryContentSource()
            .Add("packs/mm7-tables/pack.json", Manifest())
            .Add("packs/mm7-tables/classes.json", Document("classes", "class", classes))
            .Add("packs/mm7-tables/skills.json", Document("skills", "skill", skillEntries));
        return ContentCatalogLoader.Load(source, new ContentLayout("packs", "imports", "bundles"));
    }

    /// <summary>Writes one definitions document the way the importer writes it.</summary>
    private static string Document(string documentId, string definitionKind, IReadOnlyList<object> entries) =>
        JsonSerializer.Serialize(new { schemaVersion = 1, documentId, definitionKind, entries });

    /// <summary>Writes the pack manifest that declares the two table documents.</summary>
    private static string Manifest() => """
        {
          "schemaVersion": 1,
          "packId": "mm7-tables",
          "kind": "definitions",
          "provenance": { "description": "written in memory by the creation tests" },
          "documents": [
            { "path": "classes.json", "documentId": "classes", "definitionKind": "class" },
            { "path": "skills.json", "documentId": "skills", "definitionKind": "skill" }
          ]
        }
        """;
}
