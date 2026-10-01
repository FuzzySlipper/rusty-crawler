using PartyRpg.Kit.Combat;
using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Progression;

/// <summary>
/// What a creature's death means for the party's experience: one award, through the progression owner.
/// </summary>
/// <remarks>
/// <para>
/// <b>A kill is one award source among several, and it arrives at the same entry as the rest.</b> What a
/// death is worth comes from the creature's own content — the ruleset reads its row and answers with a
/// number — and who takes a share of it is the progression rule's division, exactly as it is for a quest's
/// reward.
/// </para>
/// <para>
/// <b>A death is reported once, so it pays once.</b> The fight tells this about a death at the blow that
/// caused it and never again, so nothing here remembers which deaths were paid for, and a death worth nothing
/// pays nothing. It is still news: a death worth no experience is told to the world as a deed under the word it
/// is credited as (<see cref="PartyProgression.Deed"/>), so a game that counts a worthless person's death against
/// the party answers it the same way it answers a worthy one.
/// </para>
/// <para>
/// <b>Why the owner is read through a call rather than held.</b> A session composes its fight policy before
/// it knows which party it will play: a product that creates its party has none until the player accepts
/// one. The owner is asked when a death is reported, when the party it was composed over certainly exists,
/// and a session with no owner at all awards nothing rather than inventing one.
/// </para>
/// <para>
/// <b>What a death is credited as is the game's word, not always "kill".</b> The world does not hear every
/// death alike: a creature brought down in the wild and a person struck down where they live are both awards
/// of experience and different news. A game that tells them apart names the source a death is credited
/// under, and the standing rule reads that word as it reads any other award's; a game that names none credits
/// every death as <see cref="KillSource"/>.
/// </para>
/// </remarks>
public sealed class ProgressionAwards : ICreatureDeathObserver
{
    private readonly Func<PlacementDefinition, long> _worth;
    private readonly Func<PartyProgression?> _progression;
    private readonly Func<CreatureDeath, string>? _source;

    /// <summary>Creates the award path for deaths.</summary>
    /// <param name="worth">What a creature's placement is worth when it dies, which is the ruleset's reading of its row.</param>
    /// <param name="progression">The progression owner, read when a death is reported.</param>
    /// <param name="source">
    /// What a death is credited as, which is the game's own word for it; every death is credited as
    /// <see cref="KillSource"/> when the game names none.
    /// </param>
    public ProgressionAwards(
        Func<PlacementDefinition, long> worth,
        Func<PartyProgression?> progression,
        Func<CreatureDeath, string>? source = null)
    {
        _worth = worth ?? throw new ArgumentNullException(nameof(worth));
        _progression = progression ?? throw new ArgumentNullException(nameof(progression));
        _source = source;
    }

    /// <summary>The award source a kill is credited under, so a panel and a test can tell a kill from a quest.</summary>
    public const string KillSource = "kill";

    /// <inheritdoc />
    public void Died(CreatureDeath death)
    {
        ArgumentNullException.ThrowIfNull(death);
        if (_progression() is not { } progression) return;
        long worth = _worth(death.Placement);
        string source = _source?.Invoke(death) ?? KillSource;
        if (worth > 0) progression.Award(new PartyExperienceAward(source, worth));
        else progression.Deed(source);
    }
}
