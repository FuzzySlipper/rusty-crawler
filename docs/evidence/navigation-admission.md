# Collision-derived navigation admission

Point-in-time source and real Engine check on 2026-10-02. The importer wrote the
operator's own installation twice with identical bytes. Every emitted geometry
carried its collision vertex bounds as `navigationRegion`, beside the unchanged
Engine artifact, retaining the map file and geometry source counts.

| Imported geometry | With request metadata | With Engine-derived cells |
| --- | --- | --- |
| Interiors, sampled at 128 units | 63 | 63 |
| Regions, sampled at 512 units | 13 | 13 |
| Total | 76 | 76 |

No geometry was missing or refused. The real Engine published 699,840 walkable
cells in total. A prior 65,536-column budget excluded The Pit, The Walls of Mist
and The Titans' Stronghold. Their measured footprints were 83,600, 109,140 and
72,588 columns. The ruleset's separate derivation budget is now 131,072; the
final run had no navigation omission or refusal. The per-query pursuit budget
remains 1,024 cells. A future derivation budget refusal retains collision and
reports why pursuit holds; focused checks exercise that recoverable path.

`EnginePartyMover.Enter` admitted content, replaced the place's collision and
called `ReplaceCollisionNavigation` in that same spatial session. It supplied
the canonical ruleset controller: its body, slope and exact step, with maximum
drop from that step and support snaps scaled independently from the Engine
defaults. The final whole-install check took about 56.1 seconds of aggregate
admission time; the slowest entry, Harmondale, took about 6.1 seconds. These
include content and collision admission and are not isolated navigation timings
or a benchmark. Barrow X published 1,450 cells in about 128 milliseconds.

The headless check used the package's safe `EngineTestHost` through ordinary
test-project support. An authored floor with an impassable wall additionally
exercised the canonical party and creature movers with the actual ruleset body:
the creature went past the wall end and reached the target, and the party still
stepped in the same collision scene. This is a mechanism check, not an imported
interior live pursuit reading. Both real Engine checks passed.

Queries take feet, while character steps retain body centres. A refused route
holds by name and can be retried after the target moves. Initial placement still
uses a stationary canonical character step and the existing ray-and-settle
path, including when navigation is absent. Backing away retains its existing
character-step behavior. No product rasterizer, pathfinder or second spatial
scene was introduced.

Sampling is ours and approximate, especially the 512-unit outdoor grid. These
counts do not certify every narrow passage, every pursuit, ordinary traversal,
NativeAOT or broad traversal. The operator's local
gate evidence retains the per-place admission report and original logs; no game
data is published here.

## Imported interior observation

Owned local browser session `edcfbca8-8828-44ff-8399-5856ed36094a` ran Barrow X
with its original geometry and placements and a parent-staged healthy party.
Ordinary movement met a wall and could follow clear detours. Actor 19, a rat,
changed from waiting at about 876 units to closing at 527 and then 453 units.
Those distance readings do not locate the rat's route or establish that it went
around the wall. The product currently presents a DOM panel over an empty world
frame; this reading therefore remains uncertain for imported around-wall pursuit.

Original capture reconciliation:

| Broker capture | Original browser artifact | Reading |
| --- | --- | --- |
| `cec59d1a-8b11-47ef-b952-b68106a3e40b` | `55a25462-1a6f-4bec-8207-ac8506b7b646` | Step 3973, earlier observation |
| `1268fb3c-8221-4b70-9607-247088db8b90` | `50ee9507-92c1-4dc5-8ae5-464b244113d1` | Subsequent observation |
| `978fb498-53e5-4923-886c-20898ca76c6a` | `ea85cf71-249c-4263-ab4e-8143ca92f8bc` | Subsequent observation |

