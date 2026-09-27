using PartyRpg.Kit.Party;

namespace PartyRpg.Kit.Progression;

/// <summary>How the world's opinion of the party reads, in the words this game uses for it.</summary>
/// <remarks>
/// <para>
/// A band is a name for a stretch of one number, and the reading is what that stretch does to the party:
/// a game whose people treat a well-regarded band differently from a hated one says so here rather than in
/// a screen, because the words and the thresholds behind them are the same policy that decides what a
/// person offers and what a counter charges.
/// </para>
/// <para>
/// The two strings are the game's own and are printed unchanged: a panel that worked out "friendly" from a
/// number would be a second reading of the thresholds, and it would disagree with the mechanism the first
/// time a threshold moved.
/// </para>
/// </remarks>
/// <param name="Band">What the world calls a party standing where it stands, empty when nothing is read.</param>
/// <param name="Reading">What that standing means for how the party is treated, empty when nothing is read.</param>
public readonly record struct StandingReading(string Band, string Reading)
{
    /// <summary>Names the band a party stands in and what that band does for it.</summary>
    /// <param name="band">What the world calls the party's standing.</param>
    /// <param name="reading">What that standing means for how the party is treated.</param>
    /// <returns>The reading.</returns>
    /// <exception cref="ArgumentException">A word is blank, so the band would read as nothing.</exception>
    public static StandingReading Of(string band, string reading)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(band);
        ArgumentException.ThrowIfNullOrWhiteSpace(reading);
        return new StandingReading(band, reading);
    }

    /// <summary>What a session whose ruleset reads no standing publishes, which is not a band of its own.</summary>
    public static StandingReading None => default;

    /// <summary>Whether this is a reading at all, which a ruleset that states no thresholds does not give.</summary>
    public bool IsRead => !string.IsNullOrWhiteSpace(Band);
}

/// <summary>One thing the party has accomplished, in the words this game calls it by.</summary>
/// <remarks>
/// <para>
/// An award is not state of its own: what a party has accomplished is the records it carries — the errand a
/// finished quest left, the rank a promotion granted, the membership a guild sold — and a game reads them
/// back into names here so a screen can show what a person would say about the band rather than a record's
/// internal identity. The identity travels beside the words so a row can still be traced to the state that
/// produced it.
/// </para>
/// <para>
/// The kind is the game's own word for the family an award belongs to — a promotion, an errand, a
/// membership, a deed — and it is deliberately a word rather than a list this kit enumerates: which kinds a
/// game has is its own business, and a screen that wanted to group by kind has the word to read.
/// </para>
/// </remarks>
/// <param name="Id">The record's own identity, which is the state the award is carried as.</param>
/// <param name="Kind">What family the game counts it in.</param>
/// <param name="Label">What the game calls it.</param>
/// <param name="Detail">What is true of it beyond its name, or empty when nothing is.</param>
public readonly record struct AwardReading(string Id, string Kind, string Label, string Detail)
{
    /// <summary>Names one accomplishment a party carries.</summary>
    /// <param name="id">The record's own identity.</param>
    /// <param name="kind">What family the game counts it in.</param>
    /// <param name="label">What the game calls it.</param>
    /// <param name="detail">What is true of it beyond its name.</param>
    /// <returns>The reading.</returns>
    /// <exception cref="ArgumentException">An identity, a kind, or a label is blank.</exception>
    public static AwardReading Of(string id, string kind, string label, string detail = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        return new AwardReading(id, kind, label, detail ?? string.Empty);
    }
}

/// <summary>
/// What this game says about the party's standing and what the party has accomplished: the words, never the
/// state.
/// </summary>
/// <remarks>
/// <para>
/// <b>The state lives where it already lived.</b> Reputation and fame are the party's own component and the
/// progression owner is the only thing that writes them; an accomplishment is a party-carried record, which
/// is the same owner a conversation's own note, a quest's record, and a promotion's award reach, and which
/// content reads back through the condition vocabulary the conversation and the quests already judge. This
/// interface adds no state and no second vocabulary: it answers the two questions a screen cannot answer for
/// itself — which words this game uses for the band the party stands in, and which of the records it carries
/// this game counts as something the party has done.
/// </para>
/// <para>
/// <b>A ruleset may answer neither.</b> A game that states no thresholds has no band to name, and one that
/// counts nothing as an accomplishment reads an empty list; both are honest answers, and a session whose
/// ruleset implements this at all publishes them as such rather than inventing a band.
/// </para>
/// <para>
/// <b>Nothing here changes anything.</b> A reading is a read: a rule that moved reputation while being asked
/// what it reads as would be a second writer of one fact, which is what <see cref="IProgressionRule"/> and
/// this interface exist to keep apart.
/// </para>
/// </remarks>
public interface IStandingRule
{
    /// <summary>What this game calls the standing the party holds, and what that standing does for it.</summary>
    /// <param name="party">The party whose standing is read.</param>
    /// <returns>The words for the band the party stands in, or no reading when this game names no bands.</returns>
    /// <exception cref="ArgumentNullException">No party was supplied.</exception>
    StandingReading Read(PartyEntity party);

    /// <summary>What the party carries that this game counts as something it has accomplished.</summary>
    /// <remarks>
    /// The order is the party's own order of records, so two readings of one party read the same way; a list
    /// that sorted or grouped would be this layer deciding what belongs together.
    /// </remarks>
    /// <param name="party">The party whose records are read.</param>
    /// <returns>One reading per accomplishment, in the order the party carries them; empty when it holds none.</returns>
    /// <exception cref="ArgumentNullException">No party was supplied.</exception>
    IReadOnlyList<AwardReading> Awards(PartyEntity party);
}
