using System.Globalization;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.Persistence;

namespace PartyRpg.Kit.Knowledge;

/// <summary>One fact a party knows, as a save records it, under the product's one current schema.</summary>
/// <remarks>
/// <para>
/// <b>The date is deliberately absent and the elapsed game time is here instead.</b> A save records how
/// far into the session a fact was learned and never which day it fell on: the calendar, the date a
/// session begins at, and the rate game time runs at are the ruleset's policy and are supplied again when
/// a session is composed, so a date stored beside them would move every note the party holds the moment
/// any of them changed. The elapsed span is what the clock actually counted, and a load reads it back
/// against the calendar it was resumed on — which is what makes a loaded note read as the day it was
/// learned rather than the day it was loaded.
/// </para>
/// <para>
/// <b>The note is carried rather than recomposed.</b> What a person read when the fact was learned is what
/// the party's knowledge says it knows; re-deriving it from the owners a load happens to find would let a
/// renamed mixture or a landmark the world no longer carries rewrite what the party learned.
/// </para>
/// </remarks>
/// <param name="Kind">The kind of discovery, as the wire spells it.</param>
/// <param name="Source">Which owner reported it.</param>
/// <param name="Subject">The identity of what it is about.</param>
/// <param name="Text">The note a person reads.</param>
/// <param name="ElapsedMilliseconds">The game time since the session began at which it was learned.</param>
/// <param name="Place">The place it was learned in, or empty when it was learned nowhere in particular.</param>
public sealed record KnowledgeNoteSave(
    string Kind,
    string Source,
    string Subject,
    string Text,
    long ElapsedMilliseconds,
    string Place = "")
{
    /// <summary>The place it was learned in, or empty when it was learned nowhere in particular.</summary>
    public string Place { get; init; } = Place ?? string.Empty;

    /// <summary>Reads one recorded fact as a dated note, over the clock the session was resumed on.</summary>
    /// <remarks>
    /// The date is composed exactly as the clock composes its own: the elapsed span is added to the date
    /// the session began at, on the calendar the ruleset supplied. A note therefore reads as the day it was
    /// learned even when the party has lived for weeks since.
    /// </remarks>
    /// <param name="recorded">The fact the save recorded.</param>
    /// <param name="clock">The session's one clock, which owns the calendar and the date the session began at.</param>
    /// <returns>The dated note.</returns>
    /// <exception cref="ArgumentNullException">A recorded fact or a clock is missing.</exception>
    /// <exception cref="ArgumentException">The kind word names no kind this build has.</exception>
    public static KnowledgeNote Read(KnowledgeNoteSave recorded, GameClock clock)
    {
        ArgumentNullException.ThrowIfNull(recorded);
        ArgumentNullException.ThrowIfNull(clock);
        return new KnowledgeNote(
            ReadKind(recorded.Kind),
            recorded.Source,
            recorded.Subject,
            recorded.Text,
            recorded.ElapsedMilliseconds,
            clock.Calendar.Add(clock.Start, GameDuration.FromMilliseconds(recorded.ElapsedMilliseconds)),
            recorded.Place);
    }

    /// <summary>Records one dated note as a save writes it.</summary>
    /// <param name="note">The fact to record.</param>
    /// <returns>The recorded fact.</returns>
    /// <exception cref="ArgumentNullException">No note was supplied.</exception>
    public static KnowledgeNoteSave Record(KnowledgeNote note)
    {
        ArgumentNullException.ThrowIfNull(note);
        return new KnowledgeNoteSave(
            Word(note.Kind),
            note.Source,
            note.Subject,
            note.Text,
            note.ElapsedMilliseconds,
            note.Place);
    }

    /// <summary>The word the wire spells one kind of discovery with.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The word.</returns>
    public static string Word(KnowledgeKind kind) => kind switch
    {
        KnowledgeKind.Effect => "effect",
        KnowledgeKind.Clue => "clue",
        KnowledgeKind.Recipe => "recipe",
        _ => "find",
    };

    /// <summary>The kind one word names.</summary>
    /// <param name="word">The word.</param>
    /// <returns>The kind.</returns>
    /// <exception cref="ArgumentException">The word names no kind this build has.</exception>
    public static KnowledgeKind ReadKind(string word) => word switch
    {
        "effect" => KnowledgeKind.Effect,
        "clue" => KnowledgeKind.Clue,
        "recipe" => KnowledgeKind.Recipe,
        "find" => KnowledgeKind.Find,
        _ => throw new ArgumentException(
            $"A knowledge note is recorded as '{word}', which is not a kind this build has.",
            nameof(word)),
    };
}

