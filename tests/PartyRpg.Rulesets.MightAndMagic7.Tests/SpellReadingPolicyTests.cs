using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Magic;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Testing;
using Rusty.Engine;
using Xunit;
using static PartyRpg.Rulesets.MightAndMagic7.Tests.SpellEffectPolicyTests;

namespace PartyRpg.Rulesets.MightAndMagic7.Tests;

/// <summary>
/// The spells whose effect is a reading the fight takes or a quantity the clock moves: a shield a missile meets, a
/// reflection a blow comes back through, a regeneration the clock's own advances give out, and the scores a day of
/// the gods and an hour of power raise.
/// </summary>
/// <remarks>
/// Every case casts a shipped spell by its own id through the product's own session, as the other spell suite does,
/// and reads the result from the fight's own answers or the member's own pool rather than from the effect path. The
/// numbers are the donor's, cited where the ruleset states them.
/// </remarks>
public sealed class SpellReadingPolicyTests
{
    [Fact]
    public void A_shield_halves_a_creatures_missile_against_the_character_it_was_cast_on()
    {
        (ProductCreateContext context, RecordingUiService ui) = RulesetTestContext.Create(Readings());
        using IGameSession session = Casting(context, ui);
        MightAndMagic7Session live = (MightAndMagic7Session)session;
        MightAndMagic7Combat policy = Fight(context, live.Party!);
        CombatSubject beast = Attacker(live, policy);
        CombatSubject caster = SubjectOf(live, policy, "Aelina");
        CombatSubject other = SubjectOf(live, policy, "Borin");
        Assert.Equal(1, policy.PlanOf(beast, caster, AttackKind.Ranged).Divisor);

        // The donor halves a monster projectile against a shielded character (OpenEnroth
        // src/Engine/Objects/Character.cpp:5987-6009); this game's table aims the spell at its caster.
        Cast(session, ui, 1, "17", string.Empty);
        Assert.Equal("cast", Magic(ui).Field("outcome").AsString());
        Assert.Equal(2, policy.PlanOf(beast, caster, AttackKind.Ranged).Divisor);

        // A blow in hand is not a missile, and the member the spell did not land on is not shielded.
        Assert.Equal(1, policy.PlanOf(beast, caster, AttackKind.Melee).Divisor);
        Assert.Equal(1, policy.PlanOf(beast, other, AttackKind.Ranged).Divisor);
    }

