# tests

Planned suites, mirroring the product graph. No suite is checked in yet.

| Directory | Planned suite | Answers |
| --- | --- | --- |
| `PartyRpg.Kit.Tests/` | `PartyRpg.Kit.Tests` | Do the reusable mechanisms behave as specified, independently of any ruleset? |
| `PartyRpg.Rulesets.MightAndMagic.Tests/` | `PartyRpg.Rulesets.MightAndMagic.Tests` | Do the Might and Magic formulas, definitions, and per-game profiles match the documented behavior? |
| `PartyRpg.Architecture.Tests/` | `PartyRpg.Architecture.Tests` | Do the ownership laws hold: kit free of ruleset vocabulary, the dependency graph, the importer outside the runtime, one product project per layer? |
| `PartyRpg.Host.Tests/` | `PartyRpg.Host.Tests` | Does the product lifecycle, selection, and session construction work? |
| `MightAndMagic.Import.Tests/` | `MightAndMagic.Import.Tests` | Do the format readers and normalizers produce the expected packs, including the recorded quirks? |
| `PartyRpg.Ui.Tests/` | `PartyRpg.Ui.Tests` | Do the DOM projections render published state and report intents without owning gameplay state? |

An architecture suite is not ceremony: it is the automated half of the boundary
rules in `AGENTS.md`, and it is the check that fails when a kit file quietly
gains ruleset vocabulary.

Every checked-in suite is executed by `scripts/verify.sh`. Temporary probe files
a review lane creates inside a suite are covered by `.gitignore` and are not a
pattern to imitate in committed code.
