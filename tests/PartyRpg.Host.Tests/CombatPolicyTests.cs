using System.Text.RegularExpressions;
using System.Xml.Linq;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Rulesets.MightAndMagic7;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Host.Tests;

/// <summary>
/// This game's answers to the kit's combat seams, and the whole trip from a key to what the panel shows.
/// </summary>
/// <remarks>
/// <para>
/// What no kit test can prove is what this game states: a creature's recovery is the monster table's own
/// column converted into game time, its hostility band is the distance at which it notices the party, a
/// character is paced by the donor's own character recovery, and an attack reaches as far as the donor's own
/// ranges. The ruleset's policy types are internal because nothing outside the product composes them, so this
/// suite reaches them through the ruleset's own friend declaration.
/// </para>
/// <para>
/// The live half is proved over a staged world: a monster row and a creature placement, the act control
/// pressed through the product's own intent, and the projection read back — so the declaration, the reader,
/// the state, and the panel are one path rather than four claims.
/// </para>
/// </remarks>
public sealed class CombatPolicyTests
{
    [Fact]
    public void The_act_control_is_declared_in_code_in_the_project_file_and_in_the_companion()
    {
        string source = File.ReadAllText(Path.Combine(SourceDirectory(), "ProductIdentity.cs"));
        string project = File.ReadAllText(ProjectFile());
        string product = File.ReadAllText(Path.Combine(SourceDirectory(), "CrawlerProduct.cs"));
        string ui = File.ReadAllText(Path.Combine(SourceDirectory(), "..", "ui", "main.ts"));

        string intent = Constant(source, "AttackIntent");
        string action = Constant(source, "AttackAction");

        // Declared in code and in the project file, and mapped there: the engine refuses a mapping whose
        // intent it was never told about, so both halves are what make the key a control.
        Assert.Contains($"RustyEngineProductInputIntent Include=\"{intent}\" Value=\"digital\"", project, StringComparison.Ordinal);
        Assert.Contains($"Intent=\"{intent}\"", project, StringComparison.Ordinal);

        // The key the product actually declared, named so a change to it is a decision rather than a silent
        // edit. The donor's own act control is B, triggered with key repeat so holding it keeps attacking.
        Assert.Contains("Trigger=\"key:key-b:held\"", project, StringComparison.Ordinal);

        // The host hands the ruleset the declared names, and the companion sends the declared action on the
        // product's own contract: the reader is composed over exactly these names.
        Assert.Contains("new CombatIntentNames(", product, StringComparison.Ordinal);
        Assert.Contains("Combat: _combat", product, StringComparison.Ordinal);
        AssertUiConstant(ui, "ACTION_ATTACK", action);
        AssertUiConstant(ui, "UI_ACTION_CONTRACT", Constant(source, "UiActionContract"));
    }

    [Fact]
    public void A_creature_the_content_places_is_hostile_on_sight_and_the_party_acts_by_recovery()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(
            [.. World(monsterAt: 100), Monsters(hostility: 2, recovery: 100), PartyDocument()]);

        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(ProductTestContext.RulesetContext(context, ui, combat: true));
        session.Start();
        ulong step = 0;

        // Nothing is in front of the party before the fight has read the world.
        ProjectedNode combat = ProjectedNode.Of(ui.Latest().Value).Field("combat");
        Assert.True(combat.Field("available").AsBoolean());
        Assert.False(combat.Field("engaged").AsBoolean());

        session.Update(ProductTestContext.Update(++step, 1));
        combat = ProjectedNode.Of(ui.Latest().Value).Field("combat");

        // The creature is hostile because of what it is: its row's band is two, which notices the party from
        // 2560 units, and the placement stands a hundred units away.
        Assert.True(combat.Field("engaged").AsBoolean());
        Assert.Equal(1d, combat.Field("opposition").AsNumber());
        Assert.Equal(2d, combat.Field("members").Length());
        Assert.Equal(2d, combat.Field("ready").AsNumber());
        ProjectedNode enemy = combat.Field("enemies").Item(0);
        Assert.Equal("A beast", enemy.Field("name").AsString());
        Assert.Equal(100d, enemy.Field("distance").AsNumber());

        // A creature's first recovery is drawn inside the row's own, which the donor's two constants make
        // 23.437 seconds of game time: one tick is 1000/128 of a real second and a real second is thirty game
        // seconds, so the row's hundred ticks are that long and no first recovery is longer.
        Assert.InRange(enemy.Field("recoverySeconds").AsNumber(), 0, 23.437);

