using System.Globalization;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Party;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// This game's reading of a carried item as the inventory page shows it: what it is, its damage or armour, its
/// charges, what it is worth, and the state the party has left it in, with the picture the item table names.
/// </summary>
/// <remarks>
/// <para>
/// The words are the item table's: a weapon is named by its skill group, armour by its kind of armour, and the rest
/// by the table's own type. Damage and armour are the figure's reading of the same row the fight reads
/// (<see cref="MightAndMagic7Figure"/>), so the page states what a blow or a hit is actually computed from.
/// </para>
/// <para>
/// Approximate. The original's item description screen (OpenEnroth <c>src/GUI/UI/UIPopup.cpp</c>, <c>GameUI_DrawItemInfo</c>)
/// shows the name, a picture, the type line, the damage or armour, the value and a broken or unidentified state;
/// this page states the same facts as lines. Identification hides nothing in this game yet — every item is named
/// by its real name wherever it is shown, and a bought item is not marked identified — so the page states no
/// unidentified line rather than one that contradicts the name above it.
/// </para>
/// </remarks>
internal sealed class MightAndMagic7ItemReadings : IItemReadingRule
{
    private readonly Dictionary<ItemDefinitionId, Row> _rows;
    private readonly MightAndMagic7Figure? _figure;
    private readonly Func<ItemInstance, int>? _worth;
    private readonly MightAndMagic7Spells? _spells;

    private MightAndMagic7ItemReadings(Dictionary<ItemDefinitionId, Row> rows, MightAndMagic7Figure? figure, Func<ItemInstance, int>? worth, MightAndMagic7Spells? spells)
    {
        _rows = rows;
        _figure = figure;
        _worth = worth;
        _spells = spells;
    }

    /// <summary>Reads the item table, or nothing for content that carries none.</summary>
    /// <param name="catalog">The content the item table is read from.</param>
    /// <param name="figure">The figure, whose reading of a row is the damage and armour a fight uses.</param>
    /// <param name="worth">What the counters price an item at (<see cref="MightAndMagic7Services.ValueOf"/>), or null.</param>
    /// <param name="spells">The spells items carry, whose reading states a wand's charges before any recharge.</param>
    internal static MightAndMagic7ItemReadings? Read(ContentCatalog? catalog, MightAndMagic7Figure? figure,
        Func<ItemInstance, int>? worth = null, MightAndMagic7Spells? spells = null)
    {
        if (catalog is null) return null;
        Dictionary<ItemDefinitionId, Row> rows = [];
        foreach ((_, _, ContentEntry entry) in catalog.Entries(MightAndMagic7EquipmentUse.ItemDefinitionKind))
        {
            if (entry.Id.Length == 0) continue;
            rows[new ItemDefinitionId(entry.Id)] = new Row(
                entry.GetString("type").Trim(),
                entry.GetString("skillGroup").Trim(),
                entry.GetString("picture").Trim());
        }

        return rows.Count == 0 ? null : new MightAndMagic7ItemReadings(rows, figure, worth, spells);
    }

    /// <inheritdoc />
    public ItemReading Read(ItemInstance item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!_rows.TryGetValue(item.Definition, out Row row)) return new ItemReading("Item", [.. StateOf(item)]);
        List<string> facts = [];
        if (_figure?.Worn(item.Definition) is { } worn)
        {
            if (worn.IsWeapon && worn.Dice > 0)
                facts.Add(string.Create(CultureInfo.InvariantCulture, $"Damage {worn.Dice}d{worn.Sides}{Signed(worn.Modifier)}"));
            else if (worn.IsPassive && worn.ArmourClass > 0)
                facts.Add(string.Create(CultureInfo.InvariantCulture, $"Armour +{worn.ArmourClass}"));
            if (worn.Skill is { Length: > 0 } skill && !string.Equals(skill, "misc", StringComparison.OrdinalIgnoreCase))
                facts.Add($"Uses the {(row.SkillGroup.Length > 0 ? row.SkillGroup : skill)} skill");
        }

        // A wand holds its spell reading's charges until a recharge states its own capacity, as the casting reads it.
        if ((item.State.ChargeCapacity ?? (_spells?.Reading(item.Definition) is { ConsumedByUse: false } spell ? spell.Charges : null)) is { } capacity)
            facts.Add(string.Create(CultureInfo.InvariantCulture, $"Charges {Math.Max(0, capacity - item.State.ChargesSpent)} of {capacity}"));
        if (row.Type is "potion" or "reagent" && item.State.Potency > 0)
            facts.Add(string.Create(CultureInfo.InvariantCulture, $"Power {item.State.Potency}"));
        if ((_worth?.Invoke(item) ?? 0) is > 0 and var worth) facts.Add(string.Create(CultureInfo.InvariantCulture, $"Worth {worth} gold"));
        facts.AddRange(StateOf(item));
        return new ItemReading(KindOf(row), facts);
    }

    /// <inheritdoc />
    public string? PictureOf(ItemInstance item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return _rows.TryGetValue(item.Definition, out Row row) && row.Picture.Length > 0 ? row.Picture : null;
    }

    private static IEnumerable<string> StateOf(ItemInstance item)
    {
        if (item.State.Damage > 0) yield return "Broken — a smith or a repair service mends it";
        if (item.State.IsStolen) yield return "Stolen";
    }

    /// <summary>The table's type in the words the page uses, with a weapon named by its skill group.</summary>
    private static string KindOf(Row row) => row.Type switch
    {
        "single-handed" => row.SkillGroup.Length > 0 ? row.SkillGroup : "Weapon",
        "two-handed" => row.SkillGroup.Length > 0 ? $"Two-handed {row.SkillGroup.ToLowerInvariant()}" : "Two-handed weapon",
        "bow" => "Bow",
        "armour" => row.SkillGroup.Length > 0 ? $"{row.SkillGroup} armour" : "Armour",
        "shield" => "Shield",
        "helmet" => "Helm",
        "belt" => "Belt",
        "cloak" => "Cloak",
        "gauntlets" => "Gauntlets",
        "boots" => "Boots",
        "ring" => "Ring",
        "amulet" => "Amulet",
        "wand" => "Wand",
        "potion" => "Potion",
        "reagent" => "Reagent",
        "spell-scroll" => "Spell scroll",
        "book" => "Spellbook",
        "message-scroll" => "Message scroll",
        "gem" => "Gem",
        "gold" => "Gold",
        _ => "Item",
    };

    private static string Signed(int modifier) => modifier switch
    {
        > 0 => string.Create(CultureInfo.InvariantCulture, $"+{modifier}"),
        < 0 => string.Create(CultureInfo.InvariantCulture, $"{modifier}"),
        _ => string.Empty,
    };

    private readonly record struct Row(string Type, string SkillGroup, string Picture);
}
