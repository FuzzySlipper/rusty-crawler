using PartyRpg.Kit.Journal;
using PartyRpg.Kit.Party;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// This game's own journal: what its five books are called, how a line of its record reads, and what it
/// counts as worth writing down.
/// </summary>
/// <remarks>
/// <para>
/// <b>The books are the manual's own five.</b> "Book screens: five tabbed books — <b>Current Quests, Auto
/// Notes, Maps, Calendar, History</b> — with page tabs. Auto Notes holds potion discoveries, fountain effects,
/// obelisk clues and miscellaneous events; History is a chronological journal of the party's travels."
/// (<c>docs/research/mm7-manual-outline.md</c>, p.165, from the manual p.22.) The titles below are those
/// words, and what each book says when it holds nothing is **ours**, authored for a build that words its own
/// sentences rather than shipping the original's localized strings.
/// </para>
/// <para>
/// <b>The lines' phrasing is ours.</b> The manual states how a journal dates a line — day, month name, and
/// year (p.124, from the manual pp.4–9) — and does not state the wording of a history entry, so the phrases
/// here are authored for this game and marked as ours rather than presented as the original's.
/// </para>
/// <para>
/// <b>What is worth writing down is this game's threshold, and it is the artifacts and relics.</b> A journal
/// that noted every sword and potion it found would bury what the party actually did, so a find earns a line
/// when the shipped item table marks it as a thing the game hands out as an artifact or a relic — the same
/// reading, over the same rows, that this ruleset already uses to decide what a treasure level may yield
/// (<see cref="MightAndMagic7Loot"/>, whose range is the donor's own at
/// <c>OpenEnroth src/Engine/Objects/ItemEnums.h:977</c>). Every other kind of event is written down: a place
/// the party has been, an errand it heard, took, or finished, a rank it rose to, and a person it spoke with
/// are each what a record of an expedition is for.
/// </para>
/// <para>
/// <b>Auto Notes is a reading of what the party has learned and not of this record.</b> The discoveries that
/// book holds — a potion recipe, a fountain's effect, an obelisk's clue — are facts the party gained rather
/// than a record of what it did, so they belong to the knowledge owner beside this journal and the book
/// reads them from there. What is stated here is only what this book is called and what it says when the
/// party has learned nothing worth noting: which discoveries are worth keeping is
/// <see cref="MightAndMagic7Knowledge"/>'s answer, exactly as what is worth a line is this policy's.
/// </para>
/// </remarks>
internal sealed class MightAndMagic7Journal : IJournalRule
{
    /// <summary>The artifact's material column value in the shipped item table, which is what makes a find notable.</summary>
    private readonly MightAndMagic7Loot? _loot;

    /// <summary>Creates this game's journal policy.</summary>
    /// <param name="loot">
    /// This game's loot, which is what knows which item rows the shipped table hands out as artifacts and
    /// relics. Without one — a product composed over no content — nothing can be found, so no find is
    /// notable and the threshold answers no rather than inventing a rule.
    /// </param>
    internal MightAndMagic7Journal(MightAndMagic7Loot? loot = null) => _loot = loot;

    /// <inheritdoc />
    /// <remarks>The manual's own five books, in its own order (<c>docs/research/mm7-manual-outline.md</c> p.165).</remarks>
    public IReadOnlyList<JournalBookKind> Books { get; } =
    [
        JournalBookKind.Quests,
        JournalBookKind.Notes,
        JournalBookKind.Maps,
        JournalBookKind.Calendar,
        JournalBookKind.History,
    ];

    /// <inheritdoc />
    public JournalBookWords Book(JournalBookKind book) => book switch
    {
        JournalBookKind.Quests => new(
            "Current Quests",
            "The party has been offered nothing.",
            "This session holds no errand owner yet: what a party has been offered is kept by one composed when the party exists."),
        JournalBookKind.Notes => new(
            "Auto Notes",
            "The party has learned nothing worth noting yet.",
            "This session keeps no knowledge owner: what a party learns about the world — a potion's recipe, a fountain's effect, an obelisk's clue — is kept by the owner composed over the party and the clock."),
        JournalBookKind.Maps => new(
            "Maps",
            "The party has mapped nowhere yet.",
            "This session keeps no map owner: what a party has walked of a place is kept by the owner composed over the places' own maps."),
        JournalBookKind.Calendar => new(
            "Calendar",
            "This session holds no clock, so no day can be named.",
            "This session holds no clock, so no day can be named."),
        _ => new(
            "History",
            "Nothing has been written down yet.",
            "This session keeps no journal: its ruleset stated none."),
    };

    /// <inheritdoc />
    public string Phrase(JournalEntryKind kind) => kind switch
    {
        JournalEntryKind.Place => "Entered",
        JournalEntryKind.QuestOffered => "Was offered",
        JournalEntryKind.QuestTaken => "Took on",
        JournalEntryKind.QuestFinished => "Finished",
        JournalEntryKind.Rank => "Was raised to",
        JournalEntryKind.Meeting => "Met",

        // A history-book line is the table's own whole line, which a phrase would only interrupt.
        JournalEntryKind.Chronicle => string.Empty,
        _ => "Found",
    };

    /// <inheritdoc />
    public bool WorthRecording(JournalEvent journalEvent) => journalEvent.Kind switch
    {
        // A find is the one event this game judges: an artifact or a relic is worth a line, and the ordinary
        // contents of an ordinary chest are what a party carries rather than what it remembers.
        JournalEntryKind.Find => _loot is { } loot && loot.IsArtifact(new ItemDefinitionId(journalEvent.Subject)),
        _ => true,
    };
}
