# Rusty Crawler

Rusty Crawler is the reference repository and proving product for **PartyRpg**.

PartyRpg is an opinionated construction kit and reference host for
party-centric, first-person RPGs in the Might and Magic VI/VII/VIII tradition.
The party is the durable center of gravity: a small band of characters shares one
world, one clock, one purse, and one unfolding expedition.

Might and Magic VII: For Blood and Honor is the first compiled ruleset, content
source, and game bundle, and the only game recreated; VI and VIII are donor context
only. It is not the implicit PartyRpg architecture.

The working formula is: **Engine guarantees. Kit shapes. Ruleset decides.
Bundle assembles. Host launches.**

> **Current state.** Foundation stones 1 and 2 are closed; stones 3 to 8 (world, party, interaction and
> services, combat, progression and magic, quests and knowledge) have each landed their mechanism and each
> still carries open residue that Den tasks receive; stone 9 (breadth) has not started. With the
> operator's imported packs selected, a session creates or resumes a party, walks it through the imported
> world, pays for crossings, fares and nights, opens doors and containers, drinks from wells and reads
> obelisks and signs, talks, trades, trains, learns and casts spells, fights in real time or in rounds, takes
> and turns in errands, and keeps a dated journal, notes and an automap it can save and resume. The shipped bundle selects no packs, so a product without
> them reports no world and no party. [`AGENTS.md`](AGENTS.md) states the shape and lists the residue with
> each receiver; the project READMEs under [`src/`](src/README.md) hold the per-mechanism detail.

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
| [`docs/`](docs/README.md) | Durable documents: the [gameplay design](docs/gameplay-design.md), the [code organization](docs/code-organization.md), the [research notes](docs/research/), the [live-check procedure](docs/live-checks.md) and its published [evidence](docs/evidence/README.md), and the [review lane model](docs/agent-review/README.md). |
| [`src/`](src/README.md) | The product graph: kit, ruleset, host, importer and its tool, and the product DOM companion. |
| [`tests/`](tests/README.md) | The suites, including the architecture suite that enforces the ownership laws. |
| [`content/`](content/README.md) | Loaded content: the shipped bundle, and the packs the importer writes offline (never committed). |
| `scripts/` | `verify.sh`, the one verification entry. The Engine pair is installed and moved by the Engine's own `rusty` command. |
| [`tools/`](tools/portable-assets-example/README.md) | An independent Engine example product, built by `verify.sh` so it keeps compiling against the pin; not part of the product graph. |

For every task, identify:

- the owning layer;
- new assumptions introduced;
- whether Might and Magic vocabulary is permitted;
- whether the change is code, tuning, content, import, or infrastructure;
- dependency changes;
- focused proof for the owning mechanism and ruleset policy.

## Develop and verify

The product consumes one immutable Engine SDK/runtime pair, pinned by
`<RustyEnginePackageVersion>` in `Directory.Build.props`; do not restate a
version or revision here. The Engine's `rusty` command installs, updates and
runs it. Get `rusty` once with the Engine bootstrap
(`curl -fsSL https://raw.githubusercontent.com/FuzzySlipper/rusty-engine/main/scripts/install-rusty.sh | bash`),
then start a clean checkout with:

```bash
rusty status
rusty install
```

To take the newest published Engine pair, which is the ordinary way to pick up
newer Engine state:

```bash
rusty update
```

It installs the pair, rewrites the pin, and lists the release notes to read.
`rusty update --check` reports what is available without changing anything.

Routine verification:

```bash
./scripts/verify.sh
```

That installs the pinned pair if needed, installs the UI dependencies, compiles the DOM companion
from `src/ui/tsconfig.json` and runs its suite over the compiled modules (`npm run test:ui`), builds
every project in Release, checks the operator's data when the install is present, runs every suite,
and stages the CoreCLR product. Every step runs even when an earlier one fails, and the script ends
with a summary of what passed, what was skipped and why, and what failed, exiting non-zero if anything
did. NativeAOT is a separate fidelity target and stays opt-in with `--aot`. The project and suite lists
in the script are explicit on purpose: a discovery-based loop silently stops covering a project that
moved, so a new project is added there in the same change that adds it. The same script is the
`verify` GitHub workflow, where the operator's data is absent and those checks report skipped.

The companion suite needs a Node that `jsdom` supports (`package.json` `engines`; `.nvmrc` names the
one CI uses).

