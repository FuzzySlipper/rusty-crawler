using System.Globalization;

namespace PartyRpg.Kit.Party;

/// <summary>What one change to a member's figure did, or why nothing changed, as a panel shows it.</summary>
/// <remarks>
/// Every field is the party's own answer read straight through: who wears it, what went on, which slot it went
/// to, and what came back to the pack. A refusal carries the code and sentence of whichever rule said no — the
/// game's use rule (a skill, a mastery, a hand already full) or the party itself (an item it does not hold, a
/// pack with no room for what would come off) — so a screen shows why without knowing which rule it was.
/// </remarks>
/// <param name="Outcome"><c>equipped</c>, <c>unequipped</c>, or <c>refused</c>.</param>
/// <param name="Code">The outcome's own code, which a screen and a test key on.</param>
/// <param name="Message">What happened, in a sentence a person reads.</param>
/// <param name="Member">The member's place in the party, counted from zero.</param>
/// <param name="Wearer">What that member is called, empty when the party has no such member.</param>
/// <param name="Slot">The slot that changed, or the one asked for when nothing did; empty when none was named.</param>
/// <param name="Item">The instance that went on, or came off; empty when none did.</param>
/// <param name="ItemName">What a person reads for it.</param>
/// <param name="Displaced">The instance an equip took off the same slot, empty when the slot was free.</param>
/// <param name="DisplacedName">What a person reads for it.</param>
public sealed record OutfittingResult(
    string Outcome,
    string Code,
    string Message,
    int Member,
    string Wearer,
    string Slot,
    string Item,
    string ItemName,
    string Displaced,
    string DisplacedName)
{
    /// <summary>Whether the figure changed.</summary>
    public bool Changed => !string.Equals(Outcome, Refused, StringComparison.Ordinal);

    /// <summary>The outcome word of a change that went onto a figure.</summary>
    public const string Equipped = "equipped";

    /// <summary>The outcome word of a change that came off a figure.</summary>
    public const string Unequipped = "unequipped";

    /// <summary>The outcome word of a change nothing carried out.</summary>
    public const string Refused = "refused";
}

/// <summary>
/// The one way a player changes what a member wears: put an item the party holds on a member, or take one off,
/// through the party's own equip and unequip.
/// </summary>
/// <remarks>
/// <para>
/// <b>The party moves the item and the game judges it.</b> Everything that changes custody is
/// <see cref="PartyEntity.Equip"/> and <see cref="PartyEntity.Unequip"/>, which ask the game's use rule before
/// anything moves; this owner adds only what a player's request lacks — which slot, when the request names none
/// — and keeps the answer the last request got, so a screen shows a refusal by its own words.
/// </para>
/// <para>
/// <b>An unnamed slot is the figure's own suggestion.</b> The game's figure lists the slots an item is shaped for,
/// most likely first. A free one is tried before an occupied one, so a second ring finds the second finger rather
/// than replacing the first, and each try is a whole equip that changes nothing when refused: the first one the
/// rules admit is the change, and when none is, the refusal the first try got is the answer.
/// </para>
/// </remarks>
public sealed class PartyOutfitting
{
    private readonly IGameNames? _names;

    /// <summary>Creates the owner over a party and the game's figure.</summary>
    /// <param name="party">The party whose members wear things.</param>
    /// <param name="figure">The game's figure: its slots, and which an item may go to.</param>
    /// <param name="names">The game's words for its items, or null to print their identities.</param>
    public PartyOutfitting(PartyEntity party, IEquipmentFigure figure, IGameNames? names = null)
    {
        Party = party ?? throw new ArgumentNullException(nameof(party));
        Figure = figure ?? throw new ArgumentNullException(nameof(figure));
        _names = names;
    }

    /// <summary>The party whose members wear things.</summary>
    public PartyEntity Party { get; }

    /// <summary>The game's figure.</summary>
    public IEquipmentFigure Figure { get; }

    /// <summary>
    /// The change stamp this owner took when the last change of figure it reports last changed, or when it was
    /// made: a reader that kept what it built beside this stamp reads the owner again only when the stamp has moved
    /// (<see cref="ChangeStamp"/>).
    /// </summary>
    public long Stamp { get; private set; } = ChangeStamp.Next();

    /// <summary>What the last request did, or null when nothing has been asked.</summary>
    public OutfittingResult? Last { get; private set; }

    /// <summary>What a person reads for an item definition.</summary>
    /// <param name="definition">The definition to name.</param>
    public string NameOf(ItemDefinitionId definition) => GameNames.Item(_names, definition);

