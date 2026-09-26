using System.Globalization;
using System.Text.RegularExpressions;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Sessions;
using PartyRpg.Rulesets.MightAndMagic7;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Host.Tests;

/// <summary>
/// This game's fight played in both pacings: the toggle and the turn actions declared in code, in the project
/// file, and in the companion; a fight switched mid-fight with everything it holds surviving the switch; and
/// the order a paced fight acts in read from the same recovery this game paces real time with.
/// </summary>
/// <remarks>
/// <para>
/// The kit's own suite proves the mechanism with recoveries it states itself. What only this suite can prove
/// is that this game's numbers feed it: a monster row's own recovery column becomes the initiative an actor
/// is ordered by and the length of the round it acts in, and a fight that is switched keeps the health, the
/// conditions, and the body count the real resolution path produced.
/// </para>
/// <para>
/// The whole trip is exercised here — the intent the host declares, the reader the ruleset composes, the
/// state, and the projection the companion renders — because a toggle that reaches nothing and a projection
/// that publishes nothing would each pass a narrower test.
/// </para>
/// </remarks>
public sealed class TurnBasedPolicyTests
{
    [Fact]
    public void The_pace_controls_are_declared_in_code_in_the_project_file_and_in_the_companion()
    {
        string source = File.ReadAllText(Path.Combine(SourceDirectory(), "ProductIdentity.cs"));
        string project = File.ReadAllText(ProjectFile());
        string product = File.ReadAllText(Path.Combine(SourceDirectory(), "CrawlerProduct.cs"));
        string ui = File.ReadAllText(Path.Combine(SourceDirectory(), "..", "ui", "main.ts"));

        string toggle = Constant(source, "TurnBasedToggleIntent");
        string skip = Constant(source, "TurnSkipIntent");
        string wait = Constant(source, "TurnWaitIntent");

        // Declared in code and in the project file, and mapped there: the engine refuses a mapping whose
        // intent it was never told about, so both halves are what make a key a control.
        foreach (string intent in new[] { toggle, skip, wait })
        {
            Assert.Contains($"RustyEngineProductInputIntent Include=\"{intent}\" Value=\"digital\"", project, StringComparison.Ordinal);
            Assert.Contains($"Intent=\"{intent}\"", project, StringComparison.Ordinal);
        }

        // The keys the product actually declared, named so a change to them is a decision rather than a
        // silent edit: Enter is the original's own toggle, and the two turn actions take letters no other
        // control claims during play.
        Assert.Contains("Trigger=\"key:enter:pressed\"", project, StringComparison.Ordinal);
        Assert.Contains("Trigger=\"key:key-k:pressed\"", project, StringComparison.Ordinal);
        Assert.Contains("Trigger=\"key:key-y:pressed\"", project, StringComparison.Ordinal);

        // The host hands the ruleset the declared names beside the act control, and the companion sends the
        // declared actions on the product's own contract.
        Assert.Contains("new TurnIntentNames(", product, StringComparison.Ordinal);
        Assert.Contains("Combat: _combat", product, StringComparison.Ordinal);
        AssertUiConstant(ui, "ACTION_TURN_BASED", toggle);
        AssertUiConstant(ui, "ACTION_TURN_SKIP", skip);
        AssertUiConstant(ui, "ACTION_TURN_WAIT", wait);
    }

    [Fact]
    public void A_fight_switched_mid_fight_keeps_its_health_conditions_and_bodies_and_goes_on_in_the_other_pacing()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(
            [.. World(100), MonsterRow(recovery: 100, hitPoints: 200, damage: "1D4+4", special: "Poison2"), PartyDocument()]);
        FakeEngineContext fake = (FakeEngineContext)context.Engine;

        // The creature lands its own blow in real time — a wound and the condition its row states — and the
        // party answers it, so the switch below has both sides' state to preserve.
        fake.RandomService.Answer = request => request.Key.EndsWith("/hit", StringComparison.Ordinal) ? 0 : 1;
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(ProductTestContext.RulesetContext(context, ui, combat: true));
        session.Start();
        ulong step = 0;
        ProjectedNode combat = ProjectedNode.Of(ui.Latest().Value).Field("combat");
        for (step = 1; step <= 200 && !Struck(combat); step++)
        {
            session.Update(ProductTestContext.Update(step, 1));
            combat = ProjectedNode.Of(ui.Latest().Value).Field("combat");
        }

