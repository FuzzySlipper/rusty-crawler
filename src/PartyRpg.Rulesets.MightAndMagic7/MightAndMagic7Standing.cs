using System.Globalization;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Progression;
using PartyRpg.Kit.Promotion;
using PartyRpg.Kit.Quests;
using PartyRpg.Kit.Services;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>One band of the world's opinion: what it is called, where it begins, and what it means.</summary>
/// <remarks>
/// The word and the edge are the donor's own, read back into this game's sign convention: the donor keeps a
/// location's reputation sign-flipped and prints a category for it, so a band's name and the number that
/// opens it are one row here rather than a comparison repeated at every use.
/// </remarks>
/// <param name="Word">What the game calls a party standing in this band.</param>
/// <param name="Floor">The lowest reputation that stands in this band.</param>
/// <param name="Reading">What standing in this band means for how the party is treated.</param>
internal readonly record struct ReputationBand(string Word, int Floor, string Reading);

/// <summary>
/// What this game makes of a party's standing: the bands and their edges, what moves reputation, and the
/// words the party's standing and its accomplishments read as.
/// </summary>
/// <remarks>
/// <para>
/// <b>One number, on the party, and the world judges the band rather than a place.</b> The donor keeps a
/// reputation per location and sign-flipped — <c>LocationInfo.h:7</c>, "Sign-flipped party reputation in
/// this location, negative value means positive reputation", read back through
/// <c>src/Engine/Party.cpp:824-835</c>, <c>Party::GetPartyReputation</c> — and prints one of five category
/// words for it (<c>src/GUI/UI/UIGame.cpp:1645-1654</c>, <c>GetReputationString</c>). This game keeps one
/// number for the party, as the design pins it, and states its sign: <b>a higher reputation is a better
/// one</b>. The donor's five words and its four edges are kept, mapped through that sign — the donor's
/// <c>&gt;= 25</c> "Hated" is our <c>&lt;= -25</c>, and so on — so the bands are the game's own and the
/// number that opens each is the donor's.
/// </para>
/// <para>
/// <b>What moves it, and what does not.</b> The donor's own movers are acts against people and their
/// property: killing a townsperson (<c>src/Engine/Objects/Actor.cpp:1083-1105</c>,
/// <c>Actor::ApplyFineForKillingPeasant</c>) and being caught stealing
/// (<c>src/GUI/UI/Houses/Shops.cpp:1147-1174</c>) both push a location's reputation the wrong way, and a
/// temple donation pushes it the right way one point at a time (<c>src/GUI/UI/Houses/Temple.cpp:82-91</c>).
/// A creature killed in the wild moves nothing there: the donor pays experience for it and no reputation at
/// all (<c>src/Engine/Objects/Actor.cpp:3164-3167</c>) — what the world hears about is fame
/// (<c>src/Engine/Party.cpp:371-379</c>). This game reads the same two facts the same way and adds one of
/// its own: <b>a finished errand moves the world's opinion by one point for every thousand experience it
/// paid, and by at least one</b> — ours, in the donor's own unit of a thousand to the point, because a deed
/// somebody asked for and saw finished is what a town has an opinion about, and a deed nobody thought worth
/// much is still a deed. <b>Killing a townsperson lowers it one point</b>, the donor's own step: the death
/// is credited as <see cref="MightAndMagic7Crimes.TownspersonKillSource"/> and the fine beside it is
/// <see cref="MightAndMagic7Crimes"/>'s. Killing any other peaceful person — a guard or an adept a place
/// stands there — lowers it the same point with no fine (ours), credited as
/// <see cref="MightAndMagic7Crimes.PersonKillSource"/>. <b>A theft lowers it as the donor's does</b>
/// (<see cref="MightAndMagic7Theft"/>): a thief seen empty-handed at a counter one point, one seen with the goods
/// and one nobody saw two (<c>src/GUI/UI/Houses/Shops.cpp:1147-1171</c>), and every attempt on a person's purse
/// one (<c>src/Engine/Objects/Actor.cpp:1236</c>) — each a deed that pays no experience, told through
/// <see cref="PartyProgression.Deed"/>. The donor's last mover the wrong way, the dark sacrifice it charges
/// fifteen points for (<c>src/Engine/Spells/CastSpellInfo.cpp:2800-2809</c>), needs a follower to give up; see the
/// ruleset's README for where it is routed.
/// </para>
/// <para>
/// <b>The bands do three things, and the third is the donor's own arithmetic.</b> They word what a person
/// says about the party; they open and close what a person will do for it — the line a person will speak
/// about the party, and the notice a town hall will post — and they move what every counter charges, because
/// the donor's merchant rule reads the reputation number itself rather than a band
/// (<c>src/Engine/PriceCalculator.cpp:139-151</c>, <c>playerMerchant</c>, whose adjustment
/// <c>applyMerchantDiscount</c> applies at <c>:152-158</c>). Prices therefore change with every point of
/// standing and not only when a band is crossed, which is the donor's behaviour and is kept.
/// </para>
/// <para>
/// <b>Fame carries no bands, because the donor gives it none.</b> The donor prints fame as a bare number
/// beside the reputation category (<c>src/GUI/UI/UIQuickReference.cpp:134-143</c>) and reads it in exactly
/// one place: whether somebody will join the party, gated on the party's fame exceeding their own
/// (<c>src/GUI/UI/UIdialogue.cpp:78</c>, which the donor disables with a note that it is an MM8 behaviour).
/// This build has no follower owner yet (#8514), so nothing can join the party and that gate has nothing to
/// guard. Fame is published as its own number and nothing here
/// invents a band for it.
/// </para>
/// <para>
/// <b>An accomplishment is a record the party already carries.</b> The donor keeps awards as bits on each
/// character (<c>src/Engine/Data/AwardEnums.h:3-91</c>) and they are of three families: the promotion a
/// character earned (<c>AWARD_PROMOTION_*</c>), the guild membership it bought (<c>AWARD_MEMBERSHIP_*</c>),
/// and the deeds its errands left (the quest awards). This game carries the same three families as records
/// on the party — <c>promotion:&lt;rank&gt;</c>, a guild's own membership effect, and
/// <c>errand:&lt;bit&gt;</c> — which is why this reading adds no state of its own: it names what the party
/// holds, using the tables that wrote it.
/// </para>
/// </remarks>
internal sealed class MightAndMagic7Standing : IStandingRule
{
    /// <summary>The word this game uses for the family a promotion's own record belongs to.</summary>
    internal const string PromotionKind = "promotion";

