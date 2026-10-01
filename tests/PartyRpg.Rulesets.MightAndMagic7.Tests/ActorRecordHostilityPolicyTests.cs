using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Sessions;
using PartyRpg.Testing;
using Rusty.Engine;
using Xunit;
using static PartyRpg.Rulesets.MightAndMagic7.Tests.SpellEffectPolicyTests;

namespace PartyRpg.Rulesets.MightAndMagic7.Tests;

/// <summary>
/// What a creature a level's own actor record stands thinks of the party: the record's own aggressor bit, the kind it
/// says it counts as, and otherwise what its kind thinks of the party in the shipped matrix — not its row's band, which
/// the donor overwrites with friendly when the level loads.
/// </summary>
/// <remarks>
/// Every row here states a non-zero band, as every one of the operator's monster rows does, so a creature that stays
/// out of the fight is one the record and the matrix keep out of it. The rule is the donor's
/// (OpenEnroth <c>src/Engine/Objects/Actor.cpp:2097-2116</c> and <c>:2122-2166</c>), cited where the ruleset states it.
/// </remarks>
public sealed class ActorRecordHostilityPolicyTests
{
    [Fact]
    public void A_guard_whose_kind_the_party_row_calls_friendly_stands_peaceful_and_a_goblin_it_names_is_hostile()
    {
        (ProductCreateContext context, RecordingUiService ui) = RulesetTestContext.Create(Town());
        using IGameSession session = Casting(context, ui);
        MightAndMagic7Session live = (MightAndMagic7Session)session;
        session.Update(RulesetTestContext.Update(1, 1));
        MightAndMagic7Combat policy = Fight(context, live.Party!);

        // The guard's kind is friendly to the party in the matrix, so it is a creature that starts no fight, beside the
        // party, though its own row states a band of three.
        Combatant guard = Creature(live, "monster-actor-1");
        Assert.False(policy.NatureOf(guard.Subject).AttacksOnSight);
        Assert.True(policy.NatureOf(guard.Subject).IsCreature);
        Assert.Equal(CombatSide.Neutral, guard.Side);

        // The goblin's kind is one the party's row names at band two, so it notices the party at that band's distance
        // (2560) and is in the fight from where it stands.
        Combatant goblin = Creature(live, "monster-actor-2");
        Hostility goblinNature = policy.NatureOf(goblin.Subject);
        Assert.True(goblinNature.AttacksOnSight);
        Assert.Equal(2560, goblinNature.NoticeRange);
        Assert.Equal(CombatSide.Opposition, goblin.Side);

        // A goblin beyond that band is hostile by nature and not yet in the fight.
        Combatant distant = Creature(live, "monster-actor-5");
        Assert.True(policy.NatureOf(distant.Subject).AttacksOnSight);
        Assert.Equal(CombatSide.Neutral, distant.Side);

        // A guard whose record carries the aggressor bit is the party's enemy at the longest band, whatever its kind.
        Combatant aggressor = Creature(live, "monster-actor-3");
        Assert.Equal(10240, policy.NatureOf(aggressor.Subject).NoticeRange);
        Assert.Equal(CombatSide.Opposition, aggressor.Side);

        // A goblin whose record says it counts as the guards' kind reads the guards' feelings, and starts nothing.
        Combatant sworn = Creature(live, "monster-actor-4");
        Assert.False(policy.NatureOf(sworn.Subject).AttacksOnSight);
        Assert.Equal(CombatSide.Neutral, sworn.Side);

        // A record of the party's own faction stands with the party.
        Combatant ally = Creature(live, "monster-actor-6");
        Assert.True(policy.NatureOf(ally.Subject).IsAllied);
        Assert.Equal(CombatSide.Ally, ally.Side);

        // Attacking the peaceful guard is what makes it an enemy, and it stays one.
        Assert.True(live.Combat!.Provoke(guard.Id));
        session.Update(RulesetTestContext.Update(2, 1));
        Assert.Equal(CombatSide.Opposition, Creature(live, "monster-actor-1").Side);
    }

    /// <summary>The creature the session's own fight holds under its placement's own identity.</summary>
    private static Combatant Creature(MightAndMagic7Session live, string id) =>
        live.Combat!.Combatants.FirstOrDefault(combatant => !combatant.Subject.IsMember && combatant.Subject.Placement?.Content.Id == id)
        ?? throw new InvalidOperationException(
            $"No creature '{id}' stands in the fight, which holds: {string.Join(", ", live.Combat.Combatants.Select(combatant => $"{combatant.Name} ({combatant.Subject.Placement?.Content})"))}.");

    /// <summary>One actor record as the importer writes it.</summary>
    private static string Record(int index, int monster, double x, int attributes = 0, int hostilityGroup = 0) =>
        FormattableString.Invariant(
            $$"""
            { "id": "actor-{{index}}", "kind": "actor", "sourceField": "actors", "sourceIndex": {{index}}, "x": {{x}}, "y": 0, "z": 0, "yaw": 0,
              "positionSource": "actor-record", "actorName": "Actor", "monster": {{monster}}, "monsterName": "Actor",
              "group": 0, "attributes": {{attributes}},{{(hostilityGroup == 0 ? string.Empty : FormattableString.Invariant($" \"hostilityGroup\": {hostilityGroup},"))}} "aiState": 0, "hitPoints": 30, "sectorId": 0 }
            """);

    /// <summary>
    /// A town whose level stands guards (row 13, kind five) and goblins (row 7, kind three) by its own records, and a
    /// matrix whose party row names the goblins at band two and says nothing of the guards.
    /// </summary>
    private static (string Path, string Text)[] Town() => Content(
        monster: ($"{RulesetTestContext.ContentDirectory}/content-packs/world/monsters.json",
            $$"""
            {
              "documentId": "monsters",
              "definitionKind": "monster",
              "entries": [
                { "id": "7", "name": "A goblin", "hostility": 3, "recovery": 100, "level": 4, "speed": 0,
                  "hitPoints": 30, "armorClass": 0, {{MonsterRows.Combat(7, "Phys", "1d2+0")}} },
                { "id": "13", "name": "A guard", "hostility": 3, "recovery": 100, "level": 4, "speed": 0,
                  "hitPoints": 30, "armorClass": 0, {{MonsterRows.Combat(13, "Phys", "1d2+0")}} }
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
                    {{Record(2, 7, 400)}},
                    {{Record(3, 13, 500, attributes: 0x80000)}},
                    {{Record(4, 7, 600, hostilityGroup: 5)}},
                    {{Record(5, 7, 4000)}},
                    {{Record(6, 13, 700, hostilityGroup: 9999)}}
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
                    { "id": "Party", "kind": 0, "hostility": { "3": 2 } },
                    { "id": "Goblin", "kind": 3, "hostility": { "5": 3 } },
                    { "id": "Guard", "kind": 5, "hostility": { "3": 3 } }
                  ]
                }
                """,
                """{ "path": "hostility.json", "documentId": "hostility", "definitionKind": "hostility" }"""),
        ]);
}
