using PartyRpg.Kit.Party;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>What a monster's own attack can leave on the character it hits.</summary>
/// <remarks>
/// The donor's own list (OpenEnroth <c>src/Engine/Objects/MonsterEnums.h:462-490</c>,
/// <c>enum class MonsterSpecialAttack</c>), which is what the monster table's special-attack column names.
/// The kinds that leave a condition are the ones this game can apply; the rest are named because the table
/// states them and a fight must be able to say what it faced rather than silently ignore a cell.
/// </remarks>
internal enum MightAndMagic7SpecialAttackKind
{
    /// <summary>The row states no special attack.</summary>
    None,

    /// <summary>Curses the character it hits.</summary>
    Curse,

    /// <summary>Leaves the character weak.</summary>
    Weak,

    /// <summary>Puts the character to sleep.</summary>
    Sleep,

    /// <summary>Frightens the character.</summary>
    Fear,

    /// <summary>Makes the character drunk.</summary>
    Drunk,

    /// <summary>Drives the character insane.</summary>
    Insane,

    /// <summary>Poisons the character, mildly.</summary>
    PoisonWeak,

    /// <summary>Poisons the character.</summary>
    PoisonMedium,

    /// <summary>Poisons the character severely.</summary>
    PoisonSevere,

    /// <summary>Diseases the character, mildly.</summary>
    DiseaseWeak,

    /// <summary>Diseases the character.</summary>
    DiseaseMedium,

    /// <summary>Diseases the character severely.</summary>
    DiseaseSevere,

    /// <summary>Paralyses the character.</summary>
    Paralyzed,

    /// <summary>Knocks the character unconscious.</summary>
    Unconscious,

    /// <summary>Kills the character outright.</summary>
    Dead,

    /// <summary>Turns the character to stone.</summary>
    Petrified,

    /// <summary>Eradicates the character, which is the far end of the ladder.</summary>
    Eradicated,

    /// <summary>Breaks an item the character carries, which the items stone owns.</summary>
    BreakAny,

    /// <summary>Breaks the character's armour, which the items stone owns.</summary>
    BreakArmor,

    /// <summary>Breaks the character's weapon, which the items stone owns.</summary>
    BreakWeapon,

    /// <summary>Steals from the character, which the items stone owns.</summary>
    Steal,

    /// <summary>Ages the character, which the progression stone owns.</summary>
    Aging,

    /// <summary>Drains the character's spell points.</summary>
    ManaDrain,
}

/// <summary>One monster row's special attack: what it does, and how strongly.</summary>
/// <param name="Kind">What the attack does to the character it catches.</param>
/// <param name="Level">How strong the case is, which is the table's own <c>x</c> suffix.</param>
internal sealed record MonsterSpecialAttack(MightAndMagic7SpecialAttackKind Kind, int Level);

/// <summary>
/// The monster table's special-attack column, read as the donor reads it.
/// </summary>
/// <remarks>
/// <para>
/// The cell is <c>&lt;name&gt;[x&lt;level&gt;]</c> and defaults to level one
/// (OpenEnroth <c>src/Engine/Objects/Monsters.cpp:354-368</c>, <c>parseSpecialAttack</c>), and its names are
/// read case-insensitively by prefix (<c>Monsters.cpp:176-231</c>, <c>ParseSpecialAttack</c>), which is why
/// the shipped table's <c>Poison1</c>/<c>Disease2</c>/<c>Errad</c>/<c>BrkArmor</c> spellings all resolve.
/// Every non-zero spelling the operator's own table carries is covered here: <c>BrkArmor</c>,
/// <c>Disease1</c>-<c>3</c>, <c>Afraid</c>, <c>Errad</c>, <c>Brkweapon</c>, <c>Paralyze</c>,
/// <c>DrainSP</c>, <c>Insane</c>, <c>BrkItem</c>, <c>Poison1</c>-<c>3</c>, <c>Dead</c>, <c>Stealx2</c>,
/// <c>Asleep</c>, <c>Uncon</c>, <c>Agex2</c>, <c>Stone</c>, <c>Weak</c>, <c>Drunk</c>,
/// <c>Cursex2</c>, <c>Agex3</c>, <c>Poison3x2</c> and <c>Curse</c>.
/// </para>
/// <para>
/// <b>A cell this game cannot read is refused where the policy is composed.</b> A monster whose attack
/// nothing recognizes would be a monster that hits for harm and nothing else, which is a quiet divergence
/// from data the operator supplied rather than a decision this game made.
/// </para>
/// </remarks>
internal static class MightAndMagic7SpecialAttacks
{
    /// <summary>The cell the shipped table writes where a monster has no special attack.</summary>
    internal const string NoneText = "0";

