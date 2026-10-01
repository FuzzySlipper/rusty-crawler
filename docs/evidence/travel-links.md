# Every travel link, and how a party takes it

> Published copy of a point-in-time record (Den #8713, #8577; brought up to date by #9042). It is evidence of what was observed then, not a
> statement of current behaviour. No game data is published: the figures and names are read from the packs
> `mm7import write` produced over the operator's own install; LAN addresses are replaced by `<lan-address>`.

## What changed in how a link is taken

A travel link is one `move-to-map` instruction of a map event, and the donor raises a map event three ways: a
click on a face carrying `FACE_CLICKABLE` or on a decoration (OpenEnroth `src/Engine/Graphics/Indoor.cpp:1397-1416`,
`src/Engine/Graphics/Viewport.cpp:201-213`), a step onto a face carrying `FACE_PRESSURE_PLATE`
(`src/Engine/Graphics/Outdoor.cpp:966-980`, `Indoor.cpp:1491`), and the map's own triggers
(`src/Engine/Evt/Processor.cpp:89-140`, `:203-234`); a person's topic runs the global program instead
(`src/GUI/UI/NPCTopics.cpp:662-666`). The importer no longer turns every face of a link's event into a reach the
party walks into, and no longer takes "the first move" of an event:

- a **clicked face group** (or decoration) whose event moves the party is a `fixture` the party *uses*, standing
  over its faces at their lowest corner; its event's move step names the link it takes;
- a **pressure plate** whose event moves the party is a `place-entrance` reach that *raises* the event's
  `floor-trigger` placement when the party walks onto it;
- either way the ruleset runs the event (`MightAndMagic7Fixtures`), its comparisons and random jumps decide which
  move — if any — the run reaches, and the world takes that journey through `SessionWorld.Travel`, the one
  transition path. A move within the place sets the party down where it names. Nothing travels around that path.

## The accounting, over the operator's install

`mm7import write` states every link's disposition in its summary and in `place-graph.json`; `mm7import verify`
checks the counts.

| disposition | links | meaning |
| --- | --- | --- |
| `used` | 174 | a clicked face group (or decoration) raises the event; using it runs the event |
| `walked` | 11 | a pressure plate raises the event; walking onto it runs the event (310 plates in 13 places) |
| `used-or-walked` | 1 | both (link 4, Castle Gloaming's event 501: 2 clicked faces, 26 plates) |
| `counter` | 2 | links 38 and 47: event 376 of Celeste and of The Pit opens a building as well as moving the party; the building's counter owns the face, and opening it does not run the event's move |
| `spoken` | 3 | links 68, 69, 70: the global program's moves, each raised by a person's topic; choosing it runs the event and the world takes its move as scripted travel (#9042) |
| `unreachable` | 2 | links 5 and 51: nothing in the source map raises the event (below) |

**76 links are reached only under a condition** the event's own branches state — 74 of the 189 a party takes, and
the two counters' moves; the table names each, and who sets each quest bit it compares. Conditions this build can meet in play: a map variable
the place's own fixtures write (54 barrow doors: variable 0 or 1 below 2, or at least 2), a random pick (Zokarr's
Tomb, 50/50; Celeste's edge, 1 in 6 per drop — a pick of its step 1, a move within Celeste, falls through into step
2's drop, as the donor's steps do, so every fall lands in the Bracada Desert), carrying an item (660, both of Castle
Gryphonheart's gated doors), wearing an item (604, every member, to leave The Lincoln), a place's group of creatures down (the
Walls of Mist), and quest bits a shipped fixture sets — 246 (the Giants' shrine, event 452, sets it before moving
the party to Harmondale, which opens Harmondale's shrine, link 139), 196 (Deyja's Watchtower door, event 504, sets it
itself), 132 (Castle Harmondale's door sets it on the first use, link 140; its `move-npc` step runs on as residue).
**Quest bits the global program sets** (#9042): a person's topic now runs the global program's event of its number
through the same interpretation a fixture's event takes, so a bit such an event sets is set in play when its topic
is chosen and its own offer check allows it. Each writer in the table below is named with what raises it. What each
of these bits still waits on:

| bit | gates | set by | raised by | in this build |
| --- | --- | --- | --- | --- |
| 109 | link 47 (The Pit's door, event 376) | `GLOBAL.EVT` 155 | Robert the Wise's only topic, offered once bits 114–117 are held | **set**: the topic runs and sets it (ruleset suite, over the operator's packs); Robert stands in Celeste's hostel only once event 146 moves him there, and the door's own move is a counter's — both #9043 |
| 127 | link 38 (Celeste's door, event 376) | `GLOBAL.EVT` 165 | Tolberti's only topic, offered once bits 110–113 are held | **set**, as 109; Tolberti is moved to The Pit's hostel by event 168 — #9043 |
| 98 | link 141 (and closes 140) | `GLOBAL.EVT` 108 | Hothfarr IX's topic once event 107 turns his slot to it | **no raiser in play**: event 108 first asks which of the dwarves the party has hired, a `hireling` comparison refused by name (#8514) |
| 14 | the first branch of link 180 | `GLOBAL.EVT` 19 | Mr. Malwick's topic once event 18 turns his slot to it | **no raiser in play**: event 19 takes the wand back from Malwick (`npc-set-item`) and changes his greeting (`set-npc-greeting`), neither interpreted; link 180's plain path needs no bit |
| 99, 100 | links 157, 161, 192 | `OUT02.EVT` 37 | the door of the arbiter's house in Harmondale (house 189), one clicked face | **no raiser in play**: the face is the house's (`counter`-owned, #9043 runs such a door's event), and the branches that set the bits are reached only with the good or the evil judge hired (`hireling` 77 or 78, #8514) |

Of the three `spoken` links, 69 and 70 are taken in play when the temples' keepers are asked (the ruleset suite counts
both moves). Link 68, the crossing to Harmondale, is `GLOBAL.EVT` event 33, which William Darvees's topic raises once
Lord Markham's event 4 turns his slot to it; both runs reach `set-npc-greeting` (Markham's before the slot change,
Darvees's before the move) and are refused by name there, so the crossing is not taken yet.

### How `OUT02.EVT` event 37 is raised

#8577 recorded that nothing in Harmondale raises event 37. That was wrong: one face of Harmondale carries it with
the clickable attribute (`0x02001208`), and it is the door of the arbiter's house — event 37's own first steps send a
party holding neither bit to `speak-in-house` 189 (`Arbiter`, the importer's `residence-189`, whose `sourceEvent` is
37). The importer counted the face as the house's (an event that opens a building is the counter's,
`PlaceFixtureEmitter.Owner`), so the link accounts did not name it as a raiser. The donor raises it as any clicked
face's event (`OpenEnroth/src/Engine/Graphics/Viewport.cpp:300-320`): no timer, map-load trigger or other path is
involved. Bits 99 and 100 are set on the branches a party reaches when it brings the good or the evil judge — a
hireling the party carries (`compare hireling 77`/`78`) — to the arbiter, which then moves the judges (`move-npc`),
turns the arbiter's topics and plays the arbiter's movie. The table now names the door as the writer's raiser; what
it waits on is in the row above.

### Links 5 and 51: two events nothing raises

The task named them "places 5 and 51"; they are **links** 5 and 51 — Castle Gloaming (place 51) → The Pit and The
Pit (place 8) → Castle Gloaming, a second road between the two whose events are 502 in `D03.EVT` and 505 in
`D26.EVT`. The reader was checked against the donor's own raising paths before the disposition was accepted:

- no face of Castle Gloaming's 3,542 carries event 502, and none of its 705 face extras does — the extras' event
  list is exactly the faces' (501 ×28, 453 ×9, 452 ×45, 451 ×26, 151–153, 5, 176–182, 376), so no extra is orphaned
  by the decoder; no decoration carries it, and the event holds only its move, no trigger;
- no face of The Pit's 5,482 carries event 505, none of its 1,198 face extras does, no decoration does, and the event
  holds no trigger. The Pit's faces do raise an event **502** (two clickable faces, attribute `0x82008208`), but
  `D26.EVT` holds its own event 502 — a different event, not a move — so the numbers are not a misread of each other.

Every path the donor raises a map's own event through reads a face's or a decoration's event id or a trigger of the
map (the click paths above, `src/Engine/Objects/SpriteObject.cpp:301-476` for an object striking a face,
`src/Engine/Graphics/Collisions.cpp:735` for a creature on a face), so the original game cannot run these two moves
either: they are content the shipped maps never wire up. **Disposition: unreachable, with that evidence.**

## Live: a conditional, used link in the running product

```sh
# the operator's packs written into the worktree's ignored content/partyrpg/imports, plus a hand-written start in
# Barrow VII (place 53) at its Party Start; bundle.json named mm7-tables, mm7-world and the start, and was
# restored afterwards; the hand-written start was removed afterwards
rusty dev --project src/PartyRpg.Host/PartyRpg.Host.csproj --port 4179 --bind-host <lan-address> --live-debug
# input through the harness lane (control/claim, runtime/input, control/release): space to accept the default
# party, key-w held to walk, key-g to use; every reading is playtest.observe or interaction.inspect
```

Barrow VII's east exit is event 502 of `MDK01.EVT`: compare map variable 0 with 2 — below it, link 72 to Barrow IX;
at least it, link 73 to Barrow X. A fresh party's variable is 0. Walked from the Party Start to it:

```text
> interaction.inspect            (party at 176.3, -1120.2)
   84 Leave the Dwarven Barrow 412 -15.27 ...  Visible Ready
> playtest.look -15.27 0
> playtest.action party.use
{"id":"party.use","key":"KeyG",...,"available":true,"reason":null,...}
> playtest.observe
place {'id': '53', 'name': 'Barrow VII'}  pose 176.3, -1120.2, 2.1 @ 1971.4
facing {"target": "fixture", "label": "Leave the Dwarven Barrow", "verb": "pull", "distance": 412.1, "reason": "ready",
        "lastUse": {"outcome": "none"}}  clock 1168-01-01 09:09
> (key-g pressed and released)
> playtest.observe
place {'id': '58', 'name': 'Barrow IX'}  pose -408.2, 294.2, -8.5 @ 1792
facing {"lastUse": {"outcome": "applied", "message": "Leave the Dwarven Barrow leads the party on. The party arrives in Barrow IX."}}
clock 1168-01-02 09:10
```

The use ran the event, its comparison chose link 72 (Barrow IX, arriving at the instruction's own position
`-412, 298, -15`, facing 1792), and the crossing was charged once, on arrival, as every other crossing is. Before the
exits stood at their faces' lowest corner the same exit's point was 190 units above the party's eye and could not
be aimed at from inside its reach — the height change is what made a clicked travel face usable in play.

Not shown live: the Harmondale ↔ Land of the Giants shrines. A staged party at Harmondale's shrine faulted the
runtime within a second, before any use, on a creature step (`unresolved-character-controller-penetration` in
`EngineCreatureMotion.Move`), which is the creature-motion owner's, not the interaction's; the shrines' gate (no
bit 246: the line and no move; bit 246: link 139) and the Giants' side setting the bit and taking link 174 are held
by the ruleset suite over the same packs (`FixturePolicyTests.A_travel_event_takes_the_move_its_branches_reach_and_only_when_the_condition_holds`).

## Every link

| link | from | to | event.step | disposition | raised by | condition | evidence |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 0 | 23 The Erathian Sewers | 3 Erathia | 501.0 | `used` | 1 clicked face(s) |  |  |
| 1 | 23 The Erathian Sewers | 3 Erathia | 502.0 | `used` | 1 clicked face(s) |  |  |
| 2 | 23 The Erathian Sewers | 3 Erathia | 503.0 | `used` | 1 clicked face(s) |  |  |
| 3 | 38 The Maze | 10 Mount Nighon | 501.0 | `used` | 1 clicked face(s) |  |  |
| 4 | 51 Castle Gloaming | 8 The Pit | 501.0 | `used-or-walked` | 2 clicked face(s), 26 pressure plate(s) |  |  |
| 5 | 51 Castle Gloaming | 8 The Pit | 502.0 | `unreachable` |  |  | Place 51 ('d03.blv') raises event 502 from nothing a party does: no face of its 3542 carries the event, none of its 705 face extras carries event 502, no decoration raises it, and the event holds no trigger, so neither a click, a step, a timer nor the map's loading runs it in the donor either. Everything the donor's own code raises a map's event through is read here; the event is content the shipped map never wires up to the party. |
| 6 | 45 The Temple of Baa | 14 Avlee | 501.0 | `used` | 1 clicked face(s) |  |  |
| 7 | 76 The Arena | 2 Harmondale | 501.0 | `used` | 2 clicked face(s) |  |  |
| 8 | 20 The Temple of the Moon | 1 Emerald Island | 100.0 | `used` | 4 clicked face(s) |  |  |
| 9 | 37 Thunderfist Mountain | 10 Mount Nighon | 501.0 | `used` | 1 clicked face(s) |  |  |
| 10 | 37 Thunderfist Mountain | 10 Mount Nighon | 502.0 | `used` | 1 clicked face(s) |  |  |
| 11 | 37 Thunderfist Mountain | 10 Mount Nighon | 503.0 | `used` | 3 clicked face(s) |  |  |
| 12 | 37 Thunderfist Mountain | 10 Mount Nighon | 504.0 | `used` | 1 clicked face(s) |  |  |
| 13 | 37 Thunderfist Mountain | 69 Nighon Tunnels | 505.0 | `walked` | 1 pressure plate(s) |  |  |
| 14 | 37 Thunderfist Mountain | 70 Tunnels to Eeofol | 506.0 | `walked` | 3 pressure plate(s) |  |  |
| 15 | 25 The Tularean Caves | 4 The Tularean Forest | 501.0 | `used` | 1 clicked face(s) |  |  |
| 16 | 25 The Tularean Caves | 49 Castle Navan | 502.0 | `used` | 1 clicked face(s) |  |  |
| 17 | 44 The Titans' Stronghold | 14 Avlee | 501.0 | `used` | 1 clicked face(s) |  |  |
| 18 | 33 The Breeding Zone | 8 The Pit | 501.0 | `used` | 1 clicked face(s) |  |  |
| 19 | 33 The Breeding Zone | 8 The Pit | 502.1 | `used` | 2 clicked face(s) |  |  |
| 20 | 31 The Walls of Mist | 7 Celeste | 501.0 | `used` | 1 clicked face(s) |  |  |
| 21 | 31 The Walls of Mist | 7 Celeste | 502.3 | `used` | 1 clicked face(s) | the place's group 1 (1 of them) are down | 2 ways through the event reach the move; the condition is the shortest one's. |
| 22 | 26 Clanker's Laboratory | 4 The Tularean Forest | 501.0 | `used` | 1 clicked face(s) |  |  |
| 23 | 68 Zokarr's Tomb | 54 Barrow IV | 501.1 | `used` | 6 clicked face(s) | a random pick lands on step 1 (3 in 6) |  |
| 24 | 68 Zokarr's Tomb | 59 Barrow VI | 501.3 | `used` | 6 clicked face(s) | a random pick lands on step 3 (3 in 6) |  |
| 25 | 29 The School of Sorcery | 6 The Bracada Desert | 501.0 | `used` | 2 clicked face(s) |  |  |
| 26 | 28 Watchtower 6 | 5 Deyja | 501.0 | `used` | 2 clicked face(s) |  |  |
| 27 | 28 Watchtower 6 | 5 Deyja | 502.0 | `used` | 1 clicked face(s) |  |  |
| 28 | 43 The Wine Cellar | 13 Tatalia | 501.1 | `used` | 1 clicked face(s) |  |  |
| 29 | 42 The Tidewater Caverns | 13 Tatalia | 501.0 | `used` | 1 clicked face(s) |  |  |
| 30 | 17 Lord Markham's Manor | 13 Tatalia | 501.0 | `used` | 2 clicked face(s) |  |  |
| 31 | 35 Grand Temple of the Moon | 9 Evenmorn Island | 501.2 | `used` | 1 clicked face(s) |  |  |
| 32 | 41 The Mercenary Guild | 13 Tatalia | 501.0 | `used` | 2 clicked face(s) |  |  |
| 33 | 22 White Cliff Cave | 2 Harmondale | 501.0 | `used` | 1 clicked face(s) |  |  |
| 34 | 46 The Hall under the Hill | 14 Avlee | 501.0 | `used` | 1 clicked face(s) |  |  |
| 35 | 47 The Lincoln | 15 Shoals | 501.12 | `used` | 1 clicked face(s) | member 1 wears item 604 and member 2 wears item 604 and member 3 wears item 604 and member 4 wears item 604 |  |
| 36 | 39 Stone City | 11 The Barrow Downs | 501.0 | `used` | 2 clicked face(s) |  |  |
| 37 | 39 Stone City | 69 Nighon Tunnels | 502.0 | `used` | 1 clicked face(s) |  |  |
| 38 | 7 Celeste | 75 The Small House | 376.17 | `counter` | 1 clicked face(s) | quest bit 127 is set | Event 376 also holds 'speak-in-house', so the face is that emitter's target and using it opens what it opens; the event's move (quest bit 127 is set) is not run by it. Quest bit 127 is set by GLOBAL.EVT event 165 (raised by Tolberti's topic (person 87, slot 0)). |
| 39 | 7 Celeste | 6 The Bracada Desert | 451.2 | `walked` | 166 pressure plate(s) | a random pick lands on step 2 (1 in 6) | 2 ways through the event reach the move; the condition is the shortest one's. |
| 40 | 7 Celeste | 6 The Bracada Desert | 451.3 | `walked` | 166 pressure plate(s) | a random pick lands on step 3 (1 in 6) |  |
| 41 | 7 Celeste | 6 The Bracada Desert | 451.4 | `walked` | 166 pressure plate(s) | a random pick lands on step 4 (1 in 6) |  |
| 42 | 7 Celeste | 6 The Bracada Desert | 451.5 | `walked` | 166 pressure plate(s) | a random pick lands on step 5 (1 in 6) |  |
| 43 | 7 Celeste | 6 The Bracada Desert | 451.6 | `walked` | 166 pressure plate(s) | a random pick lands on step 6 (1 in 6) |  |
| 44 | 7 Celeste | 6 The Bracada Desert | 501.0 | `walked` | 4 pressure plate(s) |  |  |
| 45 | 7 Celeste | 31 The Walls of Mist | 502.0 | `used` | 2 clicked face(s) |  |  |
| 46 | 7 Celeste | 50 Castle Lambent | 503.0 | `used` | 3 clicked face(s) |  |  |
| 47 | 8 The Pit | 75 The Small House | 376.20 | `counter` | 1 clicked face(s) | quest bit 109 is set | Event 376 also holds 'speak-in-house', so the face is that emitter's target and using it opens what it opens; the event's move (quest bit 109 is set) is not run by it. Quest bit 109 is set by GLOBAL.EVT event 155 (raised by Robert the Wise's topic (person 83, slot 0)). |
| 48 | 8 The Pit | 27 The Hall of the Pit | 501.0 | `used` | 4 clicked face(s) |  |  |
| 49 | 8 The Pit | 33 The Breeding Zone | 503.0 | `used` | 1 clicked face(s) |  |  |
| 50 | 8 The Pit | 51 Castle Gloaming | 504.0 | `used` | 1 clicked face(s) |  |  |
| 51 | 8 The Pit | 51 Castle Gloaming | 505.0 | `unreachable` |  |  | Place 8 ('d26.blv') raises event 505 from nothing a party does: no face of its 5482 carries the event, none of its 1198 face extras carries event 505, no decoration raises it, and the event holds no trigger, so neither a click, a step, a timer nor the map's loading runs it in the donor either. Everything the donor's own code raises a map's event through is read here; the event is content the shipped map never wires up to the party. |
| 52 | 40 Colony Zod | 12 The Land of the Giants | 501.0 | `used` | 1 clicked face(s) |  |  |
| 53 | 52 The Dragon's Lair | 1 Emerald Island | 101.0 | `used` | 4 clicked face(s) |  |  |
| 54 | 21 Castle Harmondale | 2 Harmondale | 501.0 | `used` | 2 clicked face(s) |  |  |
| 55 | 50 Castle Lambent | 7 Celeste | 501.0 | `used` | 2 clicked face(s) |  |  |
| 56 | 24 Fort Riverstride | 3 Erathia | 501.0 | `used` | 1 clicked face(s) |  |  |
| 57 | 24 Fort Riverstride | 3 Erathia | 502.0 | `used` | 1 clicked face(s) |  |  |
| 58 | 49 Castle Navan | 4 The Tularean Forest | 501.0 | `used` | 2 clicked face(s) |  |  |
| 59 | 49 Castle Navan | 25 The Tularean Caves | 502.0 | `used` | 1 clicked face(s) |  |  |
| 60 | 48 Castle Gryphonheart | 3 Erathia | 501.0 | `used` | 2 clicked face(s) |  |  |
| 61 | 48 Castle Gryphonheart | 3 Erathia | 502.4 | `used` | 1 clicked face(s) | the party carries item 660 |  |
| 62 | 30 The Red Dwarf Mines | 6 The Bracada Desert | 501.0 | `used` | 1 clicked face(s) |  |  |
| 63 | 69 Nighon Tunnels | 37 Thunderfist Mountain | 501.0 | `walked` | 1 pressure plate(s) |  |  |
| 64 | 69 Nighon Tunnels | 39 Stone City | 502.0 | `used` | 1 clicked face(s) |  |  |
| 65 | 70 Tunnels to Eeofol | 12 The Land of the Giants | 501.0 | `used` | 1 clicked face(s) |  |  |
| 66 | 70 Tunnels to Eeofol | 37 Thunderfist Mountain | 502.0 | `walked` | 1 pressure plate(s) |  |  |
| 67 | 19 The Haunted Mansion | 11 The Barrow Downs | 501.0 | `used` | 1 clicked face(s) |  |  |
| 68 | — | 2 Harmondale | 33.9 | `spoken` | William Darvees's topic once GLOBAL.EVT event 4 changes slot 0 to it |  | GLOBAL.EVT event 33 is raised by William Darvees's topic once GLOBAL.EVT event 4 changes slot 0 to it: choosing the topic runs the event, and its move is the world's own, issued from no place. |
| 69 | — | 32 Temple of the Light | 173.0 | `spoken` | Temple of Light's topic (person 89, slot 0) |  | GLOBAL.EVT event 173 is raised by Temple of Light's topic (person 89, slot 0): choosing the topic runs the event, and its move is the world's own, issued from no place. |
| 70 | — | 34 Temple of the Dark | 174.0 | `spoken` | Temple of Dark's topic (person 90, slot 0) |  | GLOBAL.EVT event 174 is raised by Temple of Dark's topic (person 90, slot 0): choosing the topic runs the event, and its move is the world's own, issued from no place. |
| 71 | 53 Barrow VII | 11 The Barrow Downs | 501.0 | `used` | 1 clicked face(s) |  |  |
| 72 | 53 Barrow VII | 58 Barrow IX | 502.1 | `used` | 1 clicked face(s) | map-variable 0 is below 2 |  |
| 73 | 53 Barrow VII | 63 Barrow X | 502.3 | `used` | 1 clicked face(s) | map-variable 0 is at least 2 |  |
| 74 | 54 Barrow IV | 64 Barrow XII | 501.1 | `used` | 1 clicked face(s) | map-variable 1 is below 2 |  |
| 75 | 54 Barrow IV | 57 Barrow III | 501.3 | `used` | 1 clicked face(s) | map-variable 1 is at least 2 |  |
| 76 | 54 Barrow IV | 68 Zokarr's Tomb | 502.1 | `used` | 1 clicked face(s) | map-variable 0 is below 2 |  |
| 77 | 54 Barrow IV | 67 Barrow XV | 502.3 | `used` | 1 clicked face(s) | map-variable 0 is at least 2 |  |
| 78 | 55 Barrow II | 56 Barrow XIV | 501.1 | `used` | 1 clicked face(s) | map-variable 1 is below 2 |  |
| 79 | 55 Barrow II | 65 Barrow V | 501.3 | `used` | 1 clicked face(s) | map-variable 1 is at least 2 |  |
| 80 | 55 Barrow II | 66 Barrow XI | 502.1 | `used` | 1 clicked face(s) | map-variable 0 is below 2 |  |
| 81 | 55 Barrow II | 62 Barrow XIII | 502.3 | `used` | 1 clicked face(s) | map-variable 0 is at least 2 |  |
| 82 | 56 Barrow XIV | 55 Barrow II | 501.1 | `used` | 1 clicked face(s) | map-variable 1 is below 2 |  |
| 83 | 56 Barrow XIV | 64 Barrow XII | 501.3 | `used` | 1 clicked face(s) | map-variable 1 is at least 2 |  |
| 84 | 56 Barrow XIV | 61 Barrow VIII | 502.1 | `used` | 1 clicked face(s) | map-variable 0 is below 2 |  |
| 85 | 56 Barrow XIV | 67 Barrow XV | 502.3 | `used` | 1 clicked face(s) | map-variable 0 is at least 2 |  |
| 86 | 57 Barrow III | 63 Barrow X | 501.1 | `used` | 1 clicked face(s) | map-variable 1 is below 2 |  |
| 87 | 57 Barrow III | 54 Barrow IV | 501.3 | `used` | 1 clicked face(s) | map-variable 1 is at least 2 |  |
| 88 | 57 Barrow III | 64 Barrow XII | 502.1 | `used` | 1 clicked face(s) | map-variable 0 is below 2 |  |
| 89 | 57 Barrow III | 61 Barrow VIII | 502.3 | `used` | 1 clicked face(s) | map-variable 0 is at least 2 |  |
| 90 | 63 Barrow X | 11 The Barrow Downs | 501.0 | `used` | 1 clicked face(s) |  |  |
| 91 | 63 Barrow X | 53 Barrow VII | 502.1 | `used` | 1 clicked face(s) | map-variable 0 is below 2 |  |
| 92 | 63 Barrow X | 57 Barrow III | 502.3 | `used` | 1 clicked face(s) | map-variable 0 is at least 2 |  |
| 93 | 64 Barrow XII | 65 Barrow V | 501.1 | `used` | 1 clicked face(s) | map-variable 1 is below 2 |  |
| 94 | 64 Barrow XII | 57 Barrow III | 501.3 | `used` | 1 clicked face(s) | map-variable 1 is at least 2 |  |
| 95 | 64 Barrow XII | 56 Barrow XIV | 502.1 | `used` | 1 clicked face(s) | map-variable 0 is below 2 |  |
| 96 | 64 Barrow XII | 54 Barrow IV | 502.3 | `used` | 1 clicked face(s) | map-variable 0 is at least 2 |  |
| 97 | 65 Barrow V | 58 Barrow IX | 501.1 | `used` | 1 clicked face(s) | map-variable 1 is below 2 |  |
| 98 | 65 Barrow V | 64 Barrow XII | 501.3 | `used` | 1 clicked face(s) | map-variable 1 is at least 2 |  |
| 99 | 65 Barrow V | 55 Barrow II | 502.1 | `used` | 1 clicked face(s) | map-variable 0 is below 2 |  |
| 100 | 65 Barrow V | 60 Barrow I | 502.3 | `used` | 1 clicked face(s) | map-variable 0 is at least 2 |  |
| 101 | 66 Barrow XI | 60 Barrow I | 501.1 | `used` | 1 clicked face(s) | map-variable 1 is below 2 |  |
| 102 | 66 Barrow XI | 59 Barrow VI | 501.3 | `used` | 1 clicked face(s) | map-variable 1 is at least 2 |  |
| 103 | 66 Barrow XI | 55 Barrow II | 502.1 | `used` | 1 clicked face(s) | map-variable 0 is below 2 |  |
| 104 | 66 Barrow XI | 62 Barrow XIII | 502.3 | `used` | 1 clicked face(s) | map-variable 0 is at least 2 |  |
| 105 | 67 Barrow XV | 54 Barrow IV | 501.1 | `used` | 1 clicked face(s) | map-variable 1 is below 2 |  |
| 106 | 67 Barrow XV | 60 Barrow I | 501.3 | `used` | 1 clicked face(s) | map-variable 1 is at least 2 |  |
| 107 | 67 Barrow XV | 61 Barrow VIII | 502.1 | `used` | 1 clicked face(s) | map-variable 0 is below 2 |  |
| 108 | 67 Barrow XV | 56 Barrow XIV | 502.3 | `used` | 1 clicked face(s) | map-variable 0 is at least 2 |  |
| 109 | 58 Barrow IX | 11 The Barrow Downs | 501.0 | `used` | 1 clicked face(s) |  |  |
| 110 | 58 Barrow IX | 65 Barrow V | 502.1 | `used` | 1 clicked face(s) | map-variable 0 is below 2 |  |
| 111 | 58 Barrow IX | 53 Barrow VII | 502.3 | `used` | 1 clicked face(s) | map-variable 0 is at least 2 |  |
| 112 | 59 Barrow VI | 66 Barrow XI | 501.1 | `used` | 1 clicked face(s) | map-variable 1 is below 2 |  |
| 113 | 59 Barrow VI | 60 Barrow I | 501.3 | `used` | 1 clicked face(s) | map-variable 1 is at least 2 |  |
| 114 | 59 Barrow VI | 62 Barrow XIII | 502.1 | `used` | 1 clicked face(s) | map-variable 0 is below 2 |  |
| 115 | 59 Barrow VI | 68 Zokarr's Tomb | 502.3 | `used` | 1 clicked face(s) | map-variable 0 is at least 2 |  |
| 116 | 60 Barrow I | 65 Barrow V | 501.1 | `used` | 1 clicked face(s) | map-variable 1 is below 2 |  |
| 117 | 60 Barrow I | 67 Barrow XV | 501.3 | `used` | 1 clicked face(s) | map-variable 1 is at least 2 |  |
| 118 | 60 Barrow I | 66 Barrow XI | 502.1 | `used` | 1 clicked face(s) | map-variable 0 is below 2 |  |
| 119 | 60 Barrow I | 59 Barrow VI | 502.3 | `used` | 1 clicked face(s) | map-variable 0 is at least 2 |  |
| 120 | 61 Barrow VIII | 67 Barrow XV | 501.1 | `used` | 1 clicked face(s) | map-variable 1 is below 2 |  |
| 121 | 61 Barrow VIII | 62 Barrow XIII | 501.3 | `used` | 1 clicked face(s) | map-variable 1 is at least 2 |  |
| 122 | 61 Barrow VIII | 56 Barrow XIV | 502.1 | `used` | 1 clicked face(s) | map-variable 0 is below 2 |  |
| 123 | 61 Barrow VIII | 57 Barrow III | 502.3 | `used` | 1 clicked face(s) | map-variable 0 is at least 2 |  |
| 124 | 62 Barrow XIII | 61 Barrow VIII | 501.1 | `used` | 1 clicked face(s) | map-variable 1 is below 2 |  |
| 125 | 62 Barrow XIII | 59 Barrow VI | 501.3 | `used` | 1 clicked face(s) | map-variable 1 is at least 2 |  |
| 126 | 62 Barrow XIII | 66 Barrow XI | 502.1 | `used` | 1 clicked face(s) | map-variable 0 is below 2 |  |
| 127 | 62 Barrow XIII | 55 Barrow II | 502.3 | `used` | 1 clicked face(s) | map-variable 0 is at least 2 |  |
| 128 | 72 Wromthrax's Cave | 13 Tatalia | 501.0 | `used` | 1 clicked face(s) |  |  |
| 129 | 71 William Setag's Tower | 5 Deyja | 501.0 | `used` | 1 clicked face(s) |  |  |
| 130 | 73 The Hidden Tomb | 3 Erathia | 501.0 | `used` | 1 clicked face(s) |  |  |
| 131 | 16 The Dragon Caves | 12 The Land of the Giants | 501.0 | `used` | 1 clicked face(s) |  |  |
| 132 | 16 The Dragon Caves | 12 The Land of the Giants | 502.0 | `used` | 2 clicked face(s) |  |  |
| 133 | 18 The Bandit Caves | 3 Erathia | 501.0 | `used` | 9 clicked face(s) |  |  |
| 134 | 75 The Small House | 7 Celeste | 501.0 | `used` | 1 clicked face(s) |  |  |
| 135 | 75 The Small House | 8 The Pit | 502.0 | `used` | 1 clicked face(s) |  |  |
| 136 | 74 The Strange Temple | 2 Harmondale | 501.0 | `used` | 2 clicked face(s) |  |  |
| 137 | 1 Emerald Island | 20 The Temple of the Moon | 101.0 | `used` | 2 clicked face(s) |  |  |
| 138 | 1 Emerald Island | 52 The Dragon's Lair | 102.0 | `used` | 1 clicked face(s) |  |  |
| 139 | 2 Harmondale | 12 The Land of the Giants | 221.3 | `used` | 12 clicked face(s) | quest bit 246 is set | Quest bit 246 is set by OUT12.EVT event 452 (raised by 17 clicked face(s)). |
| 140 | 2 Harmondale | 21 Castle Harmondale | 301.9 | `used` | 2 clicked face(s) | quest bit 98 is not set and quest bit 132 is set | Quest bit 98 is set by GLOBAL.EVT event 108 (raised by Hothfarr IX's topic once GLOBAL.EVT event 107 changes slot 0 to it). Quest bit 132 is set by OUT02.EVT event 301 (raised by 2 clicked face(s)). |
| 141 | 2 Harmondale | 21 Castle Harmondale | 301.11 | `used` | 2 clicked face(s) | quest bit 98 is set | Quest bit 98 is set by GLOBAL.EVT event 108 (raised by Hothfarr IX's topic once GLOBAL.EVT event 107 changes slot 0 to it). |
| 142 | 2 Harmondale | 22 White Cliff Cave | 302.0 | `used` | 4 clicked face(s) |  |  |
| 143 | 3 Erathia | 23 The Erathian Sewers | 501.0 | `used` | 3 clicked face(s) |  |  |
| 144 | 3 Erathia | 24 Fort Riverstride | 502.0 | `used` | 2 clicked face(s) |  |  |
| 145 | 3 Erathia | 48 Castle Gryphonheart | 503.0 | `used` | 2 clicked face(s) |  |  |
| 146 | 3 Erathia | 48 Castle Gryphonheart | 504.4 | `used` | 2 clicked face(s) | the party carries item 660 |  |
| 147 | 3 Erathia | 24 Fort Riverstride | 505.0 | `used` | 2 clicked face(s) |  |  |
| 148 | 3 Erathia | 23 The Erathian Sewers | 506.0 | `used` | 64 clicked face(s) |  |  |
| 149 | 3 Erathia | 23 The Erathian Sewers | 507.0 | `used` | 5 clicked face(s) |  |  |
| 150 | 3 Erathia | 73 The Hidden Tomb | 508.0 | `used` | 1 clicked face(s) |  |  |
| 151 | 3 Erathia | 18 The Bandit Caves | 509.0 | `used` | 1 clicked face(s) |  |  |
| 152 | 4 The Tularean Forest | 49 Castle Navan | 501.0 | `used` | 2 clicked face(s) |  |  |
| 153 | 4 The Tularean Forest | 25 The Tularean Caves | 502.0 | `used` | 2 clicked face(s) |  |  |
| 154 | 4 The Tularean Forest | 26 Clanker's Laboratory | 503.1 | `used` | 2 clicked face(s) | quest bit 198 is not set | Quest bit 198 is set by D25.EVT event 376 (raised by the door of the house it opens: 1 clicked face(s)), D26.EVT event 376 (raised by the door of the house it opens: 1 clicked face(s)). |
| 155 | 5 Deyja | 27 The Hall of the Pit | 501.0 | `used` | 2 clicked face(s) |  |  |
| 156 | 5 Deyja | 28 Watchtower 6 | 502.0 | `used` | 2 clicked face(s) |  |  |
| 157 | 5 Deyja | 71 William Setag's Tower | 503.3 | `used` | 2 clicked face(s) | quest bit 99 is set | Quest bit 99 is set by OUT02.EVT event 37 (raised by the door of the house it opens: 1 clicked face(s)). |
| 158 | 5 Deyja | 28 Watchtower 6 | 504.2 | `used` | 1 clicked face(s) | quest bit 196 is set | Quest bit 196 is set by OUT05.EVT event 504 (raised by 1 clicked face(s)). 2 ways through the event reach the move; the condition is the shortest one's. |
| 159 | 6 The Bracada Desert | 29 The School of Sorcery | 501.0 | `used` | 2 clicked face(s) |  |  |
| 160 | 6 The Bracada Desert | 30 The Red Dwarf Mines | 502.0 | `used` | 1 clicked face(s) |  |  |
| 161 | 6 The Bracada Desert | 7 Celeste | 503.3 | `walked` | 1 pressure plate(s) | quest bit 99 is set | Quest bit 99 is set by OUT02.EVT event 37 (raised by the door of the house it opens: 1 clicked face(s)). 2 ways through the event reach the move; the condition is the shortest one's. |
| 162 | 9 Evenmorn Island | 35 Grand Temple of the Moon | 501.0 | `used` | 1 clicked face(s) |  |  |
| 163 | 9 Evenmorn Island | 36 Grand Temple of the Sun | 502.0 | `used` | 1 clicked face(s) |  |  |
| 164 | 10 Mount Nighon | 37 Thunderfist Mountain | 501.0 | `used` | 2 clicked face(s) |  |  |
| 165 | 10 Mount Nighon | 38 The Maze | 502.0 | `used` | 1 clicked face(s) |  |  |
| 166 | 10 Mount Nighon | 37 Thunderfist Mountain | 503.0 | `used` | 2 clicked face(s) |  |  |
| 167 | 10 Mount Nighon | 37 Thunderfist Mountain | 504.0 | `used` | 1 clicked face(s) |  |  |
| 168 | 10 Mount Nighon | 37 Thunderfist Mountain | 505.0 | `used` | 2 clicked face(s) |  |  |
| 169 | 11 The Barrow Downs | 39 Stone City | 501.0 | `used` | 2 clicked face(s) |  |  |
| 170 | 11 The Barrow Downs | 19 The Haunted Mansion | 502.0 | `used` | 2 clicked face(s) |  |  |
| 171 | 11 The Barrow Downs | 58 Barrow IX | 503.0 | `used` | 2 clicked face(s) |  |  |
| 172 | 11 The Barrow Downs | 63 Barrow X | 504.0 | `used` | 1 clicked face(s) |  |  |
| 173 | 11 The Barrow Downs | 53 Barrow VII | 505.0 | `used` | 1 clicked face(s) |  |  |
| 174 | 12 The Land of the Giants | 2 Harmondale | 452.1 | `used` | 17 clicked face(s) |  |  |
| 175 | 12 The Land of the Giants | 40 Colony Zod | 501.0 | `used` | 1 clicked face(s) |  |  |
| 176 | 12 The Land of the Giants | 70 Tunnels to Eeofol | 502.0 | `used` | 1 clicked face(s) |  |  |
| 177 | 12 The Land of the Giants | 16 The Dragon Caves | 503.0 | `used` | 3 clicked face(s) |  |  |
| 178 | 12 The Land of the Giants | 16 The Dragon Caves | 504.0 | `used` | 3 clicked face(s) |  |  |
| 179 | 13 Tatalia | 43 The Wine Cellar | 501.0 | `used` | 1 clicked face(s) |  |  |
| 180 | 13 Tatalia | 41 The Mercenary Guild | 502.6 | `used` | 2 clicked face(s) | quest bit 14 is set and quest bit 190 is set | Quest bit 14 is set by GLOBAL.EVT event 19 (raised by Mr. Malwick's topic once GLOBAL.EVT event 18 changes slot 0 to it). Quest bit 190 is set by D29.EVT event 378 (raised by the map's own trigger), GLOBAL.EVT event 193 (raised by Niles Stantley's topic once GLOBAL.EVT event 191 changes slot 0 to it; Niles Stantley's topic once GLOBAL.EVT event 192 changes slot 1 to it), OUT02.EVT event 51 (raised by the map's own trigger). 3 ways through the event reach the move; the condition is the shortest one's. |
| 181 | 13 Tatalia | 42 The Tidewater Caverns | 503.0 | `used` | 1 clicked face(s) |  |  |
| 182 | 13 Tatalia | 17 Lord Markham's Manor | 504.0 | `used` | 2 clicked face(s) |  |  |
| 183 | 13 Tatalia | 72 Wromthrax's Cave | 505.0 | `used` | 3 clicked face(s) |  |  |
| 184 | 14 Avlee | 44 The Titans' Stronghold | 501.0 | `used` | 1 clicked face(s) |  |  |
| 185 | 14 Avlee | 45 The Temple of Baa | 502.0 | `used` | 2 clicked face(s) |  |  |
| 186 | 14 Avlee | 46 The Hall under the Hill | 503.0 | `used` | 1 clicked face(s) |  |  |
| 187 | 15 Shoals | 47 The Lincoln | 501.0 | `used` | 12 clicked face(s) |  |  |
| 188 | 32 Temple of the Light | 7 Celeste | 501.0 | `used` | 1 clicked face(s) |  |  |
| 189 | 34 Temple of the Dark | 8 The Pit | 501.0 | `used` | 2 clicked face(s) |  |  |
| 190 | 36 Grand Temple of the Sun | 9 Evenmorn Island | 501.0 | `used` | 1 clicked face(s) |  |  |
| 191 | 27 The Hall of the Pit | 5 Deyja | 501.0 | `used` | 1 clicked face(s) |  |  |
| 192 | 27 The Hall of the Pit | 8 The Pit | 502.3 | `used` | 1 clicked face(s) | quest bit 99 is set | Quest bit 99 is set by OUT02.EVT event 37 (raised by the door of the house it opens: 1 clicked face(s)). 2 ways through the event reach the move; the condition is the shortest one's. |
