using PartyRpg.Kit.Party;

namespace PartyRpg.Kit.Persistence;

/// <summary>The mechanism whose meaning a carried deadline has.</summary>
public enum DeadlineKind
{
    /// <summary>The party's next debt of sleep.</summary>
    Fatigue,
    /// <summary>The end of an effect on the party or one member.</summary>
    SpellEffect,
    /// <summary>The next restock of a visited counter.</summary>
    ServiceRestock,
}

/// <summary>A deadline's durable meaning, without its transient clock handle.</summary>
/// <param name="Kind">The owner that rebuilds it.</param>
/// <param name="Subject">The effect or service identity; empty for the debt of sleep.</param>
/// <param name="DueElapsedMilliseconds">The game time since session start when it is due.</param>
/// <param name="RepeatMilliseconds">The repeat interval, including a debt re-armed by its owner.</param>
/// <param name="Member">The carrier of a member effect; otherwise null.</param>
public sealed record DeadlineSave(
    DeadlineKind Kind,
    string Subject,
    long DueElapsedMilliseconds,
    long? RepeatMilliseconds = null,
    PartyMemberId? Member = null)
{
    /// <summary>The non-null durable subject; empty for fatigue.</summary>
    public string Subject { get; init; } = Subject ?? throw new ArgumentNullException(nameof(Subject));
}