    /// <summary>The word this game uses for the family a finished errand's record belongs to.</summary>
    internal const string ErrandKind = "errand";

    /// <summary>The word this game uses for the family a counted deed's record belongs to.</summary>
    internal const string DeedKind = "deed";

    /// <summary>The word this game uses for the family a guild membership belongs to.</summary>
    internal const string MembershipKind = "membership";


    /// <summary>
    /// The bands, highest first, each with the donor's word and the lowest reputation that stands in it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The words and the edges are the donor's (<c>src/GUI/UI/UIGame.cpp:1645-1654</c>): "Liked" below its
    /// lowest edge, "Friendly" from its <c>6</c>, "Neutral" from its <c>-5</c>, "Unfriendly" from its
    /// <c>-24</c>, and "Hated" from its <c>25</c> — the donor's own numbers, read in this game's sign
    /// convention, in which a higher reputation is a better one. The readings are this game's own words for
    /// what those bands do here, and each says only what this build's mechanisms actually do.
    /// </para>
    /// <para>
    /// The floor of the lowest band is the bottom of the range rather than a number, because reputation is
    /// not clamped anywhere: a party can be worse than hated, and it reads as hated rather than as a band
    /// nothing names.
    /// </para>
    /// </remarks>
    internal static readonly ReputationBand[] Bands =
    [
        new(
            "Liked",
            25,
            "what the party has done is common knowledge, and every counter it deals with knows it"),
        new(
            "Friendly",
            6,
            "people speak well of the party, and a hall will put its notices its way"),
        new(
            "Neutral",
            -5,
            "the party is judged on what it does rather than on what is said about it"),
        new(
            "Unfriendly",
            -24,
            "people are wary, and no hall posts its notices for the party"),
        new(
            "Hated",
            int.MinValue,
            "people have no time for the party, and every counter charges what its number earns"),
    ];

    /// <summary>
    /// The standing a person must hold the party in before they will say what the town makes of it, which is
    /// also the standing a town hall posts its notice for.
    /// </summary>
    /// <remarks>
    /// This is the "Friendly" band's own floor read out of the table above rather than a second number: the
    /// line a person speaks, the notice a hall posts, and the band word all move together, so a change to
    /// where the band begins is a change to what the band does.
    /// </remarks>
    internal static int WellRegarded => Band("Friendly").Floor;

