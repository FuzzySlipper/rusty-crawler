namespace PartyRpg.Kit.Party;

/// <summary>
/// What this game makes of a character whose health has just been taken below what they had: which
/// condition the wound leaves, if any.
/// </summary>
/// <remarks>
/// <para>
/// <b>Damage produces conditions, and this is where that is answered.</b> The kit holds a character's pools
/// and their conditions and states no rule between them: what counts as a wound a character walks off, what
/// leaves them unconscious, and how deep a wound has to be before it is death are this game's calls. A
/// character's health therefore asks this one question every time harm lands, whichever way it landed — a
/// creature's bite, a sprung trap, a fall — so the same wound leaves the same state however it was taken.
/// </para>
/// <para>
/// <b>The deficit is stated, not inferred.</b> A pool stops at empty, so the harm that went past it would be
/// lost; the caller that applied it knows how far past empty it went and hands that over, which is what
/// makes a threshold below zero expressible at all.
/// </para>
/// </remarks>
public interface ICharacterHealthRule
{
    /// <summary>What a wound leaves on a character, and what it moves past.</summary>
    /// <param name="member">The character the harm landed on, whose own numbers the rule reads.</param>
    /// <param name="hitPoints">What the character has left after the harm, which is zero at or below empty.</param>
    /// <param name="deficit">How much harm went past empty, which is zero while the pool had room.</param>
    /// <returns>The condition the wound leaves and the one it ends, or <see cref="CharacterCollapse.None"/>.</returns>
    CharacterCollapse Collapse(PartyMember member, int hitPoints, int deficit);
}
