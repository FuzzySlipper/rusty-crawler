using System.Globalization;
using System.Text.Json;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Loot;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.World;
using Rusty.Engine;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// This game's loot: what a treasure level offers, what a creature's row drops, and what a container's
/// random reference resolves to.
/// </summary>
/// <remarks>
/// <para>
/// <b>The tables are the shipped data's and the rules are the donor's.</b> The item table weighs every item
/// by how often it appears at each treasure level (<c>RNDITEMS.TXT</c> section one, read into the item table
/// as the donor reads it — OpenEnroth <c>src/Engine/Tables/ItemTable.cpp:201-219</c>,
/// <c>ItemTable::LoadRandomItems</c>), and a monster row states its own drop as a chance, a handful of dice,
/// a treasure level, and an item type (<c>MONSTERS.TXT</c> column 8, parsed by the importer). What this
/// class owns is the reading of those numbers: which of them means what, in what order they are asked, and
/// what happens when a level offers nothing the request asked for.
/// </para>
/// <para>
/// <b>The order is the donor's own.</b> A creature's death rolls its cell's dice for coin and then asks the
/// chance for an item of the cell's level (<c>src/Engine/Objects/Actor.cpp:192-212</c>,
/// <c>Actor::SetRandomGoldIfTheresNoItem</c>). A container's random reference is resolved through the
/// place's own danger level and then yields one to five things, each of which is nothing, coin, or an item
/// (<c>src/Engine/Objects/Chest.cpp:323-365</c>, <c>GenerateItemsInChest</c>).
/// </para>
/// <para>
/// <b>Every draw is keyed.</b> Generation happens under a key that names the death or the container, so the
/// same body and the same chest resolve to the same loot every time they are asked — which is what lets a
/// search refused for want of room be retried without changing what the party would have found. Nothing is
/// recorded for it: the key is the state.
/// </para>
/// <para>
/// <b>What this build does not carry, stated rather than hidden.</b> An artifact found is not recorded as
/// found, so the seventh treasure level and the sixth level's own small chance can hand the party an
/// artifact it has met before: which artifacts a party holds belongs to the stone that owns artifacts and
/// relics, and this is where that record will be read.
/// </para>
/// </remarks>
internal sealed class MightAndMagic7Loot
{
    /// <summary>The monster entry field that carries a row's name, which is how the fallback row is found.</summary>
    private const string MonsterNameField = "name";

    /// <summary>The monster entry field that carries a row's parsed treasure cell.</summary>
    internal const string TreasureField = "treasureRoll";

    /// <summary>The treasure field that states how often in a hundred the row drops anything.</summary>
    internal const string TreasureChanceField = "chance";

    /// <summary>The treasure field that states how many dice of coin the row drops.</summary>
    internal const string TreasureGoldRollsField = "goldRolls";

    /// <summary>The treasure field that states how many sides those dice have.</summary>
    internal const string TreasureGoldSidesField = "goldSides";

    /// <summary>The treasure field that states which treasure level the row's item comes from.</summary>
    internal const string TreasureLevelField = "level";

    /// <summary>The treasure field that states what kind of thing the row asks that level for.</summary>
    internal const string TreasureKindField = "kind";

    /// <summary>The treasure field that states which skill the thing it asks for is used with.</summary>
    internal const string TreasureSkillField = "skill";

    /// <summary>The item entry field that tags what kind of thing the item is.</summary>
    internal const string ItemKindField = "type";

    /// <summary>The item entry field that tags which skill the item is used with.</summary>
    internal const string ItemSkillField = "skill";

    /// <summary>The item entry field that carries the item's name.</summary>
    internal const string ItemNameField = "name";

    /// <summary>The item entry field that carries the item's material, which is where its rarity is spelled out.</summary>
    internal const string ItemMaterialField = "material";

    /// <summary>The item entry field that carries how often the item appears at each treasure level.</summary>
    internal const string ItemWeightsField = "lootWeights";

