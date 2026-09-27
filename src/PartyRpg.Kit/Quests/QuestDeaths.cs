using PartyRpg.Kit.Combat;
using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Quests;

/// <summary>
/// What a fight's reading of its fallen means for the quests the party has taken: one report per death, read
/// against every kill objective an instance still wants.
/// </summary>
/// <remarks>
/// <para>
/// <b>A death is one source among several, and it reaches the owner through the fight's own report.</b> The
/// fight publishes what it read as down and whoever keeps the bodies turns that into bodies; this sits
/// between the two, so what a quest counts is the same death the party's experience is paid for, arriving in
/// the same order, rather than a second reading of the place's population.
/// </para>
/// <para>
/// <b>Why the owner is read through a call rather than held.</b> A session composes its fight policy before
/// it knows which party it will play: a product that creates its party has none until the player accepts
/// one, and the fight is composed once for every path a session can take. The owner is therefore asked the
/// moment a death is reported, when the party it was composed over certainly exists, and a session with no
/// owner at all records nothing rather than inventing one — the same reason the award path reads its owner
/// through a call.
/// </para>
/// <para>
/// <b>This reports the bodies on unchanged.</b> What the fallen left is the bodies owner's business, and this
/// only reads what passed through it, so a quest can be composed over the fight without the corpse, loot, or
/// award paths being told about quests at all.
/// </para>
/// </remarks>
public sealed class QuestDeaths : IFallenCreatureObserver
{
    private readonly Func<PartyQuests?> _quests;
    private readonly IFallenCreatureObserver? _bodies;

    /// <summary>Creates the observer over whoever keeps the bodies and the owner that counts deaths.</summary>
    /// <param name="quests">The quest owner of the party being played, or null while there is none.</param>
    /// <param name="bodies">
    /// Whoever keeps what the fallen left. It is asked first because the serial and the placement of a body
    /// are its to give, and what a kill objective counts is read from exactly that.
    /// </param>
    /// <exception cref="ArgumentNullException">A reader is missing.</exception>
    public QuestDeaths(Func<PartyQuests?> quests, IFallenCreatureObserver? bodies = null)
    {
        _quests = quests ?? throw new ArgumentNullException(nameof(quests));
        _bodies = bodies;
    }

    /// <inheritdoc />
    /// <remarks>
    /// A session with no quest owner records nothing and still reports the bodies, which is the honest state
    /// of a product whose party has taken no errands.
    /// </remarks>
    public IReadOnlyList<Corpse> Observe(PlaceId place, IReadOnlyList<FallenCreature> fallen)
    {
        IReadOnlyList<Corpse> bodies = _bodies?.Observe(place, fallen) ?? [];
        _quests()?.ObserveDeaths(place, bodies);
        return bodies;
    }
}
