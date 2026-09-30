using System.Runtime.CompilerServices;

// The ruleset's policy types are internal because nothing outside the product composes them: the host
// selects the compiled ruleset and the ruleset composes its own clock, party, provisions, and travel cost
// behind that one public entry. The ruleset's own suite is what proves those policies against content. The
// host's suite is the second friend, for one reason only: a product it creates must be handed the class and
// skill tables this game's creation offers, and it stages them from the ruleset's own creation tables so the
// fixture cannot drift from what creation offers. The architecture suite holds this list to exactly these two.
[assembly: InternalsVisibleTo("PartyRpg.Rulesets.MightAndMagic7.Tests")]
[assembly: InternalsVisibleTo("PartyRpg.Host.Tests")]