    /// <summary>The material word the shipped table uses for an artifact.</summary>
    private const string ArtifactMaterial = "artifact";

    /// <summary>The material word the shipped table uses for a relic.</summary>
    private const string RelicMaterial = "relic";

    /// <summary>The first item id the donor will hand out as an artifact (OpenEnroth <c>src/Engine/Objects/ItemEnums.h:977</c>).</summary>
    /// <remarks>
    /// The donor's own range, not a reading of the table's material column: the shipped table spells
    /// <c>Artifact</c> on eight rows beyond this range (ids 529-536) that the donor never spawns, so the
    /// material alone would hand the party things the game keeps out of random loot.
    /// </remarks>
    private const int FirstSpawnableArtifact = 500;

    /// <summary>The last item id the donor will hand out as an artifact (OpenEnroth <c>src/Engine/Objects/ItemEnums.h:978</c>).</summary>
    private const int LastSpawnableArtifact = 528;

    /// <summary>The item the donor falls back to when a treasure level offers nothing the request asked for.</summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/Tables/ItemTable.cpp:347</c>: <c>outItem-&gt;itemId = ITEM_CRUDE_LONGSWORD</c>,
    /// which the item table's own ids make item 1 (<c>src/Engine/Objects/ItemEnums.h:156</c>). The donor always
    /// hands something over rather than answering that the level had nothing to give, and this keeps that.
    /// </remarks>
    private const string FallbackItem = "1";

    /// <summary>The seed every draw of this game's loot is made under.</summary>
    /// <remarks>
    /// The engine's random service takes an explicit seed and reads no wall clock, so the product states one.
    /// A constant is deliberate: the key names the death or the container, so one lot of loot is unrelated to
    /// the next while the same one is reproducible.
    /// </remarks>
    internal const ulong RollSeed = 0x5C1E_7A11_5EE9_0003;

    /// <summary>The scope this game's loot draws live under, so they cannot collide with another owner's.</summary>
    internal const string RollScope = "mm7.loot";

    /// <summary>How many things a random reference yields at least (OpenEnroth <c>src/Engine/Objects/Chest.cpp:342</c>).</summary>
    private const int FindingsLeast = 1;

    /// <summary>How many things a random reference yields at most.</summary>
    private const int FindingsMost = 5;

    /// <summary>What one finding of a random reference comes in under when it yields nothing.</summary>
    private const int NothingBelow = 20;

    /// <summary>What one finding comes in under when it yields coin rather than an item.</summary>
    private const int CoinBelow = 60;

    /// <summary>How often a sixth-level request for anything at all may come up an artifact.</summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/Tables/ItemTable.cpp:351-363</c>: at the sixth level, and only for a request
    /// that names no kind, a hundred-sided roll under five takes an artifact instead.
    /// </remarks>
    private const int ArtifactChance = 5;

    /// <summary>What a treasure level's coin is worth, as the donor's own ranges.</summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/Objects/Item.cpp:254-283</c> (<c>Item::generateGold</c>): level one is
    /// <c>random(51) + 50</c> and each level above it spans wider, up to <c>random(3001) + 2000</c> at the
    /// sixth. The pair here is the least and the most of each of those rolls.
    /// </remarks>
    private static readonly (int Least, int Most)[] CoinsByLevel =
    [
        (50, 100),
        (100, 200),
        (200, 500),
        (500, 1000),
        (1000, 2000),
        (2000, 5000),
    ];

