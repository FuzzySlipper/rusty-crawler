# Gameplay design: the shape of the game

Status: **design intent for a product that does not exist yet.** This document
fixes the *shape* of the game — what the player does, what systems must therefore
exist, and how faithful each one is meant to be. It deliberately does not fix
tuning, formulas, or interfaces; those belong to the ruleset, the content, and
later tasks.

Evidence base: [`research/mm7-manual-outline.md`](research/mm7-manual-outline.md)
(experience, cited to manual pages) and
[`research/mm7-data-inventory.md`](research/mm7-data-inventory.md) (what the
shipped data actually contains). Where this document and the original game
disagree, this document decides, and the difference is recorded rather than
silently rounded.

## 1. The game in one page

The player guides **a party of four** through the first-person world of Erathia.
The party walks and rides across connected outdoor regions, enters towns, castles,
and dungeons as separate places, talks to the people in them, buys services,
fights in real time or turn by turn, and grows a set of classed characters from
novices into ranked specialists whose paths eventually split toward the light or
the dark.

The loop is:

> explore → meet and fight → loot → return to a service town → convert rewards
> into capability (sell, identify, repair, learn, train, promote, rest) → travel
> further, better equipped.

Two properties bind that loop together and are the reason the shape has to be
decided before code:

- **Everything costs time.** Travel between regions takes days, training takes
  days, rest takes hours, and combat recovery is measured in seconds of the same
  clock. Shop doors close, monsters respawn, and buffs expire against that one
  clock.
- **Combat happens where you stand.** There is no separate battle screen: the
  same first-person view, the same party, and the same world state serve
  exploration and combat. Turn-based mode changes *pacing*, not scene.

## 2. Fidelity: what we keep, approximate, and drop

Every system below carries one of three verdicts.

| Verdict | Meaning |
| --- | --- |
| **Match** | The structure and the player-facing behavior are reproduced; a player of the original should recognize it. |
| **Approximate** | The mechanism exists with the same role, but values, breadth, or depth are ours; fidelity is not claimed and not required. |
| **Ours** | We deliberately diverge, or the original feature is out of scope. |

The global stance, from [`AGENTS.md`](../AGENTS.md): a semi-equivalent
recreation. **Approximate is the default verdict**, and that is a feature: the
goal is to adapt the essence of the game, not to chase identical equivalence,
which is a trap that turns every adaptation into an argument about numbers.
Faithful values are welcome when they are cheap to take from the data; they are
never the point by themselves. **Out of scope everywhere:** reading or writing
original save games, loading the original executable or its extension ecosystem,
byte-exact map geometry, exact table values, and any promise that original mods
or editors keep working.

## 3. System shapes

Each system states the **shape** (what the player experiences), the **model**
(what the code must therefore represent), and its **fidelity**.

### 3.1 Party and characters — Match (with two deliberate improvements)

- One party of four created characters travels as a unit: one position, one
  facing, one shared purse, one shared food supply, one shared reputation.
- **The party is one entity.** Components attach to it — roster, inventory,
  equipment-by-member, resources, reputation, followers, party-wide effects — and
  session mechanisms address the party rather than four independent characters.
- Creation picks race, class, portrait, distributes an attribute point-buy pool,
  and assigns four starting skills (two fixed by class, two chosen).
- Each character carries seven attributes, hit points, spell points, conditions,
  a skill list, a spellbook, experience, a level, and a class rank — and owns
  only the items it has equipped.
- Followers may join: a small hired limit plus story characters outside it.

**Model.** A party entity that everything party-scoped attaches to, plus a
character component holding what a character *is* (attributes, resources,
conditions, skills, spells, progression) and what it *wears or wields*. Character
creation is a real product flow with validation, not a debug shortcut. Class,
race, and rank are ruleset definitions, not enums compiled into the kit.

**Deliberate divergence — one shared inventory.** The original gives every
character a pack and turns loot into a shuffle between four of them. We do not:
**the party owns one shared inventory**, and a character owns only its equipped
items. Nothing about what the player can carry or use changes; the busywork goes
away.

**Deliberate divergence — weight.** The shipped data has no item weight column
and the original enforces no carry limit; armor weight only affects recovery
speed. If we introduce encumbrance later, it is party-wide: one allowance equal
to the sum over members and followers, measured against the single shared
inventory — never per character.

