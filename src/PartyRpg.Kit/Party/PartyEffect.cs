namespace PartyRpg.Kit.Party;

/// <summary>One effect acting on the whole party, with the magnitude it acts at.</summary>
/// <remarks>
/// A party-wide effect is state, not a formula: what it changes, whether a stronger casting replaces a
/// weaker one, and what ends it are ruleset policy, while the party is the thing that holds it because the
/// effect applies to the band rather than to one member. Timed effects gain their deadline from the one
/// clock when a clock reports game time; until one does, an effect here lasts until it is removed, and this
/// layer invents no unit to count it in.
/// </remarks>
/// <param name="Effect">Which effect definition is acting.</param>
/// <param name="Magnitude">The magnitude it acts at.</param>
public readonly record struct PartyEffect(EffectId Effect, int Magnitude);
