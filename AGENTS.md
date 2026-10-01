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
- operator's own game copy (extraction source): `/home/research/old-games/game-mm7`
- donor surveys: `docs/research/openenroth-survey.md` and
  `docs/research/mmextension-openmm8-survey.md`
- experience outline with manual citations: `docs/research/mm7-manual-outline.md`
- extracted data inventory with donor citations: `docs/research/mm7-data-inventory.md`
- format specs for the importer's remaining work: `docs/research/mm7-map-formats.md` and
  `docs/research/mm7-media-formats.md`
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
- **Approximate is the normal verdict.** The goal is to adapt the essence of the
  game, not to chase identical equivalence — equivalence is a trap that turns
  every adaptation into an argument about numbers. Faithful values are welcome
  when they are cheap to take from the data, never required for their own sake.
- **Out of scope.** Reading or writing original save games, loading the original
  executables or their extension/plugin ecosystem, byte-exact map geometry, and
  any promise that original mods, trainers, or editors keep working.

Donor and extracted data inform the design; they do not bind it. Where a task's
required behavior and the original game disagree, the task and the design
documents decide, and the difference is recorded rather than silently rounded.

## Current state

Den owns which stones are open; this section states the shape the code has and the residue it still
carries. Stones 1 (shell) and 2 (import) are closed. Stones 3 to 8 (world; party; interaction and
services; combat; progression and magic; quests and knowledge) have each landed their mechanism, which is
load bearing, and each still carries open residue, listed below with its receiver: Den keeps 3 to 5 open as
campaigns and has closed 6 to 8 with their residue as planned children. Stone 9 (breadth) has not started.
Per-mechanism detail lives in the owning project README (`src/PartyRpg.Kit/README.md`,
`src/PartyRpg.Rulesets.MightAndMagic7/README.md`, and the others under `src/`), not here.

- **Graph and surface.** Kit, ruleset and host build against the pinned Engine pair; the importer and its
  tool are offline and outside the runtime graph in both directions. The host declares one product entry,
  25 input intents with their keys, and the `crawler.ui` payload channel. The session publishes one
  projection (`crawler.hud` / `crawler.ui.snapshot.v1`) when it has changed; the TypeScript companion
  renders it with no state, rule or timer of its own, and fixtures the host suite writes bind the C# and
  TypeScript sides of that contract. The runtime needs a GPU adapter; the product draws no world, so the
  game is the DOM panel over an empty frame.
- **Content.** The importer reads the operator's own install and `write` emits deterministic packs; no game
  data is committed, and every count the documents quote is checked by `mm7import verify`. The kit validates
  the whole content root at start and loads exactly what the bundle selects. The shipped bundle selects no
  packs, so a product without imported content reports no world and no party.
- **World and time.** Places, arrival points and transitions load from packs; using a clicked exit or treading
  on a plate (a map event whose branches pick the move), boarding a fare the party bought, and a travel spell all
  take one transition path that charges the clock
  and the larder once. Movement and collision are the Engine's (the character step over each place's
  spatial artifact, flying mode under a flight, and the water a place's packs name beside it); a fall past the
  threshold and standing in water harm members through the ruleset's rules. `GameClock`
  over an authored calendar is the only time, and every advance reaches every owner registered with it.
- **Party.** `PartyEntity` is one entity with its components: roster and members, one shared inventory of
  item instances, per-member equipment, purse and larder, reputation and fame, running effects, records,
  holdings, passages, memberships, debts and bans. Every charge is judged and settled through one ledger. Creation is a
  session mode with its own flow, and the scenario path builds a party through the same factory and the
  same owner composition (a parity suite proves every owner answers on both); the
  scenario start's `party` word picks one (creation by default) and the projection names it.
- **Interaction and services.** One use workflow serves doors, containers, people and fixtures; a fixture runs
  the steps of its imported map event this game interprets and refuses the rest by name. One service
  mechanism, an operation table judged before anything is settled, serves every shipped service kind; towns
  keep hours on the one clock; rest, camp and wait are distinct; conversations recompute their topics from
  party state and hand off to counters.
- **Combat.** One fight over the live world with two pacings over one recovery quantity, one resolution
  path, conditions up to death, corpses and loot through the container mechanism, and cleared places
  restored by the clock. Creatures come from `encounter` placements the ruleset resolves and from the maps'
  own `actor` records, both when a place is populated, and this game's AI orders them through the gate the
  player's control uses. What a spell leaves on a creature is the creature's own state, and a charm or a
  binding puts it on the party's `Ally` side.
- **Growth and magic.** `PartyProgression` is the one writer of experience, levels, skill points, ranks and
  promotions (the light/dark choice lives in the character's class and is irreversible). Skills have four
  masteries under this game's ceilings; nine schools and 99 spells share one casting workflow whose eight
  effect categories each reach their owner, stated per spell in `docs/magic-coverage.md`; alchemy mixes the
  shipped recipes.
- **Record.** Quests (definitions from content, instances on the party), `PartyJournal`, `PartyKnowledge`,
  standing read from records, and the `PartyMaps` automap.
- **Persistence.** One current schema, written only on `session.save`: party, clock, world, quests,
  journal, knowledge and maps. A load rebuilds what is transient; a contradictory document is refused with
  every problem named.
- **Refusals and rolls.** Every mechanism refuses with the kit's one `Refusal` (a code from that mechanism's
  code class and a sentence); a requirement is judged as a `Verdict`; chance goes through `KeyedRolls` over
  the Engine's keyed random service; the words a game uses for what the kit only counts come from
  `IGameNames`.
- **Verification.** `scripts/verify.sh`, also the `verify` workflow, runs every suite listed in
  `tests/README.md`. Live checks follow `docs/live-checks.md`; published readings are in `docs/evidence/`.

