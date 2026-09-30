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

    /// <summary>Breaks an item the character carries, which this build does not apply yet.</summary>
    BreakAny,

    /// <summary>Breaks the character's armour, which this build does not apply yet.</summary>
    BreakArmor,

    /// <summary>Breaks the character's weapon, which this build does not apply yet.</summary>
    BreakWeapon,

    /// <summary>Steals from the character, which this build does not apply yet.</summary>
    Steal,

    /// <summary>Ages the character, which this build does not apply yet.</summary>
    Aging,

    /// <summary>Drains the character's spell points.</summary>
    ManaDrain,
}

/// <summary>One monster row's special attack: what it does, and how strongly.</summary>
/// <param name="Kind">What the attack does to the character it catches.</param>
/// <param name="Level">How strong the case is, which is the table's own <c>x</c> suffix.</param>
internal sealed record MonsterSpecialAttack(MightAndMagic7SpecialAttackKind Kind, int Level);

/// <summary>
/// What each word the monster table uses for a special attack means in this game.
/// </summary>
/// <remarks>
/// <para>
/// The importer reads the table's special-attack cell into its word, the strength digit a poison or a disease
/// carries, and the count an <c>xN</c> suffix states, so this is a vocabulary rather than a parser: every word
/// the shipped table carries is listed, matched whole and without regard to case, and a word it does not list
/// is refused by name when the session is composed.
/// </para>
/// <para>
/// The words are the release's own, collected from its table: <c>Curse</c>, <c>Weak</c>, <c>Asleep</c>,
/// <c>Afraid</c>, <c>Drunk</c>, <c>Insane</c>, <c>Poison</c> and <c>Disease</c> at strengths 1 to 3,
/// <c>Paralyze</c>, <c>Uncon</c>, <c>Dead</c>, <c>Stone</c>, <c>Errad</c>, <c>BrkItem</c>, <c>BrkArmor</c>,
/// <c>Brkweapon</c>, <c>Steal</c>, <c>Age</c> and <c>DrainSP</c>. What each does to a character is this game's
/// condition vocabulary, and the count is the level of the case.
/// </para>
/// </remarks>
internal static class MightAndMagic7SpecialAttacks
{
    private static readonly Dictionary<string, MightAndMagic7SpecialAttackKind> Words = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Curse"] = MightAndMagic7SpecialAttackKind.Curse,
        ["Weak"] = MightAndMagic7SpecialAttackKind.Weak,
        ["Asleep"] = MightAndMagic7SpecialAttackKind.Sleep,
        ["Afraid"] = MightAndMagic7SpecialAttackKind.Fear,
        ["Drunk"] = MightAndMagic7SpecialAttackKind.Drunk,
        ["Insane"] = MightAndMagic7SpecialAttackKind.Insane,
        ["Paralyze"] = MightAndMagic7SpecialAttackKind.Paralyzed,
        ["Uncon"] = MightAndMagic7SpecialAttackKind.Unconscious,
        ["Dead"] = MightAndMagic7SpecialAttackKind.Dead,
        ["Stone"] = MightAndMagic7SpecialAttackKind.Petrified,
        ["Errad"] = MightAndMagic7SpecialAttackKind.Eradicated,
        ["BrkItem"] = MightAndMagic7SpecialAttackKind.BreakAny,
        ["BrkArmor"] = MightAndMagic7SpecialAttackKind.BreakArmor,
        ["BrkWeapon"] = MightAndMagic7SpecialAttackKind.BreakWeapon,
        ["Steal"] = MightAndMagic7SpecialAttackKind.Steal,
        ["Age"] = MightAndMagic7SpecialAttackKind.Aging,
        ["DrainSP"] = MightAndMagic7SpecialAttackKind.ManaDrain,
    };

    private static readonly MightAndMagic7SpecialAttackKind[] Poisons =
        [MightAndMagic7SpecialAttackKind.PoisonWeak, MightAndMagic7SpecialAttackKind.PoisonMedium, MightAndMagic7SpecialAttackKind.PoisonSevere];

    private static readonly MightAndMagic7SpecialAttackKind[] Diseases =
        [MightAndMagic7SpecialAttackKind.DiseaseWeak, MightAndMagic7SpecialAttackKind.DiseaseMedium, MightAndMagic7SpecialAttackKind.DiseaseSevere];

    /// <summary>What a special attack the row states is, or null when this game has no such word.</summary>
    /// <param name="word">The table's word, as the importer read it.</param>
    /// <param name="strength">The strength a poison or a disease carries, 1 to 3; zero for a word that carries none.</param>
    /// <param name="times">The count the cell states, which is the case's level.</param>
    internal static MonsterSpecialAttack? From(string word, int strength, int times)
    {
        int level = Math.Max(1, times);
        if (string.Equals(word, "Poison", StringComparison.OrdinalIgnoreCase) && strength is >= 1 and <= 3)
        {
            return new MonsterSpecialAttack(Poisons[strength - 1], level);
        }

        if (string.Equals(word, "Disease", StringComparison.OrdinalIgnoreCase) && strength is >= 1 and <= 3)
        {
            return new MonsterSpecialAttack(Diseases[strength - 1], level);
        }

        return strength == 0 && Words.TryGetValue(word, out MightAndMagic7SpecialAttackKind kind)
            ? new MonsterSpecialAttack(kind, level)
            : null;
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