    /// <summary>
    /// How a requested treasure level is read at a place of its own danger level, as the donor's table.
    /// </summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/Objects/Item.cpp:760-783</c> (<c>RemapTreasureLevel</c>), "original offset was
    /// 0x004E8168": rows are the requested item level 1-7 and columns the place's own map treasure level 0-6,
    /// and each cell is the range the request becomes before one level inside it is drawn. A request to a
    /// dangerous place therefore yields better than the same request in a quiet one.
    /// </remarks>
    private static readonly (int Least, int Most)[,] LevelByPlace =
    {
        { (1, 1), (1, 1), (1, 1), (1, 1), (1, 1), (1, 1), (1, 1) },
        { (1, 1), (1, 2), (2, 2), (2, 2), (2, 2), (2, 2), (2, 2) },
        { (1, 2), (2, 2), (2, 3), (3, 3), (3, 3), (3, 3), (3, 3) },
        { (2, 2), (2, 2), (3, 3), (3, 4), (4, 4), (4, 4), (4, 4) },
        { (2, 2), (2, 2), (3, 4), (4, 4), (4, 5), (5, 5), (5, 5) },
        { (2, 2), (2, 2), (4, 4), (4, 5), (5, 5), (5, 6), (6, 6) },
        { (2, 2), (2, 2), (7, 7), (7, 7), (7, 7), (7, 7), (7, 7) },
    };

    private readonly LootTable _table;
    private readonly Dictionary<string, string> _names;
    private readonly LootCandidate[] _artifacts;
    private readonly Dictionary<int, TreasureRoll> _treasure;
    private readonly int? _personRow;
    private readonly IRandomService? _random;

    private MightAndMagic7Loot(
        LootTable table,
        Dictionary<string, string> names,
        LootCandidate[] artifacts,
        Dictionary<int, TreasureRoll> treasure,
        int? personRow,
        IRandomService? random)
    {
        _table = table;
        _names = names;
        _artifacts = artifacts;
        _treasure = treasure;
        _personRow = personRow;
        _random = random;
    }

    /// <summary>Reads this game's item weights and monster rows, and judges them before anything is drawn.</summary>
    /// <param name="catalog">The validated content the product loaded, when it loaded any.</param>
    /// <param name="random">
    /// The engine's random service, which every draw is made through. Without one this game cannot generate
    /// loot at all, and says so by answering no rolls rather than by drawing from an invented source.
    /// </param>
    /// <returns>This game's loot.</returns>
    /// <exception cref="ContentValidationException">Content states an item weight or a treasure cell this game cannot read.</exception>
    internal static MightAndMagic7Loot Compose(ContentCatalog? catalog, IRandomService? random)
    {
        if (catalog is null) return new MightAndMagic7Loot(new LootTable([]), [], [], [], null, random);

        List<ContentValidationIssue> issues = [];
        List<LootCandidate> candidates = [];
        Dictionary<string, string> names = [];
        List<LootCandidate> artifacts = [];
        foreach ((LoadedPack pack, ContentDocument document, ContentEntry entry) in catalog.Entries(MightAndMagic7Containers.ItemDefinitionKind))
        {
            int? id = int.TryParse(entry.Id, NumberStyles.None, CultureInfo.InvariantCulture, out int parsed) ? parsed : null;
            LootCandidate candidate = new(
                new ItemDefinitionId(entry.Id),
                ReadWeights(entry, pack, document, issues),
                entry.GetString(ItemKindField),
                entry.GetString(ItemSkillField));
            candidates.Add(candidate);
            names[entry.Id] = entry.GetString(ItemNameField);
            if (IsSpawnableArtifact(entry, id)) artifacts.Add(candidate);
        }

        if (issues.Count > 0)
        {
            throw new ContentValidationException($"This game's loot cannot be read: {issues[0].Message}", issues);
        }

        (Dictionary<int, TreasureRoll> treasure, int? personRow) = ReadTreasure(catalog, issues);
        return new MightAndMagic7Loot(
            new LootTable(candidates),
            names,
            [.. artifacts],
            treasure,
            personRow,
            random);
    }

    /// <summary>How many items the item table offers, which is what a treasure level draws from.</summary>
    internal int ItemCount => _table.Candidates.Count;

    /// <summary>How many of them any treasure level weighs at all.</summary>
    internal int WeighedItemCount => _table.Candidates.Count(candidate => AlsoAt(candidate) is not null);

