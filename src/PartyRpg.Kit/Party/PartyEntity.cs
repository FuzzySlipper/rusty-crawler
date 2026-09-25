using Rusty.Engine.Entities;

namespace PartyRpg.Kit.Party;

/// <summary>
/// The party: one engine entity every party-scoped component attaches to, with named properties over them.
/// </summary>
/// <remarks>
/// <para>
/// <b>One entity, not four characters.</b> The roster and its members, the one shared inventory, equipment
/// by member, the purse, the larder, reputation and fame, followers, party-wide effects, and the identity
/// cursors are all components of this entity, and session mechanisms address the party rather than reaching
/// into members. The kit composes the engine's own <see cref="Actor"/> rather than inventing a second
/// entity graph: <see cref="Actor"/> <i>is</i> the entity, every named property below reads the component
/// the entity actually carries, and a ruleset that attaches a component of its own to this actor extends the
/// party rather than a copy of it.
/// </para>
/// <para>
/// <b>Wrapping never creates a component.</b> Every read below goes through the actor, so a party whose
/// entity lacks a component reports that by failing, not by quietly attaching an empty one — only
/// <see cref="PartyEntityFactory"/> attaches anything, and it does so explicitly, once, from creation or
/// from a save. The store the party lives in is its own, distinct from the store a visit populates with
/// world entities: the world destroys what lives in a place when the party leaves, and a party cannot be
/// destroyed by walking out of a room. <see cref="PartyEntityFactory"/> owns the store's lifetime;
/// <see cref="Wrap"/> borrows one.
/// </para>
/// <para>
/// <b>Three identities, deliberately different.</b> <see cref="RuntimeId"/> is runtime identity: the
/// engine's entity id inside this party's store, never reused by that store, never written down, and rebuilt
/// on every load. Content identity is what the party's references name — item definitions, races, classes,
/// skills, spells, conditions, effects — and is content's to define and the ruleset's to resolve. Durable
/// save identity is <see cref="PartyMemberId"/> and <see cref="ItemInstanceId"/>, minted once by
/// <see cref="Identity"/>, written in every reference inside the party, and the only identities a save
/// carries: <see cref="Capture"/> writes them, and the world outside a save never sees them change.
/// </para>
/// <para>
/// <b>The party's pose is not here.</b> Where the party stands belongs to <see cref="PartyPoseOwner"/>,
/// which the world already owns and steps; this entity deliberately holds no second pose, because two
/// positions for one party is exactly the drift the one-owner rule exists to prevent.
/// </para>
/// </remarks>
public sealed class PartyEntity : IDisposable
{
    /// <summary>The kind of engine entity a party is, which is fixed when the entity is created.</summary>
    public const string EntityKind = "party";

    private readonly EntityStore _store;
    private readonly Actor _party;
    private readonly bool _ownsStore;
    private readonly IEquipmentUseRule? _equipmentUse;
    private bool _disposed;

    internal PartyEntity(EntityStore store, Actor party, bool ownsStore, IEquipmentUseRule? equipmentUse)
    {
        _store = store;
        _party = party;
        _ownsStore = ownsStore;
        _equipmentUse = equipmentUse;
    }

    /// <summary>
    /// Wraps a party entity that already exists in a store the caller owns.
    /// </summary>
    /// <remarks>
    /// Wrapping composes nothing: the facade reads whatever components the entity carries, and a component
    /// it does not carry fails on the read instead of being created. Disposing a wrapped party disposes
    /// nothing, because the store belongs to whoever created the entity.
    /// </remarks>
    /// <param name="party">The engine actor for the party entity.</param>
    /// <param name="equipmentUse">The rule that gates equipping, when the caller has one to apply.</param>
    /// <exception cref="ArgumentNullException">The actor is null.</exception>
    public static PartyEntity Wrap(Actor party, IEquipmentUseRule? equipmentUse = null)
    {
        ArgumentNullException.ThrowIfNull(party);
        return new PartyEntity(party.Store, party, ownsStore: false, equipmentUse);
    }

    /// <summary>The engine actor for the party, through which components are attached and read.</summary>
    public Actor Actor => _party;

    /// <summary>
    /// The engine store the party's entities live in, where a ruleset attaches the components it owns.
    /// </summary>
    public EntityStore Store => _store;

