using System.Globalization;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Progression;
using PartyRpg.Kit.Sessions;
using PartyRpg.Rulesets.MightAndMagic7;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Host.Tests;

/// <summary>
/// This game's progression against the real composition: a creature's own row paying the party through the
/// one award path, a training hall turning banked experience into the level this game's curves state, and
/// the panel publishing what a player acts on.
/// </summary>
/// <remarks>
/// <para>
/// The kit's own suite proves the owner with rules it states itself. What only this suite can prove is that
/// this game's numbers reach it: the experience column of an imported monster row becomes the award a fight
/// makes, the class and rank tables become the pools a level grows, the donor's skill point grant becomes
/// the points a member holds, and the hall's own fee and ceiling come from content.
/// </para>
/// <para>
/// The content is written the way the importer writes it — a monster row with the table's own columns, a
/// service entry carrying the building table's own fields and nothing about what the kind offers — so the
/// same suite proves the shipped policy rather than a fixture invented for it. Nothing here needs the
/// operator's installation; the live check is what runs against it.
/// </para>
/// </remarks>
public sealed class ProgressionPolicyTests
{
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

    [Fact]
    public void A_creature_s_own_row_pays_the_party_through_the_one_award_path_and_the_panel_shows_it()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(FightContent());
        FakeEngineContext fake = (FakeEngineContext)context.Engine;

