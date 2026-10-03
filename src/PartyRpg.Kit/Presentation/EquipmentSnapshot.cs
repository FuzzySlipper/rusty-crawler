using PartyRpg.Kit.Party;

namespace PartyRpg.Kit.Presentation;

/// <summary>One thing a member wears, as the figure screen shows it.</summary>
/// <param name="Slot">The slot it is in, in the game's own word, which a take-off names.</param>
/// <param name="Item">The instance's durable identity.</param>
/// <param name="Definition">The content definition the instance is a copy of.</param>
/// <param name="Name">What a person reads for it.</param>
public sealed record EquipmentWornSnapshot(string Slot, string Item, string Definition, string Name)
{
    /// <summary>The URL the Engine serves the item's picture at, empty when it has none to show.</summary>
    public string Image { get; init; } = string.Empty;

    /// <summary>What a player is told about the item, in the game's words, or nothing for a game that reads none.</summary>
    public ItemReading? Reading { get; init; }

    /// <summary>Writes one occupied slot.</summary>
    /// <param name="builder">The projection being built.</param>
    /// <returns>The row's node.</returns>
    internal uint Write(UiValueBuilder builder) =>
        builder.Object(
            ("slot", builder.String(Slot)),
            ("item", builder.String(Item)),
            ("definition", builder.String(Definition)),
            ("name", builder.String(Name)),
            ("image", builder.String(Image)),
            ("kind", builder.String(Reading?.Kind ?? string.Empty)),
            ("facts", builder.Array([.. (Reading?.Facts ?? []).Select(builder.String)])));
}

/// <summary>One thing in the party's shared pack, as the inventory page shows and inspects it.</summary>
/// <param name="Item">The instance's durable identity, which every action on it echoes back.</param>
/// <param name="Definition">The content definition the instance is a copy of.</param>
/// <param name="Name">What a person reads for it.</param>
/// <param name="Image">The URL the Engine serves its picture at, empty when it has none to show.</param>
/// <param name="Reading">What a player is told about it, or null for a game that reads none.</param>
/// <param name="Slots">The slots the figure has for it, most likely first; empty when nobody can wear it.</param>
/// <param name="Use">The ordinary action the game offers for it, such as drinking or reading, or empty.</param>
/// <param name="Retained">Why the party may not part with it — the quest that needs it — or empty.</param>
public sealed record EquipmentPackSnapshot(
    string Item,
    string Definition,
    string Name,
    string Image,
    ItemReading? Reading,
    IReadOnlyList<string> Slots,
    string Use,
    string Retained)
{
    internal uint Write(UiValueBuilder builder) =>
        builder.Object(
            ("item", builder.String(Item)),
            ("definition", builder.String(Definition)),
            ("name", builder.String(Name)),
            ("image", builder.String(Image)),
            ("kind", builder.String(Reading?.Kind ?? string.Empty)),
            ("facts", builder.Array([.. (Reading?.Facts ?? []).Select(builder.String)])),
            ("slots", builder.Array([.. Slots.Select(builder.String)])),
            ("use", builder.String(Use)),
            ("retained", builder.String(Retained)));
}

/// <summary>One member's figure, as the screen draws it: who, and what each occupied slot holds.</summary>
/// <param name="Index">The member's place in the party, counted from zero, which a change names.</param>
/// <param name="Member">The member's durable identity.</param>
/// <param name="Name">What the member is called.</param>
/// <param name="Worn">The occupied slots, in the order the game's figure lists its slots.</param>
public sealed record EquipmentMemberSnapshot(int Index, string Member, string Name, IReadOnlyList<EquipmentWornSnapshot> Worn, string Powers = "")
{
    /// <summary>Writes one member's figure.</summary>
    /// <param name="builder">The projection being built.</param>
    /// <returns>The row's node.</returns>
    internal uint Write(UiValueBuilder builder) =>
        builder.Object(
            ("index", builder.Number(Index)),
            ("member", builder.String(Member)),
            ("name", builder.String(Name)),
            ("worn", builder.Array([.. Worn.Select(worn => worn.Write(builder))])),
            ("powers", builder.String(Powers)));
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
    /// <summary>The pack's actual non-spell item actions.</summary>
    public IReadOnlyList<EquipmentUseSnapshot> Uses { get; init; } = [];
    /// <summary>The last ordinary item-use answer.</summary>
    public ItemUseResult? UseOutcome { get; init; }

    /// <summary>Everything in the shared pack, wearable or not, in the order the pack holds it.</summary>
    public IReadOnlyList<EquipmentPackSnapshot> Pack { get; init; } = [];

    /// <summary>The equipment of a session that holds no figure.</summary>
    public static EquipmentSnapshot None => new(false, [], [], [], null);

