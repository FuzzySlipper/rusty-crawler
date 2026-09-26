using System.Globalization;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Progression;
using PartyRpg.Kit.Services;

namespace PartyRpg.Kit.Presentation;

/// <summary>One member's growth, as the panel shows it: the level, what earned it, and what the next costs.</summary>
/// <remarks>
/// <para>
/// Every number is read from the member the session plays and the curve the ruleset states; none of it is a
/// count kept here. <see cref="NextLevel"/> is what the member must have banked for the next level, so a
/// panel can show how far along they are without knowing the curve, and <see cref="Fee"/> is the counter's
/// own quote for this member rather than a price worked out beside it.
/// </para>
/// <para>
/// A member who stands at no counter that trains has a fee of zero and a ceiling of zero, which is what "no
/// one here trains" reads as: a hall that trained for nothing would be a hall charging nothing, and no
/// content states one.
/// </para>
/// </remarks>
/// <param name="Index">The member's place in the party, counted from zero.</param>
/// <param name="Member">The member's durable identity.</param>
/// <param name="Name">What the member is called.</param>
/// <param name="Level">The level the member stands at.</param>
/// <param name="Experience">How much experience the member has earned in total.</param>
/// <param name="SkillPoints">How many skill points the member holds unspent.</param>
/// <param name="NextLevel">How much experience the member's next level takes, which is the curve's own figure.</param>
/// <param name="Fee">What the counter the party stands at would charge this member for one level, zero when none trains.</param>
/// <param name="Cap">The highest level that counter trains to, zero when none trains.</param>
public readonly record struct ProgressionMemberSnapshot(
    int Index,
    string Member,
    string Name,
    int Level,
    long Experience,
    int SkillPoints,
    long NextLevel,
    int Fee,
    int Cap);

/// <summary>What the party has earned and what a level costs, as the panel needs it.</summary>
/// <remarks>
/// <para>
/// These are the owner's own facts copied into one presentation value: each member's level, experience, and
/// unspent points, the experience the curve takes for the next level, and what the counter the party stands
/// at would charge for it. Nothing here is computed by the screen, which is the point — a panel that worked
/// out a curve or a fee would be a second copy of the ruleset's answer.
/// </para>
/// <para>
/// A session whose ruleset answered no progression policy has no owner at all, and <see cref="None"/> is
/// that state, so the panel says the mechanism is not there rather than showing a party that never grew. A
/// session that holds the owner but has earned nothing publishes the members with a zero award, which is a
/// different fact again.
/// </para>
/// </remarks>
/// <param name="Available">Whether the session holds a progression owner at all.</param>
/// <param name="Members">The party's members and what each has earned, in the party's own order.</param>
/// <param name="Outcome">What the last progression event was: <c>none</c>, <c>awarded</c>, <c>trained</c>, or <c>refused</c>.</param>
/// <param name="Source">What the last award came from, or the counter that last trained somebody.</param>
/// <param name="Earned">How much experience the last award was worth, zero when the last event was not one.</param>
/// <param name="Code">The last refusal's code, empty when the last event landed or none has happened.</param>
/// <param name="Message">What the last event reported, empty before the party has earned or trained anything.</param>
public readonly record struct ProgressionSnapshot(
    bool Available,
    IReadOnlyList<ProgressionMemberSnapshot> Members,
    string Outcome,
    string Source,
    long Earned,
    string Code,
    string Message)
{
    /// <summary>No progression owner: nobody grows and no level has a price.</summary>
    public static ProgressionSnapshot None => new(
        Available: false,
        Members: [],
        Outcome: "none",
        Source: string.Empty,
        Earned: 0,
        Code: string.Empty,
        Message: string.Empty);

    /// <summary>Reads the progression facts out of the session's owner and the counter it stands at.</summary>
    /// <remarks>
    /// The fee is asked of the service mechanism for each member rather than priced here, because a training
    /// step is priced for a member and the mechanism is what quotes one: a projection that multiplied a
    /// level by a multiplier would be a second price policy, and it would disagree with the counter the
    /// first time either changed.
    /// </remarks>
    /// <param name="progression">The session's progression owner, or null when it holds none.</param>
    /// <param name="services">The session's service mechanism, or null when it holds none; its open counter is what prices a step.</param>
    /// <returns>The facts the panel shows, or <see cref="None"/> when there is no owner.</returns>
    public static ProgressionSnapshot From(PartyProgression? progression, PartyServices? services)
    {
        if (progression is null) return None;

        List<ProgressionMemberSnapshot> members = [];
        for (int index = 0; index < progression.Party.Members.Count; index++)
        {
            PartyMember member = progression.Party.Members[index];
            ServiceOfferLine? training = services?.TrainingOffer(index);
            members.Add(new ProgressionMemberSnapshot(
                index,
                member.Id.ToString(),
                member.Profile.Name,
                member.Progression.Level,
                member.Progression.Experience,
                member.Progression.SkillPoints,
                progression.ExperienceForNextLevel(member),
                training?.Price ?? 0,
                training?.Offer.Limit ?? 0));
        }

        if (progression.LastTraining is { } trained)
        {
            return new ProgressionSnapshot(
                Available: true,
                members,
                Outcome: trained.IsTrained ? "trained" : "refused",
                Source: trained.Counter,
                Earned: 0,
                Code: trained.Refusal?.Code ?? string.Empty,
                Message: trained.IsTrained
                    ? string.Create(
                        CultureInfo.InvariantCulture,
                        $"{trained.Name} reached level {trained.Level}, gaining {trained.Growth.HitPoints} hit point(s), {trained.Growth.SpellPoints} spell point(s), and {trained.Growth.SkillPoints} skill point(s).")
                    : trained.Refusal!.Message);
        }

        if (progression.LastAward is { } award)
        {
            return new ProgressionSnapshot(
                Available: true,
                members,
                Outcome: award.IsAwarded ? "awarded" : "refused",
                Source: award.Source,
                Earned: award.IsAwarded ? award.Awarded : 0,
                Code: award.Refusal?.Code ?? string.Empty,
                Message: award.IsAwarded
                    ? string.Create(
                        CultureInfo.InvariantCulture,
                        $"The party earned {award.Awarded} experience from {award.Source}.")
                    : award.Refusal!.Message);
        }

        return new ProgressionSnapshot(
            Available: true,
            members,
            Outcome: "none",
            Source: string.Empty,
            Earned: 0,
            Code: string.Empty,
            Message: string.Empty);
    }
}
