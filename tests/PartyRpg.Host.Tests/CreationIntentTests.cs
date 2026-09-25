using System.Text.RegularExpressions;
using System.Xml.Linq;
using PartyRpg.Kit.Sessions;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Host.Tests;

/// <summary>
/// How creation reaches the product: the intents the host declares in code and in the project file, and a
/// creation choice travelling from admitted input through the product into this game's flow.
/// </summary>
/// <remarks>
/// The engine learns the admitted intent names from the project file and rejects a mapping whose intent the
/// product never declared, so a name that exists in code alone is a control that silently does nothing.
/// The architecture suite checks the product identity it knows about; this case checks the creation controls
/// in both directions, because they are the ones a screen depends on and the newest.
/// </remarks>
public sealed class CreationIntentTests
{
    [Fact]
    public void Every_creation_intent_the_code_declares_is_declared_and_mapped_in_the_project_file()
    {
        string project = ProjectFile();
        string text = File.ReadAllText(project);
        string source = File.ReadAllText(Path.Combine(SourceDirectory(), "ProductIdentity.cs"));

        string[] declaredInCode =
        [
            Constant(source, "CreationAdvanceIntent"),
            Constant(source, "CreationAcceptIntent"),
        ];

        foreach (string intent in declaredInCode)
        {
            Assert.Contains($"RustyEngineProductInputIntent Include=\"{intent}\" Value=\"digital\"", text, StringComparison.Ordinal);
            Assert.Contains($"Intent=\"{intent}\"", text, StringComparison.Ordinal);
        }

        // And the other direction: every creation intent the project file declares is one this code names, so
        // a mapping added there alone cannot be a key that presses nothing.
        string[] declaredInProject =
        [
            .. XDocument.Load(project).Descendants("RustyEngineProductInputIntent")
                .Select(element => (string?)element.Attribute("Include") ?? string.Empty)
                .Where(intent => intent.StartsWith("creation.", StringComparison.Ordinal)),
        ];
        Assert.Equal([.. declaredInCode.Order(StringComparer.Ordinal)], [.. declaredInProject.Order(StringComparer.Ordinal)]);

        // Each one is mapped to a keyboard control, and the engine's own names are used: the product refuses
        // to start on an unsupported mapping, so a typo is a startup failure rather than a dead key.
        Assert.Contains("key:enter:pressed", text, StringComparison.Ordinal);
        Assert.Contains("key:space:pressed", text, StringComparison.Ordinal);

        // The screen's choices arrive on the payload channel the product declares. Its name and contract are
        // the ones the DOM companion claims and the ones the ruleset composes the creation reader over.
        Assert.Contains($"payload:{Constant(source, "UiActionContract")}", text, StringComparison.Ordinal);
        Assert.Equal(Constant(source, "UiActionContract"), new CreationIntentNames("a", "b", Constant(source, "UiActionContract")).ActionContract);
    }

    [Fact]
    public void A_creation_choice_travels_from_admitted_input_into_the_flow_and_a_refusal_comes_back()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create();

        using CrawlerProduct product = new(context);
        product.Start();
        Assert.Equal(SessionMode.Creating, product.Mode);

        // The default party creation opens on is finished, so changing one member begins by reopening it:
        // the screen's own member button, on the product's payload contract.
        product.Update(ProductTestContext.Update(1, 1, ProductTestContext.Payload("""{"action":"creation.select-member","member":0}""")));
        Assert.Equal("portrait", ProjectedNode.Of(ui.Latest().Value).Field("creation").Field("roster").Element(0).Field("step").AsString());

        // A choice a player clicked: the portrait, which is what decides the character's race. The flow
        // draws its race, and the screen is told what the flow decided.
        product.Update(ProductTestContext.Update(2, 1, ProductTestContext.Payload("""{"action":"creation.select-portrait","portrait":"elf-woman"}""")));

        ProjectedNode creation = ProjectedNode.Of(ui.Latest().Value).Field("creation");
        Assert.Equal("Elf", creation.Field("roster").Element(0).Field("race").AsString());
        Assert.Equal("elf-woman", creation.Field("roster").Element(0).Field("portrait").AsString());
        Assert.True(creation.Field("portraits").Element(2).Field("selected").AsBoolean());
        Assert.Equal(string.Empty, creation.Field("refusalCode").AsString());

        // An illegal choice is refused by name and the session stays in creation: the rule the flow broke is
        // what the product publishes, not an exception inside an admitted update.
        product.Update(ProductTestContext.Update(3, 1, ProductTestContext.Payload("""{"action":"creation.select-portrait","portrait":"nobody"}""")));

        creation = ProjectedNode.Of(ui.Latest().Value).Field("creation");
        Assert.Equal("portrait-unknown", creation.Field("refusalCode").AsString());
        Assert.Contains("is not a portrait creation offers", creation.Field("refusalMessage").AsString(), StringComparison.Ordinal);
        Assert.Equal(SessionMode.Creating, product.Mode);

        // Accepting a party whose member was reopened and left unfinished is refused with the member and the
        // step that are unfinished, so the screen can show what is left to do.
        product.Update(ProductTestContext.Update(4, 1, ProductTestContext.Digital(ProductIdentity.CreationAcceptIntent)));

        creation = ProjectedNode.Of(ui.Latest().Value).Field("creation");
        Assert.Equal("creation-incomplete", creation.Field("refusalCode").AsString());
        Assert.Contains("member 1 is at the Portrait step", creation.Field("refusalMessage").AsString(), StringComparison.Ordinal);
        Assert.Equal("creating", ProjectedNode.Of(ui.Latest().Value).Field("session").Field("mode").AsString());
    }

    [Fact]
    public void Accepting_the_finished_party_leaves_creation_for_the_world_through_the_product()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create();

        using CrawlerProduct product = new(context);
        product.Start();

        // The declared accept control finishes a new game: the running product is playing the party it
        // created, and the panel's accepted list is that party's own members.
        product.Update(ProductTestContext.Update(1, 1, ProductTestContext.Digital(ProductIdentity.CreationAcceptIntent)));

        ProjectedNode value = ProjectedNode.Of(ui.Latest().Value);
        Assert.Equal(SessionMode.Running, product.Mode);
        Assert.False(value.Field("creation").Field("active").AsBoolean());
        Assert.True(value.Field("creation").Field("accepted").AsBoolean());
        Assert.Equal(4d, value.Field("creation").Field("party").Count());
        Assert.Equal("Roderick", value.Field("creation").Field("party").Element(0).Field("name").AsString());
        Assert.Equal("human-man", value.Field("creation").Field("party").Element(0).Field("portrait").AsString());
        Assert.True(value.Field("party").Field("present").AsBoolean());
        Assert.Equal(4d, value.Field("party").Field("members").AsNumber());

        // And the world steps once it is playing: the update after the accept is the first one measured.
        product.Update(ProductTestContext.Update(2, 60));
        Assert.Equal(60d, ProjectedNode.Of(ui.Latest().Value).Field("session").Field("admittedSteps").AsNumber());
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
