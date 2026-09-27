using System.Text.Json.Serialization;

namespace PartyRpg.Kit.Quests;

/// <summary>How far along one party is with one quest.</summary>
/// <remarks>
/// <para>
/// Three stages, and the fourth word a journal shows — <c>completed</c> — is a reading rather than a stage:
/// a quest whose objectives are all met is one that may be turned in, and nothing has to write that down.
/// Keeping it out of the recorded state is what lets a read never change anything, which is exactly what a
/// projection and a panel need.
/// </para>
/// <para>
/// A stage only ever moves forward, and each move is one operation of the owner: an offer records the
/// errand the party was told about, taking it accepts it, and a turn-in ends it. Nothing else writes here.
/// </para>
/// </remarks>
public enum QuestStage
{
    /// <summary>The party has heard the errand and has not agreed to it.</summary>
    Offered,

    /// <summary>The party has taken the errand and is working on it.</summary>
    Accepted,

    /// <summary>The errand is finished and paid for, which is the end of an instance.</summary>
    TurnedIn,
}

/// <summary>What one objective's recorded progress stands at, by the objective's own identity.</summary>
/// <remarks>
/// <para>
/// <b>Only what cannot be read elsewhere is recorded.</b> A death the fight reports and a place the world
/// reports are moments rather than states, so the owner records them here; something the party carries, a
/// record it holds, and a person it has met are states another owner already holds, so those objectives
/// are read from that owner at the moment they are asked about and nothing is copied.
/// </para>
/// <para>
/// The count is a floor rather than a total: it never falls, so a death that happened while the errand was
/// taken stays counted even after the body is gone, and a place the party has stood in stays reached after
/// it walks out.
/// </para>
/// </remarks>
/// <param name="Objective">The objective's identity within its quest.</param>
/// <param name="Count">How much of it the party has done, which cannot be negative.</param>
/// <exception cref="ArgumentException">The objective's identity is blank.</exception>
/// <exception cref="ArgumentOutOfRangeException">The count is negative.</exception>
public readonly record struct QuestProgress
{
    /// <summary>Records progress on one objective.</summary>
    /// <param name="objective">The objective's identity within its quest.</param>
    /// <param name="count">How much of it the party has done.</param>
    /// <exception cref="ArgumentException">The objective's identity is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The count is negative.</exception>
    [JsonConstructor]
    public QuestProgress(string objective, int count)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objective);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        Objective = objective;
        Count = count;
    }

    /// <summary>The objective's identity within its quest.</summary>
    public string Objective { get; }

    /// <summary>How much of it the party has done.</summary>
    public int Count { get; }
}

/// <summary>One party's own state about one quest: the stage it stands at and what it has recorded.</summary>
/// <remarks>
/// <para>
/// <b>An instance belongs to the party and not to a place.</b> A quest spans places by nature — it is
/// offered in one, asks for deeds in others, and is turned in wherever the giver stands — so nothing here
/// is keyed by where the party is, and walking through a door changes no instance. The place an offer was
/// taken in is recorded as provenance, which is what a journal shows and what a load checks against the
/// world; it is never a lookup.
/// </para>
/// <para>
/// <b>What the instance records is what a save carries.</b> The stage, the progress, and the provenance are
/// the whole of it; the definition is re-read from the game's own content when it is needed, so a save
/// holds a party's history rather than a copy of the rules it was played under.
/// </para>
/// </remarks>
/// <param name="Quest">The quest this is an instance of.</param>
/// <param name="Stage">How far the party has got with it.</param>
/// <param name="Giver">The identity of whoever offered it, as the world names them.</param>
/// <param name="OfferedIn">The place the offer was taken in, or empty when it was offered nowhere in particular.</param>
/// <param name="Progress">What has been recorded against its objectives, in the order it was recorded.</param>
public sealed record QuestInstance(
    QuestId Quest,
    QuestStage Stage,
    string Giver,
    string OfferedIn = "",
    IReadOnlyList<QuestProgress>? Progress = null)
{
    /// <summary>The quest this is an instance of.</summary>
    public QuestId Quest { get; init; } = Quest;

    /// <summary>How far the party has got with it.</summary>
    public QuestStage Stage { get; init; } = Stage;

    /// <summary>The identity of whoever offered it.</summary>
    public string Giver { get; init; } = !string.IsNullOrWhiteSpace(Giver)
        ? Giver
        : throw new ArgumentException(
            $"The instance of '{Quest}' records no giver, so nothing says who offered the errand or who it is finished with.",
            nameof(Giver));

    /// <summary>The place the offer was taken in, or empty when it was offered nowhere in particular.</summary>
    public string OfferedIn { get; init; } = OfferedIn ?? string.Empty;

    /// <summary>What has been recorded against its objectives, in the order it was recorded.</summary>
    public IReadOnlyList<QuestProgress> Progress { get; init; } = Progress ?? [];

    /// <summary>How much of one objective the party has recorded, or zero when nothing has been recorded.</summary>
    /// <param name="objective">The objective's identity within this quest.</param>
    /// <returns>The recorded count.</returns>
    public int Recorded(string objective)
    {
        if (string.IsNullOrEmpty(objective)) return 0;
        foreach (QuestProgress progress in Progress)
        {
            if (string.Equals(progress.Objective, objective, StringComparison.Ordinal)) return progress.Count;
        }

        return 0;
    }

    /// <summary>A copy of this instance with more progress recorded against one objective.</summary>
    /// <remarks>
    /// Progress never falls, so a count below what is already recorded leaves the instance exactly as it
    /// was: a death counted while the errand was taken stays counted.
    /// </remarks>
    /// <param name="objective">The objective's identity within this quest.</param>
    /// <param name="count">How much of it the party has done, which cannot be negative.</param>
    /// <returns>The instance with the progress recorded.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The count is negative.</exception>
    public QuestInstance Record(string objective, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        int held = Recorded(objective);
        if (count <= held) return this;
        List<QuestProgress> progress = [.. Progress];
        for (int index = 0; index < progress.Count; index++)
        {
            if (string.Equals(progress[index].Objective, objective, StringComparison.Ordinal))
            {
                progress[index] = new QuestProgress(objective, count);
                return this with { Progress = progress };
            }
        }

        progress.Add(new QuestProgress(objective, count));
        return this with { Progress = progress };
    }

    /// <inheritdoc />
    public override string ToString() => $"{Quest} ({Stage})";
}

