namespace PartyRpg.Kit.Party;

/// <summary>What one wound did to one character, as the party's own damage entry reports it.</summary>
/// <remarks>
/// The three numbers are three different facts a caller acts on differently: what was taken off the pool,
/// how far past empty the harm went, and what the character's own health made of it. A trap wants to say who
/// it caught, a fight wants to know whether the target went down, and a panel wants the condition by name,
/// so all three travel together rather than being recomputed by each caller.
/// </remarks>
/// <param name="Taken">How much harm landed.</param>
/// <param name="HitPoints">What the character has left afterwards.</param>
/// <param name="Deficit">How much of the harm went past empty, which is zero while the pool had room.</param>
/// <param name="Condition">The condition the wound left, or null when the character is still standing.</param>
public readonly record struct CharacterWound(int Taken, int HitPoints, int Deficit, ActiveCondition? Condition)
{
    /// <summary>Whether this wound is what laid the character out.</summary>
    public bool Fell => Condition is not null;
}
