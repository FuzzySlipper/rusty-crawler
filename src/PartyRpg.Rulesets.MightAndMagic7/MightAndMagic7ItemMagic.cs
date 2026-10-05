using System.Globalization;
using PartyRpg.Kit;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Magic;
using PartyRpg.Kit.Knowledge;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.World;
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
internal sealed class MightAndMagic7ItemMagic : IItemUseRule
{
    private readonly Dictionary<ItemDefinitionId, ItemFacts> _items = [];
    private readonly MightAndMagic7Spells? _spells;
    private readonly GameClock? _clock;
    private readonly IRandomService? _random;
    private readonly Func<PartyEntity?> _party;
    private readonly TuningProfile _tuning;
    private readonly Func<SessionWorld?> _world;
    private readonly Dictionary<ItemDefinitionId, string> _travelItems = [];
    private const string Attempts = "item:enchant-attempts";

    internal MightAndMagic7ItemMagic(ContentCatalog? catalog, MightAndMagic7Spells? spells, GameClock? clock,
        IRandomService? random, Func<PartyEntity?> party, Func<SessionWorld?>? world = null)
    {
        _spells = spells;
        _clock = clock;
        _random = random;
        _party = party;
        _world = world ?? (() => null);
        _tuning = MightAndMagic7Tuning.Read(catalog);
        if (catalog is null) return;
        foreach ((_, _, ContentEntry entry) in catalog.Entries("item"))
            _items[new ItemDefinitionId(entry.Id)] = new(entry.GetString("name"), entry.GetString("type"),
                entry.GetString("material"), entry.GetDouble("value") is { } price ? (int)Math.Clamp(price, 0, int.MaxValue) : 0);
        var links = catalog.Entries("travel-link").ToDictionary(row => row.Entry.Id, row => row.Entry);
        foreach ((var pack, var document, var entry) in catalog.Entries("item-travel"))
        {
            ItemDefinitionId item = new(entry.GetId("item"));
            string link = entry.GetId("link");
            if (!_items.ContainsKey(item) || !links.TryGetValue(link, out var travel) || travel.GetId("fromPlace").Length > 0 ||
                !_travelItems.TryAdd(item, link))
                throw new ContentValidationException("A travel item must name one held-item definition and one world-issued transition.",
                    [new("item-travel-invalid", $"Travel item '{entry.Id}' names item '{item}' and link '{link}'.", pack.PackId, document.DocumentId)]);
        }
    }

    internal IReadOnlyList<SpellAim> Aims() => _party() is { } party
        ? [.. party.Items.Select(item => new SpellAim(item.Id.ToString(), Describe(item), "item"))] : [];

    public string Describe(ItemInstance item) =>
        $"{(_items.TryGetValue(item.Definition, out ItemFacts facts) ? facts.Name : item.Definition.Value)} ({item.Id})" +
        (Active(item) is { } property ? $" — {property.Property} {property.Strength}" : string.Empty) +
        (item.State.IsHardened ? " — hardened" : string.Empty) +
        (PowerText(item) is { Length: > 0 } powers ? $" — {powers}" : string.Empty);

    string IItemUseRule.Describe(ItemInstance item) => PowerText(item);

