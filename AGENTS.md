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

**Foundation stone 5 has landed: the world is inhabited and usable — one interaction mechanism for doors,
containers and people, one service mechanism serving every shipped building kind from imported data,
schedules that lock doors outside their hours, rest and camping, and conversations with topics whose
availability follows the party's own state.**

- `src/PartyRpg.Kit`, `src/PartyRpg.Rulesets.MightAndMagic7`, and `src/PartyRpg.Host` build against
  the pinned Engine pair. The host declares the one product entry, one admitted update, the
  `session.pause-toggle` intent, and the `crawler.ui` action channel.
- The session shell owns its mode and the admitted simulation it measures, and publishes one
  projection (`crawler.hud` / `crawler.ui.snapshot.v1`) that the DOM companion renders. The ruleset
  composes the live world, the one game clock, the party its content describes, and the ledger that
  settles the party's accounts into it; the projection publishes the clock's date and the party's
  standing beside the world and movement facts, and later stones attach the remaining mechanisms.
- The ownership laws are enforced by `tests/PartyRpg.Architecture.Tests`; the kit's content, session,
  world, party, resources, time, creation, and movement mechanisms by `tests/PartyRpg.Kit.Tests`; the
  host's composition and travel policy by `tests/PartyRpg.Host.Tests`; the importer's readers and
  writer by `tests/MightAndMagic7.Import.Tests`; and the DOM companion by `tests/PartyRpg.Ui.Tests`.
  All five run in `scripts/verify.sh`, which also stages the CoreCLR product.
- `MightAndMagic7.Import` reads the operator's own data: all five containers decode every entry, the
  rule tables and the place graph reproduce the recorded inventory, all 76 maps decode, and the media
  extractor emits 17,681 images, palettes, PCX files, and sounds with a provenance manifest. No game
  data is committed and the importer is outside the runtime graph in both directions.
- `MightAndMagic7.Import.Tool` is the operator's command line: `report`, `verify` (against the recorded
  inventory), `maps`, `media`, and `write`, which emits the content packs the product loads and proves
  two runs produce identical bytes.
- The product loads content and the ruleset consumes it: `PartyRpg.Kit` defines the pack envelope,
  validates the whole catalog at start, and resolves the game bundle the host selects; the host starts
  from the bundle it ships, reports it in the projection, and refuses to start on content that is
  present and wrong. The world's places, transitions, collision geometry, and walk-in entrances, and
  the scenario's starting place and party, all come from the loaded packs, and creation refuses a
  catalog that does not declare the classes and skills it offers.
