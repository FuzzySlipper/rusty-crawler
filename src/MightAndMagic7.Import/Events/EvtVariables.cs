namespace MightAndMagic7.Import.Events;

/// <summary>What a variable code names: a family word, and which member of the family when it has several.</summary>
/// <param name="Word">The family, for example <c>attribute</c>, <c>quest-bit</c> or <c>map-variable</c>.</param>
/// <param name="Which">
/// The member of the family the code names — <c>luck</c>, <c>fire</c>, <c>poison-weak</c> — or empty when the
/// family has one member or the instruction's value says which.
/// </param>
/// <param name="Index">The slot a numbered family's code names, such as a map variable's, or null.</param>
public readonly record struct EvtVariableName(string Word, string Which, int? Index);

/// <summary>
/// The event language's variable codes, and the word a pack names each by.
/// </summary>
/// <remarks>
/// <para>
/// The codes and their meanings are the donor's own enumeration (OpenEnroth <c>src/Engine/Evt/EvtEnums.h:82-249</c>,
/// <c>EvtVariable</c>); the words are this importer's, chosen so a pack says what a step reads or writes
/// without a reader having to know the byte. A code the table does not name is written as
/// <see cref="UnknownWord"/> with the code beside it, never guessed at.
/// </para>
/// <para>
/// Some families are many codes: the seven attributes, the eleven resistances, the skills, the conditions,
/// the seventy-five map variables. Those are written as one family word with the member beside it, which is
/// the shape the ruleset reads.
/// </para>
/// </remarks>
public static class EvtVariables
{
    /// <summary>The word a code the table does not name is written with.</summary>
    public const string UnknownWord = "unknown";

    /// <summary>The first of the map's own persistent variables (<c>VAR_MapPersistentVariable_0</c>).</summary>
    public const ushort FirstMapVariable = 0x7B;

    /// <summary>The last of them (<c>VAR_MapPersistentVariable_74</c>).</summary>
    public const ushort LastMapVariable = 0xC5;

    /// <summary>The first of the variables a decoration remembers its event in (<c>VAR_MapPersistentDecorVariable_0</c>).</summary>
    public const ushort FirstDecorationVariable = 0xC6;

    /// <summary>The last of them (<c>VAR_MapPersistentDecorVariable_24</c>).</summary>
    public const ushort LastDecorationVariable = 0xDE;

    /// <summary>The attributes, in the donor's order for the bonus, base and actual families.</summary>
    private static readonly string[] Attributes = ["might", "intellect", "personality", "endurance", "speed", "accuracy", "luck"];

    /// <summary>The resistances, in the donor's order for the base and bonus families.</summary>
    private static readonly string[] Resistances = ["fire", "air", "water", "earth", "spirit", "mind", "body", "light", "dark", "physical", "magic"];

    /// <summary>The skills, in the donor's order from <c>VAR_StaffSkill</c> to <c>VAR_LearningSkill</c>.</summary>
    private static readonly string[] Skills =
    [
        "staff", "sword", "dagger", "axe", "spear", "bow", "mace", "blaster", "shield", "leather", "chain", "plate",
        "fire", "air", "water", "earth", "spirit", "mind", "body", "light", "dark", "identify-item", "merchant",
        "repair", "bodybuilding", "meditation", "perception", "diplomacy", "thievery", "disarm-trap", "dodge",
        "unarmed", "identify-monster", "armsmaster", "stealing", "alchemy", "learning",
    ];

    /// <summary>The conditions, in the donor's order from <c>VAR_Cursed</c> to <c>VAR_Eradicated</c>.</summary>
    private static readonly string[] Conditions =
    [
        "cursed", "weak", "asleep", "afraid", "drunk", "insane", "poison-weak", "disease-weak", "poison-medium",
        "disease-medium", "poison-severe", "disease-severe", "paralyzed", "unconscious", "dead", "stoned",
        "eradicated",
    ];

