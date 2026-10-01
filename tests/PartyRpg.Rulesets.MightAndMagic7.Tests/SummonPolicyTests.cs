using System.Globalization;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.World;
using PartyRpg.Testing;
using Rusty.Engine;
using Xunit;
using static PartyRpg.Rulesets.MightAndMagic7.Tests.SpellEffectPolicyTests;

namespace PartyRpg.Rulesets.MightAndMagic7.Tests;

/// <summary>
/// The spells that create a creature: an elemental called up to stand with the party, and a body stood back up to
/// fight for it — both created by the world's own population, both on the fight's ally side, and neither carried by
/// a save.
/// </summary>
/// <remarks>
/// Every case casts the shipped spell by its own id through the product's own session and reads the result from the
/// population, the fight, and the save boundary. The numbers are the donor's, cited where the ruleset states them.
/// </remarks>
public sealed class SummonPolicyTests
{
    [Fact]
    public void An_elemental_stands_with_the_party_for_the_spells_own_length_and_a_save_is_refused_while_it_does()
    {
        InMemoryPersistenceService persistence = new();
        (ProductCreateContext context, RecordingUiService ui) = RulesetTestContext.Create(persistence, World(lightTier: 2, beastAt: 9000));
        using IGameSession session = Casting(context, ui);
        MightAndMagic7Session live = (MightAndMagic7Session)session;
        session.Update(RulesetTestContext.Update(1, 1));
        MightAndMagic7Combat policy = Fight(context, live.Party!);
        PartyMember caster = live.Party!.Members[0];

        // An expert calls up the lesser light elemental, the table's own "Elemental Light A", for five minutes a
        // level of light (OpenEnroth src/Engine/Spells/CastSpellInfo.cpp:2410-2445, src/Engine/Objects/Actor.cpp:4150-4156).
        Cast(session, ui, 2, "82", string.Empty);
        Assert.Equal("cast", Magic(ui).Field("outcome").AsString());
        PlacePopulationEntity elemental = Assert.Single(live.World!.Population.Entities, entity => entity.IsSummoned);
        Assert.Equal("43", elemental.Placement.Source.GetId(MightAndMagic7Combat.MonsterField));
        Assert.Equal(GameDurationMinutes(10), live.World.Population.RemainingOf(elemental)!.Value.Milliseconds);

        // It stands the donor's distance from the party out of doors (Actor.cpp:4165), and it is a creature of its own
        // row with that row's health.
        Assert.Equal(live.World.Party.PlacePose.X + 128, elemental.Pose.X);
        Assert.Equal(67, CreatureHealth.Find(elemental.Actor)!.Maximum);

        // The fight reads it on the party's side: it stands with the party, the party's own act never aims at it, and
        // bringing it down is worth nothing (Actor.cpp:4175, 4184-4186).
        session.Update(RulesetTestContext.Update(3, 1));
        Combatant ally = live.Combat!.Combatants.Single(combatant => combatant.Subject.Entity == elemental);
        Assert.Equal(CombatSide.Ally, ally.Side);
        Assert.True(policy.NatureOf(ally.Subject).IsAllied);
        Assert.Equal(0, policy.ExperienceOf(elemental.Placement));

        // An expert holds one at a time: a second casting is refused before anything is spent (CastSpellInfo.cpp:2431-2443).
        int points = caster.Resources.SpellPoints.Current;
        Cast(session, ui, 4, "82", string.Empty);
        ProjectedNode refused = Magic(ui);
        Assert.Equal("refused", refused.Field("outcome").AsString());
        Assert.Equal(MightAndMagic7Codes.SpellSummonLimit, refused.Field("code").AsString());
        Assert.Equal(points, caster.Resources.SpellPoints.Current);
        Assert.DoesNotContain(live.Combat.Engage(), result => result.Initiated?.Target == ally.Id);

        // A save carries no population, so a save taken while it stands is refused by name rather than written with
        // the elemental silently gone.
        SessionSaveException unsaved = Assert.Throws<SessionSaveException>(() => MightAndMagic7Ruleset.Instance.Save(session));
        Assert.Contains(unsaved.Problems, problem => problem.Code == SaveCodes.SaveFightUnsaved && problem.Subject == "summoned");

        // An hour of game time is far more than ten minutes: the elemental is gone, the fight lets go of it, the caster
        // may call another, and the save that was refused succeeds.
        Advance(session, 1);
        Assert.DoesNotContain(live.World.Population.Entities, entity => entity.IsSummoned);
        Assert.False(elemental.IsAlive);
        session.Update(RulesetTestContext.Update(300, 1));
        Assert.DoesNotContain(live.Combat.Combatants, combatant => combatant.Subject.Entity == elemental);
        MightAndMagic7Ruleset.Instance.Save(session);
        Cast(session, ui, 301, "82", string.Empty);
        Assert.Equal("cast", Magic(ui).Field("outcome").AsString());
    }