**Open residue and its receivers** (Den task ids; Den owns their status):

- Stone 3, world: creatures cannot path around a wall because places carry no navigation cells (#8665); a
  creature step can fault the runtime in Harmondale (#9041); the global event program (topics) is not run, so
  some quest-gated links have no raiser (#9042); a person moving house and a counter door's own move are not
  followed (#9043).
- Stone 5, interaction and services: opened doors and emptied containers are not saved (#8593), and a
  searched chest can be looted twice (#8696); a door's collision, and a face group an event makes passable,
  do not move (#8594); a container above or below the floor cannot be used (#8697); deadlines (fatigue, wards, light, haste) block
  a save by name instead of being carried (#8617); towns have no house doors and camping is priced per
  place (#8618); rest restores laid-out members (#8662); the panel reaches only buy and sell (#8619); a
  fixture event refuses only at a hireling step (#8514).
- Stone 6, combat: a fight is not carried in a save, and a save taken with one pending is refused (#8658);
  an order commands every member rather than a selected one (#8659).
- Stone 7, progression and magic: training takes no game time (#8671); the two counted deeds two ranks need
  have no writer (#8689); a laid-out member can be promoted (#8705); item-aimed effects (#8513) and followers
  (#8514) are "not yet" in `docs/magic-coverage.md`.
- Stone 8, record: an errand's item is protected only from sale (#8687).

When a stone lands or a residue closes, update this section, `README.md`, and the owning project README
together.

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

The concrete project graph now exists and the laws that hold it are checked; new projects and new
kit mechanisms belong in `src/README.md` and the per-project READMEs, which must be updated with the
code that changes them.

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
`tests/PartyRpg.Architecture.Tests` enforces that list — with a representative sample of the game's own
class, skill, spell, item, creature, place, and people names, each proved to be the game's word — the
dependency graph (peers, packages, and friend declarations), and the single product entry; the host suite
holds the identity and controls the host declares in code to its project file. A violation fails the suite
rather than surviving review.

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
land, and what modes the session has. They are the design the code is built to and
bind new work: where code and document disagree, one of them is wrong and is fixed in
the same change, and changing a decision they pin is a deliberate
re-plan, not an implementation detail.

**The party is one entity, and it owns the inventory.** Party-scoped components
attach to it — roster and members, shared inventory, equipment by member, purse,
food, reputation, followers, party-wide effects — and session mechanisms address
the party, not four independent characters. A character owns only what it has
equipped; there is no per-character pack. This is a deliberate improvement on the
original and is not to be "corrected" back toward per-character inventory.

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

**Engine mechanics helpers the product does not use, by decision.** The package's
`Rusty.Engine.Mechanics` offers `Stat` and `Track` (double-valued stats with
modifier stacks, and a bounded current value over one), `InventoryStore` with
`InventoryComponent` (fungible stacks and unique items, with revisions and
receipts), and `EffectsComponent` (effect definitions with stacking groups,
provenance and receipts; timing left to the product). The kit keeps its own owners:

- a character's pools are integer `ResourcePool`s with a below-empty `Deficit` the
  death ladder reads, and no modifier is stored: the ruleset reads attributes,
  conditions and running effects when a rule asks, so there is no modifier list to
  keep in step with its sources or to save;
- every item this game carries is a unique instance with custody (detached, the
  shared pack, or one member's slot), damage, charges, strength and the durable id a
  save carries; nothing stacks, and revisions and receipts are ceremony ordinary
  gameplay does not need;
- a running effect is one magnitude per effect whose end is a deadline on the one
  `GameClock`; stacking groups and receipts answer no rule this game has.

Revisit this when a rule needs modifiers stacked from several sources (item
enchantments, #8513) or fungible stacks.

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

Live work lives in Den, not in Markdown. The foundation stones are campaigns
`rusty-crawler#8454`–`#8462`, one per stone, each with child tasks carrying
outcome, scope, acceptance criteria, and expected evidence; dependencies between
them encode the building order in `docs/gameplay-design.md` §5. Den owns task
status, dependencies, and scheduling — do not mirror task lists into repository
documents, where they go stale and are later read as current (the residue list
under "Current state" names receiver ids and nothing else). Repository
documents own the durable shape and the evidence; a point-in-time feature map may
still be written here when a campaign needs one.

## Coverage execution and drift

**Build foundations first, one stone at a time.** Do not build vertical slices: a
slice leaves stubs, placeholder values, and feature-local shortcuts that a later
reconciliation campaign has to hunt down. The endpoint here is known, so each
capability lands complete and load bearing before the next rests on it.

- A completed capability works everywhere it applies, not only where it was first
  exercised. The game opens on a tutorial island with a representative sample of
  its systems, so Emerald Isle is where a person confirms landed work in play — a
  conformance area, never a scope boundary.
- No stubs and no placeholders for later capabilities. If something is not built
  yet, an earlier capability must not pretend to have it.
- Nothing is deferred by accident: a limitation is fixed, or recorded and routed
  to a concrete receiver with a stated requirement.
- Testing lands with the capability, not at the end.
- Breadth passes — more places, monsters, items, spells, quests — add content and
  tuning on top of general mechanisms; they never complete a mechanism that was
  left partial.

The order and its rationale are in `docs/gameplay-design.md` §5.

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
`rusty update` and never hand-edit a version into prose.

This file is read whole by every agent session, so it has a size budget:
**32000 bytes**. `tests/PartyRpg.Architecture.Tests` fails when the file exceeds it or
repeats a paragraph, and when a path a top-level document cites does not exist in a
clone. Per-mechanism detail belongs in the owning project README; evidence a document
cites belongs in `docs/evidence/`, never in the operator's ignored local tree.

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
leave unrelated dirty files alone.
