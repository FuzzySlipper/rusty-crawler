# PartyRpg.Kit

Planned home of the reusable, rules-agnostic mechanisms for party-centric
first-person RPGs: the construction grammar that a compiled ruleset shapes into
a concrete game.

Owns, once implemented:

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
world it steps (`SessionWorld`), the compiled ruleset and session contracts, the pack envelope with its
catalog loader, validator and bundle resolution, the world (`PlaceGraph`, `PlaceGraphLoader`,
`PlaceStateLedger`, `TransitionExecutive` with its required cost contract, and the entrances a walking
party takes — `PlaceEntrance` with its loader, consulted inside the movement step so a step that carries
the party into an entrance's reach travels through that one transition path), the party's pose and
derived view (`PartyPoseOwner`, `FacingRule`, `PartyView`), the structured UI value builder, the
Engine-backed projection channel and the session projection, the admitted-input router that turns
engine events into session commands, the population owner that fills a place from its placements and
empties it on leaving, the Engine-backed movement owner with its vertical and surface policy, the reaches that let a party walk
into a transition, and the movement facts the panel reports.
Everything else in the owner map is still to come.
