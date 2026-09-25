using System.Text.RegularExpressions;
using System.Xml.Linq;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using PartyRpg.Rulesets.MightAndMagic7;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Host.Tests;

/// <summary>
/// How a save reaches the product, and how a run is told to resume one: the two declarations the engine
/// checks, the companion's own copy of the action name, and the start switch that decides a new game from a
/// continued one.
/// </summary>
/// <remarks>
/// The engine learns the admitted intent names from the project file and rejects a mapping whose intent the
/// product never declared, so a name that exists in code alone is a control that silently does nothing.
/// This case checks the save controls in both directions, for the same reason the creation controls are
/// checked: they are the ones a player presses and the newest.
/// </remarks>
public sealed class SaveIntentTests
{
    [Fact]
    public void The_save_controls_are_declared_in_code_in_the_project_file_and_in_the_companion()
    {
        string source = File.ReadAllText(Path.Combine(SourceDirectory(), "ProductIdentity.cs"));
        string project = File.ReadAllText(ProjectFile());
        string ui = File.ReadAllText(Path.Combine(SourceDirectory(), "..", "ui", "main.ts"));

        string intent = Constant(source, "SaveIntent");
        string action = Constant(source, "SaveAction");

        // Declared in code and in the project file, and mapped there: the engine refuses a mapping whose
        // intent was never declared, so both halves are what make the key a control.
        Assert.Contains($"RustyEngineProductInputIntent Include=\"{intent}\" Value=\"digital\"", project, StringComparison.Ordinal);
        Assert.Contains($"Intent=\"{intent}\"", project, StringComparison.Ordinal);

        // The engine's keyboard controls carry no function keys — the enum ends at Escape, Shift, Control,
        // and Alt — so the save control is a letter rather than the F5 a person might expect. This names the
        // key the product actually declared so a change to it is a decision rather than a silent edit.
        Assert.Contains("Trigger=\"key:key-f:pressed\"", project, StringComparison.Ordinal);

        // The other direction: every session intent the project file declares is one this code names, so a
        // mapping added there alone cannot be a key that presses nothing.
        string[] declaredInProject =
        [
            .. XDocument.Load(ProjectFile()).Descendants("RustyEngineProductInputIntent")
                .Select(element => (string?)element.Attribute("Include") ?? string.Empty)
                .Where(name => name.StartsWith("session.", StringComparison.Ordinal))
                .Order(StringComparer.Ordinal),
        ];
        Assert.Equal(
            new[] { Constant(source, "PauseToggleIntent"), intent }.Order(StringComparer.Ordinal),
            declaredInProject);

        // The payload action the DOM companion sends is the product's own action name on the product's own
        // contract, and the session's reader is composed over exactly those names.
        string contract = Constant(source, "UiActionContract");
        Assert.Contains($"const ACTION_SAVE = '{action}';", ui, StringComparison.Ordinal);
        Assert.Contains($"const UI_ACTION_CONTRACT = '{contract}';", ui, StringComparison.Ordinal);

        SaveIntentNames names = new(intent, action, contract);
        Assert.Equal(intent, names.Intent);
        Assert.Equal(action, names.Action);
        Assert.Equal(contract, names.ActionContract);
    }

    [Fact]
    public void A_run_told_to_resume_with_nothing_saved_fails_by_name()
    {
        InMemoryPersistenceService persistence = new();
        (ProductCreateContext context, RecordingUiService _) = ProductTestContext.Create(
            persistence,
            ProductTestContext.CreationTables());

        // The switch is the operator's, so a slot that holds nothing stops the run with the loss named
        // rather than starting a new expedition in place of the one that was asked for.
        SessionSaveException refused = Assert.Throws<SessionSaveException>(() =>
        {
            using CrawlerProduct product = new(context, BuiltInRulesets.Default, bundleId: null, start: SessionStart.Resume);
        });

        Assert.Contains($"No session is saved in slot '{MightAndMagic7Persistence.SaveSlot}'", refused.Message, StringComparison.Ordinal);
        Assert.Null(persistence.Payload(MightAndMagic7Persistence.StoreScope, MightAndMagic7Persistence.SaveSlot));
    }

    [Fact]
    public void A_start_switch_that_names_neither_start_is_refused_with_the_value_it_read()
    {
        // The product's two words mean what they say, and an empty or absent switch is a new game.
        Assert.Equal(SessionStart.Fresh, ProductStart.Parse(null));
        Assert.Equal(SessionStart.Fresh, ProductStart.Parse(""));
        Assert.Equal(SessionStart.Fresh, ProductStart.Parse("fresh"));
        Assert.Equal(SessionStart.Resume, ProductStart.Parse("resume"));
        Assert.Equal(SessionStart.Resume, ProductStart.Parse("  Resume  "));

        // Anything else stops the run with the value it read, rather than being rounded to one of the two:
        // an operator who typed a switch wrong must not be handed a new game when they asked to continue.
        InvalidOperationException refused = Assert.Throws<InvalidOperationException>(() => ProductStart.Parse("continue"));
        Assert.Contains("'continue'", refused.Message, StringComparison.Ordinal);
        Assert.Contains(ProductIdentity.StartVariable, refused.Message, StringComparison.Ordinal);

        // And the switch the product reads is the declared environment variable rather than an implicit one.
        Assert.Equal(
            ProductStart.Parse(Environment.GetEnvironmentVariable(ProductIdentity.StartVariable)),
            ProductStart.FromEnvironment());
    }

    private static string SourceDirectory()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "src", "PartyRpg.Host");
            if (Directory.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find the host project above the test assembly.");
    }

    private static string ProjectFile() => Path.Combine(SourceDirectory(), "PartyRpg.Host.csproj");

    private static string Constant(string source, string name)
    {
        Match match = Regex.Match(source, $@"const string {name} = ""([^""]*)"";", RegexOptions.CultureInvariant);
        Assert.True(match.Success, $"ProductIdentity.cs must declare {name}.");
        return match.Groups[1].Value;
    }
}
