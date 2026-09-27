using PartyRpg.Kit.Party;
using PartyRpg.Kit.Progression;
using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Presentation;

/// <summary>One thing the party has accomplished, as the panel shows it.</summary>
/// <remarks>
/// The row is the reading a game gave of one party-carried record: what it is called, which family the game
/// counts it in, and what is true of it beyond its name. Nothing here is worked out by the panel, and the
/// identity travels with the words so a row can still be traced to the state that produced it.
/// </remarks>
/// <param name="Id">The record's own identity, which is the state the award is carried as.</param>
/// <param name="Kind">What family the game counts it in, as the game words it.</param>
/// <param name="Label">What the game calls it.</param>
/// <param name="Detail">What is true of it beyond its name, empty when nothing is.</param>
public readonly record struct AwardSnapshot(string Id, string Kind, string Label, string Detail);

/// <summary>The party's own accounts and standing, as the panel shows them.</summary>
/// <remarks>
/// <para>
/// Every value is read from the party entity the session holds — one purse, one larder, one standing — and
/// none of it is a count kept here, because a second copy of a balance is exactly what the party owner
/// exists to prevent. A session with no party, which is what content that declares none gets, publishes
/// <see cref="None"/> and the panel says so.
/// </para>
/// <para>
/// The conditions are published because a party can be walking while weakened, and a panel that showed only
/// the food would leave the two states looking the same. They are the content identities the members
/// actually carry, so the ruleset's own list of afflictions reaches the screen without this layer knowing
/// any of them.
/// </para>
/// <para>
/// <b>What the standing and the accomplishments read as is the ruleset's.</b> The two numbers are the
/// party's own; the band they fall in, what that band does, and the names of the records the party has
/// collected are this game's words, asked of the rule that owns its thresholds. A panel therefore prints
/// "Friendly" and "the errand the hall posted" rather than working either out from a number or a record
/// identity it would have to know how to spell.
/// </para>
/// </remarks>
/// <param name="Present">Whether the session holds a party at all.</param>
/// <param name="Members">How many characters stand in the party.</param>
/// <param name="Coins">What the party's one purse holds.</param>
/// <param name="Provisions">What the party's one larder holds.</param>
/// <param name="Unit">The unit the provisions are stated in, as the wire spells it.</param>
/// <param name="Reputation">What the world thinks of the party.</param>
/// <param name="Fame">How widely the party is known.</param>
/// <param name="Conditions">The conditions acting on the party, in the order it carries them, empty when none act.</param>
/// <param name="HitPoints">What the members have left to lose between them.</param>
/// <param name="HitPointsMax">What they could have between them, which is the measure the first number needs.</param>
/// <param name="SpellPoints">What the members have left to cast with between them.</param>
/// <param name="SpellPointsMax">What they could have between them.</param>
/// <param name="Pack">
/// How many item instances lie in the party's one shared pack, which is where everything the party takes
/// goes: a search that was refused for want of room and one that landed are told apart by this number
/// moving, and a panel that showed only the purse would leave loot invisible until it was sold.
/// </param>
/// <param name="StandingRead">
/// Whether this game reads a standing and its accomplishments at all. A session whose ruleset reads neither
/// publishes that, which is a different fact from a party standing at the middle of a scale nobody named and
/// from one whose accomplishments are merely empty.
/// </param>
/// <param name="Standing">
/// What the game calls the band the party's standing falls in, empty when its ruleset names no bands.
/// </param>
/// <param name="StandingDetail">What that band means for how the party is treated, empty when nothing is read.</param>
/// <param name="Awards">
/// What the party has accomplished, in the game's own words, in the order the party carries the records;
/// empty when it has done nothing this game counts or when its ruleset counts nothing.
/// </param>
public readonly record struct PartySnapshot(
    bool Present,
    int Members,
    int Coins,
    int Provisions,
    string Unit,
    int Reputation,
    int Fame,
    string Conditions,
    int HitPoints = 0,
    int HitPointsMax = 0,
    int SpellPoints = 0,
    int SpellPointsMax = 0,
    int Pack = 0,
    bool StandingRead = false,
    string Standing = "",
    string StandingDetail = "",
    IReadOnlyList<AwardSnapshot>? Awards = null)
{
    /// <summary>The party of a session that holds none.</summary>
    public static PartySnapshot None => new(false, 0, 0, 0, string.Empty, 0, 0, string.Empty);

    /// <summary>Reads the party as the panel needs it.</summary>
    /// <param name="party">The party the session holds, or null when it holds none.</param>
    /// <param name="standing">
    /// This game's words for the standing a party holds, or null when its ruleset reads none. Without one the
    /// two numbers are still published and no band is claimed, because a band invented here would be this
    /// layer's reading of a threshold it does not own.
    /// </param>
    /// <returns>The party's accounts and standing, or the not-known value.</returns>
    public static PartySnapshot From(PartyEntity? party, IStandingRule? standing = null)
    {
        if (party is null) return None;
        int hitPoints = 0;
        int hitPointsMax = 0;
        int spellPoints = 0;
        int spellPointsMax = 0;
        foreach (PartyMember member in party.Members)
        {
            hitPoints += member.Resources.HitPoints.Current;
            hitPointsMax += member.Resources.HitPoints.Maximum;
            spellPoints += member.Resources.SpellPoints.Current;
            spellPointsMax += member.Resources.SpellPoints.Maximum;
        }

        StandingReading reading = standing?.Read(party) ?? StandingReading.None;
        List<AwardSnapshot> awards = [];
        if (standing is not null)
        {
            foreach (AwardReading award in standing.Awards(party))
            {
                awards.Add(new AwardSnapshot(award.Id, award.Kind, award.Label, award.Detail));
            }
        }

        return new PartySnapshot(
            true,
            party.Members.Count,
            party.Purse.Coins,
            party.Food.Portions,
            WireName(party.Food.Unit),
            party.Reputation.Reputation,
            party.Reputation.Fame,
            DescribeConditions(party),
            // What the party has left to lose and to cast with, summed over its members: a night's sleep
            // restores the pools, and a panel that showed only food and conditions would leave a rested
            // party and a wounded one looking the same.
            hitPoints,
            hitPointsMax,
            spellPoints,
            spellPointsMax,
            party.Inventory.Count,
            standing is not null,
            reading.Band ?? string.Empty,
            reading.Reading ?? string.Empty,
            awards);
    }

    /// <summary>
    /// Describes the conditions acting on the party, one entry per condition however many members carry it.
    /// </summary>
    /// <remarks>
    /// A whole party that went hungry is in one state, not four, so the panel shows what is acting rather
    /// than how many times it landed. The order is the order the members and their conditions stand in, so
    /// two projections of the same party read the same way.
    /// </remarks>
    private static string DescribeConditions(PartyEntity party)
    {
        List<string> acting = [];
        HashSet<ConditionId> seen = [];
        foreach (PartyMember member in party.Members)
        {
            foreach (ActiveCondition condition in member.Conditions.Active)
            {
                if (!seen.Add(condition.Condition)) continue;
                acting.Add(condition.Severity > 0
                    ? $"{condition.Condition} ({condition.Severity})"
                    : condition.Condition.ToString());
            }
        }

        return string.Join(", ", acting);
    }

    /// <summary>The wire name for a unit of provisions.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The unit has no wire name, so the panel would show a number with no measure.</exception>
    private static string WireName(ProvisionUnit unit) => unit switch
    {
        ProvisionUnit.Portions => "portions",
        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unknown unit of provisions."),
    };
}
