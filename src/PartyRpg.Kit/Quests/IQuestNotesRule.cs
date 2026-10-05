using PartyRpg.Kit.Party;

namespace PartyRpg.Kit.Quests;

/// <summary>A quest note derived from canonical gameplay records, without a separate quest instance.</summary>
/// <param name="Id">The content identity.</param>
/// <param name="Name">The journal heading.</param>
/// <param name="Text">What the party has been asked to do.</param>
/// <param name="Giver">The giver, or empty when the note names none.</param>
public sealed record QuestNote(string Id, string Name, string Text, string Giver = "");

/// <summary>Optional ruleset reading for quests whose events already own their progress and rewards.</summary>
public interface IQuestNotesRule
{
    /// <summary>Reads active notes from the party's current records; it creates no quest state.</summary>
    IReadOnlyList<QuestNote> NotesFor(PartyEntity party);
}