    /// <summary>
    /// The party's runtime identity: the entity id inside the store that holds it. It is per-visit, never
    /// reused by that store, and never part of a save.
    /// </summary>
    public EntityId RuntimeId => _party.Entity;

    /// <summary>Whether the party's entity is still alive in the store.</summary>
    public bool IsAlive => _party.IsAlive;

    /// <summary>The members, in the order the party stands in.</summary>
    public IReadOnlyList<PartyMember> Members => Roster.Members;

    /// <summary>The party's roster: its members and the order they stand in.</summary>
    public PartyRoster Roster => _party.Get<PartyRoster>();

    /// <summary>The party's one shared pack, which is every loose item the party holds.</summary>
    public PartyInventory Inventory => _party.Get<PartyInventory>();

    /// <summary>The party's one purse.</summary>
    public PartyPurse Purse => _party.Get<PartyPurse>();

    /// <summary>The party's one food supply.</summary>
    public PartyFood Food => _party.Get<PartyFood>();

    /// <summary>The party's reputation and fame.</summary>
    public PartyReputation Reputation => _party.Get<PartyReputation>();

    /// <summary>The followers travelling with the party.</summary>
    public PartyFollowers Followers => _party.Get<PartyFollowers>();

    /// <summary>The effects acting on the whole party.</summary>
    public PartyEffects Effects => _party.Get<PartyEffects>();

    /// <summary>The origin of the party's durable identities, which a save records as a cursor.</summary>
    public PartyIdentitySource Identity => _party.Get<PartyIdentitySource>();

    /// <summary>Every item instance the party holds: the loose ones, then each member's equipped figure.</summary>
    public IReadOnlyList<ItemInstance> Items
    {
        get
        {
            List<ItemInstance> items = [.. Inventory.Items];
            foreach (PartyMember member in Roster.Members)
            {
                foreach (EquippedItem equipped in member.Equipment.Items) items.Add(equipped.Item);
            }

            return items;
        }
    }

    /// <summary>Reads one member by durable identity.</summary>
    /// <param name="id">The member's durable identity.</param>
    /// <exception cref="ArgumentException">The party has no such member.</exception>
    public PartyMember Member(PartyMemberId id) => Roster.Member(id);

    /// <summary>Reads one member by durable identity.</summary>
    /// <param name="id">The member's durable identity.</param>
    /// <param name="member">The member, when the party has one with that identity.</param>
    /// <returns>Whether the party has that member.</returns>
    public bool TryMember(PartyMemberId id, out PartyMember? member) => Roster.TryMember(id, out member);

    /// <summary>Finds one item the party holds, wherever it lies, or null when it holds none with that identity.</summary>
    /// <param name="id">The instance's durable identity.</param>
    public ItemInstance? FindItem(ItemInstanceId id)
    {
        if (Inventory.Find(id) is { } loose) return loose;
        foreach (PartyMember member in Roster.Members)
        {
            foreach (EquippedItem equipped in member.Equipment.Items)
            {
                if (equipped.Item.Id == id) return equipped.Item;
            }
        }

        return null;
    }

