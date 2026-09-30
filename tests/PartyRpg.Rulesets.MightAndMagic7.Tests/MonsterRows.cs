using System.Globalization;
using System.Text.RegularExpressions;

namespace PartyRpg.Rulesets.MightAndMagic7.Tests;

/// <summary>
/// The combat fields of a monster row, written the way the importer writes them: typed, named, and in the
/// table's own words for kinds of harm and special attacks.
/// </summary>
/// <remarks>
/// A case states what its creature does in the shipped spellings — <c>2D8+10</c>, <c>Poison2</c>, <c>Imm</c> —
/// and this writes the fields a pack carries for it, so a fixture reads like the row it stands for without
/// knowing where the table keeps anything.
/// </remarks>
internal static partial class MonsterRows
{
    /// <summary>A monster row's typed combat fields, as a fragment to place inside the row's object.</summary>
    /// <param name="id">The row's own id, whose group of three graded rows names its hostility kind.</param>
    /// <param name="attackKind">The first attack's kind of harm.</param>
    /// <param name="damage">The first attack's dice, or <c>0</c> for none.</param>
    /// <param name="special">The special attack, or <c>0</c> for none.</param>
    /// <param name="physicalResistance">The physical resistance, a number or <c>Imm</c>.</param>
    /// <param name="resistances">Any other resistances, by kind, as numbers or <c>Imm</c>.</param>
    /// <param name="secondChance">How often the second attack is used, in percent.</param>
    /// <param name="secondKind">The second attack's kind of harm, or <c>0</c> for none.</param>
    /// <param name="second">The second attack's dice, or <c>0</c> for none.</param>
    /// <param name="spell1Chance">How often the first spell is cast, in percent.</param>
    /// <param name="spell1">The first spell's name, or <c>0</c> for none.</param>
    /// <param name="spell1Mastery">The rung the first spell is cast at.</param>
    /// <param name="spell1Skill">The skill the first spell is cast at.</param>
    internal static string Combat(
        int id,
        string attackKind = "Phys",
        string damage = "0",
        string special = "0",
        string physicalResistance = "0",
        IReadOnlyDictionary<string, string>? resistances = null,
        int secondChance = 0,
        string secondKind = "0",
        string second = "0",
        int spell1Chance = 0,
        string spell1 = "0",
        string spell1Mastery = "N",
        int spell1Skill = 4)
    {
        Dictionary<string, string> resisted = new(StringComparer.Ordinal)
        {
            ["Fire"] = "0", ["Air"] = "0", ["Water"] = "0", ["Earth"] = "0", ["Mind"] = "0",
            ["Spirit"] = "0", ["Body"] = "0", ["Light"] = "0", ["Dark"] = "0", ["Phys"] = physicalResistance,
        };
        foreach ((string kind, string value) in resistances ?? new Dictionary<string, string>()) resisted[kind] = value;

        List<string> fields =
        [
            Invariant($"\"hostilityKind\": {((id - 1) / 3) + 1}"),
            $"\"attack\": {{ {Attack(attackKind, damage)} }}",
            Invariant($"\"secondAttack\": {{ \"chance\": {secondChance}{(second == "0" ? string.Empty : ", " + Attack(secondKind, second))} }}"),
            $"\"resistances\": {{ {string.Join(", ", resisted.Where(pair => pair.Value != "Imm").Select(pair => $"\"{pair.Key}\": {pair.Value}"))} }}",
            $"\"immunities\": [ {string.Join(", ", resisted.Where(pair => pair.Value == "Imm").Select(pair => $"\"{pair.Key}\""))} ]",
        ];

        if (spell1 != "0")
        {
            fields.Add(Invariant($"\"firstSpell\": {{ \"chance\": {spell1Chance}, \"name\": \"{spell1}\", \"mastery\": \"{spell1Mastery}\", \"skill\": {spell1Skill} }}"));
        }

        if (special != "0")
        {
            Match match = SpecialPattern().Match(special);
            string strength = match.Groups["strength"].Success ? $", \"strength\": {match.Groups["strength"].Value}" : string.Empty;
            string times = match.Groups["times"].Success ? match.Groups["times"].Value : "1";
            fields.Add($"\"specialAttack\": {{ \"kind\": \"{match.Groups["word"].Value}\"{strength}, \"times\": {times} }}");
        }

        return string.Join(", ", fields);
    }

    private static string Attack(string kind, string damage)
    {
        List<string> parts = [];
        if (kind != "0") parts.Add($"\"kind\": \"{kind}\"");
        Match dice = DicePattern().Match(damage);
        if (dice.Success)
        {
            string bonus = dice.Groups["bonus"].Success ? dice.Groups["bonus"].Value : "0";
            parts.Add($"\"dice\": {{ \"count\": {dice.Groups["count"].Value}, \"sides\": {dice.Groups["sides"].Value}, \"bonus\": {bonus} }}");
        }

        return string.Join(", ", parts);
    }

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    [GeneratedRegex(@"^(?<count>\d+)[dD](?<sides>\d+)(?:\+(?<bonus>\d+))?$")]
    private static partial Regex DicePattern();

    [GeneratedRegex(@"^(?<word>[A-Za-z]+?)(?<strength>\d)?(?:x(?<times>\d+))?$")]
    private static partial Regex SpecialPattern();
}