    /// <summary>
    /// The words a person speaks about the party when they will speak at all.
    /// </summary>
    /// <remarks>
    /// The line is this game's own and is composed around the donor's own band word
    /// (<c>src/GUI/UI/UIGame.cpp:1645-1654</c>), because the donor has no line of its own to read: the
    /// original writes its NPC strings with a <c>%11</c> code that stands for the party's reputation
    /// category (MMExtension, <c>MMExtension.htm</c>, "Special Codes in Texts"), and the categories are the
    /// five words above. A line that said something else would be a second reading of the same bands.
    /// </remarks>
    /// <param name="party">The party whose band is spoken.</param>
    /// <returns>What the person says.</returns>
    /// <exception cref="ArgumentNullException">No party was supplied.</exception>
    internal static string Words(PartyEntity party)
    {
        ArgumentNullException.ThrowIfNull(party);
        ReputationBand band = BandOf(party.Reputation.Reputation);
        return $"'{band.Word} is the word: {band.Reading}.'";
    }

    /// <summary>What the world's opinion of a deed is worth, as this game reads the event that carried it.</summary>
    /// <remarks>
    /// <para>
    /// A peaceful person's death lowers it by one point, the donor's own step
    /// (<c>src/Engine/Objects/Actor.cpp:1101-1102</c>, a location's sign-flipped <c>reputation++</c>), whatever
    /// the person was worth in experience — a death worth nothing reaches here as a deed rather than an award.
    /// A theft lowers it by the donor's own steps, and a finished errand raises it.
    /// </para>
    /// <para>
    /// A finished errand moves it by one point per thousand experience the errand
    /// paid, never less than one. The unit is the donor's: it reads a party's <em>fame</em> from the party's
    /// total experience at a thousand to the point (<c>src/Engine/Party.cpp:371-379</c>), and this game reads
    /// a town's opinion of one deed from the same figure, so an errand worth a great deal is one more people
    /// talk about than an errand worth little. The floor of one point is this game's own: the donor's temple
    /// moves a location's reputation one point per donation whatever was given
    /// (<c>src/GUI/UI/Houses/Temple.cpp:82-84</c>), so a deed worth little is still a deed. A creature killed
    /// in the wild is not a deed a town has an opinion about, which is the donor's own reading of it.
    /// </para>
    /// </remarks>
    /// <param name="request">The party the event happened to, which kind of event it was, and what it was worth.</param>
    /// <returns>How much the world's opinion of the party moves, zero when it moves not at all.</returns>
    /// <exception cref="ArgumentNullException">No request was supplied.</exception>
    internal static int ReputationFor(ProgressionStandingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Event == ProgressionEventKind.Training) return 0;
        switch (request.Source)
        {
            case MightAndMagic7Crimes.TownspersonKillSource or MightAndMagic7Crimes.PersonKillSource:
                return MightAndMagic7Crimes.PersonKillReputation;
            case MightAndMagic7Theft.CaughtSource:
                return MightAndMagic7Theft.CaughtReputation;
            case MightAndMagic7Theft.CaughtWithGoodsSource or MightAndMagic7Theft.UnseenSource:
                return MightAndMagic7Theft.TakenReputation;
            case MightAndMagic7Theft.PickpocketSource:
                return MightAndMagic7Theft.PickpocketReputation;
        }

