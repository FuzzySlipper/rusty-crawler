# Rusty Crawler / PartyRpg product guidance

## Direction and authority

Rusty Crawler is the reference repository and proving product for **PartyRpg**:
an opinionated construction kit and reference host for party-centric,
first-person RPGs in the Might and Magic VI/VII/VIII tradition. The party is the
durable center of gravity: a small band of characters shares one world, one
clock, one purse, and one unfolding expedition. Classes, skills, spells, quests,
services, and combat exist to develop that band and move it through the world,
rather than the world existing as a backdrop for a single hero.

The durable formula is:

> **Engine guarantees. Kit shapes. Ruleset decides. Bundle assembles. Host launches.**

Might and Magic VII: For Blood and Honor is the first compiled ruleset, content
source, and game-bundle family, and the only emulated game. It is not implicit
PartyRpg architecture. VI and VIII share its engine and remain useful donor
context for formats and divergences; they are not targets, and no code path may
quietly depend on their data.

- Crawler checkout: `/home/dev/rusty-crawler`
- paired Engine checkout: `/home/dev/rusty-engine`
- Crawler Den project: `rusty-crawler`
- primary donor reference (behavior, formats, rules): `/home/research/old-games/OpenEnroth`
- rules and table reference (game knowledge): `/home/research/old-games/MMExtension`
- secondary reimplementation reference: `/home/research/old-games/OpenMM8`
- donor surveys: `docs/research/openenroth-survey.md` and
  `docs/research/mmextension-openmm8-survey.md`
- experience outline with manual citations: `docs/research/mm7-manual-outline.md`
- extracted data inventory with donor citations: `docs/research/mm7-data-inventory.md`
- design shape: `docs/gameplay-design.md` and `docs/code-organization.md`
- repository shape, current state, and how to develop: `README.md`

Before substantial work, resolve the current Den task and project guidance. Den
owns live task status and dependencies. If it is unreachable, report the failed
read; do not invent task records or infer dependency completion from source or
Git. Continue work whose scope and authority are already established, pausing
only decisions or actions that depend on unavailable Den information.

## Fidelity: similar, not a remake

The product is a **semi-equivalent recreation of Might and Magic VII** — not a
remake and not a compatibility project. The target is that a player who knows the
original recognizes its shape: the party, the world, the services, the systems,
and the way a session unfolds. Exact numbers, byte-level formats, and original
file compatibility are not goals.

- **Match the structure.** Party play, first-person exploration, the
  real-time/turn-based combat pair, skill and mastery progression, spell schools,
  town services, the calendar, travel between regions and dungeons, quests and
  journal, promotions.
- **Approximate the tuning.** Formulas, stat tables, and monster and item values
  may be adopted from extracted data where that is cheap, retuned, or
  simplified. A task states which values are faithful and which are ours; never
  claim fidelity that was not checked.
- **Out of scope.** Reading or writing original save games, loading the original
  executables or their extension/plugin ecosystem, byte-exact map geometry, and
  any promise that original mods, trainers, or editors keep working.

Donor and extracted data inform the design; they do not bind it. Where a task's
required behavior and the original game disagree, the task and the design
documents decide, and the difference is recorded rather than silently rounded.

## Current state

**This repository is a setup pass: it owns its shape and its donor research, and
it implements nothing yet.** Concretely:

- There is no C# project, no TypeScript, and no content. The directories exist so
  the first implementation task has an owning home.
- Every directory README states *intended* ownership. Those statements are
  declarations for planning, not descriptions of working code.
- `scripts/verify.sh` verifies the pinned Engine pair and the UI toolchain and
  reports plainly that no product project exists to build.
- The shape is written down and binding: `docs/gameplay-design.md` fixes the
  loop, each system, and its fidelity verdict; `docs/code-organization.md` fixes
  the owners, the modes, and where new code goes. Both describe intent, not
  running code.
- Do not describe, review, or accept behavior this repository has not
  implemented, and do not let a planned owner's name imply that it runs.

