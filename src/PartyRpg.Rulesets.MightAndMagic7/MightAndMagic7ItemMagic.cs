using System.Globalization;
using PartyRpg.Kit;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Magic;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Time;
using Rusty.Engine;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>The item operation a utility row states; all are applied by the ordinary spell effect path.</summary>
internal enum ItemMagicShape { Enchant, Recharge, Harden, Fire, Frost, Poison, Sparks, Vampiric, Swift, Dragon }

/// <summary>MM7 eligibility and settlement over the party's actual item instances, with no separate inventory or effect ledger.</summary>
/// <remarks>
/// Behaviour references: OpenEnroth src/Engine/Spells/CastSpellInfo.cpp:687-741,1334-1510;
/// src/GUI/UI/UIPopup.cpp:2182-2254. Selection of a permanent bonus and its combat magnitude are this game's
/// approximation, not a reproduction of the original weighted enchantment tables.
/// </remarks>
internal sealed class MightAndMagic7ItemMagic
{
    private readonly Dictionary<ItemDefinitionId, ItemFacts> _items = [];
    private readonly MightAndMagic7Spells _spells;
    private readonly GameClock? _clock;
    private readonly IRandomService? _random;
    private readonly Func<PartyEntity?> _party;
    private readonly TuningProfile _tuning;
    private const string Attempts = "item:enchant-attempts";

    internal MightAndMagic7ItemMagic(ContentCatalog? catalog, MightAndMagic7Spells spells, GameClock? clock,
        IRandomService? random, Func<PartyEntity?> party)
    {
        _spells = spells;
        _clock = clock;
        _random = random;
        _party = party;
        _tuning = MightAndMagic7Tuning.Read(catalog);
        if (catalog is null) return;
        foreach ((_, _, ContentEntry entry) in catalog.Entries("item"))
            _items[new ItemDefinitionId(entry.Id)] = new(entry.GetString("name"), entry.GetString("type"),
                entry.GetString("material"), entry.GetDouble("value") is { } price ? (int)Math.Clamp(price, 0, int.MaxValue) : 0);
    }

    internal IReadOnlyList<SpellAim> Aims() => _party() is { } party
        ? [.. party.Items.Select(item => new SpellAim(item.Id.ToString(), Describe(item), "item"))] : [];

    internal string Describe(ItemInstance item) =>
        $"{(_items.TryGetValue(item.Definition, out ItemFacts facts) ? facts.Name : item.Definition.Value)} ({item.Id})" +
        (Active(item) is { } property ? $" — {property.Property} {property.Strength}" : string.Empty) +
        (item.State.IsHardened ? " — hardened" : string.Empty);

    internal Refusal? Judge(SpellApplication application, ItemMagicShape shape)
    {
        ItemInstance? item = Target(application);
        if (item is null) return Refuse(MightAndMagic7Codes.ItemMagicTarget, "Choose an item the party actually holds.");
        if (!_items.TryGetValue(item.Definition, out ItemFacts facts))
            return Refuse(MightAndMagic7Codes.ItemMagicTarget, $"Item {item.Id} has no item-table reading.");
        if (IsQuest(item.Definition) || application.Party.JudgeItemRetention(item.Definition) is not null)
            return Refuse(MightAndMagic7Codes.ItemMagicQuest, $"{facts.Name} is a quest item; its identity and properties cannot be changed by {application.Spell.Name}.");
        if (Special(facts)) return Refuse(MightAndMagic7Codes.ItemMagicSpecial, $"{facts.Name} is special; its fixed identity cannot be enchanted, coated or hardened.");
        if (item.State.Damage > 0) return Refuse(MightAndMagic7Codes.ItemMagicBroken, $"Repair {facts.Name} before applying {application.Spell.Name}.");
        if (shape == ItemMagicShape.Recharge)
        {
            if (_spells.Reading(item.Definition) is not { ConsumedByUse: false } reading)
                return Refuse(MightAndMagic7Codes.ItemMagicKind, $"{facts.Name} is not a charged wand.");
            int capacity = item.State.ChargeCapacity ?? reading.Charges;
            int refilled = RechargedCapacity(application, capacity);
            return refilled <= 0 || refilled <= capacity - item.State.ChargesSpent
                ? Refuse(MightAndMagic7Codes.ItemMagicCharged, $"{facts.Name} is already as charged as this recharge can leave it.") : null;
        }
        bool weapon = Weapon(facts);
        bool equipment = weapon || Passive(facts) || facts.Kind == "wand";
        if (!equipment || (shape is not (ItemMagicShape.Enchant or ItemMagicShape.Harden) && !weapon))
            return Refuse(MightAndMagic7Codes.ItemMagicKind, $"{application.Spell.Name} cannot be applied to {facts.Name}'s item kind ({facts.Kind}).");
        if (shape == ItemMagicShape.Harden)
            return item.State.IsHardened ? Refuse(MightAndMagic7Codes.ItemMagicAlready, $"{facts.Name} is already hardened.") : null;
        if (Active(item) is not null)
            return Refuse(MightAndMagic7Codes.ItemMagicAlready, $"{facts.Name} already bears a property; {application.Spell.Name} cannot replace it.");
        if (shape == ItemMagicShape.Enchant && (_random is null || application.Party.Records.CountOf(Attempts) == int.MaxValue))
            return Refuse(MightAndMagic7Codes.ItemMagicRolls, "Enchanting needs an available keyed roll and room for its durable attempt count.");
        if (shape is not (ItemMagicShape.Enchant or ItemMagicShape.Harden) && _clock is null)
            return Refuse(MightAndMagic7Codes.ItemMagicClock, "A temporary item property needs the session clock.");
        return null;
    }

