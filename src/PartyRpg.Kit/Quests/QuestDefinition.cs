using PartyRpg.Kit.Conversation;

namespace PartyRpg.Kit.Quests;

/// <summary>One quest as a game states it: who gives it, what it asks, what it pays, and what it leaves.</summary>
/// <remarks>
/// <para>
/// <b>A quest is data, and there is no class per quest.</b> The giver, the objectives, the conditions, the
/// rewards, the record a turn-in leaves, and any item the quest needs are all stated here, so adding an
/// errand is adding a row rather than adding a type. Nothing in this file knows what any quest is about.
/// </para>
/// <para>
/// <b>The definition is read, never written.</b> What a party has done about a quest lives in its instance,
/// so two parties that were offered the same definition are two instances of it and the definition is the
/// same value to both. That is what makes a definition cacheable, a save free of it, and a turn-in able to
/// re-read the terms it was accepted under.
/// </para>
/// <para>
/// <b>The conditions are the conversation's, deliberately.</b> A quest is offered in a conversation and
/// turned in to a person, so what must hold for either is the same vocabulary a topic's availability is
/// stated in — a party-carried flag, a standing, a class, a race, an hour, an errand — judged by the same
/// rule. A second vocabulary for quest gating would be two readings of one fact.
/// </para>
/// <para>
/// <b>The residue is what the errand asks for that this game does not judge.</b> A shipped errand can name
/// a deed — a weight moved, a code cracked, an altar defaced — that belongs to a mechanism this build does
/// not have, and a definition that said nothing about it would look like one that did it. What a game
/// cannot judge is stated here, in the same spirit as what an answer could not carry out.
/// </para>
/// </remarks>
/// <param name="Id">The quest's identity, which must not be blank.</param>
/// <param name="Name">What a person calls it, which must not be blank.</param>
/// <param name="Giver">The identity of whoever offers and receives it, which must not be blank.</param>
/// <param name="Objectives">What it asks the party to do, in the order they are stated; empty when it asks nothing.</param>
/// <param name="Rewards">What finishing it pays, or nothing when it pays nothing.</param>
/// <param name="OfferConditions">What must hold before it is offered; empty when it may always be offered.</param>
/// <param name="CompletionConditions">What must hold besides the objectives before it may be turned in; empty when nothing must.</param>
/// <param name="Record">The party-carried record a turn-in leaves, or empty when it leaves none.</param>
/// <param name="Note">What the journal shows about it, or empty when it shows nothing beyond the objectives.</param>
/// <param name="Residue">What the errand asks for that this game does not judge, or empty when it judges all of it.</param>
/// <exception cref="ArgumentException">An identity is blank or two objectives share one.</exception>
/// <exception cref="ArgumentNullException">No objectives were supplied.</exception>
public sealed record QuestDefinition
{
    /// <summary>Creates a definition.</summary>
    /// <param name="id">The quest's identity.</param>
    /// <param name="name">What a person calls it.</param>
    /// <param name="giver">The identity of whoever offers and receives it.</param>
    /// <param name="objectives">What it asks the party to do, in the order they are stated.</param>
    /// <param name="rewards">What finishing it pays, or nothing when it pays nothing.</param>
    /// <param name="offerConditions">What must hold before it is offered.</param>
    /// <param name="completionConditions">What must hold besides the objectives before it may be turned in.</param>
    /// <param name="record">The party-carried record a turn-in leaves, or empty when it leaves none.</param>
    /// <param name="note">What the journal shows about it, or empty when it shows nothing beyond the objectives.</param>
    /// <param name="residue">What the errand asks for that this game does not judge, or empty when it judges all of it.</param>
    /// <exception cref="ArgumentException">An identity is blank or two objectives share one.</exception>
    /// <exception cref="ArgumentNullException">No objectives were supplied.</exception>
    public QuestDefinition(
        QuestId id,
        string name,
        string giver,
        IReadOnlyList<QuestObjective> objectives,
        QuestRewards? rewards = null,
        IReadOnlyList<ConversationCondition>? offerConditions = null,
        IReadOnlyList<ConversationCondition>? completionConditions = null,
        string record = "",
        string note = "",
        string residue = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(giver);
        ArgumentNullException.ThrowIfNull(objectives);

        // Two objectives sharing an identity would leave which of them a death or a place advanced
        // ambiguous, and progress is recorded by identity, so the defect is caught where the definition is
        // built rather than where a party's progress would silently land on one of them.
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (QuestObjective objective in objectives)
        {
            if (!seen.Add(objective.Id))
            {
                throw new ArgumentException(
                    $"The quest '{id}' states the objective '{objective.Id}' more than once, so what a death or a place advanced would depend on which row was found first.",
                    nameof(objectives));
            }
        }

        Id = id;
        Name = name;
        Giver = giver;
        Objectives = [.. objectives];
        Rewards = rewards ?? QuestRewards.None;
        OfferConditions = offerConditions ?? [];
        CompletionConditions = completionConditions ?? [];
        Record = record ?? string.Empty;
        Note = note ?? string.Empty;
        Residue = residue ?? string.Empty;
    }

    /// <summary>The quest's identity.</summary>
    public QuestId Id { get; }

    /// <summary>What a person calls it.</summary>
    public string Name { get; }

    /// <summary>The identity of whoever offers and receives it.</summary>
    public string Giver { get; }

    /// <summary>What it asks the party to do, in the order they are stated.</summary>
    public IReadOnlyList<QuestObjective> Objectives { get; }

    /// <summary>What finishing it pays.</summary>
    public QuestRewards Rewards { get; }

    /// <summary>What must hold before it is offered.</summary>
    public IReadOnlyList<ConversationCondition> OfferConditions { get; }

    /// <summary>What must hold besides the objectives before it may be turned in.</summary>
    public IReadOnlyList<ConversationCondition> CompletionConditions { get; }

    /// <summary>The party-carried record a turn-in leaves, or empty when it leaves none.</summary>
    public string Record { get; }

    /// <summary>What the journal shows about it, or empty when it shows nothing beyond the objectives.</summary>
    public string Note { get; }

    /// <summary>What the errand asks for that this game does not judge, or empty when it judges all of it.</summary>
    public string Residue { get; }

    /// <summary>One objective by its identity, or null when the quest states none under it.</summary>
    /// <param name="id">The objective's identity within this quest.</param>
    /// <returns>The objective, or null.</returns>
    public QuestObjective? Objective(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (QuestObjective objective in Objectives)
        {
            if (string.Equals(objective.Id, id, StringComparison.Ordinal)) return objective;
        }

        return null;
    }

    /// <summary>Every item this quest's own objectives ask the party to carry, in the order they are stated.</summary>
    /// <remarks>
    /// It is read from the objectives rather than stated a second time: what a quest needs is exactly what
    /// an unmet retrieve or deliver objective names, so an item cannot be needed by a list and not by the
    /// errand, or the other way round.
    /// </remarks>
    /// <returns>The item definitions the objectives name, without duplicates.</returns>
    public IReadOnlyList<string> NeededItems()
    {
        List<string> items = [];
        foreach (QuestObjective objective in Objectives)
        {
            if (objective.Kind is not (QuestObjectiveKind.Retrieve or QuestObjectiveKind.Deliver)) continue;
            if (!items.Contains(objective.Target, StringComparer.Ordinal)) items.Add(objective.Target);
        }

        return items;
    }

    /// <inheritdoc />
    public override string ToString() => $"{Name} ({Id})";
}