    /// <summary>How many items this game will hand out as artifacts.</summary>
    internal int ArtifactCount => _artifacts.Length;

    /// <summary>How many monster rows state a treasure cell this game can answer.</summary>
    internal int TreasureRowCount => _treasure.Count;

    /// <summary>Whether this game can generate anything at all.</summary>
    internal bool CanGenerate => _random is not null;

    /// <summary>What an item is called, as content names it.</summary>
    /// <param name="definition">The item definition.</param>
    /// <returns>The name, or the definition itself when content names none.</returns>
    internal string NameOf(ItemDefinitionId definition) =>
        _names.TryGetValue(definition.Value, out string? name) && name.Length > 0 ? name : definition.Value;

    /// <summary>The rolls one generation is drawn under, or null when this product cannot draw.</summary>
    /// <param name="key">What is being generated, which must name the death or the container it belongs to.</param>
    /// <returns>The rolls, or null when there is no random service.</returns>
    internal LootRolls? RollsFor(string key) =>
        _random is null ? null : new LootRolls(_random, RollSeed, RollScope, key);

    /// <summary>What one creature's death left, as the row the creature named states it.</summary>
    /// <remarks>
    /// The row is read from the placement's own field, which is where both a creature and a person the map
    /// places carry it; a person no record gives a row reads the shipped peasant row, which is the donor's
    /// own reading of a person standing in a level (OpenEnroth
    /// <c>src/Engine/Objects/MonsterEnumFunctions.h:56-58</c>).
    /// </remarks>
    /// <param name="body">The dead creature's own placement, lying where it fell.</param>
    /// <param name="rolls">The rolls the death is drawn under.</param>
    /// <returns>What the death left, which may be nothing at all.</returns>
    /// <exception cref="ArgumentNullException">No body or no rolls were supplied.</exception>
    internal LootYield Death(PlacementDefinition body, LootRolls rolls)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(rolls);
        string named = body.Source.GetId(MightAndMagic7Combat.MonsterField);
        int? row = int.TryParse(named, NumberStyles.None, CultureInfo.InvariantCulture, out int stated) ? stated : _personRow;
        if (row is not { } id || !_treasure.TryGetValue(id, out TreasureRoll? cell)) return LootYield.Nothing;

        // The donor rolls the cell's dice whatever the chance says: coin and an item are two statements, and
        // a creature that drops coin but no item is one whose cell states dice without a level.
        int coins = cell.WantsCoins ? rolls.Dice(cell.GoldRolls, cell.GoldSides) : 0;
        List<LootItem> items = [];
        if (cell.WantsItem && rolls.Chance(cell.Chance) && ItemAt(cell.Level, cell.Filter, rolls) is { } item)
        {
            items.Add(item);
        }

