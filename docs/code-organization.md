# Code organization: how the repository expresses the game

Status: **design intent. No project exists yet.** This document fixes where
things belong so that the first implementation tasks do not have to invent the
seams. It describes owners and boundaries, not APIs.

Read it with [`gameplay-design.md`](gameplay-design.md), which defines the shape
being expressed, and [`../AGENTS.md`](../AGENTS.md), which owns the vocabulary,
engine, donor, and git rules.

## 1. The layering

```
Rusty.Engine  (immutable package: lifecycle, input, rendering, spatial, services)
      │
PartyRpg.Kit  (rules-agnostic mechanisms and state shapes)          MightAndMagic7.Import
      │                                                             (offline: formats, packs,
PartyRpg.Rulesets.MightAndMagic7  (policy, definitions, meaning)      provenance; not a runtime
      │                                                              dependency)
PartyRpg.Host  (one product entry; selects ruleset, bundle, defaults)
      │
TypeScript UI  (DOM presentation of projections; semantic actions back)
```

The rules are one-directional and enforced by an architecture suite once the
projects exist:

- **Kit never names the game.** No `MightAndMagic`, no edition names, no place,
  class, skill, spell, monster, or item names, no donor names, no source-file
  names. Kit mechanics take ruleset-supplied policy and content-supplied data.
- **The ruleset depends on Kit, never the reverse**, and never on the Host.
- **The Host depends on Kit and the ruleset** and is the only place that names a
  built-in ruleset or bundle.
- **The importer depends on neither** and nothing runtime depends on it.
- **The UI sees projections and actions only** — no product types, no state.

## 2. Where new code goes

| If the task is… | It belongs in… |
| --- | --- |
| A formula, eligibility rule, ceiling, price, or threshold | ruleset policy, over content data or a typed tuning profile |
| A new class, race, skill, spell, monster, item, service, place, or quest | content (definitions), not code |
| A mechanism several systems need (recovery, effects, containers, conversations) | Kit |
| A number that designers will retune | a typed tuning profile in content |
| A quirk of the original's files | `MightAndMagic7.Import` |
| A screen, HUD field, or interaction affordance | Kit/ruleset projection value + UI rendering |
| Original map geometry, sprites, or sounds | importer output under `content/…/imports/` |
| A missing Engine capability | a narrow upstream `rusty-engine` request, then an honest stop |

## 3. Kit owners

Working names; the responsibilities are the contract, the names are not.

| Owner | Owns | Does not own |
| --- | --- | --- |
| Session | The one live session: its clock, party, world, mode, and saved state; composed from a compiled ruleset + bundle + content packs | Rules, formulas, content meaning |
| Ruleset contract | The typed seam a ruleset implements: catalogs it supplies, policy it answers, session services it composes | Any concrete rule |
| Party | **The party entity** and every component attached to it: roster and members, shared inventory, equipment by member, shared purse, food, reputation and fame, followers, party-wide effects, party position in the world. Session mechanisms address the party, not four loose characters | Per-character detail; world state; any per-character pack |
| Character | Per-character attributes, resources, conditions, skills with mastery and level, spellbook, experience, level, rank, path — and **only the items it has equipped** | How a class grows; what a rank allows; items it is not wearing |
| Creation | The character-creation flow and its validation, driven by ruleset-supplied choices and budgets | The ruleset's class tables |
| Skills | Skill catalog shape, per-character skill entries, point spending, tier values, training sources as world entities | Which class may learn what, and to which tier |
| Magic | Spell catalog shape, known spells, casting workflow (validate → cost → target → apply), buffs with game-time duration, item-borne casting | School lists, costs, tiers, and per-spell effect policy |
| Combat | One combat state over the live world: pacing mode, per-actor recovery, turn queue, attack execution, effect and condition application, monster AI coordination, corpses and loot | Damage formulas, monster definitions, condition meanings |
| World | The place graph, entry points, transitions with cost, entity population, spatial stepping, per-place runtime state, spawn and respawn | What a place contains (content), how it looks (Engine + media) |
| Knowledge | Discovery state: automap coverage, notes, obelisk and fountain records, journal history, awards | Quest definitions; world state |
| Interaction | Interaction targets (doors, containers, chests, levers, triggers, people) and the use/search/unlock workflow | Trap and lock policy |
| Dialogue | Conversation state, topic lists, topic availability, keyword responses, quest offers | Who says what (content) |
| Services | One service mechanism with kinds (trade, heal, rest, deposit, train, teach, travel, govern) over content definitions | Prices, stock rules, membership policy, level caps |
| Progression | Experience awards, level-up, skill-point grants, rank promotion, path choice, reputation change | Curves, requirements, effects |
| Quests | Quest instances and their state; objective tracking; turn-in | Quest content |
| Items | Item definitions and instances, **the party's single shared inventory**, per-member equipment, currency, food, containers and loot, identify and repair state | Item values, enchantment rules, treasure tables; per-character packs (there are none) |
| Time | The one game clock and calendar; discrete advancement; schedule and respawn queries; duration deadlines | Schedules and constants (content and ruleset) |
| Content | Pack loading and validation: definitions, tuning, scenario, imported world data, provenance; bundle resolution | Any meaning of the data |
| Presentation | Projections (HUD and screens) and semantic actions; live-debug diagnostics | DOM, layout, styling, or state |
| Persistence | The current-schema session snapshot and restore; what is saved and what is deliberately dropped | Original save formats (out of scope) |

Two Kit rules that prevent most later refactoring:

- **One owner mutates one state family.** Effects, quests, and services request
  changes through the owner that holds the state; they do not reach into it.
- **Cross-owner interaction uses typed RuleEvents and typed notifications**, not
  a generic bus and not a proposal/acceptance ceremony.

