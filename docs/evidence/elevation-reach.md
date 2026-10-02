# Elevated use and unchanged reach

Recorded on 2 October 2026 with local CoreCLR product hosts. Source and gate identities live in Den.

The ruleset chooses an elevation-tolerant cone: acquisition is the forward hemisphere, with a further
0.11 radians for retention. Looking remains horizontal; imported target points and the 512-unit reach
are unchanged. This also widens horizontal acquisition. The installed Engine retains angular ranking,
cycling, hysteresis, visibility and fresh-use validation; the Kit supplies target observations, including
distant targets whose own reach still makes them unavailable to use.

For a forward target with vertical difference `h` and ground distance `d`, the acquisition band is
`|h| <= 512` and `0 <= d <= sqrt(512²-h²)`, subject to sight and availability. There is no elevation-dependent
minimum stand-off. The old `|h|/tan(0.20)` minimum and `512*sin(0.20)` elevation ceiling are removed.
The numbers and this closed form live beside the ruleset's interaction policy and in its README.
The reach is adopted from `OpenEnroth/src/Application/GameConfig.h:180-182`; the donor picks through its
rendered view (`src/Application/Game.cpp:1511-1515`, `src/Engine/Graphics/Vis.cpp:659-675`). This wider
cone is the product's adaptation for horizontal keyboard looking, not a donor-equivalence claim.

## Verification

The full gate passed all builds, operator inventory and map decoding, deterministic pack writes,
CoreCLR staging, Architecture 21, Kit 756, Host 85, ruleset 327, importer 189 and UI 68 checks, with no
skips. All three source review lanes approved. The earlier operator Ruleset run independently passed
327 cases with no skips. Native admission reports each cover all 76 imported geometries.

Focused Kit cases exercise forward targets above and below the party, directly above/below at the
reach boundary, outside the acquisition hemisphere, and an observed target just beyond its own reach.
Composed ruleset cases use the actual 512 reach and ordinary session use intent. A target above the
reach ceiling is refused as out of reach. These are mechanism and composition checks, not additional
live map traversals.

## Imported Manor use

Observer session `ebaf1e18-fb45-42f5-8462-067e1b0a23c5` retained the actual Manor geometry, doors, fixtures,
people, events, trigger relationships and all containers with their original centroid points, contents
and traps. Explicit staging assists relocated Party Start from `(0,-560,0)` to `(800,400,2)`, yaw 1536,
used the existing four-member scenario with 30 health each, 50,000 coins and 100 provisions, and omitted
five combat placements. No container position, pitch, geometry or trap was adjusted.

The installed Engine launch flag `--debugger` disabled its managed 30-second product-load deadline;
no debugger was attached. The broker retained its 180-second timeout. Held, action-driven time was
established after startup; the first grounded query was about 28 seconds after the lease, at game 09:09,
step 1177. This is not an exact zero-time arrival or normal 30-second startup certification.

The party settled on the actual floor at `(805.107,400,0)`, yaw 1536, with four healthy members. Ordinary
W reached the natural collision stop at `(807.250,295.133,0.030)`, still grounded. The unchanged chest
centroid was `(800,230,61)`: a vertical difference of about 60.97 units, with the live reticle naming
`A chest`, verb `search`, reason `ready`, at distance 89.511. That close floor pose is outside the former
narrow cone.

Ordinary G applied the chest use at that pose. The canonical result recorded state `sprung` and a
trap going off: Perception 1 against 10, four members taking 57 damage each. All four 30-HP members became
Dead, with combined health 0/120. The original upper panel visibly shows the four dead members; the
canonical receipt separately retains the applied outcome, state and exact trap message. This proves
the elevated target reached the ordinary use workflow. It does not claim disarming, searching the
remaining contents or receiving loot after the trap.

| Reading | Original capture identity | Browser step / sequence | Canonical observe receipt |
| --- | --- | --- | --- |
| Initial product before held setup | `f9d9d8e0-e8a5-47b7-bb30-941783eab936.png` | Not used as grounded proof | `d0a30041` is the later grounded query |
| Grounded close approach | `44de679f-d5d5-4376-85c5-76da4de66766.png` | 1915 / 1844 | `8e167189` |
| Applied use and dead party | `16094774-5ea1-4c2a-9a0f-182dd8828b7d.png` | 2207 / 2137 | `ba143997` |

Den retains exact operator paths, sidecars and journal. This published text carries the readings without
depending on ignored local artifacts. Canonical queries report frame correlation as not measured;
browser frame/step sidecars are separate observations. The observer stopped the owned host/browser,
returned released=true, and read a free ten-slot pool at 12:24:12 UTC. Post-trap movement did not obtain
a target beyond 512, so this session alone did not complete the separate refusal criterion.

## Reach refusal supplement

Observer session `3c0fe928-0354-4dd7-9427-3d8b6562d169` used the actual Harmondale terrain, targets,
events and trigger relationships. Staging relocated Party Start from `(-16832,12512,372)` to
`(768,7936,82)`, yaw 384, and omitted 181 combat placements. The same existing scenario party and
startup assist were used. No target point, reach, geometry or persisted save was changed.

The first grounded query was about 80 seconds after the lease, at game 09:17, step 2092. Held,
action-driven time had been established after startup. The party stood at
`(767.9117,7936.0884,81.9724)`, yaw 384, grounded with 120/120 health. Read-only Engine interaction
inspection found Caldar visible and available at distance **1110.6995**, with reach **512** and focus
reason `OutOfReach`. The yaw and pitch offsets were about -0.99 and -4.23 degrees. This is a genuine
aligned, visible target beyond reach; authored staging distance alone is not the proof.

After ordinary canvas focus, G refused use with code `interaction-out-of-reach` and the exact sentence
**“What the party faces is out of reach.”** The party's pose and health remained unchanged. Ordinary
DOM scrolling exposed the lower panel: position, grounded motion, facing out-of-reach, use refused,
and that sentence are visible in the original capture. No targeted-use assist was invoked.

| Reading | Original capture / receipt identity | Browser step / sequence |
| --- | --- | --- |
| Grounded party | Observe receipt `954c9584` | Query step 2092 |
| Visible distant Caldar, actual reach and reason | Interaction inspection receipt `663958c6` | Read-only inspection |
| Ordinary G refusal | Observe receipt `08034dc2` | Query step 2668 |
| Visible refusal sentence | `81081e62-cbfa-4c10-b5ca-478e004200f4.png` | 2668 / 1114 |

The observer released the owned host/browser and read all ten slots free at 12:33:10 UTC. Subsequent
pool readings may contain other products' leases; those are outside this mission's ownership. Together
with the Manor use, this supplement completes the bounded live criteria. It makes no counter-service,
save/resume or traversal claim.

## Limits and earlier failure

An earlier lease began at the parent's south-side arrival `(800,-350,2)`. Its first 200 ms step faulted
with unresolved controller penetration before gameplay; it remains an infrastructure error, with no
interaction proof. A read-only native mover probe reproduced that failure and showed a different
south-side approach blocked by the actual door 5. The north-room arrival above was admitted, and its
approach had clear sight; the parent corrected only the arrival after release and a fresh free-pool
reading. The probe prepared staging and is not substituted for ordinary live evidence.

The world frame remains empty behind the DOM panel. These bounded checks make no broad Manor or town
traversal, rendered-world, original-population, normal-creation, NativeAOT or original-save claim.
