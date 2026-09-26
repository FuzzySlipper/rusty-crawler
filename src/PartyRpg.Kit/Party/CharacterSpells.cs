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
    /// <param name="quickSpell">
    /// The spell the character casts with one key, or null when they have chosen none. It must be a spell the
    /// character knows: a quick spell nobody has learned would be a slot nothing could ever cast from.
    /// </param>
    /// <exception cref="ArgumentException">A spell is listed twice, or the quick spell is one the character does not know.</exception>
    public CharacterSpells(IEnumerable<SpellId>? known = null, SpellId? quickSpell = null)
    {
        _known = [];
        if (known is not null)
        {
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

        if (quickSpell is { } quick)
        {
            if (!_known.Contains(quick))
            {
                throw new ArgumentException(
                    $"Spell '{quick}' is chosen as the quick spell and is not in the spellbook, so the slot would hold a spell its owner never learned.",
                    nameof(quickSpell));
            }

            QuickSpell = quick;
        }
    }

    /// <summary>The spells the character knows, in the order they were learned.</summary>
    public IReadOnlyList<SpellId> Known => _known;

    /// <summary>
    /// The spell this character casts with one key, or null when they have chosen none.
    /// </summary>
    /// <remarks>
    /// One slot per character rather than one for the party, because the quick spell is cast at the caster's
    /// own mastery and paid for out of the caster's own pool. It is spellbook state rather than a screen's
    /// choice: a save carries it, and a character who forgets its spell also loses the slot.
    /// </remarks>
    public SpellId? QuickSpell { get; private set; }

    /// <summary>How many spells the character knows.</summary>
    public int Count => _known.Count;

    /// <summary>Whether the character knows a spell.</summary>
    /// <param name="spell">The spell to look for.</param>
    public bool Knows(SpellId spell) => _known.Contains(spell);

    /// <summary>Chooses the spell the character casts with one key, or clears the slot.</summary>
    /// <param name="spell">The spell to keep in the slot, or null to keep none.</param>
    /// <returns>Whether the slot changed.</returns>
    /// <exception cref="ArgumentException">The spell is one the character does not know.</exception>
    public bool SetQuickSpell(SpellId? spell)
    {
        if (spell is { } chosen && !_known.Contains(chosen))
        {
            throw new ArgumentException(
                $"Spell '{chosen}' is not in the spellbook, so it cannot be the quick spell: a slot holds what its owner can cast.",
                nameof(spell));
        }

        if (QuickSpell == spell) return false;
        QuickSpell = spell;
        return true;
    }

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
    public bool Forget(SpellId spell)
    {
        // What is forgotten cannot stay in the quick slot: the slot is a spell the character can cast, and a
        // forgotten one would leave a key that refuses every press.
        if (QuickSpell == spell) QuickSpell = null;
        return _known.Remove(spell);
    }
}