**Fidelity.** Structure matches. The shared inventory and any later encumbrance
are ours by choice and are recorded as such; attribute pools, growth curves, and
creation defaults are ours.

### 3.2 Skills and mastery — Match

- Skills fall into blocks (weapon, armor, magic, miscellaneous) and gate
  equipment: a character cannot use a weapon or armor without the skill. Belts,
  boots, capes, helmets, and gauntlets are exempt.
- The shipped table carries 37 skill rows; the manual's four blocks account for
  34 of them, and the rest are unused leftovers. Treat those extras as evidence
  of the data's history, not as scope.
- Basic skill ranks are bought in town or taught by guilds; expertise tiers are
  raised by master teachers found in the world.
- Four tiers — basic, expert, master, grand master — each unlocking or
  multiplying an effect.
- Levelling grants skill points; raising a skill to level *N* costs *N* points.
- How far a skill can grow is capped by class and rank.

**Model.** A skill catalog in content; per-character skill entries (level, tier,
points spent); a ruleset-supplied ceiling function over class, rank, and skill;
training sources as world entities; and one place that spends points. Mastery
tier is a first-class value on a skill, not a boolean per feature. The class and
rank ceilings are **not in the shipped tables** — they are executable data — so
our ceilings are authored, informed by the donor's transcription rather than
copied from a file.

**Fidelity.** Structure and tier semantics match; ceilings, costs, and per-tier
numbers are approximate.

### 3.3 Magic — Match (structure) / Approximate (spells)

- Nine schools, each with its own skill; eleven spells per school, arranged in
  four tiers, strengthened by school mastery.
- Spells are learned from books bought at guilds and magic shops or found as
  treasure, and are permanent per character. Scrolls cast once without skill;
  wands carry charges and are equipped as weapons.
- Spell points come from class and attributes; costs span a wide range.
- Each character keeps one quick spell for one-key casting.
- Alchemy mixes reagents and potions; tier gates which mixtures are possible,
  and incompatible mixtures explode.

**Model.** A spell catalog in content (school, tier, cost, targeting, effect);
known spells per character; a casting workflow that validates mastery, cost, and
target, then applies an effect; buffs whose duration is a game-time deadline;
scrolls, wands, and learning books as item behaviors; potion recipes as content.

**Fidelity.** Structure, schools, and gating match. Individual spell effects are
approximated by category first (damage, healing, resistance, condition, light,
travel, detection, utility), then deepened. Do not claim per-spell fidelity that
was not checked.

### 3.4 Combat — Match (two modes) / Approximate (depth)

- Real time is the default. Every action costs recovery time; a recovering
  character cannot act, and the ready light shows it.
- One key toggles turn-based mode at any time, in the same scene. Combatants act
  in initiative order derived from speed and recovery; fast actors act more than
  once per round; each round ends with a short party movement phase.
- Attacks are melee, ranged, or spell; a single "act" key resolves quick spell,
  then bow or wand, then hand-to-hand.
- The ready light doubles as an aggro indicator; monsters are hostile on sight.
- Damage produces conditions: unconsciousness, death, eradication, weakness,
  poison, disease, insanity, fear, curse, sleep, stoning, paralysis.
- Corpses are searchable and vanish; chests and drawers may be trapped.
- Some magic turns monsters away, charms them, or makes them fight each other.

**Model.** **One** combat state over the live world, carrying a pacing mode.
Real-time pacing uses per-actor recovery timers; turn-based pacing uses an
initiative queue with action and movement phases. Attack resolution, effect and
condition application, monster AI state, spawn/despawn, and loot are shared by
both modes — mode changes pacing, never ownership. Encountering hostiles is a
world state, not a scene transition.

**Fidelity.** The two-mode structure and its shared state match; monster
variety, AI sophistication, and per-monster numbers are approximate.

### 3.5 The world: regions, places, transitions — Match (structure) / Approximate (breadth)

- Outdoor maps are square sections of one continuous landmass; walking to an
  edge travels to the adjacent section and costs days and food.
- Towns, castles, and dungeons are separate interior places entered through
  entrances; interior doors may need keys, switches, or actions; secret doors
  are revealed by perception.
- Stables and docks offer paid routes; magic offers portals and beacons.
- The automap fills in as territory is seen; obelisks and fountains record their
  effects automatically.

