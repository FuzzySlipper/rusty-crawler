using PartyRpg.Kit.Party;

namespace PartyRpg.Kit.Skills;

/// <summary>
/// What this game answers about its skills: which rows content declares, how far a character may take one,
/// what a raise costs, and what each rung of a ladder is called.
/// </summary>
/// <remarks>
/// <para>
/// This is the ruleset's whole contribution to skills as a catalog, and it is deliberately four answers
/// rather than one. <see cref="Catalog"/> is content's rows as this game reads them, unused rows included.
/// <see cref="Ceiling"/> is the class and rank table: the level a skill may reach and the rungs open to it,
/// which is the fact the shipped data does not carry at all and every ruleset must therefore author.
/// <see cref="RaiseCost"/> is the price of training a skill with the points a level granted, which is the
/// one thing skill points are spent on. <see cref="TierName"/> is presentation meaning: what a person reads
/// for a rung, because the rungs are this game's words and not the kit's.
/// </para>
/// <para>
/// <b>The numbers are the ruleset's; the arithmetic of applying them is the kit's.</b> Nothing here is
/// called to change anything: <see cref="Progression.PartyProgression"/> asks, and the owner writes. A rule
/// that raised a skill itself would be a second writer of one fact, which is exactly what the one spend
/// path exists to prevent.
/// </para>
/// <para>
/// <b>A catalog is optional.</b> A ruleset that answers no skill policy composes a session whose party can
/// still be created, walked, and fought with; what such a session cannot do is raise a skill, because
/// nothing states how far one may go or what it costs. That is a state the panel reports rather than a
/// crash, exactly as a session without a progression rule reports no progression.
/// </para>
/// </remarks>
public interface ISkillRule
{
    /// <summary>The skills content declares, as this game reads them.</summary>
    SkillCatalog Catalog { get; }

    /// <summary>
    /// How far one member's class and rank let one skill grow.
    /// </summary>
    /// <remarks>
    /// The member travels whole because the ceiling is a function of the class, the rank, and the skill, and
    /// a ruleset may read any of the three — a class that may never learn a skill, a rank that unlocks a
    /// rung, a skill whose ladder is shorter than the rest. A skill the member has not learned is still
    /// answerable: what a character may become is a question about the class and rank, not about what they
    /// happen to hold today.
    /// </remarks>
    /// <param name="member">The member whose class and rank the ceiling is read for.</param>
    /// <param name="skill">The skill being asked about.</param>
    /// <returns>The highest level and rung that member's class and rank permit.</returns>
    SkillCeiling Ceiling(PartyMember member, SkillId skill);

    /// <summary>What raising one learned skill by a number of levels costs in skill points.</summary>
    /// <remarks>
    /// Asked of the entry rather than of the member, because the price is a function of how far the skill
    /// has already come: a game charges the next level's own number, so raising a skill twice costs what the
    /// two levels are worth rather than twice what the first one was.
    /// </remarks>
    /// <param name="skill">The entry the raise starts from.</param>
    /// <param name="levels">How many levels the raise adds, which is at least one.</param>
    /// <returns>How many skill points the raise costs.</returns>
    int RaiseCost(SkillEntry skill, int levels);

    /// <summary>What one rung of a skill's ladder is called, as a person reads it.</summary>
    /// <remarks>
    /// The kit's tier is a rung number and carries no word, because a ladder's names belong to the game that
    /// has them. An untrained rung still has to answer, so a rule states what nothing-known reads as rather
    /// than the panel inventing a word for it.
    /// </remarks>
    /// <param name="tier">The rung to name.</param>
    /// <returns>The word a person reads for that rung.</returns>
    string TierName(SkillTier tier);
}
