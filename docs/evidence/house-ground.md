# Town house hours and camping ground

Recorded on 2 October 2026 using local CoreCLR product hosts. Source and gate identities live in Den.

House entrances use the ordinary service/residence placement and interaction path. Counter entrance hours
come from the canonical service definition; residence hours come from the building row on its placement.
The existing interpreter runs conditional house events. Equal source hours normalize to all day; overnight
windows use Kit OpeningHours. Closing is exclusive here, as for counters; the donor includes the exact
closing instant (`OpenEnroth/src/GUI/UI/UIHouses.cpp:304-333`).

Camping reads the normalized ground grid at the canonical party pose through Kit MapGrid. The rest owner
settles provisions and advances the same world clock. Place terrain remains the fallback when there is
no finer grid or the pose is outside it. The donor prices grass at 1, snow/swamp at 3, badlands at 4,
desert at 5 and other ground at 2 portions (`OpenEnroth/src/Engine/Data/TileEnumFunctions.cpp:92-110`).

The importer extends its existing tile decoder, whose tileset bases and folding already supply water
classification. Ground carries dtile.bin/map provenance and the pack source release/build. The grid has
127 by 127 squares at 512-unit pitch, origin(-32768,-32256), with source rows reversed to south-to-north
order. This preserves donor coordinate boundaries (`OutdoorTerrain.h:21-41`, `OutdoorTerrain.cpp:102-111`).
It does not change collision geometry, navigation or the automap raster.

## Operator import

A fresh operator write was byte-for-byte identical to its second write. All 13 outdoor regions carry
16,129 ground squares each, totaling 209,677. They retain 293 service/residence house entrances, 112 with
explicit source opening hours, counted on canonical service definitions or household placements.
Counter placements duplicate no hours; source rows without hours remain unrestricted. Ground palettes and cells
are emitted deterministically.

| Region | Counters | Residences | Explicit hours | Ground words |
| --- | ---: | ---: | ---: | --- |
| Emerald Island | 12 | 20 | 18 | dirt, grass, road, swamp, water |
| Harmondale | 17 | 17 | 18 | dirt, grass, road, snow, water |
| Erathia | 14 | 35 | 14 | dirt, grass, road, snow, water |
| The Tularean Forest | 15 | 23 | 15 | dirt, grass, road, water |
| Deyja | 7 | 20 | 7 | badlands, desert, dirt, road, water |
| The Bracada Desert | 8 | 15 | 8 | badlands, desert, dirt, road, water |
| Evenmorn Island | 3 | 3 | 3 | desert, dirt, grass, water |
| Mount Nighon | 7 | 13 | 7 | desert, dirt, road, snow, water |
| The Barrow Downs | 1 | 6 | 1 | dirt, grass, road, water |
| The Land of the Giants | 0 | 1 | 0 | badlands, dirt, road, snow, water |
| Tatalia | 11 | 22 | 11 | dirt, road, snow, swamp, water |
| Avlee | 10 | 13 | 10 | dirt, grass, road, water |
| Shoals | 0 | 0 | 0 | desert |

## Verification

The corrected full gate passed all builds, operator inventory/map decoding, deterministic pack writes,
CoreCLR staging, Architecture 21, Kit 749, Host 85, ruleset 321, importer 189 and UI 68 checks, with no
skips. Three source lanes approved the canonical-hours correction. Focused synthetic imports exercise
both timed and equal-hour counters through complete ruleset composition; imported ruleset checks cover
entrance refusal/reopening, overnight windows, shared counter policy, grid boundaries, malformed ground
and fallback terrain.

## Bounded live check

The successful observer session was `357b7f46-9032-49bc-8704-2e3d3009156f`. Its isolated Harmondale host
retained the actual imported geometry, ground, houses, events, fixtures and trigger relationships.
Explicit staging assists relocated Party Start from `(-16832,12512,372)` to `(768,7936,82)`, yaw512,
used the existing four-member scenario with 30 health each and 100 provisions, omitted 181 combat
placements and changed camp encounter chance from 10% to zero. These assists isolate camping prices
and entrance hours; they do not demonstrate normal creation, original population or encounter risk.