    public string Describe(PartyMember member)
    {
        List<string> terms = [.. member.Resistances.Scores.Select(score => $"permanent {score.Kind.Value} resistance {score.Points}")];
        foreach (string property in new[] { "Might", "Speed", "Luck" })
            if (WornBonus(member, property) is not 0 and int bonus) terms.Add($"worn {property} {bonus:+0;-0}");
        foreach (DamageKindId kind in new[] { MightAndMagic7Damage.Fire, MightAndMagic7Damage.Air, MightAndMagic7Damage.Water,
            MightAndMagic7Damage.Earth, MightAndMagic7Damage.Mind, MightAndMagic7Damage.Body })
            if (WornResistance(member, kind) is not 0 and int bonus) terms.Add($"worn {kind.Value} resistance {bonus:+0;-0}");
        if (ShieldsMissiles(member)) terms.Add("hostile missile damage halved");
        return string.Join("; ", terms);
    }

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
            if (_spells?.Reading(item.Definition) is not { ConsumedByUse: false } reading)
                return Refuse(MightAndMagic7Codes.ItemMagicKind, $"{facts.Name} is not a charged wand.");
            int capacity = item.State.ChargeCapacity ?? reading.Charges;
            int refilled = RechargedCapacity(application, capacity);
            return refilled <= 0 || refilled <= capacity - item.State.ChargesSpent
                ? Refuse(MightAndMagic7Codes.ItemMagicCharged, $"{facts.Name} is already as charged as this recharge can leave it.") : null;
        }
        bool weapon = Weapon(facts);
        bool equipment = weapon || Passive(facts) || (shape == ItemMagicShape.Harden && facts.Kind == "wand");
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
        int rank = Math.Max(1, _spells!.LevelOf(application));
        int mastery = MightAndMagic7Spells.MasteryOf(application.Caster, application.Spell);
        if (shape == ItemMagicShape.Recharge)
        {
            int capacity = item.State.ChargeCapacity ?? _spells!.Reading(item.Definition)!.Value.Charges;
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

    internal static IReadOnlyList<SaveProblem> Problems(SessionSave save, ContentCatalog? catalog)
    {
        MightAndMagic7Spells? spells = MightAndMagic7Spells.Read(catalog);
        MightAndMagic7ItemMagic owner = new(catalog, spells, null, null, () => null);
        List<SaveProblem> problems = [];
        foreach (ItemSave item in save.Party.Items)
        {
            if (!owner._items.TryGetValue(item.Definition, out ItemFacts facts)) continue; // content identity is judged by its existing owner
            bool special = Special(facts) || IsQuest(item.Definition);
            if (item.State.IsHardened && (special || !(Weapon(facts) || Passive(facts) || facts.Kind == "wand")))
                problems.Add(new(MightAndMagic7Codes.SaveItemHardening, item.Id.ToString(), $"item {item.Id} cannot be hardened in this game's item table"));
            if (item.State.Enchantment is { } property)
            {
                bool weaponProperty = property.Property is "fire" or "frost" or "poison" or "sparks" or "vampiric" or "swift" or "dragon";
                bool passiveProperty = property.Property is "Might" or "Endurance" or "Speed" or "armour";
                int largest = (int)MightAndMagic7Tuning.EnchantGrandMasterLow.Maximum + 6;
                if (special || property.Strength > largest || !(weaponProperty && Weapon(facts) || passiveProperty && Passive(facts)))
                    problems.Add(new(MightAndMagic7Codes.SaveItemProperty, item.Id.ToString(), $"item {item.Id} bears an unsupported property {property.Property} {property.Strength} for its item kind"));
            }
            if (item.State.Enchantment?.DueElapsedMilliseconds is { } due && due <= save.Clock.ElapsedMilliseconds)
                problems.Add(new(MightAndMagic7Codes.SaveItemDeadline, item.Id.ToString(), $"item {item.Id} carries a property whose clock deadline has already passed"));
            SpellItemReading? carried = spells?.Reading(item.Definition);
            bool charged = carried is { ConsumedByUse: false };
            int maximum = charged ? item.State.ChargeCapacity ?? carried!.Value.Charges : 0;
            if (item.State.ChargesSpent > maximum || (item.State.ChargeCapacity is { } capacity && (!charged || capacity > carried!.Value.Charges)))
                problems.Add(new(MightAndMagic7Codes.SaveItemCapacity, item.Id.ToString(), $"item {item.Id} states charge capacity or use beyond what its item table can hold"));
        }
        return problems;
    }

    internal IReadOnlyList<AttackDamagePart> HarmOf(PartyMember member, CombatSubject target, AttackKind kind, MightAndMagic7Combat combat)
    {
        List<AttackDamagePart> parts = [];
        foreach (ItemInstance item in WeaponsOf(member, kind))
        {
            if (Active(item) is not { } property) continue;
            (DamageKindId Kind, int Amount)? harm = property.Property switch
            {
                "fire" => (MightAndMagic7Damage.Fire, 3 * property.Strength),
                "frost" => (MightAndMagic7Damage.Water, 3 * property.Strength),
                "poison" => (MightAndMagic7Damage.Body, 3 * property.Strength),
                "sparks" => (MightAndMagic7Damage.Air, 3 * property.Strength),
                "dragon" when combat.IsDragon(target) => (MightAndMagic7Damage.Physical, 6 * property.Strength),
                _ => null,
            };
            if (harm is { } added) parts.Add(new AttackDamagePart(added.Kind, DamageRoll.Flat(added.Amount)));
        }
        return parts;
    }

    internal void AfterHit(CombatHit hit)
    {
        if (hit.Actor.Member is not { } member || hit.Damage <= 0) return;
        if (WeaponsOf(member, hit.Kind).Any(item => Active(item)?.Property == "vampiric"))
            member.Resources.RestoreHitPoints(hit.Damage / 2);
    }

    // Character.cpp:1727-1733: one swift weapon takes twenty ticks, regardless of hands or potency.
    internal int RecoveryBonus(PartyMember member) => member.Equipment.Items.Any(worn =>
        worn.Item.State.Damage == 0 && Active(worn.Item)?.Property == "swift") ? 20 : 0;

    internal int WornBonus(PartyMember member, string property) => member.Equipment.Items
        .Where(worn => worn.Item.State.Damage == 0)
        .Sum(worn => (Active(worn.Item) is { } enchantment && enchantment.Property == property ? enchantment.Strength : 0)
            + FixedBonus(worn.Item, property));

    internal int WornResistance(PartyMember member, DamageKindId kind) => member.Equipment.Items
        .Where(worn => worn.Item.State.Damage == 0).Sum(worn => FixedBonus(worn.Item, "resistance:" + kind.Value));

    internal bool ShieldsMissiles(PartyMember member) => member.Equipment.Items.Any(worn =>
        worn.Item.State.Damage == 0 && IsFixed(worn.Item) && worn.Item.Definition.Value == "531");

    // Exact fixed identities from ItemEnums.h; values from Item.cpp PopulateArtifactBonusMap.
    // These selected rows are the repertoire, not a claim that every artifact power is compiled.
    private bool IsFixed(ItemInstance item) => _items.TryGetValue(item.Definition, out ItemFacts facts) && Special(facts);
    private int FixedBonus(ItemInstance item, string property)
    {
        if (!IsFixed(item)) return 0;
        return (item.Definition.Value, property) switch
        {
            ("500", "Speed") => 40,
            ("501", "Might") => 40,
            ("506", "resistance:Fire") => 50,
            ("525", "Speed" or "Luck") => 50,
            ("525", "resistance:Fire" or "resistance:Air" or "resistance:Water" or "resistance:Earth" or "resistance:Mind" or "resistance:Body") => -15,
            _ => 0,
        };
    }

    private string PowerText(ItemInstance item) => item.Definition.Value == "616"
        ? "Use: one permanent elemental, mind or body resistance gift; consumed; approximate"
        : !IsFixed(item) ? "" : item.Definition.Value switch
        {
            "500" => "fixed Speed +40",
            "501" => "fixed Might +40",
            "506" => "fixed Fire resistance +50",
            "525" => "fixed Speed/Luck +50; Fire/Air/Water/Earth/Mind/Body resistance -15",
            "531" => "halves hostile missiles; other fixed powers are not compiled",
            _ => "fixed special powers are not compiled",
        };

    public string? ActionOf(ItemInstance item) =>
        _spells?.TaughtBy(item.Definition) is not null ? "Study" :
        (_travelItems.ContainsKey(item.Definition) || item.Definition.Value == "616" && _items.ContainsKey(item.Definition)) ? "Use" : null;

    public ItemUseResult Use(PartyEntity party, PartyMember member, ItemInstance item)
    {
        if (ActionOf(item) is null) return ItemUseResult.Refused(new(ItemUseCodes.Unsupported, "This item has no ordinary use in this ruleset."));
        if (_travelItems.TryGetValue(item.Definition, out string? link))
        {
            if (!MightAndMagic7Conditions.CanAct(member))
                return ItemUseResult.Refused(new(MightAndMagic7Codes.ItemUseMemberIncapable, $"{member.Profile.Name} must recover before using {_items[item.Definition].Name}."));
            if (item.State.Damage > 0)
                return ItemUseResult.Refused(new(MightAndMagic7Codes.ItemUseBroken, "Repair the travel item before using it."));
            if (_world() is not { } world || world.Graph.Transitions.FirstOrDefault(candidate => candidate.Source == link) is not { } transition)
                return ItemUseResult.Refused(new("item-travel-world-absent", "This travel item needs its destination in the loaded world."));
            if (world.Place == transition.To)
                return ItemUseResult.Refused(new("item-travel-already-there", "The party is already inside; use the exit to leave."));
            TransitionResult journey = world.Travel(transition with { From = world.Place }, TransitionKind.Portal);
            return journey.Arrived
                ? new(true, "item-use-applied", $"{_items[item.Definition].Name} carries the party to {world.Graph.Require(journey.Place).Name}; the item is retained.")
                : ItemUseResult.Refused(journey.Refusal!);
        }
        if (_spells is { } spells && spells.TaughtBy(item.Definition) is { } taught)
        {
            SpellDefinition spell = spells.Catalog.Read(taught);
            if (!MightAndMagic7Conditions.CanAct(member))
            {
                return ItemUseResult.Refused(new(
                    MightAndMagic7Codes.ItemUseMemberIncapable,
                    $"{member.Profile.Name} must recover before studying {spell.Name}."));
            }

            if (spells.MayLearn(member, spell) is { } refused) return ItemUseResult.Refused(refused);
            if (party.JudgeItemRemoval(item.Id) is { } bookCustodyRefusal) return ItemUseResult.Refused(bookCustodyRefusal);

            ItemRemoval bookRemoval = party.ConsumeItem(item.Id);
            if (!bookRemoval.Removed) return ItemUseResult.Refused(bookRemoval.Refusal!);

            // Eligibility and custody were judged above in this admitted update. The item owner consumes the
            // book first, then the character's own spellbook owner records the lesson; no second callback or
            // rollback path exists between these canonical mutations.
            member.Spells.Learn(spell.Id);

            return new(
                true,
                "item-use-applied",
                $"{member.Profile.Name} studies {spell.Name}; the book is consumed and the spell is learned.");
        }

        if (!MightAndMagic7Conditions.CanAct(member))
            return ItemUseResult.Refused(new(MightAndMagic7Codes.ItemUseMemberIncapable, $"{member.Profile.Name} must recover before using the lamp."));
        if (item.State.Damage > 0)
            return ItemUseResult.Refused(new(MightAndMagic7Codes.ItemUseBroken, "Repair the Genie Lamp before using it."));
        if (_clock is null || _random is null)
            return ItemUseResult.Refused(new(MightAndMagic7Codes.ItemUseOwnerAbsent, "The Genie Lamp needs the session clock and keyed rolls."));
        if (party.JudgeItemRemoval(item.Id) is { } kept) return ItemUseResult.Refused(kept);
        DamageKindId[] kinds = [MightAndMagic7Damage.Fire, MightAndMagic7Damage.Air, MightAndMagic7Damage.Water,
            MightAndMagic7Damage.Earth, MightAndMagic7Damage.Mind, MightAndMagic7Damage.Body];
        // Our explicit adaptation always takes the donor's December resistance branch, without its calendar curses.
        KeyedRolls rolls = new(_random, MightAndMagic7Loot.RollSeed, "mm7.items.lamp", item.Id.ToString());
        DamageKindId kind = kinds[rolls.Pick(kinds.Length)];
        int value = _clock.Calendar.WeekOfMonth(_clock.Now);
        int before = member.Resistances.Of(kind);
        if ((long)before + value > int.MaxValue)
            return ItemUseResult.Refused(new(MightAndMagic7Codes.ItemUseResistanceFull, "This permanent resistance cannot be raised further."));
        ItemRemoval removed = party.ConsumeItem(item.Id);
        if (!removed.Removed) return ItemUseResult.Refused(removed.Refusal!);
        member.Resistances.Set(kind, before + value);
        return new(true, "item-use-applied", $"{member.Profile.Name} uses the Genie Lamp: permanent {kind.Value} resistance +{value}, now {before + value}; the lamp is consumed.");
    }

    private IEnumerable<ItemInstance> WeaponsOf(PartyMember member, AttackKind kind)
    {
        if (kind == AttackKind.Spell) yield break;
        EquipmentSlot[] slots = kind == AttackKind.Ranged ? [MightAndMagic7Figure.Bow] : [MightAndMagic7Figure.MainHand, MightAndMagic7Figure.OffHand];
        foreach (EquipmentSlot slot in slots)
            if (member.Equipment.ItemIn(slot) is { State.Damage: 0 } item && _items.TryGetValue(item.Definition, out ItemFacts facts) && Weapon(facts)) yield return item;
    }

    private ItemInstance? Target(SpellApplication application) => ulong.TryParse(application.TargetName, NumberStyles.None,
        CultureInfo.InvariantCulture, out ulong id) && id != 0 ? application.Party.FindItem(new ItemInstanceId(id)) : null;
    private int RechargedCapacity(SpellApplication application, int capacity)
    {
        int rank = Math.Max(1, _spells!.LevelOf(application));
        int mastery = MightAndMagic7Spells.MasteryOf(application.Caster, application.Spell);
        long percent = application.Source is not null ? 30L + rank : (mastery >= 4 ? 80L : mastery >= 3 ? 70L : 50L) + rank;
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
