using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace PartyRpg.Architecture.Tests;

/// <summary>
/// The ownership laws over the project graph, checked mechanically. Each failure names the boundary it protects,
/// because a law that only prints a path leaves the next agent to guess why the boundary exists.
/// </summary>
/// <remarks>
/// Every file these laws read is found through <see cref="Repository.Files"/>, which never enters build output,
/// installed packages, the operator's local evidence, or an agent's worktrees under <c>.claude/</c>, so a second
/// checkout beside this one is not a violation.
/// </remarks>
public sealed class ArchitectureLawTests
{
    /// <summary>Every project the repository builds, by name, with the peers it may reference.</summary>
    private static readonly Dictionary<string, string[]> ProjectReferences = new(StringComparer.Ordinal)
    {
        ["PartyRpg.Kit"] = [],
        ["PartyRpg.Rulesets.MightAndMagic7"] = ["PartyRpg.Kit"],
        ["PartyRpg.Host"] = ["PartyRpg.Kit", "PartyRpg.Rulesets.MightAndMagic7"],
        // The importer is offline tooling: it reads the operator's game data and writes packs, and it is outside
        // the runtime graph in both directions.
        ["MightAndMagic7.Import"] = [],
        ["MightAndMagic7.Import.Tool"] = ["MightAndMagic7.Import"],
        ["PortableExample"] = [],
        // The suites' shared support may know the kit, which every suite composes its fakes over, and nothing
        // above it: a helper that knew the ruleset would put this game's rules inside every suite.
        ["PartyRpg.Testing"] = ["PartyRpg.Kit"],
        ["PartyRpg.Architecture.Tests"] = ["PartyRpg.Testing"],
        ["PartyRpg.Kit.Tests"] = ["PartyRpg.Kit", "PartyRpg.Testing"],
        ["PartyRpg.Rulesets.MightAndMagic7.Tests"] = ["PartyRpg.Kit", "PartyRpg.Rulesets.MightAndMagic7", "PartyRpg.Testing"],
        ["PartyRpg.Host.Tests"] = ["PartyRpg.Host", "PartyRpg.Testing"],
        // Only a test may hold both the writer and the loader: this suite proves the packs the tool writes are
        // packs the kit loads and the ruleset composes a session over.
        ["MightAndMagic7.Import.Tests"] =
            ["MightAndMagic7.Import", "MightAndMagic7.Import.Tool", "PartyRpg.Kit", "PartyRpg.Rulesets.MightAndMagic7", "PartyRpg.Testing"],
    };

    /// <summary>The packages each project may take, beyond the peers above.</summary>
    private static readonly Dictionary<string, string[]> PackageReferences = new(StringComparer.Ordinal)
    {
        ["PartyRpg.Kit"] = ["Rusty.Engine"],
        ["PartyRpg.Rulesets.MightAndMagic7"] = ["Rusty.Engine"],
        ["PartyRpg.Host"] = ["Rusty.Engine"],
        ["MightAndMagic7.Import"] = [],
        ["MightAndMagic7.Import.Tool"] = [],
        ["PortableExample"] = ["Rusty.Engine"],
        ["PartyRpg.Testing"] = ["Microsoft.CodeAnalysis.CSharp", "xunit.assert", "xunit.extensibility.core"],
    };

    /// <summary>The packages every suite takes: the test platform and nothing else.</summary>
    private static readonly string[] SuitePackages = ["Microsoft.NET.Test.Sdk", "xunit", "xunit.runner.visualstudio"];

    /// <summary>Which assemblies each product project lets see its internals: its own suite, and nothing else.</summary>
    private static readonly Dictionary<string, string[]> Friends = new(StringComparer.Ordinal)
    {
        ["PartyRpg.Kit"] = [],
        // The ruleset's second friend is the host suite, which stages this game's creation tables from the
        // ruleset's own so a product it creates can make a party; the ruleset's AssemblyInfo says why.
        ["PartyRpg.Rulesets.MightAndMagic7"] = ["PartyRpg.Host.Tests", "PartyRpg.Rulesets.MightAndMagic7.Tests"],
        ["PartyRpg.Host"] = ["PartyRpg.Host.Tests"],
        ["MightAndMagic7.Import"] = [],
        ["MightAndMagic7.Import.Tool"] = ["MightAndMagic7.Import.Tests"],
        ["PortableExample"] = [],
    };

    [Fact]
    public void Every_project_references_only_the_peers_its_layer_allows()
    {
        // Every project is known to this law: one that appears without an entry is the failure mode it exists for.
        Assert.Equal(
            ProjectReferences.Keys.Order(StringComparer.Ordinal),
            AllProjects().Select(Name).Order(StringComparer.Ordinal));

        foreach (string project in AllProjects())
        {
            string[] actual = [.. Items(project, "ProjectReference").Select(Path.GetFileNameWithoutExtension).Order(StringComparer.Ordinal)!];
            Assert.True(
                ProjectReferences[Name(project)].Order(StringComparer.Ordinal).SequenceEqual(actual),
                $"{Name(project)} references [{string.Join(", ", actual)}]; its layer allows [{string.Join(", ", ProjectReferences[Name(project)])}].");
            AssertNoSmuggledReference(project);
        }
    }

