using Rusty.Engine.Entities;

namespace PartyRpg.Kit.Party;

/// <summary>
/// Builds a party explicitly, either from creation or from a save the product wrote.
/// </summary>
/// <remarks>
/// <para>
/// Nothing else attaches a party component. The factory composes each one by name, so what a party is made
/// of is one list in one place, and a party entity that was wrapped rather than created reports its missing
/// components instead of growing them. The rules a party obeys arrive here too — equipment gating from the
/// ruleset, capacity and stacking from its tuning, the hired limit with them — because they are policy
/// rather than state, and a save does not carry them back.
/// </para>
/// <para>
/// <b>Creation and restoration differ in exactly one respect.</b> Creation validates: a starting blueprint
/// the rules would refuse fails while the party is being built, because creation is a real product flow and
/// a party that quietly wears what it may not use is a defect nobody sees. Restoration does not re-judge:
/// the save already recorded what the party held, and re-running a changed rule could refuse a party the
/// product itself wrote — losing an artifact to a tuning change is worse than admitting a stale rule.
/// Restoration nevertheless refuses a save that cannot be rebuilt: duplicate or reused identities, a
/// cursor behind an identity the save holds, an item worn by a member the save does not record, or two
/// items in one slot.
/// </para>
/// </remarks>
public sealed class PartyEntityFactory
{
    private readonly IEquipmentUseRule? _equipmentUse;
    private readonly IInventoryCapacityRule? _inventoryCapacity;
    private readonly IItemStackingRule? _stacking;
    private readonly int _hiredFollowerLimit;

    /// <summary>Creates a factory over the rules the party it builds obeys.</summary>
    /// <param name="equipmentUse">
    /// The rule that decides whether a member may wear or wield an item. Without one nothing gates
    /// equipment, which is the honest state of a product whose ruleset has not answered yet.
    /// </param>
    /// <param name="inventoryCapacity">
    /// The rule that decides whether the shared pack takes more items. Without one the pack has no limit,
    /// which is what a game with no encumbrance has.
    /// </param>
    /// <param name="stacking">
    /// The rule that says how far copies of one definition bundle. Without one nothing stacks.
    /// </param>
    /// <param name="hiredFollowerLimit">How many hired followers the party may have at once, from tuning.</param>
    /// <exception cref="ArgumentOutOfRangeException">The hired limit is negative.</exception>
    public PartyEntityFactory(
        IEquipmentUseRule? equipmentUse = null,
        IInventoryCapacityRule? inventoryCapacity = null,
        IItemStackingRule? stacking = null,
        int hiredFollowerLimit = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(hiredFollowerLimit);
        _equipmentUse = equipmentUse;
        _inventoryCapacity = inventoryCapacity;
        _stacking = stacking;
        _hiredFollowerLimit = hiredFollowerLimit;
    }

    /// <summary>Creates a party from what a creation flow produced.</summary>
    /// <param name="creation">The members and starting values creation decided.</param>
    /// <returns>The new party, owning the store its entities live in.</returns>
    /// <exception cref="ArgumentNullException">The creation is null.</exception>
    /// <exception cref="ArgumentException">A member's starting equipment is refused by the rules the party obeys.</exception>
    public PartyEntity Create(PartyCreation creation)
    {
        ArgumentNullException.ThrowIfNull(creation);
        EntityStore store = new();
        Actor entity = NewEntity(store, PartyEntity.EntityKind);

        // The identity source comes first because it mints the identities of the members created below.
        PartyIdentitySource identity = new();
        entity.Add(identity);

        List<PartyMember> members = [];
        foreach (MemberCreation member in creation.Members) members.Add(AttachMember(store, identity.MintMemberId(), member.Seed));

        entity.Add(new PartyRoster(members));
        entity.Add(new PartyInventory(_inventoryCapacity, _stacking));
        entity.Add(new PartyPurse(creation.Coins));
        entity.Add(new PartyFood(creation.FoodPortions, creation.FoodUnit));
        entity.Add(new PartyReputation(creation.Reputation, creation.Fame));
        entity.Add(new PartyFollowers(_hiredFollowerLimit));
        entity.Add(new PartyEffects());

        PartyEntity party = new(store, entity, ownsStore: true, _equipmentUse);
        for (int index = 0; index < members.Count; index++)
        {
            EquipStarting(party, members[index], creation.Members[index].StartingEquipment);
        }

        return party;
    }

