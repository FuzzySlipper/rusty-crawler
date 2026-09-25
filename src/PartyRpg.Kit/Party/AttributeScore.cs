namespace PartyRpg.Kit.Party;

/// <summary>One attribute's value for one character.</summary>
/// <remarks>
/// A named value rather than a field of a fixed struct, so the set of attributes a character has is the
/// game's declaration and never this layer's vocabulary. The value may fall below the starting score —
/// draining and ageing are real — so no lower bound is stated here; what a floor means is ruleset policy.
/// </remarks>
/// <param name="Attribute">Which attribute this is a score for.</param>
/// <param name="Value">The score's current value.</param>
public readonly record struct AttributeScore(AttributeId Attribute, int Value);
