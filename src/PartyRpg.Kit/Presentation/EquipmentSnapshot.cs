using PartyRpg.Kit.Party;

namespace PartyRpg.Kit.Presentation;

/// <summary>One thing a member wears, as the figure screen shows it.</summary>
/// <param name="Slot">The slot it is in, in the game's own word, which a take-off names.</param>
/// <param name="Item">The instance's durable identity.</param>
/// <param name="Definition">The content definition the instance is a copy of.</param>
/// <param name="Name">What a person reads for it.</param>
public sealed record EquipmentWornSnapshot(string Slot, string Item, string Definition, string Name)
{
    /// <summary>Writes one occupied slot.</summary>
    /// <param name="builder">The projection being built.</param>
    /// <returns>The row's node.</returns>
    internal uint Write(UiValueBuilder builder) =>
        builder.Object(
            ("slot", builder.String(Slot)),
            ("item", builder.String(Item)),
            ("definition", builder.String(Definition)),
            ("name", builder.String(Name)));
}

/// <summary>One member's figure, as the screen draws it: who, and what each occupied slot holds.</summary>
/// <param name="Index">The member's place in the party, counted from zero, which a change names.</param>
/// <param name="Member">The member's durable identity.</param>
/// <param name="Name">What the member is called.</param>
/// <param name="Worn">The occupied slots, in the order the game's figure lists its slots.</param>
public sealed record EquipmentMemberSnapshot(int Index, string Member, string Name, IReadOnlyList<EquipmentWornSnapshot> Worn)
{
    /// <summary>Writes one member's figure.</summary>
    /// <param name="builder">The projection being built.</param>
    /// <returns>The row's node.</returns>
    internal uint Write(UiValueBuilder builder) =>
        builder.Object(
            ("index", builder.Number(Index)),
            ("member", builder.String(Member)),
            ("name", builder.String(Name)),
            ("worn", builder.Array([.. Worn.Select(worn => worn.Write(builder))])));
}

/// <summary>One thing in the shared pack that the game's figure has a place for.</summary>
/// <param name="Item">The instance's durable identity, which an equip echoes back.</param>
/// <param name="Definition">The content definition the instance is a copy of.</param>
/// <param name="Name">What a person reads for it.</param>
/// <param name="Slots">The slots it is shaped for, most likely first, as the game's figure answers.</param>
public sealed record EquipmentItemSnapshot(string Item, string Definition, string Name, IReadOnlyList<string> Slots)
{
    /// <summary>Writes one wearable thing in the pack.</summary>
    /// <param name="builder">The projection being built.</param>
    /// <returns>The row's node.</returns>
    internal uint Write(UiValueBuilder builder) =>
        builder.Object(
            ("item", builder.String(Item)),
            ("definition", builder.String(Definition)),
            ("name", builder.String(Name)),
            ("slots", builder.Array([.. Slots.Select(builder.String)])));
}

