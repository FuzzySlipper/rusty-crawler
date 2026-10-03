namespace PartyRpg.Kit.Combat;

/// <summary>One applied order of a fight, numbered in the order the fight resolved it.</summary>
/// <param name="Serial">Its place in the fight's sequence of applied orders; a later blow has a greater serial.</param>
/// <param name="Result">What the order did: who acted, at whom, how, and what the resolution decided.</param>
public sealed record CombatBlow(long Serial, CombatResult Result);
