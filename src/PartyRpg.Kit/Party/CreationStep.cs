namespace PartyRpg.Kit.Party;

/// <summary>Where the character being created stands in creation's step sequence.</summary>
/// <remarks>
/// The order is the game's, not an implementation detail: a portrait decides the race and therefore the
/// attribute table, a class decides which skills may be chosen, and the points can only be spent once both
/// are known. Each step is confirmed before the next begins, and a step that is left unfinished is refused by
/// name rather than carried to the end.
/// </remarks>
public enum CreationStep
{
    /// <summary>The player is choosing the portrait, which is what decides the character's race.</summary>
    Portrait,

    /// <summary>The player is choosing the class, which is what decides the skills on offer.</summary>
    Class,

    /// <summary>The player is naming the character.</summary>
    Name,

    /// <summary>The player is spending the attribute pool on the race's attributes.</summary>
    Attributes,

    /// <summary>The player is choosing the skills the class does not fix.</summary>
    Skills,

    /// <summary>The character is finished and confirmed; creation may move on to the next member.</summary>
    Complete,
}
