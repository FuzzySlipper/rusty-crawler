namespace PartyRpg.Kit.Quests;

/// <summary>One objective as it stands for a party: what it asks, how much is done, and whether that is enough.</summary>
/// <remarks>
/// The count is the party's whole progress on that objective, whether it was recorded when it happened or
/// read from the owner that holds it — the reading does not say which, because to a journal they are the
/// same fact: what the errand still wants.
/// </remarks>
/// <param name="Objective">What the quest asked for.</param>
/// <param name="Count">How much of it the party has done.</param>
/// <param name="IsMet">Whether that is enough.</param>
public readonly record struct QuestObjectiveReading(QuestObjective Objective, int Count, bool IsMet)
{
    /// <summary>How it reads to a person, with the party's own progress in it.</summary>
    public string Statement => Objective.Count > 1
        ? $"{Objective.Label} ({Count}/{Objective.Count})"
        : Objective.Label;
}

/// <summary>One quest as one party stands with it: the definition, the instance, and what each asks.</summary>
/// <remarks>
/// <para>
/// <b>This is a reading and never a change.</b> Every fact here is composed at the moment it is asked for —
/// the definition from the game's own content, the recorded progress from the instance, and the progress
/// another owner holds from that owner — so a journal, a screen, and a turn-in all see one answer rather
/// than three that could drift.
/// </para>
/// <para>
/// <b>Completion is read, not recorded.</b> A quest is complete when every objective is met and every
/// completion condition holds; nothing writes that down, so a party that loses the thing an objective named
/// stops being complete in the same breath, and no stale flag can claim otherwise.
/// </para>
/// </remarks>
/// <param name="Definition">The quest as this game states it.</param>
/// <param name="Instance">What this party has recorded about it.</param>
/// <param name="Objectives">Every objective, with the party's progress and whether it is met.</param>
/// <param name="Unmet">What is not met yet, in the order the quest states it: objectives first, then conditions.</param>
public sealed record QuestReading(
    QuestDefinition Definition,
    QuestInstance Instance,
    IReadOnlyList<QuestObjectiveReading> Objectives,
    IReadOnlyList<string> Unmet)
{
    /// <summary>The quest's identity.</summary>
    public QuestId Quest => Definition.Id;

    /// <summary>What a person calls the quest.</summary>
    public string Name => Definition.Name;

    /// <summary>Whether every objective is met and every completion condition holds.</summary>
    public bool IsComplete => Unmet.Count == 0;

    /// <summary>Whether the errand has been turned in and paid for.</summary>
    public bool IsTurnedIn => Instance.Stage == QuestStage.TurnedIn;

    /// <summary>How the party's own journal words this quest at this moment.</summary>
    /// <remarks>
    /// <c>completed</c> is the fourth word, and it is deliberately not a stage: it says the errand may be
    /// turned in, which is a fact about the objectives rather than something the party did.
    /// </remarks>
    public string State => Instance.Stage switch
    {
        QuestStage.Offered => "offered",
        QuestStage.TurnedIn => "turned-in",
        _ => IsComplete ? "completed" : "accepted",
    };

    /// <summary>Whether the party may turn this quest in to the person standing in front of it.</summary>
    /// <returns>Whether it is accepted and complete.</returns>
    public bool CanTurnIn => Instance.Stage == QuestStage.Accepted && IsComplete;

    /// <inheritdoc />
    public override string ToString() => $"{Name} ({State})";
}
