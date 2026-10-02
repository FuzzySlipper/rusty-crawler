# World, party, and interaction closure audit

Recorded on 2 October 2026 against the full closure acceptance of Den campaigns
#8456, #8457 and #8458. Child status and dependency completion were read, but
were not used as substitutes for the parent requirements. Exact source identity,
review messages and status decisions live in Den.

## World

The world criteria are met. `SessionWorld.Travel` and `TransitionExecutive` take
walked, used, scripted, paid and magical transitions through one costed path.
`PlaceStateLedger` owns visited, discovered, cleared and respawn state across
visits; `PartyKnowledge` remains separate. The movement owner submits character
steps through the Engine's safe spatial service, with place geometry admitted
and released at the same transition boundary.

The [Emerald Island walk](walk-playtest.md) records real held-key movement,
turning, jumping and stopping on release. Its missing transition caller was
subsequently exercised in the [two-way crossing](walk-transition.md). The
[creation run](creation-and-crossing.md) crosses the Temple of the Moon and
Emerald Island both ways, with clock and food charged. The fare evidence on
Den #8706 additionally records Erathia to the Tularean Forest, and the
[travel-link reading](travel-links.md) records a conditional barrow exit.
Focused transition checks cover the other transition kinds and their refusals.
This supports the general path across different places; it does not certify
every imported route or broad traversal of every map.

## Party

The party criteria are met. The [creation run](creation-and-crossing.md) uses
the actual creation screen, spends the ruleset's attribute budget, chooses
skills, rejects unfinished or illegal choices, and accepts the resulting party.
`PartyEntity` owns one shared inventory; a member owns equipment only. Travel,
rest, waiting and training advance the same `GameClock`, and duration owners
register game-time deadlines with it.

The [save/resume reading](save-resume.md) restores the saved party, clock,
place, pose and visited count after further play and a host restart. Current
semantic checks also carry nonempty records, quests, journal, knowledge and maps
through saved bytes, independently of that live reading. The
[deadline reading](deadline-persistence.md) observes the original fatigue and
party/member effect deadlines after resume.

The original parent described combat as transient. Later authorized #8658
deliberately extends the same current schema to carry the resident fight; its
[bounded live reading](fight-persistence.md) states the all-dead-party limits.
UI projections, interaction focus and Engine runtime identities still rebuild on load.
This later requirement supersedes dropping combat; closure does not undo it.

## Interaction and services

Most criteria are supported. One `PartyServices` operation table and the
ruleset's kind policies serve the imported service kinds; prices, stock,
membership and opening hours are read from their canonical owners. Focused
checks cover trade, lessons, cures, rooms, provisions, banking, training and
fares. The [imported service-panel run](service-panel.md) records actual
transactions, while the [house and camping run](house-ground.md) records a
closed-hours refusal and an open conversation and counter. These staged checks
do not claim an unmodified town traversal or a separate live visit to every kind.
The [container run](container-rest-and-interaction-save.md) records trap damage,
one transfer and named empty-container refusals. The
[door reading](door-collision.md) records a closed obstruction followed by
opening and ordinary movement through the actual imported doorway.

The parent explicitly also requires secret doors to resolve against skills.
The current imported door path offers ordinary Open/Unlock from door state and
authored requirements; generic skill requirements and map-event skill checks
do not demonstrate a secret-door discovery rule. No source-backed mapping from
an imported secret surface to a Perception gate was established in this audit.
At the initial audit this acceptance item remained with #8458, which could not
close on the done status of its children alone.

## Owner-authorized routing

After this audit, the owner explicitly requested a secret-door task under
progression campaign #8460 and closure of #8458. Planned child #9160,
"Discover imported secret doors through Perception", now carries the missing
imported consumer, named successful and unsuccessful outcomes, canonical owner
reuse, durable discovery where applicable, and focused and bounded live
verification. The source campaign's description records that transfer while
preserving the original acceptance and finding. #8458 closes under this routing
decision; the secret-door behavior remains unimplemented until its receiver lands.

## Focused checks

Fresh checks passed with no skips: 121 Kit world, transition, pose, creation,
clock, interaction and container cases; seven host persistence cases; 63
ruleset service, interaction and schedule cases; and 49 Kit persistence,
knowledge and journal cases. Existing imported-data gates and the published
live readings supply the other evidence above. No new live mission, save or
resume was performed for this audit, and no rendered-world or NativeAOT claim
is made.
