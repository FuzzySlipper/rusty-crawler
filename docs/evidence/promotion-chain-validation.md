# Promotion content validation

Evidence for task 8512. This is an approximate recreation: shared item custody, canonical
party records and content-defined class edges retain the quest structure without reproducing
per-character packs or original save formats.

## Rank reachability

The [numeric route inventory](promotion-chain/rank-reachability.json) records every rank,
its preceding class, giver's actual world placement or conversation event, offer/completion
program, quest bit and proof producer. It contains 36 ranks, 27 promotion edges, nine first
promotions, eighteen second alternatives and eighteen initial givers. The nine base ranks
are party-creation choices.

| Base | First promotion | Light alternative | Dark alternative |
| --- | --- | --- | --- |
| Knight | Cavalier | Champion | Black Knight |
| Thief | Rogue | Spy | Assassin |
| Monk | Initiate | Master | Ninja |
| Paladin | Crusader | Hero | Villain |
| Archer | Warrior Mage | Master Archer | Sniper |
| Ranger | Hunter | Ranger Lord | Bounty Hunter |
| Cleric | Priest | Priest of the Light | Priest of the Dark |
| Druid | Great Druid | Arch Druid | Warlock |
| Sorcerer | Wizard | Arch Mage | Lich |

The inventory was checked against a fresh offline import of the operator's English
Update 1.1 installation, build 1207658916, container fingerprint `8633b4033a5a7353`.
Its input digests identify the inspected normalized files; no original quest text, maps,
media or item tables are redistributed here. `global.evt`, each map's `.evt`, `npcdata.txt`,
`npcgreet.txt`, `quests.txt` and the imported placements supply the numeric references.

`PromotionReachabilityTests` executes all 27 real completion programs with explicit proof
fixtures and four eligible members. Removing the ordinary completion proof leaves the
rank unchanged; supplying it grants the expected class and rank. Attempting a sibling
second promotion then leaves the character on the chosen branch. The tests also exercise
the preceding first promotion before checking each second-rank offer's story condition.
The class graph, givers, proofs and alternatives are loaded from content; there is no
compiled list of promotion edges.

Two first promotions give their proof through a remote conversation. Initiate's water
fixture (`68.376`) opens NPC 55 only after quest 27 was accepted. Hunter's Faerie King
conversation (`46.316`, NPC 52, global 83) requires quest 37 in its offer check. Their
completion programs alone are not evidence that an unoffered dialogue can be selected.

The source's party-wide Light/Dark story records (99/100) gate second-quest offers; the
prior class and promotion awards also participate where the source states them. The
loaded previous-class edges make alternatives exclusive per character. This does not
claim a single story playthrough can offer both branches.

## Quest ownership and restored callers

Imported event programs own offers, item/follower deliveries, awards and quest-bit changes.
The Journal reads active, nonempty quest-table notes from those same party records through
`IQuestNotesRule`; it creates no parallel reward-bearing quest instance. Authored objective
quests still use `PartyQuests`. The former compiled reach-only promotion substitutes are
removed: standing in a dungeon cannot substitute for its proof.

- The Haunted Mansion sets proof 140 in `19.2` on an admitted departure, after its
  defeated-actor predicate succeeds. A focused test expands the actual encounter content
  with a deterministic test random service, checks both live and defeated populations,
  and separately exercises session departure dispatch. Entering alone gives no proof.
- The Hidden Tomb sets proof 242 after opening chest index 6 in `73.181`. The existing
  container workflow judges its trap and loot; the retained event runs its remaining
  effects. Unlocking and disarming are explicitly checked not to grant that proof.
- The Soul Jar chest uses the event actually attached to its map faces (`31.176`),
  rather than unrelated programs that happen to mention the same chest index. Quest 48
  admits the branch yielding four Soul Jars and proof 148. A focused regression checks
  both the absent-quest refusal and the actual item yield.
- The Golem's personal topic uses the joined companion through the ordinary conversation
  router, without inventing a world placement. Its six item deliveries, seventh assembly
  action, quest-note updates and disappearance of the assembly topic are exercised through
  the composed session.
