using PartyRpg.Kit.Party;
using PartyRpg.Kit.Time;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// How old a character is in this game, and what age does to the scores a fight reads.
/// </summary>
/// <remarks>
/// <para>
/// <b>Age is a natural part and an unnatural part.</b> The donor reads a character's age as the years from their
/// birth year to the clock's own year plus an age modifier that a creature's touch, a divine intervention, and a
/// potion of rejuvenation move (<c>OpenEnroth/src/Engine/Objects/Character.cpp:1890-1892</c>, <c>GetBaseAge</c>,
/// and <c>sAgeModifier</c> at <c>:733</c>). The unnatural part is the character's own state, which progression
/// owns and a save carries (<see cref="CharacterProgression.AgeOffset"/>); the natural part is read here.
/// </para>
/// <para>
/// <b>Ours: every character is born the same year.</b> The donor draws a birth year five years either way of
/// 1147 at creation (<c>Character.cpp:2891</c>, <c>1147 - grng-&gt;random(6)</c>, against a starting year of
/// 1168), which makes a new character twenty-one to twenty-six. This build draws none and reads every
/// character as the youngest of those, twenty-one at the clock's first year and a year older for each year the
/// clock has run: the first step of the donor's ageing table is fifty, so the five years the draw would add
/// change nothing a party can meet without being aged by something.
/// </para>
/// </remarks>
internal static class MightAndMagic7Ageing
{
    /// <summary>How old a character is at the clock's first year, the youngest the donor's draw makes one.</summary>
    internal const int StartingAge = 21;

    /// <summary>How many years a divine intervention ages its caster: <c>CastSpellInfo.cpp:2603-2607</c>.</summary>
    internal const int DivineInterventionYears = 10;

    /// <summary>
    /// The most unnatural years a divine intervention leaves: the donor stops its own ageing at a modifier of a
    /// hundred and twenty (<c>CastSpellInfo.cpp:2603-2607</c>).
    /// </summary>
    internal const int MostUnnaturalYears = 120;

    /// <summary>The ages at which the donor's ageing table takes its next step: <c>Character.cpp:232</c>.</summary>
    private static readonly int[] Steps = [50, 100, 150];

    /// <summary>
    /// What each step of age leaves of a score, in percent, by the donor's own table (<c>Character.cpp:222-230</c>,
    /// <c>pAgingAttributeModifier</c>): the first entry applies from the first step, and so on.
    /// </summary>
    private static readonly Dictionary<string, int[]> Shares = new(StringComparer.Ordinal)
    {
        [MightAndMagic7Combat.MightAttribute.Value] = [100, 75, 40],
        [MightAndMagic7Combat.IntellectAttribute.Value] = [100, 150, 100],
        [MightAndMagic7Combat.PersonalityAttribute.Value] = [100, 150, 100],
        [MightAndMagic7Health.EnduranceAttribute.Value] = [100, 75, 40],
        [MightAndMagic7Combat.AccuracyAttribute.Value] = [100, 100, 40],
        [MightAndMagic7Combat.SpeedAttribute.Value] = [100, 100, 40],
        [MightAndMagic7Combat.LuckAttribute.Value] = [100, 100, 100],
    };

    /// <summary>How old a character is now.</summary>
    /// <param name="member">The character.</param>
    /// <param name="clock">The session's one clock, or null for a session that keeps no time.</param>
    /// <returns>Their age in years.</returns>
    internal static int AgeOf(PartyMember member, GameClock? clock)
    {
        ArgumentNullException.ThrowIfNull(member);
        int natural = StartingAge + (clock is { } time ? Math.Max(0, time.Now.Year - time.Start.Year) : 0);
        return natural + member.Progression.AgeOffset;
    }

    /// <summary>What a score is worth at an age, before anything a spell or an item adds to it.</summary>
    /// <remarks>
    /// The donor multiplies the score the character carries by the share its age leaves, and adds what magic and
    /// items add afterwards, so a boost is never aged (<c>Character.cpp:729-765</c>). Faithful.
    /// </remarks>
    /// <param name="attribute">The score.</param>
    /// <param name="score">What the character carries.</param>
    /// <param name="age">How old the character is.</param>
    /// <returns>What the score is worth at that age.</returns>
    internal static int Aged(AttributeId attribute, int score, int age)
    {
        int step = Steps.Count(threshold => age >= threshold);
        if (step == 0 || !Shares.TryGetValue(attribute.Value, out int[]? shares)) return score;
        return score * shares[step - 1] / 100;
    }
}