    /// <summary>
    /// Creates an item instance with a durable identity, held by nobody until the party takes it.
    /// </summary>
    /// <remarks>
    /// A loot table, a container, or a shop that generates its own contents calls this so that every
    /// instance in the session carries an identity from the one source that mints them; the instance lies
    /// where it was generated until <see cref="AcquireItem(ItemInstance)"/> takes it.
    /// </remarks>
    /// <param name="definition">The content definition the instance is a copy of.</param>
    /// <param name="stackCount">How many of the definition the instance carries, which must be at least one.</param>
    /// <exception cref="ArgumentOutOfRangeException">The stack count is below one.</exception>
    public ItemInstance CreateItem(ItemDefinitionId definition, int stackCount = 1)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new ItemInstance(Identity.MintItemId(), definition, stackCount);
    }

    /// <summary>
    /// Takes items the party is offered into its one shared pack.
    /// </summary>
    /// <remarks>
    /// This is the only way an item enters the party, and it enters the pack and nowhere else: a pickup
    /// cannot land on a member, because a member owns only what it has equipped. What the pack does with it
    /// is the capacity rule's and the stacking rule's business — an offer merges into compatible stacks up to
    /// the rule's maximum and starts whole stacks beyond it — and a refusal leaves the party exactly as it
    /// was, with the instance still held by whoever generated it.
    /// </remarks>
    /// <param name="item">The instance the party is offered, which no party may already hold.</param>
    /// <returns>Where the items landed and how many were taken, or why nothing was taken.</returns>
    /// <exception cref="ArgumentNullException">The instance is null.</exception>
    public ItemAcquisition AcquireItem(ItemInstance item)
    {
        ArgumentNullException.ThrowIfNull(item);
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!item.Custody.IsDetached)
        {
            return ItemAcquisition.Refused(new PartyRefusal(
                "item-already-held",
                $"Item {item.Id} is already held ({item.Custody}), so it was not taken a second time."));
        }

        if (Inventory.Judge(item.Definition, item.StackCount) is { } refusal) return ItemAcquisition.Refused(refusal);

        int count = item.StackCount;
        int maximum = Inventory.MaximumStack(item.Definition);
        ItemInstance? landed = null;
        int left = count;

        // Only what lies in the pack merges: an equipped stack is a member's figure and keeps its own count.
        if (maximum > 1)
        {
            foreach (ItemInstance stack in Inventory.StacksMatching(item.Definition, item.State))
            {
                int room = maximum - stack.StackCount;
                if (room <= 0) continue;
                int taken = Math.Min(room, left);
                stack.AddToStack(taken);
                left -= taken;
                landed ??= stack;
                if (left == 0) return ItemAcquisition.Taken(landed, count);
            }
        }

        // What is left becomes whole stacks of its own, each within the rule's maximum, and the offered
        // instance carries the last of them. An offer that merged away entirely leaves the identity minted for
        // it unused, which is why the cursor counts identities handed out rather than items held: an identity
        // is never reused, so a gap in them costs nothing.
        int pieces = ((left - 1) / maximum) + 1;
        for (int piece = 1; piece < pieces; piece++)
        {
            Inventory.Append(new ItemInstance(Identity.MintItemId(), item.Definition, maximum, item.State));
        }

        int last = left - (maximum * (pieces - 1));
        if (last < count) item.RemoveFromStack(count - last);
        Inventory.Append(item);
        return ItemAcquisition.Taken(landed ?? item, count);
    }

    /// <summary>Takes new items of one definition into the party's shared pack.</summary>
    /// <param name="definition">The content definition the items are copies of.</param>
    /// <param name="stackCount">How many items to take, which must be at least one.</param>
    /// <returns>Where the items landed and how many were taken, or why nothing was taken.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The stack count is below one.</exception>
    public ItemAcquisition AcquireItem(ItemDefinitionId definition, int stackCount = 1) =>
        AcquireItem(CreateItem(definition, stackCount));

    /// <summary>
    /// Releases an item out of the party entirely: onto the ground, into a container, or into a shop's
    /// stock.
    /// </summary>
    /// <remarks>
    /// The instance keeps its durable identity and its state and becomes detached, which is what the
    /// container, loot, and shop owners receive; taking it back later is an ordinary acquisition.
    /// </remarks>
    /// <param name="id">The instance's durable identity.</param>
    /// <returns>The instance the party released, or null when it held none with that identity.</returns>
    public ItemInstance? ReleaseItem(ItemInstanceId id)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ItemInstance? item = FindItem(id);
        if (item is null) return null;
        Detach(item);
        item.Place(ItemCustody.Detached);
        return item;
    }

    /// <summary>
    /// Moves an item the party holds into a member's slot, displacing whatever that slot held.
    /// </summary>
    /// <remarks>
    /// The item may come from the shared pack or from another member's slot, so handing a weapon from one
    /// character to another is this one operation. Everything that could refuse the change is decided before
    /// anything moves: the ruleset's use rule judges the member, the slot, and the instance, and the pack
    /// must have room for what the slot would displace — otherwise a swap could lose the displaced item in
    /// the middle of itself. Nothing here is applied to a member id the roster does not have.
    /// </remarks>
    /// <param name="member">The member who would wear or wield the item.</param>
    /// <param name="slot">The slot of that member's figure to fill.</param>
    /// <param name="item">The item the party holds.</param>
    /// <returns>What the slot holds now, what the pack got back, or why nothing changed.</returns>
    /// <exception cref="ArgumentException">The party has no member with that identity.</exception>
    public EquipmentChange Equip(PartyMemberId member, EquipmentSlot slot, ItemInstanceId item)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        PartyMember owner = Member(member);
        ItemInstance? instance = FindItem(item);
        if (instance is null)
        {
            return EquipmentChange.Refused(new PartyRefusal(
                "item-not-held",
                $"The party holds no item {item}, so nothing was equipped."));
        }

        if (instance.Custody.IsEquipped && instance.Custody.Member == member && instance.Custody.Slot == slot)
        {
            return EquipmentChange.Changed(instance, null);
        }

        if (_equipmentUse?.Judge(owner, slot, instance) is { } refusal) return EquipmentChange.Refused(refusal);

        ItemInstance? displaced = owner.Equipment.ItemIn(slot);
        if (displaced is not null && Inventory.Judge(displaced.Definition, displaced.StackCount) is { } noRoom)
        {
            return EquipmentChange.Refused(noRoom);
        }

        Detach(instance);
        if (displaced is not null)
        {
            owner.Equipment.Detach(slot);
            Inventory.Append(displaced);
        }

        owner.Equipment.Attach(slot, instance);
        instance.Place(ItemCustody.EquippedBy(member, slot));
        return EquipmentChange.Changed(instance, displaced);
    }

    /// <summary>Takes whatever a member's slot holds back into the shared pack.</summary>
    /// <param name="member">The member whose figure to take the item off.</param>
    /// <param name="slot">The slot to empty.</param>
    /// <returns>What left the slot for the pack, or why nothing changed.</returns>
    /// <exception cref="ArgumentException">The party has no member with that identity.</exception>
    public EquipmentChange Unequip(PartyMemberId member, EquipmentSlot slot)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        PartyMember owner = Member(member);
        ItemInstance? item = owner.Equipment.ItemIn(slot);
        if (item is null)
        {
            return EquipmentChange.Refused(new PartyRefusal(
                "slot-empty",
                $"Member {member} has nothing in '{slot}', so there was nothing to take off."));
        }

        if (Inventory.Judge(item.Definition, item.StackCount) is { } refusal) return EquipmentChange.Refused(refusal);

        owner.Equipment.Detach(slot);
        Inventory.Append(item);
        return EquipmentChange.Changed(null, item);
    }

    /// <summary>Captures the party's durable state: what a save writes and a restore rebuilds from.</summary>
    /// <remarks>
    /// The capture is a snapshot of values, not a view of live state: member seeds and item states are
    /// immutable, so playing on after a capture cannot change what was captured.
    /// </remarks>
    public PartySave Capture()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        List<PartyMemberSave> members = [];
        foreach (PartyMember member in Roster.Members)
        {
            members.Add(new PartyMemberSave(member.Id, new PartyMemberSeed(
                member.Profile.Name,
                member.Profile.Race,
                member.Profile.Class,
                [.. member.Attributes.Scores],
                [.. member.Skills.Entries],
                [.. member.Spells.Known],
                member.Progression.Experience,
                member.Progression.Level,
                member.Progression.SkillPoints,
                member.Progression.ClassRank,
                [.. member.Conditions.Active],
                member.Resources.HitPoints,
                member.Resources.SpellPoints)));
        }

        List<ItemSave> items = [];
        foreach (ItemInstance item in Items)
        {
            items.Add(new ItemSave(item.Id, item.Definition, item.StackCount, item.State, item.Custody));
        }

        return new PartySave(
            Identity.NextMemberValue,
            Identity.NextItemValue,
            members,
            items,
            Purse.Coins,
            Food.Portions,
            Food.Unit,
            Reputation.Reputation,
            Reputation.Fame,
            [.. Followers.Followers],
            [.. Effects.Active]);
    }

    /// <summary>Disposes the store the party was created in, when this party created it.</summary>
    /// <remarks>
    /// A wrapped party owns no store and disposes nothing. Disposing ends the party's entities with the
    /// store, which is what ending a session does; a save captured beforehand is unaffected.
    /// </remarks>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_ownsStore) _store.Dispose();
    }

    /// <summary>Takes an instance out of whatever holds it inside the party, leaving its custody untouched.</summary>
    private void Detach(ItemInstance item)
    {
        if (item.Custody.IsEquipped)
        {
            Member(item.Custody.Member).Equipment.Detach(item.Custody.Slot);
            return;
        }

        Inventory.Remove(item.Id);
    }
}
