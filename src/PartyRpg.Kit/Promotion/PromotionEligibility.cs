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
/// <b>An errand is the one kind this build cannot judge.</b> Nothing yet owns a quest's state, so a rank
/// that asks for a finished errand is unmet here and says so in its own words: the requirement is stated by
/// the ladder, refused by name, and carried to the owner that will judge it rather than being faked by a
/// flag nobody sets.
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

            case PromotionRequirementKind.Award:
            {
                int held = party.Effects.MagnitudeOf(new EffectId(requirement.Name));
                bool met = held >= requirement.Amount;
                return new PromotionRequirementVerdict(
                    requirement,
                    met,
                    met
                        ? $"holds the record of {requirement}"
                        : $"needs the record of {requirement}, and the party's record stands at {held.ToString(CultureInfo.InvariantCulture)}");
            }

            default:
            {
                // The errand kind is stated and not judged: the owner that will judge it does not exist yet,
                // and this says which requirement is waiting rather than treating it as met or as an absence.
                return new PromotionRequirementVerdict(
                    requirement,
                    false,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"needs {requirement}, and nothing in this build owns an errand's state to judge it"));
            }
        }
    }
}
