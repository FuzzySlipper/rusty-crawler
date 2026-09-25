# Rusty Crawler

Rusty Crawler is the reference repository and proving product for **PartyRpg**.

PartyRpg is an opinionated construction kit and reference host for
party-centric, first-person RPGs in the Might and Magic VI/VII/VIII tradition.
The party is the durable center of gravity: a small band of characters shares one
world, one clock, one purse, and one unfolding expedition.

Might and Magic VI, VII, and VIII are the first compiled ruleset, compatibility
corpus, content source, and game-bundle family. They are not the implicit
PartyRpg architecture.

The working formula is: **Engine guarantees. Kit shapes. Ruleset decides.
Bundle assembles. Host launches.**

> **Current state: foundation stone 4 is in progress.** The spine, the world, the party and its
> resources, the one clock and calendar, character creation, and what a crossing costs have landed:
> a session with imported content walks its party, places it where a scenario says, and settles the
> crossing it takes against the purse, the larder, and the clock. Combat, magic, services, quests,
> and persistence are still to come, no session mode reaches the creation flow yet, and the shipped
> bundle carries no content — so a running product without imported packs reports no world and no
> party. See [`AGENTS.md`](AGENTS.md) for the exact current state.

## Ownership

- Rusty Engine guarantees reusable infrastructure and admitted update services.
- `PartyRpg.Kit` defines the reusable party-RPG composition grammar and the
  ordinary mechanisms needed to construct one.
- `PartyRpg.Host` owns the product lifecycle, built-in ruleset registry,
  shipped bundles, launcher, defaults, and session selection.
- `PartyRpg.Rulesets.MightAndMagic7` owns all Might and Magic VII semantics,
  formulas, identities, presentation meaning, and content interpretation.
- Content packs own authored definitions, assets, maps, placements, quests,
  and scenario state.
- `MightAndMagic7.Import` owns source-format knowledge for the original game's
  data files and for the donors that document them.
- `PartyRpg.Host` is the ordinary product entry. The packaged SDK generates
  CoreCLR and NativeAOT composition beneath ignored `obj` output.

Code-bearing rulesets are compiled into the product. Content packs, validated
typed tuning profiles, and game bundles are loaded at runtime. Do not introduce
dynamic managed plug-in loading, reflection discovery, runtime C# compilation,
generic command buses, ambient dependency lookup, or a replacement gameplay DSL.

Reusable mechanisms, and mechanisms whose placement is genuinely uncertain, begin
in `PartyRpg.Kit`. Might and Magic assumptions are forbidden there and permitted
only in the ruleset, its content packs, its presentation, and
`MightAndMagic7.Import`; the Host may name a built-in ruleset only at its explicit
composition root.

Adjustable ruleset values belong in discoverable validated typed tuning handles;
authored values belong in content packs; algorithmic invariants stay beside their
algorithms; source-format quirks belong in the importer; and default bundle
selection belongs in the Host.

There is one Rusty Engine-admitted update. PartyRpg does not create a parallel
loop, clock, timer, thread, browser authority, or renderer.

## The game family

Might and Magic VII: For Blood and Honor is the game being recreated, and the
only target. VI and VIII share its engine and remain donor context for formats
and divergences; they are not targets. What this repository knows about the game
is recorded, with citations, in [`docs/research/`](docs/research/):

- Party-based first-person play with continuous movement across square outdoor
  regions joined at their edges, and separate interior places — towns, castles,
  dungeons — entered through entrances and doors.
- One session for exploration and combat: real time by default, one key toggling
  turn-based mode, both paced by the same per-character recovery quantity.
- Rules carried by tab-separated tables inside the game's data archives, with
  maps, sprites, and sounds in separate archives: 13 outdoor regions, 63 interior
  places, 276 monsters, 800 item rows, 99 spells across nine schools, 37 skill
  rows, and 36 class ranks.
- A clocked world: travel costs days and food, shops lock at night, monsters
  respawn on multi-day intervals, and spell durations are measured in game time.

The donors are the reference reimplementation at
`/home/research/old-games/OpenEnroth`, the rules and table reference at
`/home/research/old-games/MMExtension`, and the secondary reimplementation
reference at `/home/research/old-games/OpenMM8`. Their licenses differ and none of
them is a code donor — read the donor posture in [`AGENTS.md`](AGENTS.md) before
using any of them. The operator's own copy of the game at
`/home/research/old-games/game-mm7` is the extraction source. Original
game data is never committed here.

## Design shape

Two documents fix the shape the stones are built to:

- [Gameplay design](docs/gameplay-design.md) — the loop, every system's shape
  with a fidelity verdict, the foundations-first building order, non-goals, and
  the nine decisions that are expensive to reverse.
- [Code organization](docs/code-organization.md) — the layering, where new code
  goes, the Kit and ruleset owner maps, content and import shapes, the UI
  contract, session modes, and persistence.

Two deliberate divergences from the original are recorded there and are not to be
"corrected" later: **the party is one entity that owns a single shared
inventory** — a character owns only what it has equipped, so there is no
per-character pack to shuffle — and **approximate fidelity is the normal
verdict**, because the goal is to adapt the game's essence rather than reproduce
its numbers. The build order is foundations first: each capability lands complete
and global, with no vertical slices and no stubs waiting for a reconciliation
pass.

