# tests

The suites, mirroring the product graph, and the one library they share. Each suite references the project it
proves and `PartyRpg.Testing`; no suite references another suite, and no helper is copied between them.

| Directory | Project | Answers |
| --- | --- | --- |
| `PartyRpg.Testing/` | shared support (a library, not a suite) | One definition of each helper: the repository root and the walk every scan takes, the source reader the laws are written in, admitted updates and input events, the clock, travel rules, a recording mover, in-memory content and packs, the engine fakes, the bare session snapshot a projection case starts from, and the projection reader. |
| `PartyRpg.Architecture.Tests/` | `PartyRpg.Architecture.Tests` | Do the ownership laws hold across the whole graph: which peers, packages, and friends each project may have; one product entry; the verify script covering every project; the kit naming no ruleset, donor, source file, or the game's own classes, skills, spells, items, creatures, places, and people; and, over the kit, the ruleset, and the host together, one clock (no wall clock, timer, thread, `Task.Run`, `Parallel`, or `CancelAfter`), safe code only, and one larder — and do the documents hold: `AGENTS.md` under the byte budget it states with no paragraph repeated, and every repository path the top-level documents and project READMEs cite present in a clone and not ignored? |
| `PartyRpg.Kit.Tests/` | `PartyRpg.Kit.Tests` | Do the reusable mechanisms behave as specified over the kit's own fakes — content, session, world, party, resources, time, creation, movement, combat, magic, services, conversation, quests, journal, knowledge, automap, persistence — does every diagnostic the session and the world report arrive by its code, and does each owner's state have exactly the writers its source law allows? It references no ruleset. |
| `PartyRpg.Rulesets.MightAndMagic7.Tests/` | `PartyRpg.Rulesets.MightAndMagic7.Tests` | Does this game's policy answer the kit's seams as stated — creation, combat, hostility, spells and effects, skills, promotions, services, standing, quests, travel, rest, the automap — over content a case stages, with sessions composed over names the suite declares itself? Cases that need the operator's imported tables are `ImportedFact`s. It references no host. |
| `PartyRpg.Host.Tests/` | `PartyRpg.Host.Tests` | Does the product start from the bundle it ships and report it, refuse content that is present and wrong with every problem named, live through its lifecycle (pause, resume, shutdown, restart, an explicit bundle, the real persistence store) as the engine drives it, declare every control in code and in its project file with each key bound to its own intent, and publish the projection the companion's fixtures hold? |
| `MightAndMagic7.Import.Tests/` | `MightAndMagic7.Import.Tests` | Do the readers handle constructed archives, tables, and event programs correctly — both payload wrappers and both directory widths, the writer's size bug, duplicate names, quoted fields, annotation rows, the placeholder destination — do the map and media decoders decode constructed payloads that differ map by map, and are the packs the importer writes packs the product's loader and this game's ruleset accept? The recorded inventory itself is checked by `mm7import verify` against the operator's data. |
| `PartyRpg.Ui.Tests/` | companion suite (`npm run test:ui`) | Does the DOM companion render published state, offer each control exactly as published, report the right intents, and hold no state or timer — and does it read every field of the fixtures the product wrote without a problem? |

## Source laws

A law about the code reads it as syntax bound to symbols (`PartyRpg.Testing/SourceCode`, over Roslyn), never as text:
a law names what it forbids through `typeof` and the member it means, so a target-typed `new(...)`, a rename, and a
comment neither hide a violation nor invent one. Laws over the whole runtime compile the kit, the ruleset, and the
host together from their sources. Every law also proves its detector on code the suite writes, spelled the ways it
must see through, or asserts that it found the allowed site it allows, so a detector that stopped finding anything
cannot pass as a clean product. The one law that reads words — the kit's vocabulary — matches each name as a word or
an identifier's leading word, and proves every name is the game's own by finding it in the ruleset, the importer, or
the research documents.

Every repository scan walks through `Repository.Files`, which never enters `.claude/` (an agent's worktrees),
`bin/`, `obj/`, `node_modules/`, generated UI, or the operator's ignored local tree.

## Tuning a case pins

The ruleset navigation cases also use the pinned pair's safe `EngineTestHost`: one authored wall route
with the canonical controller, and admission of every imported geometry when operator packs are supplied.
Test-project support locates that pair's native library; these cases create no GPU product or second runtime
authority. `CRAWLER_NAVIGATION_REPORT` optionally names a local per-place admission report for a focused run.
The browser pursuit reading remains separate from these mechanism checks.

Tuning is approximate by design. A ruleset case that needs a value with a tuning handle reads the handle's default;
a case that states a tuned value as a literal — a price, a band edge, a level ceiling, a blow's numbers — carries
`[Trait("pins", "tuning")]`, so `dotnet test tests/PartyRpg.Rulesets.MightAndMagic7.Tests --filter pins=tuning`
lists every case a retune will move.

## The projection contract

The projection contract is bound across the two languages by fixtures rather than by reading either side's source.
`PartyRpg.Host.Tests/ProjectionContractTests` writes `PartyRpg.Ui.Tests/fixtures/` from the product's own output —
`SessionProjection.Build` over sessions that carry every block, the product itself creating a party, and
`contract.json` with the projection and action contracts, every payload action the session reads, and every intent
the host declares with its key — and fails when the checked-in files differ from what it builds. Regenerate them with
`CRAWLER_WRITE_UI_FIXTURES=1 dotnet test tests/PartyRpg.Host.Tests`, then let the companion suite judge the change:
`projection-contract.test.mjs` mounts every fixture and fails when a reader meets a field that is missing or of
another type, when the panel sends an action `contract.json` does not list, or when any companion module computes a
published quantity or decides on its own whether a control would be taken. The companion suite's harness
(`harness.mjs`) is a mount context built to the Engine's own shape: frozen ports, the current envelope delivered
synchronously on subscribe and `null` when there is none, and deeply frozen values.

## Running them

Every checked-in suite is executed by `scripts/verify.sh`, locally and in the `verify` workflow, and the shared
library is built as its own step; the architecture suite fails when a project under `tests/` is missing from the
script's `test_projects` or `test_support_projects`. A case that needs the operator's imported tables is an
`ImportedFact` and reports itself skipped without them; none returns early and counts as a pass. Temporary probe
files a review lane creates inside a suite are covered by `.gitignore` and are not a pattern to imitate in committed
code.
