using PartyRpg.Kit.Party;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// What this game makes of a character whose health has been taken below what they had.
/// </summary>
/// <remarks>
/// <para>
/// <b>The donor's own two thresholds, and nothing else.</b> OpenEnroth <c>src/Engine/Objects/Character.cpp:
/// 1310-1316</c> (<c>Character::receiveDamage</c>): a character whose health has fallen below one is
/// unconscious while <c>health + GetBaseEndurance() &gt;= 1</c> and dead otherwise. In other words the pool
/// empties into unconsciousness, and the character dies once the harm past empty is deeper than their own
/// base endurance is worth — an endurance of twenty-five is a good deal harder to kill than one of nine.
/// That is why this rule is asked with the deficit rather than only with what the pool has left: a pool
/// stops at zero, and the difference between a character who was emptied and one who was run through lives
/// entirely in the harm that went past it.
/// </para>
/// <para>
/// <b>Eradication is not a depth of damage.</b> The donor sets it from a monster's own attack or a spell
/// (<c>Character.cpp:1568</c>, <c>SPECIAL_ATTACK_ERADICATED</c>), never from hit points, so nothing here
/// invents a third threshold: a character is eradicated by what hit them, which this game's monster table
/// states for six of its rows, and the temple charges ten times as much to undo it.
/// </para>
/// <para>
/// <b>The condition is what ends it, not a removal.</b> A member laid out this way keeps their identity,
/// their place in the roster, and their belongings, and stands in the fight as an actor that cannot act;
/// what brings them back is the temple's cure or the spell that arrives with the schools.
/// </para>
/// </remarks>
internal sealed class MightAndMagic7Health : ICharacterHealthRule
{
    /// <summary>The attribute the donor's death threshold is measured against.</summary>
    /// <remarks>OpenEnroth <c>src/Engine/Objects/Character.cpp:658-660</c> — <c>GetBaseEndurance</c>.</remarks>
    internal static readonly AttributeId EnduranceAttribute = new("Endurance");

    /// <inheritdoc />
    /// <remarks>
    /// The ladder runs one way: a character emptied into unconsciousness who is then taken deeper is dead,
    /// and death is what ends the unconsciousness rather than joining it. Nothing here clears eradication or
    /// petrification, which are not depths of damage at all but what a monster's own attack left.
    /// </remarks>
    public CharacterCollapse Collapse(PartyMember member, int hitPoints, int deficit)
    {
        ArgumentNullException.ThrowIfNull(member);

        // What the donor's `health` would read: the pool stops at empty, so what lies below zero is exactly
        // the harm that went past it.
        int health = hitPoints - deficit;
        if (health >= 1) return CharacterCollapse.None;

        int endurance = MightAndMagic7Combat.AttributeBonus(member.Attributes[EnduranceAttribute]);
        return health + endurance >= 1
            ? new CharacterCollapse(new ActiveCondition(MightAndMagic7Conditions.Unconscious))
            : new CharacterCollapse(new ActiveCondition(MightAndMagic7Conditions.Dead), [MightAndMagic7Conditions.Unconscious]);
    }
}
