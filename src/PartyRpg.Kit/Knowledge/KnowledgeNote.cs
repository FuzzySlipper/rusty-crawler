using PartyRpg.Kit.Time;

namespace PartyRpg.Kit.Knowledge;

/// <summary>What kind of thing one note records: what the party learned, not where it learned it.</summary>
/// <remarks>
/// <para>
/// These are the discoveries a party's own knowledge is made of. A note is kept because the fact stays
/// true — a mixture that makes a potion keeps making it, a landmark that gives something keeps giving it —
/// which is what separates this owner from the journal beside it: a journal line records <em>when</em>
/// something happened, and a note records <em>what is known</em> and can be looked up again.
/// </para>
/// <para>
/// The four kinds are deliberately about the fact rather than about the object. A fountain, a well, an
/// altar, and a talking stone are all fixtures, and what the party keeps is what each one gives or says;
/// the kit therefore names no game object here, exactly as the journal names no quest, place, or person.
/// </para>
/// <para>
/// <b>Kind is what makes two reports different facts.</b> A recipe, a fixture's effect, and something
/// written that the party read are learned about different things even when they happen in one place, so
/// the kind is part of a note's identity rather than a label a screen adds.
/// </para>
/// </remarks>
public enum KnowledgeKind
{
    /// <summary>What using a fixture gives or does, learned by using it.</summary>
    Effect,

    /// <summary>Something written that the party read and remembers.</summary>
    Clue,

    /// <summary>A mixture the party made and can make again.</summary>
    Recipe,

    /// <summary>Something notable the party found.</summary>
    Find,
}

/// <summary>One thing the party learned, as the owner of the moment states it.</summary>
/// <remarks>
/// <para>
/// <b>The reporter states what was learned; this owner decides whether it is new and when it was.</b> An
/// owner hands over the kind of thing learned, who reported it, what it is about, what it is called in that
/// owner's own words, and where it happened — and nothing else. The date is this owner's to read from the
/// one clock, so no reporter can date a note from a clock of its own, and how a note reads is the
/// ruleset's, so no kit mechanism carries a game's vocabulary.
/// </para>
/// <para>
/// <b>A reporter reports every time it learns the fact and never checks whether it is news.</b> A party
/// that mixes the same pair twice, searches the same chest twice, or is told the same thing again reports
/// it again; the knowledge owner is the one place that decides that the second report is already known.
/// That is what keeps "learning the same fact twice is one fact" in one owner rather than in every
/// reporter.
/// </para>
/// <para>
/// <b>A fact learned about a place is about that place; a fact learned in general names none.</b> A
/// recipe is knowledge of the pair and not of where the mixing happened, so its report names no place and
/// two mixes in two rooms are one fact; a fixture's effect belongs to the fixture standing somewhere, so
/// its report names both and the same gift from two landmarks is two facts.
/// </para>
/// </remarks>
/// <param name="Kind">What kind of thing was learned.</param>
/// <param name="Source">Which owner reported it, as a note attributes it: the mechanism or workflow that learned it.</param>
/// <param name="Subject">The identity of what it is about, which is what makes one fact one note.</param>
/// <param name="Name">What it is called, in the reporting owner's own words.</param>
/// <param name="Place">The place it was learned in, or empty when it was learned nowhere in particular.</param>
/// <exception cref="ArgumentException">The source, the subject, or the name is blank.</exception>
public readonly record struct KnowledgeReport(
    KnowledgeKind Kind,
    string Source,
    string Subject,
    string Name,
    string Place = "")
{
    /// <summary>Which owner reported it, which is what a note is attributable to.</summary>
    public string Source { get; } = Require(Source, nameof(Source));

    /// <summary>The identity of what it is about.</summary>
    public string Subject { get; } = Require(Subject, nameof(Subject));

    /// <summary>What it is called, in the reporting owner's own words.</summary>
    public string Name { get; } = Require(Name, nameof(Name));

    /// <summary>The place it was learned in, or empty when it was learned nowhere in particular.</summary>
    public string Place { get; } = Place ?? string.Empty;

    /// <summary>Whether this reports the same fact as another report.</summary>
    /// <remarks>
    /// The same kind of thing learned about the same subject in the same place is one fact. The name is
    /// deliberately not compared: what makes a mixture one recipe is the pair it is made of and not the
    /// wording a screen happens to show for it, and a landmark's effect is one fact however it is phrased.
    /// </remarks>
    /// <param name="other">The other report.</param>
    /// <returns>Whether they are one fact.</returns>
    public bool IsSameFact(KnowledgeReport other) =>
        Kind == other.Kind
        && string.Equals(Subject, other.Subject, StringComparison.Ordinal)
        && string.Equals(Place, other.Place, StringComparison.Ordinal);

    private static string Require(string value, string parameterName) =>
        !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException(
                $"A knowledge report states no {parameterName}, so nothing would say what was learned or what it was about.",
                parameterName);
}

