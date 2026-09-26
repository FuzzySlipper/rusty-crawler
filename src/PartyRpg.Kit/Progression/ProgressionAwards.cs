using PartyRpg.Kit.Combat;
using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Progression;

/// <summary>
/// What a fight's reading of its fallen means for the party's experience: one award per death, through the
/// progression owner.
/// </summary>
/// <remarks>
/// <para>
/// <b>A kill is one award source among several, and it arrives at the same entry as the rest.</b> What a
/// death is worth comes from the creature's own content — the ruleset reads its row and answers with a
/// number — and who takes a share of it is the progression rule's division, exactly as it is for a quest's
/// reward. This type therefore owns nothing but the one thing a death needs and an award does not: that a
/// death read twice is paid once.
/// </para>
/// <para>
/// <b>One death pays once, and the fight's own reading is what says a death happened.</b> A fight re-reads
/// the place every update and reports every creature it read as down, so the reading is the state of a
/// place rather than an event that can be counted. Each death is named by the serial the bodies owner gave
/// it, and what this keeps is the set of serials already paid for, pruned to the deaths still lying there.
/// That is a ledger of what has been paid rather than a kill count: nothing here is ever read as "how many
/// kills", nothing here decides that a creature is down, and a creature the fight no longer reads as down
/// is forgotten along with the body that named it. A counter incremented per kill would be exactly the
/// second source of truth about a death that the fight's own state already is.
/// </para>
/// <para>
/// <b>A death worth nothing pays nothing.</b> A row worth no experience — a creature the content values at
/// nothing, a placement no row describes — is marked paid all the same, so a death worth nothing does not
/// become a death that pays on every update.
/// </para>
/// <para>
/// <b>Why the owner is read through a call rather than held.</b> A session composes its fight policy before
/// it knows which party it will play: a product that creates its party has none until the player accepts
/// one, and the fight is composed once for every path a session can take. The owner is therefore asked the
/// moment a death is read, when the party it was composed over certainly exists, and a session with no
/// owner at all awards nothing rather than inventing one.
/// </para>
/// </remarks>
public sealed class ProgressionAwards : IFallenCreatureObserver
{
    private readonly IFallenCreatureObserver? _bodies;
    private readonly Func<PlacementDefinition, long> _worth;
    private readonly Func<PartyProgression?> _progression;
    private readonly HashSet<long> _paid = [];

    /// <summary>Creates the award path over the bodies a fight reports and the owner that receives awards.</summary>
    /// <param name="worth">What one placement's death is worth in experience, which the ruleset reads from content.</param>
    /// <param name="progression">The progression owner of the party being played, or null while there is none.</param>
    /// <param name="bodies">
    /// Whoever keeps what the fallen left, or null when nothing does. It is asked first because the serial
    /// that names a death is its to give.
    /// </param>
    /// <exception cref="ArgumentNullException">A reader is missing.</exception>
    public ProgressionAwards(
        Func<PlacementDefinition, long> worth,
        Func<PartyProgression?> progression,
        IFallenCreatureObserver? bodies = null)
    {
        _worth = worth ?? throw new ArgumentNullException(nameof(worth));
        _progression = progression ?? throw new ArgumentNullException(nameof(progression));
        _bodies = bodies;
    }

    /// <summary>How many deaths this has paid for, which is the deaths still lying in the place it read.</summary>
    /// <remarks>
    /// It is a reading rather than a total: the ledger is pruned to the fight's own last report, so this
    /// never grows with the session and is never the source of anything.
    /// </remarks>
    public int PaidDeaths => _paid.Count;

    /// <inheritdoc />
    /// <remarks>
    /// A session with no progression owner reports the deaths and awards nothing, which is the honest state
    /// of a product with nobody to pay.
    /// </remarks>
    public IReadOnlyList<Corpse> Observe(PlaceId place, IReadOnlyList<FallenCreature> fallen)
    {
        IReadOnlyList<Corpse> bodies = _bodies?.Observe(place, fallen) ?? [];
        if (_progression() is not { } progression) return bodies;

        // A death the fight no longer reads as down is gone with the body that named it, so what was paid
        // for it is forgotten here: the ledger is exactly the deaths lying in the place the fight read.
        _paid.RemoveWhere(serial => !Lies(bodies, serial));

        foreach (Corpse body in bodies)
        {
            // A serial this has already paid for is a death that was read on an earlier update; adding it
            // is what makes the second reading of one death nothing to pay.
            if (!_paid.Add(body.Serial)) continue;
            long worth = _worth(body.Body);
            if (worth <= 0) continue;
            progression.Award(new PartyExperienceAward(KillSource, worth));
        }

        return bodies;
    }

    /// <summary>What this names as the source of an award a death earned.</summary>
    public const string KillSource = "kill";

    /// <summary>Whether one of the bodies lying in the place is the death a serial names.</summary>
    private static bool Lies(IReadOnlyList<Corpse> bodies, long serial)
    {
        foreach (Corpse body in bodies)
        {
            if (body.Serial == serial) return true;
        }

        return false;
    }
}