**Model.** A **graph of places**: regions (outdoor, edge-connected) and interiors
(entrance-connected), each with entry points and transition edges that carry a
travel cost and a destination. Per-place runtime state holds visited and
discovered state, respawn timers, cleared flags, and per-place transient
positioning. **Party knowledge** (maps drawn, notes, obelisk and fountain
records, known routes) is a separate owner from world state — what the party
knows survives independently of what the world currently is, and retrofitting
that split later is expensive. The graph itself is recoverable: the shipped
per-map event programs carry 271 map-transition instructions that resolve into
165 directed edges over 86 place pairs with no unresolved destinations, so the
world is imported rather than hand-authored.

**Fidelity.** The graph structure matches. Geometry and placement are imported
where the importer can carry them and authored otherwise; the shipped game's 13
outdoor regions and 63 interior places are a breadth target, not a first
milestone.

### 3.6 Towns and services — Match (kinds) / Approximate (stock and prices)

The shipped data recognizes **21 service kinds**, covering 172 of 525 building
records; the remaining records are house and entrance markers, which matter for
placement rather than for service behavior. All 21 kinds are in scope:

| Kind | Serves |
| --- | --- |
| Weapon shop, armor shop, magic shop, alchemist | Buy, sell, identify, repair, teach basic skills, stock that refreshes on a multi-day interval |
| Guilds: seven per-school guilds plus light and dark | Membership, basic school skill, learning books, and the spell levels that guild tier may sell |
| Temple | Paid removal of every condition including death and eradication |
| Tavern | Food, drink, rooms for safe rest, rumors |
| Training hall | Convert experience into a level for a fee, with a level cap set per hall |
| Bank | Store gold safely |
| Stable, dock | Paid overland and sea routes to other places |
| Town hall | Local authority, tasks, and a bounty board |
| Throne, castle, entrance markers | The doorways that lead to rulers, quest givers, and dungeons |
| Ordinary houses | Odd jobs, guild members, master teachers |

- Shops keep business hours and their doors are locked outside them.
- All of these are entered by walking to a door, not by a menu.

**Model.** **One** service mechanism with a small set of kinds, each with a
session shape (browse and trade, heal, rest, deposit, train, teach, travel,
talk), driven by content definitions for stock, hours, prices, and the ruleset's
policy for what is legal. Do **not** build one class per building type.

**Fidelity.** Kinds match. Stock, prices, membership rules, and level caps are
approximate.

### 3.7 Time and calendar — Match

- One clock runs while the player plays, shown by a calendar book with date and
  time, and driving day and night.
- The party should sleep about eight hours a day; going without causes fatigue
  and weakness. Resting restores hit and spell points; waiting passes time
  without healing.
- Camping outdoors consumes food, more on harsh terrain, and the party refuses
  to camp with hostiles near. Renting a room is the safe alternative.
- Shops open and close; monsters respawn on multi-day intervals; spell durations
  are measured in game minutes, hours, days, and weeks; some spells are limited
  per day.
- Characters age.

**Model.** **One** game clock owned by the session; every duration is a
game-time deadline, never a frame count. Travel, rest, waiting, training, and
service time advance it through explicit calls. Schedules and respawns query it.
Real-time combat recovery converts elapsed game time into action availability.
There is no second clock, timer thread, or scheduler.

**Fidelity.** The model matches; our calendar constants are ours, shaped like
the original's (the donor data carries its real constants — see the inventory).

### 3.8 Progression, rank, and reputation — Match (structure) / Approximate (curve)

- Experience comes from kills and quests; advancing a level costs a multiple of
  the current level and happens at a training hall for a fee.
- Levelling raises hit and spell points and grants skill points.
- Each class advances through ranks via **promotion quests**; new ranks raise
  growth and skill ceilings and unlock skills.
- The second promotion splits each class into two alternatives tied to light or
  dark magic. The choice is per character and effectively irreversible.
- Reputation and fame are party-level values that change how people treat the
  party.

**Model.** One progression owner: experience awards, level-up, point grants,
rank promotion, path choice, and reputation. Rank ladders, promotion
requirements, and reputation effects are ruleset policy over content data, not
conditionals scattered through gameplay code.

**Fidelity.** Structure matches; the experience curve, promotion quests, and
reputation thresholds are ours.

