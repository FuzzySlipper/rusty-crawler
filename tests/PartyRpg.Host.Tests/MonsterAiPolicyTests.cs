using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.World;
using PartyRpg.Rulesets.MightAndMagic7;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Host.Tests;

/// <summary>
/// This game's monster policy: who hates whom, what a creature does with its moment, how fast it moves, what
/// a person standing in the world is worth, and an encounter driven from a key to a kill.
/// </summary>
/// <remarks>
/// <para>
/// What the kit cannot state is what this game's data says, so every test here reads the shipped tables'
/// own columns through the policy that consumes them: the hostility matrix, the AI class that decides
/// whether a creature runs, the speed and movement columns, the attack and spell columns with their own
/// chances, and the monster row a person's own actor record names.
/// </para>
/// <para>
/// The last test is the live half: a session over content, the act control pressed through the product's
/// own intent, and a creature driven by this policy until it goes down — so the declaration, the driver, the
/// fight, and the panel are one path rather than four claims.
/// </para>
/// </remarks>
public sealed class MonsterAiPolicyTests
{
    [Fact]
    public void Two_kinds_are_each_others_enemies_when_the_matrix_content_says_so()
    {
        Fixture fixture = Fixture.Of();
        CombatState fight = fixture.Fight();
        MightAndMagic7MonsterAi ai = fixture.Ai;

        // The matrix the content carries states one feud: the kind the beast belongs to hates the rival's
        // kind with the widest band, and the rival's kind hates it back more mildly. Nothing else is
        // anybody's enemy, and a creature is its own kind's friend.
        CombatSubject beast = fixture.Subject(fight, "beast");
        CombatSubject rival = fixture.Subject(fight, "rival");
        CombatSubject neighbour = fixture.Subject(fight, "neighbour");
        Assert.True(ai.AreEnemies(beast, rival));
        Assert.True(ai.AreEnemies(rival, beast));
        Assert.False(ai.AreEnemies(beast, neighbour));
        Assert.False(ai.AreEnemies(beast, beast));

        // The party is never an enemy by this question: whether a creature fights the party is what its own
        // hostility band states, which the fight has already read.
        Assert.False(ai.AreEnemies(beast, fight.Combatants[0].Subject));
        Assert.Equal(3, ai.Kinds);
    }

    [Fact]
    public void Two_kinds_are_not_enemies_when_the_content_carries_no_matrix_at_all()
    {
        // A product whose content states no relationships is a product where nobody hates anybody by kind:
        // the reading is content's, so an absent matrix makes no creatures enemies rather than falling back
        // on a table this ruleset keeps.
        Fixture fixture = Fixture.Of(hostility: false);
        CombatState fight = fixture.Fight();
        Assert.False(fixture.Ai.AreEnemies(fixture.Subject(fight, "beast"), fixture.Subject(fight, "rival")));
        Assert.Equal(0, fixture.Ai.Kinds);
    }

    [Fact]
    public void A_creature_runs_by_its_own_ai_class_and_its_own_wounds()
    {
        Fixture fixture = Fixture.Of(engineRoll: 0);
        CombatState fight = fixture.Fight();
        MightAndMagic7MonsterAi ai = fixture.Ai;

        // Four rows, four classes, and the same wound read against each: the shipped column is what decides,
        // and the thresholds are the donor's own — always for a wimp, twenty percent for a normal creature,
        // ten for an aggressive one, never for a suicide.
        Assert.Equal("backing away", Action(ai, fight, "wimp"));
        Assert.Equal("attacking", Action(ai, fight, "normal"));
        Assert.Equal("backing away", Action(ai, fight, "normal", leave: 1));
        Assert.Equal("attacking", Action(ai, fight, "aggressive", leave: 5));
        Assert.Equal("attacking", Action(ai, fight, "aggressive", leave: 1));
        Assert.Equal("attacking", Action(ai, fight, "suicide", leave: 1));

        // A creature that holds its post does not run from it: the movement column is what says so, and a
        // wimp at a post stands rather than fleeing.
        Assert.Equal("waiting", Action(ai, fight, "statue"));
    }

