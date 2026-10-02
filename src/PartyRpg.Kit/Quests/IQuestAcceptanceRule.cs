using PartyRpg.Kit.Party;

namespace PartyRpg.Kit.Quests;

/// <summary>The accepted contract and the party undertaking it.</summary>
public sealed record QuestAcceptance(QuestDefinition Definition, PartyEntity Party);

/// <summary>A game's optional consequence of taking an errand, through its existing owners.</summary>
public interface IQuestAcceptanceRule
{
    /// <summary>Judges the consequence before the quest changes stage; null means it can proceed.</summary>
    Refusal? CanAccept(QuestAcceptance acceptance);

    /// <summary>The quest was accepted; applies the already judged consequence.</summary>
    void Accepted(QuestAcceptance acceptance);
}