    /// <summary>Rebuilds a party from a save the product wrote.</summary>
    /// <param name="save">The recorded party.</param>
    /// <returns>The restored party, owning the store its entities live in.</returns>
    /// <exception cref="ArgumentNullException">The save is null.</exception>
    /// <exception cref="ArgumentException">The save cannot be rebuilt; the message names every problem found.</exception>
    public PartyEntity Restore(PartySave save)
    {
        ArgumentNullException.ThrowIfNull(save);
        IReadOnlyList<string> problems = Problems(save);
        if (problems.Count > 0)
        {
            throw new ArgumentException(
                $"The save cannot be restored: {string.Join("; ", problems)}.",
                nameof(save));
        }

        EntityStore store = new();
        Actor entity = NewEntity(store, PartyEntity.EntityKind);
        entity.Add(new PartyIdentitySource(save.NextMemberValue, save.NextItemValue));

        List<PartyMember> members = [];
        foreach (PartyMemberSave member in save.Members) members.Add(AttachMember(store, member.Id, member.Seed));

        PartyInventory inventory = new(_inventoryCapacity, _stacking);
        PartyFollowers followers = new(_hiredFollowerLimit);
        PartyEffects effects = new();
        entity.Add(new PartyRoster(members));
        entity.Add(inventory);
        entity.Add(new PartyPurse(save.Coins));
        entity.Add(new PartyFood(save.FoodPortions, save.FoodUnit));
        entity.Add(new PartyReputation(save.Reputation, save.Fame));
        entity.Add(followers);
        entity.Add(effects);

        PartyEntity party = new(store, entity, ownsStore: true, _equipmentUse);

        // Each instance goes back where the save says it was held. No rule is consulted: the save recorded
        // what the party held, and a rule that has since changed must not lose an item the party owned.
        foreach (ItemSave item in save.Items)
        {
            ItemInstance instance = new(item.Id, item.Definition, item.StackCount, item.State);
            if (item.Custody.IsEquipped)
            {
                party.Member(item.Custody.Member).Equipment.Attach(item.Custody.Slot, instance);
                instance.Place(item.Custody);
                continue;
            }

            // An instance that is neither worn nor in the pack was refused by Problems, so what reaches
            // here was held in the shared pack and goes back there — and nowhere else, because a record
            // held by nobody would otherwise become loot the party never picked up.
            inventory.Append(instance);
        }

        foreach (PartyFollower follower in save.Followers) followers.Add(follower);
        foreach (PartyEffect effect in save.Effects) effects.Apply(effect);
        return party;
    }

