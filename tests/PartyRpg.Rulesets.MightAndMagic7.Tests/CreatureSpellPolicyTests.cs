using System.Globalization;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using PartyRpg.Testing;
using Rusty.Engine;
using Xunit;
using static PartyRpg.Rulesets.MightAndMagic7.Tests.SpellEffectPolicyTests;

namespace PartyRpg.Rulesets.MightAndMagic7.Tests;

/// <summary>
/// The spells that leave something on a creature: a paralysis the fight's gate reads, a slowing its recovery and its
/// pace read, a stun that pushes its recovery back, a shrinking its blows read, and the fears its own decisions read.
/// </summary>
/// <remarks>
/// Every case casts a shipped spell by its own id through the product's own session, at a creature the session's own
/// fight holds, and reads the result from the fight's answers about that creature. The numbers are the donor's, cited
/// where the ruleset states them.
/// </remarks>
public sealed class CreatureSpellPolicyTests
{
    [Fact]
    public void A_paralysis_holds_a_creature_until_the_clock_lets_it_go_and_an_immune_one_is_untouched()
    {
        (ProductCreateContext context, RecordingUiService ui) = RulesetTestContext.Create(Creatures());
        using IGameSession session = Casting(context, ui);
        MightAndMagic7Session live = (MightAndMagic7Session)session;
        Stand(session);
        MightAndMagic7Combat policy = Fight(context, live.Party!);
        Combatant beast = Creature(live, "beast");

        // Paralyze lasts three minutes a level of light (OpenEnroth src/Engine/Spells/CastSpellInfo.cpp:549-570): two
        // levels are six minutes, and a paralysed creature does nothing (src/Engine/Objects/Actor.cpp:169-176).
        CastAt(session, 2, "81", beast);
        Assert.Equal("cast", Magic(ui).Field("outcome").AsString());
        Assert.False(policy.CanAct(beast.Subject));
        CombatResult refused = live.Combat!.Order(new AttackOrder(beast.Id, AttackKind.Melee, live.Combat.Combatants[0].Id));
        Assert.False(refused.IsApplied);
        Assert.Equal(CombatCodes.Incapacitated, refused.Refusal!.Code);

        // A creature held still is still standing: the party can still strike it and the place still holds it.
        Assert.False(live.Combat.IsDown(beast));
        Assert.Contains(live.Combat.Opposition, combatant => combatant.Id == beast.Id);

        // An hour of game time is far more than six minutes: the paralysis has run out with the clock.
        Advance(session, 1);
        Assert.True(policy.CanAct(Creature(live, "beast").Subject));

        // A creature immune to light is named as immune, and nothing takes hold of it.
        Combatant ghost = Creature(live, "ghost");
        CastAt(session, 3, "81", ghost);
        Assert.Contains("immune", Magic(ui).Field("message").AsString(), StringComparison.Ordinal);
        Assert.True(policy.CanAct(ghost.Subject));
    }

    [Fact]
    public void A_slowing_doubles_a_creatures_recovery_and_divides_its_pace()
    {
        (ProductCreateContext context, RecordingUiService ui) = RulesetTestContext.Create(Creatures());
        using IGameSession session = Casting(context, ui);
        MightAndMagic7Session live = (MightAndMagic7Session)session;
        Stand(session);
        MightAndMagic7Combat policy = Fight(context, live.Party!);
        MightAndMagic7MonsterAi ai = MightAndMagic7MonsterAi.Compose(policy, random: null);
        Combatant beast = Creature(live, "beast");
        GameDuration recovery = policy.RecoveryAfter(beast.Subject, AttackKind.Melee);
        double pace = ai.SpeedOf(beast.Subject);

        // A novice slow is worth two (OpenEnroth src/Engine/Spells/CastSpellInfo.cpp:572-610): recovery doubles
        // (src/Engine/Objects/Actor.cpp:1296) and the pace is divided by the power (src/Engine/Graphics/Indoor.cpp:814-816).
        CastAt(session, 2, "35", beast);
        Assert.Equal("cast", Magic(ui).Field("outcome").AsString());
        Assert.Equal(recovery.Milliseconds * 2, policy.RecoveryAfter(beast.Subject, AttackKind.Melee).Milliseconds);
        Assert.Equal(pace / 2, ai.SpeedOf(beast.Subject));

        // Casting it is an act against the creature, which puts it into the fight.
        Assert.Equal(CombatSide.Opposition, Creature(live, "beast").Side);
    }