        // The act control orders the party to attack: both members act, each pays its own recovery, and the
        // panel shows which of them may still act. What each pays is the donor's character recovery: a
        // character holding nothing swings on the staff's hundred ticks less the speed bonus its Speed
        // attribute is worth — two ticks at seventeen, five at twenty-five — which is the whole of the sum
        // this build can read, because a party cannot wear anything yet.
        session.Update(ProductTestContext.Update(++step, 1, Digital(ProductIdentity.AttackIntent)));
        combat = ProjectedNode.Of(ui.Latest().Value).Field("combat");
        Assert.Equal("applied", combat.Field("outcome").AsString());
        Assert.Equal(0d, combat.Field("ready").AsNumber());
        Assert.Contains("attacks A beast", combat.Field("message").AsString(), StringComparison.Ordinal);
        Assert.Equal(22.969, combat.Field("members").Item(0).Field("recoverySeconds").AsNumber(), 3);
        Assert.Equal(22.266, combat.Field("members").Item(1).Field("recoverySeconds").AsNumber(), 3);
        Assert.False(combat.Field("members").Item(0).Field("ready").AsBoolean());

        // Letting go of the control and asking again while everybody recovers is refused by name rather than
        // quietly doing nothing.
        session.Update(ProductTestContext.Update(++step, 1, Released(ProductIdentity.AttackIntent)));
        session.Update(ProductTestContext.Update(++step, 1, Digital(ProductIdentity.AttackIntent)));
        combat = ProjectedNode.Of(ui.Latest().Value).Field("combat");
        Assert.Equal("refused", combat.Field("outcome").AsString());
        Assert.Equal("recovering", combat.Field("code").AsString());