        Assert.True(Struck(combat), $"the creature never struck the party: {combat.Field("message").AsString()}");
        Assert.Contains("Poison", combat.Field("members").Item(0).Field("conditions").AsString(), StringComparison.Ordinal);
        string[] hurt = Portrait(combat);
        Assert.True(combat.Field("members").Item(0).Field("hitPoints").AsNumber() < 40);

        // Switching the pacing mid-fight: the panel publishes the round, whose turn it is, and the order —
        // and every fact about the fight itself is exactly what it was a moment before.
        session.Update(ProductTestContext.Update(++step, 1, ProductTestContext.Digital(ProductIdentity.TurnBasedToggleIntent)));
        combat = ProjectedNode.Of(ui.Latest().Value).Field("combat");
        Assert.Equal("turnbased", combat.Field("pacing").AsString());
        Assert.Equal("action", combat.Field("turn").Field("phase").AsString());
        Assert.Equal(1, combat.Field("turn").Field("round").AsNumber());
        Assert.Equal(hurt, Portrait(combat));

        // The round's length and its order are this game's own recovery answers: the row's hundred ticks are
        // twenty-three and a half game seconds of recovery (128 ticks to a real second and thirty game
        // seconds to a real second), which is longer than a character's own swing, so the creature that owes
        // the most is what the round lasts — and every actor's remaining recovery is inside it.
        double round = combat.Field("turn").Field("roundSeconds").AsNumber();
        Assert.Equal(23.438, round, 3);
        double[] owed = [.. Owed(combat)];
        Assert.All(owed, value => Assert.True(value <= round, $"an actor owed {value} in a {round} round"));
        double[] orderOwed = [.. Enumerable
            .Range(0, (int)combat.Field("turn").Field("order").Length())
            .Select(index => OrderOwed(combat.Field("turn").Field("order").Item(index)))];
        Assert.Equal(orderOwed.Order(), orderOwed);
        Assert.Equal(owed.Order(), orderOwed.Order());

        // Its first turn is the party's, and the session waits for it: no world steps and no game time passes
        // while a committed turn is outstanding.
        Assert.True(combat.Field("turn").Field("playerTurn").AsBoolean());
        string[] order = [.. Order(combat)];
        Assert.Contains(combat.Field("turn").Field("actorName").AsString(), order);

        // Committed turns in the paced fight, until the creature has had its own: the opposition acts in
        // rounds through the same driver it acts through in real time.
        bool creatureActed = false;
        List<string> trail = [];
        for (int press = 0; press < 12 && !creatureActed; press++)
        {
            session.Update(ProductTestContext.Update(++step, 1, ProductTestContext.Digital(ProductIdentity.AttackIntent)));
            session.Update(ProductTestContext.Update(++step, 1, ProductTestContext.Digital(ProductIdentity.AttackIntent, InputEdge.Released)));
            combat = ProjectedNode.Of(ui.Latest().Value).Field("combat");
            Assert.Equal("turnbased", combat.Field("pacing").AsString());
            creatureActed = combat.Field("byParty").AsBoolean() == false && combat.Field("resolved").AsBoolean();
            trail.Add($"{combat.Field("turn").Field("actorName").AsString()}|{combat.Field("actor").AsString()}|{combat.Field("outcome").AsString()}|{combat.Field("byParty").AsBoolean()}|{combat.Field("message").AsString()}");
        }

        Assert.True(creatureActed, $"the creature never acted in the paced fight: {string.Join(" ;; ", trail)}");
        Assert.NotEqual(hurt, Portrait(combat));

        // Switching back: the fight continues in real time from exactly what the paced round left, and the
        // panel says so.
        string[] paced = Portrait(combat);
        session.Update(ProductTestContext.Update(++step, 1, ProductTestContext.Digital(ProductIdentity.TurnBasedToggleIntent)));
        combat = ProjectedNode.Of(ui.Latest().Value).Field("combat");
        Assert.Equal("realtime", combat.Field("pacing").AsString());
        Assert.Equal("none", combat.Field("turn").Field("phase").AsString());
        Assert.Equal(paced, Portrait(combat));

