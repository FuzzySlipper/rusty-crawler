using System.Globalization;
using PartyRpg.Kit.Party;

namespace PartyRpg.Kit.Promotion;

/// <summary>Reads one rank's requirements against the party, on behalf of the progression owner.</summary>
/// <remarks>
/// <para>
/// <b>This is a reading and never a change.</b> The progression owner is the only thing that moves a rank,
/// a class, or a record on the party; this type answers what the owner needs in order to decide, so the
/// judgement a refusal lists and the judgement a panel shows are one answer rather than two that could
/// drift. Everything it reads is state the party already holds — the one inventory every item instance
/// lives in, the party's own carried records, and who the party is speaking with — and nothing is inferred.
/// </para>
/// <para>
/// <b>An errand is judged as the record its turn-in leaves.</b> The quest owner writes a record on the party
/// when an errand is handed back, so a rank that asks for a finished errand asks for that record and is read
/// here like any other award, rather than by a flag nobody sets.
/// </para>
/// </remarks>
internal static class PromotionEligibility
{
    /// <summary>Reads every requirement of one rank against the party.</summary>
    /// <param name="rank">The rank whose requirements are read.</param>
    /// <param name="party">The party the requirements are read against.</param>
    /// <param name="giver">Who the party is taking the rank from, or empty when nobody is being spoken with.</param>
    /// <returns>One verdict per requirement, in the order the ladder stated them.</returns>
    internal static IReadOnlyList<PromotionRequirementVerdict> Judge(PromotionRank rank, PartyEntity party, string giver)
    {
        ArgumentNullException.ThrowIfNull(rank);
        ArgumentNullException.ThrowIfNull(party);
        List<PromotionRequirementVerdict> verdicts = [];
        foreach (PromotionRequirement requirement in rank.Requirements)
        {
            verdicts.Add(Judge(requirement, party, giver ?? string.Empty));
        }

        return verdicts;
    }

    /// <summary>Reads one requirement against the party.</summary>
    /// <param name="requirement">What the rank asked for.</param>
    /// <param name="party">The party the requirement is read against.</param>
    /// <param name="giver">Who the party is taking the rank from, or empty.</param>
    /// <returns>The verdict, with the party's own standing in its statement.</returns>
    private static PromotionRequirementVerdict Judge(PromotionRequirement requirement, PartyEntity party, string giver)
    {
        switch (requirement.Kind)
        {
            case PromotionRequirementKind.Giver:
            {
                bool met = string.Equals(requirement.Name, giver, StringComparison.Ordinal);
                string who = giver.Length > 0 ? giver : "nobody";
                return new PromotionRequirementVerdict(
                    requirement,
                    met,
                    met
                        ? requirement.ToString()
                        : $"{requirement}, and the party is speaking with {who}");
            }

            case PromotionRequirementKind.Item:
            {
                int held = party.Inventory.TotalOf(new ItemDefinitionId(requirement.Name));
                bool met = held >= requirement.Amount;
                return new PromotionRequirementVerdict(
                    requirement,
                    met,
                    met
                        ? $"carries {requirement}"
                        : $"needs {requirement}, and the party carries {held.ToString(CultureInfo.InvariantCulture)}");
            }

            case PromotionRequirementKind.Follower:
            {
                bool met = party.Followers.Find(new(requirement.Name)) is not null;
                return new(requirement, met, met ? requirement.ToString() : $"needs {requirement}");
            }

            default:
            {
                int held = party.Records.CountOf(requirement.Name);
                foreach (string alternative in requirement.AlternativeAwards)
                    held = Math.Max(held, party.Records.CountOf(alternative));
                bool met = held >= requirement.Amount;
                return new PromotionRequirementVerdict(
                    requirement,
                    met,
                    met
                        ? $"holds the record of {requirement}"
                        : $"needs the record of {requirement}, and the party's record stands at {held.ToString(CultureInfo.InvariantCulture)}");
            }
        }
    }
}
