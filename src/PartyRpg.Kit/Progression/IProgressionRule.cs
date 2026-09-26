using PartyRpg.Kit.Party;

namespace PartyRpg.Kit.Progression;

/// <summary>How one party-wide experience award divides among the members who can take it.</summary>
/// <remarks>
/// The party travels whole because who takes a share of what a fight earned is this game's rule rather than
/// a division the kit could perform: a member who is down takes nothing, a member who is learned takes more,
/// and a rule that could only state a number per member could express neither. The amount is the award
/// before the division, which the rule splits as its own curve and its own skills say.
/// </remarks>
/// <param name="Party">The party that earned it, with the state the division reads.</param>
/// <param name="Source">What earned it, as the caller names it: a kill, a quest, an act.</param>
/// <param name="Amount">How much experience the party earned in total, before the division.</param>
public sealed record ProgressionDivision(PartyEntity Party, string Source, long Amount);

/// <summary>What one level rise gives a member, as this game's own tables state it.</summary>
/// <remarks>
/// <para>
/// The three numbers are what one level <em>adds</em>: how much larger the member's hit point and spell
/// point pools become, and how many skill points the new level grants. Gifts rather than new maxima, because
/// what a member's pools hold is the party's own state — content and creation state it at level one — and a
/// rule that answered an absolute maximum would have to restate everything else that contributes to it.
/// </para>
/// <para>
/// Every value is content's or the ruleset's, never the kit's: this type carries the answer, it does not
/// decide it.
/// </para>
/// </remarks>
/// <param name="HitPoints">How much the member's hit point capacity grows.</param>
/// <param name="SpellPoints">How much the member's spell point capacity grows.</param>
/// <param name="SkillPoints">How many skill points the level grants.</param>
public readonly record struct ProgressionGrowth(int HitPoints, int SpellPoints, int SkillPoints)
{
    /// <summary>A level that gives nothing, which is what a ruleset that states no tables gets.</summary>
    public static ProgressionGrowth None => default;
}

/// <summary>What a level rise is asked about: whose level, and which level it reaches.</summary>
/// <param name="Member">The member who is rising, with the class, the rank, and the scores the tables key on.</param>
/// <param name="Level">The level being reached, which is the level the growth is read for.</param>
public sealed record ProgressionGrowthRequest(PartyMember Member, int Level);

/// <summary>Which kind of progression event is being judged for what it does to the party's standing.</summary>
public enum ProgressionEventKind
{
    /// <summary>Experience was awarded: a fight won, a quest completed, a deed done.</summary>
    Award,

    /// <summary>A member was trained a level.</summary>
    Training,
}

/// <summary>How much a progression event changes the party's standing, as this game's rule answers it.</summary>
/// <remarks>
/// Deltas rather than new values, because reputation and fame are the party's own state and the party owner
/// is the only thing that writes them: a rule that answered an absolute value would be a second place those
/// numbers live, and the kit's owner would be copying it rather than moving the party's own.
/// </remarks>
/// <param name="Reputation">How much the world's opinion of the party moves.</param>
/// <param name="Fame">How much word of the party's deeds spreads.</param>
public readonly record struct ProgressionStanding(int Reputation, int Fame)
{
    /// <summary>An event the world takes no notice of.</summary>
    public static ProgressionStanding None => default;
}

/// <summary>What a progression event is asked about its effect on the party's standing.</summary>
/// <remarks>
/// The party travels whole because fame and reputation in this game family are read from what the party has
/// done rather than from the event alone: what the party's deeds are worth to the world is a function of
/// the party's state, and the rule is asked after the award landed so that it reads the state the event
/// produced.
/// </remarks>
/// <param name="Party">The party after the event, with the state the answer is read from.</param>
/// <param name="Event">Which kind of event happened.</param>
/// <param name="Amount">What the event was worth, zero when it is not measured in experience.</param>
public sealed record ProgressionStandingRequest(PartyEntity Party, ProgressionEventKind Event, long Amount);

/// <summary>
/// What this game answers about progression: the curve a level takes, how an award divides, what a level
/// gives, and what the party's standing does about it.
/// </summary>
/// <remarks>
/// <para>
/// This is the ruleset's whole contribution to the progression owner, and it is deliberately four answers
/// rather than one. <see cref="ExperienceForLevel"/> is the curve: how much banked experience a level takes,
/// which a training hall converts and nothing spends. <see cref="Divide"/> is the award rule: who in the
/// party takes a share of what was earned, and how much. <see cref="Growth"/> is the growth table: what one
/// level adds to a member's pools and how many skill points it grants, keyed by the class and rank this game
/// describes. <see cref="Standing"/> is what the world makes of it.
/// </para>
/// <para>
/// <b>The numbers are the ruleset's; the arithmetic of applying them is the kit's.</b> Nothing here is
/// called to change anything: the owner asks, and the owner writes. A rule that mutated the party would be a
/// second owner of one fact, which is exactly what this interface exists to prevent.
/// </para>
/// </remarks>
public interface IProgressionRule
{
    /// <summary>How much experience a member must have banked to be trained from a level to the next.</summary>
    /// <remarks>
    /// Banked, not spent: training converts experience into a level and takes none of it away, so a member
    /// who has earned the level keeps every point they earned towards the next one.
    /// </remarks>
    /// <param name="level">The level the member stands at.</param>
    /// <returns>How much experience that level's next step takes.</returns>
    long ExperienceForLevel(int level);

    /// <summary>How a party-wide award divides among the members who can take it.</summary>
    /// <param name="division">The party, what earned the award, and how much it was.</param>
    /// <returns>One share per member who takes one, in the order they should be credited.</returns>
    IReadOnlyList<ProgressionShare> Divide(ProgressionDivision division);

    /// <summary>What one level rise gives a member.</summary>
    /// <param name="request">The member and the level being reached.</param>
    /// <returns>How much the member's pools grow and how many skill points the level grants.</returns>
    ProgressionGrowth Growth(ProgressionGrowthRequest request);

    /// <summary>How much a progression event changes the party's standing.</summary>
    /// <param name="request">The party, the event, and what it was worth.</param>
    /// <returns>How much reputation and fame move.</returns>
    ProgressionStanding Standing(ProgressionStandingRequest request);
}