    [Fact]
    public void A_creature_uses_the_ability_its_own_chances_choose()
    {
        Fixture fixture = Fixture.Of(random: new AlwaysRolls(0), engineRoll: 0);
        CombatState fight = fixture.Fight();
        MightAndMagic7MonsterAi ai = fixture.Ai;
        MightAndMagic7Combat combat = fixture.Combat;

        // The chance columns are the table's and the order is the donor's own: the first spell is rolled for,
        // then the second, then the second attack, and the first attack is what is left. This row casts its
        // first spell every time, and what the spell does is the spell's own content — it is a fire spell.
        CreatureDecision spell = Decide(fixture, ai, fight, "caster");
        Assert.Equal(CreatureAction.Attack, spell.Action);
        Assert.Equal(AttackKind.Spell, spell.Kind);
        Assert.Equal(MightAndMagic7Combat.AbilitySpell1, spell.Ability);
        AttackPlan cast = combat.PlanOfAbility(fixture.Subject(fight, "caster"), fight.Combatants[0].Subject, AttackKind.Spell, spell.Ability);
        Assert.Equal("Fire", cast.Kind.Value);

        // A row whose chance is for a second attack uses it, and what that attack is worth is the row's own
        // second dice and its own kind of harm rather than another swing of the first.
        CreatureDecision second = Decide(fixture, ai, fight, "double");
        Assert.Equal(MightAndMagic7Combat.AbilityAttack2, second.Ability);
        AttackPlan planned = combat.PlanOfAbility(fixture.Subject(fight, "double"), fight.Combatants[0].Subject, second.Kind, second.Ability);
        Assert.Equal((3 * 6) + 4, planned.Damage.Maximum);
        Assert.Equal("Fire", planned.Kind.Value);
        AttackPlan first = combat.PlanOfAbility(fixture.Subject(fight, "double"), fight.Combatants[0].Subject, AttackKind.Melee, MightAndMagic7Combat.AbilityAttack1);
        Assert.Equal((2 * 4) + 1, first.Damage.Maximum);

        // A creature that states neither casts nothing and swings: it is the first attack, which is what a
        // monster with one attack has.
        CreatureDecision plain = Decide(fixture, ai, fight, "brawler");
        Assert.Equal(MightAndMagic7Combat.AbilityAttack1, plain.Ability);
        Assert.Equal(AttackKind.Melee, plain.Kind);

        // With no random service a creature takes no chances at all: the same row that casts every time casts
        // nothing, which is what a product that cannot draw does.
        Fixture norandom = Fixture.Of(random: null, engineRoll: 0);
        CombatState still = norandom.Fight();
        Assert.Equal(MightAndMagic7Combat.AbilityAttack1, Decide(norandom, norandom.Ai, still, "caster").Ability);
    }

    [Fact]
    public void A_creatures_speed_and_reach_are_the_rows_own_columns()
    {
        Fixture fixture = Fixture.Of();
        CombatState fight = fixture.Fight();

        // The speed column is the pace the creature moves at, so a slow kind and a fast one differ here and
        // nowhere else; a creature whose row states none leaves the engine's profile in place.
        Assert.Equal(400, fixture.Ai.SpeedOf(fixture.Subject(fight, "beast")));
        Assert.Equal(120, fixture.Ai.SpeedOf(fixture.Subject(fight, "wimp")));

        // A swing is at arm's length and a throw reaches as far as a target can be picked, which is what
        // makes a creature close first and throw once it is close enough.
        Assert.Equal(407.2, fixture.Combat.ReachOf(fixture.Subject(fight, "beast"), AttackKind.Melee));
        Assert.Equal(5120, fixture.Combat.ReachOf(fixture.Subject(fight, "beast"), AttackKind.Ranged));
    }

    [Fact]
    public void A_person_reads_their_own_rows_hit_points_when_their_record_names_one()
    {
        Fixture fixture = Fixture.Of(persons: true);
        CombatState fight = fixture.Fight();

        // A person a map's own actor record places is the monster row that record names, which is how the
        // shipped levels give a guard, an adept, and a peasant their own hit points rather than one row for
        // everybody. The person whose placement names no row reads the peasant row this game falls back to —
        // the shipped one, at three hit points.
        Assert.Equal(10, fight.Vitals(fixture.Combatant(fight, "guard")).Maximum);
        Assert.Equal(3, fight.Vitals(fixture.Combatant(fight, "bystander")).Maximum);
    }