    /// <summary>Reads a special-attack cell, or null when this game has no name for it.</summary>
    /// <param name="cell">The cell, as the table writes it.</param>
    /// <returns>The attack, or null when the cell names none this game knows.</returns>
    internal static MonsterSpecialAttack? Parse(string cell)
    {
        string text = (cell ?? string.Empty).Trim();
        if (text.Length == 0 || text.Equals(NoneText, StringComparison.Ordinal)) return new MonsterSpecialAttack(MightAndMagic7SpecialAttackKind.None, 1);

        // The level suffix is the donor's own: the first 'x' in the cell ends the name, and what follows it
        // is the level (Monsters.cpp:357-363). A shipped "Poison3x2" is therefore Poison3 at level two.
        int level = 1;
        int mark = text.IndexOf('x', StringComparison.OrdinalIgnoreCase);
        if (mark >= 0)
        {
            string stated = text[(mark + 1)..];
            if (!int.TryParse(stated, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out level))
            {
                return null;
            }

            text = text[..mark];
        }

        string name = text.ToLowerInvariant();
        MightAndMagic7SpecialAttackKind kind = name switch
        {
            _ when name.StartsWith("curse", StringComparison.Ordinal) => MightAndMagic7SpecialAttackKind.Curse,
            _ when name.StartsWith("weak", StringComparison.Ordinal) => MightAndMagic7SpecialAttackKind.Weak,
            _ when name.StartsWith("asleep", StringComparison.Ordinal) => MightAndMagic7SpecialAttackKind.Sleep,
            _ when name.StartsWith("afraid", StringComparison.Ordinal) => MightAndMagic7SpecialAttackKind.Fear,
            _ when name.StartsWith("drunk", StringComparison.Ordinal) => MightAndMagic7SpecialAttackKind.Drunk,
            _ when name.StartsWith("insane", StringComparison.Ordinal) => MightAndMagic7SpecialAttackKind.Insane,
            _ when name.StartsWith("poison weak", StringComparison.Ordinal) || name.StartsWith("poison1", StringComparison.Ordinal) => MightAndMagic7SpecialAttackKind.PoisonWeak,
            _ when name.StartsWith("poison medium", StringComparison.Ordinal) || name.StartsWith("poison2", StringComparison.Ordinal) => MightAndMagic7SpecialAttackKind.PoisonMedium,
            _ when name.StartsWith("poison severe", StringComparison.Ordinal) || name.StartsWith("poison3", StringComparison.Ordinal) => MightAndMagic7SpecialAttackKind.PoisonSevere,
            _ when name.StartsWith("disease weak", StringComparison.Ordinal) || name.StartsWith("disease1", StringComparison.Ordinal) => MightAndMagic7SpecialAttackKind.DiseaseWeak,
            _ when name.StartsWith("disease medium", StringComparison.Ordinal) || name.StartsWith("disease2", StringComparison.Ordinal) => MightAndMagic7SpecialAttackKind.DiseaseMedium,
            _ when name.StartsWith("disease severe", StringComparison.Ordinal) || name.StartsWith("disease3", StringComparison.Ordinal) => MightAndMagic7SpecialAttackKind.DiseaseSevere,
            _ when name.StartsWith("paralyze", StringComparison.Ordinal) => MightAndMagic7SpecialAttackKind.Paralyzed,
            _ when name.StartsWith("uncon", StringComparison.Ordinal) => MightAndMagic7SpecialAttackKind.Unconscious,
            _ when name.StartsWith("dead", StringComparison.Ordinal) => MightAndMagic7SpecialAttackKind.Dead,
            _ when name.StartsWith("stone", StringComparison.Ordinal) => MightAndMagic7SpecialAttackKind.Petrified,
            _ when name.StartsWith("errad", StringComparison.Ordinal) => MightAndMagic7SpecialAttackKind.Eradicated,
            _ when name.StartsWith("brkitem", StringComparison.Ordinal) => MightAndMagic7SpecialAttackKind.BreakAny,
            _ when name.StartsWith("brkarmor", StringComparison.Ordinal) => MightAndMagic7SpecialAttackKind.BreakArmor,
            _ when name.StartsWith("brkweapon", StringComparison.Ordinal) => MightAndMagic7SpecialAttackKind.BreakWeapon,
            _ when name.StartsWith("steal", StringComparison.Ordinal) => MightAndMagic7SpecialAttackKind.Steal,
            _ when name.StartsWith("age", StringComparison.Ordinal) => MightAndMagic7SpecialAttackKind.Aging,
            _ when name.StartsWith("drainsp", StringComparison.Ordinal) => MightAndMagic7SpecialAttackKind.ManaDrain,
            _ => MightAndMagic7SpecialAttackKind.None,
        };

        return kind == MightAndMagic7SpecialAttackKind.None && !name.StartsWith("none", StringComparison.Ordinal)
            ? null
            : new MonsterSpecialAttack(kind, Math.Max(1, level));
    }

