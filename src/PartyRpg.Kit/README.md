# PartyRpg.Kit

The home of the reusable, rules-agnostic mechanisms for party-centric
first-person RPGs: the construction grammar that a compiled ruleset shapes into
a concrete game.

Owns:

- Party model: roster, members, formation or order, shared currency and party
  inventory over per-character equipment.
- Character mechanisms: attributes, skill and spell catalogs, learning and
  casting workflows, conditions and recovery, progression bookkeeping.
- Combat: attack execution, targeting and current target, damage and effect
  application, real-time and turn-based mode coordination, corpse and loot
  machinery, monster presence and AI coordination.
- World interaction: NPC conversation, services, quests and journal state,
  containers, doors, travel between world regions and indoor maps.
- Session plumbing: compiled ruleset contracts, typed IDs, bundle and
  content-pack resolution, typed tuning handles, structured UI values, and
  bootstrap of an Engine-admitted session.

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
holds and publishes, the compiled ruleset and session contracts, the pack envelope with its
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
creation flow (`PartyCreationFlow` over the `PartyCreationOptions` a ruleset supplies — races, classes,
portraits, an attribute pool bought through `AttributeCreationRange` prices, and the skills a class fixes
and offers — taken one step at a time with `CreationMember` as the answer to each, refusing an illegal
choice where it is made with the rule it broke, spending the pool exactly and choosing the promised number
of skills before a step is confirmed, applying a ruleset's `PartyCreationDefaults` through those same
steps rather than beside them, and handing `ToCreation` to `PartyEntityFactory` without minting an
identity), the structured UI value builder, the
Engine-backed projection channel and the session projection, the admitted-input router that turns
engine events into session commands, the population owner that fills a place from its placements and
empties it on leaving, the Engine-backed movement owner with its vertical and surface policy, the reaches that let a party walk
into a transition, and the movement facts the panel reports, and time (`GameClock` over a validated
`GameCalendar` — one explicit `Advance`/`AdvanceAdmittedSeconds` path with a returned `ClockAdvance`
report of the hour, day, week, month, and year boundaries it crossed and the `DeadlineDue` entries it
brought due once each, `GameDuration` and `GameDate` values, the `DeadlineId` handles travel, rest,
training, and spell durations register against, day and night from a `DaylightWindow`, and the
`IWorldTimeSource` day count the world's respawn reads).
Everything else in the owner map is still to come.

The day shape follows the donor's day boundary: a new day takes one ration and leaves every character weak
once the party is past a day without rest, and the food store is spent down to empty rather than refused
(`OpenEnroth/src/Engine/Engine.cpp`, the timed-effects party update; `src/Engine/Party.cpp`, `SetFood`).
The charge, the threshold, and the weakened consequence are the ruleset's, so the kit holds none of them.

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