        return new LootYield(items, coins);
    }

    /// <summary>What one container reference asks for, resolved at the place's own danger level.</summary>
    /// <param name="level">The treasure level the reference asks for, from one to seven.</param>
    /// <param name="placeLevel">The place's own map treasure level, from zero to six.</param>
    /// <param name="rolls">The rolls the reference is drawn under.</param>
    /// <returns>What the reference yields, which may be nothing at all.</returns>
    /// <exception cref="ArgumentNullException">No rolls were supplied.</exception>
    internal LootYield Reference(int level, int placeLevel, LootRolls rolls)
    {
        ArgumentNullException.ThrowIfNull(rolls);
        (int least, int most) = LevelAt(level, placeLevel);
        int chosen = rolls.Between(least, most);

        // A reference that resolves to the seventh level asks for an artifact outright rather than for a
        // weighted item (OpenEnroth src/Engine/Objects/Chest.cpp:333-342), and the donor's own artifact pick
        // is the pool of artifacts the party has not found yet.
        if (chosen >= TreasureRoll.HighestLevel) return new LootYield(Artifact(rolls) is { } found ? [found] : [], 0);

        List<LootItem> items = [];
        int coins = 0;
        int findings = rolls.Between(FindingsLeast, FindingsMost);
        for (int finding = 0; finding < findings; finding++)
        {
            // Each finding draws under its own name, because a keyed draw of one purpose is the same value
            // every time it is asked for: a container of five things must not be five copies of one.
            LootRolls drawn = rolls.Under($"finding/{finding}");
            int what = drawn.Between(1, 100);
            if (what <= NothingBelow) continue;
            if (what <= CoinBelow)
            {
                coins += CoinsAt(chosen, drawn);
                continue;
            }

            if (ItemAt(chosen, LootFilter.Any, drawn) is { } item) items.Add(item);
        }

        return new LootYield(items, coins);
    }

    /// <summary>One item of a treasure level, for the request that asked for it.</summary>
    /// <remarks>
    /// The fallback is the donor's: a level that offers nothing the request asked for hands over a crude
    /// longsword rather than nothing, which is what makes "a random item of this level" always an item.
    /// </remarks>
    private LootItem? ItemAt(int level, LootFilter filter, LootRolls rolls)
    {
        if (level >= TreasureRoll.HighestLevel) return Artifact(rolls);
        if (level == TreasureRoll.HighestLevel - 1 && filter == LootFilter.Any && rolls.Chance(ArtifactChance) && Artifact(rolls) is { } rare)
        {
            return rare;
        }

        LootCandidate? picked = _table.Pick(level, filter, rolls);
        return picked is not null
            ? new LootItem(picked.Definition)
            : _table.Find(new ItemDefinitionId(FallbackItem)) is { } fallback ? new LootItem(fallback.Definition) : null;
    }

    /// <summary>One artifact out of the pool this game will hand out, or nothing when it holds none.</summary>
    private LootItem? Artifact(LootRolls rolls) =>
        _artifacts.Length == 0 ? null : new LootItem(_artifacts[rolls.Pick(_artifacts.Length)].Definition);

    /// <summary>What one treasure level's coin is worth.</summary>
    private static int CoinsAt(int level, LootRolls rolls) =>
        level >= 1 && level <= CoinsByLevel.Length
            ? rolls.Between(CoinsByLevel[level - 1].Least, CoinsByLevel[level - 1].Most)
            : 0;

    /// <summary>The range a requested level becomes at a place of one danger level, one level inside it.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The requested level or the place's level is outside the donor's table.</exception>
    private static (int Least, int Most) LevelAt(int level, int placeLevel)
    {
        if (level < 1 || level > TreasureRoll.HighestLevel)
        {
            throw new ArgumentOutOfRangeException(
                nameof(level),
                level,
                $"A random reference asks for a treasure level from 1 to {TreasureRoll.HighestLevel}; '{level}' is not one, and reading it as one would hand the party a level the maps never asked for.");
        }

        if (placeLevel < 0 || placeLevel >= LevelByPlace.GetLength(1))
        {
            throw new ArgumentOutOfRangeException(
                nameof(placeLevel),
                placeLevel,
                $"A place's own map treasure level runs from 0 to {LevelByPlace.GetLength(1) - 1}; '{placeLevel}' is not one, and reading it as one would price the place's loot at a danger it does not state.");
        }

        return LevelByPlace[level - 1, placeLevel];
    }

    /// <summary>Whether the shipped table marks an item as one this game will hand out as an artifact.</summary>
    private static bool IsSpawnableArtifact(ContentEntry entry, int? id)
    {
        if (id is not { } value || value < FirstSpawnableArtifact || value > LastSpawnableArtifact) return false;
        string material = entry.GetString(ItemMaterialField);
        return string.Equals(material, ArtifactMaterial, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(material, RelicMaterial, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The treasure levels an item appears at, from the weights content states for it.</summary>
    private static IReadOnlyList<int> ReadWeights(
        ContentEntry entry,
        LoadedPack pack,
        ContentDocument document,
        List<ContentValidationIssue> issues)
    {
        List<int> weights = [];
        foreach (JsonElement weight in entry.GetArray(ItemWeightsField))
        {
            if (weight.ValueKind != JsonValueKind.Number || !weight.TryGetInt32(out int value) || value < 0)
            {
                issues.Add(new ContentValidationIssue(
                    "loot-weight-unreadable",
                    $"item '{entry.Id}' states a random-loot weight of '{weight}', which is not a count of anything: a level it weighs would draw an item an amount of times nothing can mean.",
                    pack.PackId,
                    document.DocumentId));
                continue;
            }

            weights.Add(value);
        }

        if (weights.Count > TreasureRoll.HighestLevel)
        {
            issues.Add(new ContentValidationIssue(
                "loot-weight-too-many",
                $"item '{entry.Id}' states {weights.Count} random-loot weights and this game's treasure levels run to {TreasureRoll.HighestLevel}.",
                pack.PackId,
                document.DocumentId));
        }

        return weights;
    }

    /// <summary>Reads every monster row's parsed treasure cell, and the peasant row a person falls back to.</summary>
    private static (Dictionary<int, TreasureRoll> Treasure, int? PersonRow) ReadTreasure(
        ContentCatalog catalog,
        List<ContentValidationIssue> issues)
    {
        Dictionary<int, TreasureRoll> treasure = [];
        int? personRow = null;
        foreach ((LoadedPack pack, ContentDocument document, ContentEntry entry) in catalog.Entries(MightAndMagic7Combat.MonsterDefinitionKind))
        {
            if (!int.TryParse(entry.Id, NumberStyles.None, CultureInfo.InvariantCulture, out int id)) continue;
            if (personRow is null &&
                string.Equals(entry.GetString(MonsterNameField), MightAndMagic7Combat.PersonRowName, StringComparison.OrdinalIgnoreCase))
            {
                personRow = id;
            }

            if (!entry.Has(TreasureField)) continue;
            if (ReadTreasure(entry, pack, document, issues) is { } cell) treasure[id] = cell;
        }

        return (treasure, personRow);
    }

    /// <summary>Reads one monster row's parsed treasure cell.</summary>
    private static TreasureRoll? ReadTreasure(
        ContentEntry entry,
        LoadedPack pack,
        ContentDocument document,
        List<ContentValidationIssue> issues)
    {
        if (entry.Payload.TryGetProperty(TreasureField, out JsonElement cell) && cell.ValueKind == JsonValueKind.Object)
        {
            int chance = (int)(ContentEntry.ReadDouble(cell, TreasureChanceField) ?? 0);
            int rolls = (int)(ContentEntry.ReadDouble(cell, TreasureGoldRollsField) ?? 0);
            int sides = (int)(ContentEntry.ReadDouble(cell, TreasureGoldSidesField) ?? 0);
            int level = (int)(ContentEntry.ReadDouble(cell, TreasureLevelField) ?? 0);
            if (chance is >= 0 and <= 100 && rolls >= 0 && sides >= 0 && level is >= 0 and <= TreasureRoll.HighestLevel)
            {
                return new TreasureRoll(
                    chance,
                    rolls,
                    sides,
                    level,
                    new LootFilter(ContentEntry.ReadString(cell, TreasureKindField), ContentEntry.ReadString(cell, TreasureSkillField)));
            }
        }

        issues.Add(new ContentValidationIssue(
            "monster-treasure-unreadable",
            $"monster '{entry.Id}' states a treasure cell this game cannot read, so what killing it leaves would be a question nothing answers.",
            pack.PackId,
            document.DocumentId));
        return null;
    }

    /// <summary>The first treasure level an item is weighed at, or null when no level weighs it.</summary>
    private static int? AlsoAt(LootCandidate candidate)
    {
        for (int level = 1; level <= TreasureRoll.HighestLevel; level++)
        {
            if (candidate.WeightAt(level) > 0) return level;
        }

        return null;
    }
}