/// <summary>One fact the party knows: what it learned, when, and who reported it.</summary>
/// <remarks>
/// <para>
/// <b>The note is written once and never recomputed.</b> What a person reads is composed when the fact is
/// learned, in the words the game had then, so a renamed item, a place re-titled, or a fixture the world
/// stops carrying cannot rewrite what the party's own knowledge says it found.
/// </para>
/// <para>
/// <b>The durable fact is the elapsed game time; the date is that fact read against the calendar.</b> A
/// save records the elapsed milliseconds since the session began and never a date, for the same reason the
/// clock and the journal are saved that way: a date stored beside a ruleset that composes a different
/// starting date or calendar would silently move when every note the party holds was learned. The date is
/// kept beside the elapsed time so a panel renders a day rather than doing calendar arithmetic, and a load
/// recomposes both from the save and the clock it was resumed on — which is what makes a loaded note read
/// as the day it was learned rather than the day it was loaded.
/// </para>
/// <para>
/// <b>Knowledge is party state and not place state.</b> No note is keyed by where the party stood when it
/// was written, so a place whose population the world restores touches none of them, a party that walks
/// out of the place it learned something in keeps what it learned, and the whole set survives a save
/// because it is the party's own state rather than a reading of the world's.
/// </para>
/// </remarks>
/// <param name="Kind">What kind of thing was learned.</param>
/// <param name="Source">Which owner reported it.</param>
/// <param name="Subject">The identity of what it is about.</param>
/// <param name="Text">The note a person reads, as the game worded it when it was learned.</param>
/// <param name="ElapsedMilliseconds">The game time since the session began at which it was learned.</param>
/// <param name="Date">That game time as a point on the calendar.</param>
/// <param name="Place">The place it was learned in, or empty when it was learned nowhere in particular.</param>
/// <exception cref="ArgumentException">A word is blank.</exception>
/// <exception cref="ArgumentOutOfRangeException">The elapsed game time is negative, which game time never is.</exception>
public sealed record KnowledgeNote(
    KnowledgeKind Kind,
    string Source,
    string Subject,
    string Text,
    long ElapsedMilliseconds,
    GameDate Date,
    string Place = "")
{
    /// <summary>Which owner reported it.</summary>
    public string Source { get; } = !string.IsNullOrWhiteSpace(Source)
        ? Source
        : throw new ArgumentException("A knowledge note names no source, so nothing attributes it.", nameof(Source));

    /// <summary>The identity of what it is about.</summary>
    public string Subject { get; } = !string.IsNullOrWhiteSpace(Subject)
        ? Subject
        : throw new ArgumentException("A knowledge note names no subject, so nothing says what it records.", nameof(Subject));

    /// <summary>The note a person reads.</summary>
    public string Text { get; } = !string.IsNullOrWhiteSpace(Text)
        ? Text
        : throw new ArgumentException("A knowledge note says nothing, so it would read as an empty line.", nameof(Text));

    /// <summary>The game time since the session began at which it was learned.</summary>
    public long ElapsedMilliseconds { get; } = ElapsedMilliseconds >= 0
        ? ElapsedMilliseconds
        : throw new ArgumentOutOfRangeException(
            nameof(ElapsedMilliseconds),
            ElapsedMilliseconds,
            "Game time runs forward, so a note was learned at zero or more milliseconds into the session.");

    /// <summary>The place it was learned in, or empty when it was learned nowhere in particular.</summary>
    public string Place { get; } = Place ?? string.Empty;

    /// <summary>Whether this records the same fact as a report.</summary>
    /// <remarks>
    /// The words the note reads with are deliberately not compared: what makes two reports one fact is the
    /// kind of thing, what it was about, and where — never the phrasing, which is composed once and stays.
    /// </remarks>
    /// <param name="report">The report.</param>
    /// <returns>Whether they are one fact.</returns>
    public bool Records(KnowledgeReport report) =>
        Kind == report.Kind
        && string.Equals(Subject, report.Subject, StringComparison.Ordinal)
        && string.Equals(Place, report.Place, StringComparison.Ordinal);

    /// <inheritdoc />
    public override string ToString() => $"{Date.Year:0000}-{Date.Month:00}-{Date.Day:00} {Text}";
}