        // Every blow lands, so the kill is the row's own numbers rather than a run of luck: what is under
        // test is what a death pays, not whether it happens.
        fake.RandomService.Answer = request => request.Key.EndsWith("/hit", StringComparison.Ordinal) ? 0 : 1;

        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            ProductTestContext.RulesetContext(context, ui, combat: true));
        session.Start();
        ulong step = 0;
        session.Update(ProductTestContext.Update(++step, 1));

        // A party that has earned nothing: the curve is published beside what it has banked, and no counter
        // stands here to quote a fee.
        ProjectedNode fresh = Progression(ui);
        Assert.True(fresh.Field("available").AsBoolean());
        Assert.Equal("none", fresh.Field("outcome").AsString());
        Assert.Equal(2, fresh.Field("members").Length());
        Assert.Equal(1, fresh.Field("members").Item(0).Field("level").AsNumber());
        Assert.Equal(0, fresh.Field("members").Item(0).Field("experience").AsNumber());
        Assert.Equal(1000, fresh.Field("members").Item(0).Field("nextLevel").AsNumber());
        Assert.Equal(0, fresh.Field("members").Item(0).Field("fee").AsNumber());
        Assert.Equal(0, fresh.Field("members").Item(0).Field("cap").AsNumber());

        // The fight is played in rounds, because a round is what lets the party act again: this game paces a
        // fight by recovery, and a paced round spends the game time a swing owes.
        session.Update(ProductTestContext.Update(++step, 1, ProductTestContext.Digital(ProductIdentity.TurnBasedToggleIntent)));

        // The party attacks until the creature is down. The row is worth three thousand experience and two
        // members can earn, so the donor's division gives each of them fifteen hundred.
        ProjectedNode combat = ProjectedNode.Of(ui.Latest().Value).Field("combat");
        for (int press = 0; press < 30 && !Down(combat); press++)
        {
            session.Update(ProductTestContext.Update(++step, 1, ProductTestContext.Digital(ProductIdentity.AttackIntent)));
            session.Update(ProductTestContext.Update(++step, 1, ProductTestContext.Digital(ProductIdentity.AttackIntent, InputEdge.Released)));
            combat = ProjectedNode.Of(ui.Latest().Value).Field("combat");
        }

        Assert.True(Down(combat), $"the creature never went down: {combat.Field("message").AsString()}");
        ProjectedNode awarded = Progression(ui);
        Assert.Equal("awarded", awarded.Field("outcome").AsString());
        Assert.Equal("kill", awarded.Field("source").AsString());
        Assert.Equal(3000, awarded.Field("earned").AsNumber());
        Assert.Equal(1500, awarded.Field("members").Item(0).Field("experience").AsNumber());
        Assert.Equal(1500, awarded.Field("members").Item(1).Field("experience").AsNumber());
        Assert.Equal(1000, awarded.Field("members").Item(0).Field("nextLevel").AsNumber());

        // The award moves the party's own standing through the owner: the donor derives fame from what the
        // party has earned, a thousand experience to a point, so three thousand is three (OpenEnroth
        // src/Engine/Party.cpp:371-379).
        ProjectedNode party = ProjectedNode.Of(ui.Latest().Value).Field("party");
        Assert.Equal(3, party.Field("fame").AsNumber());
        Assert.Equal(0, party.Field("reputation").AsNumber());

        // The fight keeps reading the same death, and a death pays once: the panel is unchanged however many
        // updates the body lies there (OpenEnroth src/Engine/Objects/Actor.cpp:3164-3167 pays on the kill).
        for (int update = 0; update < 4; update++) session.Update(ProductTestContext.Update(++step, 1));
        Assert.Equal(1500, Progression(ui).Field("members").Item(0).Field("experience").AsNumber());
    }

    [Fact]
    public void An_imported_training_hall_turns_banked_experience_into_the_level_this_game_s_tables_state()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(HallContent());
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            ProductTestContext.RulesetContext(context, ui) with
            {
                Use = UseControls,
                Service = ServiceControls,
                Conversation = ConversationControls,
            });
        session.Start();
        ulong step = 0;
        session.Update(ProductTestContext.Update(++step, 1));

        // What the party has banked is content's own statement: this scenario starts a knight who has earned
        // a hundred thousand experience and not yet spent any of it on a level.
        ProjectedNode before = Progression(ui);
        Assert.Equal(100000, before.Field("members").Item(0).Field("experience").AsNumber());
        Assert.Equal(1000, before.Field("members").Item(0).Field("nextLevel").AsNumber());
        Assert.Equal(0, before.Field("members").Item(0).Field("fee").AsNumber());

        // The counter is entered the one way a counter is entered: the party talks to whoever keeps it.
        Assert.Equal("Sergeant Hale", ProjectedNode.Of(ui.Latest().Value).Field("interaction").Field("label").AsString());
        session.Update(ProductTestContext.Update(++step, 1, ProductTestContext.Digital(ProductIdentity.UseIntent)));
        session.Update(ProductTestContext.Update(++step, 1, ProductTestContext.ChooseTopic(MightAndMagic7Conversation.CounterTopicId)));
        ProjectedNode hall = ProjectedNode.Of(ui.Latest().Value).Field("service");
        Assert.True(hall.Field("open").AsBoolean());
        Assert.Equal("Island Training Grounds", hall.Field("name").AsString());

        // The fee for the next level is the counter's own quote and the ceiling is content's own number —
        // ten a level for a hall whose multiplier is ten, up to the five this hall trains — and the panel
        // publishes both per member rather than working either out.
        ProjectedNode quoted = Progression(ui);
        Assert.Equal(10, quoted.Field("members").Item(0).Field("fee").AsNumber());
        Assert.Equal(5, quoted.Field("members").Item(0).Field("cap").AsNumber());

        // One step is one level: the purse pays the fee, the level rises, the pools grow by the knight's own
        // row of the donor's table, and the new level grants five skill points.
        session.Update(ProductTestContext.Update(++step, 1, ProductTestContext.Payload("""{"action":"service.train","member":0}""")));
        ProjectedNode trained = Progression(ui);
        Assert.Equal("trained", trained.Field("outcome").AsString());
        Assert.Equal(2, trained.Field("members").Item(0).Field("level").AsNumber());
        Assert.Equal(100000, trained.Field("members").Item(0).Field("experience").AsNumber());
        Assert.Equal(3000, trained.Field("members").Item(0).Field("nextLevel").AsNumber());
        Assert.Equal(5, trained.Field("members").Item(0).Field("skillPoints").AsNumber());
        Assert.Matches("level 2", trained.Field("message").AsString());
        Assert.Equal(45, ProjectedNode.Of(ui.Latest().Value).Field("party").Field("hitPointsMax").AsNumber());
        ProjectedNode paid = ProjectedNode.Of(ui.Latest().Value).Field("service");
        Assert.Equal(10, paid.Field("paid").AsNumber());
        Assert.Equal(4990, paid.Field("coins").AsNumber());

        // The hall's ceiling is content's own number, and it is the service's own refusal that says so: the
        // last step it may sell takes the member to five, and the next is refused by name.
        for (int level = 2; level < 5; level++)
        {
            session.Update(ProductTestContext.Update(++step, 1, ProductTestContext.Payload("""{"action":"service.train","member":0}""")));
            Assert.Equal("applied", ProjectedNode.Of(ui.Latest().Value).Field("service").Field("outcome").AsString());
        }

        ProjectedNode capped = Progression(ui);
        Assert.Equal(5, capped.Field("members").Item(0).Field("level").AsNumber());
        Assert.Equal(20, capped.Field("members").Item(0).Field("skillPoints").AsNumber());
        // Five knight levels of five hit points each over the forty content states.
        Assert.Equal(60, ProjectedNode.Of(ui.Latest().Value).Field("party").Field("hitPointsMax").AsNumber());

        session.Update(ProductTestContext.Update(++step, 1, ProductTestContext.Payload("""{"action":"service.train","member":0}""")));
        ProjectedNode refused = ProjectedNode.Of(ui.Latest().Value).Field("service");
        Assert.Equal("refused", refused.Field("outcome").AsString());
        Assert.Equal("service-training-capped", refused.Field("code").AsString());
        Assert.Equal(5, Progression(ui).Field("members").Item(0).Field("level").AsNumber());

        // And the fee the party paid is the fees the levels cost: ten, twenty, thirty, and forty for the four
        // steps this hall sells a member at level one (its multiplier times the level times the class tier).
        Assert.Equal(4900, ProjectedNode.Of(ui.Latest().Value).Field("service").Field("coins").AsNumber());
    }

    [Fact]
    public void Every_class_and_rank_grows_by_the_table_this_game_states_and_a_level_grants_the_donor_s_points()
    {
        // The donor's own per-level tables, row for row: hit points and spell points for the nine base
        // classes across their three tiers (OpenEnroth src/Engine/Objects/Character.cpp:135-172 and :173-209).
        // The two second-promotion alternatives of a class state the same pair of numbers, which is why one
        // entry covers tier three.
        (string Class, int[] HitPoints, int[] SpellPoints)[] table =
        [
            ("Knight", [5, 7, 9], [0, 0, 0]),
            ("Thief", [4, 6, 8], [0, 1, 1]),
            ("Monk", [5, 6, 8], [0, 1, 1]),
            ("Paladin", [4, 5, 6], [1, 2, 3]),
            ("Archer", [3, 4, 6], [1, 2, 3]),
            ("Ranger", [4, 5, 6], [0, 2, 3]),
            ("Cleric", [2, 3, 4], [3, 4, 5]),
            ("Druid", [2, 3, 4], [3, 4, 5]),
            ("Sorcerer", [2, 3, 3], [3, 4, 6]),
        ];

        using PartyEntity party = Party(table[0].Class, level: 2, rank: 1);
        PartyMember member = party.Members[0];
        foreach ((string characterClass, int[] hitPoints, int[] spellPoints) in table)
        {
            for (int rank = 1; rank <= 3; rank++)
            {
                using PartyEntity one = Party(characterClass, level: 2, rank: rank);
                ProgressionGrowth growth = MightAndMagic7Progression.Instance.Growth(
                    new ProgressionGrowthRequest(one.Members[0], 2));
                Assert.Equal(hitPoints[rank - 1], growth.HitPoints);
                Assert.Equal(spellPoints[rank - 1], growth.SpellPoints);
            }
        }

        // The points a level grants are the donor's own arithmetic over the new level: five a level, six from
        // level ten, seven from level twenty (OpenEnroth src/GUI/UI/Houses/Training.cpp:72).
        foreach ((int level, int points) in new[] { (2, 5), (9, 5), (10, 6), (19, 6), (20, 7), (30, 8) })
        {
            Assert.Equal(
                points,
                MightAndMagic7Progression.Instance.Growth(new ProgressionGrowthRequest(member, level)).SkillPoints);
        }

        // The curve is the donor's cumulative one, and the same figure the hall judges a member against:
        // a thousand times the level times the level plus one, halved.
        Assert.Equal(1000, MightAndMagic7Progression.Instance.ExperienceForLevel(1));
        Assert.Equal(3000, MightAndMagic7Progression.Instance.ExperienceForLevel(2));
        Assert.Equal(6000, MightAndMagic7Progression.Instance.ExperienceForLevel(3));

        // A class or a rank this game describes no growth for is refused by name rather than growing by
        // nothing, which is what a content set and this ruleset disagreeing about a class ladder looks like.
        using PartyEntity stranger = Party("Bard", level: 2, rank: 1);
        InvalidOperationException unknown = Assert.Throws<InvalidOperationException>(() =>
            MightAndMagic7Progression.Instance.Growth(new ProgressionGrowthRequest(stranger.Members[0], 2)));
        Assert.Contains("Bard", unknown.Message, StringComparison.Ordinal);

        using PartyEntity promoted = Party("Knight", level: 2, rank: 9);
        Assert.Throws<InvalidOperationException>(() =>
            MightAndMagic7Progression.Instance.Growth(new ProgressionGrowthRequest(promoted.Members[0], 2)));
    }

    [Fact]
    public void An_award_divides_by_the_donor_s_rule_and_the_learning_skill_raises_a_member_s_share()
    {
        using PartyEntity party = PartyOfTwo(learning: "Aelina");
        ProgressionAwardResult awarded = new PartyProgression(MightAndMagic7Progression.Instance, party)
            .Award(new PartyExperienceAward("quest", 1000));

        // Two members can earn, so the donor's division gives each half, and the learned one takes nine
        // percent more plus one percent for each of her three levels of the skill: five hundred and sixty
        // (OpenEnroth src/Engine/Party.cpp:837-855 and src/Engine/Objects/Character.cpp:625-641).
        Assert.True(awarded.IsAwarded);
        Assert.Equal(500, awarded.Shares.Single(share => share.Name == "Roderick").Amount);
        Assert.Equal(560, awarded.Shares.Single(share => share.Name == "Aelina").Amount);

        // A member who is down takes no share and is not counted: the one who is standing takes all of it,
        // and he carries no learning skill, so the whole thousand is his.
        party.Members[1].Conditions.Apply(new ActiveCondition(new ConditionId("Unconscious")));
        ProgressionAwardResult alone = new PartyProgression(MightAndMagic7Progression.Instance, party)
            .Award(new PartyExperienceAward("kill", 1000));
        Assert.Equal(1000, alone.Shares.Single().Amount);
        Assert.Equal("Roderick", alone.Shares.Single().Name);
    }

    private static ProjectedNode Progression(RecordingUiService ui) => ProjectedNode.Of(ui.Latest().Value).Field("progression");

    /// <summary>Whether the party's own blow has taken the creature down, which is what a kill is.</summary>
    private static bool Down(ProjectedNode combat) =>
        combat.Field("resolved").AsBoolean()
        && combat.Field("targetDown").AsBoolean()
        && combat.Field("byParty").AsBoolean();

    /// <summary>One party member of a stated class, level, and rank, for reading a growth table.</summary>
    private static PartyEntity Party(string characterClass, int level, int rank) =>
        new PartyEntityFactory().Create(new PartyCreation(
            [new MemberCreation(Seed("Tester", characterClass, level, rank, skills: [], hitPoints: 40, spellPoints: 10))],
            coins: 0,
            foodPortions: 0,
            reputation: 0,
            fame: 0));

    /// <summary>Two members, one of whom may carry the learning skill at three levels.</summary>
    private static PartyEntity PartyOfTwo(string? learning) =>
        new PartyEntityFactory().Create(new PartyCreation(
            [
                new MemberCreation(Seed("Roderick", "Knight", 1, 1, [], 40, 0)),
                new MemberCreation(Seed(
                    "Aelina",
                    "Sorcerer",
                    1,
                    1,
                    string.Equals(learning, "Aelina", StringComparison.Ordinal)
                        ? [new SkillEntry(new SkillId("Learning"), 3, new SkillTier(1), 6)]
                        : [],
                    24,
                    15)),
            ],
            coins: 0,
            foodPortions: 0,
            reputation: 0,
            fame: 0));

    private static PartyMemberSeed Seed(
        string name,
        string characterClass,
        int level,
        int rank,
        IReadOnlyList<SkillEntry> skills,
        int hitPoints,
        int spellPoints) => new(
        name,
        new RaceId("Human"),
        new ClassId(characterClass),
        [new AttributeScore(new AttributeId("Might"), 13)],
        skills,
        spells: [],
        experience: 0,
        level: level,
        skillPoints: 0,
        classRank: rank,
        conditions: [],
        hitPoints: ResourcePool.Full(hitPoints),
        spellPoints: ResourcePool.Full(spellPoints));

    /// <summary>
    /// A place holding one creature whose own row states what its death is worth, and a party of two.
    /// </summary>
    private static (string Path, string Text)[] FightContent() =>
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
                { "path": "monsters.json", "documentId": "monsters", "definitionKind": "monster" },
                { "path": "start.json", "documentId": "start", "definitionKind": "scenario-start" },
                { "path": "party.json", "documentId": "party", "definitionKind": "scenario-party" }
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/places.json",
            """
            {
              "documentId": "places",
              "definitionKind": "place",
              "entries": [
                { "id": "1", "kind": "region", "name": "Home", "respawnDays": 1,
                  "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 512 } ],
                  "placements": [
                    { "id": "beast", "kind": "monster", "monster": "7", "x": 60, "y": 0, "z": 0 } ] }
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/start.json",
            """
            { "documentId": "start", "definitionKind": "scenario-start", "entries": [ { "id": "start", "place": "1", "entryPoint": "Party Start" } ] }
            """),
        MonsterRow(experience: 3000),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/party.json",
            """
            {
              "documentId": "party",
              "definitionKind": "scenario-party",
              "entries": [
                { "id": "party", "coins": 5000, "food": 6, "reputation": 0, "fame": 0,
                  "members": [
                    { "name": "Roderick", "race": "Human", "class": "Knight", "level": 1, "hitPoints": 40,
                      "spellPoints": 0,
                      "attributes": [ { "id": "Might", "value": 17 }, { "id": "Accuracy", "value": 15 },
                                      { "id": "Endurance", "value": 15 }, { "id": "Luck", "value": 11 },
                                      { "id": "Speed", "value": 17 } ],
                      "skills": [], "spells": [], "conditions": [] },
                    { "name": "Aelina", "race": "Elf", "class": "Sorcerer", "level": 1, "hitPoints": 24,
                      "spellPoints": 15,
                      "attributes": [ { "id": "Might", "value": 11 }, { "id": "Accuracy", "value": 15 },
                                      { "id": "Endurance", "value": 11 }, { "id": "Luck", "value": 15 },
                                      { "id": "Speed", "value": 25 } ],
                      "skills": [], "spells": [], "conditions": [] }
                  ] }
              ]
            }
            """),
    ];

    /// <summary>
    /// A hall written the way the importer writes one — the building table's own fields and nothing about
    /// what the kind offers — and a party that has banked the experience a level takes.
    /// </summary>
    private static (string Path, string Text)[] HallContent() =>
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
                { "path": "services.json", "documentId": "services", "definitionKind": "service" },
                { "path": "start.json", "documentId": "start", "definitionKind": "scenario-start" },
                { "path": "party.json", "documentId": "party", "definitionKind": "scenario-party" }
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/places.json",
            """
            {
              "documentId": "places",
              "definitionKind": "place",
              "entries": [
                { "id": "1", "kind": "interior", "name": "Training Grounds", "respawnDays": 7,
                  "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
                  "placements": [ { "id": "hall", "kind": "service", "x": 100, "y": 0, "z": 0 } ] }
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/start.json",
            """
            { "documentId": "start", "definitionKind": "scenario-start", "entries": [ { "id": "start", "place": "1", "entryPoint": "Party Start" } ] }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/services.json",
            """
            {
              "documentId": "services",
              "definitionKind": "service",
              "entries": [
                { "id": "hall", "kind": "Training", "name": "Island Training Grounds", "proprietor": "Sergeant Hale",
                  "mapId": 1, "typeSequence": 1, "openHour": 6, "closedHour": 18,
                  "priceMultiplier": 10, "skillPriceMultiplier": 1, "trainingCap": 5, "trainingCapText": "5" }
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/party.json",
            """
            {
              "documentId": "party",
              "definitionKind": "scenario-party",
              "entries": [
                { "id": "party", "coins": 5000, "food": 6, "reputation": 0, "fame": 0,
                  "members": [
                    { "name": "Roderick", "race": "Human", "class": "Knight", "level": 1, "hitPoints": 40,
                      "spellPoints": 0, "experience": 100000,
                      "attributes": [ { "id": "Might", "value": 17 } ], "skills": [], "spells": [], "conditions": [] }
                  ] }
              ]
            }
            """),
    ];

    /// <summary>
    /// A monster row shaped the way the importer emits one: typed columns, the whole raw row, and the
    /// experience the shipped table states for it.
    /// </summary>
    private static (string Path, string Text) MonsterRow(int experience, int hitPoints = 5) =>
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/monsters.json",
            $$"""
            {
              "documentId": "monsters",
              "definitionKind": "monster",
              "entries": [
                { "id": "7", "name": "A beast", "level": 2, "hitPoints": {{hitPoints}}, "armorClass": 1,
                  "experience": {{experience}}, "hostility": 2, "recovery": 100,
                  "columns": [ {{Columns(hitPoints)}} ] }
              ]
            }
            """);

    /// <summary>The raw columns a row's fight is read from, with the hit points this row states.</summary>
    private static string Columns(int hitPoints)
    {
        string[] cells = new string[39];
        for (int index = 0; index < cells.Length; index++) cells[index] = "0";
        cells[0] = "7";
        cells[1] = "A beast";
        cells[3] = "2";
        cells[4] = hitPoints.ToString(CultureInfo.InvariantCulture);
        cells[5] = "1";
        cells[12] = "2";
        cells[14] = "100";
        cells[17] = "Phys";
        cells[18] = "1D4+4";
        return string.Join(", ", cells.Select(cell => $"\"{cell}\""));
    }
}
