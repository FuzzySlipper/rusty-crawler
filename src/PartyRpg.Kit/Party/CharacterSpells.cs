namespace PartyRpg.Kit.Party;

/// <summary>The spells one character knows.</summary>
/// <remarks>
/// Knowing a spell and being able to cast it are different questions: schools, costs, mastery requirements,
/// and spell points all belong to the ruleset and the casting workflow. This records what the character has
/// learned, in the order it was learned, so a spellbook can be shown and a save round-tripped without the
/// kit learning a single spell.
/// </remarks>
public sealed class CharacterSpells
{
    private readonly List<SpellId> _known;

    /// <summary>Creates a character's spellbook.</summary>
    /// <param name="known">The spells the character knows, in the order they were learned.</param>
    /// <exception cref="ArgumentException">A spell is listed twice.</exception>
    public CharacterSpells(IEnumerable<SpellId>? known = null)
    {
        _known = [];
        if (known is null) return;

        HashSet<SpellId> seen = [];
        foreach (SpellId spell in known)
        {
            if (!seen.Add(spell))
            {
                throw new ArgumentException(
                    $"Spell '{spell}' is listed more than once, so the spellbook would show it twice.",
                    nameof(known));
            }

            _known.Add(spell);
        }
    }

    /// <summary>The spells the character knows, in the order they were learned.</summary>
    public IReadOnlyList<SpellId> Known => _known;

    /// <summary>How many spells the character knows.</summary>
    public int Count => _known.Count;

    /// <summary>Whether the character knows a spell.</summary>
    /// <param name="spell">The spell to look for.</param>
    public bool Knows(SpellId spell) => _known.Contains(spell);

    /// <summary>Records that the character has learned a spell.</summary>
    /// <param name="spell">The spell to learn.</param>
    /// <returns>Whether the spellbook changed; learning a spell already known changes nothing.</returns>
    public bool Learn(SpellId spell)
    {
        if (_known.Contains(spell)) return false;
        _known.Add(spell);
        return true;
    }

    /// <summary>Removes a spell from the spellbook, which a game that lets magic be forgotten does.</summary>
    /// <param name="spell">The spell to forget.</param>
    /// <returns>Whether the spellbook changed.</returns>
    public bool Forget(SpellId spell) => _known.Remove(spell);
}