    /// <summary>The single codes, each its own family.</summary>
    private static readonly Dictionary<ushort, string> Singles = new()
    {
        [0x01] = "sex",
        [0x02] = "class",
        [0x03] = "hit-points",
        [0x04] = "full-hit-points",
        [0x05] = "spell-points",
        [0x06] = "full-spell-points",
        [0x07] = "armour-class",
        [0x08] = "armour-class-bonus",
        [0x09] = "level",
        [0x0A] = "level-bonus",
        [0x0B] = "age",
        [0x0C] = "award",
        [0x0D] = "experience",
        [0x0E] = "race",
        [0x10] = "quest-bit",
        [0x11] = "item",
        [0x12] = "hour",
        [0x13] = "day-of-year",
        [0x14] = "day-of-week",
        [0x15] = "gold",
        [0x16] = "random-gold",
        [0x17] = "food",
        [0x18] = "random-food",
        [0x7A] = "major-condition",
        [0xDF] = "autonote",
        [0xE7] = "member-bit",
        [0xE8] = "hireling",
        [0xF0] = "flying",
        [0xF1] = "hireling-speciality",
        [0xF2] = "circus-prizes",
        [0xF3] = "skill-points",
        [0xF4] = "month",
        [0x113] = "reputation",
        [0x131] = "alert",
        [0x132] = "bank-gold",
        [0x133] = "deaths",
        [0x134] = "bounties",
        [0x135] = "prison-terms",
        [0x136] = "arena-wins-page",
        [0x137] = "arena-wins-squire",
        [0x138] = "arena-wins-knight",
        [0x139] = "arena-wins-lord",
        [0x13A] = "invisible",
        [0x13B] = "item-equipped",
    };

    /// <summary>What a variable code names.</summary>
    /// <param name="code">The code.</param>
    /// <returns>The family word, the member, and the slot when the family is numbered.</returns>
    public static EvtVariableName Name(ushort code)
    {
        if (Singles.TryGetValue(code, out string? single)) return new EvtVariableName(single, string.Empty, null);
        return code switch
        {
            >= 0x19 and <= 0x1F => new EvtVariableName("attribute-bonus", Attributes[code - 0x19], null),
            >= 0x20 and <= 0x26 => new EvtVariableName("attribute", Attributes[code - 0x20], null),
            >= 0x27 and <= 0x2D => new EvtVariableName("attribute-actual", Attributes[code - 0x27], null),
            >= 0x2E and <= 0x38 => new EvtVariableName("resistance", Resistances[code - 0x2E], null),
            >= 0x39 and <= 0x43 => new EvtVariableName("resistance-bonus", Resistances[code - 0x39], null),
            >= 0x44 and <= 0x68 => new EvtVariableName("skill", Skills[code - 0x44], null),
            >= 0x69 and <= 0x79 => new EvtVariableName("condition", Conditions[code - 0x69], null),
            >= FirstMapVariable and <= LastMapVariable => new EvtVariableName("map-variable", string.Empty, code - FirstMapVariable),
            >= FirstDecorationVariable and <= LastDecorationVariable => new EvtVariableName("decoration-variable", string.Empty, code - FirstDecorationVariable),
            >= 0xE0 and <= 0xE6 => new EvtVariableName("attribute-raised", Attributes[code - 0xE0], null),
            >= 0xF5 and <= 0xFE => new EvtVariableName("counter", string.Empty, code - 0xF5),
            >= 0xFF and <= 0x112 => new EvtVariableName("timed-event", string.Empty, code - 0xFF),
            >= 0x114 and <= 0x130 => new EvtVariableName("history", string.Empty, code - 0x114),
            _ => new EvtVariableName(UnknownWord, string.Empty, null),
        };
    }

    /// <summary>The word a character choice is written with.</summary>
    /// <remarks>
    /// One of the four characters by position, the active one, the whole party, or one at random
    /// (OpenEnroth <c>src/Engine/Evt/EvtEnums.h:261-270</c>).
    /// </remarks>
    /// <param name="who">The choice byte.</param>
    /// <returns>The word, and the position for a choice of one character.</returns>
    public static (string Word, int? Member) Who(byte who) => who switch
    {
        <= 3 => ("member", who),
        4 => ("active", null),
        5 => ("party", null),
        6 => ("random", null),
        _ => (UnknownWord, null),
    };

    /// <summary>The word a season is written with.</summary>
    /// <remarks>OpenEnroth <c>src/Engine/Evt/EvtEnums.h:252-258</c>, <c>Season</c>.</remarks>
    /// <param name="season">The season's number.</param>
    /// <returns>The word.</returns>
    public static string Season(int season) => season switch
    {
        0 => "spring",
        1 => "summer",
        2 => "autumn",
        3 => "winter",
        _ => UnknownWord,
    };

    /// <summary>The word a damage kind is written with.</summary>
    /// <remarks>OpenEnroth <c>src/Engine/Objects/ItemEnums.h:10-23</c>, <c>DamageType</c>.</remarks>
    /// <param name="kind">The kind byte.</param>
    /// <returns>The word.</returns>
    public static string DamageKind(byte kind) => kind switch
    {
        0 => "fire",
        1 => "air",
        2 => "water",
        3 => "earth",
        4 => "physical",
        5 => "magic",
        6 => "spirit",
        7 => "mind",
        8 => "body",
        9 => "light",
        10 => "dark",
        12 => "energy",
        _ => UnknownWord,
    };
}