- The world exists as a graph: `PartyRpg.Kit` loads places and the transitions between them from the
  imported packs — 76 places (13 regions, 63 interiors) with 83 arrival points and 193 transitions,
  every arrival resolving — and answers where the party can go and where it arrives through one code
  path. The party's pose and derived view, per-place runtime state, and one costed transition path are
  wired into the session; a session with content places the party where a scenario says, marks places
  visited, advances respawn from the one clock, and populates the current place from its content
  placements through the Engine's own entity store. Movement is Engine-backed and stepped inside the
  same admitted update from declared input intents (`ProposeCharacterStep` with sliding, step-up,
  slopes, jump and falls; no C# collision anywhere), composed only when the engine actually supplies a
  spatial service — a product without one has no movement rather than movement through walls.
- The party exists. `PartyEntity` is one façade over one engine entity: the roster and its members, the
  one shared inventory of item instances, each member's equipment, the purse and the larder, reputation
  and fame, followers, and party-wide effects are components attached to it and read live where the
  entity carries them, so a wrapped party that lacks one fails on the read rather than growing an empty
  one. Custody is a closed set of detached, the shared pack, or one member's slot, which is what makes
  "carried but not worn" inexpressible and a per-character pack a shape the state does not have.
  Runtime entity identity, content identity, and the durable member and item identities a save carries
  are kept apart. Encumbrance is decided and not introduced: neither the shipped item table nor the
  donor's item state has a weight, so the capacity rule asks about the shared pack as a whole rather
  than inventing a limit no source states.
- The party's accounts have one settlement path. Every charge is judged against the purse and the
  larder before either moves and refused whole, naming every shortfall rather than overdrawing the
  purse; a day eats one ration, and a larder left short weakens every member. The donor's starving
  health loss is not implemented: the stated consequence is the weak condition, and a fed day ends it.
- Character creation exists. `PartyCreationFlow` walks portrait (which decides the race), class, name,
  the race's attribute point-buy, and the class's skills one step at a time, refusing an illegal choice
  where it is made with the rule it broke, and judging the pool-spent-exactly and skills-chosen rules
  when a step is confirmed; the ruleset supplies four races, eight portraits, nine base classes, a
  fifty-point pool, and four starting skills per character (two the class fixes, two the player
  chooses), and its default party is applied through those same operations. The nine classes across
  four races are swept as whole parties. A session has a **creation mode**: while it holds, the one
  admitted update drives the flow and nothing else — no movement is read, no world is stepped, no time
  passes — and every choice arrives as a declared action that returns the rule it broke instead of
  throwing. Accepting builds the party through the same factory the scenario path uses and composes the
  world over it, so the party's own larder pays for its first crossing. A host that declares no creation
  controls still plays the scenario's fixed party; **no scenario can ask for that choice itself yet**,
  which is a residue with its own receiver. All of it was played: a party was created in the running
  product, refused out of order and out of range by name, accepted, and then walked.
- One clock and calendar own time. `GameClock` over a validated `GameCalendar` is the only thing that
  advances game time; `Advance` reports the hour, day, week, month, and year boundaries it crossed and
  the deadlines it brought due, each once, and the world reads its day count from that same clock, so
  respawn and travel time are one time rather than two. The shipped rule tables carry no calendar, so
  the year's shape is authored — twelve months of four seven-day weeks, agreeing with the imported
  reset intervals — with the reasoning beside it, and a source scan in `tests/PartyRpg.Kit.Tests` fails
  the kit if one of its sources names an ambient time source (`DateTime`, `Stopwatch`, a timer, a
  thread, or the rest of that list).
- What a crossing costs is applied, once. Walking into an entrance or over a region edge takes the
  transition, and on arrival the world advances the clock by exactly the time the cost quoted and
  spends exactly the provisions it quoted through the party's one settlement path; a refused
  transition, and an arrival a place refuses, charge neither and leave the party where it stood. A
  party that arrives with its larder short is weakened. Paid and magical travel still refuse by name,
  because the services that sell a fare and the spell and beacon owners do not exist yet. The cave
  mouth between The Dragon's Lair and Emerald Island was crossed in the running product and read off
  the panel: the first crossing took the clock from `1168-01-01` to `1168-01-02` and left one portion,
  and the crossing back took it to `1168-01-03` with none and the party weak
  (`local/verify/travel-cost/`).
- The product shows a world and a party only when a bundle carries places, a scenario start, and a
  scenario party; the shipped bundle carries none of them, so a running product without imported packs
  reports no world and no party. Those are content the operator imports, and none of it ships.
- Fall damage is priced by movement and applied by nobody: a landing past the tuning's threshold is
  reported with its distance and its excess, and no character loses health. Collision geometry now
  travels with content: `write` emits the engine's own spatial artifact for every place whose solid
  faces can be closed (all 76 of the operator's places; 824,320 triangles), and the mover admits it
  when the party enters a place and reports the counts the engine admitted. A place whose geometry
  cannot be closed carries no entry and the party is told it stands on nothing rather than falling
  through a floor. Two limits are stated rather than hidden: no walkable navigation cells are emitted
  (the engine derives collision navigation itself, and nothing asks for a path yet), and a door's
  polygons are solid where they stand because doors do not move yet.
- The session persists under one current schema: the party (members with their skills, spells,
  progression and portraits; every item instance with its custody, damage and enchantments; purse,
  larder, standing, followers, effects and the identity cursors), the clock's elapsed game time, the
  party's place and pose, and per-place state. The document carries no version and no migration path —
  one schema, replaced rather than evolved. Saving happens only where the product asks for it, and a
  load rebuilds what is transient (movement outcomes, projections, the population's runtime entities)
  so nothing points at a handle or an entity that belonged to the previous session. A malformed or
  contradictory save is refused with every problem listed at once, not the first. Knowledge, quests,
  containers and scenario flags have no owner yet, so the document has no section for them rather than
  a placeholder. A save is asked for explicitly — the host declares `session.save` as a key and as a
  panel action, the session applies it before the update it arrives in steps anything, and exactly one
  write happens per request: nothing writes on a step, a mode change, or disposal. What the player sees
  is a result, not a promise: `saved` with the game-time it happened at, or a named failure
  (`save-unavailable`, `save-refused`, `save-failed`) that leaves the session running. Whether a start
  is fresh or resumed is a host composition decision, stated rather than inferred (`RUSTY_CRAWLER_START`),
  and a resume with nothing saved refuses by name instead of quietly starting fresh. Both were walked in
  the running product: a party created in the product crossed into Emerald Island on `1168-01-02`, was
  saved, and a restarted host resumed that same party, place, clock, and per-place state at the saved
  pose (`local/verify/save-resume/`).
- The world is usable through one interaction mechanism. A target is discovered from the current place's
  placements and the party's pose, never hand-listed, and one workflow — identify, judge the requirements
  in order, apply, report — serves search, open, unlock, pull, talk and read, so a new target kind is
  content plus a ruleset answer rather than a class. Requirements are a small vocabulary (an item, which
  is how a key works, a skill, a flag, a time of day) whose meaning this game supplies; a refusal names
  what blocked it. Reach, sight and identity are re-validated at use, and every use and refusal is
  reported to diagnostics with the party's pose. Over the operator's data this finds 786 in-use doors in
  54 interiors, 60 event decorations, and containers decoded from the map deltas: 1,520 chest records and
  722 sprite objects become 357 containers in 57 places, with traps read from the place's own map-stats
  row and their harm landing on the members the party foundation gave resources. A *chest* is placed by
  the face whose event program opens it, not by a sprite object — a distinction the format spec now
  records, alongside the chest flag bit the two donors disagree about, which nothing reads.
- Every shipped service kind is served by one mechanism from imported data. A building-table row joined to
  the faces that raise it becomes a counter — kind, proprietor, hours, multipliers, stock interval,
  training cap, with its source row as provenance — and **136 of the 172 recognised rows across all 21
  kinds are enterable**, with 84 fares and 358 placements, and the remainders named rather than averaged
  away. Buy, sell, identify, repair, teach, cure, train, provision, stay, deposit, withdraw and fare all
  take one path: resolve, judge eligibility, quote a price, settle through the party's one ledger, apply,
  credit. Shelves are lots on a repeating game-time deadline that travel time also feeds; a passage and a
  bank balance are party-carried effects, so a seat bought in one town is honoured on the road and cannot
  be spent in another's name. The kit gained capabilities, not kinds — adding a service kind means adding
  content and a ruleset answer.
- Towns are clocked. A place's hours come from the counters standing in it or from the place's own entry,
  and a door in a clocked place carries those hours as an ordinary requirement judged against the one
  clock, so a shut door refuses in the mechanism's own sentence and opens again when the hour comes —
  nothing is remembered. Rest, camp and wait are three different things: a night under a roof fills both
  pools, clears the conditions the rule names and settles the day's provisions through the ledger (a
  larder left short has the last word); camping in the open is priced by the donor's ground table, refuses
  near hostiles, draws its risk from the place's own encounter chance through the engine's keyed random
  service, and a broken night advances one hour while restoring and spending nothing; the waits advance
  time and restore nobody. Fatigue is a deadline on that clock, not a counter in the world step.
- People stand in the world and can be talked to. The delta's actor records are 0x344 each with an NPC row
  at +0x20 and a 16-bit position, and the NPC table names the rest: over the operator's install that is
  826 actors of which 123 name an NPC row, plus 246 people inside 195 buildings, with 2 residents named
  unreachable because their rows raise no event on any face. They are emitted as content and as
  placements, and a `Talk` use opens one conversation owner whose topics are content and whose
  availability is recomputed per read against real party state — a flag, standing, a class, a race, the
  hour, an errand — so a topic appears and disappears as the state does and a stale choice is refused
  with its reason. A service is now reached *through* the conversation: a person who keeps a counter
  offers it as an ordinary topic whose answer hands off to the service mechanism, and a handoff nothing
  routes is reported by name. An answer records what it told the party as party-carried state.
- **No combat, magic, or quests exists, and a save carries no deadlines.** Do not describe, review, or
  accept behavior those stones will add as though it were here. What a player can do today is create a
  party, walk it, open doors and containers, get caught by a trap, buy and sell at a counter, learn from a
  guild, rest or camp, talk to people, and save or resume: on the agent playtest service's remote browser a held `W` walks the
  party about 382 units a second and the released key
  stops it where it stands (Emerald Island, `12552, 800, 193` to `12552, 3859, 98` over eight seconds
  of held key, the pose then unchanged for the next seventy seconds while the admitted steps kept
  advancing), `Q` turns and Space jumps. Walking into a transition now changes the place: the importer
  emits, per travel link, the reach its source map's own event face gives the party to walk into (532
  reaches for 155 of the 193 links, with the reason recorded per link for the rest), and the world
  consults the current place's reaches inside the movement step, taking one through the single travel
  path when a step carries the party from outside its reach to inside it. Both crossings of Emerald
  Island's cave mouth and The Dragon's Lair's exit were walked in the running product, with the HUD and
  the product's own travel reports recorded in `local/verify/walk-transition/`; the earlier walk that
  could not cross anything is in `local/verify/walk-playtest/`.
- An interactive session has only been observed through the agent playtest service's remote browser,
  which held one for about five minutes without stopping. This box's own headless browser still reports
  `DEV_HOST_VIDEO_FEEDBACK_UNSUPPORTED` a few seconds after attach and stops the runtime, and the
  engine's development host admits a request only from the origin it bound to, so a browser reaching the
  product from another machine needs the host bound to an address that browser can reach rather than to
  loopback. The panel reports the pose and the stepped counts and nothing about movement quality, so a
  blocked direction and an input that never arrived look the same on screen.

When the next stone lands, update this section, `README.md`, and the owning directory README
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
`tests/PartyRpg.Architecture.Tests` enforces that list, the dependency graph, the single product
entry, and the identity the host declares in both code and MSBuild; a violation fails the suite
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
land, and what modes the session has. Both are design intent for an unimplemented
product. They bind new work, and changing a decision they pin is a deliberate
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
documents, where they go stale and are later read as current. Repository
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