    [Fact]
    public void A_master_calls_a_stronger_elemental_and_holds_three_at_once()
    {
        (ProductCreateContext context, RecordingUiService ui) = RulesetTestContext.Create(World(lightTier: 3, beastAt: 9000));
        using IGameSession session = Casting(context, ui);
        MightAndMagic7Session live = (MightAndMagic7Session)session;
        session.Update(RulesetTestContext.Update(1, 1));

        // A master calls grade B for fifteen minutes a level, and up to three at once (CastSpellInfo.cpp:2421-2424).
        for (ulong cast = 2; cast <= 4; cast++)
        {
            Cast(session, ui, cast, "82", string.Empty);
            Assert.Equal("cast", Magic(ui).Field("outcome").AsString());
        }

        IReadOnlyList<PlacePopulationEntity> called = [.. live.World!.Population.Entities.Where(entity => entity.IsSummoned)];
        Assert.Equal(3, called.Count);
        Assert.All(called, entity => Assert.Equal("44", entity.Placement.Source.GetId(MightAndMagic7Combat.MonsterField)));
        Assert.All(called, entity => Assert.InRange(live.World.Population.RemainingOf(entity)!.Value.Milliseconds, GameDurationMinutes(29), GameDurationMinutes(30)));
        Assert.Equal(3, called.Select(entity => entity.Content).Distinct().Count());

        Cast(session, ui, 5, "82", string.Empty);
        Assert.Equal(MightAndMagic7Codes.SpellSummonLimit, Magic(ui).Field("code").AsString());

        // Leaving the place ends what was called up there: it belongs to the visit that made it.
        live.World.ArriveAt(new PlaceId("2"), PlacePose.Origin);
        session.Update(RulesetTestContext.Update(6, 1));
        Assert.All(called, entity => Assert.False(entity.IsAlive));
    }

    [Fact]
    public void A_body_rises_to_fight_for_the_party_unless_it_is_too_strong_and_a_save_is_refused_while_it_stands()
    {
        InMemoryPersistenceService persistence = new();
        (ProductCreateContext context, RecordingUiService ui) = RulesetTestContext.Create(persistence, World(lightTier: 2, beastAt: 100));
        FakeEngineContext fake = (FakeEngineContext)context.Engine;
        fake.RandomService.Answer = request => request.Key.EndsWith("/hit", StringComparison.Ordinal) ? 0 : null;
        using IGameSession session = Casting(context, ui);
        MightAndMagic7Session live = (MightAndMagic7Session)session;
        ulong step = 0;
        session.Update(RulesetTestContext.Update(++step, 1));
        MightAndMagic7Combat policy = Fight(context, live.Party!);
        PartyMember caster = live.Party!.Members[0];

        // A creature still standing is not a body: the casting is refused before anything is spent (ours; the donor
        // spends the points, CastSpellInfo.cpp:2641-2652).
        int points = caster.Resources.SpellPoints.Current;
        CastAt(session, ++step, "89", Creature(live, "beast"));
        Assert.Equal(MightAndMagic7Codes.SpellNotABody, Magic(ui).Field("code").AsString());
        Assert.Equal(points, caster.Resources.SpellPoints.Current);

        // The beast is brought down by the party's own blows, and its body lies where it fell.
        step = BringDown(session, live, "beast", step);
        Combatant fallen = Creature(live, "beast");
        Assert.True(live.Combat!.IsDown(fallen));

        // A novice of three levels of dark raises a body of up to level six, and leaves it at most sixty hit points
        // (CastSpellInfo.cpp:2613-2631, 2659-2661); the beast is level four and rises.
        CastAt(session, ++step, "89", fallen);
        Assert.Equal("cast", Magic(ui).Field("outcome").AsString());
        PlacePopulationEntity raised = Assert.Single(live.World!.Population.Entities, entity => entity.IsSummoned);
        Assert.Equal("7", raised.Placement.Source.GetId(MightAndMagic7Combat.MonsterField));
        Assert.Equal(60, CreatureHealth.Find(raised.Actor)!.Current);
        Assert.False(fallen.Subject.Entity!.IsAlive);
        Assert.DoesNotContain(live.World.Population.Entities, entity => entity.Content.Id == "beast");

        // It fights for the party, and its death would be worth nothing a second time.
        session.Update(RulesetTestContext.Update(++step, 1));
        Combatant risen = live.Combat.Combatants.Single(combatant => combatant.Subject.Entity == raised);
        Assert.Equal(CombatSide.Ally, risen.Side);
        Assert.Equal(0, policy.ExperienceOf(raised.Placement));

        // The titan is level forty, far past what this caster raises: the casting is spent and the body stays down
        // (CastSpellInfo.cpp:2657-2658).
        step = BringDown(session, live, "titan", step);
        int before = caster.Resources.SpellPoints.Current;
        CastAt(session, ++step, "89", Creature(live, "titan"));
        Assert.Equal("cast", Magic(ui).Field("outcome").AsString());
        Assert.Contains("too strong", Magic(ui).Field("message").AsString(), StringComparison.Ordinal);
        Assert.True(before > caster.Resources.SpellPoints.Current);
        Assert.True(live.Combat.IsDown(Creature(live, "titan")));

        // A save carries no population, so the risen beast is named rather than dropped.
        SessionSaveException unsaved = Assert.Throws<SessionSaveException>(() => MightAndMagic7Ruleset.Instance.Save(session));
        Assert.Contains(unsaved.Problems, problem => problem.Code == SaveCodes.SaveFightUnsaved && problem.Subject == "summoned");
    }