When the first product project lands, update this section, `README.md`, and the
owning directory README together.

## Current product graph

| Owner | Responsibility |
| --- | --- |
| `PartyRpg.Kit` | Reusable and reasonably uncertain party-RPG mechanisms: typed IDs, compiled ruleset/session contracts, bundle/content-pack/tuning resolution, party roster and members, shared inventory and currency, attributes, skills, spells and casting workflows, conditions and recovery, progression bookkeeping, combat execution in real-time and turn-based modes, targeting, monster presence and AI coordination, corpse and loot machinery, containers and doors, NPC conversation, shops and services, quests and journal state, world and spatial session stepping, structured UI values. It is not a universal RPG framework. |
| `PartyRpg.Rulesets.MightAndMagic7` | Might and Magic VII identities, classes, races, skills and mastery, spell schools and formulas, monster and item definitions, combat and reward formulas, time and calendar policy, service and training policy, quest and guild policy, promotion and path policy, content interpretation, presentation meaning, save meaning, and session composition. |
| `PartyRpg.Host` | Product lifecycle, explicit built-in ruleset/bundle selection, product defaults, and the one ordinary product entry. It may select Might and Magic; it never interprets Might and Magic rules or reads original game data. |
| `MightAndMagic7.Import` | Offline knowledge of the original game's data files and of the donors that document them: source formats, conversion quirks, provenance, normalization into packs, and differential validation against the donor reimplementation. Not a runtime dependency. |
| `MightAndMagic7.Import.Tool` | The operator-facing command line that drives the importer and writes normalized packs. |
| Content packs | Authored classes, skills, spells, monsters, items, services, quests, encounters, maps, placements, assets, and scenario state interpreted by a ruleset. |
| TypeScript UI | Thin DOM presentation of Engine-delivered projections and semantic actions. It owns neither gameplay state nor game-world rendering. |

Rulesets are **compiled** into the SDK-generated product composition. Content
packs, typed tuning profiles, and game bundles are **loaded**. Adding code-bearing
ruleset semantics requires a product rebuild; changing valid content or tuning
does not. Do not add runtime assembly loading, reflection discovery,
`Assembly.Load`, ambient `Resolve<T>()` lookup, generic command buses, a new
gameplay DSL, or a universal plug-in ABI. Named, explicitly composed Kit services
and typed RuleEvents are encouraged where they make gameplay ownership and
contribution discoverable.

The concrete project graph, and the seam between kit and ruleset, are planning
decisions. `src/README.md` and the per-project READMEs record the current
intention.

## Kit, Might and Magic, and tuning rules

Reusable mechanisms begin in `PartyRpg.Kit`; when placement is genuinely
uncertain, prefer Kit and keep concrete Might and Magic policy behind
ruleset-owned definitions and configuration. Do not make Kit universal, and do
not move ruleset vocabulary into it merely by renaming it.

`PartyRpg.Kit` must not contain Might and Magic vocabulary. The forbidden set
includes the game and ruleset names (`MightAndMagic`, `MightAndMagic7`, MM6, MM7,
MM8), world and place names from those games, their class/skill/spell/item/monster
names, donor project names (`OpenEnroth`, `MMExtension`, `OpenMM8`), and source
file names (`.lod`, `.odm`, `.ddm`, `.blv`, `.dlv`, `events.lod`, `games.lod`).
An architecture suite enforces this list once the projects exist; until then the
rule is a review obligation, not a checked one.

Might and Magic assumptions are legal only in the ruleset, Might and Magic
content packs, Might and Magic presentation, and `MightAndMagic7.Import`. The Host
may select a built-in Might and Magic ruleset and bundle only at its explicit
catalog/default composition seam.

**The source game and its provenance are explicit.** The ruleset targets one game
— Might and Magic VII — and imported packs record which release and build the
data came from, so a pack cannot silently mix editions. VI and VIII appear only
as donor documentation (a table column, a struct variant, a divergence note) and
never as a supported target. Where a divergence matters to imported data, record
it with the donor path that documents it rather than guessing.

