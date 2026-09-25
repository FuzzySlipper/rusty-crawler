using System.Text.RegularExpressions;
using System.Xml.Linq;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Host.Tests;

/// <summary>
/// How a use reaches the product: the declaration the engine checks in both directions, the companion's own
/// copy of the action name, and the whole trip from a pressed key to what the panel reports.
/// </summary>
/// <remarks>
/// The engine learns the admitted intent names from the project file and rejects a mapping whose intent the
/// product never declared, so a name that exists in code alone is a control that silently does nothing. This
/// case checks the use controls the way the save and creation controls are checked, and then presses the key
/// at a door in a staged world so the declaration is proved by a use rather than by a string.
/// </remarks>
public sealed class UseIntentTests
{
    [Fact]
    public void The_use_controls_are_declared_in_code_in_the_project_file_and_in_the_companion()
    {
        string source = File.ReadAllText(Path.Combine(SourceDirectory(), "ProductIdentity.cs"));
        string project = File.ReadAllText(ProjectFile());
        string product = File.ReadAllText(Path.Combine(SourceDirectory(), "CrawlerProduct.cs"));
        string ui = File.ReadAllText(Path.Combine(SourceDirectory(), "..", "ui", "main.ts"));

        string intent = Constant(source, "UseIntent");
        string action = Constant(source, "UseAction");

        // Declared in code and in the project file, and mapped there: both halves are what make the key a
        // control, because the engine refuses a mapping whose intent was never declared.
        Assert.Contains($"RustyEngineProductInputIntent Include=\"{intent}\" Value=\"digital\"", project, StringComparison.Ordinal);
        Assert.Contains($"Intent=\"{intent}\"", project, StringComparison.Ordinal);

        // The key the product actually declared, named so a change to it is a decision rather than a silent
        // edit. The original's interaction key is Space and its jump key is X; Space already jumps here.
        Assert.Contains("Trigger=\"key:key-g:pressed\"", project, StringComparison.Ordinal);

        // The host hands the ruleset the declared names, and the companion sends the declared action on the
        // product's own contract: the reader is composed over exactly these names.
        Assert.Contains("new UseIntentNames(", product, StringComparison.Ordinal);
        Assert.Contains("Use: _use", product, StringComparison.Ordinal);
        AssertUiConstant(ui, "ACTION_USE", action);
        AssertUiConstant(ui, "UI_ACTION_CONTRACT", Constant(source, "UiActionContract"));
    }

    [Fact]
    public void The_use_key_opens_a_door_in_a_staged_world_and_the_panel_reports_it()
    {
        // A world with one interior whose door stands in front of the scenario's starting point, and the
        // creation tables the product needs to make its party before the world is composed over it.
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(
        [
            ProductTestContext.Bundle("partyrpg-default", "world", "creation-tables"),
            .. ProductTestContext.CreationTables(),
            ($"{ProductTestContext.ContentDirectory}/content-packs/world/pack.json",
                """
                {
                  "schemaVersion": 1,
                  "packId": "world",
                  "kind": "definitions",
                  "provenance": { "description": "authored for a test" },
                  "documents": [
                    { "path": "places.json", "documentId": "places", "definitionKind": "place" },
                    { "path": "start.json", "documentId": "start", "definitionKind": "scenario-start" }
                  ]
                }
                """),
            ($"{ProductTestContext.ContentDirectory}/content-packs/world/places.json",
                """
                {
                  "documentId": "places",
                  "definitionKind": "place",
                  "entries": [
                    { "id": "52", "kind": "interior", "name": "The Dragon's Lair", "respawnDays": 7,
                      "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
                      "placements": [
                        { "id": "door-0", "kind": "door", "sourceField": "doors", "sourceIndex": 0,
                          "x": 100, "y": 0, "z": 0, "positionSource": "vertexIds", "doorId": 1,
                          "state": 2, "attributes": 1, "moveLength": 96, "openSpeed": 250, "closeSpeed": 250 } ] }
                  ]
                }
                """),
            ($"{ProductTestContext.ContentDirectory}/content-packs/world/start.json",
                """
                { "documentId": "start", "definitionKind": "scenario-start", "entries": [ { "id": "start", "place": "52", "entryPoint": "Party Start" } ] }
                """),
        ]);

        using CrawlerProduct product = new(context);
        product.Start();

        // The party is created first: the world this session plays is composed over the party when creation
        // is accepted, which is also when the interaction mechanism exists.
        product.Update(ProductTestContext.Update(1, 1, ProductTestContext.Digital(ProductIdentity.CreationAcceptIntent)));
        product.Attach();

        ProjectedNode world = ProjectedNode.Of(ui.Latest().Value).Field("world");
        Assert.Equal("52", world.Field("place").AsString());
        ProjectedNode before = ProjectedNode.Of(ui.Latest().Value).Field("interaction");
        Assert.True(before.Field("available").AsBoolean());
        Assert.Equal("none", before.Field("outcome").AsString());

        // The world's own admitted update faces the door the party stands in front of and reports the door's
        // own state, and the press in the next update is what uses it.
        product.Update(ProductTestContext.Update(2, 1));
        Assert.Equal("A door", ProjectedNode.Of(ui.Latest().Value).Field("interaction").Field("label").AsString());

        product.Update(ProductTestContext.Update(3, 1, ProductTestContext.Digital(ProductIdentity.UseIntent)));
        ProjectedNode applied = ProjectedNode.Of(ui.Latest().Value).Field("interaction");
        Assert.Equal("applied", applied.Field("outcome").AsString());
        Assert.Equal("open", applied.Field("state").AsString());
        Assert.Contains("swings open", applied.Field("message").AsString(), StringComparison.Ordinal);
        // The passage this build cannot deliver is stated beside the success rather than left implied.
        Assert.Contains("cannot be walked through yet", applied.Field("residue").AsString(), StringComparison.Ordinal);

        // The same door used again is refused with its own reason rather than doing nothing quietly, and the
        // refusal is part of the projection the press arrived in.
        product.Update(ProductTestContext.Update(4, 1, ProductTestContext.Digital(ProductIdentity.UseIntent)));
        ProjectedNode refused = ProjectedNode.Of(ui.Latest().Value).Field("interaction");
        Assert.Equal("refused", refused.Field("outcome").AsString());
        Assert.Equal("door-already-open", refused.Field("code").AsString());
        Assert.Contains("already stands open", refused.Field("message").AsString(), StringComparison.Ordinal);
    }

    private static string SourceDirectory() =>
        Path.Combine(RepositoryRoot(), "src", "PartyRpg.Host");

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
}