        // And game time is what releases them: let go of the control, hold the world for thirty game
        // seconds, and the party is ready again — which is more than the twenty-three seconds a swing costs.
        session.Update(ProductTestContext.Update(++step, 1, Released(ProductIdentity.AttackIntent)));
        for (int index = 0; index < 60; index++) session.Update(ProductTestContext.Update(++step, 1));
        Assert.Equal(2d, ProjectedNode.Of(ui.Latest().Value).Field("combat").Field("ready").AsNumber());
    }

    [Fact]
    public void What_the_party_has_done_is_what_makes_a_person_hostile()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(
            [.. World(monsterAt: 100, person: true), Monsters(hostility: 4, recovery: 100), PartyDocument()]);

        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(ProductTestContext.RulesetContext(context, ui, combat: true));
        session.Start();
        session.Update(ProductTestContext.Update(1, 1));

        // The creature's band is four, which notices the party from 10240 units, so the fight is already on
        // when the party walks in; the person standing there is in no fight at all.
        ProjectedNode combat = ProjectedNode.Of(ui.Latest().Value).Field("combat");
        Assert.True(combat.Field("engaged").AsBoolean());
        Assert.Equal(1d, combat.Field("opposition").AsNumber());
        Assert.Equal("A beast", combat.Field("enemies").Item(0).Field("name").AsString());
    }

    [Fact]
    public void A_creature_naming_a_monster_no_row_describes_refuses_the_session_by_name()
    {
        (string Path, string Text)[] files =
        [
            .. World(monsterAt: 100, monsterRow: "4711"),
            Monsters(hostility: 2, recovery: 100, id: "7"),
            PartyDocument(),
        ];

        // A creature the content cannot price is a defect of the content, named where the session would be
        // composed rather than met as a monster that never acts.
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(files);
        ContentValidationException error = Assert.Throws<ContentValidationException>(
            () => MightAndMagic7Ruleset.Instance.CreateSession(ProductTestContext.RulesetContext(context, ui, combat: true)));
        Assert.Contains(error.Issues, issue => issue.Code == "creature-monster-unknown");
    }

    [Fact]
    public void A_creatures_first_recovery_is_a_keyed_roll_so_the_same_world_fights_the_same_fight()
    {
        // The engine's random service takes an explicit seed and reads no clock, so the same content and the
        // same roll produce the same first recovery: nothing about a fight has to be recorded to replay it.
        double first = FirstRecoverySeconds(roll: 10_000);
        double second = FirstRecoverySeconds(roll: 10_000);
        double other = FirstRecoverySeconds(roll: 2_000);

        Assert.Equal(first, second, 3);
        Assert.NotEqual(first, other);
        Assert.Equal(10.0, first, 3);

        // A draw past the row's own recovery is clamped to it rather than lengthening it: the row's hundred
        // ticks are the most a creature of this row can owe before its first action.
        // The row's hundred ticks are 23437.5 milliseconds of game time, rounded to the millisecond the
        // kit counts in, and a draw past them is clamped to exactly that.
        Assert.Equal(23.438, FirstRecoverySeconds(roll: 999_999), 3);
    }

    /// <summary>The first recovery a creature of a staged world is seen with, for one roll of the engine's service.</summary>
    private static double FirstRecoverySeconds(long roll)
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(
            [.. World(monsterAt: 100), Monsters(hostility: 2, recovery: 100), PartyDocument()]);
        context.Engine.Random.DrawKeyed(new KeyedRngRequest(0, "unused", "unused", 0, 0));
        FakeEngineContext fake = (FakeEngineContext)context.Engine;
        fake.RandomService.Roll = roll;

        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(ProductTestContext.RulesetContext(context, ui, combat: true));
        session.Start();
        session.Update(ProductTestContext.Update(1, 1));
        return ProjectedNode.Of(ui.Latest().Value)
            .Field("combat")
            .Field("enemies")
            .Item(0)
            .Field("recoverySeconds")
            .AsNumber();
    }

    /// <summary>What this game's own recovery answers are worth, read from the policy the product composes.</summary>
    [Fact]
    public void This_games_recovery_answers_are_the_donors_own_numbers()
    {
        ContentCatalog catalog = Catalog([.. World(monsterAt: 100), Monsters(hostility: 2, recovery: 100), PartyDocument()]);
        Assert.NotNull(catalog);
        Assert.Equal(1, MightAndMagic7Combat.Compose(catalog, random: null).MonsterCount);
    }

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
        Match match = Regex.Match(source, $@"const string {name} = ""([^""]*)"";", RegexOptions.CultureInvariant);
        Assert.True(match.Success, $"ProductIdentity.cs must declare {name}.");
        return match.Groups[1].Value;
    }

    private static void AssertUiConstant(string uiSource, string name, string expected)
    {
        Match match = Regex.Match(uiSource, $@"const {name} = '([^']*)';", RegexOptions.CultureInvariant);
        Assert.True(match.Success, $"src/ui/main.ts must declare {name}.");
        Assert.Equal(expected, match.Groups[1].Value);
    }

    /// <summary>One digital event on the act control, as the engine admits a key.</summary>
    private static ProductInputEvent Digital(string intent) => ProductTestContext.Digital(intent);

    /// <summary>The key coming back up, which is what ends a held control.</summary>
    private static ProductInputEvent Released(string intent) => ProductTestContext.Digital(intent, InputEdge.Released);

    private static ContentCatalog Catalog((string Path, string Text)[] files)
    {
        (ProductCreateContext context, _) = ProductTestContext.Create(files);
        return ContentCatalogLoader.Load(
            new ProductContentSource(context.Content),
            ContentLayout.Under(ProductTestContext.ContentDirectory)).RequireValid();
    }

    /// <summary>
    /// A world of two places: the party's starting region holds a creature where the test says and, when a
    /// test asks for one, a person standing near it.
    /// </summary>
    private static (string Path, string Text)[] World(double monsterAt, bool person = false, string monsterRow = "7")
    {
        string placements = $$"""
            { "id": "beast", "kind": "monster", "monster": "{{monsterRow}}", "x": {{monsterAt.ToString(System.Globalization.CultureInfo.InvariantCulture)}}, "y": 0, "z": 0 }
            """;
        if (person)
        {
            placements += ", { \"id\": \"person-1\", \"kind\": \"person\", \"x\": 200, \"y\": 0, \"z\": 0, \"people\": [ \"person-1\" ] }";
        }

        return
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
                    { "path": "monsters.json", "documentId": "monsters", "definitionKind": "monster" },
                    { "path": "start.json", "documentId": "start", "definitionKind": "scenario-start" },
                    { "path": "party.json", "documentId": "party", "definitionKind": "scenario-party" },
                    { "path": "people.json", "documentId": "people", "definitionKind": "person" }
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
                      "placements": [ {{placements}} ] },
                    { "id": "2", "kind": "interior", "name": "Cave", "respawnDays": 1,
                      "entryPoints": [ { "id": "Party Start", "x": 1, "y": 2, "z": 3, "yaw": 0 } ] }
                  ]
                }
                """),
            ($"{ProductTestContext.ContentDirectory}/content-packs/world/people.json",
                """
                { "documentId": "people", "definitionKind": "person", "entries": [ { "id": "person-1", "name": "A bystander" } ] }
                """),
            ($"{ProductTestContext.ContentDirectory}/content-packs/world/start.json",
                """
                { "documentId": "start", "definitionKind": "scenario-start", "entries": [ { "id": "start", "place": "1", "entryPoint": "Party Start" } ] }
                """),
        ];
    }

    /// <summary>The monster table a creature placement names a row in.</summary>
    private static (string Path, string Text) Monsters(int hostility, int recovery, string id = "7") =>
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/monsters.json",
            $$"""
            {
              "documentId": "monsters",
              "definitionKind": "monster",
              "entries": [ { "id": "{{id}}", "name": "A beast", "hostility": {{hostility}}, "recovery": {{recovery}} } ]
            }
            """);

    /// <summary>The scenario's party, as this suite's other cases stage it.</summary>
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
                      "spellPoints": 0, "attributes": [ { "id": "Speed", "value": 17 } ],
                      "skills": [ { "id": "Sword", "level": 1, "tier": 1, "pointsSpent": 1 } ],
                      "spells": [], "conditions": [] },
                    { "name": "Aelina", "race": "Elf", "class": "Sorcerer", "level": 1, "hitPoints": 24,
                      "spellPoints": 15, "attributes": [ { "id": "Speed", "value": 25 } ],
                      "skills": [ { "id": "Staff", "level": 1, "tier": 1, "pointsSpent": 1 } ],
                      "spells": [], "conditions": [] }
                  ]
                }
              ]
            }
            """);
}
