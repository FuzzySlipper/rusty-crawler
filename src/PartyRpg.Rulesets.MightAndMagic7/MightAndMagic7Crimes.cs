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
/// <b>The fine is the donor's arithmetic, approximated in three stated ways.</b> The donor adds
/// <c>100 × (the map's base fine + the peasant's level + the party's reputation in its own sign)</c> to the
/// party's fine, clamped to <c>0..4,000,000</c> (<c>Actor.cpp:1093-1099</c>). This game reads the same sum
/// with the level from the row the person fights as and the reputation turned into the donor's sign, so a
/// party the town already dislikes pays more and a well-liked one pays less, down to nothing. What is ours:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// The map's base fine (the map table's "Steal Perm" column, <c>src/Engine/Tables/MapTable.cpp:73</c>) is
/// read as zero everywhere: the importer does not carry the column onto a place. Zero is Emerald Isle's own
/// value, so the tutorial island fines exactly as the donor does; the other places' one hundred to fifteen
/// hundred gold more per death are not charged.
/// </description>
/// </item>
/// <item>
/// <description>
/// The fine is taken from the purse at the moment of the death, as much of it as the purse holds, through
/// the party's one ledger. The donor carries it as a debt instead — paid at a town hall
/// (<c>src/GUI/UI/Houses/TownHall.cpp:30-45</c>) or cleared by the throne room's jail
/// (<c>src/GUI/UI/UIHouses.cpp:346-351</c>) — and this build has no debt on the party, so what the purse
/// cannot cover is not owed afterwards.
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
    private readonly Func<PartyResourceLedger?> _accounts;

    /// <summary>Composes the crime path over the fight's reading of a placement and the party's own accounts.</summary>
    /// <param name="townsperson">The level of the townsperson a placement holds, or null when it is not one.</param>
    /// <param name="person">Whether a placement holds a peaceful person the place's own records stand there.</param>
    /// <param name="party">The party, read when a death is reported, because a session may create it later.</param>
    /// <param name="accounts">The party's one ledger, read when a death is reported for the same reason.</param>
    internal MightAndMagic7Crimes(
        Func<PlacementDefinition, int?> townsperson,
        Func<PlacementDefinition, bool> person,
        Func<PartyEntity?> party,
        Func<PartyResourceLedger?> accounts)
    {
        _townsperson = townsperson ?? throw new ArgumentNullException(nameof(townsperson));
        _person = person ?? throw new ArgumentNullException(nameof(person));
        _party = party ?? throw new ArgumentNullException(nameof(party));
        _accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
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

    /// <summary>The donor's fine for one townsperson's death, with the place's base read as zero.</summary>
    /// <param name="level">The townsperson's row level.</param>
    /// <param name="reputation">The party's reputation in this game's sign, in which higher is better.</param>
    /// <returns>The fine in gold, never below zero and never above the donor's ceiling.</returns>
    internal static int FineFor(int level, int reputation)
    {
        long points = (long)level - reputation;
        return (int)Math.Clamp(points * FinePerPoint, 0, FineCeiling);
    }

    /// <inheritdoc />
    public void Died(CreatureDeath death)
    {
        ArgumentNullException.ThrowIfNull(death);
        if (_townsperson(death.Placement) is not { } level) return;
        if (_party() is not { } party || _accounts() is not { } accounts) return;
        int taken = Math.Min(FineFor(level, party.Reputation.Reputation), party.Purse.Coins);

        // What is taken is what the purse holds up to the fine, so the settlement is judged whole and cannot
        // refuse; a purse with nothing in it pays nothing, which is the stated approximation of a debt.
        if (taken > 0) accounts.Settle(PartyCost.OfGold(taken));
    }
}