    /// <summary>
    /// Every problem that stops this save being rebuilt, named in the save's own terms, or an empty list
    /// when it can be restored.
    /// </summary>
    /// <remarks>
    /// Naming all of them at once is deliberate: a defective save is fixed in one pass instead of one
    /// problem per attempt, and a caller that loads a session can report the whole list to the player. The
    /// problems are contradictions between what the save records and what a party could be — an identity
    /// recorded twice, a cursor behind an identity the save holds, an item worn by nobody who exists, an
    /// item held by nobody at all — rather than anything this factory's rules would refuse, because a
    /// restore must not re-judge a party the product itself saved.
    /// </remarks>
    /// <param name="save">The recorded party.</param>
    /// <exception cref="ArgumentNullException">The save is null.</exception>
    public IReadOnlyList<string> Problems(PartySave save)
    {
        ArgumentNullException.ThrowIfNull(save);
        List<string> problems = [];
        if (save.NextMemberValue == 0) problems.Add("the member identity cursor is zero, so a member could be minted with no identity");
        if (save.NextItemValue == 0) problems.Add("the item identity cursor is zero, so an item could be minted with no identity");

        HashSet<PartyMemberId> members = [];
        for (int position = 0; position < save.Members.Count; position++)
        {
            PartyMemberSave member = save.Members[position];
            if (member.Id.Value == 0)
            {
                problems.Add($"member {position + 1} is recorded without an identity, so nothing in the save can say who it is");
            }
            else if (!members.Add(member.Id))
            {
                problems.Add($"member {member.Id} is recorded more than once");
            }
            else if (member.Id.Value >= save.NextMemberValue)
            {
                problems.Add($"member {member.Id} is not below the member cursor {save.NextMemberValue}, so a restored party could mint that identity again");
            }
        }

        HashSet<ItemInstanceId> items = [];
        HashSet<(PartyMemberId Member, string Slot)> occupied = [];
        foreach (ItemSave item in save.Items)
        {
            if (item.Id.Value == 0)
            {
                problems.Add("an item is recorded without an identity, so nothing in the save can say which instance it is");
                continue;
            }

            if (!items.Add(item.Id))
            {
                problems.Add($"item {item.Id} is recorded more than once");
            }
            else if (item.Id.Value >= save.NextItemValue)
            {
                problems.Add($"item {item.Id} is not below the item cursor {save.NextItemValue}, so a restored party could mint that identity again");
            }

            // Where an instance was held is one of three places. A record held by nobody is refused here
            // rather than appended to the pack: a loose world item loaded through this path would become
            // loot the party never picked up.
            if (item.Custody.IsDetached)
            {
                problems.Add($"item {item.Id} is recorded as held by nobody, so restoring it would hand the party an item it never took");
                continue;
            }

            if (!item.Custody.IsEquipped) continue;
            if (!members.Contains(item.Custody.Member))
            {
                problems.Add($"item {item.Id} is recorded as worn by member {item.Custody.Member}, whom the save does not record");
            }
            else if (!occupied.Add((item.Custody.Member, item.Custody.Slot.Value)))
            {
                problems.Add($"member {item.Custody.Member} has two items recorded in slot '{item.Custody.Slot}'");
            }
        }

        HashSet<PartyMemberId> followers = [];
        foreach (PartyFollower follower in save.Followers)
        {
            if (follower.Id.Value == 0)
            {
                problems.Add($"follower '{follower.Name}' is recorded without an identity");
            }
            else if (!followers.Add(follower.Id))
            {
                problems.Add($"follower {follower.Id} is recorded more than once");
            }
            else if (follower.Id.Value >= save.NextMemberValue)
            {
                problems.Add($"follower {follower.Id} is not below the member cursor {save.NextMemberValue}, so a restored party could mint that identity again");
            }
            else if (members.Contains(follower.Id))
            {
                problems.Add($"follower {follower.Id} shares an identity with a member");
            }
        }

        if (save.Coins < 0) problems.Add($"the purse is recorded holding {save.Coins}");
        if (save.FoodPortions < 0) problems.Add($"the larder is recorded holding {save.FoodPortions}");

        return problems;
    }

    /// <summary>Creates an entity of a kind this kit owns, in the store the party lives in.</summary>
    private static Actor NewEntity(EntityStore store, string kind) =>
        new(store, store.Create(new EntityTypeId(kind), EntityLifecycle.Active));

    /// <summary>Attaches every character component a member is made of, and returns the facade over them.</summary>
    private static PartyMember AttachMember(EntityStore store, PartyMemberId id, PartyMemberSeed seed)
    {
        Actor entity = NewEntity(store, PartyMember.EntityKind);
        entity.Add(new CharacterProfile(id, seed.Name, seed.Race, seed.Class, seed.Portrait));
        entity.Add(new CharacterAttributes(seed.Attributes));
        entity.Add(new CharacterSkills(seed.Skills));
        entity.Add(new CharacterSpells(seed.Spells));
        entity.Add(new CharacterProgression(seed.Experience, seed.Level, seed.SkillPoints, seed.ClassRank));
        entity.Add(new CharacterConditions(seed.Conditions));
        entity.Add(new CharacterResources(seed.HitPoints, seed.SpellPoints));
        entity.Add(new CharacterEquipment());
        return new PartyMember(entity);
    }

    /// <summary>Puts creation's starting equipment on a member through the same gated path a later equip takes.</summary>
    /// <exception cref="ArgumentException">The party's rules refuse the equipment creation asked for.</exception>
    private static void EquipStarting(PartyEntity party, PartyMember member, IReadOnlyList<StartingEquipment> equipment)
    {
        foreach (StartingEquipment start in equipment)
        {
            ItemAcquisition taken = party.AcquireItem(start.Definition);
            if (taken.Refusal is { } refusal)
            {
                throw new ArgumentException(
                    $"The created party cannot carry '{start.Definition}' for {member.Profile.Name}: {refusal}",
                    nameof(equipment));
            }

            EquipmentChange change = party.Equip(member.Id, start.Slot, taken.Item!.Id);
            if (change.Refusal is { } equipped)
            {
                throw new ArgumentException(
                    $"The created party cannot equip {member.Profile.Name} with '{start.Definition}' in '{start.Slot}': {equipped}",
                    nameof(equipment));
            }
        }
    }
}