    [Fact]
    public void Every_project_takes_only_the_packages_its_layer_allows()
    {
        // A package is a dependency as surely as a project is: a runtime project that took a second engine, a
        // reflection container, or a timer library would be coupled to something the graph never names.
        foreach (string project in AllProjects())
        {
            string[] expected = IsSuite(project) ? SuitePackages : PackageReferences[Name(project)];
            string[] actual = [.. Items(project, "PackageReference").Order(StringComparer.Ordinal)];
            Assert.True(
                expected.Order(StringComparer.Ordinal).SequenceEqual(actual),
                $"{Name(project)} takes the packages [{string.Join(", ", actual)}]; its layer allows [{string.Join(", ", expected)}].");
        }
    }

    [Fact]
    public void Only_a_projects_own_suite_may_see_its_internals()
    {
        // A friend declaration is a dependency the reference graph cannot see: an assembly that sees another's
        // internals is coupled to its implementation. It can be declared in the project file or in source, so both
        // are read — the source as syntax, so a comment that names an assembly is not a declaration.
        foreach (string project in AllProjects().Where(project => !IsUnderTests(project)))
        {
            string name = Name(project);
            string directory = Path.GetDirectoryName(project)!;
            List<string> declared = [.. Items(project, "InternalsVisibleTo")];
            foreach (string file in Repository.Files(Repository.Relative(directory), "*.cs"))
            {
                SyntaxNode root = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot();
                foreach (AttributeSyntax attribute in root.DescendantNodes().OfType<AttributeSyntax>())
                {
                    if (!attribute.Name.ToString().EndsWith("InternalsVisibleTo", StringComparison.Ordinal)) continue;
                    ExpressionSyntax argument = Assert.Single(attribute.ArgumentList!.Arguments).Expression;
                    Assert.True(argument is LiteralExpressionSyntax, $"{Repository.Relative(file)} names a friend by something other than a literal.");
                    declared.Add(((LiteralExpressionSyntax)argument).Token.ValueText);
                }
            }

            Assert.True(
                Friends[name].Order(StringComparer.Ordinal).SequenceEqual(declared.Order(StringComparer.Ordinal)),
                $"{name} lets [{string.Join(", ", declared)}] see its internals; only [{string.Join(", ", Friends[name])}] may.");
        }
    }

    [Fact]
    public void No_runtime_project_reaches_the_importer()
    {
        // No runtime project may reference the importer, and the importer may not reference a runtime project. An
        // import type reachable from the product would put source-shaped game data on a runtime path. The graph
        // above states this already; it is stated again here by what it protects, so relaxing the table cannot
        // relax it silently.
        foreach (string runtime in new[] { "PartyRpg.Kit", "PartyRpg.Rulesets.MightAndMagic7", "PartyRpg.Host" })
        {
            Assert.DoesNotContain(ProjectReferences[runtime], reference => reference.StartsWith("MightAndMagic7.Import", StringComparison.Ordinal));
        }

        Assert.DoesNotContain(ProjectReferences["MightAndMagic7.Import"], reference => reference.StartsWith("PartyRpg.", StringComparison.Ordinal));
        Assert.DoesNotContain(ProjectReferences["MightAndMagic7.Import.Tool"], reference => reference.StartsWith("PartyRpg.", StringComparison.Ordinal));
    }

    [Fact]
    public void Exactly_one_project_of_the_product_declares_a_product_entry_type()
    {
        // The product is what src/ builds; the portable-assets example under tools/ is a separate product of its own
        // that shows an asset consumer, and is not an entry into this one.
        string[] declaring =
        [
            .. Repository.Files("src", "*.csproj")
                .Where(project => !string.IsNullOrWhiteSpace(Property(project, "RustyEngineProductEntryType")))
                .Select(Name),
        ];

        Assert.Equal(["PartyRpg.Host"], declaring);
    }

