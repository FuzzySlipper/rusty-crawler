using PartyRpg.Kit.Conversation;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Quests;

/// <summary>What one quest a party has taken is asked about: whether a death counts for one of its objectives.</summary>
/// <remarks>
/// The placement travels whole rather than as a name, because what a creature <em>is</em> is content's own
/// reading: this game reads a monster row from the placement's entry, and the kit never learns what a
/// monster row contains. The place is passed beside it so an objective that names one can be answered
/// without this mechanism comparing places itself.
/// </remarks>
/// <param name="Definition">The quest the objective belongs to.</param>
/// <param name="Objective">The objective the death is being read against.</param>
/// <param name="Place">The place the creature fell in.</param>
/// <param name="Body">The creature's placement, exactly as the world created it from content.</param>
/// <param name="Name">What the game calls the creature, as the fight reported it.</param>
public sealed record QuestKillRequest(
    QuestDefinition Definition,
    QuestObjective Objective,
    PlaceId Place,
    PlacementDefinition Body,
    string Name);

/// <summary>What one of a quest's stated conditions is asked about.</summary>
/// <remarks>
/// The party and the clock travel whole for the same reason they do everywhere else: what a flag means,
/// how much standing is enough, which class or race a member is, which hours are day, and what an errand is
/// are this game's policy over its own state, so this mechanism asks and never compares.
/// </remarks>
/// <param name="Quest">The quest whose condition it is.</param>
/// <param name="Condition">The condition as the quest states it.</param>
/// <param name="Party">The party the condition is read against.</param>
/// <param name="Clock">The session's one clock, or null when its ruleset composed none.</param>
public sealed record QuestConditionRequest(
    QuestDefinition Quest,
    ConversationCondition Condition,
    PartyEntity Party,
    GameClock? Clock);

/// <summary>
/// What this game answers about quests: which quests it states, and what one of their objectives or
/// conditions means.
/// </summary>
/// <remarks>
/// <para>
/// This is the ruleset's whole contribution to quests, and it is deliberately three answers rather than one.
/// <see cref="Definitions"/> is the catalog: every quest this game states, read from its own content and its
/// own tables. <see cref="Definition"/> is that catalog read by identity, which a save's load and a person's
/// offer both need. <see cref="Counts"/> is what a creature's death means for an objective — the one thing
/// about an objective this mechanism cannot decide, because a creature is a content row and not a name.
/// <see cref="Holds"/> is what a stated condition means.
/// </para>
/// <para>
/// <b>Nothing here changes anything.</b> A quest's stage, its progress, and its rewards are the owner's to
/// write; a rule that advanced an objective itself would be a second writer of one fact. What the rule
/// answers is what the owner needs in order to decide, so the judgement a refusal lists and the judgement a
/// journal shows are one answer rather than two.
/// </para>
/// <para>
/// <b>A ruleset may state no quests at all.</b> A session composed without one has none of this: no quest is
/// offered, no errand a rank asks for is finished, and the panel says the mechanism is not there rather than
/// showing a journal nobody fills.
/// </para>
/// </remarks>
public interface IQuestRule
{
    /// <summary>Every quest this game states, in the order it states them.</summary>
    IReadOnlyList<QuestDefinition> Definitions { get; }

    /// <summary>One quest by the identity content and a save name it, or null when this game states none.</summary>
    /// <param name="quest">The quest's identity.</param>
    /// <returns>The definition, or null.</returns>
    QuestDefinition? Definition(QuestId quest);

    /// <summary>Whether one creature's death counts for one objective, and how much of it.</summary>
    /// <remarks>
    /// The amount is a number rather than a flag because a death can be worth more than one step of an
    /// objective — a placement that stands for a group, a deed counted twice — and because stating it here
    /// keeps the arithmetic in the owner where a count is recorded.
    /// </remarks>
    /// <param name="request">The quest, the objective, and the death as the fight reported it.</param>
    /// <returns>How much of that objective the death is worth, zero when it does not count.</returns>
    int Counts(QuestKillRequest request);

    /// <summary>Whether one condition a quest states holds for this party now.</summary>
    /// <param name="request">The quest, the condition, the party, and the clock.</param>
    /// <returns>Whether it holds.</returns>
    bool Holds(QuestConditionRequest request);
}