    [Fact]
    public void An_encounter_is_fought_to_a_kill_and_the_panel_shows_it()
    {
        Fixture fixture = Fixture.Of(encounter: true);
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(fixture.Files);
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(ProductTestContext.RulesetContext(context, ui, combat: true));
        session.Start();

        // One update in, the creature has noticed the party on its own and is driving itself: the panel shows
        // an enemy whose last action was its own decision, made by the product rather than by this test.
        session.Update(ProductTestContext.Update(1, 1));
        ProjectedNode combat = ProjectedNode.Of(ui.Latest().Value).Field("combat");
        Assert.True(combat.Field("engaged").AsBoolean());
        Assert.False(combat.Field("byParty").AsBoolean());
        Assert.Contains(
            combat.Field("enemies").Item(0).Field("activity").AsString(),
            new[] { "closing", "attacking", "waiting" });
        Assert.Equal(100d, combat.Field("enemies").Item(0).Field("distance").AsNumber());

        // The party attacks: the act control is pressed through the product's own intent, and both sides are
        // paced by the same gate and the same clock.
        for (ulong step = 2; step <= 200 && !combat.Field("enemies").Item(0).Field("down").AsBoolean(); step++)
        {
            session.Update(ProductTestContext.Update(step, 1, ProductTestContext.Digital(ProductIdentity.AttackIntent)));
            combat = ProjectedNode.Of(ui.Latest().Value).Field("combat");
        }

        // The creature is down: the panel says so, and nothing hostile is left. What took it there was one
        // fight in which the creature closed and struck and the party struck back, with no scene, no second
        // population, and no battle mode.
        Assert.True(combat.Field("enemies").Item(0).Field("down").AsBoolean(), $"the creature was never brought down: {combat.Field("message").AsString()}");
        Assert.Equal(0, combat.Field("enemies").Item(0).Field("hitPoints").AsNumber());
        Assert.Equal(0, combat.Field("opposition").AsNumber());
        Assert.False(combat.Field("engaged").AsBoolean());
        Assert.True(combat.Field("byParty").AsBoolean());

        // The place the party emptied is marked cleared, which is the place state the clock later restores:
        // the fight does not hold that fact, the world does.
        MightAndMagic7Session played = (MightAndMagic7Session)session;
        Assert.True(played.World!.Places.StateOf(played.World.Place).Cleared);
    }

    /// <summary>One creature's decision, with its own health brought down to a stated value first.</summary>
    private static string Action(MightAndMagic7MonsterAi ai, CombatState fight, string placement, int leave = 10)
    {
        Combatant self = fight.Combatants.First(combatant => combatant.Subject.Placement?.Content.Id == placement);
        if (CreatureHealth.Find(self.Subject.Entity!.Actor) is { } health) health.Wound(health.Current - leave);
        return ai.Decide(Situation(fight, self)).Action switch
        {
            CreatureAction.Attack => "attacking",
            CreatureAction.Advance => "closing",
            CreatureAction.Retreat => "backing away",
            _ => "waiting",
        };
    }

    /// <summary>One creature's decision, unwounded.</summary>
    private static CreatureDecision Decide(Fixture fixture, MightAndMagic7MonsterAi ai, CombatState fight, string placement) =>
        ai.Decide(Situation(fight, fight.Combatants.First(combatant => combatant.Subject.Placement?.Content.Id == placement)));

    /// <summary>
    /// What one creature is told: every other actor in the fight, with this policy's own answer about each
    /// and the distance between them.
    /// </summary>
    private static CreatureSituation Situation(CombatState fight, Combatant self)
    {
        _ = fight;
        List<CreatureCandidate> candidates = [];
        foreach (Combatant other in _fight!.Combatants)
        {
            if (ReferenceEquals(other, self)) continue;
            bool party = other.Side == CombatSide.Party;
            candidates.Add(new CreatureCandidate(
                other,
                party || _ai!.AreEnemies(self.Subject, other.Subject),
                party,
                Distance(self.Subject.Pose, other.Subject.Pose)));
        }

        (int current, int maximum) = _fight.Vitals(self);
        return new CreatureSituation(self, current, maximum, candidates, Round: 0, PartyPose: _fight.PartyPose);
    }

    /// <summary>The fight and policy the last fixture composed, which the situation helper builds from.</summary>
    private static CombatState? _fight;

    private static MightAndMagic7MonsterAi? _ai;

    private static double Distance(PlacePose from, PlacePose to)
    {
        double x = to.X - from.X;
        double y = to.Y - from.Y;
        double z = to.Z - from.Z;
        return Math.Sqrt((x * x) + (y * y) + (z * z));
    }

    /// <summary>One content set, its world, and the fight and policy composed over it.</summary>
    private sealed class Fixture
    {
        private Fixture(ContentCatalog catalog, (string Path, string Text)[] files, MightAndMagic7MonsterAi ai, MightAndMagic7Combat combat)
        {
            Catalog = catalog;
            Files = files;
            Ai = ai;
            Combat = combat;
        }