    /// <summary>The condition a special attack leaves, or null when it leaves something other than one.</summary>
    /// <param name="attack">The attack that may land.</param>
    /// <remarks>
    /// The donor's own mapping (OpenEnroth <c>src/Engine/Objects/Character.cpp:1459-1600</c>,
    /// <c>ReceiveSpecialAttackEffect</c>), with the severity the donor states: a condition a monster leaves
    /// is a fresh case, which is severity one.
    /// </remarks>
    internal static ConditionId? Condition(MonsterSpecialAttack attack) => attack.Kind switch
    {
        MightAndMagic7SpecialAttackKind.Curse => MightAndMagic7Conditions.Cursed,
        MightAndMagic7SpecialAttackKind.Weak => MightAndMagic7Conditions.Weak,
        MightAndMagic7SpecialAttackKind.Sleep => MightAndMagic7Conditions.Sleep,
        MightAndMagic7SpecialAttackKind.Fear => MightAndMagic7Conditions.Fear,
        MightAndMagic7SpecialAttackKind.Drunk => MightAndMagic7Conditions.Drunk,
        MightAndMagic7SpecialAttackKind.Insane => MightAndMagic7Conditions.Insane,
        MightAndMagic7SpecialAttackKind.PoisonWeak => MightAndMagic7Conditions.PoisonWeak,
        MightAndMagic7SpecialAttackKind.PoisonMedium => MightAndMagic7Conditions.PoisonMedium,
        MightAndMagic7SpecialAttackKind.PoisonSevere => MightAndMagic7Conditions.PoisonSevere,
        MightAndMagic7SpecialAttackKind.DiseaseWeak => MightAndMagic7Conditions.DiseaseWeak,
        MightAndMagic7SpecialAttackKind.DiseaseMedium => MightAndMagic7Conditions.DiseaseMedium,
        MightAndMagic7SpecialAttackKind.DiseaseSevere => MightAndMagic7Conditions.DiseaseSevere,
        MightAndMagic7SpecialAttackKind.Paralyzed => MightAndMagic7Conditions.Paralyzed,
        MightAndMagic7SpecialAttackKind.Unconscious => MightAndMagic7Conditions.Unconscious,
        MightAndMagic7SpecialAttackKind.Dead => MightAndMagic7Conditions.Dead,
        MightAndMagic7SpecialAttackKind.Petrified => MightAndMagic7Conditions.Petrified,
        MightAndMagic7SpecialAttackKind.Eradicated => MightAndMagic7Conditions.Eradicated,
        _ => null,
    };
}