Give each value one honest home:

- Adjustable ruleset values use discoverable, validated, typed tuning handles.
- Party, character, item, monster, encounter, service, and world values belong in
  content packs.
- Algorithmic invariants stay beside the owning algorithm.
- Source-format quirks stay in `MightAndMagic7.Import`.
- Product default selection stays in the Host.

Do not solve this with magic numbers hidden in call sites or a const field for
every authored value. Keep compact structural constants local and promote a value
only when it is genuinely adjustable or authored data. The original games keep
most rules in tab-separated tables loaded from their data archives; that is a
donor fact, not an architecture. Our ruleset owns policy, content packs carry
tables, and source-file layout does not leak into runtime types.

## Gameplay composition direction

Read [`docs/gameplay-design.md`](docs/gameplay-design.md) for the shape being
expressed — the loop, each system's shape with a fidelity verdict, and the
decisions that are expensive to reverse — and
[`docs/code-organization.md`](docs/code-organization.md) for the owner map: which
Kit owner holds which state, what the ruleset supplies, where content and imports
land, and what modes the session has. Both are design intent for an unimplemented
product. They bind new work, and changing a decision they pin is a deliberate
re-plan, not an implementation detail.

Compose Engine `Actor` in Kit/ruleset facades with named properties over the
actual attached components. Explicit factories construct entities; wrapping an
entity never silently creates components. Keep runtime entity identity, kind or
origin type identity, and durable product identity distinct. No reflection
scanning, no duplicate actor graph, no Unity-style cache/rebinding machinery.

Use direct methods for simple reads and actions, typed RuleEvents for interactions
with real participant contributions, and typed notifications for completed
changes. Explicitly compose base rules and contributors. Application rules may
mutate through canonical owners; ordinary gameplay does not require
proposal/acceptance, snapshots, receipts, revision guards, or rollback. Optional
diagnostics must not become mandatory replay or audit work.

Real-time and turn-based combat are **two modes of one session**, not two
products: the donor implementation keeps per-actor recovery times for real-time
pacing and a separate initiative queue for turn-based mode, switched by one party
flag (`docs/research/openenroth-survey.md`). Preserve that shape — one session,
one world clock, one admitted update — and do not grow a second scheduler.

Engine `ProductStateStore` stores current bytes without product-schema policy;
`JsonProductStateCodec` accepts source-generated `JsonTypeInfo` for AOT-safe JSON.
Capture meaningful state at explicit save boundaries. Only the current product
schema exists during development: no versions, migration branches, historical
readers, or compatibility fingerprints. The original game's save files are out of
scope — the product never reads or writes them, and no knowledge of that binary
layout belongs in runtime types.

## Engine boundary

> The product decides. The Engine guarantees.

The product owns application/gameplay logic, authoritative state, entities,
catalogs, content meaning, policy, and ordering within each Engine-admitted
update. Engine owns reusable host lifecycle and admission, input, rendering and
resources, spatial mechanisms, and published service families.

Use direct, safe, named C# Engine APIs from the installed `Rusty.Engine` package.
The SDK generates CoreCLR and NativeAOT composition beneath ignored `obj` output.
Reverify the actual packaged contract when using a capability; a capability list
in a document is boundary routing, not an API catalog.

Do not write downstream Rust or move product logic into Rust. Ordinary safe
product code must not use `unsafe`, pointers, `Native*`, `GCHandle`, raw statuses,
or handwritten native declarations. Generated `obj/` sources are ignored output:
never edit or commit them.

If a required behavior is absent from the safe API: name the behavior and the
Engine owner, and confirm no safe wrapper already exposes it. If the owning Engine
change is already authorized, implement it upstream and continue the dependent
work. Otherwise file or link one narrow purpose-neutral `rusty-engine` request
when authorized and report the blocked behavior at that boundary. Do not
reimplement Engine machinery in C#, TypeScript, or downstream Rust, and do not
substitute a fake proof path or parallel host.

