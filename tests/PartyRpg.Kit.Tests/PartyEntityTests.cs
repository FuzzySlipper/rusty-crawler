using PartyRpg.Kit.Party;
using PartyRpg.Kit.Progression;
using PartyRpg.Kit.World;
using Rusty.Engine.Entities;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// The party entity and the components attached to it: the roster and its members, the one shared pack, the
/// equipped figure a member owns, the party's resources, identity discipline, and what a save round-trips.
/// </summary>
/// <remarks>
/// Every rule a party obeys in these tests is the test's own choice — a stacking limit, a pack size, an
/// equipment gate — which is the point of the seams: the kit holds no number and no name a game would
/// recognize, so a test can state its own and demand that the same mechanism serve it.
/// </remarks>
public sealed class PartyEntityTests
{
    private static readonly RaceId TestRace = new("testfolk");
    private static readonly ClassId Fighter = new("fighter");
    private static readonly ClassId Adept = new("adept");
    private static readonly SkillId Blades = new("blades");
    private static readonly SkillId Lore = new("lore");
    private static readonly AttributeId Vigour = new("vigour");
    private static readonly AttributeId Wit = new("wit");
    private static readonly SpellId Spark = new("spark");
    private static readonly ConditionId Weakened = new("weakened");
    private static readonly EffectId Warded = new("warded");
    private static readonly EffectId Swift = new("swift");
    private static readonly EnchantmentId Keen = new("keen");
    private static readonly EquipmentSlot Hand = new("hand");
    private static readonly FollowerDefinitionId Porter = new("porter");
    private static readonly ItemDefinitionId Blade = new("blade");
    private static readonly ItemDefinitionId Arrow = new("arrow");
    private static readonly ItemDefinitionId Torch = new("torch");

    [Fact]
    public void A_created_party_carries_its_components_on_one_engine_entity()
    {
        using PartyEntity party = Build(
            new PartyEntityFactory(),
            Member("Ann", Fighter, new SkillEntry(Blades, 2, new SkillTier(1), 3)),
            Member("Bo", Adept, new SkillEntry(Lore, 1, SkillTier.None, 1)));

        // The facade reads the entity's actual components: one party, with every party-scoped component
        // attached to it rather than kept in a parallel graph.
        Assert.True(party.Actor.Has<PartyRoster>());
        Assert.True(party.Actor.Has<PartyInventory>());
        Assert.True(party.Actor.Has<PartyPurse>());
        Assert.True(party.Actor.Has<PartyFood>());
        Assert.True(party.Actor.Has<PartyReputation>());
        Assert.True(party.Actor.Has<PartyFollowers>());
        Assert.True(party.Actor.Has<PartyEffects>());
        Assert.True(party.Actor.Has<PartyIdentitySource>());
        Assert.Equal(new[] { "Ann", "Bo" }, party.Members.Select(member => member.Profile.Name));

        // Members are entities of their own, carrying what a character is and what it wears.
        PartyMember ann = party.Members[0];
        Assert.True(ann.Actor.Has<CharacterProfile>());
        Assert.True(ann.Actor.Has<CharacterAttributes>());
        Assert.True(ann.Actor.Has<CharacterSkills>());
        Assert.True(ann.Actor.Has<CharacterSpells>());
        Assert.True(ann.Actor.Has<CharacterProgression>());
        Assert.True(ann.Actor.Has<CharacterConditions>());
        Assert.True(ann.Actor.Has<CharacterResources>());
        Assert.True(ann.Actor.Has<CharacterEquipment>());
        Assert.Equal(12, ann.Attributes[Vigour]);
        Assert.Equal(2, ann.Skills.LevelOf(Blades));
        Assert.Equal(new SkillTier(1), ann.Skills.TierOf(Blades));
        Assert.True(ann.Spells.Knows(Spark));
        Assert.Equal(100, party.Purse.Coins);
        Assert.Equal(10, party.Food.Portions);
        Assert.Equal(5, party.Reputation.Reputation);
        Assert.Equal(2, party.Reputation.Fame);
        Assert.Empty(party.Inventory.Items);
        Assert.Empty(party.Items);
    }

