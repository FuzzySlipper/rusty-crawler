using PartyRpg.Kit.Party;

namespace PartyRpg.Kit.Magic;

/// <summary>
/// What this game answers about its magic: which spells content declares, what one costs the caster casting
/// it, how much a member can hold, and whether a member may learn one.
/// </summary>
/// <remarks>
/// <para>
/// This is the ruleset's whole contribution to magic as a catalog, and it is deliberately four answers
/// rather than one. <see cref="Catalog"/> is content's rows as this game reads them.
/// <see cref="SpellPointCapacity"/> is the pool: what a member's class, level, and scores add up to, which
/// the shipped spell table does not state at all. <see cref="CostFor"/> is what one casting takes out of
/// that pool, which this game's own table states per spell and per mastery rung.
/// <see cref="MayLearn"/> is the learning rule a counter's lesson and a found book both judge a character
/// against.
/// </para>
/// <para>
/// <b>The numbers are the ruleset's; the arithmetic of applying them is the kit's.</b> Nothing here changes
/// anything: the casting workflow asks, spends through the member's own pool, and reports. A rule that spent
/// a spell point itself would be a second writer of one fact.
/// </para>
/// <para>
/// <b>A catalog is optional.</b> A ruleset that answers no magic policy composes a session that still walks,
/// fights, and trades; what such a session cannot do is cast, and its projection says so rather than showing
/// a spellbook nothing could cast from.
/// </para>
/// </remarks>
public interface ISpellRule
{
    /// <summary>The spells content declares, as this game reads them.</summary>
    SpellCatalog Catalog { get; }

    /// <summary>
    /// What one member can hold of what casting spends, derived from the class, the level, and the scores.
    /// </summary>
    /// <remarks>
    /// The capacity travels as a whole member rather than as a class because this game's pool is a function
    /// of the class <em>and</em> the attributes the class draws its magic from: a class that casts from
    /// intellect, one that casts from personality, and one that casts from both are three different
    /// functions over the same scores, and a rule that could see only a class could state none of them.
    /// </remarks>
    /// <param name="member">The member whose capacity is being read.</param>
    /// <returns>How many spell points the member holds when full, never negative.</returns>
    int SpellPointCapacity(PartyMember member);

    /// <summary>What one casting of a spell costs the member casting it, at the mastery that member holds.</summary>
    /// <remarks>
    /// Asked of the member rather than of the definition alone, because this game's own table prices a spell
    /// per rung of its school's ladder: the same spell costs one caster the novice's price and a grand
    /// master the grand master's, and one of them may be free. A spell the member's mastery does not reach
    /// still answers — a price is a fact about the table, not a permission.
    /// </remarks>
    /// <param name="member">The member who would cast it.</param>
    /// <param name="spell">The spell being priced.</param>
    /// <returns>What the casting costs in spell points, never negative.</returns>
    int CostFor(PartyMember member, SpellDefinition spell);

    /// <summary>
    /// Whether a member may learn a spell, or why they may not.
    /// </summary>
    /// <remarks>
    /// The learning rule is this game's: a spell is learned from the book that carries it, and the character
    /// must know the school's skill at all and stand at the rung the spell asks for. A spell already in the
    /// character's spellbook is refused here as well, so a counter, a found book, and a test all read one
    /// answer rather than three.
    /// </remarks>
    /// <param name="member">The member the spell would be learned by.</param>
    /// <param name="spell">The spell being learned.</param>
    /// <returns>The refusal, or null when the member may learn it.</returns>
    SpellRefusal? MayLearn(PartyMember member, SpellDefinition spell);
}
