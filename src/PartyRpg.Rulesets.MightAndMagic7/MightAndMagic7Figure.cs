using System.Globalization;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Party;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>What the item table says one kind of thing is when it is worn.</summary>
internal enum MightAndMagic7WornKind
{
    /// <summary>Not worn at all: a potion, a scroll, a reagent, a gem, coin.</summary>
    None,

    /// <summary>A weapon held in one hand, which a trained hand may also hold in the other.</summary>
    OneHanded,

    /// <summary>A weapon held in both hands.</summary>
    TwoHanded,

    /// <summary>A bow, crossbow or sling, which has a slot of its own.</summary>
    Bow,

    /// <summary>Body armour.</summary>
    Armour,

    /// <summary>A shield, which is held in the off hand.</summary>
    Shield,

    /// <summary>A helmet.</summary>
    Helmet,

    /// <summary>A belt.</summary>
    Belt,

    /// <summary>A cloak.</summary>
    Cloak,

    /// <summary>Gauntlets.</summary>
    Gauntlets,

    /// <summary>Boots.</summary>
    Boots,

    /// <summary>A ring.</summary>
    Ring,

    /// <summary>An amulet.</summary>
    Amulet,

    /// <summary>A wand, which is held in the main hand and fired as its spell.</summary>
    Wand,
}

