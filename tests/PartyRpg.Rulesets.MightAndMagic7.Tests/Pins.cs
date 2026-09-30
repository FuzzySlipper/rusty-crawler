namespace PartyRpg.Rulesets.MightAndMagic7.Tests;

/// <summary>
/// The trait a case carries when it pins a value this game tunes, so a retune has a known blast radius.
/// </summary>
/// <remarks>
/// Tuning is approximate by design: a value adopted from the donor's tables or authored by this game may be retuned
/// without the mechanism changing. A case that states such a value as a literal — a price, a band edge, a level
/// ceiling, a blow's numbers — carries <c>[Trait(Pins.Trait, Pins.Tuning)]</c>, and
/// <c>dotnet test --filter pins=tuning</c> lists every case a retune will move. Where a value has a tuning handle,
/// a case reads the handle's default instead and needs no trait.
/// </remarks>
internal static class Pins
{
    /// <summary>The trait's name.</summary>
    internal const string Trait = "pins";

    /// <summary>The trait's value for a case that pins tuned values.</summary>
    internal const string Tuning = "tuning";
}
