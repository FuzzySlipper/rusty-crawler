using PartyRpg.Kit.Combat;

namespace PartyRpg.Kit.Quests;

/// <summary>
/// What a creature's death means for the quests the party has taken: one report per death, read against every
/// kill objective an instance still wants.
/// </summary>
/// <remarks>
/// <para>
/// <b>A death is reported once, where it happens.</b> What a quest counts is the same death the party's
/// experience is paid for and a body is laid for, told to each of them by the fight at the blow that caused
/// it — so a kill is counted once, however long the body lies there afterwards.
/// </para>
/// <para>
/// <b>Why the owner is read through a call rather than held.</b> A session composes its fight policy before
/// it knows which party it will play, so the quest owner is asked when a death is reported, when the party it
/// was composed over certainly exists; a session with no owner records nothing rather than inventing one.
/// </para>
/// </remarks>
public sealed class QuestDeaths : ICreatureDeathObserver
{
    private readonly Func<PartyQuests?> _quests;

    /// <summary>Creates the quest path for deaths.</summary>
    /// <param name="quests">The quest owner, read when a death is reported.</param>
    public QuestDeaths(Func<PartyQuests?> quests)
    {
        _quests = quests ?? throw new ArgumentNullException(nameof(quests));
    }

    /// <inheritdoc />
    public void Died(CreatureDeath death)
    {
        ArgumentNullException.ThrowIfNull(death);
        _quests()?.ObserveDeath(death);
    }
}
