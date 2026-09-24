using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace PartyRpg.Architecture.Tests;

/// <summary>
/// The ownership laws, checked mechanically. Each failure names the boundary it protects, because a
/// law that only prints a path leaves the next agent to guess why the boundary exists.
/// </summary>
public sealed class ArchitectureLawTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void Kit_does_not_contain_ruleset_or_donor_vocabulary()
    {
        // Code and project configuration only: the kit's own README explains this boundary by name,
        // and a rule that forbids explaining itself is not a boundary, it is a trap.
        string kit = Path.Combine(RepositoryRoot, "src", "PartyRpg.Kit");
        string[] files = [.. Directory.EnumerateFiles(kit, "*.cs", SearchOption.AllDirectories),
            .. Directory.EnumerateFiles(kit, "*.csproj", SearchOption.AllDirectories)];
        foreach (string file in files)
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            string text = File.ReadAllText(file);
            foreach (string forbidden in ForbiddenKitVocabulary)
            {
                Assert.False(
                    text.Contains(forbidden, StringComparison.OrdinalIgnoreCase),
                    $"The kit must not name the ruleset, its world, its donors, or its source files, but {Path.GetFileName(file)} contains '{forbidden}'.");
            }
        }
    }

    [Fact]
    public void Project_references_follow_the_dependency_graph()
    {
        AssertProjectReferences("PartyRpg.Kit", []);
        AssertProjectReferences("PartyRpg.Rulesets.MightAndMagic7", ["PartyRpg.Kit"]);
        AssertProjectReferences("PartyRpg.Host", ["PartyRpg.Kit", "PartyRpg.Rulesets.MightAndMagic7"]);
    }

    [Fact]
    public void Exactly_one_project_declares_a_product_entry_type()
    {
        List<string> declaring = [];
        foreach (string project in SourceProjects())
        {
            string entryType = PropertyValue(project, "RustyEngineProductEntryType");
            if (!string.IsNullOrWhiteSpace(entryType)) declaring.Add(Path.GetFileName(project));
        }

        Assert.Equal(["PartyRpg.Host.csproj"], declaring);
    }

    [Fact]
    public void Host_product_identity_matches_the_declared_msbuild_properties()
    {
        string project = ProjectFile("PartyRpg.Host");
        string source = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "PartyRpg.Host", "ProductIdentity.cs"));

        Assert.Equal(ConstantValue(source, "Id"), PropertyValue(project, "RustyEngineProductId"));
        Assert.Equal(ConstantValue(source, "Title"), PropertyValue(project, "RustyEngineProductTitle"));
        Assert.Equal(ConstantValue(source, "UiStream"), PropertyValue(project, "RustyEngineProductUiProjectionStream"));
        Assert.Equal(ConstantValue(source, "UiContract"), PropertyValue(project, "RustyEngineProductUiProjectionContract"));
        Assert.Contains($"payload:{ConstantValue(source, "UiActionContract")}", File.ReadAllText(project), StringComparison.Ordinal);
        Assert.Contains(ConstantValue(source, "PauseToggleIntent"), File.ReadAllText(project), StringComparison.Ordinal);
    }

    [Fact]
    public void Ui_companion_declares_the_same_contract_as_the_host()
    {
        string source = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "PartyRpg.Host", "ProductIdentity.cs"));
        string ui = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "ui", "main.ts"));

        AssertUiConstant(ui, "UI_CONTRACT", ConstantValue(source, "UiContract"));
        AssertUiConstant(ui, "UI_ACTION_INTENT", ConstantValue(source, "UiActionIntent"));
        AssertUiConstant(ui, "UI_ACTION_CONTRACT", ConstantValue(source, "UiActionContract"));
    }

    private static readonly string[] ForbiddenKitVocabulary =
    [
        "MightAndMagic",
        "Might and Magic",
        "MM6",
        "MM7",
        "MM8",
        "Enroth",
        "Erathia",
        "Harmondale",
        "Emerald Isle",
        "OpenEnroth",
        "MMExtension",
        "OpenMM8",
        ".lod",
        ".odm",
        ".ddm",
        ".blv",
        ".dlv",
        "events.lod",
        "games.lod",
    ];

    private static void AssertUiConstant(string uiSource, string name, string expected)
    {
        Match match = Regex.Match(uiSource, $@"const {name} = '([^']*)';", RegexOptions.CultureInvariant);
        Assert.True(match.Success, $"src/ui/main.ts must declare {name}.");
        Assert.Equal(expected, match.Groups[1].Value);
    }

    private static string ConstantValue(string source, string name)
    {
        Match match = Regex.Match(source, $@"const string {name} = ""([^""]*)"";", RegexOptions.CultureInvariant);
        Assert.True(match.Success, $"ProductIdentity.cs must declare {name}.");
        return match.Groups[1].Value;
    }

    private static string PropertyValue(string projectFile, string property)
    {
        XDocument document = XDocument.Load(projectFile);
        return document.Descendants(property).FirstOrDefault()?.Value.Trim() ?? string.Empty;
    }

    private static void AssertProjectReferences(string projectName, string[] expected)
    {
        XDocument document = XDocument.Load(ProjectFile(projectName));
        string[] actual = [.. document.Descendants("ProjectReference")
            .Select(element => (string?)element.Attribute("Include") ?? string.Empty)
            .Select(include => Path.GetFileNameWithoutExtension(include))
            .Order(StringComparer.Ordinal)];

        Assert.Equal([.. expected.Order(StringComparer.Ordinal)], actual);
    }

    private static IEnumerable<string> SourceProjects() =>
        Directory.EnumerateFiles(Path.Combine(RepositoryRoot, "src"), "*.csproj", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    private static string ProjectFile(string projectName) =>
        Path.Combine(RepositoryRoot, "src", projectName, $"{projectName}.csproj");

    private static string FindRepositoryRoot()
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
}