With the operator's install present (`CRAWLER_MM7_INSTALL`, by default `/home/research/old-games/game-mm7`),
the script also runs `mm7import verify` and `maps`, and writes the packs twice into a scratch root and
compares them. The ruleset suite's cases that check this game's policy against the shipped tables read that
root through `CRAWLER_IMPORTED_CONTENT` and nothing else; without it they report themselves skipped
rather than passing.

Ordinary development runs the product on the pinned runtime:

```bash
rusty dev --project ./src/PartyRpg.Host/PartyRpg.Host.csproj
```

The same command is what `.den-serve.json` uses; `--headless` runs it unattended, and `--live-debug`
opens the engine's debug surface. **The runtime needs a GPU adapter**: `rusty dev` always builds the
engine's renderer and refuses to load without one (a software Vulkan driver such as llvmpipe counts).
**The product draws no world**: the only engine services it uses are UI, spatial, content, random,
diagnostics and persistence, so the frame the renderer presents is empty and the game is the DOM panel
over it. A session driven through the agent playtest service's browser takes the player's keys; with
`--live-debug` the product's `playtest.observe`, `playtest.action` and `interaction.inspect` commands read the
place, pose, facing target, hostiles and each control's key and availability without scraping the panel.

The offline importer reads the operator's own installation and never writes to it:

```bash
dotnet src/MightAndMagic7.Import.Tool/bin/Release/net10.0/mm7import.dll report --install /path/to/mm7
dotnet src/MightAndMagic7.Import.Tool/bin/Release/net10.0/mm7import.dll verify --install /path/to/mm7
```

`report` prints what the containers, tables, event programs, and map graph actually contain;
`verify` checks the readers against the recorded inventory in `docs/research/mm7-data-inventory.md`,
and decodes every map, writes every pack and extracts the media into a scratch directory it deletes, to
check the figures the documents state about what an import yields; it fails when either drifts from the
data. `scripts/verify.sh` runs `verify` when the installation is present, and reports it skipped when it
is not.

`encounters` prints the opposition the levels' own spawn records ask for, which is the read-only half of
the monster import:

```bash
dotnet src/MightAndMagic7.Import.Tool/bin/Release/net10.0/mm7import.dll encounters --install /path/to/mm7
```

Over the operator's own installation that is 3,175 spawn records of which 1,843 ask for an actor, 1,800
encounters emitted into 72 places, and 43 records refused by name because the encounter slot they name is
one their map leaves empty. The importer chooses no grade and no count: 1,775 encounters leave both to the
ruleset, which draws them through the engine's keyed random service under the place and the spawn record
when the place is populated, so the creatures are reported as the range the slots allow — 1,900 at the
fewest and 5,458 at the most. `write` states the same counts in its summary.

`write` produces the content packs the product loads, and proves its own reproducibility:

```bash
dotnet src/MightAndMagic7.Import.Tool/bin/Release/net10.0/mm7import.dll write \
  --install /path/to/mm7 --output content/partyrpg/imports --check-determinism
```

Packs land under `content/partyrpg/imports` (generated, never committed) and are loaded once their ids
are listed in a bundle under `content/partyrpg/bundles`. The bundle is the selection: only the packs it
names contribute definitions, placements, the scenario's start, and the scenario's party, so a pack nobody
selected changes nothing about what plays — while the whole root is still validated when the product
starts, and a bundle naming a pack that is not present stops it with the missing pack named. What the
selection must state once it states as a set rather than as a sequence: two scenario starts, or two
scenario parties, are refused with every candidate named rather than the first one playing because its
pack loaded first. `write` also
emits each place's collision geometry into the world pack, in the engine's own spatial artifact, and
refuses a place whose solid faces cannot be closed enough for a party to stand on — the shape and the
rules are in [`docs/research/mm7-map-formats.md`](docs/research/mm7-map-formats.md) §8.

Den serves the product through `.den-serve.json` (preferred port 4176, `--live-debug`). The procedure for
a live check — serving a checkout of its own, staging content and a pose, driving the product and reading
it through the playtest commands and the panel, and restoring tracked content afterwards — is in
[`docs/live-checks.md`](docs/live-checks.md); published readings are in [`docs/evidence/`](docs/evidence/README.md).

## Guidance and proof

Repository-specific instructions are in [`AGENTS.md`](AGENTS.md). The installed
SDK's C# guidance is the authority on the product/Engine boundary; this repository
does not restate it.

A check that only compiles is not verification, and a demonstration is not
completion. Run the smallest proof that answers the changed seam, and state
plainly what was not run.
