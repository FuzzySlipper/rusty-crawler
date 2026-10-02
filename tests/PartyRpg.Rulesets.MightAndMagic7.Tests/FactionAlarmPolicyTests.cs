using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Sessions;
using PartyRpg.Testing;
using Rusty.Engine;
using Xunit;
using static PartyRpg.Rulesets.MightAndMagic7.Tests.SpellEffectPolicyTests;

namespace PartyRpg.Rulesets.MightAndMagic7.Tests;

/// <summary>
/// What an act against one of a level's own actors turns against the party besides it — those of its faction standing
/// within 4,096 units — and what a person's own actor record says of them.
/// </summary>
/// <remarks>
/// The rule is the donor's (OpenEnroth <c>src/Engine/Objects/Actor.cpp:694-725</c>, <c>ArePeasantsOfSameFaction</c> and
/// <c>AggroSurroundingPeasants</c>), cited where the ruleset states it. Every kind here is friendly to the party in the
/// matrix, so a creature or person in the fight is one the party's act or a record put there.
/// </remarks>
public sealed class FactionAlarmPolicyTests
{
    [Fact]
    public void Attacking_a_guard_turns_the_guard_beside_it_and_leaves_one_five_thousand_units_off_peaceful()
    {
        (ProductCreateContext context, RecordingUiService ui) = RulesetTestContext.Create(Town());
        using IGameSession session = Casting(context, ui);
        MightAndMagic7Session live = (MightAndMagic7Session)session;
        session.Update(RulesetTestContext.Update(1, 1));
        MightAndMagic7Combat policy = Fight(context, live.Party!);

        Combatant attacked = Actor(live, "monster-actor-1");
        Combatant beside = Actor(live, "monster-actor-2");
        Combatant distant = Actor(live, "monster-actor-3");
        Combatant goblin = Actor(live, "monster-actor-4");
        Assert.All([attacked, beside, distant, goblin], creature => Assert.Equal(CombatSide.Neutral, creature.Side));

        // The ruleset's own answer: one kind and close by, one kind and too far, another kind close by.
        Assert.True(policy.ProvokedWith(attacked.Subject, beside.Subject));
        Assert.False(policy.ProvokedWith(attacked.Subject, distant.Subject));
        Assert.False(policy.ProvokedWith(attacked.Subject, goblin.Subject));

        // The party's attack ordered at the first guard is the fight's own provocation, and it reaches the second.
        CombatResult order = live.Combat!.Order(new AttackOrder(Member(live), AttackKind.Melee, attacked.Id));
        Assert.True(order.IsApplied, order.Refusal?.Message);
        session.Update(RulesetTestContext.Update(2, 1));
        Assert.Equal(CombatSide.Opposition, Actor(live, "monster-actor-1").Side);
        Assert.Equal(CombatSide.Opposition, Actor(live, "monster-actor-2").Side);
        Assert.Equal(CombatSide.Neutral, Actor(live, "monster-actor-3").Side);
        Assert.Equal(CombatSide.Neutral, Actor(live, "monster-actor-4").Side);
        Assert.Equal(CombatSide.Neutral, Actor(live, "person-11").Side);
    }

    [Fact]
    public void Wronging_a_peasant_turns_the_peasants_of_its_race_close_by_and_not_those_of_another()
    {
        (ProductCreateContext context, RecordingUiService ui) = RulesetTestContext.Create(Town());
        using IGameSession session = Casting(context, ui);
        MightAndMagic7Session live = (MightAndMagic7Session)session;
        session.Update(RulesetTestContext.Update(1, 1));

        // A person of one dwarf kind, another of a second dwarf kind beside them, and an elf beside them both.
        Assert.True(live.Combat!.Provoke(Actor(live, "person-10").Id));
        session.Update(RulesetTestContext.Update(2, 1));
        Assert.Equal(CombatSide.Opposition, Actor(live, "person-10").Side);
        Assert.Equal(CombatSide.Opposition, Actor(live, "person-11").Side);
        Assert.Equal(CombatSide.Neutral, Actor(live, "person-12").Side);
        Assert.Equal(CombatSide.Neutral, Actor(live, "monster-actor-1").Side);
    }

    [Fact]
    public void A_person_whose_record_carries_the_aggressor_bit_is_hostile()
    {
        (ProductCreateContext context, RecordingUiService ui) = RulesetTestContext.Create(Town());
        using IGameSession session = Casting(context, ui);
        MightAndMagic7Session live = (MightAndMagic7Session)session;
        session.Update(RulesetTestContext.Update(1, 1));
        MightAndMagic7Combat policy = Fight(context, live.Party!);

        // The same row as a peaceful person, read through the record: the aggressor bit makes them the party's enemy at
        // the longest band, so they are in the fight from 2,000 units off.
        Combatant aggressor = Actor(live, "person-13");
        Hostility nature = policy.NatureOf(aggressor.Subject);
        Assert.True(nature.AttacksOnSight);
        Assert.Equal(10240, nature.NoticeRange);
        Assert.Equal(CombatSide.Opposition, aggressor.Side);

        Combatant peaceful = Actor(live, "person-10");
        Assert.False(policy.NatureOf(peaceful.Subject).AttacksOnSight);
        Assert.Equal(CombatSide.Neutral, peaceful.Side);
    }

