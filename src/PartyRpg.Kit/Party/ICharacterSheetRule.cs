namespace PartyRpg.Kit.Party;

/// <summary>
/// A game's reading of one member as its character screen states them: the scores as they actually read, the
/// resistances, and whatever else the game puts on that page, each row in the game's own words.
/// </summary>
/// <remarks>
/// What a score reads as — with worn items, running effects and age — is decided where the game's rules decide it,
/// so the sheet reads those same rules rather than a second copy of them. The kit publishes the rows and decides
/// nothing about them.
/// </remarks>
public interface ICharacterSheetRule
{
    /// <summary>Reads one member's sheet.</summary>
    /// <param name="member">The member.</param>
    /// <returns>The sheet's sections, in the order the game shows them.</returns>
    IReadOnlyList<CharacterSheetSection> Read(PartyMember member);
}

/// <summary>One section of a character's sheet: its title and its rows.</summary>
/// <param name="Title">What the section is called, such as the scores or the resistances.</param>
/// <param name="Rows">Its rows, in order.</param>
public sealed record CharacterSheetSection(string Title, IReadOnlyList<CharacterSheetRow> Rows);

/// <summary>One row of a character's sheet.</summary>
/// <param name="Label">What the row is, in the game's words.</param>
/// <param name="Value">What it reads as now.</param>
/// <param name="Detail">How that reading came about, such as the carried score beneath it, or empty.</param>
public sealed record CharacterSheetRow(string Label, string Value, string Detail = "");
