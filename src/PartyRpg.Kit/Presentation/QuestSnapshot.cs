using PartyRpg.Kit.Quests;

namespace PartyRpg.Kit.Presentation;

/// <summary>One objective of a quest, as the journal shows it.</summary>
/// <param name="Id">The objective's identity within its quest.</param>
/// <param name="Label">What the quest asks for, in words a person reads.</param>
/// <param name="Count">How much of it the party has done.</param>
/// <param name="Required">How much of it the quest asks for.</param>
/// <param name="Met">Whether that is enough.</param>
public sealed record QuestObjectiveSnapshot(string Id, string Label, int Count, int Required, bool Met)
{
    /// <summary>Writes one objective of a quest and its progress.</summary>
    /// <param name="builder">The projection being built.</param>
    /// <returns>The row's node.</returns>
    internal uint Write(UiValueBuilder builder) =>
        builder.Object(
            ("id", builder.String(Id)),
            ("label", builder.String(Label)),
            ("count", builder.Number(Count)),
            ("required", builder.Number(Required)),
            ("met", builder.Boolean(Met)));
}

/// <summary>One quest the party stands with, as the journal shows it.</summary>
/// <remarks>
/// The state word is the reading's own, so a screen never works out whether an errand is finished: the
/// objectives, their progress, and whether the party may hand it in are all copied from the one reading the
/// owner composes against the owners that report them.
/// </remarks>
/// <param name="Quest">The quest's identity, which a screen names when it asks for nothing.</param>
/// <param name="Name">What a person calls the quest.</param>
/// <param name="State">How it stands: <c>offered</c>, <c>accepted</c>, <c>completed</c>, or <c>turned-in</c>.</param>
/// <param name="Giver">The person who gives it, as the world names them.</param>
/// <param name="Note">What the game tells the party about it, empty when it tells nothing.</param>
/// <param name="Residue">What the errand asks for that this game does not judge, empty when it judges all of it.</param>
/// <param name="Objectives">What it asks, in the order the quest states it.</param>
/// <param name="CanTurnIn">Whether the party may finish it with its giver right now.</param>
public sealed record QuestJournalSnapshot(
    string Quest,
    string Name,
    string State,
    string Giver,
    string Note,
    string Residue,
    IReadOnlyList<QuestObjectiveSnapshot> Objectives,
    bool CanTurnIn)
{
    /// <summary>Writes one errand the party stands with.</summary>
    /// <param name="builder">The projection being built.</param>
    /// <returns>The row's node.</returns>
    internal uint Write(UiValueBuilder builder) =>
        builder.Object(
            ("quest", builder.String(Quest)),
            ("name", builder.String(Name)),
            ("state", builder.String(State)),
            ("giver", builder.String(Giver)),
            ("note", builder.String(Note)),
            ("residue", builder.String(Residue)),
            ("objectives", builder.Array([.. Objectives.Select(objective => objective.Write(builder))])),
            ("canTurnIn", builder.Boolean(CanTurnIn)));
}