/// <summary>Every fact one party knows, as a save records them.</summary>
/// <remarks>
/// <para>
/// <b>This is party state and not place state.</b> The knowledge belongs to the party that learned it, so
/// nothing here is keyed by where the party stood, a place whose population is restored touches none of
/// it, and a party that walks out of the place it learned something in keeps what it learned. The world's
/// own per-place state is a different section of the document and is deliberately not consulted here.
/// </para>
/// <para>
/// <b>The section is the whole of what the party knows and never a second copy of what is true.</b> What a
/// landmark currently does and what a container currently holds stay with the owners that hold them, so
/// knowledge is a record of what was learned rather than a copy of the world.
/// </para>
/// </remarks>
/// <param name="Notes">The facts, in the order the party learned them.</param>
public sealed record KnowledgeSave(IReadOnlyList<KnowledgeNoteSave>? Notes = null)
{
    /// <summary>The facts, in the order the party learned them.</summary>
    public IReadOnlyList<KnowledgeNoteSave> Notes { get; init; } = Notes ?? [];

    /// <summary>A party that has learned nothing it keeps.</summary>
    public static KnowledgeSave None { get; } = new();

    /// <summary>Whether this records nothing, which is what a party that has just begun knows.</summary>
    public bool IsEmpty => Notes.Count == 0;

    /// <summary>
    /// Every contradiction between this knowledge and the session it would be resumed into.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The problems are contradictions rather than rules the product might refuse, because a load must not
    /// re-judge what the product itself recorded: a kind word this build does not have came from something
    /// else, a note with no source, subject, or words says nothing, a note dated after the game time the
    /// save had reached was learned in a future the session never lived, and two notes about one fact mean
    /// the dedupe this owner exists for did not hold.
    /// </para>
    /// <para>
    /// <b>A note whose place the world no longer carries is deliberately not one of them.</b> Knowledge
    /// outlives the places it was learned in: what the party knows about a place is a fact about the party,
    /// and a world that stopped carrying a region is a reason to leave the note standing rather than to
    /// refuse the expedition that holds it.
    /// </para>
    /// </remarks>
    /// <param name="elapsedMilliseconds">The game time the save's clock had reached.</param>
    /// <param name="limit">How many facts a knowledge this build keeps may hold.</param>
    /// <returns>Every problem found, in the order the facts are recorded.</returns>
    public IReadOnlyList<SaveProblem> Problems(long elapsedMilliseconds, int limit)
    {
        List<SaveProblem> problems = [];
        if (Notes.Count > limit)
        {
            problems.Add(new SaveProblem(
                SaveCodes.SaveKnowledgeOversize,
                string.Empty,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the party knows {Notes.Count} facts and this build keeps at most {limit}, so the document was written by something that does not bound what a party knows")));
        }

        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (KnowledgeNoteSave note in Notes)
        {
            if (note.Kind is not ("effect" or "clue" or "recipe" or "find"))
            {
                problems.Add(new SaveProblem(SaveCodes.SaveKnowledgeKindUnknown, note.Text ?? string.Empty, $"a knowledge note is recorded as '{note.Kind}', which is not a kind this build has"));
            }

            if (string.IsNullOrWhiteSpace(note.Source) || string.IsNullOrWhiteSpace(note.Subject) || string.IsNullOrWhiteSpace(note.Text))
            {
                problems.Add(new SaveProblem(
                    SaveCodes.SaveKnowledgeEmpty,
                    note.Text ?? string.Empty,
                    $"a knowledge note of kind '{note.Kind}' records no source, subject, or words, so it would read as an empty note"));
            }

            if (note.ElapsedMilliseconds < 0 || note.ElapsedMilliseconds > elapsedMilliseconds)
            {
                problems.Add(new SaveProblem(
                    SaveCodes.SaveKnowledgeFuture,
                    note.Text ?? string.Empty,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"a knowledge note was learned {note.ElapsedMilliseconds} ms into the session and the save had reached {elapsedMilliseconds} ms, so it is dated in a future the party never lived")));
            }

            string identity = $"{note.Kind}|{note.Subject}|{note.Place}";
            if (!seen.Add(identity))
            {
                problems.Add(new SaveProblem(SaveCodes.SaveKnowledgeTwice, note.Text ?? string.Empty, $"the party knows '{note.Text}' twice, so the same fact would be two notes"));
            }
        }

        return problems;
    }
}
