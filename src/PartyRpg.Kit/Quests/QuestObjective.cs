namespace PartyRpg.Kit.Quests;

/// <summary>What kind of thing a quest objective asks the party to do, as the workflow's vocabulary.</summary>
/// <remarks>
/// <para>
/// Six kinds, and each one reads a fact some existing owner already reports: a creature's death comes from
/// the fight, something carried comes from the party's one inventory, a place comes from the world, a person
/// comes from the conversation that records having met them, and a flag is a record the party carries. A
/// kind nobody reports would be a vocabulary of guesses, so the list holds nothing this build cannot judge.
/// </para>
/// <para>
/// The kinds are deliberately few and deliberately closed: an objective is a named thing of one of these
/// shapes and a count, so a game states its quests as data and this mechanism judges them without a branch
/// per quest. Which identity a kind's target names is content's or the ruleset's — a monster, an item, a
/// place, a person, a record — and this mechanism only compares what it is told with what it was given.
/// </para>
/// </remarks>
public enum QuestObjectiveKind
{
    /// <summary>A creature the party must bring down, by the placement's own content identity and a count.</summary>
    Kill,

    /// <summary>Something the party must carry, by the item definition's own identity and a count.</summary>
    Retrieve,

    /// <summary>A place the party must stand in, by the place's own content identity.</summary>
    Reach,

    /// <summary>A person the party must have spoken with, by the record the conversation leaves of meeting them.</summary>
    Talk,

    /// <summary>Something the party must carry to somebody, by an item's identity and the record of the person.</summary>
    Deliver,

    /// <summary>Something the party must have done or learned, recorded as a party-carried flag and a magnitude.</summary>
    Flag,
}

/// <summary>One thing a quest asks the party to do: a kind, what it is about, and how much of it.</summary>
/// <remarks>
/// <para>
/// <b>The target is an identity and never a resolved rule.</b> <see cref="Target"/> names whatever the
/// owner of its kind resolves — a creature placement, an item definition, a place, the record a
/// conversation leaves of a person, or a flag — and this type carries it unchanged. The label is what a
/// person reads, because a journal that listed a definition's number would leave a player unable to tell
/// what the errand wants.
/// </para>
/// <para>
/// <b>An amount is meaningful per kind.</b> A kill's amount is how many creatures, a retrieve's how many
/// items, a deliver's how many are handed over, and a flag's the magnitude the record must have reached; a
/// reach or a talk asks for exactly one thing and states one. The amount is at least one, so an objective
/// that asks for nothing is inexpressible rather than quietly always met.
/// </para>
/// <para>
/// <b>Two kinds carry a second identity, and both are stated rather than inferred.</b>
/// <see cref="Place"/> scopes a kill to the place the deed must happen in, and <see cref="Person"/> names
/// the person a delivery goes to — the record the conversation leaves of meeting them, which is what makes
/// "carried to somebody" readable from the same owner the party's records already are.
/// </para>
/// </remarks>
/// <param name="Id">The objective's identity within its quest, which must not be blank and must be unique there.</param>
/// <param name="Kind">What kind of thing the objective asks for.</param>
/// <param name="Target">The identity the owner of that kind resolves, which must not be blank.</param>
/// <param name="Count">How much of it is asked for, which is at least one.</param>
/// <param name="Label">How it reads to a person, or empty to read as the target itself.</param>
/// <param name="Place">For a kill, the place the deed must happen in, or empty when anywhere will do.</param>
/// <param name="Person">For a delivery, the record the recipient is met under, or empty for the giver.</param>
/// <exception cref="ArgumentException">The identity, the target, or the label is blank where one is required.</exception>
/// <exception cref="ArgumentOutOfRangeException">The count is below one.</exception>
public sealed record QuestObjective
{
    /// <summary>Creates an objective.</summary>
    /// <param name="id">The objective's identity within its quest.</param>
    /// <param name="kind">What kind of thing the objective asks for.</param>
    /// <param name="target">The identity the owner of that kind resolves.</param>
    /// <param name="count">How much of it is asked for, which is at least one.</param>
    /// <param name="label">How it reads to a person, or empty to read as the target itself.</param>
    /// <param name="place">For a kill, the place the deed must happen in, or empty when anywhere will do.</param>
    /// <param name="person">For a delivery, the record the recipient is met under, or empty for the giver.</param>
    /// <exception cref="ArgumentException">The identity, the target, or the label is blank where one is required.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The count is below one.</exception>
    public QuestObjective(
        string id,
        QuestObjectiveKind kind,
        string target,
        int count = 1,
        string label = "",
        string place = "",
        string person = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);

        // A delivery is the one kind that names two things: what is carried and who receives it. Stating it
        // without the person would make "carried to somebody" unjudgeable, so the defect is caught where the
        // objective is written rather than read as a delivery that is satisfied by carrying alone.
        if (kind == QuestObjectiveKind.Deliver && string.IsNullOrWhiteSpace(person))
        {
            throw new ArgumentException(
                $"The delivery objective '{id}' names no person to hand '{target}' to, so whether it was carried there could never be judged.",
                nameof(person));
        }
        Id = id;
        Kind = kind;
        Target = target;
        Count = count;
        Label = string.IsNullOrWhiteSpace(label) ? target : label;
        Place = place ?? string.Empty;
        Person = person ?? string.Empty;
    }

    /// <summary>The objective's identity within its quest.</summary>
    public string Id { get; init; }

    /// <summary>What kind of thing the objective asks for.</summary>
    public QuestObjectiveKind Kind { get; init; }

    /// <summary>The identity the owner of that kind resolves.</summary>
    public string Target { get; init; }

    /// <summary>How much of it is asked for.</summary>
    public int Count { get; init; }

    /// <summary>How it reads to a person.</summary>
    public string Label { get; init; }

    /// <summary>For a kill, the place the deed must happen in, or empty when anywhere will do.</summary>
    public string Place { get; init; }

    /// <summary>For a delivery, the record the recipient is met under, or empty for the quest's giver.</summary>
    public string Person { get; init; }

    /// <summary>How this objective reads inside a list of what a quest asks for.</summary>
    /// <returns>The words a person reads.</returns>
    public override string ToString() => Count > 1 ? $"{Label} ({Count})" : Label;
}