        internal ContentCatalog Catalog { get; }

        internal (string Path, string Text)[] Files { get; }

        internal MightAndMagic7MonsterAi Ai { get; }

        internal MightAndMagic7Combat Combat { get; }

        /// <summary>Composes one content set and reads this game's policies over it.</summary>
        internal static Fixture Of(
            IRandomService? random = null,
            bool hostility = true,
            bool persons = false,
            bool encounter = false,
            long? engineRoll = null)
        {
            (string Path, string Text)[] files = Files_(hostility, persons, encounter);
            (ProductCreateContext context, _) = ProductTestContext.Create(files);
            ContentCatalog catalog = ContentCatalogLoader.Load(
                new ProductContentSource(context.Content),
                ContentLayout.Under(ProductTestContext.ContentDirectory)).RequireValid();
            MightAndMagic7Combat combat = MightAndMagic7Combat.Compose(catalog, random);
            return new Fixture(catalog, files, MightAndMagic7MonsterAi.Compose(catalog, combat, random), combat)
            {
                EngineRoll = engineRoll,
            };
        }

        /// <summary>The fight a session over this content plays.</summary>
        internal CombatState Fight()
        {
            (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(Files);

            // What the session's own engine answers, which is what paces a creature's first action: a test
            // that wants a creature ready when it looks at it rolls the bottom of every range.
            if (EngineRoll is { } roll) ((FakeEngineContext)context.Engine).RandomService.Roll = roll;
            // The session is disposed with the fixture's own reader: what the fight is read for is the actors
            // and their placements, which the session's world holds while it lives.
            _session = MightAndMagic7Ruleset.Instance.CreateSession(ProductTestContext.RulesetContext(context, ui, combat: true));
            _session.Start();
            _session.Update(ProductTestContext.Update(1, 1));
            _fight = ((MightAndMagic7Session)_session).Combat!;
            _ai = Ai;
            return _fight;
        }

        private IGameSession? _session;

        /// <summary>What the session's own random service answers, when a test states it.</summary>
        private long? EngineRoll { get; init; }

        internal Combatant Combatant(CombatState fight, string placement) =>
            fight.Combatants.First(combatant => combatant.Subject.Placement?.Content.Id == placement);

        internal CombatSubject Subject(CombatState fight, string placement) => Combatant(fight, placement).Subject;
    }

