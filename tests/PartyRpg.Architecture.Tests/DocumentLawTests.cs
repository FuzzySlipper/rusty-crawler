using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace PartyRpg.Architecture.Tests;

/// <summary>
/// The working contract stays small enough to be read whole, says each thing once, and cites only what a clone has.
/// </summary>
/// <remarks>
/// <para>
/// <c>AGENTS.md</c> is loaded by every agent session. It once grew past what a session could read and was truncated
/// silently, and then restored with its duplicates, so its budget is a number the file states and this suite holds:
/// raising it is a change to both, made on purpose.
/// </para>
/// <para>
/// A citation that resolves only on the operator's machine — a path in the ignored <c>local</c> tree, generated
/// output, an imported pack — is proof nobody else can check. The top-level documents and every project README are
/// read for backticked repository paths and relative links, and each must name something that exists and that
/// <c>.gitignore</c> does not exclude. A backticked path directly after a donor's name is the donor's path, not
/// this repository's. The research notes are not read: they cite donor trees throughout.
/// </para>
/// </remarks>
public sealed class DocumentLawTests
{
    /// <summary>The budget AGENTS.md states for itself, held here too so that raising it is deliberate.</summary>
    private const int AgentsBudgetBytes = 32000;

    /// <summary>Paragraphs shorter than this are headings, rules, or list stubs, whose repetition is not a duplicate.</summary>
    private const int ParagraphFloor = 120;

    private static readonly string[] Donors = ["OpenEnroth", "MMExtension", "OpenMM8"];

    [Fact]
    public void AGENTS_md_states_its_budget_and_stays_under_it()
    {
        string text = Repository.Read("AGENTS.md");
        Match stated = Regex.Match(text, @"size budget:\s*\*\*(\d+) bytes\*\*");
        Assert.True(stated.Success, "AGENTS.md must state its size budget as '**<n> bytes**' after 'size budget:'.");
        Assert.Equal(AgentsBudgetBytes, int.Parse(stated.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));

        int size = Encoding.UTF8.GetByteCount(text);
        Assert.True(
            size <= AgentsBudgetBytes,
            $"AGENTS.md is {size} bytes, over its {AgentsBudgetBytes}-byte budget: move per-mechanism detail to the owning project README.");
    }

    [Fact]
    public void AGENTS_md_repeats_no_paragraph()
    {
        Assert.Empty(RepeatedParagraphs(Repository.Read("AGENTS.md")));
    }

    [Fact]
    public void The_repeated_paragraph_detector_sees_through_wrapping_and_ignores_short_lines()
    {
        string paragraph = string.Join(' ', Enumerable.Repeat("the party is one entity and it owns the inventory", 4));
        string rewrapped = paragraph.Replace(" it owns", "\nit   owns", StringComparison.Ordinal);
        string text = $"{paragraph}\n\n- short\n\n{rewrapped}\n\n- short\n";

        Assert.Single(RepeatedParagraphs(text));
        Assert.Empty(RepeatedParagraphs(paragraph));
    }

    [Fact]
    public void Every_path_the_top_level_documents_cite_exists_in_a_clone()
    {
        IgnoreRules ignores = IgnoreRules.Parse(Repository.Read(".gitignore"));
        List<string> unresolved = [];
        foreach (string document in CitingDocuments())
        {
            unresolved.AddRange(Unresolved(Repository.Root, document, File.ReadAllText(Repository.PathOf(document)), ignores));
        }

        Assert.True(
            unresolved.Count == 0,
            "These citations do not resolve in a fresh clone (publish evidence to docs/evidence/ rather than citing local/):\n" +
            string.Join('\n', unresolved));
    }

