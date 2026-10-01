using PartyRpg.Kit.Combat;

namespace PartyRpg.Kit.Party;

/// <summary>What one character resists of one kind of harm by their own nature, stored rather than derived.</summary>
/// <param name="Kind">The kind of harm.</param>
/// <param name="Points">How much of it the character resists, which may be negative when something lowered it.</param>
public readonly record struct ResistanceScore(DamageKindId Kind, int Points);

/// <summary>
/// A character's stored base resistances: what a permanent gift has added to what they resist of each kind of
/// harm, kept on the character and carried in the save.
/// </summary>
/// <remarks>
/// <para>
/// This is the stored half of a character's resistance only. What a race adds, what a class sets as a floor,
/// what worn things and running effects add are the ruleset's terms, read when a rule asks; this keeps the one
/// term nothing else could recompute — a gift the character received once and keeps — so there is no modifier
/// list here to keep in step with its sources.
/// </para>
/// <para>
/// A kind nothing has raised is stored as nothing, which is what every character starts with.
/// </para>
/// </remarks>
public sealed class CharacterResistances
{
    private readonly SortedDictionary<string, ResistanceScore> _scores = new(StringComparer.Ordinal);

    /// <summary>The change stamp this owner last took, which a reading kept beside it compares (<see cref="ChangeStamp"/>).</summary>
    public long Stamp { get; private set; } = ChangeStamp.Next();

    /// <summary>Creates a character's stored resistances.</summary>
    /// <param name="scores">What is stored, or null when nothing is.</param>
    /// <exception cref="ArgumentException">One kind is stored twice, so which figure stands would be a coin toss.</exception>
    public CharacterResistances(IReadOnlyList<ResistanceScore>? scores = null)
    {
        foreach (ResistanceScore score in scores ?? [])
        {
            if (!_scores.TryAdd(score.Kind.Value, score))
            {
                throw new ArgumentException($"The resistance to '{score.Kind}' is stored twice.", nameof(scores));
            }
        }
    }

    /// <summary>Every stored figure, by kind in ordinal order.</summary>
    public IReadOnlyList<ResistanceScore> Scores => [.. _scores.Values];

    /// <summary>What is stored for one kind of harm; nothing for a kind nothing has raised.</summary>
    /// <param name="kind">The kind of harm.</param>
    public int Of(DamageKindId kind) => _scores.TryGetValue(kind.Value, out ResistanceScore score) ? score.Points : 0;

    /// <summary>Stores a new figure for one kind of harm.</summary>
    /// <param name="kind">The kind of harm.</param>
    /// <param name="points">The figure it is stored at afterwards.</param>
    public void Set(DamageKindId kind, int points)
    {
        if (points == 0) _scores.Remove(kind.Value);
        else _scores[kind.Value] = new ResistanceScore(kind, points);
        Stamp = ChangeStamp.Next();
    }
}