- Entry and departure hooks are ruleset actions inside the admitted session update and
  canonical world transition. There is no additional update loop or scheduler.

Donor semantics were checked in OpenEnroth `src/Engine/Evt/Processor.cpp` (map entry and
leave dispatch), `src/Engine/Evt/EvtInterpreter.cpp` (dialogue offer checks and event
execution), `src/Engine/Objects/Character.cpp` (quest records and class writes), and
`src/GUI/UI/Books/QuestBook.cpp` (active quest notes). Donor code was not copied or translated.

## Evidence limits

The 36-rank report establishes source routes and exercised completion semantics, not
36 manually played acquisition expeditions. Item placements and predecessor programs are
identified in the inventory. The ordinary-play check below stages its initial party,
materials and story prerequisite; those initial conditions are not acquisition evidence.

The Soul Jar program's decoration-variable write uses the existing place ledger, separately
from map variables. Its recorded value is accepted by the same native-save boundary that
validates other place values. The test checks that the write does not overwrite the map
variable with the same numeric slot.

## Automated checks

- Ruleset: 474 passed with the fresh operator import, no skips. This includes all promotion
  completion/offer checks and the composed companion, departure and chest regressions.
- Importer: 218 passed, no skips, including the real operator event-argument check.
- Kit: 855 passed, including projection-cache and silent/visible journey-feedback cases.
- Host: 108 passed. Architecture: 21 passed. DOM UI: 106 passed.

The fresh import contains 1,062 map programs, 101 entry hooks, 13 departure hooks and
57 timed programs. Lifecycle hooks do not count as timers.
Every retained mixed chest program corresponds to a raised surface event; no chest index
is claimed by multiple retained programs in this import. These counts describe this
source installation, not every possible authored pack.

## Ordinary promotion chain

A fresh hosted New Game used the normal world controls, conversations, companion button,
Journal, Character/Inventory, Save and Resume. The disclosed starting scenario placed four
healthy Sorcerers near Thomas Grey in the School of Sorcery, with six Golem pieces, Divine
Intervention, ample funds/food and the prior Light-story record. It did not stage either
promotion or Golem assembly.

1. Thomas's Wizard offer added the [current quest note](promotion-chain/wizard-current-journal.png).
2. Six ordinary Golem Parts choices consumed the pieces; a seventh [assembled the Golem](promotion-chain/golem-assembly-choice-7.png).
3. Returning to Thomas granted Wizard, then the Archmage offer added the next quest.
4. Presenting the book produced the [completion dialogue](promotion-chain/archmage-completion.png)
   and [Arch Mage character identity](promotion-chain/character-after-archmage.png).
5. An ordinary save and confirmed Resume preserved [Arch Mage](promotion-chain/character-after-resume.png),
   the companion and the [retained Divine Intervention book](promotion-chain/inventory-item-after-resume.png).

Book retention is intentional source behavior: global event 95 compares item 487 and clears
quest records 47/226, but does not subtract the item. The visible dialogue says the party
may keep a copy. The six Golem parts are consumed by global 104. This observation proves the
ordinary assembly, promotion and persistence interactions from disclosed initial conditions;
it does not prove collecting those items or completing the Light story through an expedition.

The parent inspected original captures. Capture sidecars preserve action/frame metadata;
they report frame correlation uncertain where the runtime advanced during capture. This is
visible interaction evidence, not a performance or exact-frame claim. The
[observer report](promotion-chain/playtest-report.json) records the staged inputs, actions
and clean browser/host shutdown; the [capture index](promotion-chain/captures.json) identifies
the retained original images and their hashes.

The replay used the promotion candidate before the chest, timer-accounting, projection-cache
and silent-lifecycle feedback corrections. Fresh-import regressions separately cover those
corrections. The observed bookkeeping-only entry toast is now suppressed through the normal
action-feedback projection; lifecycle events with source dialogue still show it. That feedback
correction passed automated checks but was not replayed visually.