The later party pose `(1498.85, -110.55, 80.53)` and blocked-wall reading exist
in the original observation receipt `20261002T052433.727-observe-1aa1ebe5.json`;
they must not be attributed to the earlier step-3973 capture. Originals and
sidecars remain under the operator's local browser and receipt directories for
this session. The observer stopped its owned host and browser, released its
slot, and reported zero active sessions. This early reading remained uncertain; the controlled follow-up below supplies
the missing route evidence. These observations add no traversal or NativeAOT certification.


## Controlled imported pursuit

A later owned local browser session, `bd4567a2-02a2-4737-842e-445fc6c6189e`,
used the unchanged imported collision and navigation geometry of Lord Markham's
Manor. Staging moved Party Start to `(-1344, -448, 0)` and placed one actual
imported Giant Rat encounter at the Manor's original encounter position
`(-896, 160, 0)`, with explicit grade A, one creature and zero spread. Other
population placements were omitted for this bounded check. The monster definition
was unchanged. This is assisted encounter staging, not unmodified population or
ordinary traversal evidence.

The observer selected Engine action-driven time while the normal creation screen
was still open, then accepted its default four members through the panel. The
acceptance needed one 200-millisecond admission before a live world existed. Its
first live observation is consequently at 0.183 simulation seconds, rather than
at the exact authored spawn. Every subsequent 200-millisecond advance was followed
by an actual product query and an original screenshot. The party remained at its
start position with 120/120 combined hit points throughout.

| Simulation seconds | Rat feet, place coordinates | Distance to stationary party | Reading |
| --- | --- | --- | --- |
| 0.183 | (-872.004, 121.257, 0.107) | 742.063 | First live observation, still occluded |
| 1.183 | (-836.898, -125.118, 2.087) | 603.159 | Moving along the corridor, still occluded |
| 1.783 | (-833.353, -275.070, 2.087) | 540.458 | Last sampled occluded position |
| 1.983 | (-832.962, -325.068, 2.087) | 526.579 | Clear of the wall end |
| 2.383 | (-866.361, -403.624, 2.114) | 483.617 | First sampled arrival within the existing 512-unit melee reach |
| 2.583 | (-912.947, -420.487, 2.131) | 436.084 | Short continuation toward the party |

The coordinates are the actual creature entity's pose, exposed through the existing
combat observation, rather than positions inferred from distance. A separate safe
Engine test-host check evaluated line of sight from each captured position to the
party against the same imported geometry: the first nine samples were occluded,
the remaining four visible. This is offline geometric corroboration of the live
route, not live line-of-sight telemetry. The rat walked along the corridor and
turned toward the party only after clearing the wall, with no product pathfinder
or alternate collision scene.

Original artifacts remain in the operator's local browser and receipt directories
under that session ID. Key observation receipts are
`20261002T070449.807-observe-516476f4.json` (initial),
`20261002T070542.797-observe-a361de1d.json` (corridor),
`20261002T070631.724-observe-cea586b8.json` (arrival), and
`20261002T070641.512-observe-ad4cdc73.json` (continuation). The arrival screenshot
`7af68b03-1ced-43da-b8e4-1eaa7e794c92.png` shows the rat closing at rounded distance
484, with every member at full health. The creation, initial and continuation
originals are `47d083d9-1779-44a0-87c0-389aa60da845.png`,
`613a64b8-e36f-46ba-96ff-d0b40b7b9635.png`, and
`4ece9810-67fe-4d45-8928-513a6d273c3e.png`. These identify separate observations and
captures; they do not assert a measured render-frame correlation.

Two earlier Manor attempts are retained separately. One omitted the encounter's
explicit grade and produced no creature. In the other, the first observation was
already inside melee reach after startup; its stationary rat did not establish a
navigation stall. The creation-mode follow-up captured the missing pursuit.

Verdict: passed for this bounded imported-interior around-wall pursuit and arrival.
The full source verification and whole-install native admission also passed. The
observer stopped its owned browser and host and confirmed the pool was empty.
No graphical world rendering, exact zero-step spawn, full combat encounter,
all-passage coverage, broad traversal or NativeAOT certification is claimed.