    [Fact]
    public void A_dead_member_rises_as_a_zombie_whose_state_holds_its_pools_down_and_survives_a_save()
    {
        InMemoryPersistenceService persistence = new();
        (ProductCreateContext context, RecordingUiService ui) = RulesetTestContext.Create(persistence, World(lightTier: 2, beastAt: 9000));
        using IGameSession session = Casting(context, ui);
        MightAndMagic7Session live = (MightAndMagic7Session)session;
        session.Update(RulesetTestContext.Update(1, 1));
        PartyMember caster = live.Party!.Members[0];
        PartyMember borin = live.Party.Members[1];

        // The spell names an actor of either side, so the panel offers the party's own members beside the creatures.
        ProjectedNode row = Items(Magic(ui).Field("members").Item(0).Field("spells")).Single(spell => spell.Field("spell").AsString() == "89");
        Assert.Equal("either", row.Field("targeting").AsString());
        Assert.Equal("any", row.Field("targetSide").AsString());

        // A living member is not a body: the casting is refused before anything is spent (ours; the donor spends the
        // points and changes nothing, OpenEnroth src/Engine/Spells/CastSpellInfo.cpp:2632-2640).
        int points = caster.Resources.SpellPoints.Current;
        Cast(session, ui, 2, "89", Target(borin));
        Assert.Equal(MightAndMagic7Codes.SpellNotABody, Magic(ui).Field("code").AsString());
        Assert.Equal(points, caster.Resources.SpellPoints.Current);

        // Borin dies, with a curse and a poison on him.
        borin.Conditions.Apply(new ActiveCondition(MightAndMagic7Conditions.Cursed, 1));
        borin.Conditions.Apply(new ActiveCondition(MightAndMagic7Conditions.PoisonWeak, 1));
        borin.Resources.TakeDamage(borin.Resources.HitPoints.Current + 5);
        borin.Conditions.Apply(new ActiveCondition(MightAndMagic7Conditions.Dead));
        session.Update(RulesetTestContext.Update(3, 1));

        // Reanimate aimed at him stands him up as a zombie: every condition ends, his health is filled and his spell
        // points emptied (Character.cpp:485-505), and he acts again (Character.cpp:350-357).
        Cast(session, ui, 4, "89", Target(borin));
        Assert.Equal("cast", Magic(ui).Field("outcome").AsString());
        Assert.True(points > caster.Resources.SpellPoints.Current);
        Assert.Equal([MightAndMagic7Conditions.Zombie], borin.Conditions.Active.Select(condition => condition.Condition));
        Assert.Equal(400, borin.Resources.HitPoints.Current);
        Assert.Equal(0, borin.Resources.SpellPoints.Current);
        Assert.True(MightAndMagic7Conditions.CanAct(borin));
        Assert.Contains(Items(Magic(ui).Field("facts")), fact => fact.Field("name").AsString() == "zombie" && fact.Field("value").AsString() == "Borin");

        // A zombie is not dead, so a second casting has nothing to raise.
        Cast(session, ui, 5, "89", Target(borin));
        Assert.Equal(MightAndMagic7Codes.SpellNotABody, Magic(ui).Field("code").AsString());

        // An hour of game time is twelve five-minute steps, and each takes one health toward half his maximum
        // (Engine.cpp:1236, :1425-1429).
        Advance(session, 1);
        Assert.Equal(388, borin.Resources.HitPoints.Current);
        Assert.Equal(0, borin.Resources.SpellPoints.Current);

        // A heal stops at half his maximum (Character.cpp:1283-1297), and a night leaves him no spell points and half
        // the health it filled (Party.cpp:737-739).
        borin.Resources.TakeDamage(288);
        Assert.Equal(100, MightAndMagic7Undeath.Heal(borin, 500));
        Assert.Equal(200, borin.Resources.HitPoints.Current);
        Assert.Equal(0, MightAndMagic7Undeath.Heal(borin, 500));
        borin.Resources.RestoreAll();
        MightAndMagic7Undeath.Rested(borin);
        Assert.Equal(200, borin.Resources.HitPoints.Current);
        Assert.Equal(0, borin.Resources.SpellPoints.Current);

        // The state is the member's own condition, so the product's save carries it and a resumed product plays him as
        // the zombie he was, drifting as before.
        borin.Resources.RestoreHitPoints(50);
        SessionSave written = MightAndMagic7Ruleset.Instance.Save(session);
        Assert.Contains(written.Party.Members[1].Seed.Conditions, condition => condition.Condition == MightAndMagic7Conditions.Zombie);
        (ProductCreateContext resumedContext, RecordingUiService resumedUi) = RulesetTestContext.Create(persistence, World(lightTier: 2, beastAt: 9000));
        using IGameSession resumed = MightAndMagic7Ruleset.Instance.ResumeSession(
            RulesetTestContext.RulesetContext(resumedContext, resumedUi) with { Cast = new CastIntentNames(Declared.UiActionContract) });
        resumed.Start();
        PartyMember again = ((MightAndMagic7Session)resumed).Party!.Members[1];
        Assert.True(MightAndMagic7Undeath.IsZombie(again));
        Assert.False(again.Conditions.Has(MightAndMagic7Conditions.Dead));
        Assert.Equal(250, again.Resources.HitPoints.Current);
        Advance(resumed, 1);
        Assert.Equal(238, again.Resources.HitPoints.Current);
    }