    private static (string Path, string Text)[] Files_(bool hostility, bool persons, bool encounter)
    {
        List<(string Path, string Text)> files =
        [
            ProductTestContext.Bundle("partyrpg-default", "world"),
            ($"{ProductTestContext.ContentDirectory}/content-packs/world/pack.json",
                $$"""
                {
                  "schemaVersion": 1,
                  "packId": "world",
                  "kind": "definitions",
                  "provenance": { "description": "authored for a test" },
                  "documents": [
                    { "path": "places.json", "documentId": "places", "definitionKind": "place" },
                    { "path": "start.json", "documentId": "start", "definitionKind": "scenario-start" },
                    { "path": "party.json", "documentId": "party", "definitionKind": "scenario-party" },
                    { "path": "monsters.json", "documentId": "monsters", "definitionKind": "monster" },
                    { "path": "people.json", "documentId": "people", "definitionKind": "person" },
                    { "path": "spells.json", "documentId": "spells", "definitionKind": "spell" }
                    {{(hostility ? ", { \"path\": \"hostility.json\", \"documentId\": \"hostility\", \"definitionKind\": \"hostility\" }" : string.Empty)}}
                  ]
                }
                """),
            ($"{ProductTestContext.ContentDirectory}/content-packs/world/places.json", Places(persons, encounter)),
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
                    { "id": "party", "coins": 200, "food": 6, "reputation": 0, "fame": 0,
                      "members": [
                        { "name": "Roderick", "race": "Human", "class": "Knight", "level": 1,
                          "hitPoints": 40, "spellPoints": 0, "attributes": [
                            { "id": "Might", "value": 17 }, { "id": "Accuracy", "value": 15 },
                            { "id": "Endurance", "value": 15 }, { "id": "Luck", "value": 11 },
                            { "id": "Speed", "value": 17 }, { "id": "Personality", "value": 11 },
                            { "id": "Intellect", "value": 11 } ],
                          "skills": [], "spells": [], "conditions": [] } ] }
                  ]
                }
                """),
            ($"{ProductTestContext.ContentDirectory}/content-packs/world/monsters.json", Monsters(encounter)),
            ($"{ProductTestContext.ContentDirectory}/content-packs/world/people.json",
                """
                {
                  "documentId": "people",
                  "definitionKind": "person",
                  "entries": [ { "id": "person-1", "name": "A bystander", "topics": [] } ]
                }
                """),
            ($"{ProductTestContext.ContentDirectory}/content-packs/world/spells.json",
                """
                {
                  "documentId": "spells",
                  "definitionKind": "spell",
                  "entries": [ { "id": "1", "school": "Fire", "level": 1, "name": "Fire Bolt", "resist": "Fire" } ]
                }
                """),
        ];

        if (hostility)
        {
            files.Add(($"{ProductTestContext.ContentDirectory}/content-packs/world/hostility.json",
                """
                {
                  "documentId": "hostility",
                  "definitionKind": "hostility",
                  "entries": [
                    { "id": "kinds", "columns": [ "Party", "Kind 1", "Kind 2", "Beast", "Rival", "Neighbour" ] },
                    { "id": "Beast", "kind": 3, "hostility": { "0": 4, "4": 4 } },
                    { "id": "Rival", "kind": 4, "hostility": { "0": 4, "3": 3 } },
                    { "id": "Neighbour", "kind": 5, "hostility": { "0": 4 } }
                  ]
                }
                """));
        }

        return [.. files];
    }

    /// <summary>
    /// The place the encounter happens in: one creature per row this suite states, plus a person whose own
    /// record names a row and one whose record names none.
    /// </summary>
    private static string Places(bool persons, bool encounter)
    {
        if (encounter)
        {
            return $$"""
            {
              "documentId": "places",
              "definitionKind": "place",
              "entries": [
                { "id": "1", "kind": "region", "name": "A field", "respawnDays": 7, "terrain": "grass", "encounterPercent": 0,
                  "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
                  "placements": [ { "id": "beast", "kind": "monster", "monster": "7", "x": 100, "y": 0, "z": 0 } ] }
              ]
            }
            """;
        }

        string people = persons
            ? """, { "id": "guard", "kind": "person", "x": 300, "y": 0, "z": 0, "monster": "13", "people": [ "person-1" ] }"""
              + """, { "id": "bystander", "kind": "person", "x": 400, "y": 0, "z": 0, "people": [ "person-1" ] }"""
            : string.Empty;
        return $$"""
        {
          "documentId": "places",
          "definitionKind": "place",
          "entries": [
            { "id": "1", "kind": "region", "name": "A field", "respawnDays": 7, "terrain": "grass", "encounterPercent": 0,
              "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
              "placements": [
                { "id": "beast", "kind": "monster", "monster": "7", "x": 100, "y": 0, "z": 0 },
                { "id": "rival", "kind": "monster", "monster": "10", "x": 150, "y": 0, "z": 0 },
                { "id": "neighbour", "kind": "monster", "monster": "13", "x": 250, "y": 0, "z": 0 },
                { "id": "wimp", "kind": "monster", "monster": "16", "x": 300, "y": 0, "z": 0 },
                { "id": "normal", "kind": "monster", "monster": "19", "x": 300, "y": 0, "z": 0 },
                { "id": "aggressive", "kind": "monster", "monster": "22", "x": 300, "y": 0, "z": 0 },
                { "id": "suicide", "kind": "monster", "monster": "25", "x": 300, "y": 0, "z": 0 },
                { "id": "statue", "kind": "monster", "monster": "34", "x": 300, "y": 0, "z": 0 },
                { "id": "brawler", "kind": "monster", "monster": "37", "x": 300, "y": 0, "z": 0 },
                { "id": "caster", "kind": "monster", "monster": "28", "x": 300, "y": 0, "z": 0 },
                { "id": "double", "kind": "monster", "monster": "31", "x": 300, "y": 0, "z": 0 }{{people}} ] }
          ]
        }
        """;
    }

    /// <summary>
    /// The monster rows this suite states, shaped the way the importer emits them: typed columns plus the
    /// whole raw row, at the donor's own column positions.
    /// </summary>
    private static string Monsters(bool encounter)
    {
        List<string> rows = encounter
            ?
            [
                // The encounter's creature is one blow from being brought down, so a test drives a whole
                // fight rather than a siege.
                Row(7, "Beast", "2D4+1", speed: 400, hitPoints: 4),
            ]
            :
            [
                Row(7, "Beast", "2D4+1", speed: 400),
                Row(10, "Rival", "2D4+1", speed: 250),
                Row(13, "Neighbour", "1D4", speed: 140),
                Row(16, "Wimp", "1D4", speed: 120, ai: "Wimp", hostility: 0),
                Row(19, "Normal", "1D4", speed: 140, ai: "Normal", hostility: 0),
                Row(22, "Aggressive", "1D4", speed: 160, ai: "Aggress", hostility: 0),
                Row(25, "Suicide", "1D4", speed: 180, ai: "Suicidal", hostility: 0),
                Row(28, "Caster", "1D4", speed: 150, hostility: 0, spell1: "Fire Bolt", spell1Chance: 100),
                Row(31, "Double", "2D4+1", speed: 170, hostility: 0, second: "3D6+4", secondChance: 100, secondKind: "Fire"),
                Row(34, "Statue", "1D4", speed: 0, ai: "Wimp", hostility: 0, movement: "stand"),

                // A creature with one attack and nothing else, so a test can read what it does with it.
                Row(37, "Brawler", "1D4", speed: 150, hostility: 0),

                // A person with no record of their own reads this row, which is the shipped peasant's own
                // three hit points.
                Row(100, "Peasant", "1D2", speed: 140, ai: "Wimp", hostility: 0, hitPoints: 3),
            ];
        return $$"""
        { "documentId": "monsters", "definitionKind": "monster", "entries": [ {{string.Join(", ", rows)}} ] }
        """;
    }

    /// <summary>One monster row, written at the table's own column positions.</summary>
    private static string Row(
        int id,
        string name,
        string damage,
        int speed,
        string ai = "Aggress",
        int hostility = 3,
        string spell1 = "0",
        int spell1Chance = 0,
        string second = "0",
        string secondKind = "Phys",
        int secondChance = 0,
        string movement = "Long",
        string attackKind = "Phys",
        int hitPoints = 10)
    {
        string[] cells = new string[39];
        for (int index = 0; index < cells.Length; index++) cells[index] = "0";
        cells[0] = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        cells[1] = name;

        // A row's kind is the group of three graded variants it stands in, which is how the matrix names it:
        // rows 7, 10, and 13 are the third of their trios and so the kinds the matrix's third, fourth, and
        // fifth columns are about.
        cells[2] = name + " A";
        cells[3] = "2";
        cells[4] = hitPoints.ToString(System.Globalization.CultureInfo.InvariantCulture);
        cells[5] = "5";
        cells[10] = movement;
        cells[11] = ai;
        cells[12] = hostility.ToString(System.Globalization.CultureInfo.InvariantCulture);
        cells[13] = speed.ToString(System.Globalization.CultureInfo.InvariantCulture);
        cells[14] = "100";
        cells[17] = attackKind;
        cells[18] = damage;
        cells[20] = secondChance.ToString(System.Globalization.CultureInfo.InvariantCulture);
        cells[21] = secondKind;
        cells[22] = second;
        cells[24] = spell1Chance.ToString(System.Globalization.CultureInfo.InvariantCulture);
        cells[25] = spell1 == "0" ? "0" : $"{spell1},N,4";
        string columns = string.Join(", ", cells.Select(cell => $"\"{cell}\""));
        return $$"""
        { "id": "{{id}}", "name": "{{name}}", "level": 2, "hitPoints": {{hitPoints}}, "armorClass": 5,
          "hostility": {{hostility}}, "recovery": 100, "speed": {{speed}}, "aiType": "{{ai}}", "movement": "{{movement}}",
          "columns": [ {{columns}} ] }
        """;
    }

    /// <summary>The engine's randomness, answered with one value so a chance is certain or impossible.</summary>
    private sealed class AlwaysRolls(long value) : IRandomService
    {
        public KeyedRngReceipt DrawKeyed(KeyedRngRequest request) =>
            new(Math.Clamp(value, request.Minimum, request.Maximum));

        public Lcg15Receipt DrawLcg15(Lcg15Request request) => throw new NotSupportedException("Keyed rolls only.");

        public Rng CreateScoped(ScopedRngCreateRequest request) => throw new NotSupportedException("Keyed rolls only.");

        public Rng ForkScoped(ScopedRngForkRequest request) => throw new NotSupportedException("Keyed rolls only.");

        public RngValue NextU64(Rng stream) => throw new NotSupportedException("Keyed rolls only.");

        public RngValue NextBoundedU32(ScopedRngBoundedRequest request) => throw new NotSupportedException("Keyed rolls only.");

        public RngValue NextBool(Rng stream) => throw new NotSupportedException("Keyed rolls only.");
    }
}