    /// <summary>Puts an item the party holds on a member, in the slot named or the figure's best free one.</summary>
    /// <param name="member">The member's place in the party, counted from zero.</param>
    /// <param name="item">The instance to wear, from the pack or from any member's figure.</param>
    /// <param name="slot">The slot to fill, or null to let the figure choose.</param>
    /// <returns>What changed, or why nothing did.</returns>
    public OutfittingResult Equip(int member, ItemInstanceId item, EquipmentSlot? slot = null)
    {
        if (Wearer(member) is not { } wearer) return Refuse(member, slot, Unknown(member));

        if (Party.FindItem(item) is not { } instance)
        {
            return Refuse(member, slot, new Refusal(
                PartyCodes.ItemNotHeld,
                $"The party holds no item {item}, so nothing was put on {wearer.Profile.Name}."));
        }

        List<EquipmentSlot> tries = [];
        if (slot is { } named)
        {
            if (!Figure.Slots.Contains(named))
            {
                return Refuse(member, slot, new Refusal(
                    PartyCodes.SlotNotOnFigure,
                    $"A figure has no '{named}'; its places are {string.Join(", ", Figure.Slots)}."));
            }

            tries.Add(named);
        }
        else
        {
            IReadOnlyList<EquipmentSlot> shaped = Figure.SlotsFor(instance);
            tries.AddRange(shaped.Where(candidate => !wearer.Equipment.Has(candidate)));
            tries.AddRange(shaped.Where(candidate => wearer.Equipment.Has(candidate)));
        }

        if (tries.Count == 0)
        {
            return Refuse(member, slot, new Refusal(
                PartyCodes.ItemNotWearable,
                $"{NameOf(instance.Definition)} is not something a character wears or wields, so {wearer.Profile.Name} was given nothing."));
        }

        Refusal? first = null;
        foreach (EquipmentSlot candidate in tries)
        {
            EquipmentChange change = Party.Equip(wearer.Id, candidate, item);
            if (change.Refusal is { } refused)
            {
                first ??= refused;
                continue;
            }

            string displaced = change.Displaced is { } off
                ? string.Create(CultureInfo.InvariantCulture, $"; {NameOf(off.Definition)} went back to the pack")
                : string.Empty;
            return Record(new OutfittingResult(
                OutfittingResult.Equipped,
                PartyCodes.ItemEquipped,
                $"{wearer.Profile.Name} now wears {NameOf(instance.Definition)} in '{candidate}'{displaced}.",
                member,
                wearer.Profile.Name,
                candidate.Value,
                instance.Id.ToString(),
                NameOf(instance.Definition),
                change.Displaced?.Id.ToString() ?? string.Empty,
                change.Displaced is { } back ? NameOf(back.Definition) : string.Empty));
        }

        return Refuse(member, slot, first!);
    }

    /// <summary>Takes whatever a member's slot holds back into the shared pack.</summary>
    /// <param name="member">The member's place in the party, counted from zero.</param>
    /// <param name="slot">The slot to empty.</param>
    /// <returns>What came off, or why nothing did.</returns>
    public OutfittingResult Unequip(int member, EquipmentSlot slot)
    {
        if (Wearer(member) is not { } wearer) return Refuse(member, slot, Unknown(member));
        EquipmentChange change = Party.Unequip(wearer.Id, slot);
        if (change.Refusal is { } refused) return Refuse(member, slot, refused);

        ItemInstance off = change.Displaced!;
        return Record(new OutfittingResult(
            OutfittingResult.Unequipped,
            PartyCodes.ItemUnequipped,
            $"{wearer.Profile.Name} took {NameOf(off.Definition)} off '{slot}', and it is in the pack.",
            member,
            wearer.Profile.Name,
            slot.Value,
            off.Id.ToString(),
            NameOf(off.Definition),
            string.Empty,
            string.Empty));
    }

    private PartyMember? Wearer(int member) =>
        member >= 0 && member < Party.Members.Count ? Party.Members[member] : null;

    private Refusal Unknown(int member) => new(
        PartyCodes.EquipMemberUnknown,
        string.Create(CultureInfo.InvariantCulture, $"A change named member {member + 1}, and the party has {Party.Members.Count}."));

    private OutfittingResult Refuse(int member, EquipmentSlot? slot, Refusal refusal) =>
        Record(new OutfittingResult(
            OutfittingResult.Refused,
            refusal.Code,
            refusal.Message,
            member,
            Wearer(member)?.Profile.Name ?? string.Empty,
            slot?.Value ?? string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty));

    private OutfittingResult Record(OutfittingResult result)
    {
        Last = result;
        Stamp = ChangeStamp.Next();
        return result;
    }
}
