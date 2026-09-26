using PartyRpg.Kit.Party;

namespace PartyRpg.Kit.Skills;

/// <summary>One row of a game's skill list, as this game reads it: which skill it is and what block it is in.</summary>
/// <remarks>
/// The identity is content's — a skill is the row the shipped table declares — and the block is the
/// ruleset's reading of that row. A definition carries no ceiling, no price, and no tier semantics: those
/// depend on who is asking, and they are answered by <see cref="ISkillRule"/> over the member the question
/// is about.
/// </remarks>
/// <param name="Id">Which skill definition this row is, as content names it.</param>
/// <param name="Block">Which block of the game's skill list the row belongs to.</param>
public readonly record struct SkillDefinition(SkillId Id, SkillBlock Block)
{
    /// <summary>Whether this game's own blocks carry the row.</summary>
    /// <remarks>
    /// A row that belongs to no block is one the shipped table carries and the game does not use. Nothing
    /// offers it, nobody may learn it, and it is reported rather than hidden, because an operator checking
    /// an import needs to see the difference between a row this product dropped and a row the game never
    /// had.
    /// </remarks>
    public bool IsUsed => Block != SkillBlock.Unused;
}
