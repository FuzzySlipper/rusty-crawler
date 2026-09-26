using System.Globalization;
using System.Text;
using PartyRpg.Kit.Magic;
using PartyRpg.Rulesets.MightAndMagic7;
using Xunit;

namespace PartyRpg.Host.Tests;

/// <summary>
/// The spell effect coverage report: every one of this game's spells listed as implemented, approximated, or
/// not yet, and a test that fails when the report and the game's own table disagree.
/// </summary>
/// <remarks>
/// <para>
/// <b>The report is generated from the same rows the effect path applies by.</b> Every line of
/// <c>docs/magic-coverage.md</c> comes from <see cref="MightAndMagic7Spells.Rows"/> — the compiled table of
/// what each spell is read as and how far this build expresses it — so a row whose category, rung, aim, or
/// coverage changes and a report that was not regenerated fail this test rather than surviving as a claim
/// about code that no longer exists. The catalog's own identity is tied to the same ids: loading content
/// refuses a pack that declares a spell this table states no numbers for, or states one where the table has
/// another, so a report keyed by a row's id and a catalog keyed by content's id are about the same spells.
/// </para>
/// <para>
/// <b>Regenerating it is one command.</b> Setting <c>CRAWLER_WRITE_MAGIC_COVERAGE=1</c> makes this test write
/// the document it would otherwise compare against, which is how the file is produced rather than
/// hand-edited. Without it the test compares and names the first line that differs.
/// </para>
/// </remarks>
public sealed class MagicCoverageTests
{
    /// <summary>The environment variable that writes the report instead of checking it.</summary>
    private const string WriteVariable = "CRAWLER_WRITE_MAGIC_COVERAGE";

    [Fact]
    public void Every_spell_is_listed_once_with_the_state_the_ruleset_answers()
    {
        string path = Path.Combine(RepositoryRoot(), "docs", "magic-coverage.md");
        string expected = Report();
        if (string.Equals(Environment.GetEnvironmentVariable(WriteVariable), "1", StringComparison.Ordinal))
        {
            File.WriteAllText(path, expected);
            return;
        }

        Assert.True(File.Exists(path), $"{path} is missing; regenerate it with {WriteVariable}=1.");
        string actual = File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n');
        Assert.Equal(expected.TrimEnd('\n'), actual);
    }

    [Fact]
    public void The_three_states_are_counted_and_every_one_of_the_rows_is_covered()
    {
        IReadOnlyList<MightAndMagic7Spells.SpellRowReading> rows = MightAndMagic7Spells.Rows;

        // Every row the table states numbers for is listed exactly once, and the three states are counted
        // over all of them: a spell can be implemented, coarser than the game, or not applied at all, and
        // nothing else.
        Assert.Equal(MightAndMagic7Spells.ExpectedSpells, rows.Count);
        Assert.Equal(rows.Count, rows.Select(row => row.Id).Distinct().Count());
        int implemented = rows.Count(row => row.Coverage.State == SpellEffectCoverageState.Implemented);
        int approximated = rows.Count(row => row.Coverage.State == SpellEffectCoverageState.Approximated);
        int notYet = rows.Count(row => row.Coverage.State == SpellEffectCoverageState.NotYet);
        Assert.Equal(rows.Count, implemented + approximated + notYet);

        // Every state is present, and every gap names the owner that would close it: a "not yet" without a
        // receiver is exactly the unrouted silence the report exists to prevent.
        Assert.True(implemented > 0 && approximated > 0 && notYet > 0);
        Assert.All(
            rows.Where(row => row.Coverage.State == SpellEffectCoverageState.NotYet),
            row => Assert.False(string.IsNullOrWhiteSpace(row.Coverage.Receiver), $"spell {row.Id} leaves its gap unrouted"));

        // Every spell carries one of the design's eight categories, and each category has at least one spell
        // this build applies, which is what "effects exist by category first" means in a report.
        string[] categories =
        [
            "damage", "healing", "resistance", "condition", "light", "travel", "detection", "utility",
        ];
        Assert.Equal(categories.Order(StringComparer.Ordinal), rows.Select(row => row.Effect).Distinct().Order(StringComparer.Ordinal));
        foreach (string category in categories)
        {
            Assert.Contains(
                rows,
                row => string.Equals(row.Effect, category, StringComparison.Ordinal) && row.Coverage.State == SpellEffectCoverageState.Implemented);
        }
    }

