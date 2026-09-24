using MightAndMagic7.Import.Lod;

namespace MightAndMagic7.Import.Tables;

/// <summary>One item row.</summary>
/// <param name="Id">The item id.</param>
/// <param name="Picture">The icon or sprite reference.</param>
/// <param name="Name">The item's name.</param>
/// <param name="Value">The item's base value in gold.</param>
/// <param name="EquipStat">The equipment category the table records.</param>
/// <param name="SkillGroup">The skill the item belongs to.</param>
/// <param name="DamageDice">The first modifier, damage dice for weapons.</param>
/// <param name="DamageModifier">The second modifier.</param>
/// <param name="Material">The material or rarity value; special rarities are spelled out.</param>
/// <param name="IdentifyRepair">The identify and repair difficulty field.</param>
/// <param name="UnidentifiedName">The name shown before the item is identified.</param>
/// <param name="SpriteIndex">The sprite reference.</param>
/// <param name="Fields">Every field of the row, for the columns this importer does not type yet.</param>
public readonly record struct ItemRecord(
    int Id,
    string Picture,
    string Name,
    int Value,
    string EquipStat,
    string SkillGroup,
    string DamageDice,
    string DamageModifier,
    string Material,
    string IdentifyRepair,
    string UnidentifiedName,
    int SpriteIndex,
    IReadOnlyList<string> Fields)
{
    /// <summary>Whether the row is the table's empty placeholder rather than an item.</summary>
    public bool IsPlaceholder => Name.Length == 0 && Picture.Length == 0;
}

/// <summary>The item table.</summary>
public sealed class ItemTable
{
    /// <summary>How many rows this table's own header occupies.</summary>
    public const int HeaderRowCount = 1;

    /// <summary>How many rows the shipped table carries, including the empty placeholder at id zero.</summary>
    public const int ExpectedRows = 800;

    private ItemTable(TabularTable table, ItemRecord[] items)
    {
        Table = table;
        Items = items;
    }

    /// <summary>The table this was read from.</summary>
    public TabularTable Table { get; }

    /// <summary>Every item row, in table order.</summary>
    public IReadOnlyList<ItemRecord> Items { get; }

    /// <summary>Reads the table from an installation.</summary>
    public static ItemTable Read(LodInstall install)
    {
        TabularTable table = TabularTable.Read(install, Mm7TableSources.Items, HeaderRowCount);
        (IReadOnlyList<TabularRow> data, _) = table.Partition();
        ItemRecord[] items = [.. data.Select(row => new ItemRecord(
            TableValue.Integer(table, row, 0, "Item #"),
            TableValue.Text(row, 1),
            TableValue.Text(row, 2),
            TableValue.OptionalInteger(table, row, 3, "Value") ?? 0,
            TableValue.Text(row, 4),
            TableValue.Text(row, 5),
            TableValue.Text(row, 6),
            TableValue.Text(row, 7),
            TableValue.Text(row, 8),
            TableValue.Text(row, 9),
            TableValue.Text(row, 10),
            TableValue.OptionalInteger(table, row, 11, "Sprite Index") ?? 0,
            row.Fields))];

        if (items.Length != ExpectedRows)
        {
            throw new LodFormatException($"{table.Source}: expected {ExpectedRows} item rows, read {items.Length}.");
        }

        return new ItemTable(table, items);
    }
}
