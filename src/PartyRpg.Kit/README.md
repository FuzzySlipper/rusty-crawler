# PartyRpg.Kit

The home of the reusable, rules-agnostic mechanisms for party-centric
first-person RPGs: the construction grammar that a compiled ruleset shapes into
a concrete game.

The [foundation acceptance audit](../../docs/evidence/foundation-closure.md) connects the world,
party, clock and persistence owners to their semantic checks and recorded product readings.
Generic skill requirements remain a mechanism; a ruleset connects actual discovery policy to the same
interaction outcome, place-value ledger, knowledge and current save owners.

Owns:

- Party model: roster, members, formation or order, shared currency and party
  inventory over per-character equipment.
- Character mechanisms: attributes, skill and spell catalogs, learning and
  casting workflows, conditions and recovery, and progression: experience,
  levels, skill points, and rank with one owner that moves them (`Progression/`),
  the one award entry every source arrives at (a kill's worth coming from the
  ruleset that reads the creature's own row), the training step a counter
  settles through that owner, the growth a level gives, and the promotion that
  hands a rank over (`Promotion/` for the ladder and the requirements it asks
  for, `PartyProgression.Promote` for the transition itself, and `PartyProgression.Grant` for a rank a
  game's own program judged).
- Magic (`Magic/`): the spell catalog content declares, the one casting workflow — resolve the caster and
  the spell, judge its tier against that character's mastery of its school, resolve the aim, ask the effect
  path whether the casting may go ahead, pay the spell points through the member's own pool, hand the
  casting over — and the effect seam a game fills. The kit knows no spell, no school, no cost, and no
  effect: a definition carries the school's skill, the rung it asks for, the price this caster pays, what it
  is aimed at, and an opaque effect identity, and `ISpellEffectRule` is where every effect is expressed. An aim
  (`SpellTargeting`) is the caster, one member, one opponent, one actor of either side (`Either`, a member first and a
  creature the fight holds otherwise, which the projection publishes as the side `any`), or the band.
- Magic's effect mechanisms (`Magic/Effects/`): what a game's category paths apply through. A duration is
  a deadline on the session's one clock, held by `RunningSpellEffects` and applied through the party's own
  carried effects, so a ward or a light lapses on an advance and not on a count of updates; an effect a
  casting aimed at one character is that character's own entry, with its own deadline, read where it
  applies (`IMemberSpellEffects`) and ended by the clock, by a dispelling, or by the game's own answer about
  a character who no longer carries anything (`RunningSpellEffects.StartOn`); a cast's
  outcome carries named readings of the state it changed (`SpellEffectFact`) rather than a field per
  category; what a spell may be pointed at when its aim names no actor is the effect path's own offer
  (`ISpellAimRule`); what the party sees by is read against the clock's daylight window through the game's
  answer (`PartySight`, `IPartySightRule`); and how far each spell is expressed is the game's own report
  (`SpellEffectCoverage`). The kit still names no spell and no effect: it carries an identity and hands it
  back, which a source law in `tests/PartyRpg.Kit.Tests` (read as syntax bound to symbols) holds it to.
- Magic in the pack (`Magic/SpellItems.cs`): the one casting workflow also takes an item as the spell's
  source — a scroll read once and used up, a charged item that is wielded and spends a use — through the
  game's own reading of its item rows (`ISpellItemRule`) and the party's own item state, so there is no
  second cast path and no second count of what is left. An item's charges are spent through the party
  (`PartyEntity.SpendItemCharge`, `ConsumeItem`): the instance records the uses it has paid for, the game's
  row states its initial capacity; a recharge's reduced instance capacity travels with it, and an item that empties leaves through the inventory's own custody. A charged
  item in hand is the weapon a fight fires (`CombatWeapon`, `ICombatWeaponRule`), so the attack is the
  spell it carries, one charge goes with it, and the recovery it costs is the fight's own answer.
- Alchemy (`Alchemy/`): the one mixing workflow — resolve the character and the two things out of the
  party's own pack, look the pair up in the game's own mixture table, judge the rung its result asks for
  against that character's mastery through the skill entry every ceiling and lesson already reads, ask the
  pack whether it can take what would come out, take both ingredients out through `ConsumeItem`, and put the
  potion back through `AcquireItem` — so a mixture is a transfer of the party's own things and a pack that
  cannot take the result refuses the whole attempt. A pair the game's table states as incompatible is
  carried out as its own row states it: both ingredients are destroyed and what the burst costs the mixing
  character is the game's answer (`IAlchemyRule.Backfire`), applied through the member's one damage entry and
  their own conditions. The kit knows no reagent, no potion, and no recipe: a mixture is two definitions and
  what the table says about them, and the vocabulary law in `tests/PartyRpg.Architecture.Tests` holds it to that. A
  potion's effect is not a mechanism of its own — it is an item that carries one, drunk through the one
  casting workflow with the item as the spell's source, and read at the strength the instance itself states
  (`ItemState.Potency`), which is what makes a potion the way a character with no school gets a spell's
  effect.
- Combat: independently resisted additional damage parts in the same attack plan and resolution,
  with explicitly composed hit observers hearing canonical harm once after health settlement; attack execution, targeting and current target, attack resolution, damage kinds,
  resistance and immunity, conditions a hit leaves, and the thresholds a wound is judged against
  application, real-time and turn-based mode coordination, what a downed creature leaves
  (`CorpseGround`, fed by the fight's own reading), monster presence and AI coordination.
- Loot: the keyed draws one generation makes (`KeyedRolls`, the same keyed draws an attack makes), the candidates content weighs by
  treasure level (`LootTable`, `LootCandidate`, `LootFilter`), the shape of a treasure request
  (`TreasureRoll`), and what one generation produced (`LootYield`). Which numbers a game's tables
  carry and what its levels mean stay the ruleset's.
- World interaction: NPC conversation, services (`Services/` — one `PartyServices` operation table judged before
  anything is settled; a theft is one of its operations — `Steal` takes one line off a shelf without pricing it,
  and `StealFrom` lifts from a person the party stands with — drawn by a game's `IServiceRule.Steal` as a
  `ServiceTheft` and carried out by one step whichever kind it was: coin through the ledger, goods into the pack
  with the stolen mark when the draw says so, the fine onto `PartyDebts`, the deed to `PartyProgression.Deed`, and a
  counter's ban onto `PartyBans`; `Repay` pays coin toward what the party owes on an account a counter collects; a
  `Browse` quotes each offer for its actual member or the visit's chosen coin amount, using the same
  judgments as `Transact`; `ChooseAmount` changes only that transient visit selection and moves no coin;
  a cure ends what its offer `Clears` and leaves what it `Leaves` on a member it ended something for),
  quests (`Quests/` — one owner of what a party has been
  offered, taken, and finished, with definitions a game states, objectives that read the owners already
  reporting them, and one turn-in that pays each reward to its own owner. An optional `IQuestAcceptanceRule`
  judges taking an errand before its stage changes and applies its consequence through existing owners; it supplies `IItemRetentionRule`
  explicitly to its party so `ConsumeItem`, `ReleaseItem` and `SpendItemCharge` share its existing `Needs`
  answer and named refusal. Removal returns `ItemRemoval`; sale, mixing and item casting judge before
  effects or payment. An event can judge retention by definition before giving and spending an item.
  Only the quest owner's already-judged delivery uses the internal custody transfer; meeting an accepted
  item objective still retains its item until turn-in), containers, doors, travel
  between world regions and indoor maps.
- Journal and history (`Journal/` — one owner of what a party has written down: dated lines reported by
  the owners of the events themselves — a place, an errand, a rank, a meeting, a find, or a `Chronicle` line a
  game's content writes whole — with the same event written once, a bounded history that outlives the
  places it happened in, and the five books a session reads its record and its world through), and what
  the party has learned (`Knowledge/` — one owner of the facts it can look up again, keyed so learning the
  same fact twice is one fact, dated by the one clock, bounded beside the history, and deliberately kept
  apart from the world's per-place state so a place the clock restores clears nothing a party knows).
- The automap (`Maps/` — one owner of what a party has walked: a per-place set of squares over the place's
  own map, filled as the party sees ground and never by a place the world restores, bounded by that place's
  own grid, plus the drawing the projection builds from it — the window the game's zoom ladder shows, the
  runs of seen squares, the marks on ground already on the map, and the party's own position and facing —
  with the maps book's page per place reading the same owner. It is its own owner rather than a kind of note
  because a map is keyed by place and shaped by a grid: the knowledge owner is deliberately blind to place
  state and its notes have no room for a thousand squares, which the kit's own source laws hold to).
- Session plumbing: compiled ruleset contracts, typed IDs, bundle and
  content-pack resolution, typed tuning handles, structured UI values, and
  bootstrap of an Engine-admitted session.
- Persistence: the session's one current save schema (`SessionSave` over the
  party's own `PartySave`, `ClockSave`, `WorldSave`, and `CombatSave`), the explicit
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

Implemented today: the session shell (`PartyRpgSession`, `SessionMode`, `IGameSession`), composed one way
from `SessionOwners` (the mechanisms it composes, created empty before the session so a game's answers can
read them when an act arrives, and composed by one sequence per party: a party handed to the session and a
party accepted from creation both reach the owners through the same `SessionOwners.Take`, because
`SessionCreation` answers an accepted party with the very `SessionParty.Playing` a handed party starts from —
both paths must go through that one composition, and the ruleset suite's `SessionCompositionParityTests` fails
naming the owner and the path when one does not), `SessionRules` (a game's answers
grouped by mechanism: `CombatRules`, `ProgressionRules`, `MagicRules`, `AlchemyRules`, `MapRules`),
`SessionControls`, `SessionSaving`, and a `SessionParty` that is either `Playing` a party or `Creating` one
(the session states which start that was as `SessionComposition.PartyStart` — creation, scenario, or resumed —
and the composition block publishes it as `partyStart`; which start a new session takes is the ruleset's);
the update applies a player's acts through `SessionActs`, drives the fight in either pacing through
`CombatDriver` and `ActControl`, hands a conversation's offer to its owner through the exhaustive
`ConversationHandoffRouter` over the closed `HandoffOwner` list (`HandoffOwner.Use` runs what a topic set going as
one use of the speaker's placement through `SessionWorld.Answer`, hands what it taught to the knowledge owner, puts
what it said into the conversation as the person's answer through `PartyConversations.Hear` — an answer handing to
a use may leave its own words blank — and ends the conversation when the use took the party away), and settles save requests through
`SaveRequests`. The session answers a playtest harness between updates without stepping anything:
`IGameSession.Inspect` returns the snapshot its projection is built from (the world read live), and
`IGameSession.Look` turns the party through its pose owner's facing rule when its turn keys would;
`PlaytestReadout` writes that snapshot as the compact observation, including each combat actor's feet position
read from the live population entity or the shared party pose, and states, once, whether the movement keys
would step the party now (`PlaytestReadout.Steering`, refusing with a `PlaytestCodes` code). Every reader takes its actions from one `ActionInbox` per update — each payload parsed
once, each semantic action's name the kit's own constant beside its reader, a product declaring only keys
and contracts — and an action on the session's contracts that nothing took is reported as
`action-unclaimed`; the live
world it steps (`SessionWorld`), the one clock it advances by the admitted interval and the party it
holds and publishes — with the clock's own schedule (`OpeningHours`, `PlaceSchedule`: which hours a place
keeps and when that next changes, read against the clock's position rather than counted in a step) and the
stops a party takes on it (`PartyRest`, `FatigueWatch`, `IRestRule`, `IRestSite`: rest, camp, wait, and the
rest a service provides (`PartyRest.SleepInRoom`, used by rooms and a training visit and sharing the same sleep),
each advancing the one clock by a game-time period, settling the day through the party's own ledger, and
holding the debt of sleep as a deadline the clock brings due; a completed night asks `IRestRule.Unrestored`
which members may benefit, fills and clears only those members, then asks `IRestRule.Rested` what each keeps;
the result names every member left as they were and the rule's reason) — the compiled ruleset and session contracts, the pack envelope with its
catalog loader, validator and bundle resolution (`ContentCatalog.Selected` is the one place a bundle's
selection becomes the content a session reads: the packs it named contribute, and the packs it did not
are not loaded at all, though a broken one still refuses the start with its issue marked `NotSelected` and the
directory it was read from; the loader refuses the whole root by name when two packs claim one id, or an entry
id or a document id is declared twice, so a reader that looks an entry up by id finds the only one there
is), the world (`PlaceGraph`, `PlaceGraphLoader`, which refuses by name what nesting hides from the catalog's
check — a place declaring one arrival-point id twice, ids differing only in case counting as one because that is
how `PlaceDefinition.FindEntryPoint` looks a point up (and that lookup refuses rather than choose on a definition
built some other way), a fare-network crossing under an id another transition holds, and two sold crossings alike
in origin, destination and route, which a ticket could not tell apart; placements are held to one identity each
by `PlacePopulationContent`, and an entrance's loader refuses a world holding one transition id twice,
`PlaceStateLedger`, `TransitionExecutive` with its required cost contract, and the entrances a walking
party takes — `PlaceEntrance` with its loader, consulted inside the movement step so a step that
carries the party into an entrance's reach travels through that one transition path, or, for an entrance that
`raises` a placement instead of naming a transition (a plate in the floor whose event decides where the party
goes), uses that placement through the one interaction workflow (`PartyInteraction.Raise`, a target whose verb is
`InteractionVerb.Tread` and which the reticle never offers) and takes the journey its outcome names; a crossing taken
that way is charged on arrival, its quoted time to the session's one clock and its quoted provisions
to the party's larder through the ledger's one path, exactly once, and a refused transition is charged
nothing; the cost rule's `Quote` reads without spending and `Arrived` settles any rule-owned cost only
after destination and ground admission; a crossing a counter sells names only the `route` it runs on — authored as a travel link, or
answered by the game's `IFareNetwork` over the places read, which is how a ruleset decides a counter's
destinations — and how many days it takes is the game's `IFareDurationRule`, asked by `PlaceGraphLoader` once
per sold crossing; a passage the party holds names the place and the route, never the days, and boarding
matches it on both, so a retune changes the journey without content being written again and never strands a
held or saved ticket), the party's pose and derived view (`PartyPoseOwner`, `FacingRule`, `PartyView`), the party
entity and its attached components (`PartyEntity` over the engine's own entity store, with `PartyRoster` and `PartyMember`, the one shared
`PartyInventory` of `ItemInstance`s beside each member's `CharacterEquipment` — every instance carrying a
durable `ItemInstanceId` and an `ItemState` of identified, damaged, enchanted, hardened, charged, and stolen, and reporting one
`ItemCustody` that is detached, the shared pack, or a single member's slot and nothing else, so a
per-character pack is a state these types cannot express; `Capture` writes each instance's identity, state,
and custody and `Restore` rebuilds them), `PartyPurse`, `PartyFood`,
`PartyReputation`, the running effects on the party and on each member (`ActiveEffects`,
written only by `RunningSpellEffects`), each member's stored base resistances (`CharacterResistances` —
what a permanent gift added, by kind of harm, carried in the member's seed; a ruleset's racial and class terms
are read beside it, never stored), `PartyFollowers` (authored person identities, hired/story kinds and join order;
the factory attaches it and the current save carries it, with no separate follower actors, packs, purses or clock;
the [bounded product reading](../../docs/evidence/followers.md) records its ordinary conversation callers),
`PartyRecords`, `PartyHoldings`, `PartyPassages`, `PartyMemberships`,
`PartyDebts` (what the party owes, account by account, written by a game's crimes and the service mechanism's theft
and repayment), `PartyBans` (the counters shut against the party until a moment of the one clock),
the minting of durable identities in
`PartyIdentitySource`, and `PartyEntityFactory`, which builds a party from creation or from a `PartySave`
and is the only code that attaches a party component, with the one item rule it composes arriving as
`IEquipmentUseRule` — what may be worn is the ruleset's answer over content and tuning, never a skill name
or a slot name in the kit), the one way a player changes what a member wears (`PartyOutfitting`, over a game's
`IEquipmentFigure` — its slots in the order a screen draws them and the slots one item is shaped for — which
fills the slot a request names or the figure's best free one through `PartyEntity.Equip` and `Unequip`, keeps the
answer the last request got, and is reached by the `party.equip` and `party.unequip` actions (`EquipActions`)
and published as the `equipment` block (`EquipmentSnapshot`)), the party's owned resources (`PartyResourceLedger`, the
one path that settles a `PartyCost` against the purse and the larder whole or not at all — refusing with
every shortfall named rather than overdrawing the purse — credits the same two accounts, and spends a
travelling or camping day as a `ProvisionDay`, priced by a ruleset's `IProvisionDayRule`, with `ResourceSettlement` as the
outcome), the
one progression owner (`Progression/` — `PartyProgression` is where experience, a level, a skill
point, and a rank move and nowhere else — the years a character was aged beyond their natural age,
`CharacterProgression.AgeOffset`, are the one exception, written by whatever the game says ages a character or
gives the years back (`Age`, `Rejuvenate`) and carried in the member's seed: `Award` is the one entry a kill, a quest, or any other source arrives at and
divides by the ruleset's own rule, `Deed` is the entry a deed that pays no experience — a theft, a sacrifice, a
worthless death a game still counts — reaches the world's opinion by, through the same standing step, `Gift` gives
one named member experience or skill points outright — a well's gift rather than an earned award, so nothing is
divided and standing does not move — `Train` is what a counter's step settles through — the fee charged by
the party's one ledger, the level's pools grown by the ruleset's class and rank tables, the points granted,
and both pools filled — a training offer may also include a `RestPeriod`, charged only on the first
successful step of its `ServiceVisit` through `PartyRest.SleepInRoom`, whose rules decide recovery and
whose one clock informs every time owner; leaving and returning resets that transient visit (the product's
reading is in [`docs/evidence/training-rest.md`](../../docs/evidence/training-rest.md)) — and `RaiseSkill` is the only way a skill point is spent: it asks the skill policy
for the price of the levels and the ceiling the member's class and rank impose, refuses past that ceiling
with the limit named or with what the pool is short, and charges the pool and raises the skill together, so
a raise that failed leaves the character exactly where they stood — with `Plan` publishing the same answer
to a screen that is only asking, with `ProgressionAwards`
paying each death the fight reports exactly once from the ledger of deaths it is still reading, under the
source word the game names for that death (`kill` unless the game tells them apart), and
`ProgressionSnapshot` publishing the level, the experience against the curve, the points held, and the fee
the counter the party stands at quoted — every number the ruleset's, none of them the screen's — and
`Promote` the ordinary entry a rank arrives at: it asks the promotion policy for its ladder and per-member eligibility (`Promotion/` —
`PromotionLadder` is content's or a ruleset's own table of ranks, each `PromotionRank` naming the class it
promotes from and to, the rank it reaches, the alternative it takes, the record it leaves, and every
`PromotionRequirement` it asks for; `IPromotionRule` is the ruleset's one answer over it), judges each
requirement against the party — a giver the party is speaking with, an item the one inventory holds, a
record the party carries, which is also how a finished errand is asked for, as the record the quest owner
writes when it is turned in — and moves the class and the rank together, so a ceiling, a
growth table, and every class condition read one fact rather than three that could drift. `Grant` is the
same move for one member on terms a game's own scripted program has already judged (a promoter's event): it
asks none of the ladder's requirements again, and judges the member — of the class the rank promotes from, at
the rank it continues from — in the one place `Promote` judges each member too (`JudgeGrant` answers that
judgement before a program settles anything). `PromotionSnapshot`
publishes the ladder a panel shows and the report a rank left: who rose, from which class to which, what
each of them met, and who it passed over with what they were missing), the skill
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
structured UI value builder, the Engine-backed projection channel and the session projection (each block is a sealed
`*Snapshot` record read from its owner that writes its own keys, so a block's wire shape is spelled once; `SessionSnapshot`
requires every block, a session without a mechanism passing that block's `None`, so no unread block reaches
`SessionProjection.Build`, which only composes the writers in order; the session reads the projection on every
running update but keeps the blocks whose owners move only when somebody acts or the clock delivers something —
party, skills, promotion, magic, alchemy, quests, journal, automap, equipment — in `ProjectionReadings`, each
read again only when its own key moves: the `ChangeStamp` of each owner it reads (`PartyEntity.Stamp`, the latest
over every party component, member component and held item, each of which takes a stamp in every mutator; the
quest, journal, knowledge, map, progression, casting, mixing and outfitting owners' own), and the few live facts it
shows besides — the clock's hour for an errand's condition, its minute for the calendar book, the party's pose for
the drawing, the running effects and the fight's sides for the spellbook, and no keeping at all while a detection
marks the map; with its controls block,
`ControlsSnapshot`: each stand-alone control's action, whether the session would take it now, and the key the host bound it
to as `ControlKeys` — so the panel prints every verdict and works none out), the admitted-input router that turns
engine events into session commands, the population owner that fills a place from its placements and
empties it on leaving (a placement that states a request rather than an answer — an encounter asking for some
creatures of a kind — is resolved by the game's `IPlacementExpansion` while the placements are read, and what it
answers stands in its stead for every reader; the game must answer the same on every read, which is why it
draws under a key naming the place and the placement; whether each of what a place holds stands this visit is
asked of the same seam every time the place is populated, `IPlacementExpansion.Stands`, so what a game keeps for the
place — a group its events hid — can hold a placement off the field; something a game creates while the party stands there — a
creature a spell calls up or stands back up — is created by the same owner, `PlacePopulation.Summon`, from a
placement the game states, in the same store and through the same composer, marked `IsSummoned`, ended by
`Dismiss`, by a length the one clock counts down through `SessionWorld`'s own clock observation (`Elapse`), or
by the visit ending, and never rebuilt from content), the Engine-backed movement owner with its vertical and surface policy (and a leap a game asks of the next step,
`PartyMotion.Leap` through `IPartyMover.Leap`, the party's own jump at a stated multiple whose landing is not a fall;
and flight, the engine's flying mode asked for while a game's `IFlightRule` allows it and the party has risen —
`MovementIntent.Vertical` from the optional rise and sink controls of `MovementIntentNames`, a `FlightTuning` on the
`MovementTuning` with its speed and ceiling, a landing when a sink meets the ground, and a fall measured from where a
flight that ends in the air left the party; and a place's named ground, `PlaceSurfaces` read beside its artifact by
`ContentPlaceGeometry` and looked up at the ground point the engine reports, so the mover's `Footing` says when the
party stands in water, and a game's `IGroundHazardRule` harms it there once for every interval the clock crosses,
through `SessionWorld`, while `IRestRule.Stop` may refuse any stop where it stands; `SessionWorld.Ground` reads the
footing, the hazard's interval, the calendar boundary its next harm lands at (`GameCalendar.NextBoundary`, the one
`Endure` counts next) and the rule's `GroundShelter`s, which the movement block publishes as `footing`; and
`IPartyMover.GroundUnder` looks a pose in the party's place up on the same `PlaceSurfaces` at the point it stands on,
`PlaceSpace.GroundPosition`), the reaches that let a party walk into a transition, the movement facts the panel
reports, the one combat state
(`Combat/` — a `CombatState` over the live world and nothing else, with a `Combatant` per party member and
per creature the ruleset recognizes in the party's place, one `Combatant.Recovery` quantity each advanced
from the game time the one clock reports and gated before any `AttackOrder` is applied, `Hostility` as a
ruleset answer about what a thing is plus the fight's own memory of what the party has done to it — a creature
the game answers `Hostility.Allied` for stands on the `Ally` side whatever the party did to it, is driven by the
`CombatDirector` beside the opposition, and is never the party's target — and an
`ICombatRule` seam for recovery values, notice ranges, reach, and names — an act against one creature, a blow or
anything the game calls one through `CombatState.Provoke`, also turns every other standing actor the optional
`ICombatProvocationRule` `CombatRules` names says stands with it — and the one resolution path every
kind of attack takes — the fight consumes the `AttackInitiation` it published, asks the
`ICombatResolutionRule` seam for a chance, a kind of harm, dice, and the target's resistance, rolls them
through keyed `KeyedRolls` under a key that names the attack, applies what is left to whoever owns the
target's health, applies the `CombatCondition` a landed hit leaves, records a `CombatResolution`, and
reports it, with `DamageKindId`, `DamageRoll` (dice, a bonus, a floor, and any `DamageMultiplier` — a run of its dice and a share of its bonus
that counts again by a factor when a draw of its own lands), `Resistance` (a weight or full
immunity), `HitChance` in ten-thousandths, and `AttackPlan` as the vocabulary — a plan may also state a
`Divisor` a defence turns part of the rolled harm aside by before resistance, and a wound that landed may be
turned back onto the attacker through the optional `ICombatReflectionRule` `CombatRules` names, landed on the
attacker's own health and reported as the resolution's `Reflected`; `CreatureHealth` is a
creature's own health, a component attached by the population's `IPlacementComposer` when the creature is
placed (`CreatureComposer` over the game's `ICreatureVitals`, which also attaches the `CreatureEffects` a spell
leaves on a creature — a paralysis, a slowing, a fear, a charm — counted down by the fight's own clock advances
and read by the game's answers), so a fight keeps no tally of its own beside
it; where a placed entity stands is its own too (`PlacePopulationEntity.Pose`, moved only by `MoveTo`); a death
is reported once, from the wound that caused it, to the `ICreatureDeathObserver`s `CombatRules` names; and `CombatState.Vitals`, `IsDown`, and `LastResolution` are
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
engine-backed mover — built over the party's own `EnginePartyMover`, so every creature's character step goes
to the one scene the place's collision was admitted to, steered at the engine's waypoint only when the place's
admission published navigation cells and the engine reports the path reached, and no C# collision anywhere;
the place's collision bounds and sampling width travel beside the unchanged artifact, and `EnginePartyMover`
asks Engine to derive navigation over that same scene for the actual controller body. A derivation budget
refusal retains collision and names why pursuit holds. Queries use feet, not body centres; an unavailable or
unreachable pursuit holds by name and is retried, while an initial stationary body step preserves settling
even without navigation. Backing away retains its existing character-step path. The
[imported pursuit reading](../../docs/evidence/navigation-admission.md) correlates actual entity poses
with a bounded route around an interior wall and arrival within melee reach. Each
step hands the engine the party's controller profile with its ground speeds and acceleration scaled to the pace
the request states (the policy's `SpeedOf`, bounded to a twentieth to four times the profile's), so how far a
creature goes is the engine's answer to its own pace; a
creature whose body starts deeper in collision than the engine's controller recovers, which the engine refuses
with `unresolved-character-controller-penetration`, is stood on the first surface the engine's own ray meets
straight above its feet within the ruleset's settling reach, and one with no such ground, or still refused there,
is held where it stands with a `creature-embedded` refusal the driver reports as `stuck`, never a fault), one damage entry for a character's
own health
(`Party/` — `PartyMember.TakeDamage` is where every wound arrives, a creature's bite and a sprung trap
alike, taking harm into the party's own pool, keeping `CharacterResources.Deficit` for how far past empty
it went (carried in `PartyMemberSave` and restored by the same factory), and asking the `ICharacterHealthRule` seam which condition the wound leaves and which it moves
past), and the one interaction mechanism
(`Interaction/` — an `InteractionTarget` discovered from the place's own placements and the party's pose
rather than from a list, with the engine's own reticle selection composed over the candidates — the product's
one `InteractionSelection`, which outlives every world so an inspection registered once reads the focus the
live world holds, with targeted use off; the Engine query observes distant targets as well as reachable ones,
with each candidate's own reach still controlling selection and use, so an out-of-reach target is refused by
name (the [elevated-use reading](../../docs/evidence/elevation-reach.md) records both paths) — one use
workflow that identifies the target, judges each `InteractionRequirement` in the order the ruleset stated
them, settles what the use costs through the party's one settlement path, asks the ruleset what the use
produces, applies it against the party's owners, records the `InteractionTargetState` that use left and the
place values its outcome kept — named whole numbers every target of the place reads through
`InteractionContext.PlaceValues`, which `InteractionLedger.Capture` carries in the save's world section and a
restore of the place forgets — and the other targets of the place it changed (`InteractionTargetChange`: a lever
reads a door through `InteractionContext.PlaceTargets` and `TargetState` and the mechanism records the door's
new word under the door's own identity), hands the party to a conversation when the outcome names somebody
(`InteractionOutcome.Speaks`, opened by the session as using a person is), takes the party on the journey the outcome
names after recording the use where it was made (`InteractionOutcome.Travels`: an `InteractionTravel` over a transition
the place issues, read by the rule from `InteractionContext.PlaceTransitions` and taken by `SessionWorld.Travel` — a
refused journey is the use's residue; a use a person's word raised, `PartyInteraction.Answer` with the word in
`InteractionTargetRequest.Raised` and `InteractionContext.Raised`, records no state on the person's placement and may
also take a transition the world issues from no place, `IInteractionWorld.WorldIssued`, as `Scripted` travel) or sets it down elsewhere in its own place (`InteractionOutcome.Relocates`,
asserted through `PartyPoseOwner.Enter`, nothing crossed or charged); a use that reached somebody's door and kept the
party outside says so (`InteractionOutcome.KeptOut`), and the session opens no conversation for it nor for a use that led
the party away, and reports an `InteractionResult`; every failure — nothing faced, out of reach, out of sight, a requirement
unmet, a charge the party cannot cover, a ruleset's own refusal, a pack with no room for what was found —
is an outcome with a code and a sentence rather than a silent no-op — and a corpse is a target that
mechanism discovers: `CorpseGround` keeps what the fight read as down, the ruleset hands it back as the
creature's own placement lying where it fell, and searching it is the same workflow a chest goes through),
what a death leaves (`Loot/` — `TreasureRoll` is the shape a treasure rule takes once its format has been
read, `LootTable` draws a weighted candidate at a level behind an opaque `LootFilter`, and `KeyedRolls`
makes every draw under a key that names the death or the container, so the same kill yields the same loot
twice; what the numbers mean stays the ruleset's), and time (`GameClock` over a validated
`GameCalendar` — one explicit `Advance`/`AdvanceAdmittedSeconds` path with a returned `ClockAdvance`
report of the hour, day, week, month, and year boundaries it crossed and the `DeadlineDue` entries it
brought due once each, delivered by the clock itself to every `IGameTimeObserver` registered with it
(`GameClock.Observe`) whoever moved it, so a journey, a rest, a wait and a night at an inn reach the same
owners an admitted update does and no caller forwards an advance by hand, `GameDuration` and `GameDate` values, the `DeadlineId` handles travel, rest,
training, and spell durations register against, `GameCalendar.Boundaries` for a rule that acts every
interval of its own length (a regeneration's five minutes) however the advances were cut, day and night from a `DaylightWindow`, and the
`IWorldTimeSource` day count the world's respawn reads).
Followers are produced by the game's actual conversation and content-event paths. `PartyFollowers` judges
duplicates and the explicitly supplied hired limit; story joins stay outside it. A game's
`IFollowerConversationRule` reads authored names and portraits for the party projection, and
`PartyConversations.OpenFollower` talks to actual joined presence without inventing a world placement.
Departure closes that travelling conversation anchor. Item enchantments likewise have the actual casting,
combat, equipment, counter and current-save producers described above.

The shared `PartyResourceLedger.Find` asks `IFoundGoldRule` to divide found gold before crediting the kept
portion. Container and quest rewards use it; ordinary `Credit` for sales and refunds never does. The game
owns the finding bonuses and salary, and the result names total, share and kept coins.

Persistence landed with the party. `SessionSave` is one current schema and nothing else: the party's own
`PartySave`, `ClockSave`'s elapsed game time and owned deadlines, and `WorldSave`'s place, pose, and per-place state, with no
version field, no migration branch, no compatibility reader, and nothing of the original games' save files.
The bytes are written and read with the engine's own `ProductStateStore` and `JsonProductStateCodec` over
metadata the build generates (`SessionSaveJsonContext`), so nothing on the path discovers a type at runtime.
A save happens only where the product asks for one: `SessionSaveBoundary` is the one writer,
`PartyRpgSession.Save` is the one call, and no admitted update, mode change, or release writes anything. What
a save leaves out is as decided as what it carries — in-flight movement outcomes, cached projections, the
population's runtime entities, engine handles, and every store-local entity identity are composed again on
load. `CombatSave` carries the resident visit's creatures by content identity and ruleset kind, their feet poses,
health, recovery, provocation and remaining effects, created placements and their remaining lives, bodies with
their death incarnations and already-held yields, attack cursor and the one pacing's turn bookkeeping. Restore
uses the existing population composer, health/effect owners, corpse ground and turn owner; it advances no time
and reports no new death or loot roll. The [bounded fight reading](../../docs/evidence/fight-persistence.md)
records an imported creature saved provoked and recovering, then resumed, with all-dead party and startup-time limits. `ICombatSaveRule` gives content-only meaning and recovery bounds before
anything is rebuilt. Each resident creature is either carried or explicitly absent from the saved visit,
so an omitted record cannot silently remove it and a legitimately hidden or previously defeated resident
need not reappear. A missing resident placement, unknown kind, excessive recovery, body whose pose differs
from its fallen creature, or contradictory round is refused with every problem named at once, never only the first. Scenario
flags are the party's own records and travel in its section; what the party did to a place's doors and
containers, each target's incarnation, defeated placements, remaining personal purses and the values each
place keeps are the world's `InteractionLedger` capture. The kit checks placement identity and structure;
the ruleset's `PlacementStateJudge` and `PlaceValueJudge` check their meaning. `WorldDeaths` hears the
fight's completed deaths and records only content placements in that ledger; a party record whose name a ruleset gives a shape is judged by its `PartyRecordJudge`
(`save-record-unknown`). The quests section is
the one that arrived with its owner: it carries every instance a party holds — the stage, the progress
recorded against objectives that are moments rather than states, and the place each offer was taken in —
and no definition at all, because what a quest is means is read from the game's own content when the
document is loaded. A save whose instance names a quest the game no longer states, or a place the world no
longer has, is refused with that instance named rather than resumed as an errand nothing could finish. The
journal section is the party's own history beside it: dated lines carried as the game time they happened at
rather than as dates — the calendar and the starting date are the ruleset's policy and are supplied again on
the way back — with the line's own words frozen as they were written, so a renamed quest or a place the world
no longer carries cannot rewrite what the party did. Its bound is enforced on the way out and on the way in,
and a document that dates a line after the game time it had reached, or records one event twice, is refused
with that line named. The knowledge section carries the party's other record — the facts it has learned —
as the game time each was learned at rather than as dates, with every fact keyed by what it is about and
where, so the same fact is one note; a document that names a kind this build has no word for, says nothing,
dates a note after the game time it had reached, records one fact twice, or exceeds the owner's own bound is
refused with that note named. Nothing in that section is keyed by a place, which is what makes what a party
knows unaffected by a place reset.

The day shape follows the donor's day boundary: a new day takes one ration, the food store is spent down to
empty rather than the day being refused, and the ruleset's consequence for the larder the day left — weakness
on every member — is applied by the ledger that spent it
(`OpenEnroth/src/Engine/Engine.cpp`, the timed-effects party update; `OpenEnroth/src/Engine/Party.cpp`, `SetFood`).
What ends that condition — rest, a cure, a day's recovery — is recovery's work, not the day's. The charge,
the threshold, and the weakened consequence are the ruleset's, so the kit holds none of them.

**Encumbrance is deliberately absent.** The shipped item table has no weight column — its 17 columns are
recorded in [`../../docs/research/mm7-data-inventory.md`](../../docs/research/mm7-data-inventory.md) — the
donor's item state carries no weight field to read (`OpenEnroth/src/Engine/Objects/Item.h`, whose only
size is a grid footprint that the one-shared-pack divergence replaces), and armour's cost there is attack
recovery rather than a carry allowance (`OpenEnroth/src/Engine/Objects/Character.cpp`, `GetAttackRecoveryTime`).
Inventing a weight would invent a limit the game does not have, so the shared pack has none.


A `PlaceGeometry` may also carry a complete authored `PlaceCollisionGeometry`. Its positions and triangles
are supplied by the game's existing content and state owners; `EnginePartyMover` submits them through safe
`ReplaceCollision` in the same session and derives navigation there. The full replacement owns its mesh
identities and does not retain an immutable artifact identity. `InteractionLedger.Changed` notifies the
world to refresh the current place; an unchanged geometry projection skips replacement. The explicitly
composed ledger can be shared with the geometry source before the first entry, including a resumed entry,
and the world releases its subscription when disposed. No Kit owner interprets door state or face bits.

Authored collision may identify mesh parts and the placements whose surfaces they represent. The mover
assigns explicit identities during the complete replacement and rebases each part's triangle indices for
the Engine asset contract. Interaction visibility asks the Engine for the nearest segment hit: the
target's own surface is visible; intervening collision remains an obstruction. This ephemeral mapping
belongs to the admitted geometry, not party state. Ordinary sight and automap queries use the full scene.
The [imported doorway reading](../../docs/evidence/door-collision.md) records ordinary-control traversal
and the separate native closing check, with the staging and observation limits stated.

Clock saves carry each pending deadline's kind, durable subject, member where applicable, due elapsed
millisecond and repeat interval. `IDeadlineOwner` is the same explicit owner roster used for clock delivery:
rest restores its original sleep debt, running effects restore the end of each party or member effect,
and services restore each visited shelf's repeat schedule. Restore retains registration order and uses
new transient handles. Unknown kinds, past due times, impossible repeats, duplicate schedules and effects
absent from their named carrier are refused at load. A save capture cancels or suspends nothing.
Shelf contents and buy-back lots remain transient; preserving the restock schedule does not preserve stock.

The [deadline save/resume reading](../../docs/evidence/deadline-persistence.md) records a successful
ordinary save with fatigue, party light and member wards, then their original ends after loading the
same stored bytes. Exact boundary and malformed-schedule checks are separate focused tests.


`PartyRoster.SelectedMember` is the durable ordinary-order choice over the actual roster, not another
combatant graph. Factory creation chooses the first member; `PartySave` carries the choice and refuses
a member identity outside that roster. `CombatState` judges capability with the existing action rule,
cycles in roster order, and reconciles an incapable choice to the first capable member or none. Recovery
does not alter selection. The ordinary payload/key driver orders only the selected combatant through
the existing attack gate; a recovering choice receives its named refusal. Each new paced player turn
selects its actual actor; changing to a ready off-turn member refuses without spending that turn.
Selection fields and messages are projected for the thin panel, which sends the actual durable identity.

The [bounded member-control reading](../../docs/evidence/member-selection.md) records two ordinary selections, individual attack recovery,
and the recovering member's named refusal.

`PartyRecords.Increment` adds a positive earned amount to a durable record. A `QuestRewardRecord` can
explicitly accumulate instead of replacing its magnitude; the existing once-only `PartyQuests.TurnIn`
settles it through that owner. A payment whose declared counts cannot fit is refused before any reward
settles. Party and quest sections carry both the count and the completed earning instance, so restoration
cannot pay it again. [Focused evidence](../../docs/evidence/counted-deeds.md) covers accumulation,
unchanged ordinary marks, the actual JSON save, and repeated settlement refusal.

An instance property is `ItemEnchantment`: the ruleset identity, positive strength and optional absolute elapsed-clock deadline. Canonical item mutations preserve it, and the current source-generated save carries it with hardening and reduced charge capacity. The kit interprets no property word; ruleset spell effects apply and expire it, and figure/combat policy reads its meaning. Casting, combat and the magic projection share the instance capacity.

`PartyItemUse` applies named non-spell uses of real shared-pack instances through the compiled `IItemUseRule`. The game judges and mutates canonical owners; this workflow keeps only the last answer, with no inventory, timer or saved ledger. `party.item.use` shares the inventory screen's declared payload contract. Equipment projections publish the rule's current power words, permanent gifts and usable rows; the DOM echoes instance and member identities.

`Scene/` draws the live world. `WorldView` (an `IWorldPresenter` the session holds through `SessionRules.View`,
handed the party after every admitted update and released with the session) reads a place's `PlaceScene` from an
`IPlaceSceneSource`: the content path of its `RenderMesh` — the kit's own binary document (`PRMESH01`: positions,
normals, texture coordinates and door travel per vertex, divided into parts drawn independently) — its
`SceneMaterial`s, its doors' identities and its sky. It reads the mesh and opens images through the Engine content
service, binds materials and meshes through the safe Graphics API (a door part rebuilt where `ISceneRule.IsClosed`
puts it: a closed door's corners stand at rest plus travel, the collision's own pairing), aims the camera from the
movement `PlaceSpace`'s eye (`EyeHeight`; the reticle shares its heading and keeps its body-centre reach) along its heading, sets the sky panorama or clear colour and the ambient,
sun and carried lights the rule's `SceneLighting` names (a light changes only when the answer does), and publishes
one snapshot when what it draws changed. A place with no scene and an image the Engine refuses are each said once in
`Notes`; a material without an image is drawn flat grey rather than guessed. It reaches for the Engine's services only
when it first draws. `LoadedPack.Directory` lets a ruleset name a file beside a pack's documents, which the view reads through the Engine. The view also draws each `SceneObject` the rule reports — under its
canonical identity — as a cylindrical billboard cut from its `SceneSprite` atlas (an Engine sprite atlas per group,
opened once and sampled nearest), showing the frame its time selects and the view its facing turns to the eye; a frame
or view change is set on its sprite, and only an object that moved, appeared, left or changed group republishes. The product disables the Engine's default light rig, so the scene is lit only by those lights.
Each `SceneBurst` the rule reports (`ISceneRule.Bursts`, empty by default) is emitted once as an Engine particle burst at
its point; a refused or budget-dropped burst is noted once and changes nothing it marks. `CombatState.RecentBlows`
keeps the fight's last applied orders, each with a growing serial, for a presentation to read without re-deciding;
nothing saves it. The view's notes are also published as `scene-note` diagnostics.
