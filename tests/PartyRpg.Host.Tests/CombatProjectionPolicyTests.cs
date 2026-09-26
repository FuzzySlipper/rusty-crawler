using System.Globalization;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Sessions;
using PartyRpg.Rulesets.MightAndMagic7;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Host.Tests;

/// <summary>
/// What the panel reads about a fight in this game, driven the way the DOM companion drives it: the
/// product's own action names on the product's own contract, and every fact read back from the projection
/// this ruleset composes.
/// </summary>
/// <remarks>
/// <para>
/// The kit's own suite proves the mechanism over a test rule. This one proves the composition: the creature's
/// row is this game's monster table, the recovery is the donor's character recovery, and the panel's
/// controls are the payload actions <c>ProductIdentity</c> declares and the companion sends. A fact the kit
/// publishes and this game fails to compose would be invisible here.
/// </para>
/// <para>
/// The fight is a two-character party against one row, which is the smallest shape in which readiness,
/// a refusal, a wound, and a death can all be read off the panel.
/// </para>
/// </remarks>
public sealed class CombatProjectionPolicyTests
{
    /// <summary>The action the companion's act control sends, which is also the intent the B key is mapped to.</summary>
    private const string Act = """{ "action": "party.attack" }""";

    [Fact]
    public void The_panel_reads_readiness_pools_and_the_refusal_of_a_recovering_member_from_this_games_state()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(
            [.. World(monsterAt: 100), MonsterRow(recovery: 100, hitPoints: 400), PartyDocument()]);
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            ProductTestContext.RulesetContext(context, ui, combat: true));
        session.Start();
        ulong step = 0;

        session.Update(ProductTestContext.Update(++step, 1));
        ProjectedNode combat = Combat(ui);

        // The aggro half of the ready light: the row's own band notices the party from 2560 units, the
        // creature stands a hundred off, and the panel says the party is engaged with it — while both members
        // are ready, which is the other half of the same light.
        Assert.True(combat.Field("engaged").AsBoolean());
        Assert.Equal(1d, combat.Field("opposition").AsNumber());
        Assert.Equal(2d, combat.Field("ready").AsNumber());
        Assert.Equal(100d, combat.Field("enemies").Item(0).Field("distance").AsNumber());

        // Each member's health and conditions stand in the fight block, read from the party that owns them.
        Assert.Equal(40d, combat.Field("members").Item(0).Field("hitPoints").AsNumber());
        Assert.Equal(string.Empty, combat.Field("members").Item(0).Field("conditions").AsString());
        Assert.False(combat.Field("members").Item(0).Field("down").AsBoolean());

        // And the party's own pools stand beside the fight in the same projection: what a member has left to
        // lose, what the party has left to cast with, and what is acting on it.
        ProjectedNode party = Party(ui);
        Assert.True(party.Field("present").AsBoolean());
        Assert.Equal(64d, party.Field("hitPoints").AsNumber());
        Assert.Equal(64d, party.Field("hitPointsMax").AsNumber());
        // The pool is the ruleset's own formula over the class, the level, and the scores
        // (MightAndMagic7Spells.SpellPointCapacity): a sorcerer's base of fifteen, plus the class's three a
        // level times the level one plus the intellect bonus the member's own score earns, which is nothing
        // for a record that states no intellect. A party that has just come into being holds all of it, so
        // what the scenario declared as its pool is the formula's answer rather than the scenario's number.
        Assert.Equal(18d, party.Field("spellPoints").AsNumber());
        Assert.Equal(18d, party.Field("spellPointsMax").AsNumber());
        Assert.Equal(string.Empty, party.Field("conditions").AsString());

        // The act control as the companion sends it: a payload action on the product's own contract, which is
        // what the panel's Attack button claims.
        session.Update(ProductTestContext.Update(++step, 1, ProductTestContext.Payload(Act)));
        combat = Combat(ui);
        Assert.Equal("applied", combat.Field("outcome").AsString());
        Assert.Equal(0d, combat.Field("ready").AsNumber());
        // What each member owes is the donor's own character recovery, read off the panel: a hundred-tick
        // swing less the ticks the member's Speed attribute is worth.
        Assert.Equal(22.969, combat.Field("members").Item(0).Field("recoverySeconds").AsNumber(), 3);
        Assert.Equal(22.266, combat.Field("members").Item(1).Field("recoverySeconds").AsNumber(), 3);
        Assert.All(
            Enumerable.Range(0, 2).Select(index => combat.Field("members").Item(index)),
            member => Assert.False(member.Field("ready").AsBoolean()));

        // The very same control asked again while everybody recovers is refused by name rather than being
        // quietly ignored: the panel is not merely showing a disabled light, the product answers.
        session.Update(ProductTestContext.Update(++step, 1, ProductTestContext.Payload(Act)));
        combat = Combat(ui);
        Assert.Equal("refused", combat.Field("outcome").AsString());
        Assert.Equal("recovering", combat.Field("code").AsString());
        Assert.Contains("still recovering", combat.Field("message").AsString(), StringComparison.Ordinal);
        Assert.False(combat.Field("resolved").AsBoolean());

        // Game time releases them, and the projection published by the update that released them is the one
        // that says so: the panel is never an update behind the fight it shows, and never ahead of it.
        int released = -1;
        for (int index = 0; index < 200 && released < 0; index++)
        {
            session.Update(ProductTestContext.Update(++step, 1));
            combat = Combat(ui);
            // The light and the number are one fact: a member reads ready exactly when the game time it owed
            // has reached zero, and the projection carries both readings of it in the same update.
            Assert.All(
                Enumerable.Range(0, 2).Select(member => combat.Field("members").Item(member)),
                member => Assert.Equal(
                    member.Field("ready").AsBoolean(),
                    member.Field("recoverySeconds").AsNumber() == 0));
            if (combat.Field("ready").AsNumber() == 2) released = index;
        }

        Assert.InRange(released, 0, 199);
    }

    [Fact]
    public void A_wound_and_a_condition_reach_the_fight_block_from_the_state_that_owns_them()
    {
        // The creature lands its own blow, which is where a wound and this game's own condition come from:
        // the row's special-attack column, applied to the member the swing reaches.
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(
            [.. World(monsterAt: 100), MonsterRow(recovery: 100, hitPoints: 400, damage: "1D4+4", special: "Poison2"), PartyDocument()]);
        FakeEngineContext fake = (FakeEngineContext)context.Engine;
        fake.RandomService.Answer = request => request.Key.EndsWith("/hit", StringComparison.Ordinal) ? 0 : 1;

        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            ProductTestContext.RulesetContext(context, ui, combat: true));
        session.Start();
        ulong step = 0;
        ProjectedNode combat = Combat(ui);
        for (step = 1; step <= 200 && !(combat.Field("resolved").AsBoolean() && !combat.Field("byParty").AsBoolean()); step++)
        {
            session.Update(ProductTestContext.Update(step, 1));
            combat = Combat(ui);
        }

        // The wound and what followed it are one reading of the member the party owns, and the party's own
        // condition row carries the same fact for a player reading the totals rather than the rows.
        Assert.False(combat.Field("byParty").AsBoolean());
        Assert.Contains("Poison", combat.Field("members").Item(0).Field("conditions").AsString(), StringComparison.Ordinal);
        Assert.True(combat.Field("members").Item(0).Field("hitPoints").AsNumber() < 40);
        Assert.Contains("Poison", Party(ui).Field("conditions").AsString(), StringComparison.Ordinal);
        Assert.True(Party(ui).Field("hitPoints").AsNumber() < 64);
    }

    [Fact]
    public void A_creature_brought_down_reads_as_down_and_leaves_a_body_the_panel_counts()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(
            [.. World(monsterAt: 100), MonsterRow(recovery: 100, hitPoints: 1), PartyDocument()]);
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            ProductTestContext.RulesetContext(context, ui, combat: true));
        session.Start();
        ulong step = 0;
        session.Update(ProductTestContext.Update(++step, 1));
        Assert.Equal(0d, ProjectedNode.Of(ui.Latest().Value).Field("interaction").Field("bodies").AsNumber());

        // Orders through the panel's own control until the creature is down: the panel publishes the death,
        // the opposition count drops, and the creature keeps its row so a player can see a body rather than a
        // place it never was.
        ProjectedNode combat = Combat(ui);
        for (int order = 0; order < 20 && combat.Field("opposition").AsNumber() > 0; order++)
        {
            session.Update(ProductTestContext.Update(++step, 1, ProductTestContext.Payload(Act)));
            combat = Combat(ui);
        }

        Assert.Equal(0d, combat.Field("opposition").AsNumber());
        ProjectedNode body = combat.Field("enemies").Item(0);
        Assert.True(body.Field("down").AsBoolean());
        Assert.False(body.Field("ready").AsBoolean());
        Assert.Equal(0d, body.Field("hitPoints").AsNumber());

        // The body is something the party can find here, which is what the interaction block counts: it is
        // published in the update after the one that made it, because the mechanism that discovers what the
        // place holds is stepped inside the same admitted update it is read in.
        session.Update(ProductTestContext.Update(++step, 1));
        Assert.Equal(1d, ProjectedNode.Of(ui.Latest().Value).Field("interaction").Field("bodies").AsNumber());
    }

    /// <summary>The fight block the panel renders, from the newest projection the product published.</summary>
    private static ProjectedNode Combat(RecordingUiService ui) => ProjectedNode.Of(ui.Latest().Value).Field("combat");

    /// <summary>The party block the panel renders, from the same projection.</summary>
    private static ProjectedNode Party(RecordingUiService ui) => ProjectedNode.Of(ui.Latest().Value).Field("party");

    /// <summary>
    /// A world of two places whose starting region holds one creature where the test says, plus the scenario
    /// start that puts the party in it.
    /// </summary>
    private static (string Path, string Text)[] World(double monsterAt) =>
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
            $$"""
            {
              "documentId": "places",
              "definitionKind": "place",
              "entries": [
                { "id": "1", "kind": "region", "name": "Home", "respawnDays": 1,
                  "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 512 } ],
                  "placements": [
                    { "id": "beast", "kind": "monster", "monster": "7", "x": {{monsterAt.ToString(CultureInfo.InvariantCulture)}}, "y": 0, "z": 0 } ] },
                { "id": "2", "kind": "interior", "name": "Cave", "respawnDays": 1,
                  "entryPoints": [ { "id": "Party Start", "x": 1, "y": 2, "z": 3, "yaw": 0 } ] }
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/start.json",
            """
            { "documentId": "start", "definitionKind": "scenario-start", "entries": [ { "id": "start", "place": "1", "entryPoint": "Party Start" } ] }
            """),
    ];

    /// <summary>A monster row shaped the way the importer emits one: typed columns and the whole raw row.</summary>
    private static (string Path, string Text) MonsterRow(
        int recovery = 100,
        int hitPoints = 40,
        string damage = "1D4+4",
        string special = "0",
        int hostility = 2,
        int level = 2,
        int armorClass = 5)
    {
        string[] cells = new string[39];
        for (int index = 0; index < cells.Length; index++) cells[index] = "0";
        cells[0] = "7";
        cells[1] = "A beast";
        cells[3] = level.ToString(CultureInfo.InvariantCulture);
        cells[4] = hitPoints.ToString(CultureInfo.InvariantCulture);
        cells[5] = armorClass.ToString(CultureInfo.InvariantCulture);
        cells[12] = hostility.ToString(CultureInfo.InvariantCulture);
        cells[14] = recovery.ToString(CultureInfo.InvariantCulture);
        cells[16] = special;
        cells[17] = "Phys";
        cells[18] = damage;
        string columns = string.Join(", ", cells.Select(cell => $"\"{cell}\""));
        return ($"{ProductTestContext.ContentDirectory}/content-packs/world/monsters.json",
            $$"""
            {
              "documentId": "monsters",
              "definitionKind": "monster",
              "entries": [
                { "id": "7", "name": "A beast", "level": {{level}}, "hitPoints": {{hitPoints}},
                  "armorClass": {{armorClass}}, "hostility": {{hostility}}, "recovery": {{recovery}},
                  "columns": [ {{columns}} ] }
              ]
            }
            """);
    }

    /// <summary>
    /// The scenario's party: a knight who can only swing and a sorcerer who carries spell points, so the
    /// party's pools are two different numbers rather than one repeated.
    /// </summary>
    private static (string Path, string Text) PartyDocument() =>
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/party.json",
            """
            {
              "documentId": "party",
              "definitionKind": "scenario-party",
              "entries": [
                {
                  "id": "party",
                  "coins": 200,
                  "food": 6,
                  "reputation": 0,
                  "fame": 0,
                  "members": [
                    { "name": "Roderick", "race": "Human", "class": "Knight", "level": 1, "hitPoints": 40,
                      "spellPoints": 0,
                      "attributes": [ { "id": "Might", "value": 17 }, { "id": "Accuracy", "value": 15 },
                                      { "id": "Endurance", "value": 15 }, { "id": "Luck", "value": 11 },
                                      { "id": "Speed", "value": 17 } ],
                      "skills": [ { "id": "Sword", "level": 1, "tier": 1, "pointsSpent": 1 } ],
                      "spells": [], "conditions": [] },
                    { "name": "Aelina", "race": "Elf", "class": "Sorcerer", "level": 1, "hitPoints": 24,
                      "spellPoints": 15,
                      "attributes": [ { "id": "Might", "value": 11 }, { "id": "Accuracy", "value": 15 },
                                      { "id": "Endurance", "value": 11 }, { "id": "Luck", "value": 15 },
                                      { "id": "Speed", "value": 25 } ],
                      "skills": [ { "id": "Staff", "level": 1, "tier": 1, "pointsSpent": 1 } ],
                      "spells": [], "conditions": [] }
                  ]
                }
              ]
            }
            """);
}