    /// <summary>The entries of one projected list, in order.</summary>
    private static IEnumerable<ProjectedNode> Items(ProjectedNode list) => Enumerable.Range(0, list.Length()).Select(list.Item);

    /// <summary>A length of whole game minutes, in the milliseconds a remaining length is counted in.</summary>
    private static long GameDurationMinutes(int minutes) => minutes * 60L * 1000L;

    /// <summary>Wounds a creature to its last hit point and lets the party's knight swing until it is down.</summary>
    private static ulong BringDown(IGameSession session, MightAndMagic7Session live, string name, ulong step)
    {
        Combatant target = Creature(live, name);
        CreatureHealth health = CreatureHealth.Find(target.Subject.Entity!.Actor)!;
        health.Wound(health.Current - 1);
        CombatantId knight = CombatantId.Of(live.Party!.Members[1].Id);
        for (int tries = 0; tries < 400 && !live.Combat!.IsDown(Creature(live, name)); tries++)
        {
            live.Combat.Order(new AttackOrder(knight, AttackKind.Melee, Creature(live, name).Id));
            session.Update(RulesetTestContext.Update(++step, 1));
        }

        Assert.True(live.Combat!.IsDown(Creature(live, name)), $"{name} was never brought down.");
        return step;
    }

    /// <summary>The creature the session's own fight holds under its placement's own identity.</summary>
    private static Combatant Creature(MightAndMagic7Session live, string name) =>
        live.Combat!.Combatants.First(combatant => !combatant.Subject.IsMember && combatant.Subject.Placement?.Content.Id == name);

    /// <summary>Casts one spell at one creature, as the panel's own control does.</summary>
    private static void CastAt(IGameSession session, ulong step, string spell, Combatant creature) =>
        session.Update(RulesetTestContext.Update(
            step,
            1,
            RulesetTestContext.Payload(
                string.Create(CultureInfo.InvariantCulture, $$"""{"action":"party.cast","member":0,"spell":"{{spell}}","target":"{{creature.Id}}"}"""))));

