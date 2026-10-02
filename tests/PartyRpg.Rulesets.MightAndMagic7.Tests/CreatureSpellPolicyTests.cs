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
    public void A_slowed_creature_covers_half_the_ground_in_the_same_step()
    {
        // The engine here walks a body at the pace of the profile each step hands it, on open ground; the composed
        // world's own creature mover is what hands it the creature's pace.
        ScriptedSpatialService spatial = new()
        {
            StepEnds = ScriptedSpatialService.AtProfilePace,
            NavigationCells = 12,
            Navigation = request => default(NavigationStepResult) with { Outcome = NavigationPathOutcome.Reached, NextWaypoint = request.Target },
        };
        (ProductCreateContext context, RecordingUiService ui) =
            RulesetTestContext.Create(persistence: null, spatial, new ScriptedContentService(), Creatures());
        using IGameSession session = Casting(context, ui);
        MightAndMagic7Session live = (MightAndMagic7Session)session;
        Stand(session);
        MightAndMagic7Combat policy = Fight(context, live.Party!);
        MightAndMagic7MonsterAi ai = MightAndMagic7MonsterAi.Compose(policy, random: null);
        ICreatureMover mover = live.World!.Creatures!;
        Combatant beast = Creature(live, "beast");
        CombatantId party = live.Combat!.Combatants[0].Id;
        PlacePose target = new(0, 100000, 0, 0, 0);

        // The pace is the row's own speed column, 200, the one the donor moves the creature at (OpenEnroth
        // src/Engine/Objects/Actor.cpp:2259), and not the party's own walking pace.
        CreatureMoveOutcome before = mover.Move(new CreatureMoveRequest(beast.Id, PlacePose.Origin, party, target, CreatureMovePurpose.Toward, ai.SpeedOf(beast.Subject), 0.25));
        Assert.Equal(200 * 0.25, before.MovedBy, precision: 2);

        // A novice slow is worth two, and the pace the mover hands the engine is divided by it (OpenEnroth
        // src/Engine/Spells/CastSpellInfo.cpp:572-610, src/Engine/Graphics/Indoor.cpp:814-816).
        CastAt(session, 2, "35", beast);
        Assert.Equal("cast", Magic(ui).Field("outcome").AsString());
        mover.Forget(beast.Id);
        CreatureMoveOutcome after = mover.Move(new CreatureMoveRequest(beast.Id, PlacePose.Origin, party, target, CreatureMovePurpose.Toward, ai.SpeedOf(beast.Subject), 0.25));
        Assert.Equal(before.MovedBy / 2, after.MovedBy, precision: 2);
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

    [Fact]
    public void A_charm_and_a_binding_put_a_creature_on_the_partys_side_and_a_berserk_one_is_everybodys_enemy()
    {
        (ProductCreateContext context, RecordingUiService ui) = RulesetTestContext.Create(Creatures());
        using IGameSession session = Casting(context, ui);
        MightAndMagic7Session live = (MightAndMagic7Session)session;
        Stand(session);
        MightAndMagic7Combat policy = Fight(context, live.Party!);
        MightAndMagic7MonsterAi ai = MightAndMagic7MonsterAi.Compose(policy, random: null);
        Assert.Equal(CombatSide.Opposition, Creature(live, "beast").Side);

        // A grand master's binding puts a living creature on the party's side (OpenEnroth
        // src/Engine/Spells/CastSpellInfo.cpp:2053-2087): the fight reads it as an ally, the party's own act no longer
        // aims at it, and it treats what fights the party as its enemy (src/Engine/Objects/Actor.cpp:2135-2160).
        CastAt(session, 2, "66", Creature(live, "beast"));
        Assert.Equal("cast", Magic(ui).Field("outcome").AsString());
        session.Update(RulesetTestContext.Update(3, 1));
        Combatant bound = Creature(live, "beast");
        Assert.Equal(CombatSide.Ally, bound.Side);
        Assert.True(policy.NatureOf(bound.Subject).IsAllied);
        Assert.True(ai.AreEnemies(bound.Subject, Creature(live, "ghost").Subject));
        Assert.True(ai.AreEnemies(Creature(live, "ghost").Subject, bound.Subject));

        // An undead creature is not bound by it, and the casting is spent all the same.
        CastAt(session, 4, "66", Creature(live, "ghost"));
        Assert.Contains("no creature in reach is one it takes hold of", Magic(ui).Field("message").AsString(), StringComparison.Ordinal);

        // A binding of the dead takes the ghost instead (CastSpellInfo.cpp:2719-2762), and two creatures bound to the
        // party are not each other's enemies.
        CastAt(session, 5, "94", Creature(live, "ghost"));
        Assert.Equal("cast", Magic(ui).Field("outcome").AsString());
        session.Update(RulesetTestContext.Update(6, 1));
        Assert.Equal(CombatSide.Ally, Creature(live, "ghost").Side);
        Assert.False(ai.AreEnemies(Creature(live, "ghost").Subject, Creature(live, "beast").Subject));

        // A berserk creature is everybody's enemy (Actor.cpp:2134, 2142), and it ends the binding the ghost was under.
        CastAt(session, 7, "62", Creature(live, "ghost"));
        Assert.Equal("cast", Magic(ui).Field("outcome").AsString());
        session.Update(RulesetTestContext.Update(8, 1));
        Combatant raging = Creature(live, "ghost");
        Assert.Equal(CombatSide.Opposition, raging.Side);
        Assert.True(ai.AreEnemies(raging.Subject, Creature(live, "beast").Subject));
        Assert.True(ai.AreEnemies(Creature(live, "far").Subject, raging.Subject));

        // A charm stands a creature with the party and keeps its own quarrels with other kinds (Actor.cpp:2152-2154).
        CastAt(session, 9, "60", Creature(live, "far"));
        Assert.Equal("cast", Magic(ui).Field("outcome").AsString());
        session.Update(RulesetTestContext.Update(10, 1));
        Assert.Equal(CombatSide.Ally, Creature(live, "far").Side);
        Assert.False(ai.AreEnemies(Creature(live, "far").Subject, Creature(live, "beast").Subject));

        // The party's own act aims at what fights it, never at a creature standing with it.
        IReadOnlyList<CombatResult> struck = live.Combat!.Engage();
        Assert.DoesNotContain(struck, result => result.Initiated?.Target == bound.Id);
        Assert.DoesNotContain(struck, result => result.Initiated?.Target == Creature(live, "far").Id);
    }

    [Fact]
    public void A_charmed_creature_beside_the_party_does_not_keep_it_from_making_camp()
    {
        (ProductCreateContext context, RecordingUiService ui) = RulesetTestContext.Create(Creatures());
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            RulesetTestContext.RulesetContext(context, ui) with
            {
                Cast = new CastIntentNames(Declared.UiActionContract),
                Rest = new RestIntentNames(
                    Declared.RestIntent,
                    Declared.CampIntent,
                    Declared.WaitUntilDawnIntent,
                    Declared.WaitAnHourIntent,
                    Declared.WaitFiveMinutesIntent,
                    Declared.UiActionContract),
            });
        session.Start();
        MightAndMagic7Session live = (MightAndMagic7Session)session;
        Stand(session);

        // A beast and a ghost stand a few hundred units off, well inside the donor's camping range (OpenEnroth
        // src/Engine/Objects/Actor.cpp:3458-3481), so the party will not lie down.
        session.Update(RulesetTestContext.Update(2, 1, RulesetTestContext.Digital(Declared.CampIntent)));
        ProjectedNode wary = ProjectedNode.Of(ui.Latest().Value).Field("rest");
        Assert.Equal(MightAndMagic7Codes.CampHostilesNear, wary.Field("code").AsString());
        Assert.Contains("There are 2 hostile", wary.Field("message").AsString(), StringComparison.Ordinal);

        // A charm stands the beast with the party: the donor's check passes over what is friendly to the party
        // (Actor.cpp:3473-3477, with a charm read as friendly at Actor.cpp:2097-2104), so it no longer counts.
        CastAt(session, 3, "60", Creature(live, "beast"));
        Assert.Equal("cast", Magic(ui).Field("outcome").AsString());
        session.Update(RulesetTestContext.Update(4, 1));
        Assert.Equal(CombatSide.Ally, Creature(live, "beast").Side);
        session.Update(RulesetTestContext.Update(5, 1, RulesetTestContext.Digital(Declared.CampIntent)));
        ProjectedNode still = ProjectedNode.Of(ui.Latest().Value).Field("rest");
        Assert.Equal(MightAndMagic7Codes.CampHostilesNear, still.Field("code").AsString());
        Assert.Contains("There are 1 hostile", still.Field("message").AsString(), StringComparison.Ordinal);

        // Whatever made a creature an ally makes it one here: the ghost a binding of the dead takes is the other, and
        // with both standing with the party it makes camp beside them.
        CastAt(session, 6, "94", Creature(live, "ghost"));
        Assert.Equal("cast", Magic(ui).Field("outcome").AsString());
        session.Update(RulesetTestContext.Update(7, 1));
        Assert.Equal(CombatSide.Ally, Creature(live, "ghost").Side);
        session.Update(RulesetTestContext.Update(8, 1, RulesetTestContext.Digital(Declared.CampIntent)));
        ProjectedNode camped = ProjectedNode.Of(ui.Latest().Value).Field("rest");
        Assert.Equal("camp", camped.Field("kind").AsString());
        Assert.Equal("applied", camped.Field("outcome").AsString());
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
                { "id": "60", "school": "Mind", "level": 5, "name": "Charm", "resist": "Mind" },
                { "id": "62", "school": "Mind", "level": 7, "name": "Berserk", "resist": "Mind" },
                { "id": "63", "school": "Mind", "level": 8, "name": "Mass Fear", "resist": "Mind" },
                { "id": "66", "school": "Mind", "level": 11, "name": "Enslave", "resist": "Mind" },
                { "id": "81", "school": "Light", "level": 6, "name": "Paralyze", "resist": "Light" },
                { "id": "92", "school": "Dark", "level": 2, "name": "Shrinking Ray", "resist": "Dark" },
                { "id": "94", "school": "Dark", "level": 4, "name": "Control Undead", "resist": "Dark" }
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
                                  { "id": "Mind", "level": 4, "tier": 4, "pointsSpent": 1 },
                                  { "id": "Light", "level": 2, "tier": 1, "pointsSpent": 1 },
                                  { "id": "Dark", "level": 4, "tier": 2, "pointsSpent": 1 } ],
                      "spells": [ "34", "35", "48", "60", "62", "63", "66", "81", "92", "94" ], "conditions": [] },
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
            ($"{RulesetTestContext.ContentDirectory}/content-packs/world/place-geometry.json",
                """
                { "documentId":"place-geometry", "definitionKind":"place-geometry",
                  "entries":[{ "id":"1", "artifact":{"stated":"by the scripted engine, which reads nothing"},
                               "navigationRegion":{"minimum":[-4096,0,-4096],"maximum":[4096,0,4096],"cellSize":128} }] }
                """,
                """{ "path":"place-geometry.json", "documentId":"place-geometry", "definitionKind":"place-geometry" }"""),
            ($"{RulesetTestContext.ContentDirectory}/content-packs/world/hostility.json",
                """
                {
                  "documentId": "hostility",
                  "definitionKind": "hostility",
                  "entries": [
                    { "id": "kinds", "columns": [ "Party", "Kind 1", "Kind 2", "Beast", "Ghost" ] },
                    { "id": "Party", "kind": 0, "hostility": { "3": 4, "4": 4 } }
                  ]
                }
                """,
                """{ "path": "hostility.json", "documentId": "hostility", "definitionKind": "hostility" }"""),
        ]);
}