### 3.9 Quests, journal, and knowledge — Match (structure) / Ours (content)

- Quests are given in conversation, tracked in a current-quests book, and
  usually turned in to the giver; some carry quest items that cannot be
  enchanted or sold freely.
- Auto notes record potion discoveries, fountain effects, obelisk clues, and
  odd events; history records where the party has been.
- The awards screen records what the party has accomplished.

**Model.** Quest definitions in content plus per-instance quest state (offered,
accepted, objectives met, turned in); a knowledge store for notes, discoveries,
and history; awards as flags the ruleset and content can test. Quest state and
knowledge are saved; quest *content* is authored or imported, never code.

**Fidelity.** The structure matches. Quest content is ours: start with a small
authored set, not the original's hundreds.

### 3.10 Items, inventory, and economy — Match (structure) / Approximate (breadth)

- Each character has a loose inventory and an equipped figure; equipping requires
  the matching skill; rings are the exception that stacks.
- The party shares one purse and one food supply; a bank stores gold.
- Shopkeepers identify, repair, and sell; characters with the right skills can
  identify, repair, and enchant themselves.
- Chests and drawers may be trapped; bodies are searched for loot and vanish.
- Items can break; enchantments and special item classes exist.

**Model.** Item definitions in content and item instances in runtime state, with
identity that survives a save (a specific artifact stays that artifact). The
shared inventory is a party-owned container; equipment is per member and is the
only item state a character owns. Currency, food, shop stock, containers, and
loot tables are separate owners with clear ownership of mutation. Identify and
repair are item state, not shop-only side effects. Imported item tables supply
breadth.

**Fidelity.** Structure matches; item counts, values, and enchantment rules are
approximate. Artifacts and relics exist in the shipped item table even though the
manual never describes the classes, so they are real scope with an unknown rule
set — treat their rules as ours until a task verifies them.

### 3.11 Interface surfaces — Match (information architecture) / Ours (presentation)

- One adventure screen: first-person view, four portraits with hit and spell
  point bars, the ready light, follower portraits, active buff icons, an automap
  corner, a books row, food and gold readouts, and the action buttons.
- Character screens with tabs; five books (quests, notes, maps, calendar,
  history); spellbook per school; inventory; rest and camp; options; save and
  load with thumbnails.

**Model.** The product publishes **one HUD projection** plus per-screen values
and receives **semantic actions** back. The DOM companion is thin: it renders
values and reports intents, and owns no state, no rules, and no loop.

**Fidelity.** The information architecture matches; layout, art, and
interaction details are ours.

### 3.12 Rules the shipped data does not carry — Ours

Three rule families a designer would expect in the tables are not there; the
game keeps them in its executable or in localization strings:

- **Class and rank skill ceilings**, and which skills a class may learn at all.
- **Guild spell-level gating**: which spell levels each guild tier may sell.
- **Calendar constants and race stat tables**: month structure and per-race
  creation ranges.

Consequences: our ceilings, guild tiers, calendar, and race tables are **authored
by us**, informed by the donor's transcription and the manual, and recorded as
ours rather than presented as extracted facts. Projects that must be checked
against the executable are separate tasks with their own evidence, not
assumptions smuggled into the importer.

## 4. A session's lifecycle

1. Product start → title and load.
2. Party creation (attributes, classes, skills) → starting scenario resolves.
3. Adventure mode: the world runs in real time; movement, interaction, combat.
4. Screens (inventory, books, spellbook, services) open **without pausing the
   world** in real time, matching the original; turn-based mode instead waits for
   the player's committed action.
5. Rest, camping, training, and travel advance the clock in discrete steps.
6. Save at meaningful boundaries; loading restores party, world, knowledge,
   clock, and place.
7. The arc runs from a starting region through promotion quests to the ranked
   endgame.

One session, one clock, one admitted update. No mode gets its own loop.

## 5. Building order: foundations, one stone at a time

**No vertical slices.** A slice builds a thin end-to-end path and leaves stubs,
placeholder values, and feature-local shortcuts behind it; the stubs are then
either forgotten or found later by a long reconciliation campaign. This project
has a known endpoint, so it is built the other way — like a stone bridge, one
durable foundation at a time, where each stone is finished, global, and load
bearing before the next one rests on it.

The rules that make that work:

