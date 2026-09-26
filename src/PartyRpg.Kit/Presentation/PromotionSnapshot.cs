using System.Globalization;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Progression;
using PartyRpg.Kit.Promotion;

namespace PartyRpg.Kit.Presentation;

/// <summary>One thing a rank asks for, as the ladder states it: no judgement, only what it says.</summary>
/// <remarks>
/// The ladder block states what a rank asks for and never whether the party has it: a requirement is judged
/// when a rank is actually asked for, against the person the party is speaking with, and a panel that
/// judged one from a ladder list would show a giver as missing while the party stands in front of them.
/// What was met and what was missing belongs to the report of an attempt, which carries the judgement.
/// </remarks>
/// <param name="Kind">What kind of thing it is: <c>giver</c>, <c>item</c>, <c>award</c>, or <c>quest</c>.</param>
/// <param name="Name">The identity the game's own owner resolves.</param>
/// <param name="Label">How it reads to a person.</param>
/// <param name="Amount">How much of it is asked for.</param>
/// <param name="Text">The requirement as one phrase a screen prints.</param>
public readonly record struct PromotionRequirementSnapshot(
    string Kind,
    string Name,
    string Label,
    int Amount,
    string Text);

/// <summary>One rank a class leads to, as the panel needs it.</summary>
/// <param name="Promotion">The rank's identity in the ladder, which a conversation hands over.</param>
/// <param name="ToClass">The class the rank names.</param>
/// <param name="Rank">The rank in the class ladder it reaches.</param>
/// <param name="Choice">The alternative it takes, empty when it is not a split of two.</param>
/// <param name="Giver">Who gives it, as the person's identity.</param>
/// <param name="GiverName">Who gives it, as a person reads the name.</param>
/// <param name="Words">What the giver says when the rank is taken.</param>
/// <param name="Requirements">What the rank asks for, in the ladder's own order.</param>
public readonly record struct PromotionRankSnapshot(
    string Promotion,
    string ToClass,
    int Rank,
    string Choice,
    string Giver,
    string GiverName,
    string Words,
    IReadOnlyList<PromotionRequirementSnapshot> Requirements);

/// <summary>One member and the ranks its class leads to.</summary>
/// <param name="Index">The member's place in the party, counted from zero.</param>
/// <param name="Member">The member's durable identity.</param>
/// <param name="Name">What the member is called.</param>
/// <param name="Class">The class the member belongs to.</param>
/// <param name="Rank">The rank the member holds.</param>
/// <param name="Promotions">The ranks this class leads to, which is empty at the top of a ladder.</param>
public readonly record struct PromotionMemberSnapshot(
    int Index,
    string Member,
    string Name,
    string Class,
    int Rank,
    IReadOnlyList<PromotionRankSnapshot> Promotions);

/// <summary>One member a rank was given to, as the panel reads it.</summary>
/// <param name="Member">The member's durable identity.</param>
/// <param name="Name">What the member is called.</param>
/// <param name="FromClass">The class the member stood in.</param>
/// <param name="FromRank">The rank the member stood at.</param>
/// <param name="ToClass">The class the member now belongs to.</param>
/// <param name="Rank">The rank the member now holds.</param>
/// <param name="Choice">The alternative the rank took, empty when it is not a split of two.</param>
/// <param name="Met">What the rank asked for and the member met, in the ladder's own order.</param>
public readonly record struct PromotionGrantSnapshot(
    string Member,
    string Name,
    string FromClass,
    int FromRank,
    string ToClass,
    int Rank,
    string Choice,
    IReadOnlyList<string> Met);

/// <summary>One member a rank was not given to, and what they were missing.</summary>
/// <param name="Member">The member's durable identity.</param>
/// <param name="Name">What the member is called.</param>
/// <param name="Class">The class the member stands in.</param>
/// <param name="Rank">The rank the member stands at.</param>
/// <param name="Missing">What the rank asked for and the member did not have.</param>
public readonly record struct PromotionDenialSnapshot(
    string Member,
    string Name,
    string Class,
    int Rank,
    IReadOnlyList<string> Missing);