    [Fact]
    public void Wrapping_a_party_entity_creates_no_component()
    {
        using EntityStore store = new();
        Actor entity = new(store, store.Create(new EntityTypeId(PartyEntity.EntityKind), EntityLifecycle.Active));
        PartyEntity wrapped = PartyEntity.Wrap(entity);

        // Wrapping is composition, not construction: a party entity that carries nothing reports that on the
        // read rather than growing an empty component the caller never asked for.
        Assert.Throws<InvalidOperationException>(() => wrapped.Inventory);
        Assert.Throws<InvalidOperationException>(() => wrapped.Roster);
        Assert.Empty(store.Diagnostics().Components);

        // A wrapped party borrows the store, so disposing the facade leaves the caller's store alone.
        wrapped.Dispose();
        Assert.Equal(1, store.Diagnostics().EntityCount);
    }

    [Fact]
    public void Creation_equips_starting_equipment_through_the_gated_path()
    {
        SkillGatedEquipment gate = new(new Dictionary<string, (string Class, string Skill)>
        {
            [Blade.Value] = (Fighter.Value, Blades.Value),
        });
        MemberCreation ann = new(
            Seed("Ann", Fighter, new SkillEntry(Blades, 1, SkillTier.None, 1)),
            [new StartingEquipment(Hand, Blade)]);

        using PartyEntity party = Build(new PartyEntityFactory(gate), ann);

        PartyMember member = party.Members[0];
        Assert.True(member.Equipment.TryGet(Hand, out ItemInstance? equipped));
        Assert.Equal(Blade, equipped.Definition);
        Assert.True(equipped.Custody.IsEquipped);
        Assert.Empty(party.Inventory.Items);

        // Creation is a real flow: a blueprint the rules refuse fails while the party is being built, rather
        // than producing a character who quietly wields what it may not use.
        MemberCreation bo = new(
            Seed("Bo", Fighter, new SkillEntry(Lore, 1, SkillTier.None, 1)),
            [new StartingEquipment(Hand, Blade)]);
        ArgumentException refused = Assert.Throws<ArgumentException>(() => Build(new PartyEntityFactory(gate), bo));
        Assert.Contains("skill-missing", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Picking_something_up_lands_in_the_shared_inventory_and_not_on_a_member()
    {
        using PartyEntity party = Build(new PartyEntityFactory(), Member("Ann", Fighter));
        PartyMember ann = party.Members[0];

        ItemAcquisition taken = party.AcquireItem(Blade);

        Assert.True(taken.Admitted);
        Assert.Equal(1, taken.Count);
        Assert.Same(taken.Item, party.Inventory.Items.Single());
        Assert.True(taken.Item!.Custody.IsInSharedInventory);
        Assert.Empty(ann.Equipment.Items);
        Assert.Equal(0, ann.Equipment.Count);

        // There is no per-character carried list to land in: what the party owns lies in the one pack, and
        // the member's item state is still its (empty) equipped figure.
        Assert.Single(party.Items);
    }

    [Fact]
    public void Equipping_moves_the_instance_from_the_pack_onto_the_member()
    {
        using PartyEntity party = Build(new PartyEntityFactory(), Member("Ann", Fighter));
        PartyMember ann = party.Members[0];
        ItemInstance blade = party.AcquireItem(Blade).Item!;

        EquipmentChange change = party.Equip(ann.Id, Hand, blade.Id);

        Assert.True(change.Admitted);
        Assert.Same(blade, change.Equipped);
        Assert.Null(change.Displaced);
        Assert.True(ann.Equipment.TryGet(Hand, out ItemInstance? worn));
        Assert.Same(blade, worn);
        Assert.Equal(ItemCustody.EquippedBy(ann.Id, Hand), worn!.Custody);
        Assert.Empty(party.Inventory.Items);
        Assert.Same(blade, party.Items.Single());
    }

    [Fact]
    public void Equipping_into_an_occupied_slot_returns_what_was_displaced_to_the_pack()
    {
        using PartyEntity party = Build(new PartyEntityFactory(), Member("Ann", Fighter));
        PartyMember ann = party.Members[0];
        ItemInstance torch = party.AcquireItem(Torch).Item!;
        ItemInstance blade = party.AcquireItem(Blade).Item!;
        Assert.True(party.Equip(ann.Id, Hand, torch.Id).Admitted);

        EquipmentChange change = party.Equip(ann.Id, Hand, blade.Id);

        Assert.True(change.Admitted);
        Assert.Same(blade, change.Equipped);
        Assert.Same(torch, change.Displaced);
        Assert.Same(blade, ann.Equipment.ItemIn(Hand));
        Assert.Same(torch, party.Inventory.Items.Single());
        Assert.True(torch.Custody.IsInSharedInventory);
    }

    [Fact]
    public void Unequipping_returns_the_instance_to_the_pack()
    {
        using PartyEntity party = Build(new PartyEntityFactory(), Member("Ann", Fighter));
        PartyMember ann = party.Members[0];
        ItemInstance blade = party.AcquireItem(Blade).Item!;
        party.Equip(ann.Id, Hand, blade.Id);

        EquipmentChange change = party.Unequip(ann.Id, Hand);

        Assert.True(change.Admitted);
        Assert.Null(change.Equipped);
        Assert.Same(blade, change.Displaced);
        Assert.False(ann.Equipment.Has(Hand));
        Assert.Same(blade, party.Inventory.Items.Single());
        Assert.True(blade.Custody.IsInSharedInventory);
    }

    [Fact]
    public void An_item_one_member_wears_can_be_handed_to_another()
    {
        using PartyEntity party = Build(
            new PartyEntityFactory(),
            Member("Ann", Fighter),
            Member("Bo", Adept));
        ItemInstance blade = party.AcquireItem(Blade).Item!;
        PartyMember ann = party.Members[0];
        PartyMember bo = party.Members[1];
        party.Equip(ann.Id, Hand, blade.Id);

        EquipmentChange change = party.Equip(bo.Id, Hand, blade.Id);

        Assert.True(change.Admitted);
        Assert.False(ann.Equipment.Has(Hand));
        Assert.Same(blade, bo.Equipment.ItemIn(Hand));
        Assert.Equal(ItemCustody.EquippedBy(bo.Id, Hand), blade.Custody);
        Assert.Empty(party.Inventory.Items);
        Assert.Single(party.Items);
    }

    [Fact]
    public void Every_item_the_party_holds_lies_in_exactly_one_place()
    {
        SkillGatedEquipment gate = new(new Dictionary<string, (string Class, string Skill)>
        {
            [Blade.Value] = (Fighter.Value, Blades.Value),
            [Torch.Value] = (Fighter.Value, Blades.Value),
        });
        using PartyEntity party = Build(
            new PartyEntityFactory(gate, stacking: new StackLimits(new Dictionary<string, int> { [Arrow.Value] = 4 })),
            Member("Ann", Fighter, new SkillEntry(Blades, 1, SkillTier.None, 1)),
            Member("Bo", Adept));
        PartyMember ann = party.Members[0];
        ItemInstance blade = party.AcquireItem(Blade).Item!;
        party.AcquireItem(Torch);
        party.AcquireItem(Arrow, 6);
        party.Equip(ann.Id, Hand, blade.Id);

        // Walk the whole party the way a save does and account for every instance exactly once: the loose
        // ones in the shared pack, the worn ones in a slot, and each instance's own custody agreeing with
        // where it was found. A per-character pack would make one of these counts disagree.
        HashSet<ItemInstanceId> seen = [];
        foreach (ItemInstance item in party.Items) Assert.True(seen.Add(item.Id), $"Item {item.Id} was reached twice.");

        List<ItemInstance> inPack = [.. party.Inventory.Items];
        List<ItemInstance> onFigures = [.. party.Members.SelectMany(member => member.Equipment.Items).Select(worn => worn.Item)];
        Assert.Equal(party.Items.Count, inPack.Count + onFigures.Count);
        Assert.All(inPack, item => Assert.True(item.Custody.IsInSharedInventory));
        foreach (PartyMember member in party.Members)
        {
            foreach (EquippedItem worn in member.Equipment.Items)
            {
                Assert.Equal(ItemCustody.EquippedBy(member.Id, worn.Slot), worn.Item.Custody);
            }
        }

        Assert.Equal(4, party.Items.Count);
        Assert.Equal(3, inPack.Count);
        Assert.Single(onFigures);
        Assert.Equal(8, party.Items.Sum(item => item.StackCount));
        Assert.Equal(6, party.Inventory.TotalOf(Arrow));
    }

    [Fact]
    public void No_character_component_family_can_hold_items_a_member_is_not_wearing()
    {
        using PartyEntity party = Build(
            new PartyEntityFactory(),
            Member("Ann", Fighter),
            Member("Bo", Adept));
        party.AcquireItem(Arrow, 3);

        // The party's store enumerates every component family it holds, and the party composes exactly eight
        // party-scoped families and eight per-member ones. A per-character pack would be a ninth per-member
        // family and would fail here, which is what makes "only what it has equipped" structural rather than
        // a promise: there is no family for a loose list to live in.
        EntityStoreDiagnostics diagnostics = party.Store.Diagnostics();
        Assert.Equal(16, diagnostics.Components.Count);
        Assert.Equal(8, diagnostics.Components.Count(family => family.ValueCount == 1));
        Assert.Equal(8, diagnostics.Components.Count(family => family.ValueCount == party.Members.Count));
    }

    [Fact]
    public void Taking_more_than_a_stack_holds_starts_more_instances()
    {
        using PartyEntity party = Build(
            new PartyEntityFactory(stacking: new StackLimits(new Dictionary<string, int> { [Arrow.Value] = 3 })),
            Member("Ann", Fighter));

        ItemAcquisition first = party.AcquireItem(Arrow, 2);
        ItemAcquisition rest = party.AcquireItem(Arrow, 7);

        // The stack already in the pack takes one, and the remainder becomes whole stacks of its own.
        Assert.Equal(3, first.Item!.StackCount);
        Assert.Same(first.Item, rest.Item);
        Assert.Equal(3, party.Inventory.Items.Count);
        Assert.Equal(new[] { 3, 3, 3 }, party.Inventory.Items.Select(item => item.StackCount));
        Assert.Equal(9, party.Inventory.TotalOf(Arrow));
        Assert.Equal(3, party.Inventory.Items.Select(item => item.Id).Distinct().Count());
    }

    [Fact]
    public void An_instance_that_differs_in_state_never_merges_into_another()
    {
        using PartyEntity party = Build(
            new PartyEntityFactory(stacking: new StackLimits(new Dictionary<string, int> { [Arrow.Value] = 5 })),
            Member("Ann", Fighter));
        ItemInstance damaged = party.AcquireItem(Arrow, 2).Item!;
        damaged.TakeDamage(1);

        ItemAcquisition taken = party.AcquireItem(Arrow, 1);

        // A damaged item must not disappear into a sound stack: instances merge only when the whole state
        // matches.
        Assert.NotSame(damaged, taken.Item);
        Assert.Equal(2, party.Inventory.Items.Count);
        Assert.Equal(1, party.Inventory.Items.Single(item => !item.State.Matches(damaged.State)).StackCount);
    }

    [Fact]
    public void A_refused_pickup_leaves_the_party_and_the_offer_alone()
    {
        using PartyEntity party = Build(new PartyEntityFactory(inventoryCapacity: new LooseItemLimit(1)), Member("Ann", Fighter));
        party.AcquireItem(Blade);
        ItemInstance offered = party.CreateItem(Torch);

        ItemAcquisition refused = party.AcquireItem(offered);

        Assert.False(refused.Admitted);
        Assert.Equal("pack-full", refused.Refusal!.Code);
        Assert.Null(refused.Item);
        Assert.Single(party.Inventory.Items);
        Assert.True(offered.Custody.IsDetached);
        Assert.DoesNotContain(party.Items, item => item.Id == offered.Id);
    }

    [Fact]
    public void A_ruleset_rule_gates_equipping_and_nothing_moves_when_it_refuses()
    {
        SkillGatedEquipment gate = new(new Dictionary<string, (string Class, string Skill)>
        {
            [Blade.Value] = (Fighter.Value, Blades.Value),
        });
        using PartyEntity party = Build(new PartyEntityFactory(gate), Member("Bo", Adept, new SkillEntry(Lore, 1, SkillTier.None, 1)));
        PartyMember bo = party.Members[0];
        ItemInstance blade = party.AcquireItem(Blade).Item!;

        EquipmentChange change = party.Equip(bo.Id, Hand, blade.Id);

        Assert.False(change.Admitted);
        Assert.Equal("class-not-allowed", change.Refusal!.Code);
        Assert.False(bo.Equipment.Has(Hand));
        Assert.Same(blade, party.Inventory.Items.Single());

        // The kit knows no class and no skill: the rule was handed the member, read the class and the skills
        // off it, and answered for itself.
        Assert.Equal(new[] { Adept.Value }, gate.SeenClasses);
    }

    [Fact]
    public void Equipping_is_refused_when_the_pack_cannot_take_what_the_slot_would_displace()
    {
        using PartyEntity party = Build(new PartyEntityFactory(inventoryCapacity: new LooseItemLimit(1)), Member("Ann", Fighter));
        PartyMember ann = party.Members[0];
        ItemInstance torch = party.AcquireItem(Torch).Item!;
        party.Equip(ann.Id, Hand, torch.Id);
        ItemInstance blade = party.AcquireItem(Blade).Item!;

        EquipmentChange change = party.Equip(ann.Id, Hand, blade.Id);

        // A swap with nowhere to put the displaced item is refused whole: the party keeps what it had instead
        // of losing an item in the middle of a change.
        Assert.False(change.Admitted);
        Assert.Equal("pack-full", change.Refusal!.Code);
        Assert.Same(torch, ann.Equipment.ItemIn(Hand));
        Assert.Same(blade, party.Inventory.Items.Single());
    }

    [Fact]
    public void Raising_a_skill_charges_the_pool_and_the_entry_together()
    {
        // The points a character holds come from creation or from a level, so this member is created holding
        // five of them; what is under test is the one entry that may spend them, which is the progression
        // owner's rather than a member's own: a member's progression fields are reachable only from inside
        // the kit, which is what makes that entry the only way a pool can be charged.
        using PartyEntity party = HoldingSkillPoints(
            "Ann",
            Fighter,
            skillPoints: 5,
            new SkillEntry(Blades, 1, SkillTier.None, 0));
        PartyMember ann = party.Members[0];
        PartyProgression progression = new(new NoProgressionRule(), party);

        Assert.Null(progression.RaiseSkill(ann.Id, Blades, levels: 2, points: 3));

        Assert.Equal(2, ann.Progression.SkillPoints);
        Assert.Equal(3, ann.Skills.LevelOf(Blades));
        Assert.Equal(3, ann.Skills.Entries.Single().PointsSpent);

        PartyRefusal? refused = progression.RaiseSkill(ann.Id, Blades, levels: 1, points: 3);
        Assert.Equal("insufficient-skill-points", refused!.Code);
        Assert.Equal(3, ann.Skills.LevelOf(Blades));
    }

    [Fact]
    public void Resources_are_spent_and_restored_inside_their_pools()
    {
        using PartyEntity party = Build(new PartyEntityFactory(), Member("Ann", Fighter));
        PartyMember ann = party.Members[0];

        ann.Resources.TakeDamage(4);
        Assert.Equal(6, ann.Resources.HitPoints.Current);
        Assert.False(ann.Resources.TrySpendSpellPoints(9));
        Assert.True(ann.Resources.TrySpendSpellPoints(2));
        Assert.Equal(3, ann.Resources.SpellPoints.Current);

        // A pool never carries past its own bounds, whatever it is asked to do.
        ann.Resources.TakeDamage(100);
        Assert.Equal(0, ann.Resources.HitPoints.Current);
        ann.Resources.RestoreHitPoints(100);
        Assert.Equal(10, ann.Resources.HitPoints.Current);
        ann.Resources.SetMaximumHitPoints(20);
        Assert.Equal(new ResourcePool(10, 20), ann.Resources.HitPoints);
    }

    [Fact]
    public void Party_resources_are_state_the_later_systems_debit_and_credit()
    {
        using PartyEntity party = Build(new PartyEntityFactory(), Member("Ann", Fighter));

        party.Purse.Credit(50);
        Assert.True(party.Purse.TryDebit(120));
        Assert.Equal(30, party.Purse.Coins);
        Assert.False(party.Purse.TryDebit(31));
        Assert.Equal(30, party.Purse.Coins);

        Assert.True(party.Food.TrySpend(new Provisions(4, ProvisionUnit.Portions)));
        Assert.Equal(6, party.Food.Portions);
        Assert.False(party.Food.TrySpend(new Provisions(7, ProvisionUnit.Portions)));
        Assert.Equal(6, party.Food.Portions);

        party.Reputation.ChangeReputation(-3);
        party.Reputation.ChangeFame(4);
        Assert.Equal(2, party.Reputation.Reputation);
        Assert.Equal(6, party.Reputation.Fame);
    }

    [Fact]
    public void Conditions_and_effects_are_held_by_the_member_and_the_party()
    {
        using PartyEntity party = Build(new PartyEntityFactory(), Member("Ann", Fighter));
        PartyMember ann = party.Members[0];

        ann.Conditions.Apply(new ActiveCondition(Weakened, 2));
        ann.Conditions.Apply(new ActiveCondition(Weakened, 3));
        Assert.Equal(3, ann.Conditions.SeverityOf(Weakened));
        Assert.True(ann.Conditions.Has(Weakened));
        Assert.True(ann.Conditions.Clear(Weakened));
        Assert.Empty(ann.Conditions.Active);

        party.Effects.Apply(new PartyEffect(Warded, 1));
        party.Effects.Apply(new PartyEffect(Swift, 2));
        party.Effects.Apply(new PartyEffect(Warded, 4));
        Assert.Equal(2, party.Effects.Count);
        Assert.Equal(4, party.Effects.MagnitudeOf(Warded));
        Assert.True(party.Effects.Remove(Swift));
        Assert.False(party.Effects.Has(Swift));
    }

    [Fact]
    public void Hired_followers_obey_the_limit_and_story_followers_do_not()
    {
        using PartyEntity party = Build(new PartyEntityFactory(hiredFollowerLimit: 1), Member("Ann", Fighter));

        Assert.Null(party.Followers.Add(new PartyFollower(party.Identity.MintMemberId(), Porter, "Tam", FollowerKind.Hired)));
        PartyRefusal? refused = party.Followers.Add(new PartyFollower(party.Identity.MintMemberId(), Porter, "Ula", FollowerKind.Hired));
        Assert.Equal("hired-limit-reached", refused!.Code);
        Assert.Null(party.Followers.Add(new PartyFollower(party.Identity.MintMemberId(), Porter, "Vik", FollowerKind.Story)));

        Assert.Equal(2, party.Followers.Count);
        Assert.Equal(1, party.Followers.HiredCount);
    }

    [Fact]
    public void A_captured_party_keeps_its_durable_identities_across_a_restore()
    {
        SkillGatedEquipment gate = new(new Dictionary<string, (string Class, string Skill)>
        {
            [Blade.Value] = (Fighter.Value, Blades.Value),
        });
        using PartyEntity original = Build(
            new PartyEntityFactory(gate),
            Member("Ann", Fighter, new SkillEntry(Blades, 1, SkillTier.None, 1)),
            Member("Bo", Adept));
        ItemInstance blade = original.AcquireItem(Blade).Item!;
        blade.Identify();
        original.Equip(original.Members[0].Id, Hand, blade.Id);
        original.AcquireItem(Torch);
        original.Followers.Add(new PartyFollower(original.Identity.MintMemberId(), Porter, "Tam", FollowerKind.Story));
        original.Effects.Apply(new PartyEffect(Warded, 3));
        original.Purse.Credit(25);

        PartySave save = original.Capture();
        using PartyEntity first = new PartyEntityFactory(gate).Restore(save);
        using PartyEntity second = new PartyEntityFactory(gate).Restore(save);

        // Durable identity is what a save carries, so two restores of one save are the same party by identity.
        Assert.Equal(original.Members.Select(member => member.Id), first.Members.Select(member => member.Id));
        Assert.Equal(first.Members.Select(member => member.Id), second.Members.Select(member => member.Id));
        Assert.Equal(first.Items.Select(item => item.Id), second.Items.Select(item => item.Id));

        // Runtime identity does not survive and cannot: a restore builds its own store, and each store numbers
        // its own entities from one — so a restored party's runtime ids are fresh numbers two different stores
        // happen to agree on. Nothing durable may name one, which is why only the durable identities above are
        // compared across a save.
        Assert.NotSame(original.Store, first.Store);
        Assert.Equal(first.RuntimeId, second.RuntimeId);
        Assert.Equal(new EntityId(1), first.RuntimeId);
        Assert.Equal(new EntityId(2), first.Members[0].RuntimeId);

        // What the save recorded comes back: the artifact is still the artifact, identified, on the member it
        // was handed to, and the party's own state is what it was.
        ItemInstance restored = first.Items.Single(item => item.Id == blade.Id);
        Assert.True(restored.State.IsIdentified);
        Assert.Equal(ItemCustody.EquippedBy(first.Members[0].Id, Hand), restored.Custody);
        Assert.Equal(Torch, first.Inventory.Items.Single().Definition);
        Assert.Equal(125, first.Purse.Coins);
        Assert.Equal(FollowerKind.Story, first.Followers.Followers.Single().Kind);
        Assert.Equal(3, first.Effects.MagnitudeOf(Warded));

        // The identity cursor comes back too, so nothing a restore mints can collide with what it loaded.
        ItemInstance minted = first.CreateItem(Arrow);
        Assert.True(minted.Id.Value >= save.NextItemValue);
        Assert.DoesNotContain(second.Items, item => item.Id == minted.Id);
    }

    [Fact]
    public void A_capture_is_a_snapshot_and_not_a_view_of_live_state()
    {
        using PartyEntity party = Build(new PartyEntityFactory(), Member("Ann", Fighter, new SkillEntry(Blades, 1, SkillTier.None, 1)));
        ItemInstance blade = party.AcquireItem(Blade).Item!;
        blade.Enchant(new ItemEnchantment(Keen, 2));
        party.Equip(party.Members[0].Id, Hand, blade.Id);
        ItemSave recorded = party.Capture().Items.Single();

        blade.TakeDamage(3);
        blade.Disenchant(Keen);
        party.AcquireItem(Torch);
        party.Members[0].Resources.TakeDamage(5);

        Assert.Equal(0, recorded.State.Damage);
        Assert.Equal(Keen, recorded.State.Enchantments.Single().Enchantment);
        Assert.Single(party.Capture().Items, item => item.Id == blade.Id);
        Assert.Equal(2, party.Capture().Items.Count);
    }

    [Fact]
    public void Restoring_does_not_re_judge_what_the_save_recorded()
    {
        using PartyEntity original = Build(new PartyEntityFactory(), Member("Ann", Fighter, new SkillEntry(Blades, 1, SkillTier.None, 1)));
        ItemInstance blade = original.AcquireItem(Blade).Item!;
        original.Equip(original.Members[0].Id, Hand, blade.Id);
        original.AcquireItem(Arrow, 3);
        original.AcquireItem(Torch);

        // The rules changed since the save was written: nothing may be equipped any more and the pack takes
        // nothing at all. The save already recorded what the party held, and losing an artifact to a tuning
        // change is worse than admitting a rule that is now stale.
        PartyEntityFactory hostile = new(new RefusingEquipment(), new LooseItemLimit(0));
        using PartyEntity restored = hostile.Restore(original.Capture());

        Assert.True(restored.Members[0].Equipment.Has(Hand));
        Assert.Equal(3, restored.Inventory.TotalOf(Arrow));
        Assert.Equal(Torch, restored.Inventory.Items.Single(item => item.Definition == Torch).Definition);
        Assert.Equal(5, restored.Items.Sum(item => item.StackCount));
    }

    [Fact]
    public void A_save_that_cannot_be_rebuilt_is_refused_with_every_problem_named()
    {
        using PartyEntity original = Build(new PartyEntityFactory(), Member("Ann", Fighter), Member("Bo", Adept));
        ItemInstance blade = original.AcquireItem(Blade).Item!;
        ItemInstance torch = original.AcquireItem(Torch).Item!;
        PartySave good = original.Capture();

        PartySave broken = new(
            nextMemberValue: good.NextMemberValue,
            nextItemValue: 1,
            members: [good.Members[0], good.Members[0]],
            items:
            [
                new ItemSave(blade.Id, Blade, 1, ItemState.Unidentified, ItemCustody.InSharedInventory),
                new ItemSave(torch.Id, Torch, 1, ItemState.Unidentified, ItemCustody.EquippedBy(new PartyMemberId(99), Hand)),
            ]);

        ArgumentException refused = Assert.Throws<ArgumentException>(() => new PartyEntityFactory().Restore(broken));

        Assert.Contains("recorded more than once", refused.Message, StringComparison.Ordinal);
        Assert.Contains("not below the item cursor", refused.Message, StringComparison.Ordinal);
        Assert.Contains("whom the save does not record", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Releasing_an_item_takes_it_out_of_the_party_with_its_identity_intact()
    {
        using PartyEntity party = Build(new PartyEntityFactory(), Member("Ann", Fighter));
        ItemInstance blade = party.AcquireItem(Blade).Item!;
        party.Equip(party.Members[0].Id, Hand, blade.Id);

        ItemInstance released = party.ReleaseItem(blade.Id)!;

        Assert.Same(blade, released);
        Assert.True(released.Custody.IsDetached);
        Assert.Empty(party.Items);
        Assert.Empty(party.Inventory.Items);
        Assert.False(party.Members[0].Equipment.Has(Hand));
        Assert.Null(party.ReleaseItem(blade.Id));

        // An instance nobody holds cannot be taken twice: it has to be offered again by whoever holds it.
        ItemInstance offered = party.CreateItem(Blade);
        Assert.True(party.AcquireItem(offered).Admitted);
        ItemAcquisition twice = party.AcquireItem(offered);
        Assert.False(twice.Admitted);
        Assert.Equal("item-already-held", twice.Refusal!.Code);
    }

    private static MemberCreation Member(string name, ClassId characterClass, params SkillEntry[] skills) =>
        new(Seed(name, characterClass, skills));

    /// <summary>Builds a one-member party whose character was created holding the given skill points.</summary>
    /// <remarks>
    /// The points are creation's own statement rather than a grant: only the progression owner may add them,
    /// and a test that wants a pool to spend starts a character who has one.
    /// </remarks>
    private static PartyEntity HoldingSkillPoints(string name, ClassId characterClass, int skillPoints, params SkillEntry[] skills) =>
        Build(new PartyEntityFactory(), new MemberCreation(Seed(name, characterClass, skills, skillPoints)));

    private static PartyMemberSeed Seed(string name, ClassId characterClass, params SkillEntry[] skills) =>
        Seed(name, characterClass, skills, skillPoints: 0);

    private static PartyMemberSeed Seed(string name, ClassId characterClass, SkillEntry[] skills, int skillPoints) => new(
        name,
        TestRace,
        characterClass,
        [new AttributeScore(Vigour, 12), new AttributeScore(Wit, 9)],
        skills,
        [Spark],
        experience: 0,
        level: 1,
        skillPoints: skillPoints,
        classRank: 1,
        conditions: [],
        hitPoints: ResourcePool.Full(10),
        spellPoints: ResourcePool.Full(5));

    private static PartyEntity Build(PartyEntityFactory factory, params MemberCreation[] members) =>
        factory.Create(new PartyCreation(members, coins: 100, foodPortions: 10, reputation: 5, fame: 2));

    /// <summary>
    /// The least a progression owner can be composed over: no curve, no division, no growth, and no standing.
    /// </summary>
    /// <remarks>
    /// This test is about the skill raise, which reads none of the rule's answers, so the rule states the
    /// smallest thing that is still a rule rather than an answer invented for the test to assert on.
    /// </remarks>
    private sealed class NoProgressionRule : IProgressionRule
    {
        public long ExperienceForLevel(int level) => 0;

        public IReadOnlyList<ProgressionShare> Divide(ProgressionDivision division) => [];

        public ProgressionGrowth Growth(ProgressionGrowthRequest request) => ProgressionGrowth.None;

        public ProgressionStanding Standing(ProgressionStandingRequest request) => ProgressionStanding.None;
    }

    /// <summary>An equipment rule of the kind a ruleset writes: a class and a skill decide, and the kit knows neither.</summary>
    private sealed class SkillGatedEquipment : IEquipmentUseRule
    {
        private readonly Dictionary<string, (string Class, string Skill)> _requirements;

        internal SkillGatedEquipment(Dictionary<string, (string Class, string Skill)> requirements) =>
            _requirements = requirements;

        internal List<string> SeenClasses { get; } = [];

        public PartyRefusal? Judge(PartyMember member, EquipmentSlot slot, ItemInstance item)
        {
            SeenClasses.Add(member.Profile.Class.Value);
            if (!_requirements.TryGetValue(item.Definition.Value, out (string Class, string Skill) requirement))
            {
                return new PartyRefusal("nothing-says-how", $"Nothing says how '{item.Definition}' is worn.");
            }

            if (member.Profile.Class.Value != requirement.Class)
            {
                return new PartyRefusal(
                    "class-not-allowed",
                    $"{member.Profile.Name} is a {member.Profile.Class} and may not use '{item.Definition}'.");
            }

            if (!member.Skills.Knows(new SkillId(requirement.Skill)))
            {
                return new PartyRefusal(
                    "skill-missing",
                    $"{member.Profile.Name} has not learned '{requirement.Skill}'.");
            }

            return null;
        }
    }

    /// <summary>A rule that refuses every equip, which is what a changed ruleset looks like to an old save.</summary>
    private sealed class RefusingEquipment : IEquipmentUseRule
    {
        public PartyRefusal? Judge(PartyMember member, EquipmentSlot slot, ItemInstance item) =>
            new("never", "This world equips nothing.");
    }

    /// <summary>A pack that takes at most this many loose items.</summary>
    private sealed class LooseItemLimit : IInventoryCapacityRule
    {
        private readonly int _maximum;

        internal LooseItemLimit(int maximum) => _maximum = maximum;

        public PartyRefusal? Judge(IReadOnlyList<ItemInstance> held, ItemDefinitionId definition, int count) =>
            held.Count + count > _maximum
                ? new PartyRefusal("pack-full", $"The pack holds {held.Count} and takes at most {_maximum}.")
                : null;
    }

    /// <summary>How far copies of a definition bundle, stated by the test rather than by the kit.</summary>
    private sealed class StackLimits : IItemStackingRule
    {
        private readonly Dictionary<string, int> _limits;

        internal StackLimits(Dictionary<string, int> limits) => _limits = limits;

        public int MaximumStack(ItemDefinitionId definition) => _limits.GetValueOrDefault(definition.Value, 1);
    }
}
