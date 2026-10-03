using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Party;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// What a character resists before anything they wear or anything running on them: their race, and a Lich's
/// own body.
/// </summary>
/// <remarks>
/// <para>
/// The donor's base resistance (OpenEnroth <c>src/Engine/Objects/Character.cpp:1900-1942</c>,
/// <c>GetBaseResistance</c>) is the character's own stored base, a racial bonus, and what worn items'
/// enchantments add, capped at two hundred for a Lich. This is the first two; the enchantments wait for item
/// enchantments (#8513) and are the combat sum's own line when they come.
/// </para>
/// <para>
/// <b>The racial bonus is a ruleset table, not content.</b> The shipped data carries no race table at all
/// (<see cref="MightAndMagic7CreationTables"/>), and the bonuses live only in the executable, which the donor
/// transcribes as code: five fire and five air for a goblin, five water and five earth for a dwarf, ten mind for
/// an elf, and five body for a human — and body is also what spirit reads, so a human has five spirit too
/// (<c>:1927-1931</c>). Faithful. The races are the creation table's own ids, read without regard to case as
/// content writes them; a race the table does not know has no bonus.
/// </para>
/// <para>
/// <b>The stored base.</b> Every character the donor creates starts it at nothing (<c>:6728-6738</c>). What
/// raises it is a map event's permanent resistance (<c>:4788-4817</c>), the genie lamp used in the last month of the
/// year (<c>:3474-3512</c>), and
/// becoming a Lich, which lifts the four elements to at least twenty and sets mind and body to two hundred
/// (<c>:4025-4042</c>). This build stores the base on the member (<see cref="CharacterResistances"/>, carried in
/// the save): a map event's permanent resistance writes it (<see cref="MightAndMagic7Fixtures"/>); this build's
/// ordinary item use grants the lamp's approximate resistance gift through that same owner. A Lich's stored base is read with its promotion's figures as a
/// floor — the four elements at least twenty, mind, body and spirit at least two hundred — rather than as values
/// the promotion wrote once, because the class is what this build keeps of having become one. Faithful for every
/// character the donor would leave at its stored figures; a Lich's gift below the floor is lost in the floor, which
/// the donor would add on top of it — an approximation.
/// </para>
/// </remarks>
internal static class MightAndMagic7BaseResistance
{
    /// <summary>The class whose body resists as the donor's undead promotion leaves it.</summary>
    internal const string LichClass = "Lich";

    /// <summary>The most a Lich resists of anything, base or in all.</summary>
    /// <remarks>OpenEnroth <c>src/Engine/Objects/Character.cpp:1938-1940</c> and <c>:1988-1990</c>.</remarks>
    internal const int LichCeiling = 200;

    /// <summary>What a Lich's stored base is lifted to in each of the four elements.</summary>
    /// <remarks>OpenEnroth <c>src/Engine/Objects/Character.cpp:4037-4040</c>.</remarks>
    private const int LichElementFloor = 20;

    /// <summary>What a Lich's stored base is set to in mind and body.</summary>
    /// <remarks>OpenEnroth <c>src/Engine/Objects/Character.cpp:4041-4042</c>.</remarks>
    private const int LichMindAndBody = 200;

    /// <summary>Each race's bonus, by the kind of harm it resists.</summary>
    /// <remarks>OpenEnroth <c>src/Engine/Objects/Character.cpp:1906-1931</c>.</remarks>
    private static readonly Dictionary<string, (DamageKindId Kind, int Points)[]> Racial =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Goblin"] = [(MightAndMagic7Damage.Fire, 5), (MightAndMagic7Damage.Air, 5)],
            ["Dwarf"] = [(MightAndMagic7Damage.Water, 5), (MightAndMagic7Damage.Earth, 5)],
            ["Elf"] = [(MightAndMagic7Damage.Mind, 10)],
            ["Human"] = [(MightAndMagic7Damage.Body, 5), (MightAndMagic7Damage.Spirit, 5)],
        };

    /// <summary>The races the table states a bonus for, which a test holds to the races creation offers.</summary>
    internal static IEnumerable<string> Races => Racial.Keys;

    /// <summary>Whether a member is of the class whose resistance is capped.</summary>
    internal static bool IsLich(PartyMember member) =>
        string.Equals(member.Profile.Class.Value, LichClass, StringComparison.OrdinalIgnoreCase);

    /// <summary>What a member resists of one kind of harm before anything worn or running.</summary>
    /// <param name="member">The member.</param>
    /// <param name="kind">The kind of harm.</param>
    /// <returns>The stored base and the racial bonus, capped for a Lich.</returns>
    internal static int Of(PartyMember member, DamageKindId kind)
    {
        ArgumentNullException.ThrowIfNull(member);
        bool lich = IsLich(member);
        // Spirit reads body's stored figure, as it reads body's racial bonus (Character.cpp:1927-1931).
        DamageKindId stored = kind == MightAndMagic7Damage.Spirit ? MightAndMagic7Damage.Body : kind;
        int points = Stored(lich, kind, member.Resistances.Of(stored)) + RacialBonus(member.Profile.Race, kind);
        return lich ? Math.Min(points, LichCeiling) : points;
    }

    /// <summary>What one race adds to one kind of harm.</summary>
    internal static int RacialBonus(RaceId race, DamageKindId kind)
    {
        if (!Racial.TryGetValue(race.Value, out (DamageKindId Kind, int Points)[]? bonuses)) return 0;
        foreach ((DamageKindId bonusKind, int points) in bonuses)
        {
            if (bonusKind == kind) return points;
        }

        return 0;
    }

    /// <summary>The stored base: what the member keeps, lifted to the floor becoming a Lich sets.</summary>
    private static int Stored(bool lich, DamageKindId kind, int stored)
    {
        if (!lich) return stored;
        if (kind == MightAndMagic7Damage.Fire || kind == MightAndMagic7Damage.Air ||
            kind == MightAndMagic7Damage.Water || kind == MightAndMagic7Damage.Earth)
        {
            return Math.Max(stored, LichElementFloor);
        }

        // Spirit reads body's stored base in the donor (Character.cpp:1927-1931).
        return kind == MightAndMagic7Damage.Mind || kind == MightAndMagic7Damage.Body || kind == MightAndMagic7Damage.Spirit
            ? Math.Max(stored, LichMindAndBody)
            : stored;
    }
}