    [Fact]
    public void A_stun_pushes_a_creatures_recovery_back_and_a_shrinking_divides_its_blow()
    {
        (ProductCreateContext context, RecordingUiService ui) = RulesetTestContext.Create(Creatures());
        using IGameSession session = Casting(context, ui);
        MightAndMagic7Session live = (MightAndMagic7Session)session;
        Stand(session);
        MightAndMagic7Combat policy = Fight(context, live.Party!);
        Combatant beast = Creature(live, "beast");
        long before = beast.Recovery.Milliseconds;

        // Twenty of the donor's ticks onto what the creature must recover (OpenEnroth src/Engine/Objects/Actor.cpp:3179-3188).
        CastAt(session, 2, "34", beast);
        Assert.Equal("cast", Magic(ui).Field("outcome").AsString());
        Assert.True(
            Creature(live, "beast").Recovery.Milliseconds >= before + MightAndMagic7Combat.Ticks(20).Milliseconds - 1000,
            "the stun added the donor's twenty ticks, less what the update that carried it recovered");

        // An expert shrinking ray is worth three, and a shrunk creature's blow is divided by it
        // (src/Engine/Objects/SpriteObject.cpp:1006-1022, src/Engine/Objects/Character.cpp:5842-5846).
        CombatSubject member = live.Combat!.Combatants.First(combatant => combatant.Subject.IsMember).Subject;
        Assert.Equal(1, policy.PlanOf(beast.Subject, member, AttackKind.Melee).Divisor);
        CastAt(session, 3, "92", beast);
        Assert.Equal("cast", Magic(ui).Field("outcome").AsString());
        Assert.Equal(3, policy.PlanOf(beast.Subject, member, AttackKind.Melee).Divisor);
    }

    [Fact]
    public void A_turning_frightens_the_undead_in_view_and_a_mass_fear_the_living_and_both_run()
    {
        (ProductCreateContext context, RecordingUiService ui) = RulesetTestContext.Create(Creatures());
        using IGameSession session = Casting(context, ui);
        MightAndMagic7Session live = (MightAndMagic7Session)session;
        Stand(session);
        MightAndMagic7Combat policy = Fight(context, live.Party!);
        MightAndMagic7MonsterAi ai = MightAndMagic7MonsterAi.Compose(policy, random: null);
        Combatant beast = Creature(live, "beast");
        Combatant ghost = Creature(live, "ghost");
        Combatant far = Creature(live, "far");

        // Turn undead takes hold of every undead creature in view (OpenEnroth src/Engine/Spells/CastSpellInfo.cpp:1734-1762),
        // and the ghost's kind is one of the donor's undead (src/Engine/Objects/MonsterEnumFunctions.cpp:278-288).
        Assert.True(policy.IsUndead(ghost.Subject));
        CastAt(session, 2, "48", ghost);
        Assert.Equal("cast", Magic(ui).Field("outcome").AsString());
        Assert.True(MightAndMagic7Combat.IsAfraid(ghost.Subject));
        Assert.False(MightAndMagic7Combat.IsAfraid(beast.Subject));

        // A mass fear takes hold of the living in view and leaves the undead alone (CastSpellInfo.cpp:2089-2119); the
        // beast standing beyond the donor's mass-spell depth is out of view.
        CastAt(session, 3, "63", beast);
        Assert.Equal("cast", Magic(ui).Field("outcome").AsString());
        Assert.True(MightAndMagic7Combat.IsAfraid(beast.Subject));
        Assert.False(MightAndMagic7Combat.IsAfraid(far.Subject));

        // A frightened creature runs from what it fights (src/Engine/TurnEngine/TurnEngine.cpp:889-892).
        Combatant member = live.Combat!.Combatants.First(combatant => combatant.Subject.IsMember);
        CreatureSituation situation = new(
            Creature(live, "beast"),
            HitPoints: 200,
            HitPointsMax: 200,
            [new CreatureCandidate(member, IsEnemy: true, IsParty: true, Distance: 300)],
            Round: 0,
            PlacePose.Origin);
        Assert.Equal(CreatureAction.Retreat, ai.Decide(situation).Action);
    }

    /// <summary>The creature the session's own fight holds under its placement's own identity.</summary>
    private static Combatant Creature(MightAndMagic7Session live, string name) =>
        live.Combat!.Combatants.FirstOrDefault(combatant => !combatant.Subject.IsMember && combatant.Subject.Placement?.Content.Id == name)
        ?? throw new InvalidOperationException(
            $"No creature '{name}' stands in the fight, which holds: {string.Join(", ", live.Combat.Combatants.Select(combatant => $"{combatant.Name} ({combatant.Subject.Placement?.Content})"))}.");