/// <summary>One instance as a save records it, under the product's one current schema.</summary>
/// <remarks>
/// The definition is deliberately absent: a save records what the party has done, and the quest it was done
/// about is re-read from the game's own content when the save is loaded, so a document never carries a copy
/// of the rules it was written under.
/// </remarks>
/// <param name="Quest">The quest the instance is of.</param>
/// <param name="Stage">How far the party had got with it, as the wire spells it.</param>
/// <param name="Giver">The identity of whoever offered it.</param>
/// <param name="OfferedIn">The place the offer was taken in, or empty.</param>
/// <param name="Progress">What had been recorded against its objectives.</param>
public sealed record QuestInstanceSave(
    QuestId Quest,
    string Stage,
    string Giver,
    string OfferedIn = "",
    IReadOnlyList<QuestProgress>? Progress = null)
{
    /// <summary>What had been recorded against its objectives.</summary>
    public IReadOnlyList<QuestProgress> Progress { get; init; } = Progress ?? [];

    /// <summary>Reads the instance a save recorded, judging the stage word it carries.</summary>
    /// <param name="instance">The instance to read.</param>
    /// <returns>The live instance.</returns>
    /// <exception cref="ArgumentNullException">No instance was supplied.</exception>
    /// <exception cref="ArgumentException">The stage word names no stage this build has.</exception>
    public static QuestInstance Read(QuestInstanceSave instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        QuestStage stage = instance.Stage switch
        {
            "offered" => QuestStage.Offered,
            "accepted" => QuestStage.Accepted,
            "turned-in" => QuestStage.TurnedIn,
            _ => throw new ArgumentException(
                $"The quest '{instance.Quest}' is recorded at the stage '{instance.Stage}', which is not one this build has.",
                nameof(instance)),
        };

        return new QuestInstance(instance.Quest, stage, instance.Giver, instance.OfferedIn, instance.Progress);
    }

    /// <summary>Records an instance as a save writes it.</summary>
    /// <param name="instance">The instance to record.</param>
    /// <returns>The recorded instance.</returns>
    /// <exception cref="ArgumentNullException">No instance was supplied.</exception>
    public static QuestInstanceSave Record(QuestInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        string stage = instance.Stage switch
        {
            QuestStage.Offered => "offered",
            QuestStage.Accepted => "accepted",
            _ => "turned-in",
        };

        return new QuestInstanceSave(instance.Quest, stage, instance.Giver, instance.OfferedIn, instance.Progress);
    }
}

/// <summary>Every quest one party has a state about, as a save records them.</summary>
/// <remarks>
/// The section is a party's own history and nothing else: each instance names the quest it is of, how far
/// the party had got with it, and what had been recorded against its objectives. The definitions are
/// deliberately absent, because a save records what the party did rather than a copy of the rules it was
/// played under — so what an instance's quest is now means is read from the game's own content when the
/// document is loaded, and an instance whose quest the game no longer states is a contradiction the load
/// names rather than a quest it guesses at.
/// </remarks>
/// <param name="Instances">The instances, in the order they were first recorded.</param>
public sealed record QuestSave(IReadOnlyList<QuestInstanceSave>? Instances = null)
{
    /// <summary>The instances, in the order they were first recorded.</summary>
    public IReadOnlyList<QuestInstanceSave> Instances { get; init; } = Instances ?? [];

    /// <summary>A party with no quest state at all.</summary>
    public static QuestSave None { get; } = new();

    /// <summary>Whether this records nothing, which is what a party that has been offered nothing has.</summary>
    public bool IsEmpty => Instances.Count == 0;
}
