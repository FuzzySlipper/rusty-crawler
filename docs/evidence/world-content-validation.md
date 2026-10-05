# Whole-world content validation

Recorded on 5 October 2026 for Den task #8510 (provenance only). This is structural
coverage and a bounded traversal observation, not acceptance of ordinary access to every place.

## Source and reproduction

The operator's Might and Magic VII English install identifies itself as Update v. 1.1,
build 1207658916, with container fingerprint `8633b4033a5a7353`. No original data or
converted assets accompany this record. The summarized source measurements are in
[`world-source-validation.json`](world-source-validation.json); the per-place inventory
and generated places-table digest are in [`world-content-inventory.json`](world-content-inventory.json).

`scripts/developer-launch.sh prepare-content --install <operator-install> --output <fresh-output>`
passed 91 source checks, decoded all 76 maps, emitted all 76 geometries with no refusals,
and compared two writes as identical. A second fresh `mm7import write --check-determinism`
after the arrival normalization also completed with identical output.

The focused importer suite (`PackWriterTests|IncomingArrivalTests`) passed 22 cases.
The architecture suite passed 21. With `CRAWLER_IMPORTED_CONTENT` set to the fresh content root
and `CRAWLER_WORLD_REPORT` set to the report destination, the ruleset suite filter
`ImportedSessionTests|WorldContentInventoryTests` passed four cases without skips.
The structural check loads the canonical ruleset graph, interaction validation and populations;
it checks place/geometry/map/render identity sets, named arrivals, transition resolution,
entrance-to-placement identity, and people/service/monster/event placement references.

## Arrivals and reachability

There are 13 regions and 63 interiors. The source has 83 decorated entry points. Five
interiors have none, although incoming map events provide explicit arrival poses:
Bandit Caves, Erathian Sewers, Barrow IX, William Setag's Tower and Wromthrax's Cave.
Naming those nine poses yields 92 entry points and no place without one. Each added point
records its original travel-link, event program, event id and step. Existing travel-link
poses are unchanged; no origin fallback or invented Party Start is used.

The source distinction is documented by OpenEnroth
`src/Engine/Evt/EvtInterpreter.cpp:117-135` (explicit coordinates versus Party Start)
and `src/Engine/PartyPlacement.h:14-27` (retained orientation). Donor source was consulted
for semantics, not translated or copied.

The canonical graph has 305 transitions, including 85 fare transitions, and 1,049
entrances. Its optimistic reachable set from Emerald Island contains **76 of 76 places**.
This calculation admits all world-issued conversation arrivals and ignores conditional gates;
it excludes source events classified as unreachable. It validates graph connectivity, not that
all 76 places have been visited or every quest gate exercised in ordinary play. The refreshed
`ImportedSessionTests|WorldContentInventoryTests` run passed all four cases without skips.

The outdoor boundaries, Arena fare and bottle route are included in the canonical graph.
Their route-specific ordinary-control observations and donor provenance are retained in
[`world-routes-validation.md`](world-routes-validation.md).

## Explicit source exclusions

All 136 defined service counters have placements. Of 195 households containing people,
192 have source-backed entrances. The remaining source records are explicitly excluded
from the imported accessible-house inventory:

| House | Resident | Reason |
| --- | --- | --- |
| 291 | Poundlin Lotts, person 125 | Erathia event 74 can open the house, but no decoded local face raises it. There is no source-backed entrance location. |
| 453 | Shiara FireOpal, person 426 | The building table reserves the id but gives it no type, name or map. It is not a defined building. |
| 521 | Thomas Grey, person 48 | No decoded map face raises an event opening this house. There is no source-backed entrance location. |

The fresh source event inventory contains no move-person operation relocating these three
residents or moving someone into these houses. The exclusion preserves the source gap rather
than joining unrelated maps' event numbers or assigning an arbitrary face. It does not claim
that the three residents are available in ordinary play. A future authored placement would be
a deliberate content addition, not recovery of an established source entrance.

## Bounded ordinary traversal

One continuous owned browser session (`db64165b-586b-4e51-a35c-5dea06876777`, slot 2)
used the `crawler-9463-arena` isolated profile with the fresh imported packs. The initial
scenario supplied high health, 100,000 coins, 50 food, equipment and a Temple in a Bottle,
and placed the party beside the real Harmondale stable door. This tests traversal, not
acquisition, survival balance or unaided discovery. Gameplay code matched the validated
checkout; subsequent changes before this observation were evidence documents only.

The observer used ordinary controls throughout this connected itinerary:

1. [Harmondale stable](world-content/harmondale-start.png): G opened Christian's
   conversation, then the visible counter and Erathia fare were selected.
2. [Erathia arrival](world-content/erathia-arrival.png): the outdoor view and map title
   changed; feedback states 50 coins and two days, and the purse became 99,950.
3. [Strange Temple arrival](world-content/temple-arrival.png): the character inventory's
   bottle Use control moved the party into the interior and explicitly retained the item.
4. [Harmondale return](world-content/harmondale-return.png): ordinary movement and G at
   the actual temple exit produced outdoor scenery, the Harmondale title and arrival feedback.
5. [Retained bottle](world-content/bottle-retained.png): the inventory still held the bottle.

Read-only interaction inspection guided the exit approach; it did not move the party or
activate the fixture. The parent inspected the original initial, fare-arrival, temple and
return images. The party acquired Weak (1), and food fell to 49; the trip still completed.
This establishes a two-region and interior sample, not a complete walkthrough of the world.

The original action timeline, capture/frame identities, assistance and cleanup receipt are
retained in [captures.json](world-content/captures.json).

The private host stopped with `stopped: true`, `released: true` and `browser_closed: true`.
The earlier Bandit Caves observation established only an exit to Erathia with an incapacitated
party; no re-entry or multi-region claim is based on that earlier run.
