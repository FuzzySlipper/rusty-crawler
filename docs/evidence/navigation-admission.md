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
NativeAOT or the pending imported-interior browser reading. The operator's local
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
slot, and reported zero active sessions. Imported pursuit acceptance remains
open; these observations add no traversal or NativeAOT certification.
