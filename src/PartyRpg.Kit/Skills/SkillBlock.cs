namespace PartyRpg.Kit.Skills;

/// <summary>Which block of a game's skill list a skill belongs to.</summary>
/// <remarks>
/// <para>
/// The blocks are the kit's own four-part division of a skill list, because every game in this family reads
/// its skills that way: what a character wields, what a character wears, the schools a caster learns, and
/// everything else. Which block a skill actually belongs to is the ruleset's reading of its own content —
/// the kit states the vocabulary and never a skill's name.
/// </para>
/// <para>
/// <b><see cref="Unused"/> is a reading, not a fifth block.</b> A shipped table can carry rows the game
/// itself does not use — its own descriptions say so, or no class may ever learn them — and such a row is
/// still a row of the catalog: it is published, counted, and reported as belonging to no block rather than
/// deleted from the data. A catalog therefore answers both "what is in this game" and "what the table
/// carries but the game does not", which is exactly the distinction an operator checking an import wants.
/// </para>
/// </remarks>
public enum SkillBlock
{
    /// <summary>A weapon skill: what a character wields.</summary>
    Weapon,

    /// <summary>An armour skill: what a character wears.</summary>
    Armour,

    /// <summary>A magic skill: a school whose spells the character casts.</summary>
    Magic,

    /// <summary>A miscellaneous skill: everything the other three blocks are not.</summary>
    Miscellaneous,

    /// <summary>A shipped row this game's blocks do not carry, which nothing offers and nobody may learn.</summary>
    Unused,
}