/// <summary>What the party may become and what its last rank did, as the panel needs it.</summary>
/// <remarks>
/// <para>
/// Two facts and not one. <see cref="Members"/> is the ladder as it stands for the party now: each member's
/// class and rank, and the ranks that class leads to with everything each of them asks for — which is what
/// a screen shows a player who is deciding where to take a character. The report is the other fact: which
/// members the last rank was given to, what each of them met, and which members it passed over with what
/// they were missing. A panel that only showed the ladder would leave a refusal invisible, and one that
/// only showed the report would leave the player guessing what is possible.
/// </para>
/// <para>
/// <b>A session whose ruleset stated no ladder is a different fact from a party at the top of one.</b> The
/// first has no ranks at all and says so; the second has members whose classes lead nowhere, which is what
/// an earned third rank looks like from the outside.
/// </para>
/// </remarks>
/// <param name="Available">Whether the session's ruleset stated a ladder at all.</param>
/// <param name="Members">The party's members and the ranks their classes lead to, in the party's own order.</param>
/// <param name="Outcome">What the last rank was: <c>none</c>, <c>granted</c>, or <c>refused</c>.</param>
/// <param name="Promotion">The identity of the last rank asked for, empty before any was.</param>
/// <param name="ToClass">The class the last rank names, empty before any was.</param>
/// <param name="Rank">The rank the last promotion reaches, zero before any was.</param>
/// <param name="Choice">The alternative the last rank took, empty when it is not a split of two.</param>
/// <param name="Granted">Who rose, in the party's own order.</param>
/// <param name="Denied">Who did not and what they were missing, in the party's own order.</param>
/// <param name="Code">The last refusal's code, empty when the last rank landed or none has been asked for.</param>
/// <param name="Message">What the last rank reported, empty before anybody has been promoted.</param>
public readonly record struct PromotionSnapshot(
    bool Available,
    IReadOnlyList<PromotionMemberSnapshot> Members,
    string Outcome,
    string Promotion,
    string ToClass,
    int Rank,
    string Choice,
    IReadOnlyList<PromotionGrantSnapshot> Granted,
    IReadOnlyList<PromotionDenialSnapshot> Denied,
    string Code,
    string Message)
{
    /// <summary>No ladder: no class leads anywhere and no rank is ever given.</summary>
    public static PromotionSnapshot None => new(
        Available: false,
        Members: [],
        Outcome: "none",
        Promotion: string.Empty,
        ToClass: string.Empty,
        Rank: 0,
        Choice: string.Empty,
        Granted: [],
        Denied: [],
        Code: string.Empty,
        Message: string.Empty);

    /// <summary>Reads the ladder and the last rank out of the progression owner.</summary>
    /// <remarks>
    /// Every row is sent whole and nothing is judged here: the requirements are what the ladder states, and
    /// what an attempt met or missed travels in the report the owner itself recorded, so a screen prints the
    /// game's own answer rather than working one out.
    /// </remarks>
    /// <param name="progression">The session's progression owner, or null when it holds none.</param>
    /// <returns>What the panel shows, or <see cref="None"/> when no ladder is stated.</returns>
    public static PromotionSnapshot From(PartyProgression? progression)
    {
        if (progression?.Promotions is not { } policy) return None;

        List<PromotionMemberSnapshot> members = [];
        for (int index = 0; index < progression.Party.Members.Count; index++)
        {
            PartyMember member = progression.Party.Members[index];
            List<PromotionRankSnapshot> rows = [];
            foreach (PromotionRank rank in policy.Ladder.From(member.Profile.Class))
            {
                List<PromotionRequirementSnapshot> requirements = [];
                foreach (PromotionRequirement requirement in rank.Requirements)
                {
                    requirements.Add(new PromotionRequirementSnapshot(
                        WireName(requirement.Kind),
                        requirement.Name,
                        requirement.Label,
                        requirement.Amount,
                        requirement.ToString()));
                }

                rows.Add(new PromotionRankSnapshot(
                    rank.Id,
                    rank.To.Value,
                    rank.Rank,
                    rank.Choice,
                    rank.Giver,
                    Label(rank, PromotionRequirementKind.Giver),
                    rank.Words,
                    requirements));
            }

            members.Add(new PromotionMemberSnapshot(
                index,
                member.Id.ToString(),
                member.Profile.Name,
                member.Profile.Class.Value,
                member.Progression.ClassRank,
                rows));
        }

        if (progression.LastPromotion is not { } last)
        {
            return new PromotionSnapshot(
                Available: true,
                members,
                Outcome: "none",
                Promotion: string.Empty,
                ToClass: string.Empty,
                Rank: 0,
                Choice: string.Empty,
                Granted: [],
                Denied: [],
                Code: string.Empty,
                Message: string.Empty);
        }

        List<PromotionGrantSnapshot> granted = [];
        foreach (PromotionGrant grant in last.Granted)
        {
            granted.Add(new PromotionGrantSnapshot(
                grant.Member.ToString(),
                grant.Name,
                grant.FromClass,
                grant.FromRank,
                grant.ToClass,
                grant.Rank,
                grant.Choice,
                grant.Met));
        }

        List<PromotionDenialSnapshot> denied = [];
        foreach (PromotionDenial denial in last.Denied)
        {
            denied.Add(new PromotionDenialSnapshot(
                denial.Member.ToString(),
                denial.Name,
                denial.Class,
                denial.Rank,
                denial.Missing));
        }

        return new PromotionSnapshot(
            Available: true,
            members,
            Outcome: last.IsGranted ? "granted" : "refused",
            Promotion: last.Promotion,
            ToClass: last.ToClass,
            Rank: last.Rank,
            Choice: last.Choice,
            granted,
            denied,
            last.Refusal?.Code ?? string.Empty,
            last.IsGranted ? Report(last, granted) : Denial(last, denied));
    }

    /// <summary>What a rank that landed reports: who rose, from where, and what each of them met.</summary>
    private static string Report(PromotionResult last, IReadOnlyList<PromotionGrantSnapshot> granted)
    {
        List<string> names = [.. granted.Select(grant => grant.Name)];
        string choice = last.Choice.Length > 0 ? $", the {last.Choice} path" : string.Empty;
        List<string> met = [.. granted[0].Met];
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{string.Join(", ", names)} rose to {last.ToClass} at rank {last.Rank}{choice}, meeting {string.Join("; ", met)}.");
    }

    /// <summary>What a rank that was refused reports: every member it passed over and what each was missing.</summary>
    private static string Denial(PromotionResult last, IReadOnlyList<PromotionDenialSnapshot> denied)
    {
        if (denied.Count == 0 || last.Refusal is { } refusal) return last.Refusal?.Message ?? string.Empty;
        List<string> lines = [];
        foreach (PromotionDenialSnapshot denial in denied)
        {
            lines.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"{denial.Name}, a {denial.Class} of rank {denial.Rank}, is missing {string.Join("; ", denial.Missing)}"));
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"The rank of {last.ToClass} was not given: {string.Join(". ", lines)}.");
    }

    /// <summary>How one requirement kind reads on the wire.</summary>
    private static string WireName(PromotionRequirementKind kind) => kind switch
    {
        PromotionRequirementKind.Giver => "giver",
        PromotionRequirementKind.Item => "item",
        PromotionRequirementKind.Award => "award",
        _ => "quest",
    };

    /// <summary>The label a rank's requirement of one kind carries, or the identity when it carries none.</summary>
    private static string Label(PromotionRank rank, PromotionRequirementKind kind)
    {
        foreach (PromotionRequirement requirement in rank.Requirements)
        {
            if (requirement.Kind == kind) return requirement.Label.Length > 0 ? requirement.Label : requirement.Name;
        }

        return string.Empty;
    }
}
