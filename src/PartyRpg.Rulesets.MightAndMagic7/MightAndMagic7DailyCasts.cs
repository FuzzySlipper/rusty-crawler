using System.Globalization;
using PartyRpg.Kit.Magic;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Time;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// How many times one caster has cast a spell the game limits by the day, counted against the session's one clock.
/// </summary>
/// <remarks>
/// <para>
/// <b>The count is the caster's, and the party's records carry it.</b> The donor keeps a count per character of
/// the divine interventions they cast today and refuses a fourth (OpenEnroth
/// <c>src/Engine/Spells/CastSpellInfo.cpp:2590-2610</c>, <c>uNumDivineInterventionCastsThisDay</c>). This build
/// keeps it as one record in the party's records under the spell, the caster, and the day it counts, so a save
/// carries it as it carries every other record and a load reads the same count back. A count for an earlier day
/// is taken off the record when the next one is written, so a caster carries at most one.
/// </para>
/// <para>
/// <b>The day turns at three in the morning.</b> The donor clears every character's count when the clock passes
/// three o'clock (<c>src/Engine/Engine.cpp:1036-1081</c>, "new day dawns at 3am"), so the day a count belongs to is
/// the clock's calendar day, read as the day before until three. Faithful. The donor also clears the count when a
/// stay heals the party outright (<c>src/Engine/Party.cpp:764-786</c>); this build does not (ours). A session with
/// no clock counts every casting in one day, which is the honest answer for a product that keeps no time.
/// </para>
/// </remarks>
internal static class MightAndMagic7DailyCasts
{
    /// <summary>How many divine interventions one caster may cast a day: <c>CastSpellInfo.cpp:2592</c>.</summary>
    internal const int DivineInterventionsADay = 3;

    /// <summary>The hour the donor's day turns at: <c>Engine.cpp:1036-1038</c>.</summary>
    private const int DayTurnsAtHour = 3;

    /// <summary>How many times a caster has cast a spell on the day the clock stands in.</summary>
    /// <param name="party">The party whose records carry the count.</param>
    /// <param name="caster">The caster.</param>
    /// <param name="spell">The spell.</param>
    /// <param name="clock">The session's one clock, or null for a session that keeps no time.</param>
    internal static int CastsToday(PartyEntity party, PartyMember caster, SpellDefinition spell, GameClock? clock)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(caster);
        return party.Records.CountOf(Record(caster, spell, DayOf(clock)));
    }

    /// <summary>Counts one more casting of a spell by a caster today, forgetting the count of any earlier day.</summary>
    /// <param name="party">The party whose records carry the count.</param>
    /// <param name="caster">The caster.</param>
    /// <param name="spell">The spell.</param>
    /// <param name="clock">The session's one clock, or null for a session that keeps no time.</param>
    /// <returns>How many times they have cast it today, this casting included.</returns>
    internal static int Count(PartyEntity party, PartyMember caster, SpellDefinition spell, GameClock? clock)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(caster);
        string today = Record(caster, spell, DayOf(clock));
        string prefix = Prefix(caster, spell);
        foreach (PartyRecord stale in party.Records.All)
        {
            if (stale.Name.StartsWith(prefix, StringComparison.Ordinal) && !string.Equals(stale.Name, today, StringComparison.Ordinal))
            {
                party.Records.Remove(stale.Name);
            }
        }

        int count = party.Records.CountOf(today) + 1;
        party.Records.Set(today, count);
        return count;
    }

    /// <summary>The record one caster's count of one spell on one day is kept under.</summary>
    private static string Record(PartyMember caster, SpellDefinition spell, long day) =>
        string.Concat(Prefix(caster, spell), day.ToString(CultureInfo.InvariantCulture));

    /// <summary>What every day's record of one caster's count of one spell begins with.</summary>
    private static string Prefix(PartyMember caster, SpellDefinition spell) =>
        string.Concat("spell.per-day.", spell.Id.ToString(), ".", caster.Id.ToString(), ".");

    /// <summary>Which day the clock stands in, as the donor's day turning at three counts it.</summary>
    private static long DayOf(GameClock? clock)
    {
        if (clock is null) return 0;
        GameDate now = clock.Now;
        long day = ((long)now.Year * clock.Calendar.DaysPerYear) + clock.Calendar.DayOfYear(now);
        return now.Hour < DayTurnsAtHour ? day - 1 : day;
    }
}
