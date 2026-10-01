using PartyRpg.Kit.Knowledge;
using PartyRpg.Kit.Party;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// This game's own reading of what a party discovers: the words its notes read with, and what it counts as
/// worth knowing.
/// </summary>
/// <remarks>
/// <para>
/// <b>The vocabulary is the shipped discovery table's own.</b> The original keeps auto notes as a table of
/// numbered rows — each a sentence and a type word — and a party-owned set of the numbers it has learned
/// (OpenEnroth <c>src/Engine/Tables/AutonoteTable.cpp:19-35</c>, which reads the index, the text, and one of
/// the type words <c>potion</c>, <c>stat</c>, <c>obelisk</c>, <c>seer</c>, <c>teacher</c>, and <c>misc</c>;
/// <c>src/Engine/Party.h:329</c>, <c>IndexedBitset&lt;1, 208&gt; _autonoteBits</c>, which the save carries at
/// <c>src/Engine/Snapshots/EntitySnapshots.h:509</c>). The four kinds below are this game's reading of those
/// words: a <c>stat</c> row is what a landmark gives (<see cref="KnowledgeKind.Effect"/>), an
/// <c>obelisk</c> row is something written that the party read (<see cref="KnowledgeKind.Clue"/>), a
/// <c>potion</c> row is a mixture (<see cref="KnowledgeKind.Recipe"/>), and a notable discovery is a
/// <see cref="KnowledgeKind.Find"/>. A <c>misc</c> row is something a landmark did and is read as an effect, and
/// a <c>teacher</c> or <c>seer</c> row is something the party was told and is read as a clue
/// (<see cref="MightAndMagic7Fixtures"/>); a person's own lines are the conversation owner's, which reports
/// none of them yet.
/// </para>
/// <para>
/// <b>The operator's own data carries both halves of that table's use.</b> The importer writes the discovery
/// table as <c>discovery</c> entries (186 rows that hold a note), and the map events that set its rows are run by
/// the fixture that raises them: a well's step writing note 3 reports that row, and the knowledge owner keeps
/// it once. What is stated here is the phrase a note of each kind opens with and the threshold over what is
/// kept.
/// </para>
/// <para>
/// <b>The threshold is this game's, and for a find it is the artifacts and relics.</b> A party that kept a
/// note of every sword it found would bury what it knows, so a find keeps a note when the shipped item table
/// marks it as a thing the game hands out as an artifact or a relic — the same reading, over the same rows,
/// that this ruleset already uses for the journal's own lines and for what a treasure level may yield
/// (<see cref="MightAndMagic7Loot"/>, whose range is the donor's own at
/// <c>OpenEnroth src/Engine/Objects/ItemEnums.h:977</c>). A mixture keeps a note only when its own row
/// states a discovery, a landmark's effect is whatever the event that gave it stated, and something written
/// is a clue by being read; every one of those is the reporting owner's own fact rather than a second
/// judgement here, so this threshold has one thing to decide.
/// </para>
/// <para>
/// <b>What the phrases are is ours.</b> The shipped table's own sentences are its rows, and the manual says
/// only what the book holds ("Auto Notes holds potion discoveries, fountain effects, obelisk clues and
/// miscellaneous events", <c>docs/research/mm7-manual-outline.md</c> p.165 from the manual p.22), so the
/// phrasing below is authored for this game and marked as ours rather than presented as the original's: the
/// journal's lines are worded the same way and for the same reason.
/// </para>
/// </remarks>
internal sealed class MightAndMagic7Knowledge : IKnowledgeRule
{
    private readonly MightAndMagic7Loot? _loot;

    /// <summary>Creates this game's knowledge policy.</summary>
    /// <param name="loot">
    /// This game's loot, which is what knows which item rows the shipped table hands out as artifacts and
    /// relics. Without one — a product composed over no content — nothing can be found, so no find is worth
    /// keeping and the threshold answers no rather than inventing a rule.
    /// </param>
    internal MightAndMagic7Knowledge(MightAndMagic7Loot? loot = null) => _loot = loot;

    /// <inheritdoc />
    /// <remarks>
    /// A landmark's effect and a clue read from something written are both facts the party gained, so the
    /// words say it learned them; a mixture says what it is a recipe for; and a find says it was found.
    /// </remarks>
    public string Phrase(KnowledgeKind kind) => kind switch
    {
        KnowledgeKind.Effect => "Learned",
        KnowledgeKind.Clue => "Read",
        KnowledgeKind.Recipe => "Learned the recipe for",
        _ => "Found",
    };

    /// <inheritdoc />
    public bool WorthLearning(KnowledgeReport report) => report.Kind switch
    {
        // A find is the one discovery this game judges: an artifact or a relic is worth knowing, and the
        // ordinary contents of an ordinary chest are what a party carries rather than what it remembers.
        KnowledgeKind.Find => _loot is { } loot && loot.IsArtifact(new ItemDefinitionId(report.Subject)),
        _ => true,
    };
}