The installed Engine launch flag `--debugger` disabled the managed 30-second product-load deadline;
no debugger was attached and the broker retained its 180-second startup timeout. Browser navigation
began about 39.7 seconds after the lease, and the first neutral capture was about 49.0 seconds after it.
The observer established held, action-driven time after startup. The first reading was 09:04, with
canonical feet `(767.912,7936.088,81.972)`, grounded, and the four members at 120/120 combined health.
This is not a normal 30-second startup claim or an exact zero-time arrival capture.

Ordinary controls produced the following readings in the same region:

| Action | Clock and position | Result |
| --- | --- | --- |
| Visible Make camp on grass | 09:04 to 17:04, feet near `(768,7936,82)` | Eight hours, four members restored, operation cost 1 of 1 portions. |
| Ordinary W north, then Make camp on road | 17:05 to next day 01:05, feet `(767.912,8454.501,2.133)` | Grounded movement; eight hours, operation cost 2 of 2 portions. |
| Ordinary W and E toward the house keeper | 01:06, feet `(767.912,8684.915,2.133)`, yaw256 | Caldar selected at distance494.284; ordinary G refused `interaction-requirement-unmet`, naming hours06:00–18:00. |
| Visible Wait until dawn, then Wait an hour and ordinary G | 06:00, same pose | Conversation opened with Caldar; last use applied. |
| Ask to see the wares | 06:00 | Visible `Tempered Steel · Caldar`, `Weapon Shop · open (06:00–18:00)`, stock and training offers. |
| Leave the counter | 06:00 | The party left Tempered Steel; conversation and service screens closed. |

The camp costs are per operation, not a cumulative inventory reading. The clock advanced through the
canonical rest/wait owner. The shut-hours refusal is retained in the canonical observation receipt;
the original capture's viewport showed the upper rest panel, rather than the refusal below the fold.
The conversation and open shop were visible in their original captures. This demonstrates ordinary
access to the imported house through its keeper and service workflow, not passage through a rendered
exterior door or a separate interior map.

Original captures and sidecars remain under
`/home/agent/.local/state/crew-playtest-local/browser/357b7f46-9032-49bc-8704-2e3d3009156f/`:

| Reading | Original capture | Browser step / sequence |
| --- | --- | --- |
| Initial grounded party | `61dec4b7-0a6b-461f-961c-742d2fd28a59.png` | 564 / 562 |
| Grass camp | `c7f924ad-bed2-4481-9729-2030c876898e.png` | 576 / 569 |
| Road camp | `70c4c457-fd5d-4e9e-b699-42560a1b25b6.png` | 671 / 662 |
| Shut-hours use receipt | `331b4083-f5a9-4b27-b3b4-ec0195f3ba70.png` | 750 / 738 |
| Open conversation | `f1b084e3-4657-4285-866d-1e8c3324be88.png` | 786 / 759 |
| Open shop | `b23d564d-34f9-47d0-8b44-e1113aa7efc0.png` | 798 / 764 |
| Leave counter | `af8eaca4-970a-4156-91cd-d317b7f02eae.png` | 810 / 770 |

Canonical observe receipts are under the corresponding `receipts/<session>/` directory: initial
`6699a347`, grass `02d2aca8`, road `c7c58558`, refused use `56d2c96a`, open conversation `ca83d12e`,
shop `9354358d` and exit `e77c3a48`. The journal is `events.jsonl` in the browser directory. Capture
sidecars name held browser frames; query and screenshot correlation are separate observations.

An earlier lease failed the runtime startup deadline before gameplay. Another connected run began
below a steep terrain triangle and fell below every house target: its camp prices were visible, but
its no-target refusals could not establish house hours. It remains uncertain, not accepted house
proof. The parent corrected only the arrival after that lease was released; the successful run above
kept the same gameplay source, geometry and targets.

The observer stopped the successful host and browser, returning released=true. Its final pool read
at11:40:49 UTC and the parent's later read both found zero active leases and all ten slots free.
No observer source, profile, staging or save edits occurred. The panel remains over an empty world
frame. This reading makes no broad town traversal, rendered-world, NativeAOT or original-save claim.