## 4. Ruleset owners (MightAndMagic7)

| Owner | Owns |
| --- | --- |
| Identities and catalogs | Classes and races, rank ladders and light/dark pairs, the skill list with per-class and per-rank ceilings, the nine schools and their spells, monsters, items, service kinds, condition meanings |
| Formulas and policy | Attack and defense, damage and resistance, recovery time, spell effects and costs, experience and level costs, training fees, prices, rest and fatigue, trap and lock difficulty, reputation effects |
| Interpretation | What a content definition means here: which definitions are legal, how stock and hours work, which quests gate which promotions, what an imported place is |
| Progression policy | Promotion requirements and order, path choice and its consequences, award and reputation checks |
| Session composition | Assembling the named Kit services with this game's policy, and this game's save meaning |
| Presentation meaning | Which projection fields exist and what the books and screens show |
| Provenance rules | What an imported pack must record about the source it came from |

Skills, spells, classes, items, monsters, and places are **data**. Their numbers
live in content packs and tuning profiles; their interpretation lives here; their
names never appear in Kit.

## 5. Host and product composition

One product entry type, declared once, with the product id, title, UI root,
content root, lifecycle mode, fixed step, declared input intents and mappings,
and content bundles. The Host selects a built-in ruleset and default bundle at an
explicit composition seam and otherwise only starts, pauses, resumes, and stops
the session. It never interprets rules and never reads original game data.

## 6. Content shapes

Four kinds of content, kept separate because they change at different rates and
have different authors:

| Kind | Holds | Author |
| --- | --- | --- |
| Definitions | Catalogs with meaning: classes, races, skills, spells, monsters, items, services, quests, conditions, places | Authored, or generated from imported tables |
| Tuning | One discoverable typed profile tree of adjustable values (curves, prices, coefficients) | Authored |
| Scenario | The starting state: party defaults, placements, spawns, quest state, initial scenario flags | Authored |
| Imported world | Geometry, spatial data, media, and tables normalized by the importer, with provenance | Offline generation from an operator-supplied install |

Rules:

- **Regeneration never overwrites authored content**, and authored content never
  overwrites an import silently.
- Every imported pack records its source: game, release or build, importer
  revision, and what was transformed.
- Original game data is never committed; extracted tables stay in `local/`.

## 7. Import pipeline

```
operator install → readers (archives, rule tables, map geometry, media)
                 → normalized packs under content/partyrpg/imports/<place>/
                 → provenance record + table dumps used as content sources
```

The tool is its own project and is built and tested by `scripts/verify.sh`, so an
import API change cannot leave it silently broken. Differential checks compare
the importer's output against the donor reimplementation where a comparison is
possible. Nothing in the runtime path parses source-shaped game data.

Two known traps that shape the importer, both recorded in the data inventory:
the game's archives do not identify which title they belong to (every MM7 archive
still carries the MM6 version marker), and one archive holds a second, older copy
of rule tables from a different game in the family. A table must therefore be
resolved by an explicit, per-table source, never by "whichever archive opened
first" — that mistake would silently import another game's classes.

## 8. UI contract

- The product publishes a HUD projection and per-screen values; the UI renders
  them and returns semantic actions.
- Screens are values, not state holders. Opening one does not create authority,
  and closing one loses nothing.
- The UI never evaluates rules, never advances time, and never starts a loop.
- Input intents are declared by the Host; the UI reports actions, not keys.

## 9. Modes, update, and pause semantics

One Engine-admitted update drives everything. Inside it, the session has exactly
one mode:

| Mode | Stepping |
| --- | --- |
| Party creation | No world stepping; the creation flow is the authority |
| Adventure (real time) | The world steps every admitted update; screens may be open while it does |
| Combat (real time) | Same stepping, with combat resolution and recovery included |
| Combat (turn-based) | The world steps only in response to a committed player action |
| Service or dialogue screen | The world keeps running unless the mode above says otherwise |
| Rest, camp, travel, training | Discrete clock advancement through the Time owner — not a second loop |
| Menu, save, load | Session is quiescent; no world stepping |

Pause is a session concept, not a thread, timer, or scheduler. Recovery,
durations, and respawns are game-time values, so changing mode never changes what
time means.

## 10. Persistence

A save is a snapshot of the session: the party (members with skills, spells,
progression, and path; the shared inventory; each member's equipment), clock and
calendar, current place and position, per-place world state, knowledge, quest
state, containers and loose world items, and the scenario flags. Transient things
— in-flight combat pacing, open screens, target selections, AI intentions — are
deliberately dropped and rebuilt on load.

One current schema during development. No versions, migrations, compatibility
readers, or original-format support.

## 11. Deliberate non-architecture

Do not build any of these, however convenient they look:

- an ECS/ESS framework, a generic component registry, or a service locator
- a generic event bus, command bus, or message broker
- runtime plugin loading, reflection discovery, C# scripting, or a Lua layer
- a second loop, clock, timer, thread, renderer, or browser authority
- a class per building type, per spell effect, or per quest
- state held by UI screens, or UI-side rules evaluation
- content that carries code, or code that carries content
- schema versions, save migrations, or original-format readers

## 12. What would force a re-plan

The nine decisions listed in
[`gameplay-design.md` §7](gameplay-design.md#7-decisions-that-are-expensive-to-reverse)
— one party entity that owns the inventory, one combat state with two pacings,
the place graph, knowledge versus world state, one clock, services as one
mechanism, policy over data, projection-only UI, and one save schema. Changing
any of them is a deliberate re-plan with the user, not an implementation detail.

The building order in
[`gameplay-design.md` §5](gameplay-design.md#5-building-order-foundations-one-stone-at-a-time)
is the other half of that contract: foundations first, complete and global, never
a vertical slice with stubs waiting to be reconciled.
