using System.Text;
using PartyRpg.Kit.Input;
using PartyRpg.Kit.Party;

namespace PartyRpg.Kit.Sessions;

internal sealed partial class CombatDriver
{
    private readonly byte[]? _nextMemberIntent = names?.NextMember is { } intent ? Encoding.UTF8.GetBytes(intent) : null;

    // Selection is an instant over the actual roster, before the attack in this same admitted update. It
    // changes no recovery or clock and may be inspected while a screen or a player's hold owns the world.
    private void SelectMembers(ActionInbox input)
    {
        if (names is null || owners.Combat is not { } combat) return;
        foreach (UiAction action in input.Take(names.ActionContract, CombatActions.SelectMember))
        {
            PartyMemberId member = action.Identity("member") is { } value ? new(value) : default;
            Refusal? refusal = combat.SelectMember(member);
            owners.Diagnostics.Report(refusal is null, "party-selection", refusal?.Code ?? "member-selected", combat.Party.Roster.SelectionMessage);
        }

        bool next = _nextMemberIntent is not null && input.Activated(_nextMemberIntent);
        next = input.Take(names.ActionContract, CombatActions.NextMember).Count > 0 || next;
        if (!next) return;
        Refusal? answer = combat.SelectNextMember();
        owners.Diagnostics.Report(answer is null, "party-selection", answer?.Code ?? "member-selected", combat.Party.Roster.SelectionMessage);
    }
}