    [Fact]
    public void Pain_reflection_turns_a_creatures_blow_back_onto_it_through_the_fight()
    {
        (ProductCreateContext context, RecordingUiService ui) = RulesetTestContext.Create(Readings());
        using IGameSession session = Casting(context, ui);
        MightAndMagic7Session live = (MightAndMagic7Session)session;
        Cast(session, ui, 1, "95", string.Empty);
        Assert.Equal("cast", Magic(ui).Field("outcome").AsString());

        // A fight over the session's own world and party, with rolls to draw: the creature swings at the caster.
        TestRandomService random = new();
        MightAndMagic7Combat policy = Fight(context, live.Party!, random);
        CombatState fight = new(Capabilities.Combat(policy), live.Party!, live.World);
        fight.Step();
        Combatant beast = fight.Combatants.First(combatant => !combatant.Subject.IsMember);
        Combatant caster = fight.Combatants.First(combatant => combatant.Subject.Member?.Profile.Name == "Aelina");
        fight.Observe(new ClockAdvance(default, default, GameDuration.FromSeconds(60), PeriodCrossings.None, []));
        CreatureHealth health = CreatureHealth.Find(beast.Subject.Entity!.Actor)!;
        int before = health.Current;

        CombatResult struck = fight.Order(new AttackOrder(beast.Id, AttackKind.Melee, caster.Id));

        // What the caster took comes back onto the creature through its own resistance to the same kind of harm,
        // which this creature does not resist (OpenEnroth src/Engine/Objects/Character.cpp:5875-5900).
        CombatResolution resolved = struck.Resolution!;
        Assert.True(resolved.Damage > 0);
        Assert.Equal(resolved.Damage, resolved.Reflected);
        Assert.Equal(before - resolved.Damage, health.Current);
        Assert.Contains("turned back onto", resolved.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_regeneration_gives_health_back_for_every_five_minutes_the_clock_passes_until_its_deadline()
    {
        (ProductCreateContext context, RecordingUiService ui) = RulesetTestContext.Create(Readings());
        using IGameSession session = Casting(context, ui);
        MightAndMagic7Session live = (MightAndMagic7Session)session;
        PartyMember wounded = live.Party!.Members[1];
        wounded.Resources.TakeDamage(350);
        Assert.Equal(50, wounded.Resources.HitPoints.Current);

        // An expert regeneration is worth five hit points every five minutes and lasts an hour a level of the
        // school (OpenEnroth src/Engine/Spells/CastSpellInfo.cpp:744-766, src/Engine/Engine.cpp:1236, 1398-1401):
        // three levels of body are three hours.
        Cast(session, ui, 1, "71", Target(wounded));
        Assert.Equal("cast", Magic(ui).Field("outcome").AsString());

        // An hour is twelve five-minute boundaries.
        Advance(session, 1);
        Assert.Equal(50 + (12 * 5), wounded.Resources.HitPoints.Current);

        // Five more hours pass, and only the two the regeneration still ran for give anything back.
        Advance(session, 5);
        Assert.Equal(50 + (36 * 5), wounded.Resources.HitPoints.Current);
        Assert.Equal(0, Magic(ui).Field("memberRunning").Length());
    }

    [Fact]
    public void A_day_of_the_gods_raises_every_score_the_fight_reads_for_every_member()
    {
        (ProductCreateContext context, RecordingUiService ui) = RulesetTestContext.Create(Readings());
        using IGameSession session = Casting(context, ui);
        MightAndMagic7Session live = (MightAndMagic7Session)session;
        MightAndMagic7Combat policy = Fight(context, live.Party!);
        PartyMember knight = live.Party!.Members[1];
        CombatSubject beast = Attacker(live, policy);
        CombatSubject borin = SubjectOf(live, policy, "Borin");
        DamageRoll bare = policy.PlanOf(borin, beast, AttackKind.Melee).Damage;
        Assert.Equal(13, policy.ActualAttribute(knight, MightAndMagic7Combat.MightAttribute));

        // Master light at two levels: four a level plus ten, to all seven scores, for four hours a level
        // (OpenEnroth src/Engine/Spells/CastSpellInfo.cpp:2450-2475, read at Character.cpp:2360-2387).
        Cast(session, ui, 1, "83", string.Empty);
        Assert.Equal("cast", Magic(ui).Field("outcome").AsString());
        Assert.Equal(18d, Running(Magic(ui), "spell.day-of-the-gods").Field("magnitude").AsNumber());
        Assert.Equal(13 + 18, policy.ActualAttribute(knight, MightAndMagic7Combat.MightAttribute));
        Assert.Equal(9 + 18, policy.ActualAttribute(knight, MightAndMagic7Combat.LuckAttribute));

        // Might is what a blow adds, by the donor's own table: thirteen is worth nothing and thirty-one is worth five.
        DamageRoll raised = policy.PlanOf(borin, beast, AttackKind.Melee).Damage;
        Assert.Equal(
            MightAndMagic7AttributeBonus.Of(31) - MightAndMagic7AttributeBonus.Of(13),
            raised.Bonus - bare.Bonus);
    }

    [Fact]
    public void An_hour_of_power_blesses_every_character_and_withholds_its_haste_from_a_weak_party()
    {
        (ProductCreateContext context, RecordingUiService ui) = RulesetTestContext.Create(Readings());
        using IGameSession session = Casting(context, ui);
        MightAndMagic7Session live = (MightAndMagic7Session)session;
        MightAndMagic7Combat policy = Fight(context, live.Party!);
        CombatSubject beast = Attacker(live, policy);
        CombatSubject borin = SubjectOf(live, policy, "Borin");
        GameDuration slow = policy.RecoveryAfter(borin, AttackKind.Melee);

        // A weak character keeps the haste from the whole party, as the donor's own hour of power does
        // (OpenEnroth src/Engine/Spells/CastSpellInfo.cpp:2560-2590); everything else lands.
        live.Party!.Members[1].Conditions.Apply(new ActiveCondition(MightAndMagic7Conditions.Weak));
        Cast(session, ui, 1, "86", string.Empty);
        ProjectedNode magic = Magic(ui);
        Assert.Equal("cast", magic.Field("outcome").AsString());
        Assert.Contains("withheld", magic.Field("message").AsString(), StringComparison.Ordinal);
        Assert.Equal(7d, MemberRunning(magic, live.Party.Members[0].Id.ToString(), "spell.bless").Field("magnitude").AsNumber());
        Assert.Equal(7d, MemberRunning(magic, live.Party.Members[1].Id.ToString(), "spell.bless").Field("magnitude").AsNumber());
        Assert.Equal(7d, Running(magic, "spell.heroism").Field("magnitude").AsNumber());
        Assert.Equal(7d, Running(magic, "spell.armour").Field("magnitude").AsNumber());
        Assert.Equal(2, policy.PlanOf(beast, borin, AttackKind.Ranged).Divisor);
        Assert.Equal(slow.Milliseconds, policy.RecoveryAfter(borin, AttackKind.Melee).Milliseconds);

        // Once nobody is weak, the haste lands with the rest and takes the donor's twenty-five ticks off an action.
        live.Party.Members[1].Conditions.Clear(MightAndMagic7Conditions.Weak);
        Cast(session, ui, 2, "86", string.Empty);
        Assert.Equal(25d, Running(Magic(ui), "spell.haste").Field("magnitude").AsNumber());
        Assert.True(policy.RecoveryAfter(borin, AttackKind.Melee).Milliseconds < slow.Milliseconds);
    }

    [Fact]
    public void A_boost_and_a_shield_drunk_from_a_bottle_are_read_where_the_fight_reads_them()
    {
        (ProductCreateContext context, RecordingUiService ui) = RulesetTestContext.Create(Readings());
        using IGameSession session = Casting(context, ui);
        MightAndMagic7Session live = (MightAndMagic7Session)session;
        MightAndMagic7Combat policy = Fight(context, live.Party!);
        PartyMember drinker = live.Party!.Members[0];
        CombatSubject beast = Attacker(live, policy);
        CombatSubject aelina = SubjectOf(live, policy, "Aelina");

        // Three times the potion's strength to the score, for thirty minutes a point (OpenEnroth
        // src/Engine/Objects/Character.cpp:3174-3212); a bottle the scenario packed is of the first strength.
        Drink(session, 1, 240);
        Assert.Equal("cast", Magic(ui).Field("outcome").AsString());
        Assert.Equal(9 + 3, policy.ActualAttribute(drinker, MightAndMagic7Combat.MightAttribute));
        Assert.Equal(13, policy.ActualAttribute(live.Party.Members[1], MightAndMagic7Combat.MightAttribute));

        // The shield potion is the donor's character shield, the same one the spell leaves (Character.cpp:3144-3149).
        Drink(session, 2, 232);
        Assert.Equal("cast", Magic(ui).Field("outcome").AsString());
        Assert.Equal(2, policy.PlanOf(beast, aelina, AttackKind.Ranged).Divisor);
    }

    /// <summary>Drinks the first bottle of one potion the party carries, as the panel's own control does.</summary>
    private static void Drink(IGameSession session, ulong step, int potion)
    {
        MightAndMagic7Session live = (MightAndMagic7Session)session;
        string definition = potion.ToString(System.Globalization.CultureInfo.InvariantCulture);
        ItemInstance bottle = live.Party!.Items.First(item => item.Definition.Value == definition);
        string spell = MightAndMagic7Potions.EffectId(potion).Value;
        session.Update(RulesetTestContext.Update(
            step,
            1,
            RulesetTestContext.Payload(
                $$"""{"action":"party.cast","member":0,"spell":"{{spell}}","target":"","item":{{bottle.Id}}}""")));
    }

    /// <summary>The spells this suite casts, by their shipped ids.</summary>
    private static (string Path, string Text)[] Readings() => Content(
        spells: ($"{RulesetTestContext.ContentDirectory}/content-packs/world/spells.json",
            """
            {
              "documentId": "spells",
              "definitionKind": "spell",
              "entries": [
                { "id": "17", "school": "Air", "level": 6, "name": "Shield", "resist": "0" },
                { "id": "71", "school": "Body", "level": 5, "name": "Regeneration", "resist": "0" },
                { "id": "83", "school": "Light", "level": 6, "name": "Day of the Gods", "resist": "0" },
                { "id": "86", "school": "Light", "level": 9, "name": "Hour of Power", "resist": "0" },
                { "id": "95", "school": "Dark", "level": 7, "name": "Pain Reflection", "resist": "0" }
              ]
            }
            """),
        party: ($"{RulesetTestContext.ContentDirectory}/content-packs/world/party.json",
            """
            {
              "documentId": "party",
              "definitionKind": "scenario-party",
              "entries": [
                {
                  "id": "party", "coins": 5000, "food": 30, "reputation": 0, "fame": 0,
                  "pack": [ { "item": "232", "count": 1 }, { "item": "240", "count": 1 } ],
                  "members": [
                    { "name": "Aelina", "race": "Elf", "class": "Sorcerer", "level": 30, "hitPoints": 40, "spellPoints": 0,
                      "attributes": [ { "id": "Might", "value": 9 }, { "id": "Intellect", "value": 50 },
                                      { "id": "Personality", "value": 15 }, { "id": "Endurance", "value": 30 },
                                      { "id": "Accuracy", "value": 30 }, { "id": "Speed", "value": 25 },
                                      { "id": "Luck", "value": 13 } ],
                      "skills": [ { "id": "Air", "level": 3, "tier": 2, "pointsSpent": 1 },
                                  { "id": "Body", "level": 3, "tier": 2, "pointsSpent": 1 },
                                  { "id": "Light", "level": 2, "tier": 3, "pointsSpent": 1 },
                                  { "id": "Dark", "level": 4, "tier": 2, "pointsSpent": 1 } ],
                      "spells": [ "17", "71", "83", "86", "95" ], "conditions": [] },
                    { "name": "Borin", "race": "Human", "class": "Knight", "level": 1, "hitPoints": 400, "spellPoints": 0,
                      "attributes": [ { "id": "Might", "value": 13 }, { "id": "Intellect", "value": 9 },
                                      { "id": "Personality", "value": 9 }, { "id": "Endurance", "value": 13 },
                                      { "id": "Accuracy", "value": 13 }, { "id": "Speed", "value": 9 },
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
                { "id": "7", "name": "A beast", "hostility": 1, "recovery": 100, "level": 4,
                  "hitPoints": 200, "armorClass": 0, {{MonsterRows.Combat(7, "Fire", "2d6+0")}} }
              ]
            }
            """),
        extra:
        [
            ($"{RulesetTestContext.ContentDirectory}/content-packs/world/items.json",
                """
                {
                  "documentId": "items",
                  "definitionKind": "item",
                  "entries": [
                    { "id": "232", "name": "Shield", "value": 300, "equipStat": "Bottle", "type": "potion", "skillGroup": "Misc", "skill": "misc", "damageDice": "0", "damageModifier": "1" },
                    { "id": "240", "name": "Might Boost", "value": 300, "equipStat": "Bottle", "type": "potion", "skillGroup": "Misc", "skill": "misc", "damageDice": "0", "damageModifier": "1" }
                  ]
                }
                """,
                """{ "path": "items.json", "documentId": "items", "definitionKind": "item" }"""),
            ($"{RulesetTestContext.ContentDirectory}/content-packs/world/potions.json",
                """
                {
                  "documentId": "potions",
                  "definitionKind": "potion",
                  "entries": [
                    { "id": "232", "name": "Shield", "description": "Purple Potion", "effect": "Cast Shield", "kind": "potion", "units": [1, 0, 1], "tier": 2, "mixtures": { "232": "none" } },
                    { "id": "240", "name": "Might Boost", "description": "Yellow Potion", "effect": "+3 Might", "kind": "potion", "units": [0, 0, 1], "tier": 2, "mixtures": { "240": "none" } }
                  ]
                }
                """,
                """{ "path": "potions.json", "documentId": "potions", "definitionKind": "potion" }"""),
        ]);
}
