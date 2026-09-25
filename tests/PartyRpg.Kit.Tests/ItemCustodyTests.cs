using PartyRpg.Kit.Party;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// The item custody questions the party entity suite leaves open: what a capture carries and a restore
/// rebuilds, one definition worn twice when the data names two slots for it, and where the pack's capacity
/// comes from.
/// </summary>
/// <remarks>
/// Each test here states a claim <c>PartyEntityTests</c> does not exercise, so the two files together cover
/// the item acceptance rather than repeating it. The kit holds no vocabulary in either: every slot,
/// definition, enchantment, size, and allowance below is the test's own data, which is what the seams are
/// for.
/// </remarks>
public sealed class ItemCustodyTests
{
    private static readonly RaceId TestRace = new("testfolk");
    private static readonly ClassId Fighter = new("fighter");
    private static readonly AttributeId Vigour = new("vigour");
    private static readonly SpellId Spark = new("spark");
    private static readonly EquipmentSlot Hand = new("hand");
    private static readonly EquipmentSlot RingLeft = new("ring-left");
    private static readonly EquipmentSlot RingRight = new("ring-right");
    private static readonly ItemDefinitionId Blade = new("blade");
    private static readonly ItemDefinitionId Ring = new("ring");
    private static readonly ItemDefinitionId Arrow = new("arrow");
    private static readonly EnchantmentId Keen = new("keen");
    private static readonly EnchantmentId Warded = new("warded");

    [Fact]
    public void An_artifacts_identity_state_and_custody_survive_a_capture_and_a_restore()
    {
        using PartyEntity original = Build(new PartyEntityFactory(), Member("Ann"));
        PartyMember ann = original.Members[0];
        ItemInstance artifact = original.AcquireItem(Blade).Item!;
        artifact.Identify();
        artifact.TakeDamage(4);
        artifact.Enchant(new ItemEnchantment(Keen, 2));
        artifact.Enchant(new ItemEnchantment(Warded, 1));
        Assert.True(original.Equip(ann.Id, Hand, artifact.Id).Admitted);
        ItemInstance loose = original.AcquireItem(Arrow).Item!;

        // A restore rebuilds the party from the capture alone, which is the guarantee a save file will need:
        // what a save must carry is exactly what this round trip loses if it is missing. Writing these values
        // to a store and reading them back on the next launch is the persistence owner's half.
        PartySave save = original.Capture();
        using PartyEntity restored = new PartyEntityFactory().Restore(save);

        ItemInstance back = restored.Items.Single(item => item.Id == artifact.Id);
        Assert.Equal(Blade, back.Definition);
        Assert.True(back.State.IsIdentified);
        Assert.Equal(4, back.State.Damage);
        Assert.Equal(2, back.State.Enchantments.Count);
        Assert.Equal(new ItemEnchantment(Keen, 2), back.State.Enchantments[0]);
        Assert.Equal(new ItemEnchantment(Warded, 1), back.State.Enchantments[1]);
        Assert.Equal(ItemCustody.EquippedBy(restored.Members[0].Id, Hand), back.Custody);
        Assert.Same(back, restored.Members[0].Equipment.ItemIn(Hand));

        // The loose half of the party comes back too, in the shared pack and nowhere else.
        ItemInstance looseBack = restored.Items.Single(item => item.Id == loose.Id);
        Assert.Equal(Arrow, looseBack.Definition);
        Assert.True(looseBack.Custody.IsInSharedInventory);
        Assert.Equal(2, restored.Items.Count);

        // A capture is a value and not a view of live state: playing on cannot rewrite what a later load
        // reads, which is what lets one captured save be loaded twice and give the same artifact both times.
        back.TakeDamage(10);
        back.Disenchant(Keen);
        ItemSave recorded = save.Items.Single(item => item.Id == artifact.Id);
        Assert.Equal(4, recorded.State.Damage);
        Assert.Equal(2, recorded.State.Enchantments.Count);

        using PartyEntity reloaded = new PartyEntityFactory().Restore(save);
        ItemInstance again = reloaded.Items.Single(item => item.Id == artifact.Id);
        Assert.Equal(4, again.State.Damage);
        Assert.Equal(2, again.State.Enchantments.Count);
    }

