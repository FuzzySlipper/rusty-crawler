using PartyRpg.Kit.Input;
using PartyRpg.Kit.Party;

namespace PartyRpg.Kit.Sessions;

/// <summary>The control a product declares for changing what members wear: the contract its payload actions arrive on.</summary>
/// <remarks>
/// <para>
/// The actions' names are the kit's (<see cref="EquipActions"/>), as every semantic action's is; a product
/// declares the contract they arrive on.
/// </para>
/// <para>
/// <b>Equipping has no key of its own, and that is deliberate.</b> A change names a member and one of the
/// things the party carries, and no single press can say which of them the player meant — it is a drag onto a
/// figure in the original, so it is a payload action for the same reason mixing is: the screen's own rows name
/// what was chosen.
/// </para>
/// </remarks>
public sealed record EquipIntentNames
{
    /// <summary>Creates the declared equipment control.</summary>
    /// <param name="actionContract">The payload contract a screen's equipment commands arrive on.</param>
    /// <exception cref="ArgumentException">The contract is missing, so no event could ever be claimed on it.</exception>
    public EquipIntentNames(string actionContract)
    {
        ActionContract = !string.IsNullOrWhiteSpace(actionContract)
            ? actionContract
            : throw new ArgumentException(
                "The equipment control declares no contract, so no event could ever be claimed on it.",
                nameof(actionContract));
    }

    /// <summary>The payload contract a screen's equipment commands arrive on.</summary>
    public string ActionContract { get; }
}

/// <summary>The payload actions a figure screen sends when a player puts something on or takes it off.</summary>
public static class EquipActions
{
    /// <summary>Puts an item the party holds on a member, in a named slot or the figure's best one.</summary>
    public const string Equip = "party.equip";

    /// <summary>Takes what a member's slot holds back into the shared pack.</summary>
    public const string Unequip = "party.unequip";

    /// <summary>Uses a real pack item on a member through the game's item policy.</summary>
    public const string UseItem = "party.item.use";
}

/// <summary>One change a screen asked for: put an item on a member, or take a slot's item off.</summary>
/// <remarks>
/// The member is the party's own index, as a casting's and a mixture's are: the screen was shown the party in its
/// order and names the row it drew. An item is named by the identity the projection published, because two
/// swords of one kind are two things and which of them is worn is the party's fact.
/// </remarks>
/// <param name="Unequip">Whether the request takes a slot's item off rather than putting one on.</param>
/// <param name="Member">The member's place in the party, counted from zero.</param>
/// <param name="Item">The item to put on, or null when the request takes one off.</param>
/// <param name="Slot">The slot named, or null when an equip lets the figure choose.</param>
public readonly record struct EquipRequest(bool Unequip, int Member, ItemInstanceId? Item, EquipmentSlot? Slot);

/// <summary>The equipment changes a player asked for, read from the admitted input of each update, in the order they arrived.</summary>
public sealed class EquipInput
{
    private readonly string _actionContract;

    /// <summary>Creates the reader for the declared equipment control.</summary>
    /// <param name="names">The equipment control the host declared.</param>
    public EquipInput(EquipIntentNames names)
    {
        ArgumentNullException.ThrowIfNull(names);
        _actionContract = names.ActionContract;
    }

    /// <summary>
    /// The changes this update carried; an equip that names no item and an unequip that names no slot are dropped,
    /// because neither says what to change.
    /// </summary>
    /// <param name="inbox">The update's input.</param>
    /// <summary>The non-spell item uses this update carried on the inventory screen's contract.</summary>
    public IReadOnlyList<(int Member, ItemInstanceId Item)> ReadUses(ActionInbox inbox) =>
        [.. inbox.Take(_actionContract, name => name == EquipActions.UseItem)
            .Where(action => action.Identity("item") is not null)
            .Select(action => (action.Int("member") ?? 0, new ItemInstanceId(action.Identity("item")!.Value)))];

    public IReadOnlyList<EquipRequest> Read(ActionInbox inbox)
    {
        ArgumentNullException.ThrowIfNull(inbox);
        List<EquipRequest> requests = [];
        foreach (UiAction action in inbox.Take(_actionContract, name => name is EquipActions.Equip or EquipActions.Unequip))
        {
            string slotText = action.Text("slot").Trim();
            EquipmentSlot? slot = slotText.Length > 0 ? new EquipmentSlot(slotText) : null;
            int member = action.Int("member") ?? 0;
            if (string.Equals(action.Name, EquipActions.Unequip, StringComparison.Ordinal))
            {
                if (slot is not null) requests.Add(new EquipRequest(true, member, null, slot));
                continue;
            }

            if (action.Identity("item") is { } item) requests.Add(new EquipRequest(false, member, new ItemInstanceId(item), slot));
        }

        return requests;
    }
}