/// <summary>What the party's journal holds and what its last errand did, as the panel needs it.</summary>
/// <remarks>
/// <para>
/// Two facts and not one. <see cref="Journal"/> is every errand the party stands with — offered, taken,
/// finished, or handed in — each with what it asks and how much of it is done, which is what a screen shows
/// a player deciding what to do next. The report is the other fact: what the last offer, acceptance, or
/// turn-in did, what it paid, and why a refusal refused. A panel that only showed the journal would leave a
/// refused turn-in invisible, and one that only showed the report would leave the player guessing what they
/// have taken on.
/// </para>
/// <para>
/// <b>A session whose ruleset stated no quests is a different fact from a party that has taken none.</b> The
/// first has no journal at all and says so; the second has an owner with nothing in it, which is what a
/// party that has been offered nothing looks like.
/// </para>
/// </remarks>
/// <param name="Available">Whether the session holds a quest owner at all.</param>
/// <param name="Journal">Every quest the party stands with, in the order they were first recorded.</param>
/// <param name="Action">What the last errand operation was: <c>offer</c>, <c>accept</c>, <c>turn-in</c>, or <c>none</c>.</param>
/// <param name="Outcome">What it did: <c>none</c>, <c>applied</c>, or <c>refused</c>.</param>
/// <param name="Quest">The identity of the quest the last operation named, empty before any.</param>
/// <param name="Experience">How much experience the last turn-in was worth, zero when nothing was paid.</param>
/// <param name="Coins">How much coin the last turn-in paid, zero when nothing was paid.</param>
/// <param name="Items">What the last turn-in handed the party, one line per reward.</param>
/// <param name="Records">What the last turn-in left on the party, in the order it was applied.</param>
/// <param name="Delivered">What the last turn-in took back, one line per delivery.</param>
/// <param name="Code">The last refusal's code, empty when the last operation applied or none has happened.</param>
/// <param name="Message">What the last operation reported, empty before anything has happened.</param>
public sealed record QuestSnapshot(
    bool Available,
    IReadOnlyList<QuestJournalSnapshot> Journal,
    string Action,
    string Outcome,
    string Quest,
    long Experience,
    int Coins,
    IReadOnlyList<string> Items,
    IReadOnlyList<string> Records,
    IReadOnlyList<string> Delivered,
    string Code,
    string Message)
{
    /// <summary>No quest owner: the session's ruleset stated no quests, so there is no journal.</summary>
    public static QuestSnapshot None => new(
        Available: false,
        Journal: [],
        Action: "none",
        Outcome: "none",
        Quest: string.Empty,
        Experience: 0,
        Coins: 0,
        Items: [],
        Records: [],
        Delivered: [],
        Code: string.Empty,
        Message: string.Empty);

    /// <summary>Reads the quest owner into the panel's own value.</summary>
    /// <param name="quests">The owner, or null when the session holds none.</param>
    /// <returns>What the journal holds today, and what the last errand did.</returns>
    public static QuestSnapshot From(PartyQuests? quests)
    {
        if (quests is null) return None;

        List<QuestJournalSnapshot> journal = [];
        foreach (QuestReading reading in quests.Journal)
        {
            List<QuestObjectiveSnapshot> objectives = [];
            foreach (QuestObjectiveReading objective in reading.Objectives)
            {
                objectives.Add(new QuestObjectiveSnapshot(
                    objective.Objective.Id,
                    objective.Objective.Label,
                    objective.Count,
                    objective.Objective.Count,
                    objective.IsMet));
            }

            journal.Add(new QuestJournalSnapshot(
                reading.Quest.Value,
                reading.Name,
                reading.State,
                reading.Definition.Giver,
                reading.Definition.Note,
                reading.Definition.Residue,
                objectives,
                reading.CanTurnIn));
        }

        QuestResult? last = quests.Last;
        if (last is null)
        {
            return None with { Available = true, Journal = journal };
        }

        List<string> items = [];
        foreach (QuestRewardItem item in last.Payment.Items) items.Add($"{item.Count} × {item.Item}");

        List<string> records = [];
        foreach (QuestRewardRecord record in last.Payment.Records) records.Add($"{record.Record} ({record.Amount})");

        List<string> delivered = [];
        foreach (QuestRewardItem item in last.Payment.Delivered) delivered.Add($"{item.Count} × {item.Item}");

        return new QuestSnapshot(
            Available: true,
            Journal: journal,
            Action: last.Action switch
            {
                QuestAction.Offer => "offer",
                QuestAction.Accept => "accept",
                _ => "turn-in",
            },
            Outcome: last.IsApplied ? "applied" : "refused",
            Quest: last.Quest.Value,
            Experience: last.Payment.Experience?.Amount ?? 0,
            Coins: last.Payment.Coins,
            Items: items,
            Records: records,
            Delivered: delivered,
            Code: last.Refusal?.Code ?? string.Empty,
            Message: last.Describe());
    }

    /// <summary>Writes the quest block: every errand the party stands with, and what the last one did.</summary>
    /// <remarks>
    /// Every list is sent whole so the screen decides nothing: the journal with each quest's own objectives
    /// and their progress, what a turn-in paid, and the words of a refusal.
    /// </remarks>
    /// <param name="builder">The projection being built.</param>
    /// <returns>The block's node.</returns>
    internal uint Write(UiValueBuilder builder) =>
        builder.Object(
            ("available", builder.Boolean(Available)),
            ("journal", builder.Array([.. Journal.Select(quest => quest.Write(builder))])),
            ("action", builder.String(Action)),
            ("outcome", builder.String(Outcome)),
            ("quest", builder.String(Quest)),
            ("experience", builder.Number(Experience)),
            ("coins", builder.Number(Coins)),
            ("items", builder.Array([.. Items.Select(builder.String)])),
            ("records", builder.Array([.. Records.Select(builder.String)])),
            ("delivered", builder.Array([.. Delivered.Select(builder.String)])),
            ("code", builder.String(Code)),
            ("message", builder.String(Message)));
}
