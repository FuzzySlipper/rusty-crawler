using PartyRpg.Kit.Party;
using PartyRpg.Kit.World;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// This game's larder policy: what a day on the road costs the party, and what hunger does to it.
/// </summary>
/// <remarks>
/// <para>
/// <b>One food unit a day, whatever the party's size.</b> The donor's food store is the party's own single
/// number and its day tick takes exactly one unit from it while it holds any (OpenEnroth
/// <c>src/Engine/Engine.cpp:1047-1053</c>: a new day takes one food; <c>src/Engine/Party.cpp:275-295</c>:
/// <c>SetFood</c> clamps at zero and never goes negative). The manual says the same in words — an
/// overland crossing "takes several days and consumes 1 food unit per day", and camping on grass consumes
/// one unit (p.26 and p.24 of <c>docs/research/mm7-manual-outline.md</c>). The day's charge therefore does
/// not scale with the number of heads, which is why the members and followers the interface offers are
/// deliberately unused: multiplying by them would be a rule no donor states.
/// </para>
/// <para>
/// <b>An empty larder weakens every member.</b> That is the donor's starving case (OpenEnroth
/// <c>src/Engine/Engine.cpp:1053-1058</c> puts the weak condition on every character and, with no food
/// left, starts dividing health), and it is applied through the condition every member carries rather than
/// by losing food the party does not have. The donor's health loss is not repeated here: health is spent
/// through a character's own resource owner, and a rule that quietly reached into hit points while
/// reporting a condition would be doing one thing and reporting another.
/// </para>
/// <para>
/// <b>A fed day ends the hunger.</b> The rule is stated here because this is where hunger starts: the
/// ledger asks this rule what a larder at the level it was left at does to the party, and a larder that
/// still covers a day's rations means the party ate. The donor clears conditions through rest and cures
/// rather than through eating, so this is our rule rather than its number — and it is stated where a
/// recovery owner will read it when rest and temples arrive.
/// </para>
/// <para>
/// The party is held because hunger is the party's state and this rule is what both applies and ends it;
/// the rule owns no count of its own, so the larder remains the only place food is stored.
/// </para>
/// </remarks>
internal sealed class MightAndMagic7Provisions : IProvisionDayRule
{
    /// <summary>
    /// How much food one day of travelling or camping takes.
    /// </summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/Engine.cpp:1052</c> — one unit per day, taken while the larder holds any —
    /// and the manual's "1 food unit per day" for overland travel (p.26).
    /// </remarks>
    internal const int RationsPerDay = 1;

    /// <summary>The condition hunger puts on a member, as this game names it.</summary>
    internal static readonly ConditionId Weakness = new("weak");

    private readonly PartyEntity _party;

    /// <summary>Creates the rule over the party whose hunger it speaks for.</summary>
    /// <param name="party">The party that owns the larder this rule prices and the members it weakens.</param>
    /// <exception cref="ArgumentNullException">No party was supplied.</exception>
    internal MightAndMagic7Provisions(PartyEntity party) =>
        _party = party ?? throw new ArgumentNullException(nameof(party));

    /// <summary>The provisions one day's rations are, which the travel cost is stated in as well.</summary>
    internal static Provisions DayRations => new(RationsPerDay, ProvisionUnit.Portions);

    /// <inheritdoc />
    public Provisions DailyCharge(int members, int followers) => DayRations;

    /// <inheritdoc />
    /// <exception cref="ArgumentOutOfRangeException">The party's state cannot be changed; the count is never negative.</exception>
    public ActiveCondition? Consequence(int portionsAfter, int members, int followers)
    {
        if (portionsAfter >= RationsPerDay)
        {
            // The larder still covers a day, so nobody goes hungry: a member who was weakened by an earlier
            // short day is fed again and the condition ends here.
            foreach (PartyMember member in _party.Members) member.Conditions.Clear(Weakness);
            return null;
        }

        return new ActiveCondition(Weakness, 1);
    }
}