Work is sequenced in Den as nine foundation-stone campaigns
(`rusty-crawler#8454`–`#8462`) with child tasks carrying outcome, scope,
acceptance, and evidence. Den owns status and dependencies; repository documents
do not mirror the task list, because a copied list goes stale and is later read as
current.

## Repository layout

| Path | Holds |
| --- | --- |
| [`AGENTS.md`](AGENTS.md) | The working contract: direction, ownership, boundary rules, donor posture, git and documentation conventions. |
| [`docs/`](docs/README.md) | Durable documents: the [gameplay design](docs/gameplay-design.md), the [code organization](docs/code-organization.md), the [research notes](docs/research/), and the [review lane model](docs/agent-review/README.md). |
| [`src/`](src/README.md) | The product graph: kit, ruleset, host, importer and its tool, and the product DOM companion. |
| [`tests/`](tests/README.md) | The suites, including the architecture suite that enforces the ownership laws. |
| [`content/`](content/README.md) | Loaded content: bundles, authored content packs, and per-region imports produced offline. |
| [`data/`](data/README.md) | Small checked-in reference tables a person maintains. |
| `scripts/` | Engine pair installation and pin movement, and `verify.sh`. |

For every task, identify:

- the owning layer;
- new assumptions introduced;
- whether Might and Magic vocabulary is permitted;
- whether the change is code, tuning, content, import, or infrastructure;
- dependency changes;
- focused proof for the owning mechanism and ruleset policy.

## Develop and verify

The product consumes the immutable `Rusty.Engine` package from the installed
`.runtime/sdk-feed` and the matched `.runtime/runtime-pack`. That pair's identity
belongs in `Directory.Build.props`, where the install and verify scripts check it;
do not restate a version or revision here.

Start a clean checkout with the pinned, noninteractive pair install. It validates
the release checksum, payloads, ABI, package version, and Engine source revision
before atomically replacing the whole ignored pair:

```bash
./scripts/install-engine-pair.sh
```

To take the newest published Engine pair, which is the ordinary way to pick up
newer Engine state:

```bash
./scripts/update-engine-pin.sh
```

It resolves the newest `csharp-sdk` release, rewrites both identities in
`Directory.Build.props`, and installs the pair. `--check` reports what is
available without changing anything.

Routine verification:

```bash
./scripts/verify.sh
```

That verifies the installed pair identity, installs the UI dependencies, runs the DOM companion
tests, builds every product project, runs every suite, and stages the CoreCLR product.
NativeAOT is a separate fidelity target and stays opt-in with `--aot`. The project and suite lists
in the script are explicit on purpose: a discovery-based loop silently stops covering a project that
moved, so a new project is added there in the same change that adds it.

Ordinary development runs the staged product through the runtime pack:

```bash
./.runtime/runtime-pack/bin/rusty dev \
  --project ./src/PartyRpg.Host/PartyRpg.Host.csproj \
  --runtime ./.runtime/runtime-pack
```

The same command is what `.den-serve.json` uses. **This box's own headless browser cannot hold an
interactive session**: a few seconds after it attaches, the host reports
`DEV_HOST_VIDEO_FEEDBACK_UNSUPPORTED` and stops the runtime, so a live check from here confirms the
product-to-DOM leg (the projection renders with real values). A session held through the agent
playtest service's remote browser does keep running and takes the player's keys, which is where the
movement, transition, and travel-cost readings under `local/verify/` come from.

The offline importer reads the operator's own installation and never writes to it:

```bash
dotnet src/MightAndMagic7.Import.Tool/bin/Release/net10.0/mm7import.dll report --install /path/to/mm7
dotnet src/MightAndMagic7.Import.Tool/bin/Release/net10.0/mm7import.dll verify --install /path/to/mm7
```

`report` prints what the containers, tables, event programs, and map graph actually contain;
`verify` checks the readers against the recorded inventory in `docs/research/mm7-data-inventory.md`
and fails when a reader drifts from the data. `scripts/verify.sh` runs `verify` when the installation
is present, and says so plainly when it is not.

`write` produces the content packs the product loads, and proves its own reproducibility:

```bash
dotnet src/MightAndMagic7.Import.Tool/bin/Release/net10.0/mm7import.dll write \
  --install /path/to/mm7 --output content/partyrpg/imports --check-determinism
```

Packs land under `content/partyrpg/imports` (generated, never committed) and are loaded once their ids
are listed in a bundle under `content/partyrpg/bundles`. The product validates that content when it
starts: a bundle naming a pack that is not present stops it with the missing pack named. `write` also
emits each place's collision geometry into the world pack, in the engine's own spatial artifact, and
refuses a place whose solid faces cannot be closed enough for a party to stand on — the shape and the
rules are in [`docs/research/mm7-map-formats.md`](docs/research/mm7-map-formats.md) §8.

Den serves the product through `.den-serve.json` on port 4176.

## Guidance and proof

Repository-specific instructions are in [`AGENTS.md`](AGENTS.md). The installed
SDK's C# guidance is the authority on the product/Engine boundary; this repository
does not restate it.

A check that only compiles is not verification, and a demonstration is not
completion. Run the smallest proof that answers the changed seam, and state
plainly what was not run.
