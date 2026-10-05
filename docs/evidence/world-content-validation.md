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

The canonical graph has 277 transitions, including 84 fare transitions, and 1,023
entrances. Its optimistic reachable set from Emerald Island contains **72 of 76 places**.
This calculation admits all world-issued conversation arrivals and ignores conditional gates;
it excludes source events classified as unreachable. It is an upper bound, not proof that
the other 72 places can all be reached physically or that every conditional event succeeds.
Shoals, The Lincoln, The Strange Temple and Arena remain outside even this upper bound.
The report deliberately records `allPlacesReachableEvenOptimistically: false`.

The missing route families are visible in donor references: outdoor edge travel and the
Avlee/Shoals suit check in `src/Engine/Graphics/Outdoor.cpp:68-123,282-327`, Arena route 34
in `src/GUI/UI/Houses/Transport.cpp:38-95`, and Temple in a Bottle use in
`src/Engine/Objects/Character.cpp:3550-3552` with arrival in `src/Engine/Engine.cpp:1501-1510`.
The existing runtime fare network joins placed stable/boat counters; its ordinary item-use
owner has no bottle travel action. Portal and beacon casting require a prior visit or recorded
beacon and therefore cannot establish these first visits. This record does not substitute
debug travel or extra generic portals for those behaviors.

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

Owned browser session `4eb4f338-6163-48c2-a7f6-41abfaccc02b`, slot 3, used the fresh
packs in an isolated checkout. Its scenario changed only the new-party starting place to
Bandit Caves and selected the derived `arrival-151`; the normal party creation and runtime
were retained. Source coordinates were supplied to the observer as labelled navigation help.

The observer completed ordinary creation, turned and moved with held keyboard inputs,
then used G. Capture `e5cc5ace-9e16-494a-bf0c-3a24dbc12966` shows outdoor terrain,
the Erathia title and feedback that the fixture led the party there. This proves one
interior-to-region transition from a derived entry point, not access there from Emerald Island.

Return traversal was not established. The party was already two Dead and two Unconscious
members by the transition capture. T/G selected a distant out-of-sight chest rather than a
cave entrance. Capture `8c67f789-2c01-469c-9e44-af399d2a9812` shows the rest screen
explaining that sleep cannot restore the dead members. These observations do not establish
a defective return entrance or its absence. They also do not satisfy a multi-region sample.
The parent inspected both original captures. The session was stopped with `released: true`
and `browser_closed: true`; its private host port refused connections afterward.