    /// <summary>Builds the report from the ruleset's own rows, exactly as the checked-in file holds it.</summary>
    private static string Report()
    {
        IReadOnlyList<MightAndMagic7Spells.SpellRowReading> rows = MightAndMagic7Spells.Rows;
        string[] categories =
        [
            "damage", "healing", "resistance", "condition", "light", "travel", "detection", "utility",
        ];
        StringBuilder text = new();
        text.Append(
            """
            # Spell effect coverage

            Every spell this game states numbers for, and how far this build expresses its effect. The file is
            generated from the ruleset's own table by `MagicCoverageTests` and checked by that test on every
            run, so it cannot drift from the code: change a row's category, its rung, what it is aimed at, or
            how far it is expressed, and this document has to be regenerated with
            `CRAWLER_WRITE_MAGIC_COVERAGE=1 dotnet test tests/PartyRpg.Host.Tests`.

            Names live in the operator's own content — this repository commits none — so a row is identified by
            content's own spell id, which is also the id the ruleset's table is keyed by and the id this
            document's rows carry. A reader who wants the shipped name for a row will find it beside the same
            id in `src/PartyRpg.Rulesets.MightAndMagic7/MightAndMagic7Spells.cs`.

            ## How a state is read

            | state | what it means |
            | --- | --- |
            | implemented | the cast changes state through the owner that holds it, and this build reads that state where it applies |
            | approximated | the cast changes real state through that owner, more coarsely than the game does; the difference is named |
            | not yet | nothing this build reads changes, and the owner that would close the gap is named |

            ## Counts

            """
            + "\n");

        text.Append("| category | implemented | approximated | not yet | spells |\n");
        text.Append("| --- | --- | --- | --- | --- |\n");
        foreach (string category in categories)
        {
            List<MightAndMagic7Spells.SpellRowReading> of = [.. rows.Where(row => string.Equals(row.Effect, category, StringComparison.Ordinal))];
            text.Append(string.Create(
                CultureInfo.InvariantCulture,
                $"| {category} | {of.Count(row => row.Coverage.State == SpellEffectCoverageState.Implemented)} | {of.Count(row => row.Coverage.State == SpellEffectCoverageState.Approximated)} | {of.Count(row => row.Coverage.State == SpellEffectCoverageState.NotYet)} | {of.Count} |\n"));
        }

        text.Append(string.Create(
            CultureInfo.InvariantCulture,
            $"| **all** | **{rows.Count(row => row.Coverage.State == SpellEffectCoverageState.Implemented)}** | **{rows.Count(row => row.Coverage.State == SpellEffectCoverageState.Approximated)}** | **{rows.Count(row => row.Coverage.State == SpellEffectCoverageState.NotYet)}** | **{rows.Count}** |\n"));

        text.Append(
            """

            ## Every spell

            A rung is the mastery of the spell's school the spell requires: one is basic, two expert, three
            master, and four grand master.

            | spell | category | rung | aim | state | what it does, or what is missing | receiver |
            | --- | --- | --- | --- | --- | --- | --- |
            """
            + "\n");

        foreach (MightAndMagic7Spells.SpellRowReading row in rows)
        {
            text.Append(string.Create(
                CultureInfo.InvariantCulture,
                $"| {row.Id} | {row.Effect} | {row.Tier} | {SpellTargetings.WireName(row.Targeting)} | {SpellEffectCoverageStates.WireName(row.Coverage.State)} | {Cell(row.Coverage.Note)} | {Cell(row.Coverage.Receiver)} |\n"));
        }

        return text.ToString();
    }

    /// <summary>Writes a cell so a sentence containing a table's own delimiter cannot break the row.</summary>
    private static string Cell(string text) => text.Replace("|", "\\|", StringComparison.Ordinal);

    /// <summary>The repository root, found the way the suites that read checked-in files find it.</summary>
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
}
