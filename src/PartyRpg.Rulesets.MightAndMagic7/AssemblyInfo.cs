using System.Runtime.CompilerServices;

// The ruleset's policy types are internal because nothing outside the product composes them: the host
// selects the compiled ruleset and the ruleset composes its own clock, party, provisions, and travel cost
// behind that one public entry. The host's own suite is what proves those policies against content, and it
// is the one friend named here; a second friend would be a second answer to who may reach into the rules.
[assembly: InternalsVisibleTo("PartyRpg.Host.Tests")]