        if (request.Event != ProgressionEventKind.Award || !string.Equals(request.Source, PartyQuests.QuestSource, StringComparison.Ordinal)) return 0;
        return (int)Math.Max(1, request.Amount / ExperiencePerPoint);
    }

    /// <summary>How much experience one point of the world's opinion is worth, which is the donor's own figure.</summary>
    internal const long ExperiencePerPoint = 1000;

    /// <summary>The band a reputation stands in, which is the highest band whose floor it reaches.</summary>
    /// <param name="reputation">The party's reputation.</param>
    /// <returns>The band.</returns>
    internal static ReputationBand BandOf(int reputation)
    {
        foreach (ReputationBand band in Bands)
        {
            if (reputation >= band.Floor) return band;
        }

        return Bands[^1];
    }

    /// <summary>The band a word names, which is how a gate is read out of the table rather than restated.</summary>
    /// <param name="word">The band's own word.</param>
    /// <returns>The band.</returns>
    /// <exception cref="InvalidOperationException">No band carries that word, so the table and its reader disagree.</exception>
    private static ReputationBand Band(string word)
    {
        foreach (ReputationBand band in Bands)
        {
            if (string.Equals(band.Word, word, StringComparison.Ordinal)) return band;
        }

        throw new InvalidOperationException($"This game's band table carries no band called '{word}', so nothing could read what standing it opens at.");
    }

    private readonly Dictionary<string, PromotionRankReading> _ranks = new(StringComparer.Ordinal);
    private readonly Dictionary<string, QuestDefinition> _errands = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PromotionRequirement> _deeds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _memberships = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _givers = new(StringComparer.Ordinal);

    /// <summary>Reads this game's standing policy over the tables that write what a party accomplishes.</summary>
    /// <remarks>
    /// The tables are the ones the rest of the session already read, handed over rather than read a second
    /// time: a rank's own record and the class it names come from the ladder, an errand's record and its
    /// words from the quest definitions, a counted deed from the ladder's own requirement, and a membership
    /// from the counter that sells it. A ruleset that composed none of them answers an empty list, which is
    /// the honest state of a game whose tables state nothing a party could accomplish.
    /// </remarks>
    /// <param name="promotions">This game's ladder, or null when its ruleset stated none.</param>
    /// <param name="quests">This game's quests, or null when its ruleset stated none.</param>
    /// <param name="services">This game's counters, or null when its ruleset stated none.</param>
    internal MightAndMagic7Standing(
        MightAndMagic7Promotions? promotions,
        MightAndMagic7Quests? quests,
        MightAndMagic7Services? services)
    {
        if (promotions is { } ladder)
        {
            foreach (PromotionRank rank in ladder.Ladder.Ranks)
            {
                if (rank.Award.Length > 0)
                {
                    _ranks[rank.Award] = new PromotionRankReading(rank.To.Value, rank.From.Value, rank.Choice);
                }

                foreach (PromotionRequirement requirement in rank.Requirements)
                {
                    if (requirement.Kind == PromotionRequirementKind.Giver)
                    {
                        _givers[requirement.Name] = requirement.Label.Length > 0 ? requirement.Label : requirement.Name;
                    }
                    else if (requirement.Kind == PromotionRequirementKind.Award
                        && requirement.Name.StartsWith(MightAndMagic7Identities.DeedPrefix, StringComparison.Ordinal))
                    {
                        _deeds[requirement.Name] = requirement;
                    }
                }
            }
        }

        if (quests is not null)
        {
            foreach (QuestDefinition definition in quests.Definitions)
            {
                if (definition.Record.Length > 0) _errands[definition.Record] = definition;
            }
        }

        if (services is not null)
        {
            foreach ((string effect, string label) in services.Memberships) _memberships[effect] = label;
        }
    }

    /// <inheritdoc />
    public StandingReading Read(PartyEntity party)
    {
        ArgumentNullException.ThrowIfNull(party);
        ReputationBand band = BandOf(party.Reputation.Reputation);
        return StandingReading.Of(band.Word, band.Reading);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The order is the party's own order of records and then of memberships, so two readings of one party read
    /// the same way. A record that is none of the families — the flag a conversation left — is not an
    /// accomplishment and is not listed here: the donor's awards are a named
    /// set of things a character did, not everything that happened to it.
    /// </remarks>
    public IReadOnlyList<AwardReading> Awards(PartyEntity party)
    {
        ArgumentNullException.ThrowIfNull(party);
        List<AwardReading> awards = [];
        foreach (PartyRecord held in party.Records.All)
        {
            string record = held.Name;
            if (_ranks.TryGetValue(record, out PromotionRankReading rank))
            {
                string detail = rank.Choice.Length > 0
                    ? string.Create(CultureInfo.InvariantCulture, $"{rank.From}, the {rank.Choice} path")
                    : rank.From;
                awards.Add(AwardReading.Of(record, PromotionKind, rank.To, detail));
                continue;
            }

            if (_errands.TryGetValue(record, out QuestDefinition? errand))
            {
                awards.Add(AwardReading.Of(
                    record,
                    ErrandKind,
                    errand.Name,
                    _givers.TryGetValue(errand.Giver, out string? giver) ? $"given by {giver}" : string.Empty));
                continue;
            }

            if (_deeds.TryGetValue(record, out PromotionRequirement? deed))
            {
                awards.Add(AwardReading.Of(
                    record,
                    DeedKind,
                    deed.Label.Length > 0 ? deed.Label : record,
                    string.Create(CultureInfo.InvariantCulture, $"{held.Count} to its name")));
            }
        }

        // The memberships the party was granted are listed after its records, in the order they were granted.
        foreach (string held in party.Memberships.All)
        {
            if (_memberships.TryGetValue(held, out string? membership))
            {
                awards.Add(AwardReading.Of(held, MembershipKind, membership, string.Empty));
            }
        }

        return awards;
    }

    /// <summary>What one rank a party holds reads as: the class it named and the class it was taken from.</summary>
    /// <param name="To">The class the rank names, which is what a person calls the character who holds it.</param>
    /// <param name="From">The class the member stood in before the rank was given.</param>
    /// <param name="Choice">The alternative the rank took, or empty when it took none.</param>
    private readonly record struct PromotionRankReading(string To, string From, string Choice);
}
