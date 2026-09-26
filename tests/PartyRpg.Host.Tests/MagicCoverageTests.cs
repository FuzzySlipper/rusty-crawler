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

        // The potions are counted on the same terms and by the same test: every one of the game's own potion
        // rows is listed once, its three states add up to the rows, and every gap names the owner that would
        // close it.
        IReadOnlyList<MightAndMagic7Potions.PotionRowReading> potions = MightAndMagic7Potions.Rows;
        Assert.Equal(MightAndMagic7Potions.Count, potions.Count);
        Assert.Equal(potions.Count, potions.Select(row => row.Id).Distinct().Count());
        Assert.Equal(
            potions.Count,
            potions.Count(row => row.Coverage.State == SpellEffectCoverageState.Implemented) +
            potions.Count(row => row.Coverage.State == SpellEffectCoverageState.Approximated) +
            potions.Count(row => row.Coverage.State == SpellEffectCoverageState.NotYet));
        Assert.DoesNotContain(potions, row => string.IsNullOrWhiteSpace(row.Effect));
        Assert.All(
            potions.Where(row => row.Coverage.State == SpellEffectCoverageState.NotYet),
            row => Assert.False(string.IsNullOrWhiteSpace(row.Coverage.Receiver), $"potion {row.Id} leaves its gap unrouted"));
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

            ## What a duration does across a save

            A duration is a deadline registered with the session's one clock, and the effect it ends is carried
            state: an effect a spell aimed at one character is written under that character's own entry, and a
            spell aimed at the party is carried by the party. Neither the deadline nor the effect's end moment
            is a number the effect carries, which is what makes the save boundary's own answer the honest one:

            | what | across a save today |
            | --- | --- |
            | an item's spent charges | carried: a charge is item state, written with the instance's damage and enchantments, so a half-spent wand resumes half spent |
            | an effect's existence and magnitude | not carried: while any deadline is registered the clock refuses to be captured, so a session holding a running ward, light, or haste cannot be saved at all |
            | when an effect ends | not carried, for the same reason: the save records elapsed game time and no deadlines |

            The refusal is by name rather than silent — `PartyRpg.Kit.Persistence.ClockSave.Capture` throws a
            `SessionSaveException` listing the deadlines the clock holds — so a save taken while magic runs is
            refused where a player can read it instead of dropping the schedule. Carrying deadlines (which
            owner registered one, when it is due, and how it repeats) is Den task #8617's own requirement, and
            it names the spell-effect deadlines among the owners that task must carry.

            ## Casting from an item

            A scroll and a wand carry one spell each, read from the shipped item table's own reference column,
            and both are cast through the session's one casting workflow with the item as the spell's source:
            no school skill and no spell point is asked for, and the item is what pays.

            | what is used | how |
            | --- | --- |
            | a scroll | the one spell it carries, once, and the scroll is used up; the donor's own scroll cast carries no mana cost at all (OpenEnroth `src/Engine/Spells/CastSpellInfo.cpp:207`, the `overrideSkillValue` branch that sets `uRequiredMana = 0`) |
            | a wand | the spell it carries, fired as the weapon it is wielded as, one charge spent per use, and the item leaves the party through the inventory when its last charge goes; the donor fires it at a fixed eighth level of novice mastery (OpenEnroth `src/Engine/Spells/CastSpellInfo.h:61`, `WANDS_SKILL_VALUE`) |
            | a potion | the effect this game states for its own row, once, and the potion is used up; what it is read at is the potion's own strength rather than any character's school level (OpenEnroth `src/Engine/Objects/Character.cpp:3081-3085`, `potionStrength`), which is what makes a potion the way a character with no school at all gets a spell's effect |

            What this build does not take from the donor is the fixed skill reading of a scroll cast: the donor
            casts one at the fifth level of master mastery (OpenEnroth `src/Engine/Spells/CastSpellInfo.h:60`,
            `SCROLL_OR_NPC_SPELL_SKILL_VALUE`), while this build casts it at the caster's own school, because a
            damaging spell's numbers are resolved by the fight's own ability answer and that answer is asked
            with the caster rather than with the item that carried the spell (receiver: the fight's ability
            resolution, which would have to be handed the casting's own skill reading). A wand's own value *is*
            taken, because a wand is the weapon the fight resolves the attack with.

            ## Potions

            A potion is an item that carries one effect, and drinking it is a casting whose source is the item:
            the same workflow that reads a scroll, the same effect path that applies a spell, and a per-character
            deadline on the one clock wherever the donor's potion lasts. What is different is where the strength
            comes from — a potion's own, not a caster's school — and that this game authors the effect's numbers
            from the donor's drinking switch (OpenEnroth `src/Engine/Objects/Character.cpp:3080-3300`), because
            the shipped `POTION.TXT` states what each potion is for in words and no numbers at all.

            | potion | category | aim | state | what it does, or what is missing | receiver |
            | --- | --- | --- | --- | --- | --- |

            """
            + "\n");

        foreach (MightAndMagic7Potions.PotionRowReading potion in MightAndMagic7Potions.Rows)
        {
            text.Append(string.Create(
                CultureInfo.InvariantCulture,
                $"| {potion.Id} | {potion.Effect} | {potion.Targeting} | {SpellEffectCoverageStates.WireName(potion.Coverage.State)} | {Cell(potion.Coverage.Note)} | {Cell(potion.Coverage.Receiver)} |\n"));
        }

        text.Append(
            """

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
