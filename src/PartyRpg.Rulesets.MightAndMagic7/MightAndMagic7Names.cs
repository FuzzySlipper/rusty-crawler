using System.Globalization;
using PartyRpg.Kit;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Skills;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>What this game calls a rung of a skill's ladder and each item its content declares.</summary>
/// <remarks>
/// <para>
/// This is the one place the game's words for the kit's counted things are read, so a casting, a mixture, a
/// lesson, a find, and every panel name a rung and an item alike. The rungs are the design's own four names
/// ([`docs/gameplay-design.md`](../../../docs/gameplay-design.md) §3.2: "basic, expert, master, grand
/// master"), which are the manual's B/E/M/GM and the shipped table's four effect columns; a rung above the
/// ladder reads as its number rather than as a word this game does not have.
/// </para>
/// <para>
/// An item's name is its content entry's own. A potion's row names it first, because the potion table is
/// the one that states what a mixture makes; every other item is named by the item table's row.
/// </para>
/// </remarks>
internal sealed class MightAndMagic7Names : IGameNames
{
    private readonly Dictionary<ItemDefinitionId, string> _items;

    private MightAndMagic7Names(Dictionary<ItemDefinitionId, string> items) => _items = items;

    /// <summary>A reading of no content: rungs still have their words, and no item has a name.</summary>
    internal static MightAndMagic7Names Unnamed { get; } = new([]);

    /// <summary>Reads every item name the content declares.</summary>
    /// <param name="catalog">The loaded content, or null when the product carries none.</param>
    /// <returns>The names.</returns>
    internal static MightAndMagic7Names Read(ContentCatalog? catalog)
    {
        if (catalog is null) return Unnamed;
        Dictionary<ItemDefinitionId, string> items = [];
        foreach ((_, _, ContentEntry entry) in catalog.Entries(MightAndMagic7Alchemy.PotionDefinitionKind))
        {
            string name = entry.GetString("name").Trim();
            if (entry.Id.Length > 0 && name.Length > 0) items[new ItemDefinitionId(entry.Id)] = name;
        }

        foreach ((_, _, ContentEntry entry) in catalog.Entries(MightAndMagic7Containers.ItemDefinitionKind))
        {
            string name = entry.GetString("name").Trim();
            if (entry.Id.Length > 0 && name.Length > 0) items.TryAdd(new ItemDefinitionId(entry.Id), name);
        }

        return new MightAndMagic7Names(items);
    }

    /// <summary>What one rung of a skill's ladder is called in this game.</summary>
    /// <param name="tier">The rung to name.</param>
    /// <returns>The word a person reads for that rung.</returns>
    internal static string Tier(SkillTier tier) => tier.Value switch
    {
        0 => "untrained",
        1 => "basic",
        2 => "expert",
        3 => "master",
        4 => "grand master",
        _ => string.Create(CultureInfo.InvariantCulture, $"rung {tier.Value}"),
    };

    /// <inheritdoc />
    public string TierName(SkillTier tier) => Tier(tier);

    /// <inheritdoc />
    public string ItemName(ItemDefinitionId definition) =>
        _items.TryGetValue(definition, out string? name) ? name : string.Empty;

    /// <summary>What an item is called, or its identity when content names none.</summary>
    /// <param name="definition">The item definition to name.</param>
    /// <returns>The name a person reads.</returns>
    internal string Item(ItemDefinitionId definition) => GameNames.Item(this, definition);
}