    /// <summary>The actor the session's own fight holds under its placement's own identity.</summary>
    private static Combatant Actor(MightAndMagic7Session live, string id) =>
        live.Combat!.Combatants.FirstOrDefault(combatant => !combatant.Subject.IsMember && combatant.Subject.Placement?.Content.Id == id)
        ?? throw new InvalidOperationException(
            $"No actor '{id}' stands in the fight, which holds: {string.Join(", ", live.Combat.Combatants.Select(combatant => $"{combatant.Name} ({combatant.Subject.Placement?.Content})"))}.");

    /// <summary>The party's first member, as the fight holds them.</summary>
    private static CombatantId Member(MightAndMagic7Session live) =>
        live.Combat!.Combatants.First(combatant => combatant.Subject.IsMember).Id;

    /// <summary>One creature's actor record as the importer writes it.</summary>
    private static string Record(int index, int monster, double x) =>
        FormattableString.Invariant(
            $$"""
            { "id": "actor-{{index}}", "kind": "actor", "sourceField": "actors", "sourceIndex": {{index}}, "x": {{x}}, "y": 0, "z": 0, "yaw": 0,
              "positionSource": "actor-record", "actorName": "Actor", "monster": {{monster}}, "monsterName": "Actor",
              "group": 0, "attributes": 0, "aiState": 0, "hitPoints": 30, "sectorId": 0 }
            """);

    /// <summary>One person's actor record as the importer writes it.</summary>
    private static string Person(int index, int monster, double y, int attributes = 0) =>
        FormattableString.Invariant(
            $$"""
            { "id": "person-{{index}}", "kind": "person", "sourceField": "actors", "sourceIndex": {{index}}, "x": 0, "y": {{y}}, "z": 0, "yaw": 0,
              "actorName": "Someone", "monster": {{monster}}, "attributes": {{attributes}}, "people": [ "npc-1" ] }
            """);

    /// <summary>One monster row, its kind the donor's grouping of three graded rows.</summary>
    private static string Row(int id, string name) =>
        FormattableString.Invariant(
            $$"""
            { "id": "{{id}}", "name": "{{name}}", "hostility": 3, "recovery": 100, "level": 4, "speed": 0,
              "hitPoints": 30, "armorClass": 0, {{MonsterRows.Combat(id, "Phys", "1d2+0")}} }
            """);

    /// <summary>
    /// A town whose level stands two guards side by side and a third 5,000 units from the first (row 13, kind five), a
    /// goblin beside them (row 7, kind three), and people: two dwarves of different kinds (rows 115 and 130, kinds 39
    /// and 44) and an elf (row 133, kind 45) side by side, and a dwarf whose record carries the aggressor bit.
    /// </summary>
    private static (string Path, string Text)[] Town() => Content(
        monster: ($"{RulesetTestContext.ContentDirectory}/content-packs/world/monsters.json",
            $$"""
            {
              "documentId": "monsters",
              "definitionKind": "monster",
              "entries": [
                {{Row(7, "A goblin")}},
                {{Row(13, "A guard")}},
                {{Row(115, "Peasant")}},
                {{Row(130, "Peasant")}},
                {{Row(133, "Peasant")}}
              ]
            }
            """),
        places: ($"{RulesetTestContext.ContentDirectory}/content-packs/world/places.json",
            $$"""
            {
              "documentId": "places",
              "definitionKind": "place",
              "entries": [
                { "id": "1", "kind": "region", "name": "The Town", "respawnDays": 1,
                  "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
                  "placements": [
                    {{Record(1, 13, 300)}},
                    {{Record(2, 13, 600)}},
                    {{Record(3, 13, 5300)}},
                    {{Record(4, 7, 450)}},
                    {{Person(10, 115, 3000)}},
                    {{Person(11, 130, 3300)}},
                    {{Person(12, 133, 3600)}},
                    {{Person(13, 115, -2000, attributes: 0x80000)}}
                  ] },
                { "id": "2", "kind": "interior", "name": "Cave", "respawnDays": 1,
                  "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ] }
              ]
            }
            """),
        extra:
        [
            ($"{RulesetTestContext.ContentDirectory}/content-packs/world/hostility.json",
                """
                {
                  "documentId": "hostility",
                  "definitionKind": "hostility",
                  "entries": [
                    { "id": "kinds", "columns": [ "Party", "Kind 1", "Kind 2", "Goblin", "Kind 4", "Guard" ] },
                    { "id": "Party", "kind": 0, "hostility": { } }
                  ]
                }
                """,
                """{ "path": "hostility.json", "documentId": "hostility", "definitionKind": "hostility" }"""),
            ($"{RulesetTestContext.ContentDirectory}/content-packs/world/people.json",
                """
                { "documentId": "people", "definitionKind": "person", "entries": [ { "id": "npc-1", "name": "Someone", "topics": [] } ] }
                """,
                """{ "path": "people.json", "documentId": "people", "definitionKind": "person" }"""),
        ]);
}
