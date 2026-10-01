using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Progression;
using PartyRpg.Kit.World;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// What this game does about a peaceful person the party kills: the deed the world hears of, and, for a
/// townsperson, the fine it takes.
/// </summary>
/// <remarks>
/// <para>
/// <b>The deed is an award like every other, credited under its own word.</b> The donor pays a peasant's
/// death exactly as it pays a creature's — the row's experience, through the one award path
/// (OpenEnroth <c>src/Engine/Objects/Actor.cpp:3162-3167</c>) — and beside it charges the fine and moves the
/// location's reputation one point the wrong way (<c>src/Engine/Objects/Actor.cpp:1083-1105</c>,
/// <c>Actor::ApplyFineForKillingPeasant</c>; the location's number is sign-flipped, <c>LocationInfo.h:7</c>,
/// so its <c>reputation++</c> is our one point down). This game keeps the award and credits it as
/// <see cref="TownspersonKillSource"/> rather than as an ordinary kill, so the experience lands as it would
/// and <see cref="MightAndMagic7Standing.ReputationFor"/> reads the word and answers the fall: one point,
/// through <see cref="PartyProgression.Award"/>, the one entry every deed reaches the world's opinion by.
/// </para>
/// <para>
/// <b>Any peaceful person's death is a deed against the party; only a townsperson's is fined.</b> The donor
/// moves reputation only beside a peasant's fine. This game widens the fall (ours): a person a place's own
/// records stand there — whatever row they fight as, a guard's or an adept's — is someone the town knows, so
/// their death is credited as <see cref="PersonKillSource"/> and lowers the world's opinion by the same one
/// point. The fine stays the donor's: only a peasant row is fined, as <c>IsPeasant</c> decides
/// (<c>src/Engine/Objects/MonsterEnumFunctions.h:48-54</c>). A creature the fight reads as peaceful for
/// another reason — the party unseen, or a band-zero creature that never starts a fight — is not a person,
/// and its death is an ordinary kill.
/// </para>
/// <para>
/// <b>The fine is the donor's arithmetic, carried as the donor carries it.</b> The donor adds
/// <c>100 × (the map's base fine + the peasant's level + the party's reputation in its own sign)</c> to the
/// party's fine, clamped to <c>0..4,000,000</c> (<c>Actor.cpp:1093-1099</c>). This game reads the same sum
/// with the level from the row the person fights as, the reputation turned into the donor's sign, and the
/// place's base fine — the map table's "Steal Perm" column (<c>src/Engine/Tables/MapTable.cpp:73</c>), which
/// the importer carries onto the place — so a party the town already dislikes pays more and a well-liked one
/// pays less, down to nothing. The fine is not taken from the purse: it is added to what the party owes on
/// <see cref="MightAndMagic7Theft.FineAccount"/>, the one account a caught thief's fine is owed on too, and a
/// town hall collects it (<c>src/GUI/UI/Houses/TownHall.cpp:30-45</c>), so a purse too light to pay leaves the
/// party owing rather than forgiven. What is ours:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// The donor's throne room clears a fine by a year in jail (<c>src/GUI/UI/UIHouses.cpp:346-351</c>); this
/// build has no throne room, so a fine is only ever paid off.
/// </description>
/// </item>
/// <item>
/// <description>
/// The donor's two exemptions — a dark party in the light's two lands and a light party in the dark's
/// (<c>Actor.cpp:1087-1091</c>) — are not read: every townsperson's death is a crime here.
/// </description>
/// </item>
/// </list>
/// <para>
/// <b>The fine reads the standing before the deed moves it</b>, as the donor's own order does, which is why
/// the session tells this observer about a death before it tells the award path.
/// </para>
/// </remarks>
internal sealed class MightAndMagic7Crimes : ICreatureDeathObserver
{
    /// <summary>The source a townsperson's death is credited under, which is what the standing rule reads.</summary>
    internal const string TownspersonKillSource = "townsperson-kill";

    /// <summary>The source any other peaceful person's death is credited under: a deed with no fine beside it.</summary>
    internal const string PersonKillSource = "person-kill";

    /// <summary>How far one peaceful person's death moves the world's opinion, which is the donor's one point.</summary>
    internal const int PersonKillReputation = -1;

    /// <summary>How many gold one point of the donor's fine sum is worth, which is the donor's own figure.</summary>
    internal const int FinePerPoint = 100;

    /// <summary>The most the donor lets a fine stand at, which bounds one death's fine here.</summary>
    internal const int FineCeiling = 4_000_000;

    private readonly Func<PlacementDefinition, int?> _townsperson;
    private readonly Func<PlacementDefinition, bool> _person;
    private readonly Func<PartyEntity?> _party;
    private readonly Func<PlaceId, int> _baseFine;

    /// <summary>Composes the crime path over the fight's reading of a placement and the party's own debts.</summary>
    /// <param name="townsperson">The level of the townsperson a placement holds, or null when it is not one.</param>
    /// <param name="person">Whether a placement holds a peaceful person the place's own records stand there.</param>
    /// <param name="party">The party, read when a death is reported, because a session may create it later.</param>
    /// <param name="baseFine">A place's base fine, the map table's own column as the place carries it.</param>
    internal MightAndMagic7Crimes(
        Func<PlacementDefinition, int?> townsperson,
        Func<PlacementDefinition, bool> person,
        Func<PartyEntity?> party,
        Func<PlaceId, int> baseFine)
    {
        _townsperson = townsperson ?? throw new ArgumentNullException(nameof(townsperson));
        _person = person ?? throw new ArgumentNullException(nameof(person));
        _party = party ?? throw new ArgumentNullException(nameof(party));
        _baseFine = baseFine ?? throw new ArgumentNullException(nameof(baseFine));
    }

    /// <summary>
    /// What a death is credited as: a townsperson's and any other peaceful person's under their own words,
    /// anything else as a kill.
    /// </summary>
    /// <param name="death">The death the fight reported.</param>
    /// <returns>The award source.</returns>
    /// <exception cref="ArgumentNullException">No death was supplied.</exception>
    internal string SourceOf(CreatureDeath death)
    {
        ArgumentNullException.ThrowIfNull(death);
        if (_townsperson(death.Placement) is not null) return TownspersonKillSource;
        return _person(death.Placement) ? PersonKillSource : ProgressionAwards.KillSource;
    }

    /// <summary>The donor's fine for one townsperson's death.</summary>
    /// <param name="level">The townsperson's row level.</param>
    /// <param name="reputation">The party's reputation in this game's sign, in which higher is better.</param>
    /// <param name="baseFine">The place's base fine, the map table's own column.</param>
    /// <returns>The fine in gold, never below zero and never above the donor's ceiling.</returns>
    internal static int FineFor(int level, int reputation, int baseFine = 0)
    {
        long points = (long)baseFine + level - reputation;
        return (int)Math.Clamp(points * FinePerPoint, 0, FineCeiling);
    }

    /// <inheritdoc />
    public void Died(CreatureDeath death)
    {
        ArgumentNullException.ThrowIfNull(death);
        if (_townsperson(death.Placement) is not { } level) return;
        if (_party() is not { } party) return;
        int owed = party.Debts.OwedOn(MightAndMagic7Theft.FineAccount);
        int added = MightAndMagic7Theft.Added(owed, FineFor(level, party.Reputation.Reputation, _baseFine(death.Place)));

        // The fine is owed rather than taken: the whole is kept between nothing and the donor's ceiling, and a
        // town hall is where it is paid.
        if (added > 0) party.Debts.Owe(MightAndMagic7Theft.FineAccount, owed + added);
    }
}