/// <summary>What members wear, what the pack could be worn from, and what the last change did.</summary>
/// <remarks>
/// Every row is the party's own state read through the game's own figure: the slots are the game's words, an
/// item appears in the pack's list because the figure has a place for it, and whether this member may put it
/// there is judged when a change is asked for — a skill, a mastery, a full hand — and refused by name in the
/// outcome rather than worked out by the screen. A session whose ruleset states no figure publishes
/// <see cref="None"/>.
/// </remarks>
/// <param name="Available">Whether the session holds the equipment owner at all.</param>
/// <param name="Slots">The figure's slots, in the order a screen draws them.</param>
/// <param name="Members">Each member's figure, in party order.</param>
/// <param name="Items">The pack's wearable things, in the order the pack holds them.</param>
/// <param name="Outcome">What the last change did, or null when nothing has been asked.</param>
public sealed record EquipmentSnapshot(
    bool Available,
    IReadOnlyList<string> Slots,
    IReadOnlyList<EquipmentMemberSnapshot> Members,
    IReadOnlyList<EquipmentItemSnapshot> Items,
    OutfittingResult? Outcome)
{
    /// <summary>The equipment of a session that holds no figure.</summary>
    public static EquipmentSnapshot None => new(false, [], [], [], null);

    /// <summary>The outcome row of a block no change has been asked of: every field in its empty reading.</summary>
    private static readonly OutfittingResult NoOutcome =
        new(string.Empty, string.Empty, string.Empty, 0, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);

    /// <summary>Reads the figure screen out of the equipment owner, or nothing when the session holds none.</summary>
    /// <param name="outfitting">The session's equipment owner, or null when it composes none.</param>
    /// <returns>What the figure screen shows.</returns>
    public static EquipmentSnapshot From(PartyOutfitting? outfitting)
    {
        if (outfitting is not { } owner) return None;
        IReadOnlyList<EquipmentSlot> slots = owner.Figure.Slots;

        List<EquipmentMemberSnapshot> members = [];
        for (int index = 0; index < owner.Party.Members.Count; index++)
        {
            PartyMember member = owner.Party.Members[index];
            List<EquipmentWornSnapshot> worn = [];

            // The figure's own order first, so two projections of the same figure read alike; a slot the figure
            // does not list still shows, after them, rather than an item the member wears going unseen.
            foreach (EquipmentSlot slot in slots)
            {
                if (member.Equipment.ItemIn(slot) is { } item) worn.Add(Worn(owner, slot, item));
            }

            foreach (EquippedItem equipped in member.Equipment.Items)
            {
                if (!slots.Contains(equipped.Slot)) worn.Add(Worn(owner, equipped.Slot, equipped.Item));
            }

            members.Add(new EquipmentMemberSnapshot(index, member.Id.ToString(), member.Profile.Name, worn));
        }

        List<EquipmentItemSnapshot> items = [];
        foreach (ItemInstance item in owner.Party.Inventory.Items)
        {
            IReadOnlyList<EquipmentSlot> shaped = owner.Figure.SlotsFor(item);
            if (shaped.Count == 0) continue;
            items.Add(new EquipmentItemSnapshot(
                item.Id.ToString(),
                item.Definition.Value,
                owner.NameOf(item.Definition),
                [.. shaped.Select(slot => slot.Value)]));
        }

        return new EquipmentSnapshot(true, [.. slots.Select(slot => slot.Value)], members, items, owner.Last);
    }

    /// <summary>Writes the equipment block: each member's figure, the pack's wearable things, and the last change.</summary>
    /// <param name="builder">The projection being built.</param>
    /// <returns>The block's node.</returns>
    internal uint Write(UiValueBuilder builder)
    {
        OutfittingResult last = Outcome ?? NoOutcome;
        return builder.Object(
            ("available", builder.Boolean(Available)),
            ("slots", builder.Array([.. Slots.Select(builder.String)])),
            ("members", builder.Array([.. Members.Select(member => member.Write(builder))])),
            ("items", builder.Array([.. Items.Select(item => item.Write(builder))])),
            // A change is made on a member, so one is offered while there is somebody to wear it.
            ("canEquip", builder.Boolean(Members.Count > 0)),
            ("outcome", builder.Object(
                ("outcome", builder.String(last.Outcome)),
                ("code", builder.String(last.Code)),
                ("message", builder.String(last.Message)),
                ("member", builder.Number(last.Member)),
                ("wearer", builder.String(last.Wearer)),
                ("slot", builder.String(last.Slot)),
                ("item", builder.String(last.Item)),
                ("itemName", builder.String(last.ItemName)),
                ("displaced", builder.String(last.Displaced)),
                ("displacedName", builder.String(last.DisplacedName)))));
    }

    private static EquipmentWornSnapshot Worn(PartyOutfitting owner, EquipmentSlot slot, ItemInstance item) =>
        new(slot.Value, item.Id.ToString(), item.Definition.Value, owner.NameOf(item.Definition));
}
