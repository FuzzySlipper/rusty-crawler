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

> **Current state: setup pass.** This repository owns its shape, its build
> configuration, its verification script, and its donor research. It contains no
> C# project, no TypeScript, and no content yet — the directory structure states
> intended ownership so the first implementation task has an owning home. Do not
> read the layout below as a description of working code.

## Ownership

- Rusty Engine guarantees reusable infrastructure and admitted update services.
- `PartyRpg.Kit` will define the reusable party-RPG composition grammar and the
  ordinary mechanisms needed to construct one.
- `PartyRpg.Host` will own the product lifecycle, built-in ruleset registry,
  shipped bundles, launcher, defaults, and session selection.
- `PartyRpg.Rulesets.MightAndMagic` will own all Might and Magic VI/VII/VIII
  semantics, formulas, identities, per-edition profiles, presentation meaning,
  and content interpretation.
- Content packs will own authored definitions, assets, maps, placements, quests,
  and scenario state.
- `MightAndMagic.Import` will own source-format knowledge for the original games'
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
`MightAndMagic.Import`; the Host may name a built-in ruleset only at its explicit
composition root.

Adjustable ruleset values belong in discoverable validated typed tuning handles;
authored values belong in content packs; algorithmic invariants stay beside their
algorithms; source-format quirks belong in the importer; and default bundle
selection belongs in the Host.

There is one Rusty Engine-admitted update. PartyRpg does not create a parallel
loop, clock, timer, thread, browser authority, or renderer.

## The game family

Might and Magic VI, VII, and VIII share one engine and one gameplay skeleton, and
diverge per edition — so the ruleset treats edition identity as a first-class
value instead of splitting into three rulesets. What this repository currently
knows about that family is recorded, with donor file paths, in
[`docs/research/`](docs/research/):

- Party-based first-person play with free movement over a gridded outdoor world,
  and separate outdoor and indoor map formats with per-map respawn deltas.
- Real-time combat paced by per-actor recovery times, plus a switchable
  turn-based mode driven by an initiative queue: two modes of one session, not
  two products.
- Rules carried mostly by tab-separated tables inside the games' data archives,
  with maps and assets in separate archives.
- Per-edition divergence in party capacity, classes, skills and mastery, spell
  schools, promotion paths, event identifiers, table columns, and world content.

The donors are the reference reimplementation at
`/home/research/old-games/OpenEnroth`, the rules and table reference at
`/home/research/old-games/MMExtension`, and the secondary reimplementation
reference at `/home/research/old-games/OpenMM8`. Their licenses differ and none of
them is a code donor — read the donor posture in [`AGENTS.md`](AGENTS.md) before
using any of them. Original game data is operator-supplied and is never
committed here.

## Repository layout

| Path | Holds |
| --- | --- |
| [`AGENTS.md`](AGENTS.md) | The working contract: direction, ownership, boundary rules, donor posture, git and documentation conventions. |
| [`docs/`](docs/README.md) | Durable documents: the [donor surveys](docs/research/), the [review lane model](docs/agent-review/README.md), and the index of what is still to be written. |
| [`src/`](src/README.md) | The planned product graph: kit, ruleset, host, importer and its tool, and the product DOM companion. |
| [`tests/`](tests/README.md) | The planned suites, including the architecture suite that will enforce the ownership laws. |
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

The product will consume the immutable `Rusty.Engine` package from the installed
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

Today that verifies the installed pair identity and installs the product UI
dependencies, then reports plainly that no product project exists to build.
NativeAOT is a separate fidelity target and stays opt-in with `--aot`. When the
first project lands, add it to `product_projects` and its suite to
`test_projects` in the script — the lists are explicit on purpose, because a
discovery-based loop silently stops covering a project that moved.

Den serves the product through `.den-serve.json` on port 4176 once the host
project exists.

## Guidance and proof

Repository-specific instructions are in [`AGENTS.md`](AGENTS.md). The installed
SDK's C# guidance is the authority on the product/Engine boundary; this repository
does not restate it.

A check that only compiles is not verification, and a demonstration is not
completion. Run the smallest proof that answers the changed seam, and state
plainly what was not run.