## Update, donors, and evidence

There is one Engine-admitted update. Host and ruleset code may use the optional
`Rusty.Engine.Application` phases or implement `IEngineProduct.Update` directly;
it must not create a second loop, clock, timer, thread, browser authority,
parallel ECS, scheduler, or renderer. Product game-time and calendar advancement
inside admitted updates is allowed and required by this game family; it does not
establish an independent clock.

The original games and the donor checkouts are behavior, format, and rules
references — not code-style or architecture templates. Each carries its own
licensing and derivation history, so:

- **OpenEnroth** (LGPL-3.0, decompilation-derived C++) is the primary behavior and
  format reference. Read it to settle what the games do; do not translate its
  source into C#, and do not copy its code.
- **MMExtension** documents the games' rules and tables most completely. Its
  overall license is unstated and only some files claim MIT, so treat it as
  all-rights-reserved: mine the knowledge, cite the file, and re-implement.
- **OpenMM8** has no license and redistributes converted original content. It is
  documentation of one project's understanding only; copy nothing from it.
- Original game data is **operator-supplied**. Never commit it, and never copy
  converted game data out of a donor.

A claim about game behavior, formulas, tables, or formats is checked against a
donor file path or a documented table, not recalled from memory. Record the path
with the claim, as the donor surveys do. Where donors disagree or a divergence is
unverified, say so rather than picking the convenient answer.

Coverage planning — what behavior is in scope for the emulated game, in what
order, and which donor artifact documents it — does not exist yet. Plan
dependencies and explicit behavior contracts in a coverage plan plus feature map
before large implementation campaigns, and keep the point-in-time donor inventory
separate from live status.

## Coverage execution and drift

Incremental implementation is fine, but a proof-only slice or demonstration is not
a completed task. Do not leave no-op branches, hardcoded examples, parallel paths,
or partial adapters to satisfy a demonstration. Include the task's real callers,
state changes, content, and save/UI interoperability where applicable.

Before creating a mechanism, check both the current Engine surface and existing
repository owners for reuse or extension. Narrow drift reviews separately check
upstream reinvention, local duplication, ownership and tuning leakage, and
behavior completeness. Review the task's change and its relevant callers; report
concrete source-backed defects, not new scope or stylistic preferences. The root
reconciles findings. The lane model lives in `docs/agent-review/`.

Use focused compilation and semantic checks appropriate to the change. Broader
interactive evaluation can inform later reconciliation without becoming each
task's definition of done. Stop an upstream-blocked task honestly and continue
independent ready work; never invent a substitute to unblock the queue.

## Documentation and check posture

Keep durable repository documents free of commit revisions and pinned versions: a
stale pin in prose invites a later agent to roll the code back to match the
document, and the document is not the owner of that identity. The Engine pair
identity lives in `Directory.Build.props`, where scripts verify it; move it with
`scripts/update-engine-pin.sh` and never hand-edit a version into prose.

A hard failure must name the loss it prevents. Where the consequence is
recoverable, warn and report the actual observed value instead. Keep hard stops
for data loss, an ownership-boundary violation, or a silently wrong artifact. A
concrete collision is a real hard stop; a merely potential one is not.

Rescoping during implementation is expected, but the deferred requirement must
move to a concrete receiving task — that task's required behavior and
verification, or a new follow-up task — and the source task's record points at
it. A note that a task became narrower does not hold the requirement: a concern
is only passed on while some task is still carrying it.

## Git

Commit and push the work of a turn or task directly. This is a solo repository
used for backup and change tracking, so there is no push-approval ceremony and no
blast-radius review. Keep each commit scoped to the work that produced it, and
leave unrelated dirty files alone. `AGENTS.md` is intentionally tracked despite
its `.gitignore` rule; stage it with `git add -f AGENTS.md`.
