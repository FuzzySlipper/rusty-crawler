using PartyRpg.Kit.Party;

namespace PartyRpg.Kit.Combat;

public sealed partial class CombatState
{
    /// <summary>The party's durable choice, addressed through this fight's existing combatant.</summary>
    public Combatant? Selected => _party.Roster.SelectedMember is { } member ? Find(CombatantId.Of(member)) : null;

    /// <summary>Whether the existing action rule permits selecting this member; recovery does not decide.</summary>
    public Verdict MemberSelection(PartyMember member)
    {
        Combatant? actor = Find(CombatantId.Of(member.Id));
        if (actor is null) return Verdict.Unmet("the member is no longer present in this fight.");
        return IsDown(actor) ? Verdict.Unmet($"{Describe(actor)} leaves them unable to act.") : Verdict.Met;
    }

    /// <summary>Selects one actual party member, refusing an incapable or unknown choice by name.</summary>
    public Refusal? SelectMember(PartyMemberId member)
    {
        Verdict capability = _party.Roster.TryMember(member, out PartyMember? candidate)
            ? MemberSelection(candidate)
            : Verdict.Unmet("the member is not in this party.");
        Refusal? refusal = _party.Roster.Select(member, capability);
        return refusal;
    }

    /// <summary>Cycles in roster order over capable members, including members who still owe recovery.</summary>
    public Refusal? SelectNextMember()
    {
        Refusal? refusal = _party.Roster.SelectNext(MemberSelection);
        return refusal;
    }

    /// <summary>Orders only the selected member through the existing attack gate.</summary>
    public CombatResult? EngageSelected()
    {
        if (Selected is not { } actor)
        {
            SelectNextMember();
            if (Selected is not { } replacement) return null;
            actor = replacement;
        }

        // The ordinary recovery gate still supplies its existing named refusal, even if another member
        // currently owns the turn. A ready off-turn member may be selected, but cannot spend another's turn.
        if (actor.IsReady && Pacing == CombatPacing.TurnBased && Turns.IsHolding
            && Turns.Phase == TurnPhase.Action && Turns.Current?.Id != actor.Id)
            return Report(CombatResult.Refused(actor.Id, actor.Name,
                new Refusal(CombatCodes.SelectedMemberNotTurn, $"{actor.Name} is selected, but this turn belongs to {Turns.Current?.Name ?? "another combatant"}; nothing was spent.")));

        return Engage(actor.Id);
    }

    private void ReconcileSelection() => _party.Roster.ReconcileSelection(MemberSelection);
}