/// <summary>One item row read as something worn: its kind, the skill it belongs to, and its two numbers.</summary>
/// <remarks>
/// The numbers are the item table's own damage and modifier columns read as the donor reads them
/// (OpenEnroth <c>src/Engine/Tables/ItemTable.cpp:146-158</c>): <c>3d3</c> is three three-sided dice, a bare
/// <c>8</c> is eight dice of one side — which is how a piece of armour states its armour class — and a cell
/// that begins with <c>S</c> or <c>M</c> (a scroll's or a wand's level) is no dice at all. The modifier is the
/// table's next column whole.
/// </remarks>
/// <param name="Kind">What the row is when worn.</param>
/// <param name="Skill">The skill word the row names, lower-case as the importer writes it.</param>
/// <param name="Dice">How many dice the row rolls, or what a bare number states.</param>
/// <param name="Sides">How many sides each die has; one for a bare number.</param>
/// <param name="Modifier">The row's modifier column.</param>
internal readonly record struct MightAndMagic7WornItem(MightAndMagic7WornKind Kind, string Skill, int Dice, int Sides, int Modifier)
{
    /// <summary>
    /// The armour class a passive piece adds: the dice count and the modifier, which is the donor's own sum for
    /// armour, a shield, a helmet, a belt, a cloak, gauntlets, boots, a ring and an amulet
    /// (OpenEnroth <c>src/Engine/Objects/Character.cpp:2299-2304</c>, over <c>isPassiveEquipment</c>,
    /// <c>ItemEnumFunctions.h:213-215</c>).
    /// </summary>
    internal int ArmourClass => IsPassive ? Dice + Modifier : 0;

    /// <summary>Whether the row is passive equipment rather than something held to attack with.</summary>
    internal bool IsPassive => Kind is >= MightAndMagic7WornKind.Armour and <= MightAndMagic7WornKind.Amulet;

    /// <summary>Whether the row is a weapon swung or shot, which a wand is not (<c>ItemEnumFunctions.h:229-231</c>).</summary>
    internal bool IsWeapon => Kind is MightAndMagic7WornKind.OneHanded or MightAndMagic7WornKind.TwoHanded or MightAndMagic7WornKind.Bow;

    /// <summary>Whether the row is a weapon swung in hand.</summary>
    internal bool IsMeleeWeapon => Kind is MightAndMagic7WornKind.OneHanded or MightAndMagic7WornKind.TwoHanded;

    /// <summary>Whether the row names a skill.</summary>
    internal bool IsSkill(string word) => string.Equals(Skill, word, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// This game's paper doll: the sixteen places a character wears things in, which of them each kind of item goes
/// to, and what each worn item states.
/// </summary>
/// <remarks>
/// <para>
/// <b>The slots are the donor's.</b> A character holds an off hand, a main hand, a bow, armour, a helmet, a belt, a
/// cloak, gauntlets, boots, an amulet and six rings (OpenEnroth <c>src/Engine/Objects/ItemEnums.h:1054-1072</c>,
/// <c>ItemSlot</c>), listed here in that order. Their names are this game's words for them — the item table's own
/// <c>Helm</c> for the helmet — and they are the only slot names this game accepts, so a scenario or a save that
/// names another place is refused by name rather than worn somewhere the fight never reads.
/// </para>
/// <para>
/// <b>Where a kind goes is the donor's map.</b> A one-handed weapon goes in either hand, a two-handed one and a
/// wand in the main hand, a shield in the off hand, a ring on any of the six fingers, and everything else in its
/// own slot (<c>ItemEnumFunctions.h:240-266</c>, <c>itemSlotsForItemType</c>). The main hand is offered before
/// the off hand because the donor fills the main hand unless the player drops the weapon on the off-hand side of
/// the doll (<c>src/GUI/UI/UICharacter.cpp:1898-1946</c>).
/// </para>
/// </remarks>
internal sealed class MightAndMagic7Figure : IEquipmentFigure
{
    /// <summary>The off hand, which holds a shield or a second weapon.</summary>
    internal static readonly EquipmentSlot OffHand = new("off hand");

    /// <summary>The main hand, which holds a weapon or a wand.</summary>
    internal static readonly EquipmentSlot MainHand = new("main hand");

    /// <summary>The bow slot.</summary>
    internal static readonly EquipmentSlot Bow = new("bow");

    /// <summary>The armour slot.</summary>
    internal static readonly EquipmentSlot Armour = new("armour");

    /// <summary>The helmet slot, named as the item table names its helmets.</summary>
    internal static readonly EquipmentSlot Helm = new("helm");

    /// <summary>The belt slot.</summary>
    internal static readonly EquipmentSlot Belt = new("belt");

    /// <summary>The cloak slot.</summary>
    internal static readonly EquipmentSlot Cloak = new("cloak");

    /// <summary>The gauntlets slot.</summary>
    internal static readonly EquipmentSlot Gauntlets = new("gauntlets");

    /// <summary>The boots slot.</summary>
    internal static readonly EquipmentSlot Boots = new("boots");

    /// <summary>The amulet slot.</summary>
    internal static readonly EquipmentSlot Amulet = new("amulet");

    /// <summary>The six ring fingers, in the donor's order.</summary>
    internal static readonly EquipmentSlot[] Rings =
        [new("ring 1"), new("ring 2"), new("ring 3"), new("ring 4"), new("ring 5"), new("ring 6")];

    private static readonly EquipmentSlot[] All =
        [OffHand, MainHand, Bow, Armour, Helm, Belt, Cloak, Gauntlets, Boots, Amulet, .. Rings];

    private readonly Dictionary<ItemDefinitionId, MightAndMagic7WornItem> _items;

    private MightAndMagic7Figure(Dictionary<ItemDefinitionId, MightAndMagic7WornItem> items) => _items = items;

    /// <summary>Reads what every item row is when worn, or null when there is no content to read.</summary>
    /// <param name="catalog">The validated content, or null when no bundle supplied any.</param>
    internal static MightAndMagic7Figure? Read(ContentCatalog? catalog)
    {
        if (catalog is null) return null;
        Dictionary<ItemDefinitionId, MightAndMagic7WornItem> items = [];
        foreach ((_, _, ContentEntry entry) in catalog.Entries(MightAndMagic7EquipmentUse.ItemDefinitionKind))
        {
            // The source table calls the suit miscellaneous; the game wears it as armour
            // (OpenEnroth Character.cpp:5736-5738). This compiled identity is ruleset policy.
            MightAndMagic7WornKind kind = entry.Id == "604" ? MightAndMagic7WornKind.Armour : KindOf(entry.GetString("type"));
            if (kind == MightAndMagic7WornKind.None) continue;
            (int dice, int sides) = Dice(entry.GetString("damageDice"));
            items[new ItemDefinitionId(entry.Id)] = new MightAndMagic7WornItem(
                kind,
                entry.GetString("skill").Trim().ToLowerInvariant(),
                dice,
                sides,
                Number(entry.GetString("damageModifier")));
        }

        return new MightAndMagic7Figure(items);
    }

    /// <inheritdoc />
    public IReadOnlyList<EquipmentSlot> Slots => All;

    /// <inheritdoc />
    public IReadOnlyList<EquipmentSlot> SlotsFor(ItemInstance item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return SlotsFor(Worn(item.Definition)?.Kind ?? MightAndMagic7WornKind.None);
    }

    /// <summary>The slots one kind of item goes to, most likely first.</summary>
    internal static IReadOnlyList<EquipmentSlot> SlotsFor(MightAndMagic7WornKind kind) => kind switch
    {
        MightAndMagic7WornKind.OneHanded => [MainHand, OffHand],
        MightAndMagic7WornKind.TwoHanded or MightAndMagic7WornKind.Wand => [MainHand],
        MightAndMagic7WornKind.Bow => [Bow],
        MightAndMagic7WornKind.Armour => [Armour],
        MightAndMagic7WornKind.Shield => [OffHand],
        MightAndMagic7WornKind.Helmet => [Helm],
        MightAndMagic7WornKind.Belt => [Belt],
        MightAndMagic7WornKind.Cloak => [Cloak],
        MightAndMagic7WornKind.Gauntlets => [Gauntlets],
        MightAndMagic7WornKind.Boots => [Boots],
        MightAndMagic7WornKind.Amulet => [Amulet],
        MightAndMagic7WornKind.Ring => Rings,
        _ => [],
    };

    /// <summary>What a definition is when worn, or null when the table states nothing worn for it.</summary>
    internal MightAndMagic7WornItem? Worn(ItemDefinitionId definition) =>
        _items.TryGetValue(definition, out MightAndMagic7WornItem worn) ? worn : null;

    /// <summary>
    /// What a member holds in one slot that still works, or null when the slot is empty, holds something the table
    /// states nothing worn for, or holds something broken.
    /// </summary>
    /// <remarks>
    /// The donor reads every worn term through its functional entries, and a broken item is not functional
    /// (OpenEnroth <c>src/Engine/Objects/Item.cpp:755-757</c>); this game marks an item broken by the damage it
    /// carries, which is what a counter mends.
    /// </remarks>
    internal MightAndMagic7WornItem? Functional(PartyMember member, EquipmentSlot slot) =>
        member.Equipment.ItemIn(slot) is { State.Damage: <= 0 } item ? Worn(item.Definition) : null;

    /// <summary>Every working worn item a member carries, in the figure's order.</summary>
    internal IEnumerable<MightAndMagic7WornItem> Functional(PartyMember member)
    {
        foreach (EquipmentSlot slot in All)
        {
            if (Functional(member, slot) is { } worn) yield return worn;
        }
    }

    /// <summary>Whether a slot is one this figure has.</summary>
    internal static bool Has(EquipmentSlot slot) => Array.IndexOf(All, slot) >= 0;

    private static MightAndMagic7WornKind KindOf(string type) => type.Trim().ToLowerInvariant() switch
    {
        "single-handed" => MightAndMagic7WornKind.OneHanded,
        "two-handed" => MightAndMagic7WornKind.TwoHanded,
        "bow" => MightAndMagic7WornKind.Bow,
        "armour" => MightAndMagic7WornKind.Armour,
        "shield" => MightAndMagic7WornKind.Shield,
        "helmet" => MightAndMagic7WornKind.Helmet,
        "belt" => MightAndMagic7WornKind.Belt,
        "cloak" => MightAndMagic7WornKind.Cloak,
        "gauntlets" => MightAndMagic7WornKind.Gauntlets,
        "boots" => MightAndMagic7WornKind.Boots,
        "ring" => MightAndMagic7WornKind.Ring,
        "amulet" => MightAndMagic7WornKind.Amulet,
        "wand" => MightAndMagic7WornKind.Wand,
        _ => MightAndMagic7WornKind.None,
    };

    /// <summary>Reads a damage cell the way the donor does: <c>XdY</c>, a bare count of one-sided dice, or nothing.</summary>
    private static (int Dice, int Sides) Dice(string cell)
    {
        string text = cell.Trim();
        if (text.Length == 0 || char.ToLowerInvariant(text[0]) is 's' or 'm') return (0, 0);
        int split = text.IndexOfAny(['d', 'D']);
        if (split < 0) return (Number(text), 1);
        return (Number(text[..split]), Number(text[(split + 1)..]));
    }

    private static int Number(string text) =>
        int.TryParse(text.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int value) ? value : 0;
}
