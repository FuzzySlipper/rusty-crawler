using PartyRpg.Kit.Party;
using PartyRpg.Kit.Quests;

namespace PartyRpg.Kit.Tests;

/// <summary>A real accepted retrieve errand for caller tests that must preserve its needed instance.</summary>
internal static class NeededItemErrand
{
    internal static PartyQuests Take(PartyEntity party, ItemDefinitionId item)
    {
        QuestDefinition definition = new(new QuestId("needed-item"), "Keep the needed item", "keeper",
            [new QuestObjective("carry", QuestObjectiveKind.Retrieve, item.Value, label: "Carry the needed item")]);
        PartyQuests quests = new(new Rule(definition), party);
        quests.Offer(definition.Id, "keeper");
        quests.Accept(definition.Id);
        return quests;
    }

    private sealed class Rule(QuestDefinition definition) : IQuestRule
    {
        public IReadOnlyList<QuestDefinition> Definitions => [definition];
        public QuestDefinition? Definition(QuestId quest) => quest == definition.Id ? definition : null;
        public int Counts(QuestKillRequest request) => 0;
        public bool Holds(QuestConditionRequest request) => true;
    }
}