    /// <summary>Lets the session take one admitted update, which is the one that reads the place's creatures into its fight.</summary>
    private static void Stand(IGameSession session) => session.Update(RulesetTestContext.Update(1, 1));

    /// <summary>Casts one spell at one creature, as the panel's own control does.</summary>
    private static void CastAt(IGameSession session, ulong step, string spell, Combatant creature) =>
        session.Update(RulesetTestContext.Update(
            step,
            1,
            RulesetTestContext.Payload(
                string.Create(CultureInfo.InvariantCulture, $$"""{"action":"party.cast","member":0,"spell":"{{spell}}","target":"{{creature.Id}}"}"""))));

    /// <summary>This suite's world: a beast and a ghost beside the party, and a beast far off.</summary>
    private static (string Path, string Text)[] Creatures() => Content(
        spells: ($"{RulesetTestContext.ContentDirectory}/content-packs/world/spells.json",
            """
            {
              "documentId": "spells",
              "definitionKind": "spell",
              "entries": [
                { "id": "34", "school": "Earth", "level": 1, "name": "Stun", "resist": "Earth" },
                { "id": "35", "school": "Earth", "level": 2, "name": "Slow", "resist": "Earth" },
                { "id": "48", "school": "Spirit", "level": 4, "name": "Turn Undead", "resist": "Spirit" },
                { "id": "63", "school": "Mind", "level": 8, "name": "Mass Fear", "resist": "Mind" },
                { "id": "81", "school": "Light", "level": 6, "name": "Paralyze", "resist": "Light" },
                { "id": "92", "school": "Dark", "level": 2, "name": "Shrinking Ray", "resist": "Dark" }
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
                  "members": [
                    { "name": "Aelina", "race": "Elf", "class": "Sorcerer", "level": 30, "hitPoints": 40, "spellPoints": 0,
                      "attributes": [ { "id": "Might", "value": 9 }, { "id": "Intellect", "value": 50 },
                                      { "id": "Personality", "value": 15 }, { "id": "Endurance", "value": 30 },
                                      { "id": "Accuracy", "value": 30 }, { "id": "Speed", "value": 25 },
                                      { "id": "Luck", "value": 13 } ],
                      "skills": [ { "id": "Earth", "level": 3, "tier": 1, "pointsSpent": 1 },
                                  { "id": "Spirit", "level": 3, "tier": 1, "pointsSpent": 1 },
                                  { "id": "Mind", "level": 4, "tier": 3, "pointsSpent": 1 },
                                  { "id": "Light", "level": 2, "tier": 1, "pointsSpent": 1 },
                                  { "id": "Dark", "level": 4, "tier": 2, "pointsSpent": 1 } ],
                      "spells": [ "34", "35", "48", "63", "81", "92" ], "conditions": [] },
                    { "name": "Borin", "race": "Human", "class": "Knight", "level": 1, "hitPoints": 40, "spellPoints": 0,
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
                { "id": "7", "name": "A beast", "hostility": 1, "recovery": 100, "level": 4, "speed": 200,
                  "hitPoints": 200, "armorClass": 0, {{MonsterRows.Combat(7, "Fire", "2d6+0")}} },
                { "id": "10", "name": "A ghost", "hostility": 1, "recovery": 100, "level": 4, "speed": 200,
                  "hitPoints": 200, "armorClass": 0,
                  {{MonsterRows.Combat(10, "Phys", "1d6+0", resistances: new Dictionary<string, string> { ["Light"] = "Imm" })}} }
              ]
            }
            """),
        places: ($"{RulesetTestContext.ContentDirectory}/content-packs/world/places.json",
            """
            {
              "documentId": "places",
              "definitionKind": "place",
              "entries": [
                { "id": "1", "kind": "region", "name": "The Guild of Fire", "respawnDays": 1,
                  "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
                  "placements": [
                    { "id": "beast", "kind": "monster", "monster": "7", "name": "A beast", "x": 300, "y": 0, "z": 0 },
                    { "id": "ghost", "kind": "monster", "monster": "10", "name": "A ghost", "x": 400, "y": 0, "z": 0 },
                    { "id": "far", "kind": "monster", "monster": "7", "name": "A distant beast", "x": 9000, "y": 0, "z": 0 }
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
                    { "id": "kinds", "columns": [ "Party", "Kind 1", "Kind 2", "Beast", "Ghost" ] },
                    { "id": "Beast", "kind": 3, "hostility": { "0": 4 } },
                    { "id": "Ghost", "kind": 4, "hostility": { "0": 4 } }
                  ]
                }
                """,
                """{ "path": "hostility.json", "documentId": "hostility", "definitionKind": "hostility" }"""),
        ]);
}
