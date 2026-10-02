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
/// not scale with the number of heads, which is why the member count the interface offers is
/// deliberately unused: multiplying by them would be a rule no donor states.
/// </para>
/// <para>
/// <b>An empty larder weakens every member.</b> That is the donor's short arrival: a journey on foot whose
/// days the larder cannot cover puts the weak condition on every character, and one begun with no food at
/// all does the same (OpenEnroth <c>src/Application/Game.cpp:763-777</c>, the travel-by-foot message). Its
/// day tick also divides health once the larder is empty (<c>src/Engine/Engine.cpp:1051-1058</c>; the weak
/// condition set just above it, <c>1047-1049</c>, is its fatigue rule, which <see cref="MightAndMagic7Rest"/>
/// owns). The condition is applied through the condition every member carries rather than by losing food the
/// party does not have. The donor's health loss is not repeated here: health is spent through a character's
/// own resource owner, and a rule that quietly reached into hit points while reporting a condition would be
/// doing one thing and reporting another.
/// </para>
/// <para>
/// <b>Eating does not end it; rest does.</b> The donor clears the weak condition only on a full rest
/// (<c>src/Engine/Party.cpp:698-721</c>, <c>Party::restAndHeal</c>) and through cures, and this game keeps
/// that end: a completed sleep in <see cref="MightAndMagic7Rest"/> clears it. A fed day clears nothing,
/// because the weak condition is one condition whoever set it — hunger here, or the fatigue rule
/// <see cref="MightAndMagic7Rest"/> owns — and a meal that ended it would end tiredness without rest.
/// </para>
/// <para>
/// The rule owns no count of its own, so the larder remains the only place food is stored.
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
    internal static readonly ConditionId Weakness = MightAndMagic7Conditions.Weak;

    /// <summary>The provisions one day's rations are, which the travel cost is stated in as well.</summary>
    internal static Provisions DayRations => new(RationsPerDay, ProvisionUnit.Portions);

    /// <inheritdoc />
    public Provisions DailyCharge(int members) => DayRations;

    /// <inheritdoc />
    /// <exception cref="ArgumentOutOfRangeException">The party's state cannot be changed; the count is never negative.</exception>
    public ActiveCondition? Consequence(int portionsAfter, int members)
    {
        // The larder still covering a day means nobody goes hungry today; a member an earlier short day
        // weakened stays weak until a rest ends it, as the donor's does.
        return portionsAfter >= RationsPerDay ? null : new ActiveCondition(Weakness, 1);
    }
}