        // And the world steps again: the session measures admitted intervals while it is running, which is
        // exactly what a switch that had left it waiting for a turn would not do.
        double measured = ProjectedNode.Of(ui.Latest().Value).Field("session").Field("simulationSeconds").AsNumber();
        Assert.Equal(SessionMode.Running, session.Mode);
        for (int update = 0; update < 4; update++) session.Update(ProductTestContext.Update(++step, 1));
        Assert.True(
            ProjectedNode.Of(ui.Latest().Value).Field("session").Field("simulationSeconds").AsNumber() > measured,
            "the session did not step the world again after the pacing was switched back");
        combat = ProjectedNode.Of(ui.Latest().Value).Field("combat");
        Assert.Equal(paced, Portrait(combat));
        Assert.False(combat.Field("turn").Field("playerTurn").AsBoolean());
    }

    [Fact]
    public void The_turn_actions_pass_and_defer_a_turn_and_the_panel_shows_which()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(
            [.. World(100), MonsterRow(recovery: 100, hitPoints: 400), PartyDocument()]);
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(ProductTestContext.RulesetContext(context, ui, combat: true));
        session.Start();
        ulong step = 0;
        session.Update(ProductTestContext.Update(++step, 1));
        session.Update(ProductTestContext.Update(++step, 1, ProductTestContext.Digital(ProductIdentity.TurnBasedToggleIntent)));

        ProjectedNode combat = ProjectedNode.Of(ui.Latest().Value).Field("combat");
        Assert.Equal(SessionMode.TurnBased, session.Mode);
        string first = combat.Field("turn").Field("actorName").AsString();
        Assert.True(combat.Field("turn").Field("playerTurn").AsBoolean());

        // Skipping forfeits the first member's turn: the panel says a skip happened, the order says who holds
        // the turn now, and the skipped member is not the one holding it.
        session.Update(ProductTestContext.Update(++step, 1, ProductTestContext.Payload("""{ "action": "combat.turn-skip" }""")));
        combat = ProjectedNode.Of(ui.Latest().Value).Field("combat");
        Assert.Equal("skip", combat.Field("turn").Field("last").AsString());
        Assert.NotEqual(first, combat.Field("turn").Field("actorName").AsString());

        // Waiting defers the turn to the end of the round, which the order publishes as a waiting actor.
        session.Update(ProductTestContext.Update(++step, 1, ProductTestContext.Payload("""{ "action": "combat.turn-wait" }""")));
        combat = ProjectedNode.Of(ui.Latest().Value).Field("combat");
        Assert.Equal("wait", combat.Field("turn").Field("last").AsString());
        Assert.Contains(Order(combat), name => name.Length > 0);
        Assert.Contains(
            Enumerable.Range(0, (int)combat.Field("turn").Field("order").Length()),
            index => combat.Field("turn").Field("order").Item(index).Field("waiting").AsBoolean());
    }

    [Fact]
    public void The_same_fight_takes_the_same_turns_under_one_seed_in_the_paced_mode()
    {
        string[] first = Paced(attackTurns: 6);
        string[] again = Paced(attackTurns: 6);
        Assert.Equal(first, again);

        // The pacing adds no draw of its own: the same committed turns against the same world produce the
        // same blows, in the same order, with the same numbers.
        Assert.Contains(first, line => line.Contains("melee", StringComparison.Ordinal));
        Assert.Contains(first, line => line.Contains("hit=True", StringComparison.Ordinal) || line.Contains("hit=False", StringComparison.Ordinal));
    }

    /// <summary>One staged fight played in the paced mode, with what each committed turn left.</summary>
    private static string[] Paced(int attackTurns)
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(
            [.. World(100), MonsterRow(recovery: 100, hitPoints: 400, damage: "1D4+4"), PartyDocument()]);
        ((FakeEngineContext)context.Engine).RandomService.Roll = 40;
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(ProductTestContext.RulesetContext(context, ui, combat: true));
        session.Start();
        ulong step = 0;
        session.Update(ProductTestContext.Update(++step, 1));
        session.Update(ProductTestContext.Update(++step, 1, ProductTestContext.Digital(ProductIdentity.TurnBasedToggleIntent)));

        List<string> trail = [];
        for (int press = 0; press < attackTurns; press++)
        {
            session.Update(ProductTestContext.Update(++step, 1, ProductTestContext.Digital(ProductIdentity.AttackIntent)));
            session.Update(ProductTestContext.Update(++step, 1, ProductTestContext.Digital(ProductIdentity.AttackIntent, InputEdge.Released)));
            ProjectedNode combat = ProjectedNode.Of(ui.Latest().Value).Field("combat");
            trail.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"{combat.Field("turn").Field("round").AsNumber()}/{combat.Field("turn").Field("actorName").AsString()}:{combat.Field("actor").AsString()}:{combat.Field("kind").AsString()}:{combat.Field("target").AsString()}:{combat.Field("outcome").AsString()}:{combat.Field("code").AsString()}:hit={combat.Field("hit").AsBoolean()}:{combat.Field("damage").AsNumber()}:{combat.Field("members").Item(0).Field("hitPoints").AsNumber()}:{combat.Field("enemies").Item(0).Field("hitPoints").AsNumber()}"));
        }

        return [.. trail];
    }

    /// <summary>
    /// The whole fight as the panel published it: both sides, their health, what is acting on them, and
    /// where they stand — everything a switch must leave exactly as it was.
    /// </summary>
    private static string[] Portrait(ProjectedNode combat)
    {
        List<string> lines = [];
        foreach (string side in new[] { "members", "enemies" })
        {
            for (int index = 0; index < combat.Field(side).Length(); index++)
            {
                ProjectedNode actor = combat.Field(side).Item(index);
                lines.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{actor.Field("name").AsString()}|{actor.Field("hitPoints").AsNumber()}/{actor.Field("hitPointsMax").AsNumber()}|{actor.Field("conditions").AsString()}|{actor.Field("down").AsBoolean()}|{actor.Field("distance").AsNumber()}|{actor.Field("ready").AsBoolean()}"));
            }
        }

        return [.. lines];
    }

    /// <summary>What every actor in the fight still owes, from the fight block and from the order.</summary>
    private static IEnumerable<double> Owed(ProjectedNode combat)
    {
        for (int index = 0; index < combat.Field("members").Length(); index++)
        {
            yield return combat.Field("members").Item(index).Field("recoverySeconds").AsNumber();
        }

        for (int index = 0; index < combat.Field("enemies").Length(); index++)
        {
            yield return combat.Field("enemies").Item(index).Field("recoverySeconds").AsNumber();
        }
    }

    /// <summary>What one actor of the order still owes.</summary>
    private static double OrderOwed(ProjectedNode entry) => entry.Field("remainingSeconds").AsNumber();

    /// <summary>The order the round will act in, as the panel named it.</summary>
    private static IEnumerable<string> Order(ProjectedNode combat)
    {
        for (int index = 0; index < combat.Field("turn").Field("order").Length(); index++)
        {
            yield return combat.Field("turn").Field("order").Item(index).Field("name").AsString();
        }
    }

    /// <summary>Whether the last order the panel shows is a creature's own resolved blow.</summary>
    private static bool Struck(ProjectedNode combat) =>
        combat.Field("byParty").AsBoolean() == false && combat.Field("message").AsString().Contains("attacks", StringComparison.Ordinal);

    private static string SourceDirectory() => Path.Combine(RepositoryRoot(), "src", "PartyRpg.Host");

    private static string ProjectFile() => Path.Combine(SourceDirectory(), "PartyRpg.Host.csproj");

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")) &&
                Directory.Exists(Path.Combine(directory.FullName, "src")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find the repository root above the test assembly.");
    }

    private static string Constant(string source, string name)
    {
        Match match = Regex.Match(source, $@"const string {name} = ([^;]*);", RegexOptions.CultureInvariant);
        Assert.True(match.Success, $"ProductIdentity.cs must declare {name}.");
        // The pace constants are the wire names themselves, so the declaration is read through the same
        // vocabulary the panel sends rather than through a literal repeated here.
        string expression = match.Groups[1].Value.Trim();
        Match wire = Regex.Match(expression, @"TurnActions\.(\w+)", RegexOptions.CultureInvariant);
        if (wire.Success)
        {
            return wire.Groups[1].Value switch
            {
                "Toggle" => TurnActions.Toggle,
                "Skip" => TurnActions.Skip,
                _ => TurnActions.Wait,
            };
        }

        Match literal = Regex.Match(expression, "^\"([^\"]*)\"$", RegexOptions.CultureInvariant);
        Assert.True(literal.Success, $"ProductIdentity.cs must declare {name} as a name or a wire constant.");
        return literal.Groups[1].Value;
    }

    private static void AssertUiConstant(string uiSource, string name, string expected)
    {
        Match match = Regex.Match(uiSource, $@"const {name} = '([^']*)';", RegexOptions.CultureInvariant);
        Assert.True(match.Success, $"src/ui/main.ts must declare {name}.");
        Assert.Equal(expected, match.Groups[1].Value);
    }

    /// <summary>
    /// A world of two places whose starting region holds a creature where the test says, close enough to
    /// notice the party and to reach it with a swing.
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

    /// <summary>The scenario's party: two characters whose pools and conditions a switch must preserve.</summary>
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