- **A stone is complete, not demonstrative.** When a capability lands, it works
  everywhere it applies — every place, every character, every item — not just in
  the area where it was first exercised. "It works on Emerald Isle" is a check,
  not a scope.
- **No stubs and no placeholders for later stones.** A stone either does its job
  or it is not started. If a later stone is not built yet, the earlier stone must
  not pretend to have it.
- **Nothing is deferred by accident.** A limitation found while building a stone
  is either fixed, or recorded and routed to a concrete receiver (a later stone
  with a stated requirement, or a named follow-up). Silent gaps are the failure
  this order exists to prevent.
- **Testing happens with the stone**, not at the end. Each stone carries the
  focused proof for its own seam, and the repository's verification stays green.
- **Emerald Isle is the conformance area.** The game itself opens on a tutorial
  island with a representative sample of its systems; as stones land, the island
  is where a person confirms they actually work in play. It is a place to check,
  never a boundary on what a stone must handle.

The stones, in dependency order — each line is a capability class, not a task
list, and the coverage plan will break them into tasks:

1. **Product and engine shell.** Host entry, lifecycle, one admitted update,
   input intents, staging, and the UI shell that later stones render into.
2. **Content and import pipeline.** Archive and table readers, map geometry and
   media extraction, provenance, pack shapes, and content validation — the game's
   own data becomes the source of breadth before gameplay leans on it.
3. **World foundation.** The place graph, transitions, entry points, entity
   population, and spatial stepping, exercised through imported regions and
   interiors.
4. **Party foundation.** The party entity and its components, character creation,
   equipment, the shared inventory, resources, the clock and calendar, and
   persistence.
5. **Interaction and services.** Interaction targets, doors, containers, dialogue,
   the one service mechanism across all kinds, and schedules.
6. **Combat foundation.** Real-time pacing, recovery, attack resolution, damage
   and resistance, conditions, monster presence and AI, corpses and loot — then
   turn-based mode as a second pacing over the same state.
7. **Progression, skills, and magic.** Experience, levels, training, skill points
   and mastery tiers, the schools and their spells, effects by category, and
   alchemy.
8. **Quests, journal, and knowledge.** Quest instances, objectives and turn-in,
   auto notes, history, awards, and discovery.
9. **Breadth and depth passes.** All regions and places, the full monster, item,
   and spell sets, promotions and the light/dark paths, artifacts and relics,
   followers, and the endgame.

Breadth is not a stone that fixes earlier shortcuts: by the time it starts, the
systems underneath are already general, so breadth adds content and tuning rather
than completing mechanisms.

## 6. Non-goals

- Original save-file compatibility, in either direction.
- Loading the original executable, its data-driven extensions, or its mods.
- Byte-exact map geometry, exact table values, or exact balance.
- Reproducing the original's videos, music, or art distribution; extracted media
  is a local development convenience with recorded provenance, never a shipped
  artifact.
- Multiplayer, dedicated server, or a second product runtime.

## 7. Decisions that are expensive to reverse

These are the seams the rest of the work hangs on. Changing one is a deliberate
re-plan, not an implementation detail.

1. **The party is one entity, and it owns the inventory.** Roster, shared
   inventory, equipment-by-member, purse, food, reputation, followers, and
   party-wide effects attach to the party; a character owns only what it wears or
   wields. Party state and world state are different owners.
2. **Real-time and turn-based are two pacings of one combat state.** Not two
   systems, not two scenes, and not a second scheduler.
3. **The world is a graph of places with costed transitions**, and **party
   knowledge is separate from world state**.
4. **One clock, game-time durations.** Every duration, schedule, and respawn is a
   game-time deadline; nothing counts frames.
5. **Services are one mechanism with kinds**, driven by content and ruleset
   policy — never a class per building type.
6. **Ruleset policy over content data.** Formulas, eligibility, ceilings, and
   promotion rules live in the compiled ruleset; numbers and definitions live in
   content; neither is scattered through kit mechanisms.
7. **The UI is a projection plus semantic actions.** Screens own no state, and
   the world keeps running behind them in real time.
8. **The importer output shape is a runtime contract.** Content packs, tuning
   profiles, world geometry, media, and provenance are consumed by runtime code
   and only change deliberately.
9. **One current save schema.** No versions, migrations, or compatibility
   readers during development.