    /// <summary>The outcome row of a block no change has been asked of: every field in its empty reading.</summary>
    private static readonly OutfittingResult NoOutcome =
        new(string.Empty, string.Empty, string.Empty, 0, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);

    /// <summary>Reads the figure screen out of the equipment owner, or nothing when the session holds none.</summary>
    /// <param name="outfitting">The session's equipment owner, or null when it composes none.</param>
    /// <returns>What the figure screen shows.</returns>
    /// <param name="itemUses">The ordinary item uses, when the session composes them.</param>
    /// <param name="readings">The game's reading of an item, when it states one.</param>
    /// <param name="pictures">The images items are drawn with, when the session grants any.</param>
    public static EquipmentSnapshot From(PartyOutfitting? outfitting, PartyItemUse? itemUses = null,
        IItemReadingRule? readings = null, ContentImages? pictures = null)
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
                if (member.Equipment.ItemIn(slot) is { } item) worn.Add(Worn(owner, slot, item, itemUses, readings, pictures));
            }

            foreach (EquippedItem equipped in member.Equipment.Items)
            {
                if (!slots.Contains(equipped.Slot)) worn.Add(Worn(owner, equipped.Slot, equipped.Item, itemUses, readings, pictures));
            }

            members.Add(new EquipmentMemberSnapshot(index, member.Id.ToString(), member.Profile.Name, worn, itemUses?.Rule.Describe(member) ?? ""));
        }

        List<EquipmentItemSnapshot> items = [];
        foreach (ItemInstance item in owner.Party.Inventory.Items)
        {
            IReadOnlyList<EquipmentSlot> shaped = owner.Figure.SlotsFor(item);
            if (shaped.Count == 0) continue;
            items.Add(new EquipmentItemSnapshot(
                item.Id.ToString(),
                item.Definition.Value,
                ItemName(owner, item, itemUses),
                [.. shaped.Select(slot => slot.Value)]));
        }

        return new EquipmentSnapshot(true, [.. slots.Select(slot => slot.Value)], members, items, owner.Last)
        {
            Uses = itemUses is null ? [] : [.. owner.Party.Inventory.Items
                .Where(item => itemUses.Rule.ActionOf(item) is not null)
                .Select(item => new EquipmentUseSnapshot(item.Id.ToString(), owner.NameOf(item.Definition) + " — " + itemUses.Rule.Describe(item), itemUses.Rule.ActionOf(item)!))],
            UseOutcome = itemUses?.Last,
            Pack = [.. owner.Party.Inventory.Items.Select(item => new EquipmentPackSnapshot(
                item.Id.ToString(),
                item.Definition.Value,
                ItemName(owner, item, itemUses),
                Picture(item, readings, pictures),
                readings?.Read(item),
                [.. owner.Figure.SlotsFor(item).Select(slot => slot.Value)],
                itemUses?.Rule.ActionOf(item) ?? string.Empty,
                owner.Party.JudgeItemRetention(item.Definition)?.Message ?? string.Empty))],
        };
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
            ("uses", builder.Array([.. Uses.Select(item => builder.Object(("item", builder.String(item.Item)), ("name", builder.String(item.Name)), ("action", builder.String(item.Action))))])),
            ("pack", builder.Array([.. Pack.Select(item => item.Write(builder))])),
            ("useOutcome", builder.Object(("outcome", builder.String(UseOutcome is null ? "" : UseOutcome.Applied ? "used" : "refused")),
                ("code", builder.String(UseOutcome?.Code ?? "")), ("message", builder.String(UseOutcome?.Message ?? "")))) ,
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

    private static string ItemName(PartyOutfitting owner, ItemInstance item, PartyItemUse? uses = null) => owner.NameOf(item.Definition) +
        (uses?.Rule.Describe(item) is { Length: > 0 } powers ? $" — {powers}" : string.Empty) +
        (item.State.Enchantment is { } property ? $" — {property.Property} {property.Strength}" : string.Empty) +
        (item.State.IsHardened ? " — hardened" : string.Empty);

    private static EquipmentWornSnapshot Worn(PartyOutfitting owner, EquipmentSlot slot, ItemInstance item, PartyItemUse? uses,
        IItemReadingRule? readings, ContentImages? pictures) =>
        new(slot.Value, item.Id.ToString(), item.Definition.Value, ItemName(owner, item, uses))
        {
            Image = Picture(item, readings, pictures),
            Reading = readings?.Read(item),
        };

    private static string Picture(ItemInstance item, IItemReadingRule? readings, ContentImages? pictures) =>
        pictures is not null && readings?.PictureOf(item) is { } key ? pictures.Url(key) : string.Empty;
}

/// <summary>An ordinary item action offered by the compiled game policy.</summary>
public sealed record EquipmentUseSnapshot(string Item, string Name, string Action);
