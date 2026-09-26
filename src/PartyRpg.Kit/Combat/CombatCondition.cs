using PartyRpg.Kit.Party;

namespace PartyRpg.Kit.Combat;

/// <summary>What one landed attack does to its target besides harm: a condition, and where it came from.</summary>
/// <remarks>
/// <para>
/// A condition is the party's own state — an identity and a severity — so this pairs one with the sentence
/// that says what inflicted it. A fight applies conditions through this value and never by editing a
/// character's numbers: <em>poisoned by the bite of a giant rat</em> is something a player can read, act on,
/// and have cured, while a silent subtraction from a pool is none of those.
/// </para>
/// <para>
/// <b>The source is what makes it observable.</b> The same condition can arrive from a monster's bite, a
/// spell, or a sprung trap, and which of them happened is the first thing a panel says and the last thing a
/// bare condition list could tell it. The sentence is display text and nothing branches on it.
/// </para>
/// </remarks>
/// <param name="Condition">Which condition the attack leaves.</param>
/// <param name="Severity">How severe the case is, as the ruleset judged it.</param>
/// <param name="Source">What inflicted it, in a sentence a person reads.</param>
public sealed record CombatCondition(ConditionId Condition, int Severity, string Source)
{
    /// <summary>Which condition the attack leaves.</summary>
    public ConditionId Condition { get; } = Condition;

    /// <summary>How severe the case is.</summary>
    public int Severity { get; } = Severity;

    /// <summary>What inflicted it, in a sentence a person reads.</summary>
    public string Source { get; } = Source;

    /// <inheritdoc />
    public override string ToString() => Severity > 0
        ? $"{Condition} ({Severity}) from {Source}"
        : $"{Condition} from {Source}";
}