    /// <summary>
    /// This suite's world: a beast and a titan, the light elementals' rows under the table's own internal names, and a
    /// caster of light at the rung a case asks for and of dark at novice.
    /// </summary>
    private static (string Path, string Text)[] World(int lightTier, int beastAt) => Content(
        spells: ($"{RulesetTestContext.ContentDirectory}/content-packs/world/spells.json",
            """
            {
              "documentId": "spells",
              "definitionKind": "spell",
              "entries": [
                { "id": "82", "school": "Light", "level": 7, "name": "Summon Elemental", "resist": "0" },
                { "id": "89", "school": "Dark", "level": 1, "name": "Reanimate", "resist": "0" }
              ]
            }
            """),
        party: ($"{RulesetTestContext.ContentDirectory}/content-packs/world/party.json",
            $$"""
            {
              "documentId": "party",
              "definitionKind": "scenario-party",
              "entries": [
                {
                  "id": "party", "coins": 5000, "food": 30, "reputation": 0, "fame": 0,
                  "members": [
                    { "name": "Aelina", "race": "Elf", "class": "Sorcerer", "level": 30, "hitPoints": 400, "spellPoints": 0,
                      "attributes": [ { "id": "Might", "value": 9 }, { "id": "Intellect", "value": 50 },
                                      { "id": "Personality", "value": 15 }, { "id": "Endurance", "value": 30 },
                                      { "id": "Accuracy", "value": 30 }, { "id": "Speed", "value": 25 },
                                      { "id": "Luck", "value": 13 } ],
                      "skills": [ { "id": "Light", "level": 2, "tier": {{lightTier}}, "pointsSpent": 1 },
                                  { "id": "Dark", "level": 3, "tier": 1, "pointsSpent": 1 } ],
                      "spells": [ "82", "89" ], "conditions": [] },
                    { "name": "Borin", "race": "Human", "class": "Knight", "level": 10, "hitPoints": 400, "spellPoints": 0,
                      "attributes": [ { "id": "Might", "value": 30 }, { "id": "Intellect", "value": 9 },
                                      { "id": "Personality", "value": 9 }, { "id": "Endurance", "value": 13 },
                                      { "id": "Accuracy", "value": 30 }, { "id": "Speed", "value": 9 },
                                      { "id": "Luck", "value": 9 } ],
                      "skills": [], "spells": [], "conditions": [] }
                  ]
                }
              ]
            }
            """),
        monster: ($"{RulesetTestContext.ContentDirectory}/content-packs/world/monsters.json",
            $$"""
            {
              "documentId": "monsters",
              "definitionKind": "monster",
              "entries": [
                { "id": "7", "name": "A beast", "internalName": "Beast A", "hostility": 1, "recovery": 100, "level": 4, "speed": 0,
                  "hitPoints": 200, "armorClass": 0, {{MonsterRows.Combat(7, "Phys", "1d1+0")}} },
                { "id": "43", "name": "Lesser Light Elemental", "internalName": "Elemental Light A", "hostility": 3, "recovery": 100,
                  "level": 15, "speed": 0, "hitPoints": 67, "armorClass": 0, {{MonsterRows.Combat(43, "Light", "1d1+0")}} },
                { "id": "44", "name": "Light Elemental", "internalName": "Elemental Light B", "hostility": 3, "recovery": 100,
                  "level": 23, "speed": 0, "hitPoints": 121, "armorClass": 0, {{MonsterRows.Combat(44, "Light", "1d1+0")}} },
                { "id": "50", "name": "A titan", "internalName": "Titan A", "hostility": 1, "recovery": 100, "level": 40, "speed": 0,
                  "hitPoints": 200, "armorClass": 0, {{MonsterRows.Combat(50, "Phys", "1d1+0")}} }
              ]
            }
            """),
        places: ($"{RulesetTestContext.ContentDirectory}/content-packs/world/places.json",
            $$"""
            {
              "documentId": "places",
              "definitionKind": "place",
              "entries": [
                { "id": "1", "kind": "region", "name": "The Guild of Fire", "respawnDays": 1,
                  "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
                  "placements": [
                    { "id": "beast", "kind": "monster", "monster": "7", "name": "A beast", "x": {{beastAt}}, "y": 0, "z": 0 },
                    { "id": "titan", "kind": "monster", "monster": "50", "name": "A titan", "x": {{beastAt}}, "y": 50, "z": 0 }
                  ] },
                { "id": "2", "kind": "interior", "name": "Cave", "respawnDays": 1,
                  "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ] }
              ]
            }
            """));
}
