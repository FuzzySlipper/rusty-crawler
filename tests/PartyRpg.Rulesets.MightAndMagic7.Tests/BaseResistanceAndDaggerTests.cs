using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using PartyRpg.Testing;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Rulesets.MightAndMagic7.Tests;

/// <summary>
/// The two terms of a character's sums that come from who they are rather than what they wear: their race's
/// and a Lich's resistance, and a master dagger's chance to triple what it rolled.
/// </summary>
/// <remarks>
/// The numbers are the donor's, written out here independently of the code that computes them: the racial
/// bonuses and a Lich's floor and ceiling (OpenEnroth <c>src/Engine/Objects/Character.cpp:1900-1993</c>,
/// <c>:4025-4042</c>), and the dagger's tripled weapon roll at a chance of its level in a hundred (<c>:899-905</c>).
/// </remarks>
public sealed class BaseResistanceAndDaggerTests
{
    [Fact]
    [Trait(Pins.Trait, Pins.Tuning)]
    public void A_race_changes_what_a_member_resists_and_a_lich_resists_to_its_ceiling()
    {
        using Fight fight = Fight.Begin();

        // The creature's blow is mind. An elf resists ten of it, a human nothing, and the elf Lich two hundred:
        // the promotion's two hundred and the elf's ten, held to the Lich's ceiling.
        Assert.Equal(0, fight.Rule.PlanOf(fight.Beast, fight.Member("Roderick"), AttackKind.Melee).Resistance.Points);
        Assert.Equal(10, fight.Rule.PlanOf(fight.Beast, fight.Member("Aria"), AttackKind.Melee).Resistance.Points);
        Assert.Equal(200, fight.Rule.PlanOf(fight.Beast, fight.Member("Vex"), AttackKind.Melee).Resistance.Points);

        // The resistance the fight reads is the one the check halves by: four failed checks of thirty in forty
        // take a sixteenth of a hundred for the elf, and the human, resisting nothing and with no luck to add,
        // is never checked at all (Actor.cpp:3743-3758, Character.cpp:1097-1108).
        Assert.Equal(6, fight.Rule.DamageAfterResistance(fight.Member("Aria"), MightAndMagic7Damage.Mind, 100, new Highest()));
        Assert.Equal(100, fight.Rule.DamageAfterResistance(fight.Member("Roderick"), MightAndMagic7Damage.Mind, 100, new Highest()));
    }

    [Fact]
    public void Every_race_creation_offers_has_its_racial_resistances_stated()
    {
        Assert.Equal(
            MightAndMagic7CreationTables.Races.Select(race => race.Id.Value).Order(StringComparer.Ordinal),
            MightAndMagic7BaseResistance.Races.Order(StringComparer.Ordinal));
        Assert.Equal(5, MightAndMagic7BaseResistance.RacialBonus(new RaceId("Goblin"), MightAndMagic7Damage.Fire));
        Assert.Equal(5, MightAndMagic7BaseResistance.RacialBonus(new RaceId("Dwarf"), MightAndMagic7Damage.Earth));
        Assert.Equal(5, MightAndMagic7BaseResistance.RacialBonus(new RaceId("human"), MightAndMagic7Damage.Spirit));
        Assert.Equal(0, MightAndMagic7BaseResistance.RacialBonus(new RaceId("Human"), MightAndMagic7Damage.Fire));
    }

    [Fact]
    [Trait(Pins.Trait, Pins.Tuning)]
    public void A_master_dagger_triples_what_it_rolled_at_a_chance_of_its_level_in_a_hundred()
    {
        using Fight fight = Fight.Begin();

        // A master of the dagger at level twelve, holding a dagger of two two-sided dice: the dagger's own dice and
        // modifier are the tripled part, at twelve in a hundred; might's two is added once whatever the draw.
        DamageRoll blow = fight.Rule.PlanOf(fight.Member("Roderick"), fight.Beast, AttackKind.Melee).Damage;
        Assert.Equal(
            new DamageRoll(2, 2, 2, floor: 1).WithMultiplier(new DamageMultiplier(0, 2, 0, HitChance.Of(12, 100), 3)),
            blow);

        // Both dice at two: four and might's two is six; tripled, the dice count twice more, fourteen.
        Assert.Equal(6, blow.Roll(new Drawing(chance: 1200), "damage"));
        Assert.Equal(14, blow.Roll(new Drawing(chance: 1199), "damage"));
        Assert.Equal(14, blow.Maximum);

        // A novice of the dagger holding the same dagger has no such chance.
        Assert.Empty(fight.Rule.PlanOf(fight.Member("Aria"), fight.Beast, AttackKind.Melee).Damage.Multipliers);
    }

    /// <summary>A fight beside one creature whose blow is mind, with a party of three members of different races.</summary>
    private sealed class Fight : IDisposable
    {
        private readonly IGameSession _session;
        private readonly CombatState _state;

        private Fight(IGameSession session, MightAndMagic7Combat rule, CombatState state)
        {
            _session = session;
            Rule = rule;
            _state = state;
        }

