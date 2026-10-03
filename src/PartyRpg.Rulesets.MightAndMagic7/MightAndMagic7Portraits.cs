using PartyRpg.Kit.Content;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// The images this game draws its portraits with: a member's creation portrait as one of the installed face sets, and a
/// person's portrait as the interface image of that name.
/// </summary>
/// <remarks>
/// <para>
/// The face sets run in the donor's order (OpenEnroth <c>src/Engine/mm7_data.cpp:50-55</c>, <c>pPlayerPortraitsNames</c>),
/// faces 0 to 7 human, 8 to 11 elf, 12 to 15 dwarf and 16 to 19 goblin (<c>src/Engine/Objects/Character.cpp:2779-2791</c>),
/// men before women within each race (<c>Character.cpp:2808-2834</c>, the voice table). Each creation portrait is drawn
/// with the first face of its race and sex; which of a group's faces it is, is ours, as the portraits themselves are.
/// </para>
/// <para>
/// A neutral frame is the face set's name and <c>01</c>, and the importer writes it as an <c>icon</c> beside the
/// interface images; a person's portrait already names its interface image (<c>npc</c> and three digits).
/// </para>
/// </remarks>
internal sealed class MightAndMagic7Portraits
{
    /// <summary>The content kind the importer writes interface images as.</summary>
    internal const string IconDefinitionKind = "icon";

    /// <summary>Each creation portrait's face, as the first face of its race and sex in the donor's order.</summary>
    private static readonly Dictionary<string, string> Faces = new(StringComparer.Ordinal)
    {
        ["human-man"] = "pc01-01",
        ["human-woman"] = "pc05-01",
        ["elf-man"] = "pc09-01",
        ["elf-woman"] = "pc11-01",
        ["dwarf-man"] = "pc1301",
        ["dwarf-woman"] = "pc1501",
        ["goblin-man"] = "pc17-01",
        ["goblin-woman"] = "pc1901",
    };

    private readonly Dictionary<string, string> _icons = new(StringComparer.OrdinalIgnoreCase);

    private MightAndMagic7Portraits()
    {
    }

    /// <summary>Reads where each interface image lies beside its pack.</summary>
    internal static MightAndMagic7Portraits Read(ContentCatalog? catalog)
    {
        MightAndMagic7Portraits portraits = new();
        if (catalog is null) return portraits;
        foreach ((LoadedPack pack, _, ContentEntry entry) in catalog.Entries(IconDefinitionKind))
        {
            if (entry.Payload.TryGetProperty("path", out var path) && path.GetString() is { Length: > 0 } relative)
                portraits._icons.TryAdd(entry.Id, $"{pack.Directory}/{relative}");
        }

        return portraits;
    }

    /// <summary>The content path a portrait is drawn with, or null when content carries no image for it.</summary>
    internal string? PathOf(string portrait) =>
        _icons.GetValueOrDefault(Faces.GetValueOrDefault(portrait) ?? portrait);
}
