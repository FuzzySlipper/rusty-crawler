using PartyRpg.Kit.Party;

namespace PartyRpg.Kit.Presentation;

/// <summary>One member's character page, as the game's sheet reads them.</summary>
/// <param name="Index">The member's place in the party, counted from zero.</param>
/// <param name="Member">The member's durable identity.</param>
/// <param name="Name">What the member is called.</param>
/// <param name="Sections">The sheet's sections, in the game's order.</param>
public sealed record CharacterMemberSnapshot(int Index, string Member, string Name, IReadOnlyList<CharacterSheetSection> Sections)
{
    internal uint Write(UiValueBuilder builder) =>
        builder.Object(
            ("index", builder.Number(Index)),
            ("member", builder.String(Member)),
            ("name", builder.String(Name)),
            ("sections", builder.Array([.. Sections.Select(section => builder.Object(
                ("title", builder.String(section.Title)),
                ("rows", builder.Array([.. section.Rows.Select(row => builder.Object(
                    ("label", builder.String(row.Label)),
                    ("value", builder.String(row.Value)),
                    ("detail", builder.String(row.Detail))))]))))])));
}

/// <summary>Every member's character page, or the no-sheet value for a session whose game states none.</summary>
/// <param name="Available">Whether the session's game reads a sheet at all.</param>
/// <param name="Members">Each member's page, in party order.</param>
public sealed record CharacterSnapshot(bool Available, IReadOnlyList<CharacterMemberSnapshot> Members)
{
    /// <summary>The character block of a session with no party or no sheet.</summary>
    public static CharacterSnapshot None { get; } = new(false, []);

    /// <summary>Reads every member's sheet through the game's rule.</summary>
    /// <param name="party">The party, when the session has one.</param>
    /// <param name="sheet">The game's sheet, or null when it states none.</param>
    public static CharacterSnapshot From(PartyEntity? party, ICharacterSheetRule? sheet)
    {
        if (party is null || sheet is null) return None;
        return new CharacterSnapshot(true, [.. party.Members.Select((member, index) =>
            new CharacterMemberSnapshot(index, member.Id.ToString(), member.Profile.Name, sheet.Read(member)))]);
    }

    /// <summary>Writes the character block.</summary>
    internal uint Write(UiValueBuilder builder) =>
        builder.Object(
            ("available", builder.Boolean(Available)),
            ("members", builder.Array([.. Members.Select(member => member.Write(builder))])));
}