    [Fact]
    public void Verify_script_covers_every_project_and_suite()
    {
        string script = Repository.Read("scripts", "verify.sh");

        // Every project outside the suites is built, including the tools beside the product, so a project
        // nobody builds cannot sit in the tree looking verified.
        Assert.Equal(
            AllProjects().Where(project => !IsUnderTests(project)).Select(Repository.Relative).Order(StringComparer.Ordinal),
            DeclaredList(script, "product_projects").Order(StringComparer.Ordinal));

        // A suite is a project that brings the test platform; a project under tests/ that does not is support the
        // suites share, which the script builds on its own so a break in it is reported as its own.
        string[] suites = [.. AllProjects().Where(IsSuite).Select(Repository.Relative).Order(StringComparer.Ordinal)];
        Assert.NotEmpty(suites);
        Assert.Equal(suites, DeclaredList(script, "test_projects").Order(StringComparer.Ordinal));
        Assert.Equal(
            AllProjects().Where(project => IsUnderTests(project) && !IsSuite(project)).Select(Repository.Relative).Order(StringComparer.Ordinal),
            DeclaredList(script, "test_support_projects").Order(StringComparer.Ordinal));

        // The companion suite has no project file, so the script is held to running the package's own suite
        // command, and that command to running every companion test file.
        Assert.Contains("npm run test:ui", script, StringComparison.Ordinal);
        using JsonDocument package = JsonDocument.Parse(Repository.Read("package.json"));
        string uiSuite = package.RootElement.GetProperty("scripts").GetProperty("test:ui").GetString() ?? string.Empty;
        Assert.Contains("node --test tests/PartyRpg.Ui.Tests/*.test.mjs", uiSuite, StringComparison.Ordinal);
    }

    [Fact]
    public void A_repository_scan_never_enters_an_agent_worktree_or_build_output()
    {
        // An agent session keeps whole checkouts under .claude/worktrees/; a law that read them would find a second
        // copy of every project and fail because a worktree exists, which is not a boundary. The walk is proved on
        // a tree of its own making so the proof does not depend on whether a worktree happens to exist today.
        string tree = Path.Combine(Path.GetTempPath(), "crawler-scan-" + Guid.NewGuid().ToString("N"));
        try
        {
            string[] planted =
            [
                "src/Owned/Owned.csproj",
                "tests/Suite/Suite.csproj",
                ".claude/worktrees/agent/src/Owned/Owned.csproj",
                "src/Owned/bin/Release/Owned.csproj",
                "src/Owned/obj/Owned.csproj",
                "node_modules/package/Package.csproj",
                "local/verify/Evidence.csproj",
                "src/ui/generated/Generated.csproj",
            ];
            foreach (string file in planted)
            {
                string path = Path.Combine([tree, .. file.Split('/')]);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, "<Project />");
            }

            Assert.Equal(
                ["src/Owned/Owned.csproj", "tests/Suite/Suite.csproj"],
                Repository.Walk(tree, string.Empty, "*.csproj")
                    .Select(path => Path.GetRelativePath(tree, path).Replace(Path.DirectorySeparatorChar, '/'))
                    .Order(StringComparer.Ordinal));
        }
        finally
        {
            Directory.Delete(tree, recursive: true);
        }
    }

    /// <summary>Every project file the repository owns.</summary>
    private static IReadOnlyList<string> AllProjects() => [.. Repository.Files(string.Empty, "*.csproj")];

    private static string Name(string project) => Path.GetFileNameWithoutExtension(project);

    private static bool IsUnderTests(string project) => Repository.Relative(project).StartsWith("tests/", StringComparison.Ordinal);

    private static bool IsSuite(string project) =>
        IsUnderTests(project) && Items(project, "PackageReference").Contains("Microsoft.NET.Test.Sdk", StringComparer.Ordinal);

    private static IEnumerable<string> Items(string project, string item) =>
        XDocument.Load(project).Descendants(item).Select(element => (string?)element.Attribute("Include") ?? string.Empty);

    private static string Property(string project, string property) =>
        XDocument.Load(project).Descendants(property).FirstOrDefault()?.Value.Trim() ?? string.Empty;

    private static List<string> DeclaredList(string script, string name)
    {
        Match match = Regex.Match(script, $@"^{name}=\((?<items>[^)]*)\)", RegexOptions.Multiline | RegexOptions.CultureInvariant);
        Assert.True(match.Success, $"scripts/verify.sh must declare {name}=( ... ).");
        return [.. match.Groups["items"].Value
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !line.StartsWith('#'))];
    }

    private static void AssertNoSmuggledReference(string projectFile)
    {
        foreach (XElement element in XDocument.Load(projectFile).Descendants())
        {
            string name = element.Name.LocalName;
            if (name is "Reference" or "Import")
            {
                Assert.Fail(
                    $"{Path.GetFileName(projectFile)} must reference the engine by package and its peers by ProjectReference; " +
                    $"a <{name}> element can couple layers the dependency graph does not describe.");
            }

            string? include = (string?)element.Attribute("Include");
            if (include is not null && include.Contains("..", StringComparison.Ordinal) &&
                name is "Compile" or "Content" or "None" or "EmbeddedResource")
            {
                Assert.Fail(
                    $"{Path.GetFileName(projectFile)} includes a path outside its own directory (<{name} Include=\"{include}\" />), " +
                    "which is a source-level dependency the project-reference law cannot see.");
            }
        }
    }
}