    [Fact]
    public void The_citation_detector_refuses_missing_ignored_and_local_paths_and_accepts_the_rest()
    {
        string tree = Path.Combine(Path.GetTempPath(), "crawler-docs-" + Guid.NewGuid().ToString("N"));
        try
        {
            foreach (string file in new[] { "src/Present.cs", "docs/guide.md", "local/verify/README.md", "src/ui/generated/main.js" })
            {
                string path = Path.Combine([tree, .. file.Split('/')]);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, string.Empty);
            }

            IgnoreRules ignores = IgnoreRules.Parse("/local\n**/bin/\nsrc/ui/generated/\n");
            string document =
                "Cites `src/Present.cs:12`, `src/Missing.cs`, `local/verify/README.md`, `src/ui/generated/main.js`, " +
                "OpenEnroth `src/Engine/Party.cpp`, [a guide](guide.md), [gone](gone.md), and [web](https://example.com).";

            Assert.Equal(
                ["docs/readme.md: `src/Missing.cs`", "docs/readme.md: `local/verify/README.md`",
                    "docs/readme.md: `src/ui/generated/main.js`", "docs/readme.md: ](gone.md)"],
                Unresolved(tree, "docs/readme.md", document, ignores));
        }
        finally
        {
            Directory.Delete(tree, recursive: true);
        }
    }

    /// <summary>The documents whose citations a clone must be able to follow.</summary>
    private static IEnumerable<string> CitingDocuments()
    {
        List<string> documents = ["AGENTS.md", "README.md"];
        documents.AddRange(Directory.EnumerateFiles(Repository.PathOf("docs"), "*.md").Select(Repository.Relative));
        foreach (string top in new[] { "src", "tests", "content", "tools", "scripts" })
        {
            if (!Directory.Exists(Repository.PathOf(top))) continue;
            documents.AddRange(
                Repository.Files(top, "README.md")
                    .Select(Repository.Relative)
                    // Generated packs are the operator's, and whatever they carry is not this repository's text.
                    .Where(path => !path.StartsWith("content/partyrpg/imports/", StringComparison.Ordinal) ||
                                   path == "content/partyrpg/imports/README.md"));
        }

        return documents.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
    }

    private static List<string> RepeatedParagraphs(string text)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        List<string> repeated = [];
        foreach (string block in Regex.Split(text.Replace("\r\n", "\n", StringComparison.Ordinal), @"\n\s*\n"))
        {
            string paragraph = Regex.Replace(block, @"\s+", " ").Trim();
            if (paragraph.Length < ParagraphFloor) continue;
            if (!seen.Add(paragraph)) repeated.Add(paragraph[..Math.Min(80, paragraph.Length)] + "…");
        }

        return repeated;
    }

    private static List<string> Unresolved(string root, string document, string text, IgnoreRules ignores)
    {
        // What a clone has at its top, and the operator's local tree, whose every citation is one a clone cannot follow.
        // An ignored entry such as a runtime directory is a place the procedure names, not a citation of proof.
        HashSet<string> topLevel = new(
            Directory.EnumerateFileSystemEntries(root).Select(Path.GetFileName).OfType<string>()
                .Where(name => name is not ".git" and not ".claude" && !ignores.Excludes(name)),
            StringComparer.Ordinal) { "local" };
        List<string> unresolved = [];

        foreach (Match match in Regex.Matches(text, @"`([^`\n]+)`"))
        {
            string token = Regex.Replace(match.Groups[1].Value.Trim(), @":\d+(-\d+)?$", string.Empty);
            if (!Regex.IsMatch(token, @"^[A-Za-z0-9_.\-/]+$")) continue;
            string first = token.Split('/')[0];
            if (!topLevel.Contains(first) || (!token.Contains('/') && !File.Exists(Path.Combine(root, token)))) continue;
            string before = text[..match.Index];
            if (Donors.Any(donor => before.EndsWith(donor + " ", StringComparison.Ordinal))) continue;
            if (!Resolves(root, token.TrimEnd('/'), ignores)) unresolved.Add($"{document}: `{match.Groups[1].Value}`");
        }

        string directory = Path.GetDirectoryName(document.Replace('/', Path.DirectorySeparatorChar)) ?? string.Empty;
        foreach (Match match in Regex.Matches(text, @"\]\(([^)\s]+)\)"))
        {
            string target = match.Groups[1].Value.Split('#')[0];
            if (target.Length == 0 || Regex.IsMatch(target, "^[a-z][a-z0-9+.-]*:")) continue;
            string full = Path.GetFullPath(Path.Combine(root, directory, target));
            string relative = Path.GetRelativePath(root, full).Replace(Path.DirectorySeparatorChar, '/');
            if (relative.StartsWith("..", StringComparison.Ordinal) || !Resolves(root, relative.TrimEnd('/'), ignores))
            {
                unresolved.Add($"{document}: ]({match.Groups[1].Value})");
            }
        }

        return unresolved;
    }

    private static bool Resolves(string root, string relative, IgnoreRules ignores)
    {
        string path = Path.Combine([root, .. relative.Split('/')]);
        return (File.Exists(path) || Directory.Exists(path)) && !ignores.Excludes(relative);
    }

    /// <summary>The subset of <c>.gitignore</c> the repository uses: anchored and floating paths, wildcards, negation.</summary>
    private sealed class IgnoreRules
    {
        private readonly List<(Regex Pattern, bool Negated)> _rules = [];

        public static IgnoreRules Parse(string text)
        {
            IgnoreRules rules = new();
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#')) continue;
                bool negated = line.StartsWith('!');
                if (negated) line = line[1..];
                bool anchored = line.StartsWith('/') || line.TrimEnd('/').Contains('/');
                string body = line.Trim('/');
                string pattern = Regex.Escape(body)
                    .Replace(@"\*\*/", "(.*/)?", StringComparison.Ordinal)
                    .Replace(@"\*\*", ".*", StringComparison.Ordinal)
                    .Replace(@"\*", "[^/]*", StringComparison.Ordinal)
                    .Replace(@"\?", "[^/]", StringComparison.Ordinal);
                rules._rules.Add((new Regex((anchored ? "^" : "^(.*/)?") + pattern + "(/.*)?$"), negated));
            }

            return rules;
        }

        /// <summary>Whether the path, or a directory above it, is excluded; the last rule that matches decides.</summary>
        public bool Excludes(string relative)
        {
            bool excluded = false;
            foreach ((Regex pattern, bool negated) in _rules)
            {
                if (pattern.IsMatch(relative)) excluded = !negated;
            }

            return excluded;
        }
    }
}
