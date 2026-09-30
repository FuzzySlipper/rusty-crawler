# tests

The suites, mirroring the product graph. Checked in today are the architecture laws; the kit's
content, session, world, party, resources, time, creation, and movement tests; the host's composition
and travel tests; the importer's reader, writer, map, and media tests; and the DOM companion's tests.
The kit's suite is the only one that loads the compiled ruleset, because creation's races, classes,
and skills are the ruleset's and the content they are checked against is what proves them. The rest
arrive with the stones whose mechanisms they cover.

| Directory | Suite | Answers |
| --- | --- | --- |
| `PartyRpg.Kit.Tests/` | `PartyRpg.Kit.Tests` | Do the reusable mechanisms behave as specified, and do the compiled ruleset's creation definitions hold over the content that must carry them? |
| `PartyRpg.Architecture.Tests/` | `PartyRpg.Architecture.Tests` | Do the ownership laws hold: kit free of ruleset vocabulary, the dependency graph, one declared product entry, and the host identity its project file declares? |
| `PartyRpg.Ui.Tests/` | `PartyRpg.Ui.Tests` | Does the DOM companion render published state, offer each control exactly as published, report the right intents, and hold no state or timer — and does it read every field of the fixtures the product wrote without a problem? |
| `PartyRpg.Host.Tests/` | `PartyRpg.Host.Tests` | Does the product start from the bundle it ships and report it, does content that is present and wrong stop it with every problem named, and is the content this repository ships valid? |
| `MightAndMagic7.Import.Tests/` | `MightAndMagic7.Import.Tests` | Do the readers handle constructed archives, tables, and event programs correctly — both payload wrappers, the writer's size bug, duplicate names, quoted fields, annotation rows, the placeholder destination — does the graph fail when a destination is unknown, do the map and media decoders decode constructed payloads, and are the packs the importer writes packs the product's loader accepts? The recorded inventory itself is checked by `mm7import verify` against the operator's data. |

An architecture suite is not ceremony: it is the automated half of the boundary
rules in `AGENTS.md`, and it is the check that fails when a kit file quietly
gains ruleset vocabulary.

The projection contract is bound across the two languages by fixtures rather than by reading either side's
source. `PartyRpg.Host.Tests/ProjectionContractTests` writes `PartyRpg.Ui.Tests/fixtures/` from the product's
own output — `SessionProjection.Build` over sessions that carry every block, the product itself creating a
party, and `contract.json` with the projection and action contracts, every payload action the session reads,
and every intent the host declares with its key — and fails when the checked-in files differ from what it
builds. Regenerate them with `CRAWLER_WRITE_UI_FIXTURES=1 dotnet test tests/PartyRpg.Host.Tests`, then let the
companion suite judge the change: `projection-contract.test.mjs` mounts every fixture and fails when a reader
meets a field that is missing or of another type, when the panel sends an action `contract.json` does not
list, or when any companion module computes a published quantity or decides on its own whether a control
would be taken. The companion suite's harness (`harness.mjs`) is a mount context built to the Engine's own
shape: frozen ports, the current envelope delivered synchronously on subscribe and `null` when there is none,
and deeply frozen values.

Every checked-in suite is executed by `scripts/verify.sh`, locally and in the `verify` workflow. A case
that needs the operator's imported tables is an `ImportedFact` and reports itself skipped without them;
none returns early and counts as a pass. Temporary probe files
a review lane creates inside a suite are covered by `.gitignore` and are not a
pattern to imitate in committed code.