        public MightAndMagic7Combat Rule { get; }

        public CombatSubject Beast => _state.Combatants.First(combatant => !combatant.Subject.IsMember).Subject;

        public CombatSubject Member(string name) =>
            _state.Combatants.First(combatant => combatant.Subject.Member?.Profile.Name == name).Subject;

        public static Fight Begin()
        {
            (ProductCreateContext context, RecordingUiService ui) = RulesetTestContext.Create(Content());
            IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(RulesetTestContext.RulesetContext(context, ui, combat: true));
            session.Start();
            session.Update(RulesetTestContext.Update(1, 1));
            MightAndMagic7Session live = (MightAndMagic7Session)session;
            ContentCatalog catalog = ContentCatalogLoader
                .Load(RulesetTestContext.Content(context), ContentLayout.Under(RulesetTestContext.ContentDirectory))
                .RequireValid();
            MightAndMagic7Combat rule = MightAndMagic7Combat.Compose(catalog, random: null, party: () => live.Party);
            CombatState state = new(Capabilities.Combat(rule), live.Party!, live.World);
            state.Step();
            return new Fight(session, rule, state);
        }

        public void Dispose() => _session.Dispose();
    }

    /// <summary>Every die at its highest, and one stated value for the multiplier's draw.</summary>
    private sealed class Drawing(int chance) : IAttackRolls
    {
        public int Roll(string purpose, int minimum, int maximum) =>
            purpose.Contains("/multiplier/", StringComparison.Ordinal) ? chance : maximum;
    }

    /// <summary>Every draw at its highest, so every resistance check fails to stop the halving.</summary>
    private sealed class Highest : IAttackRolls
    {
        public int Roll(string purpose, int minimum, int maximum) => maximum;
    }

    private static string Member(string name, string race, string characterClass, string skills, string equipment) =>
        $$"""
        { "name": "{{name}}", "race": "{{race}}", "class": "{{characterClass}}", "level": 1,
          "hitPoints": 40, "spellPoints": 0, "attributes": [
            { "id": "Might", "value": 17 }, { "id": "Accuracy", "value": 15 },
            { "id": "Endurance", "value": 15 }, { "id": "Luck", "value": 11 },
            { "id": "Speed", "value": 17 }, { "id": "Personality", "value": 11 },
            { "id": "Intellect", "value": 11 } ],
          "skills": [ {{skills}} ], "equipment": [ {{equipment}} ], "spells": [], "conditions": [] }
        """;

    private static (string Path, string Text)[] Content() =>
    [
        RulesetTestContext.Bundle("partyrpg-default", "world"),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/pack.json",
            TestPacks.Manifest(
                "world",
                ("places", "place"),
                ("monsters", "monster"),
                ("skills", "skill"),
                ("items", "item"),
                ("start", "scenario-start"),
                ("party", "scenario-party"))),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/places.json",
            """
            {
              "documentId": "places",
              "definitionKind": "place",
              "entries": [
                { "id": "1", "kind": "region", "name": "Home", "respawnDays": 1,
                  "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 512 } ],
                  "placements": [ { "id": "beast", "kind": "monster", "monster": "7", "x": 100, "y": 0, "z": 0 } ] }
              ]
            }
            """),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/monsters.json",
            $$"""
            {
              "documentId": "monsters",
              "definitionKind": "monster",
              "entries": [
                { "id": "7", "name": "A mind flayer", "level": 2, "hitPoints": 400, "armorClass": 5, "hostility": 2,
                  "recovery": 100, {{MonsterRows.Combat(7, "Mind", "2D8+10", "0", "0")}} }
              ]
            }
            """),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/skills.json",
            """
            { "documentId": "skills", "definitionKind": "skill", "entries": [ { "id": "Dagger" } ] }
            """),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/items.json",
            """
            {
              "documentId": "items",
              "definitionKind": "item",
              "entries": [
                { "id": "15", "name": "A dagger", "value": 8, "type": "single-handed", "skill": "dagger", "damageDice": "2d2", "damageModifier": "0" }
              ]
            }
            """),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/start.json",
            """
            { "documentId": "start", "definitionKind": "scenario-start", "entries": [ { "id": "start", "place": "1", "entryPoint": "Party Start" } ] }
            """),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/party.json",
            $$"""
            {
              "documentId": "party",
              "definitionKind": "scenario-party",
              "entries": [
                {
                  "id": "party", "coins": 200, "food": 6, "reputation": 0, "fame": 0,
                  "members": [
                    {{Member("Roderick", "Human", "Knight", """{ "id": "Dagger", "level": 12, "tier": 3, "pointsSpent": 12 }""", """{ "slot": "main hand", "item": "15" }""")}},
                    {{Member("Aria", "Elf", "Sorcerer", """{ "id": "Dagger", "level": 1, "tier": 1, "pointsSpent": 1 }""", """{ "slot": "main hand", "item": "15" }""")}},
                    {{Member("Vex", "Elf", "Lich", string.Empty, string.Empty)}}
                  ]
                }
              ]
            }
            """),
    ];
}
