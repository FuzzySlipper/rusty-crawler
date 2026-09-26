# PartyRpg.Kit

The home of the reusable, rules-agnostic mechanisms for party-centric
first-person RPGs: the construction grammar that a compiled ruleset shapes into
a concrete game.

Owns:

- Party model: roster, members, formation or order, shared currency and party
  inventory over per-character equipment.
- Character mechanisms: attributes, skill and spell catalogs, learning and
  casting workflows, conditions and recovery, and progression: experience,
  levels, and skill points with one owner that moves them (`Progression/`),
  the one award entry every source arrives at (a kill's worth coming from the
  ruleset that reads the creature's own row), the training step a counter
  settles through that owner, and the growth a level gives.
- Combat: attack execution, targeting and current target, attack resolution, damage kinds,
  resistance and immunity, conditions a hit leaves, and the thresholds a wound is judged against
  application, real-time and turn-based mode coordination, what a downed creature leaves
  (`CorpseGround`, fed by the fight's own reading), monster presence and AI coordination.
- Loot: the keyed draws one generation makes (`LootRolls`), the candidates content weighs by
  treasure level (`LootTable`, `LootCandidate`, `LootFilter`), the shape of a treasure request
  (`TreasureRoll`), and what one generation produced (`LootYield`). Which numbers a game's tables
  carry and what its levels mean stay the ruleset's.
- World interaction: NPC conversation, services, quests and journal state,
  containers, doors, travel between world regions and indoor maps.
- Session plumbing: compiled ruleset contracts, typed IDs, bundle and
  content-pack resolution, typed tuning handles, structured UI values, and
  bootstrap of an Engine-admitted session.
- Persistence: the session's one current save schema (`SessionSave` over the
  party's own `PartySave`, `ClockSave`, and `WorldSave`), the explicit
  `SessionSaveBoundary` a save is written through, the `ISessionSaveStore` seam
  the engine's own product state store implements, and the named failure a
  document that does not fit its world is refused with.

Boundary rules:

- No Might and Magic vocabulary, data-file names, or donor-project names.
  Adjustable values arrive as typed tuning handles; authored values arrive from
  content packs; only algorithmic invariants live beside their algorithm.
- Mechanisms begin here when their placement is genuinely uncertain. Do not
  make the kit universal, and do not move ruleset vocabulary here by renaming
  it.
- One owner mutates one state family; cross-owner interaction uses typed
  RuleEvents and typed notifications, never a generic bus.

The owner-by-owner contract — what each Kit owner holds, what the ruleset
supplies, and where new code goes — is in
[`../../docs/code-organization.md`](../../docs/code-organization.md).

Implemented today: the session shell (`PartyRpgSession`, `SessionMode`, `IGameSession`) with the live
world it steps (`SessionWorld`), the one clock it advances by the admitted interval and the party it
holds and publishes — with the clock's own schedule (`OpeningHours`, `PlaceSchedule`: which hours a place
keeps and when that next changes, read against the clock's position rather than counted in a step) and the
stops a party takes on it (`PartyRest`, `FatigueWatch`, `IRestRule`, `IRestSite`: rest, camp, and wait, each
advancing the one clock by a game-time period, settling the day through the party's own ledger, and holding
the debt of sleep as a deadline the clock brings due) — the compiled ruleset and session contracts, the pack envelope with its
catalog loader, validator and bundle resolution, the world (`PlaceGraph`, `PlaceGraphLoader`,
`PlaceStateLedger`, `TransitionExecutive` with its required cost contract, and the entrances a walking
party takes — `PlaceEntrance` with its loader, consulted inside the movement step so a step that
carries the party into an entrance's reach travels through that one transition path; a crossing taken
that way is charged on arrival, its quoted time to the session's one clock and its quoted provisions
to the party's larder through the ledger's one path, exactly once, and a refused transition is charged
nothing), the party's pose and derived view (`PartyPoseOwner`, `FacingRule`, `PartyView`), the party
entity and its attached components (`PartyEntity` over the engine's own entity store, with `PartyRoster` and `PartyMember`, the one shared
`PartyInventory` of `ItemInstance`s beside each member's `CharacterEquipment` — every instance carrying a
durable `ItemInstanceId` and an `ItemState` of identified, damaged, and enchanted, and reporting one
`ItemCustody` that is detached, the shared pack, or a single member's slot and nothing else, so a
per-character pack is a state these types cannot express; `Capture` writes each instance's identity, state,
and custody and `Restore` rebuilds them), `PartyPurse`, `PartyFood`,
`PartyReputation`, `PartyFollowers`, `PartyEffects`, the minting of durable identities in
`PartyIdentitySource`, and `PartyEntityFactory`, which builds a party from creation or from a `PartySave`
and never lets a wrapped party grow a component, with the item rules it composes arriving as
`IEquipmentUseRule`, `IInventoryCapacityRule`, and `IItemStackingRule` — what may be worn, what the shared
pack takes, and how far copies bundle are the ruleset's answers over content and tuning, never a skill
name, a slot name, or a limit in the kit), the party's owned resources (`PartyResourceLedger`, the
one path that settles a `PartyCost` against the purse and the larder whole or not at all — refusing with
every shortfall named rather than overdrawing the purse — credits the same two accounts, and spends a
travelling or camping day as a `ProvisionDay`, priced and judged by a ruleset's `ISettlementRule` and
`IProvisionDayRule` with `SettlementQuote` and `ResourceSettlement` as the answer and the outcome), the
one progression owner (`Progression/` — `PartyProgression` is where experience, a level, and a skill
point move and nowhere else: `Award` is the one entry a kill, a quest, or any other source arrives at and
divides by the ruleset's own rule, `Train` is what a counter's step settles through — the fee charged by
the party's one ledger, the level's pools grown by the ruleset's class and rank tables, the points granted,
and both pools filled — and `RaiseSkill` is the only way a skill point is spent: it asks the skill policy
for the price of the levels and the ceiling the member's class and rank impose, refuses past that ceiling
with the limit named or with what the pool is short, and charges the pool and raises the skill together, so
a raise that failed leaves the character exactly where they stood — with `Plan` publishing the same answer
to a screen that is only asking, with `ProgressionAwards`
paying each death the fight reports exactly once from the ledger of deaths it is still reading, and
`ProgressionSnapshot` publishing the level, the experience against the curve, the points held, and the fee
the counter the party stands at quoted — every number the ruleset's, none of them the screen's), the skill
catalog and its ceilings (`Skills/` — `SkillCatalog` is content's own rows as a ruleset reads them, each
`SkillDefinition` carrying the block it belongs to or `SkillBlock.Unused` for a shipped row the game does
not use, and `ISkillRule` is the ruleset's four answers over them: the catalog, the `SkillCeiling` a class
and rank impose, what a raise costs, and the word a rung reads as — with `SkillsSnapshot` publishing each
member's levels, rungs, ceilings, the level the next point would reach, and either its price or the sentence
that refuses it, so a screen renders a spend rather than working one out, and `SkillRaiseInput` reading the
one control a screen's raise arrives on), the
creation flow (`PartyCreationFlow` over the `PartyCreationOptions` a ruleset supplies — races, classes,
portraits, an attribute pool bought through `AttributeCreationRange` prices, and the skills a class fixes
and offers — taken one step at a time with `CreationMember` as the answer to each, refusing an illegal
choice where it is made with the rule it broke, spending the pool exactly and choosing the promised number
of skills before a step is confirmed, applying a ruleset's `PartyCreationDefaults` through those same
steps rather than beside them, and handing `ToCreation` to `PartyEntityFactory` without minting an
identity — held as the session's creation mode (`SessionMode.Creating` with `SessionCreation` and the
commands `CreationInput` reads), in which the one admitted update does nothing but drive the flow and
the world, movement, and the clock are untouched, and published to the screen as `CreationSnapshot`:
where the flow stands, every choice it offers, and the rule the last illegal choice broke), the
structured UI value builder, the Engine-backed projection channel and the session projection, the admitted-input router that turns
engine events into session commands, the population owner that fills a place from its placements and
empties it on leaving, the Engine-backed movement owner with its vertical and surface policy, the reaches that let a party walk
into a transition, the movement facts the panel reports, the one combat state
(`Combat/` — a `CombatState` over the live world and nothing else, with a `Combatant` per party member and
per creature the ruleset recognizes in the party's place, one `Combatant.Recovery` quantity each advanced
from the game time the one clock reports and gated before any `AttackOrder` is applied, `Hostility` as a
ruleset answer about what a thing is plus the fight's own memory of what the party has done to it, and an
`ICombatRule` seam for recovery values, notice ranges, reach, and names, and the one resolution path every
kind of attack takes — the fight consumes the `AttackInitiation` it published, asks the
`ICombatResolutionRule` seam for a chance, a kind of harm, dice, and the target's resistance, rolls them
through keyed `AttackRolls` under a key that names the attack, applies what is left to whoever owns the
target's health, applies the `CombatCondition` a landed hit leaves, records a `CombatResolution`, and
reports it, with `DamageKindId`, `DamageRoll` (dice, a bonus, and a floor), `Resistance` (a weight or full
immunity), `HitChance` in ten-thousandths, and `AttackPlan` as the vocabulary; `CreatureHealth` is a
creature's own health, a component on the entity's actor attached the first time a fight reads it, so a
fight keeps no tally of its own beside it, and `CombatState.Vitals`, `IsDown`, and `LastResolution` are
what the panel reads; no scene, no second population, no per-kind cooldown, no per-kind damage class, and
no timer); the second pacing of that same state is a reading of it rather than a second fight
(`Combat/` — `CombatPacing` on the state, and a `TurnBasedPacing` that orders the fight's actors by ascending
remaining recovery with the fight's own order breaking ties, lengths a round by the longest recovery any
actor owes, runs an action phase into the party's movement phase, publishes `TurnOrderEntry` rows for the
panel, and carries `TurnAction.Act`, `Skip`, and `Wait` with the fight's own recovery charged for the action a
skip did not take; the fight's `Step` reconciles it with the world every update, so a round begins when a
fight does and lets go when the fight does, and switching the pacing touches nothing else), the driver that gives the opposition its half
(`Combat/` — `CombatDirector` decides for every creature the fight has engaged and orders it through the
same `CombatState.Order` gate the player's control uses, whether it is driving every creature in an update
or taking the one turn a paced round handed it, asks the `IMonsterAiPolicy` seam who is whose
enemy, how fast a creature moves, and what it does with its moment, applies a decision as an order or as a
step through the `ICreatureMover` seam, reports what every creature is doing, and marks a place whose
opposition is all down as cleared through the world's own per-place state; `EngineCreatureMotion` is the
engine-backed mover — one character step per creature in the party's own collision scene, steered by the
engine's navigation when a place has one, and no C# collision anywhere), one damage entry for a character's
own health
(`Party/` — `PartyMember.TakeDamage` is where every wound arrives, a creature's bite and a sprung trap
alike, taking harm into the party's own pool, keeping `CharacterResources.Deficit` for how far past empty
it went, and asking the `ICharacterHealthRule` seam which condition the wound leaves and which it moves
past), and the one interaction mechanism
(`Interaction/` — an `InteractionTarget` discovered from the place's own placements and the party's pose
rather than from a list, with the engine's own reticle selection composed over the candidates, one use
workflow that identifies the target, judges each `InteractionRequirement` in the order the ruleset stated
them, settles what the use costs through the party's one settlement path, asks the ruleset what the use
produces, applies it against the party's owners, records the `InteractionTargetState` that use left, and
reports an `InteractionResult`; every failure — nothing faced, out of reach, out of sight, a requirement
unmet, a charge the party cannot cover, a ruleset's own refusal, a pack with no room for what was found —
is an outcome with a code and a sentence rather than a silent no-op — and a corpse is a target that
mechanism discovers: `CorpseGround` keeps what the fight read as down, the ruleset hands it back as the
creature's own placement lying where it fell, and searching it is the same workflow a chest goes through),
what a death leaves (`Loot/` — `TreasureRoll` is the shape a treasure rule takes once its format has been
read, `LootTable` draws a weighted candidate at a level behind an opaque `LootFilter`, and `LootRolls`
makes every draw under a key that names the death or the container, so the same kill yields the same loot
twice; what the numbers mean stays the ruleset's), and time (`GameClock` over a validated
`GameCalendar` — one explicit `Advance`/`AdvanceAdmittedSeconds` path with a returned `ClockAdvance`
report of the hour, day, week, month, and year boundaries it crossed and the `DeadlineDue` entries it
brought due once each, `GameDuration` and `GameDate` values, the `DeadlineId` handles travel, rest,
training, and spell durations register against, day and night from a `DaylightWindow`, and the
`IWorldTimeSource` day count the world's respawn reads).
Everything else in the owner map is still to come.

Persistence landed with the party. `SessionSave` is one current schema and nothing else: the party's own
`PartySave`, `ClockSave`'s elapsed game time, and `WorldSave`'s place, pose, and per-place state, with no
version field, no migration branch, no compatibility reader, and nothing of the original games' save files.
The bytes are written and read with the engine's own `ProductStateStore` and `JsonProductStateCodec` over
metadata the build generates (`SessionSaveJsonContext`), so nothing on the path discovers a type at runtime.
A save happens only where the product asks for one: `SessionSaveBoundary` is the one writer,
`PartyRpgSession.Save` is the one call, and no admitted update, mode change, or release writes anything. What
a save leaves out is as decided as what it carries — in-flight movement outcomes, cached projections, the
population's runtime entities, engine handles, and every store-local entity identity are composed again on
load — and a document wrong in several places is refused with every problem named at once, never only the
first. A load also does not re-judge what it carries: the rules a party obeys are supplied when it is built,
so a capacity rule that has changed gates new pickups and never loses an item the party already owned. The
sections a session does not own yet — knowledge, quests and journal, containers and loose world items, and
scenario flags — are absent because no owner holds their state; the schema grows a section when one does.

The day shape follows the donor's day boundary: a new day takes one ration, the food store is spent down to
empty rather than the day being refused, and the ruleset's consequence for the larder the day left — weakness
on every member — is applied by the ledger that spent it
(`OpenEnroth/src/Engine/Engine.cpp`, the timed-effects party update; `src/Engine/Party.cpp`, `SetFood`).
What ends that condition — rest, a cure, a day's recovery — is recovery's work, not the day's. The charge,
the threshold, and the weakened consequence are the ruleset's, so the kit holds none of them.

**Encumbrance is deliberately absent.** The shipped item table has no weight column — its 17 columns are
recorded in [`../../docs/research/mm7-data-inventory.md`](../../docs/research/mm7-data-inventory.md) — the
donor's item state carries no weight field to read (`OpenEnroth/src/Engine/Objects/Item.h`, whose only
size is a grid footprint that the one-shared-pack divergence replaces), and armour's cost there is attack
recovery rather than a carry allowance (`src/Engine/Objects/Character.cpp`, `GetAttackRecoveryTime`).
Inventing a weight would invent a limit the game does not have, so the seam is left named instead:
`IInventoryCapacityRule` is asked about the shared pack as a whole and never about a character, which is
the shape the design pins for a later party-wide allowance — the sum over members and followers needs a
hook that can read the party, and today's rule is composed before the party exists and sees only the
pack's contents.
