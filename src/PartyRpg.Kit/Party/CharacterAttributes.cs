namespace PartyRpg.Kit.Party;

/// <summary>The scores of one character's attributes, in the order the game declares them.</summary>
/// <remarks>
/// How many attributes a character has and what they are called arrive from creation, so this holds a set
/// rather than a fixed shape: a game with six attributes and a game with eight both fit, and no attribute
/// name appears in the kit. Setting a score an attribute the character does not have fails by name instead
/// of quietly adding a ninth, because a mistyped identity would otherwise become a real attribute nobody
/// ever rolls against.
/// </remarks>
public sealed class CharacterAttributes
{
    private readonly List<AttributeScore> _scores;

    /// <summary>Creates a character's attribute scores.</summary>
    /// <param name="scores">The attributes and their starting values, in the game's own order.</param>
    /// <exception cref="ArgumentException">An attribute is declared twice, so one of the two values could never be read.</exception>
    public CharacterAttributes(IEnumerable<AttributeScore> scores)
    {
        ArgumentNullException.ThrowIfNull(scores);
        _scores = [];
        HashSet<AttributeId> seen = [];
        foreach (AttributeScore score in scores)
        {
            if (!seen.Add(score.Attribute))
            {
                throw new ArgumentException(
                    $"Attribute '{score.Attribute}' is declared more than once, so which score the character has would be ambiguous.",
                    nameof(scores));
            }

            _scores.Add(score);
        }
    }

    /// <summary>The character's attributes and their values, in the order they were created.</summary>
    public IReadOnlyList<AttributeScore> Scores => _scores;

    /// <summary>Reads one attribute's current value.</summary>
    /// <param name="attribute">The attribute to read.</param>
    /// <exception cref="ArgumentException">The character has no such attribute.</exception>
    public int this[AttributeId attribute] =>
        TryGet(attribute, out int value)
            ? value
            : throw new ArgumentException($"The character has no attribute '{attribute}', so it has no value to read.", nameof(attribute));

    /// <summary>Reads one attribute's current value.</summary>
    /// <param name="attribute">The attribute to read.</param>
    /// <param name="value">The attribute's current value, when the character has it.</param>
    /// <returns>Whether the character has that attribute.</returns>
    public bool TryGet(AttributeId attribute, out int value)
    {
        foreach (AttributeScore score in _scores)
        {
            if (score.Attribute != attribute) continue;
            value = score.Value;
            return true;
        }

        value = 0;
        return false;
    }

    /// <summary>Records one attribute's value.</summary>
    /// <param name="attribute">The attribute to set, which the character must have.</param>
    /// <param name="value">The value to record.</param>
    /// <exception cref="ArgumentException">The character has no such attribute.</exception>
    public void Set(AttributeId attribute, int value)
    {
        int index = IndexOf(attribute);
        if (index < 0)
        {
            throw new ArgumentException(
                $"The character has no attribute '{attribute}', so a value for it cannot be recorded.",
                nameof(attribute));
        }

        _scores[index] = new AttributeScore(attribute, value);
    }

    /// <summary>Changes one attribute by a delta, which may be negative.</summary>
    /// <param name="attribute">The attribute to change, which the character must have.</param>
    /// <param name="delta">How much to add to the score.</param>
    /// <exception cref="ArgumentException">The character has no such attribute.</exception>
    /// <exception cref="OverflowException">The change would leave the numbers a score is described in.</exception>
    public void Change(AttributeId attribute, int delta)
    {
        int index = IndexOf(attribute);
        if (index < 0)
        {
            throw new ArgumentException(
                $"The character has no attribute '{attribute}', so it cannot be changed.",
                nameof(attribute));
        }

        _scores[index] = new AttributeScore(attribute, checked(_scores[index].Value + delta));
    }

    private int IndexOf(AttributeId attribute)
    {
        for (int index = 0; index < _scores.Count; index++)
        {
            if (_scores[index].Attribute == attribute) return index;
        }

        return -1;
    }
}
