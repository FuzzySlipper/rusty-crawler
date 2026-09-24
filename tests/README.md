# tests

The suites, mirroring the product graph. Checked in today are the architecture laws, the kit's
session-shell and input-router tests, and the DOM companion's tests; the rest arrive with the stones
whose mechanisms they cover.

| Directory | Suite | Answers |
| --- | --- | --- |
| `PartyRpg.Kit.Tests/` | `PartyRpg.Kit.Tests` | Do the reusable mechanisms behave as specified, independently of any ruleset? |
| `PartyRpg.Architecture.Tests/` | `PartyRpg.Architecture.Tests` | Do the ownership laws hold: kit free of ruleset vocabulary, the dependency graph, one declared product entry, and the host identity its project file declares? |
| `PartyRpg.Ui.Tests/` | `PartyRpg.Ui.Tests` | Does the DOM companion render published state, report the right intents, and hold no state or timer? |
| `PartyRpg.Rulesets.MightAndMagic7.Tests/` | `PartyRpg.Rulesets.MightAndMagic7.Tests` | Do the formulas, ceilings, promotion rules, and per-system fidelity verdicts match what `docs/gameplay-design.md` and the cited evidence say? |
| `PartyRpg.Host.Tests/` | `PartyRpg.Host.Tests` | Does the product lifecycle, selection, and session construction work? |
| `MightAndMagic7.Import.Tests/` | `MightAndMagic7.Import.Tests` | Do the format readers and normalizers produce the expected packs, including the recorded quirks? |

An architecture suite is not ceremony: it is the automated half of the boundary
rules in `AGENTS.md`, and it is the check that fails when a kit file quietly
gains ruleset vocabulary.

Every checked-in suite is executed by `scripts/verify.sh`. Temporary probe files
a review lane creates inside a suite are covered by `.gitignore` and are not a
pattern to imitate in committed code.