    internal SpellApplicationOutcome Apply(SpellApplication application, ItemMagicShape shape)
    {
        ItemInstance item = Target(application)!; // Judge ran in this same admitted casting.
        ItemFacts facts = _items[item.Definition];
        int rank = Math.Max(1, _spells.LevelOf(application));
        int mastery = MightAndMagic7Spells.MasteryOf(application.Caster, application.Spell);
        if (shape == ItemMagicShape.Recharge)
        {
            int capacity = item.State.ChargeCapacity ?? _spells.Reading(item.Definition)!.Value.Charges;
            item.Recharge(RechargedCapacity(application, capacity));
            return Outcome(application, item, $"recharged to {item.State.ChargeCapacity} charge(s)");
        }
        if (shape == ItemMagicShape.Harden)
        {
            item.Harden();
            return Outcome(application, item, "hardened against an enchantment failure");
        }
        string property;
        int strength;
        long? due = null;
        if (shape == ItemMagicShape.Enchant)
        {
            application.Party.Records.Increment(Attempts);
            KeyedRolls rolls = new(_random!, MightAndMagic7Loot.RollSeed, "mm7.items.enchant",
                $"{application.Caster.Id}/{item.Id}/{application.Party.Records.CountOf(Attempts)}");
            int floor = _tuning.Whole(Weapon(facts) ? MightAndMagic7Tuning.EnchantWeaponValue : MightAndMagic7Tuning.EnchantEquipmentValue);
            if (facts.Value < floor || !rolls.Chance(Math.Min(100, (int)Math.Min(100L, (long)rank * _tuning.Whole(MightAndMagic7Tuning.EnchantChancePerRank)))))
            {
                if (!item.State.IsHardened) item.TakeDamage(1);
                return Outcome(application, item, item.State.IsHardened ? "enchantment failed; hardening spared it" : "enchantment failed and broke it");
            }
            string[] properties = Weapon(facts) ? ["fire", "frost", "poison", "sparks"] : ["Might", "Endurance", "Speed", "armour"];
            property = properties[rolls.Between(0, properties.Length - 1)];
            int low = _tuning.Whole(mastery >= 4 ? MightAndMagic7Tuning.EnchantGrandMasterLow : MightAndMagic7Tuning.EnchantMasterLow);
            strength = rolls.Between(low, low + mastery + 2);
        }
        else
        {
            property = shape.ToString().ToLowerInvariant();
            strength = Math.Max(1, mastery);
            // Donor coating lasts thirty minutes per potion power; spell coats rank hours, permanent at GM.
            if (application.Source is not null) due = checked(_clock!.Elapsed.Milliseconds + GameDuration.FromMinutes(30L * rank).Milliseconds);
            else if (mastery < 4) due = checked(_clock!.Elapsed.Milliseconds + GameDuration.FromHours(rank).Milliseconds);
        }
        item.SetEnchantment(new ItemEnchantment(property, strength, due));
        return Outcome(application, item, $"bears {property} {strength}" + (due is null ? " permanently" : " until its deadline on the session clock"));
    }

    internal void Observe()
    {
        if (_clock is null || _party() is not { } party) return;
        foreach (ItemInstance item in party.Items)
            if (item.State.Enchantment?.DueElapsedMilliseconds is { } due && due <= _clock.Elapsed.Milliseconds) item.SetEnchantment(null);
    }

    internal ItemEnchantment? Active(ItemInstance item) => item.State.Enchantment is { } property &&
        (property.DueElapsedMilliseconds is null || (_clock is not null && property.DueElapsedMilliseconds > _clock.Elapsed.Milliseconds)) ? property : null;

    private ItemInstance? Target(SpellApplication application) => ulong.TryParse(application.TargetName, NumberStyles.None,
        CultureInfo.InvariantCulture, out ulong id) && id != 0 ? application.Party.FindItem(new ItemInstanceId(id)) : null;
    private int RechargedCapacity(SpellApplication application, int capacity)
    {
        int rank = Math.Max(1, _spells.LevelOf(application));
        int mastery = MightAndMagic7Spells.MasteryOf(application.Caster, application.Spell);
        int percent = application.Source is not null ? 30 + rank : (mastery >= 4 ? 80 : mastery >= 3 ? 70 : 50) + rank;
        return (int)((long)capacity * Math.Min(100, percent) / 100);
    }
    private static bool IsQuest(ItemDefinitionId id) => int.TryParse(id.Value, out int number) && number is >= 600 and <= 699;
    private static bool Special(ItemFacts facts) => facts.Material.Equals("artifact", StringComparison.OrdinalIgnoreCase) ||
        facts.Material.Equals("relic", StringComparison.OrdinalIgnoreCase) || facts.Material.Equals("special", StringComparison.OrdinalIgnoreCase);
    private static bool Weapon(ItemFacts facts) => facts.Kind is "single-handed" or "two-handed" or "bow";
    private static bool Passive(ItemFacts facts) => facts.Kind is "armour" or "armor" or "shield" or "helmet" or "belt" or "cloak" or "gauntlets" or "boots" or "ring" or "amulet";
    private static Refusal Refuse(string code, string message) => new(code, message);
    private static SpellApplicationOutcome Outcome(SpellApplication application, ItemInstance item, string message) =>
        SpellApplicationOutcome.Expressed(application.Spell.Effect, $"{application.Spell.Name}: item {item.Id} {message}.",
            [new SpellEffectFact("item", item.Id.ToString()), new SpellEffectFact("property", item.State.Enchantment?.Property ?? string.Empty)]);
    private readonly record struct ItemFacts(string Name, string Kind, string Material, int Value);
}
