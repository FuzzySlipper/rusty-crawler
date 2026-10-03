using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Time;

/// <summary>What one kind of stop would be here and now, judged before it is taken (<see cref="PartyRest.Judge"/>).</summary>
/// <param name="Kind">The kind of stop.</param>
/// <param name="Period">How long it would last; none when it would be refused.</param>
/// <param name="Charge">What it would take from the larder; none for a wait or a refused stop.</param>
/// <param name="Refusal">Why the party would not take it, or null when it would.</param>
/// <param name="Unrestored">The members a completed sleep would leave as they are, each with the ruleset's reason.</param>
public sealed record RestOffer(RestKind Kind, GameDuration Period, Provisions Charge, Refusal? Refusal, IReadOnlyList<string> Unrestored)
{
    /// <summary>Whether the party would take the stop.</summary>
    public bool IsOffered => Refusal is null;
}