    [Fact]
    public void One_definition_can_be_worn_twice_when_the_data_names_two_slots_for_it()
    {
        // The original wears one of each item type at a time except rings
        // (docs/research/mm7-manual-outline.md, p.23). The kit knows no item type and no slot, so that
        // exception is expressed as data: two slots, two instances of one definition, both worn. The only
        // thing the kit refuses is two instances in one slot, which is a contradiction rather than a rule.
        using PartyEntity party = Build(
            new PartyEntityFactory(stacking: new Bundles(new Dictionary<string, int> { [Arrow.Value] = 3 })),
            Member("Ann"));
        PartyMember ann = party.Members[0];
        ItemInstance left = party.AcquireItem(Ring).Item!;
        ItemInstance right = party.AcquireItem(Ring).Item!;

        Assert.True(party.Equip(ann.Id, RingLeft, left.Id).Admitted);
        Assert.True(party.Equip(ann.Id, RingRight, right.Id).Admitted);

        Assert.Equal(2, ann.Equipment.Count);
        Assert.Same(left, ann.Equipment.ItemIn(RingLeft));
        Assert.Same(right, ann.Equipment.ItemIn(RingRight));
        Assert.Equal(ItemCustody.EquippedBy(ann.Id, RingLeft), left.Custody);
        Assert.Equal(ItemCustody.EquippedBy(ann.Id, RingRight), right.Custody);
        Assert.Empty(party.Inventory.Items);

        // Two of a kind stay two things: identifying or damaging one says nothing about the other, which is
        // why each instance carries its own state rather than the definition carrying it for all copies.
        left.Identify();
        left.TakeDamage(1);
        Assert.False(right.State.IsIdentified);
        Assert.Equal(0, right.State.Damage);

        // Bundling is the shared pack's business and reaches nothing a member wears: what the data lets
        // bundle up in the pack tops up the loose stack, while the two worn rings stay two things in two
        // slots, because only what lies in the pack merges.
        Assert.True(party.AcquireItem(Arrow, 2).Admitted);
        ItemAcquisition bundled = party.AcquireItem(Arrow);
        Assert.True(bundled.Admitted);
        Assert.Equal(3, bundled.Item!.StackCount);
        Assert.True(bundled.Item.Custody.IsInSharedInventory);
        Assert.Same(left, ann.Equipment.ItemIn(RingLeft));
        Assert.Same(right, ann.Equipment.ItemIn(RingRight));
        Assert.Equal(3, party.Items.Count);
    }

    [Fact]
    public void Capacity_comes_from_the_rules_data_and_not_from_a_number_in_the_kit()
    {
        // The same three offers judged by one table with two allowances and by no rule at all. A constant
        // hiding in the mechanism could not let the third party differ from the first two.
        Dictionary<string, int> sizes = new() { [Blade.Value] = 4, [Arrow.Value] = 1 };
        using PartyEntity tight = Build(
            new PartyEntityFactory(inventoryCapacity: new Footprints(sizes, allowance: 6)),
            Member("Ann"));
        Assert.True(tight.AcquireItem(Blade).Admitted);

        ItemAcquisition refused = tight.AcquireItem(Arrow, 3);

        Assert.False(refused.Admitted);
        Assert.Equal("no-room", refused.Refusal!.Code);
        Assert.Single(tight.Inventory.Items);

        using PartyEntity roomy = Build(
            new PartyEntityFactory(inventoryCapacity: new Footprints(sizes, allowance: 12)),
            Member("Ann"));
        Assert.True(roomy.AcquireItem(Blade).Admitted);
        Assert.True(roomy.AcquireItem(Arrow, 3).Admitted);
        Assert.Equal(4, roomy.Inventory.Items.Count);

        // With no rule the pack takes everything, which is the honest state of a game that has not said what
        // the party can carry — and the evidence that no limit lives in the kit.
        using PartyEntity unbounded = Build(new PartyEntityFactory(), Member("Ann"));
        Assert.True(unbounded.AcquireItem(Blade).Admitted);
        Assert.True(unbounded.AcquireItem(Arrow, 8).Admitted);
        Assert.Equal(9, unbounded.Inventory.Items.Count);
    }

    private static MemberCreation Member(string name) => new(Seed(name));

    private static PartyMemberSeed Seed(string name) => new(
        name,
        TestRace,
        Fighter,
        [new AttributeScore(Vigour, 12)],
        [],
        [Spark],
        experience: 0,
        level: 1,
        skillPoints: 0,
        classRank: 1,
        conditions: [],
        hitPoints: ResourcePool.Full(10),
        spellPoints: ResourcePool.Full(5));

    private static PartyEntity Build(PartyEntityFactory factory, params MemberCreation[] members) =>
        factory.Create(new PartyCreation(members, coins: 100, foodPortions: 10));

    /// <summary>A pack allowance over per-definition sizes, of the kind content carries: sizes and total are both data.</summary>
    private sealed class Footprints : IInventoryCapacityRule
    {
        private readonly Dictionary<string, int> _sizes;
        private readonly int _allowance;

        internal Footprints(Dictionary<string, int> sizes, int allowance)
        {
            _sizes = sizes;
            _allowance = allowance;
        }

        public PartyRefusal? Judge(IReadOnlyList<ItemInstance> held, ItemDefinitionId definition, int count)
        {
            int used = 0;
            foreach (ItemInstance item in held) used = checked(used + (Size(item.Definition) * item.StackCount));
            int incoming = Size(definition) * count;
            return used + incoming > _allowance
                ? new PartyRefusal("no-room", $"The pack holds {used} and takes {incoming} more, up to {_allowance}.")
                : null;
        }

        /// <summary>A definition the table does not mention takes one unit, so content states only exceptions.</summary>
        private int Size(ItemDefinitionId definition) => _sizes.GetValueOrDefault(definition.Value, 1);
    }

    /// <summary>How far copies of a definition bundle, stated by the test's data rather than by the kit.</summary>
    private sealed class Bundles : IItemStackingRule
    {
        private readonly Dictionary<string, int> _limits;

        internal Bundles(Dictionary<string, int> limits) => _limits = limits;

        public int MaximumStack(ItemDefinitionId definition) => _limits.GetValueOrDefault(definition.Value, 1);
    }
}
